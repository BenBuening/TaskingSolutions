using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskingSolutions.Interfaces;
using TestAssemblyDependency;

namespace TestAssembly
{
    [JobName("Long Running Job")]
    [ScheduleRepeatByMinutes("3/17/2019 12:00:00", 20)]
    [JobDefaultMetadata(true, false, false, OnShutdown = OnShutdownAction.FinishJob)]
    public class LongRunningJob : IJob
    {
        public void Start(DateTime scheduledTime, ISystemServices statCollector)
        {
            statCollector.LogStat("Test Job Run",  $"for Long Running Job schedule time {scheduledTime} utc started at {DateTime.UtcNow.ToString()} utc");

            System.Threading.Thread.Sleep(4 * 60 * 1000);

            statCollector.LogStat("Test Job Run", $"for Long Running Job schedule time {scheduledTime} utc finished at {DateTime.UtcNow.ToString()} utc");
        }

    }
}
