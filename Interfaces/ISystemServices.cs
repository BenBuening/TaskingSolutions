using System;

namespace TaskingSolutions.Interfaces
{
    public interface ISystemServices
    {
        void LogStat(string key, string value);
        void LogError(string message);
        void LogError(Exception ex);
        void LogError(Exception ex, string additionalComment);
    }
}
