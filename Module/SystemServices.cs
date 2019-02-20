using System;
using TaskingSolutions.Data.DataAccess;
using TaskingSolutions.Data.Entities;
using TaskingSolutions.Interfaces;

namespace TaskingSolutions.Module
{
    internal class SystemServices : ISystemServices
    {

        private IDataAccessFactory _dataAccess;
        private readonly int _jobRunId;
        
        public SystemServices(IDataAccessFactory dataAccess, int jobRunId)
        {
            _dataAccess = dataAccess;
            _jobRunId = jobRunId;
        }

        public void LogStat(string key, string value)
        {
            try
            {
                _dataAccess.GetJobRunStatsAccessor().Insert(_jobRunId, key, value);
            }
            catch (Exception ex)
            {
                _dataAccess.GetErrorLogsAccessor().LogException("Error trying to log a stat", ex);
            }
        }

    }
}
