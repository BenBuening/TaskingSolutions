using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TaskingSolutions.Logging;
using TaskingSolutions.Module;

namespace TaskingSolutions.Service
{
    public partial class JobRunnerService : ServiceBase
    {

        private const int _watcherWorkWaitInterval = 1000 * 10; // 10 seconds

        private FileLogger _logger;
        private string _dropFolder;
        private string _workFolder;
        private FileSystemWatcher _watcher;
        private AppDomain _workerDomain;
        private Runner _runner;
        private volatile Action _onStoppedFollowup;
        private Timer _watcherWorkTrigger;
        private bool _isShuttingDown;


        private void UpdateJobsAssemblies(object state)
        {
            _logger.LogDebug("JobRunnerService.UpdateJobsAssemblies - enter");
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcherWorkTrigger.Change(Timeout.Infinite, Timeout.Infinite);
                _watcherWorkTrigger.Dispose();
                _watcher.Dispose();
                _watcherWorkTrigger = null;
                _watcher = null;


                EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                _onStoppedFollowup = () => { _onStoppedFollowup = null; handle.Set(); };

                _runner.StopAfterCurrentJobsFinish();

                _logger.LogDebug("JobRunnerService.UpdateJobsAssemblies - begin wait for stop");
                handle.WaitOne();
                _logger.LogDebug("JobRunnerService.UpdateJobsAssemblies - end wait for stop");

                // clear out the working folder and copy over the contents of the drop folder
                foreach (var file in new DirectoryInfo(_workFolder).GetFiles())
                    file.Delete();
                foreach (var filePath in Directory.GetFiles(_dropFolder))
                    File.Copy(filePath, Path.Combine(_workFolder, Path.GetFileName(filePath)));

                OnStart(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                Stop();
            }
            _logger.LogDebug("JobRunnerService.UpdateJobsAssemblies - exit");
        }

        private void Stop(bool isShutDown)
        {
            _watcher.EnableRaisingEvents = false;
            _watcherWorkTrigger.Change(Timeout.Infinite, Timeout.Infinite);
            _watcherWorkTrigger.Dispose();
            _watcher.Dispose();
            _watcherWorkTrigger = null;
            _watcher = null;

            EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _onStoppedFollowup = () => { _onStoppedFollowup = null; handle.Set(); };

            if (isShutDown)
                _runner.ShutDown();
            else
                _runner.Stop();

            handle.WaitOne();
        }

        private static void DirectoryCopy(string source, string dest, bool overwrite, bool copySubDirs)
        {
            DirectoryInfo info = new DirectoryInfo(source);
            if (info.Exists)
            {
                if (!Directory.Exists(dest))
                    Directory.CreateDirectory(dest);

                foreach (FileInfo file in info.GetFiles())
                    file.CopyTo(Path.Combine(dest, file.Name), overwrite);

                if (copySubDirs)
                    foreach (DirectoryInfo subdir in info.GetDirectories())
                        DirectoryCopy(subdir.FullName, Path.Combine(dest, subdir.Name), overwrite, copySubDirs);
            }
        }

        private void _watcher_Changed(object sender, FileSystemEventArgs e)
        {
            _logger.LogDebug("JobRunnerService._watcher_Changed");
            _watcherWorkTrigger.Change(_watcherWorkWaitInterval, Timeout.Infinite);
        }

        private void _watcher_Error(object sender, ErrorEventArgs e)
        {
            _logger.LogError("JobRunnerService._watcher_Error", e.GetException());
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        private void _runner_Stopped(object sender, EventArgs e)
        {
            void RunnerStopped()
            {
                _logger.LogDebug("JobRunnerService._runner_Stopped - enter");
                _runner.Stopped -= _runner_Stopped;
                _runner = null;
                AppDomain.Unload(_workerDomain);
                _onStoppedFollowup?.Invoke();
                _logger.LogDebug("JobRunnerService._runner_Stopped - exit");
            }

            Task.Factory.StartNew(RunnerStopped);
        }


        protected override void OnStart(string[] args)
        {
            _logger.LogDebug("JobRunnerService.OnStart - enter");
            try
            {
                string rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _workFolder = Path.Combine(rootPath, "WorkBinaries");
                _dropFolder = Path.Combine(rootPath, "DropBinaries");

                if (!Directory.Exists(_workFolder)) Directory.CreateDirectory(_workFolder);
                if (!Directory.Exists(_dropFolder)) Directory.CreateDirectory(_dropFolder);
                DirectoryCopy(_dropFolder, _workFolder, true, true);


                _watcherWorkTrigger = new Timer(UpdateJobsAssemblies);

                _watcher = new FileSystemWatcher();
                _watcher.Path = _dropFolder;
                _watcher.NotifyFilter = (NotifyFilters)383; // all
                _watcher.Filter = "*.dll";
                _watcher.Error += _watcher_Error;
                _watcher.Changed += _watcher_Changed;
                _watcher.Deleted += _watcher_Changed;
                _watcher.Created += _watcher_Changed;
                _watcher.EnableRaisingEvents = true;

                _workerDomain = AppDomain.CreateDomain("TaskingSolutionsDomain");

                _runner = (Runner)_workerDomain.CreateInstanceFromAndUnwrap(typeof(Runner).Assembly.Location, typeof(Runner).FullName);
                _runner.Stopped += _runner_Stopped;
                _runner.Start(_workFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError("JobRunnerService.OnStart - exception", ex);
                throw;
            }
            _logger.LogDebug("JobRunnerService.OnStart - exit");
        }

        protected override void OnStop()
        {
            _logger.LogDebug("JobRunnerService.OnStop - enter");
            try
            {
                Stop(false);
            }
            catch (Exception ex)
            {
                _logger.LogError("JobRunnerService.OnStop - exception", ex);
                throw;
            }
            _logger.LogDebug("JobRunnerService.OnStop - exit");
        }

        protected override void OnShutdown()
        {
            _logger.LogDebug("JobRunnerService.OnShutdown - enter");
            try
            {
                Stop(true);
            }
            catch (Exception ex)
            {
                _logger.LogError("JobRunnerService.OnShutdown - exception", ex);
                throw;
            }
            _logger.LogDebug("JobRunnerService.OnShutdown - exit");
        }





        public JobRunnerService()
        {
            InitializeComponent();
            _logger = new FileLogger(@"c:\_temp\JobRunnerServiceLog.txt");
        }

        public void RunAsConsole(string[] args)
        {
            Console.WriteLine("Starting...");
            OnStart(args);
            Console.WriteLine("Press any key to exit...");
            var result = Console.ReadKey();
            Stop(false);
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


    }
}
