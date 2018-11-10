using System;
using JobRunner.Data.Accessors;
using JobRunner.Interfaces;

namespace JobRunnerModule
{
    internal class SystemServices : ISystemServices
    {

        private int _jobRunId;
        
        public SystemServices(int jobRunId)
        {
            _jobRunId = jobRunId;
        }

        public void LogError(string message)
        {
            throw new NotImplementedException();
        }

        public void LogError(Exception ex)
        {
            throw new NotImplementedException();
        }

        public void LogError(Exception ex, string additionalComment)
        {
            throw new NotImplementedException();
        }

        public void LogStat(string key, string value)
        {
            if (_jobRunId != 0 && key != null && value != null)
                JobRunStatsAccessor.Insert(_jobRunId, key, value);
        }

    }
}
