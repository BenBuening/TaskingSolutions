using System;
using System.ServiceProcess;

namespace TaskingSolutions.Service
{
    static class Program
    {
        static void Main(string[] args)
        {
            JobRunnerService service = new JobRunnerService();
            if (Environment.UserInteractive)
                service.RunAsConsole(args);

            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { service };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
