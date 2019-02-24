using System;

namespace TaskingSolutions.Logging
{
    public interface ILogger
    {
        void Log(LogLevel logLevel, string message);
        void Log(LogLevel logLevel, string message, Exception ex);
        void LogError(string message, Exception ex);
        void LogError(Exception ex);
        void LogDebug(string message);
    }
}
