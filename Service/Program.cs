using System.ServiceProcess;

namespace JobRunner
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
