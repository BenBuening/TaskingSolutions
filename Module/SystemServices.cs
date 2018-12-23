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

        public void LogError(string message)
        {
            try
            {
                _dataAccess.GetErrorLogsAccessor().Insert(new ErrorLog() { Message = message });
            }
            catch { }
        }

        public void LogError(Exception ex)
        {
            try
            {
                _dataAccess.GetErrorLogsAccessor().Insert(new ErrorLog() { Exception = ex.ToString() });
            }
            catch { }
        }

        public void LogError(Exception ex, string additionalComment)
        {
            try
            {
                _dataAccess.GetErrorLogsAccessor().Insert(new ErrorLog() { Exception = ex.ToString(), Message = additionalComment });
            }
            catch { }
        }

        public void LogStat(string key, string value)
        {
            try
            {
                _dataAccess.GetJobRunStatsAccessor().Insert(_jobRunId, key, value);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error trying to log a stat");
            }
        }

    }
}
