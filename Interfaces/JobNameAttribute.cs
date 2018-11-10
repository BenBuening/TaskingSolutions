using System;

namespace TaskingSolutions.Interfaces
{
    /// <summary>
    /// Specify the name of the job. If this attribute is not used, the IJob-implementing class's name will be used for the job's name.
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
