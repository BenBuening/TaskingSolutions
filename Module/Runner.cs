using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TaskingSolutions.Data.DataAccess;
using TaskingSolutions.Data.Entities;
using TaskingSolutions.Interfaces;
using TaskingSolutions.Logging;
using TaskingSolutions.Module.System_Jobs;

namespace TaskingSolutions.Module
{
    public class Runner : MarshalByRefObject
    {

        private class JobStartInfo
        {
            public Job Job { get; set; }
            public DateTime TriggerTime { get; set; }
        }


        private const int _timerPollingInterval = 1000 * 60; // 60 seconds
        private const int _stopWaitTimeout = 1000 * 60 * 2; // 2 minute
        private const int _shutdownWaitTimeout = 1000 * 20; // 20 seconds

        private readonly FileLogger _logger;
        private Timer _checkJobsTimer;
        private readonly DataAccessFactory _dataAccess;
        private readonly EventWaitHandle _initGate;
        private Dictionary<string, Type> _jobTypesIndex;

        private readonly object _runningTaskLock = new object();
        private readonly Dictionary<Task, Job> _runningTasks = new Dictionary<Task, Job>();
        private readonly HashSet<int> _jobsBlockedFromRunning = new HashSet<int>();


        private void Initialize(object state)
        {
            _logger.LogDebug("Runner.Start.init - enter");
            try
            {
                string workFolderPath = (string)state;

                ValidateLicense();
                InitDynamicAssemblies(workFolderPath);
                new JobReconciler().Start();
                InitFlagUncompletedJobRuns();

                _checkJobsTimer?.Change(0, Timeout.Infinite);
                _logger.LogDebug("Runner.Start.init - exit");
            }
            catch (Exception ex)
            {
                _logger.LogError("Runner.Start.init - error", ex);
                Stop();
            }
            finally
            {
                _initGate.Set();
            }
        }

        private void ValidateLicense()
        {

            /*
            * 
            * installer calls home & verifies license. calls home with machine unique id & returns hash of machine id and key
            * on startup, validate hash
            * 
            * 
            * */
        }

        private void InitDynamicAssemblies(string workFolderPath)
        {
            if (string.IsNullOrEmpty(workFolderPath))
                throw new ArgumentException("WorkFolderPath not set", nameof(workFolderPath));

            Type ijobType = typeof(IJob);
            Type sysJobType = typeof(SystemJobAttribute);
            Dictionary<string, Type> jobTypesIndex = new Dictionary<string, Type>();

            foreach (var filePath in Directory.EnumerateFiles(workFolderPath, "*.dll", SearchOption.TopDirectoryOnly))
                if (Path.GetFileName(filePath) != "TaskingSolutions.Interfaces.dll")
                {
                    Assembly loaded = Assembly.LoadFrom(filePath);
                    // apparently this style loading does not register all these types so they can be retrieved with type.gettype without having to manually intervene anyway... 

                    // so, since that doesn't just work, i'm going to index all the jobs as they're loaded and look them up that way.
                    List<Type> jobTypes = new List<Type>();
                    foreach (var type in loaded.GetTypes())
                    {
                        bool isClass = type.IsClass;
                        bool isNotAbstract = !type.IsAbstract;
                        bool isIJob = ijobType.IsAssignableFrom(type);
                        bool isNotSysJob = !type.IsDefined(sysJobType);
                        if (isClass && isNotAbstract && isIJob && isNotSysJob)
                            jobTypes.Add(type);
                    }
                    //var jobTypes = loaded.GetTypes().Where(x => x.IsClass && !x.IsAbstract && ijobType.IsAssignableFrom(x) && !x.IsDefined(sysJobType)).ToList();
                    foreach (var jobType in jobTypes)
                        jobTypesIndex.Add(jobType.FullName, jobType);
                }
            _jobTypesIndex = jobTypesIndex;
        }

        private void InitFlagUncompletedJobRuns()
        {
            // flag all uncompleted job runs as errors! dashboard will provide opportunity to reschedule
            var jobRunErrorsAccessor = _dataAccess.GetJobRunErrorLogsAccessor();
            var uncompletedRuns = _dataAccess.GetJobRunsAccessor().GetAllUncompleted();
            foreach (var record in uncompletedRuns)
                jobRunErrorsAccessor.LogException(record.Id, "The Job Runner service was terminated before this job could finish. Please verify your data integrity.");

            // todo: send emails about failed jobs?
        }

        private void CheckJobsTimerTick(object state)
        {
            _logger.LogDebug("Runner.CheckJobsTimerTick - enter");

            _checkJobsTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            Task.Factory.StartNew(CheckJobs);

            _logger.LogDebug("Runner.CheckJobsTimerTick - exit");
        }

        private void CheckJobs()
        {
            _logger.LogDebug("Runner.CheckJobs - enter");

            try
            {
                var debugAccessor = _dataAccess.GetSystemLogsAccessor();
                var scheduleAccessor = _dataAccess.GetJobSchedulesAccessor();
                var dueJob = scheduleAccessor.GetNextDueSchedule();

                while (dueJob != null)
                {
                    _logger.LogDebug($"Runner.CheckJobs - job found ({dueJob.Job.Name})");

                    bool isBlocked;
                    lock (_runningTaskLock)
                        isBlocked = _jobsBlockedFromRunning.Contains(dueJob.Job.Id);

                    if (dueJob.Job.AllowSimultaneousInstances || !isBlocked)
                    {
                        var jobTask = new Task(StartJob, new JobStartInfo() { Job = dueJob.Job, TriggerTime = dueJob.JobSchedule.NextTriggerTime.Value });
                        var endTask = jobTask.ContinueWith(JobEnded);

                        if (dueJob.Job.QueueMultipleInstances)
                            SetNextTriggerDate(dueJob.JobSchedule);
                        else
                            AdjustScheduleForPassedTriggers(dueJob.JobSchedule, dueJob.Job);
                        scheduleAccessor.Update(dueJob.JobSchedule);

                        if (dueJob.Job.CanRunConcurrentlyWithOtherJobs)
                        {
                            lock (_runningTaskLock)
                            {
                                _jobsBlockedFromRunning.Add(dueJob.Job.Id);
                                _runningTasks.Add(jobTask, dueJob.Job);

                                jobTask.Start();
                            }
                        }
                        else
                        {
                            endTask.ContinueWith(x => _checkJobsTimer?.Change(0, Timeout.Infinite));

                            Task[] tasks;
                            lock (_runningTaskLock)
                                tasks = _runningTasks.Keys.ToArray();
                            Task.WaitAll(tasks);

                            lock (_runningTaskLock)
                            {
                                _jobsBlockedFromRunning.Add(dueJob.Job.Id);
                                _runningTasks.Add(jobTask, dueJob.Job);

                                jobTask.Start();
                            }
                            return; // without restarting check timer. that will happen on task end
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"Runner.CheckJobs - job was blocked ({dueJob.Job.Name})");
                        AdjustScheduleForPassedTriggers(dueJob.JobSchedule, dueJob.Job);
                        scheduleAccessor.Update(dueJob.JobSchedule);
                    }

                    dueJob = scheduleAccessor.GetNextDueSchedule();
                }

                _checkJobsTimer?.Change(_timerPollingInterval, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                // todo: this is a critical method... send notification?
                _checkJobsTimer?.Change(_timerPollingInterval, Timeout.Infinite);
            }

            _logger.LogDebug("Runner.CheckJobs - exit");
        }

        private void StartJob(object input)
        {
            _logger.LogDebug("Runner.StartJob - enter");

            IDataAccessFactory factory = new DataAccessFactory();
            var jobrunsAccessor = factory.GetJobRunsAccessor();

            try
            {
                JobStartInfo data = (JobStartInfo)input;

                // take job schedule, calc next, new jobrun record & save
                JobRun jobRun = new JobRun();
                jobRun.JobId = data.Job.Id;
                jobRun.TriggerTime = data.TriggerTime;
                jobRun.StartTime = DateTime.UtcNow;
                jobrunsAccessor.Insert(jobRun);


                IJob jobInstance = (IJob)Activator.CreateInstance(_jobTypesIndex[data.Job.DotNetType]);
                try
                {
                    jobInstance.Start(jobRun.StartTime, new SystemServices(factory, jobRun.Id));
                }
                catch (Exception ex)
                {
                    jobRun.EndTime = DateTime.UtcNow;
                    jobrunsAccessor.SaveJobErrored(jobRun, ex);

                    // todo: send error email

                    _logger.LogDebug("Runner.StartJob - exit");
                    return;
                }

                // mark job run completed
                jobRun.EndTime = DateTime.UtcNow;
                jobrunsAccessor.Update(jobRun);
            }
            catch (Exception ex)
            {
                _logger.LogError("Runner.StartJob - exception", ex);
            }

            _logger.LogDebug("Runner.StartJob - exit");
        }

        private void JobEnded(Task task)
        {
            _logger.LogDebug("Runner.JobEnded - enter");

            lock (_runningTaskLock)
            {
                var job = _runningTasks[task];
                _runningTasks.Remove(task);
                _jobsBlockedFromRunning.Remove(job.Id);
            }

            _logger.LogDebug("Runner.JobEnded - exit");
        }

        private void SetNextTriggerDate(JobSchedule schedule)
        {
            if (!schedule.NextTriggerTime.HasValue)
                return;

            schedule.TimesTriggered += 1;
            if (schedule.TimesToRecur.HasValue && schedule.TimesTriggered > schedule.TimesToRecur.Value)
            {
                schedule.NextTriggerTime = null;
                return;
            }
            
            if (!schedule.RecurrenceInterval.HasValue)
            {
                schedule.NextTriggerTime = null;
                return;
            }

            switch (schedule.RecurrenceType)
            {
                case Data.RecurranceType.Daily:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddDays(schedule.RecurrenceInterval.Value);
                    break;

                case Data.RecurranceType.Weekly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddDays(7.0 * schedule.RecurrenceInterval.Value);
                    break;

                case Data.RecurranceType.Monthly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddMonths(schedule.RecurrenceInterval.Value);
                    break;

                case Data.RecurranceType.Hourly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddHours(schedule.RecurrenceInterval.Value);
                    break;

                case Data.RecurranceType.Minutely:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddMinutes(schedule.RecurrenceInterval.Value);
                    break;

                case Data.RecurranceType.Yearly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddYears(schedule.RecurrenceInterval.Value);
                    break;

                case Data.RecurranceType.None:
                default:
                    schedule.NextTriggerTime = null;
                    break;
            }

            if (schedule.RecurUntil.HasValue && schedule.NextTriggerTime.HasValue && schedule.NextTriggerTime.Value >= schedule.RecurUntil.Value)
                schedule.NextTriggerTime = null;
        }

        private void AdjustScheduleForPassedTriggers(JobSchedule schedule, Job job)
        {
            int jobRunsSkipped = 0;
            while (schedule.NextTriggerTime.HasValue && schedule.NextTriggerTime.Value < DateTime.UtcNow)
            {
                jobRunsSkipped++;
                SetNextTriggerDate(schedule);
            }
            if (jobRunsSkipped > 0)
            {
                _dataAccess.GetJobSchedulesAccessor().Update(schedule);
                _logger.LogDebug($"{jobRunsSkipped} job runs skipped for job id = {job.Id} as there was one already running");
            }
        }



        public event EventHandler Stopped;


        public Runner()
        {
            _logger = new FileLogger(@"c:\_temp\JobRunnerModuleLog.txt");
            _dataAccess = new DataAccessFactory();
            _checkJobsTimer = new Timer(CheckJobsTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            _initGate = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void Start(string workFolderPath)
        {
            new Task(Initialize, workFolderPath).Start();
        }

        public void Stop()
        {
            _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _checkJobsTimer = null;

            _initGate.WaitOne();

            // check running job metadata for stop action
            Task[] tasks;
            lock (_runningTaskLock)
                tasks = _runningTasks.Keys.ToArray();
            Task.WaitAll(tasks.ToArray(), _stopWaitTimeout);

            // future enhancement:
            //  use previous job runs data to deterimine if too long running for wait

            this.Stopped?.Invoke(null, EventArgs.Empty);
        }

        public void ShutDown()
        {
            _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _checkJobsTimer = null;

            _initGate.WaitOne();

            // check running job metadata for stop action
            Task[] tasks;
            lock (_runningTaskLock)
                tasks = _runningTasks.Keys.ToArray();
            Task.WaitAll(tasks.ToArray(), _shutdownWaitTimeout);

            // future enhancement:
            //  use previous job runs data to deterimine if too long running for wait

            this.Stopped?.Invoke(null, EventArgs.Empty);
        }

        public void StopAfterCurrentJobsFinish()
        {
            _logger.LogDebug("Runner.StopAfterCurrentJobsFinish - exit");

            _initGate.WaitOne();

            Task[] tasks;
            _logger.LogDebug("Runner.StopAfterCurrentJobsFinish - check lock");
            lock (_runningTaskLock)
            {
                _logger.LogDebug("Runner.StopAfterCurrentJobsFinish - enter lock");
                _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _checkJobsTimer = null;
                tasks = _runningTasks.Keys.ToArray();
            }
            Task.WaitAll(tasks);

            this.Stopped?.Invoke(null, EventArgs.Empty);

            _logger.LogDebug("Runner.StopAfterCurrentJobsFinish - exit");
        }

    }
}
