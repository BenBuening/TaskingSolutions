using System;

namespace TaskingSolutions.Interfaces
{
    public interface IJob
    {
        void Start(DateTime scheduledTime, ISystemServices statCollector);

    }
}
