using JobRunner.Data.DataAccess;
using JobRunner.Data.Entities;
using JobRunner.Interfaces;
using JobRunnerModule.System_Jobs;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace JobRunnerModule
{
    public class Runner : MarshalByRefObject, IDisposable
    {

        private const int _timerPollingInterval = 1000 * 60;

        private Timer _checkJobsTimer;
        private DataAccessFactory _dataAccess;



        private Job SystemJob(string name)
        {
            var job = new Job();
            job.Name = name;
            job.IsSystemJob = true;
            job.CanRunConcurrent = false;
            job.JobQueuePriority = JobQueuePriority.High;

            return job;
        }

        private void CheckJobsTimerTick(object state)
        {
            if (_checkJobsTimer != null)
            {
                _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);


                // check db for newly triggered jobs
                //  update db for those jobs with new trigger times (trigger missed)

                // check db for next job to start. consider priority, threadedness, trigger time



                _checkJobsTimer.Change(_timerPollingInterval, Timeout.Infinite);
            }
        }





        public event EventHandler Stopped;


        public Runner()
        {
            _dataAccess = new DataAccessFactory();
            _checkJobsTimer = new Timer(CheckJobsTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start(string workFolderPath)
        {
            void init()
            {
                try
                {
                    JobRunnerInitializer initializer = new JobRunnerInitializer();
                    initializer.WorkFolderPath = workFolderPath;
                    initializer.Start(null);

                    // initializer
                    _checkJobsTimer.Change(0, Timeout.Infinite);
                }
                catch (Exception ex)
                {
                    ErrorLogsAccessor.Insert(ex);
                    Stop();
                }
            }

            new Task(init).Start();
        }

        public void Stop()
        {
            _checkJobsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _checkJobsTimer = null;

            // disable timer
            // check running job metadata for stop action (wait or abort)
            //  use previous job data to deterimine if too long running for wait


            this.Stopped?.Invoke(null, EventArgs.Empty);
        }

        public void ShutDown()
        {
            // call same logic as stop, but with shorter wait time
        }

        public void Dispose()
        {
            // same logic as stop
        }

        public void StopAfterCurrentJob()
        {

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
