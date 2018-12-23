using System;

namespace TaskingSolutions.Interfaces
{
    /// <summary>
    /// Specify the friendly name of the job. This is the name that will be shown in the Tasking Solutions dashboard.
    ///     If this attribute is not used, the class's name will be used for the job's name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class JobNameAttribute : Attribute
    {

        public string JobName { get; set; }

        public JobNameAttribute(string jobName)
        {
            this.JobName = jobName;
        }

    }
}
