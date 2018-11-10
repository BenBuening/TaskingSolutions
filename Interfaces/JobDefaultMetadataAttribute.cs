using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class JobDefaultMetadataAttribute : Attribute
    {

        public JobDefaultMetadataAttribute(bool canRunConcurrent, bool queueMultipleInstances)
        {
            this.CanRunConcurrent = canRunConcurrent;
            this.QueueMultipleInstances = queueMultipleInstances;
        }

        public bool CanRunConcurrent { get; set; }
        public bool QueueMultipleInstances { get; set; }
        public OnShutdownAction OnShutdown { get; set; }
        public JobQueuePriority JobQueuePriority { get; set; }
        public string AlertsEmailList { get; set; }
        public string AlertIfNotRunForDuration { get; set; }

    }
}
