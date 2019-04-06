using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskingSolutions.Interfaces;
using TestAssemblyDependency;

namespace TestAssembly
{
    [JobName("Erroring Job")]
    [ScheduleRepeatByMinutes("3/17/2019 12:00:00", 20)]
    [JobDefaultMetadata(true, true, OnShutdown = OnShutdownAction.FinishJob)]
    public class ErroringJob : IJob
    {
        public void Start(DateTime scheduledTime, ISystemServices statCollector)
        {
            statCollector.LogStat("Test Job Run",  $"for Erroring Job schedule time {scheduledTime} utc started at {DateTime.UtcNow.ToString()} utc");

            System.Threading.Thread.Sleep(15 * 1000);
            throw new Exception("test job error");

            statCollector.LogStat("Test Job Run", $"for Erroring Job schedule time {scheduledTime} utc finished at {DateTime.UtcNow.ToString()} utc");
        }

    }
}
