using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskingSolutions.Interfaces;
using TestAssemblyDependency;

namespace TestAssembly
{
    [JobName("Test Job")]
    [ScheduleRepeatByMinutes("2/16/2019", 2)]
    [JobDefaultMetadata(true, true, OnShutdown = OnShutdownAction.FinishJob)]
    public class TestJob : IJob
    {
        public void Start(DateTime scheduledTime, ISystemServices statCollector)
        {
            statCollector.LogStat("Test Job Run",  $"for schedule time {scheduledTime} utc at {DateTime.UtcNow.ToString()} utc");

            Class1 temp = new Class1("test dependency");
            temp.DoSomething();

        }

    }
}
