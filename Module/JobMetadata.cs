using JobRunner.Data.Entities;
using JobRunner.Interfaces;
using System.Threading;

namespace JobRunnerModule
{
    internal class JobMetadata
    {
        public IJob Job { get; private set; }
        public Job JobData { get; private set; }
        public JobRun JobRun { get; set; }

        public Thread RunThread { get; set; }

        public JobMetadata(IJob job, Job jobdata)
        {
            this.Job = job;
            this.JobData = jobdata;
        }
    }
}
