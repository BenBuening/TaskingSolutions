using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskingSolutions.Interfaces;

namespace TestAssembly
{
    [JobName("Test Job")]
    [ScheduleRepeatByMinutes("11/17/2018", 5)]
    [JobDefaultMetadata(true, true, OnShutdown = OnShutdownAction.FinishJob)]
    public class TestJob : IJob
    {
        public void Start(ISystemServices statCollector)
        {
            statCollector.LogStat("Test Job Run", DateTime.Now.ToString());
        }

    }
}
