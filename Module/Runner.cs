using TaskingSolutions.Data.DataAccess;
using TaskingSolutions.Data.Entities;
using TaskingSolutions.Interfaces;
using TaskingSolutions.Module.System_Jobs;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskingSolutions.Logging;
using System.Collections.Generic;

namespace TaskingSolutions.Module
{
    public class Runner : MarshalByRefObject, IDisposable
    {

        private const int _timerPollingInterval = 1000 * 60; // 60 seconds

        private FileLogger _logger;
        private Timer _checkJobsTimer;
        private DataAccessFactory _dataAccess;
        private EventWaitHandle _initGate;


        private readonly object _runningTaskLock = new object();
        private readonly Dictionary<Task, Job> _runningTasks = new Dictionary<Task, Job>();
        private readonly HashSet<int> _jobsBlockedFromRunning = new HashSet<int>();


        private void CheckJobsTimerTick(object state)
        {
            _logger.LogDebug("Runner.CheckJobsTimerTick - enter");

            try
            {
                if (_checkJobsTimer != null)
                {
                    _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    var dueJobs = _dataAccess.GetJobSchedulesAccessor().GetDueSchedules();

                    foreach (var dueJob in dueJobs) // already ordered by priority and trigger time in sql
                    {
                        bool isBlocked;
                        lock (_runningTaskLock)
                            isBlocked = _jobsBlockedFromRunning.Contains(dueJob.Job.Id);

                        if (!isBlocked)
                        {
                            if (dueJob.Job.CanRunConcurrent)
                                StartJob(dueJob);

                            else // cannot run concurrent. wait for all running tasks to finish before starting this single task. 
                            {       // the check jobs timer won't be reenabled to check for more until this single task completes
                                Task[] tasks;
                                lock (_runningTaskLock)
                                    tasks = _runningTasks.Keys.ToArray();

                                Task.WaitAll(tasks);
                                var task = StartJob(dueJob);
                                task.ContinueWith(x => _checkJobsTimer?.Change(_timerPollingInterval, Timeout.Infinite));

                                _logger.LogDebug("Runner.CheckJobsTimerTick - exit");
                                return;
                            }
                        }
                    }

                    _checkJobsTimer.Change(_timerPollingInterval, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                _dataAccess.GetErrorLogsAccessor().LogException(ex);
                // todo: this is a critical method... send notification?
                _checkJobsTimer.Change(_timerPollingInterval, Timeout.Infinite);
            }

            _logger.LogDebug("Runner.CheckJobsTimerTick - exit");
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
            _logger.LogDebug("Runner.Start - enter");
            void init()
            {
                _logger.LogDebug("Runner.Start.init - enter");
                try
                {
                    JobRunnerInitializer initializer = new JobRunnerInitializer();
                    initializer.WorkFolderPath = workFolderPath;
                    initializer.Start(null);

                    // flag all uncompleted job runs as errors! dashboard will provide opportunity to reschedule
                    var jobRunsAccessor = _dataAccess.GetJobRunsAccessor();
                    var uncompletedRuns = jobRunsAccessor.GetAllUncompleted();
                    foreach (var record in uncompletedRuns)
                    {
                        record.EndTime = DateTime.UtcNow;
                        record.IsErrored = true;
                        record.Error = "Job Run was aborted unexpectedly";
                    }
                    jobRunsAccessor.Update(uncompletedRuns);


                    _initGate.Set();
                    _checkJobsTimer.Change(0, Timeout.Infinite);
                    _logger.LogDebug("Runner.Start.init - exit");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Runner.Start.init - error", ex);
                    _dataAccess.GetErrorLogsAccessor().LogException(ex);
                    Stop();
                }
            }

            new Task(init).Start();
            _logger.LogDebug("Runner.Start - exit");
        }

        public void Stop()
        {
            _initGate.WaitOne();

            _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _checkJobsTimer = null;

            // disable timer
            // check running job metadata for stop action (wait or abort)
            //  use previous job data to deterimine if too long running for wait


            this.Stopped?.Invoke(null, EventArgs.Empty);
        }

        public void ShutDown()
        {
            _initGate.WaitOne();

            // call same logic as stop, but with shorter wait time
        }

        public void Dispose()
        {
            // same logic as stop
        }

        public void StopAfterCurrentJobsFinish()
        {
            _initGate.WaitOne();
            _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _checkJobsTimer = null;

            //Task.WaitAll()


            // check running job metadata for stop action (wait or abort)
            //  use previous job data to deterimine if too long running for wait

            this.Stopped?.Invoke(null, EventArgs.Empty);
        }


        private Task StartJob(ScheduleAndJob startData)
        {
            lock (_runningTaskLock)
            {
                var task = Task.Factory.StartNew(StartJobThreaded, startData);
                var cleanupTask = task.ContinueWith(JobEnded);

                _runningTasks.Add(task, startData.Job);
                if (!startData.Job.QueueMultipleInstances)
                    _jobsBlockedFromRunning.Add(startData.Job.Id);
                return task;
            }
        }

        private void JobEnded(Task task)
        {
            lock (_runningTaskLock)
            {
                var job = _runningTasks[task];
                _runningTasks.Remove(task);
                if (!job.QueueMultipleInstances)
                    _jobsBlockedFromRunning.Remove(job.Id);
            }
        }

        private void StartJobThreaded(object input)
        {
            IDataAccessFactory factory = new DataAccessFactory();
            // todo: static constructor on factory? with con str?

            try
            {
                ScheduleAndJob data = (ScheduleAndJob)input;
                SetNextTriggerDate(data.JobSchedule);

                // take job schedule, calc next, new jobrun record & save
                JobRun jobRun = new JobRun();
                jobRun.JobId = data.Job.Id;
                jobRun.StartTime = DateTime.UtcNow;

                var jobrunsAccessor = factory.GetJobRunsAccessor();
                jobrunsAccessor.SaveJobTriggered(data.JobSchedule, jobRun);


                IJob jobInstance = (IJob)Activator.CreateInstance(Type.GetType(data.Job.DotNetType));
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

                    return;
                }

                // mark job run completed
                jobRun.EndTime = DateTime.UtcNow;
                jobrunsAccessor.Update(jobRun);
            }
            catch (Exception ex)
            {
                factory.GetErrorLogsAccessor().LogException(ex);
            }
        }

        private void SetNextTriggerDate(JobSchedule schedule)
        {
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






        /*
         * 
         * in order for the file system watcher to be effective, i'll need to copy the dll files into another working directory so they arent locked while running the program.
         * 
         * in order to swap the dlls out at runtime, they need loaded in another appdomain
         * 
         * so, the bulk of this program needs built in a separate assembly that can be loaded in another appdomain, but this assembly has to control the filesystemwatcher
         *      in order to be able to unload the old dlls at runtime and reload the new
         * 
         * communication between appdomains can be done via wcf named pipes or a MarshalByRefObject instantiated in the child appdomain and passed back to this one
         * 
         * 
         * domain responsibilities:
         *  service domain:
         *      filesystemwatcher & copy from drop directory to working
         *      manage child domain
         *      
         *  child domain:
         *      load dlls from working directory
         *      database
         *      scheduler
         *      runner
         * 
         *  unknown:
         *      field api requests
         * 
         * */






        // new thread / timer?
        // initial run:
        //  start filesystemwatcher
        //  check assemblies for jobs
        //      cross-reference jobs db
        //      index jobs by next trigger time
        //      set timer for next trigger

        // catalog jobs
        //  cross-reference db
        //  update db with new (un-cache missing, but dont change db settings)

        // queue a job
        //  check
        //      already active job
        //      priority, multiplexing, etc
        //  if ok, run job on new thread
        //      need to track exceptions in the thread, i need to own the job spinup code on the other thread
        //      use prior stats


        // on timer tick
        //  add item to queue
        //      queue triggers runner-manager thread
        //          runner-manager
        //  eval next trigger time & update db
        //      get next trigger time overall and set timer

        // on file watcher trigger
        //  disable timer
        //  catalog jobs
        //  re-enable timer


    }
}
