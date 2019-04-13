using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class JobDefaultMetadataAttribute : Attribute
    {

        public JobDefaultMetadataAttribute(bool canRunConcurrent, bool allowMultipleInstances)
        {
            this.CanRunConcurrent = canRunConcurrent;
            this.AllowMultipleInstances = allowMultipleInstances;
        }

        public bool CanRunConcurrent { get; set; }
        public bool AllowMultipleInstances { get; set; }
        public OnShutdownAction OnShutdown { get; set; }
        public JobQueuePriority JobQueuePriority { get; set; }
        public string AlertsEmailList { get; set; }
        public int AlertIfNotRunForXMinutes { get; set; }

    }
}
