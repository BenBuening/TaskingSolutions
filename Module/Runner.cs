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

        private const int _timerPollingInterval = 1000 * 60; // 60 seconds
        private const int _stopWaitTimeout = 1000 * 60 * 2; // 2 minute
        private const int _shutdownWaitTimeout = 1000 * 20; // 20 seconds

        private FileLogger _logger;
        private Timer _checkJobsTimer;
        private DataAccessFactory _dataAccess;
        private EventWaitHandle _initGate;
        private Dictionary<string, Type> _jobTypesIndex;

        private readonly object _checkJobsLock = new object();
        private Task _checkJobsTask;

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
                if (Path.GetFileName(filePath) != "JobRunnerInterfaces.dll")
                {
                    Assembly loaded = Assembly.LoadFrom(filePath);
                    // apparently this style loading does not register all these types so they can be retrieved with type.gettype without having to manually intervene anyway... 

                    // so, since that doesn't just work, i'm going to index all the jobs as they're loaded and look them up that way.
                    var jobTypes = loaded.GetTypes().Where(x => x.IsClass && !x.IsAbstract && ijobType.IsAssignableFrom(x) && !x.IsDefined(sysJobType));
                    foreach (var jobType in jobTypes)
                        jobTypesIndex.Add(jobType.FullName, jobType);
                }
            _jobTypesIndex = jobTypesIndex;
        }

        private void InitFlagUncompletedJobRuns()
        {
            // flag all uncompleted job runs as errors! dashboard will provide opportunity to reschedule
            var jobRunsAccessor = _dataAccess.GetJobRunsAccessor();
            var uncompletedRuns = jobRunsAccessor.GetAllUncompleted();
            foreach (var record in uncompletedRuns)
            {
                record.EndTime = DateTime.UtcNow;
                record.IsErrored = true;
                record.Error = "Job Runner service was aborted unexpectedly";
            }
            jobRunsAccessor.Update(uncompletedRuns);

            // todo: send emails about failed jobs?
        }

        private void CheckJobsTimerTick(object state)
        {
            _logger.LogDebug("Runner.CheckJobsTimerTick - enter");

            _checkJobsTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.LogDebug("Runner.CheckJobsTimerTick - check lock");
            lock (_checkJobsLock)
            {
                _logger.LogDebug("Runner.CheckJobsTimerTick - enter lock");
                _checkJobsTask = Task.Factory.StartNew(CheckJobs);
            }

            _logger.LogDebug("Runner.CheckJobsTimerTick - exit");
        }

        private void CheckJobs()
        {
            _logger.LogDebug("Runner.CheckJobs - enter");

            try
            {
                var debugAccessor = _dataAccess.GetDebugLogsAccessor();
                var scheduleAccessor = _dataAccess.GetJobSchedulesAccessor();
                var dueJobs = scheduleAccessor.GetDueSchedules();

                foreach (var dueJob in dueJobs) // already ordered by priority and trigger time in sql
                {
                    bool isBlocked;
                    lock (_runningTaskLock)
                        isBlocked = _jobsBlockedFromRunning.Contains(dueJob.Job.Id);

                    if (isBlocked)
                    {
                        if (!dueJob.Job.QueueMultipleInstances)
                            AdjustScheduleForPassedTriggers(dueJob.JobSchedule, dueJob.Job);
                    }
                    else
                    {
                        if (dueJob.Job.CanRunConcurrent)
                            StartJobThread(dueJob);

                        else // run concurrent not allowed. wait for all running tasks to finish before starting this single task. 
                        {       // the check jobs timer won't be reenabled to check for more until this single task completes
                            Task[] tasks;
                            _logger.LogDebug("Runner.CheckJobs - check lock");
                            lock (_runningTaskLock)
                            {
                                _logger.LogDebug("Runner.CheckJobs - enter lock");
                                tasks = _runningTasks.Keys.ToArray();
                            }

                            Task.WaitAll(tasks);
                            var task = StartJobThread(dueJob);
                            task.ContinueWith(x => _checkJobsTimer?.Change(_timerPollingInterval, Timeout.Infinite));

                            _logger.LogDebug("Runner.CheckJobs - exit");
                            return;
                        }
                    }
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

        private Task StartJobThread(ScheduleAndJob startData)
        {
            _logger.LogDebug("Runner.StartJobThread - enter");

            lock (_runningTaskLock)
            {
                var task = new Task(StartJob, startData);
                var cleanupTask = task.ContinueWith(JobEnded);

                _runningTasks.Add(task, startData.Job);
                if (!startData.Job.QueueMultipleInstances)
                    _jobsBlockedFromRunning.Add(startData.Job.Id);

                task.Start();
                _logger.LogDebug("Runner.StartJobThread - exit");
                return task;
            }
        }

        private void StartJob(object input)
        {
            _logger.LogDebug("Runner.StartJob - enter");

            IDataAccessFactory factory = new DataAccessFactory();

            try
            {
                ScheduleAndJob data = (ScheduleAndJob)input;
                SetNextTriggerDate(data.JobSchedule);

                // take job schedule, calc next, new jobrun record & save
                JobRun jobRun = new JobRun();
                jobRun.JobId = data.Job.Id;
                jobRun.StartTime = DateTime.UtcNow;

                if (!data.Job.QueueMultipleInstances)
                    AdjustScheduleForPassedTriggers(data.JobSchedule, data.Job);


                var jobrunsAccessor = factory.GetJobRunsAccessor();
                jobrunsAccessor.SaveJobTriggered(data.JobSchedule, jobRun);


                IJob jobInstance = (IJob)Activator.CreateInstance(_jobTypesIndex[data.Job.DotNetType]);
                try
                {
                    jobInstance.Start(jobRun.StartTime, new SystemServices(factory, jobRun.Id));
                }
                catch (Exception ex)
                {
                    jobRun.EndTime = DateTime.UtcNow;
                    jobRun.IsErrored = true;
                    jobRun.Error = ex.ToString();
                    jobrunsAccessor.Update(jobRun);

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
                if (!job.QueueMultipleInstances)
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

            switch (schedule.RecurrenceType)
            {
                case Data.RecurranceType.Daily:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddDays(1.0);
                    break;

                case Data.RecurranceType.Weekly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddDays(7.0);
                    break;

                case Data.RecurranceType.Monthly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddMonths(1);
                    break;

                case Data.RecurranceType.Hourly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddHours(1.0);
                    break;

                case Data.RecurranceType.Minutely:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddMinutes(1.0);
                    break;

                case Data.RecurranceType.Yearly:
                    schedule.NextTriggerTime = schedule.NextTriggerTime.Value.AddYears(1);
                    break;

                case Data.RecurranceType.None:
                default:
                    schedule.NextTriggerTime = null;
                    break;
            }

            if (schedule.RecurUntil.HasValue && schedule.NextTriggerTime.HasValue && schedule.NextTriggerTime.Value >= schedule.RecurUntil.Value)
                schedule.NextTriggerTime = null;
        }

        private Assembly AssemblyResolver(AssemblyName asmn)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == asmn.FullName);
        }

        private Type TypeResolver(Assembly assembly, string typeName, bool b)
        {
            return assembly.GetType(typeName);
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
                _logger.LogDebug($"{jobRunsSkipped} job runs skipped for job id = {job.Id} as there was one already queued or running");
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
