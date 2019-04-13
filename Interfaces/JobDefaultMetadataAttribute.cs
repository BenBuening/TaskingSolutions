using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class JobDefaultMetadataAttribute : Attribute
    {

        public JobDefaultMetadataAttribute(bool canRunConcurrentlyWithOtherJobs, bool allowSimultaneousInstances, bool queueMultipleInstancesWhenNotSimultaneous)
        {
            this.CanRunConcurrentlyWithOtherJobs = canRunConcurrentlyWithOtherJobs;
            this.AllowSimultaneousInstances = allowSimultaneousInstances;
            this.QueueMultipleInstancesWhenNotSimultaneous = queueMultipleInstancesWhenNotSimultaneous;
        }

        public bool CanRunConcurrentlyWithOtherJobs { get; set; }
        public bool AllowSimultaneousInstances { get; set; }
        public bool QueueMultipleInstancesWhenNotSimultaneous { get; set; }
        public OnShutdownAction OnShutdown { get; set; }
        public JobQueuePriority JobQueuePriority { get; set; }
        public string AlertsEmailList { get; set; }
        public int AlertIfNotRunForXMinutes { get; set; }

    }
}
