using System;
using TaskingSolutions.Data.DataAccess;
using TaskingSolutions.Interfaces;

namespace TaskingSolutions.Module
{
    internal class SystemServices : ISystemServices
    {

        private IDataAccessFactory _dataAccess;
        private readonly long _jobRunId;
        
        public SystemServices(IDataAccessFactory dataAccess, long jobRunId)
        {
            _dataAccess = dataAccess;
            _jobRunId = jobRunId;
        }

        public void LogStat(string key, string value)
        {
            _dataAccess.GetJobRunStatsAccessor().Insert(_jobRunId, key, value);
        }

        public void LogError(string message, Exception ex)
        {
            _dataAccess.GetJobRunErrorLogsAccessor().LogException(_jobRunId, message, ex);
        }

        public void LogError(Exception ex)
        {
            _dataAccess.GetJobRunErrorLogsAccessor().LogException(_jobRunId, ex);
        }

    }
}
