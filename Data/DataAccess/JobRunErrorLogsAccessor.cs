using System;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobRunErrorLogsAccessor
    {
        JobRunErrorLog LogException(long jobRunId, string message);
        JobRunErrorLog LogException(long jobRunId, Exception ex);
        JobRunErrorLog LogException(long jobRunId, string message, Exception ex);
    }


    internal partial class JobRunErrorLogsAccessor : IJobRunErrorLogsAccessor
    {

        public JobRunErrorLog LogException(long jobRunId, string message)
        {
            JobRunErrorLog log = new JobRunErrorLog();
            log.JobRunId = jobRunId;
            log.TimeStamp = DateTime.UtcNow;
            log.Message = message;

            Insert(log);

            return log;
        }

        public JobRunErrorLog LogException(long jobRunId, Exception ex)
        {
            JobRunErrorLog log = new JobRunErrorLog();
            log.JobRunId = jobRunId;
            log.TimeStamp = DateTime.UtcNow;
            log.Message = ex.Message;
            log.Exception = ex.ToString();

            Insert(log);

            return log;
        }

        public JobRunErrorLog LogException(long jobRunId, string message, Exception ex)
        {
            JobRunErrorLog log = new JobRunErrorLog();
            log.JobRunId = jobRunId;
            log.TimeStamp = DateTime.UtcNow;
            log.Message = message;
            log.Exception = ex.ToString();

            Insert(log);

            return log;
        }

    }

}
