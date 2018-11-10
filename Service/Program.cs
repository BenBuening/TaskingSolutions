using System.ServiceProcess;

namespace TaskingSolutions.Service
{
    static class Program
    {
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new JobRunnerService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
