using System;
using System.IO;

namespace TaskingSolutions.Logging
{
    public class Logger
    {

        public string LogFilePath { get; set; }
        public LogLevel LogLevel { get; set; }

        public Logger()
        {
            this.LogFilePath = @"c:\_temp\JobRunnerLog.txt";
        }

        public Logger(string logFilePath)
        {
            this.LogFilePath = logFilePath;
        }

        public void Log(LogLevel logLevel, string message)
        {
            if (logLevel >= this.LogLevel)
                Log(message);
        }

        public void Log(LogLevel logLevel, string message, Exception ex)
        {
            if (logLevel >= this.LogLevel)
                Log(message + Environment.NewLine + ex.ToString());
        }

        public void LogError(string message, Exception ex)
        {
            if (LogLevel.Error >= this.LogLevel)
                Log(message + Environment.NewLine + ex.ToString());
        }

        public void LogError(Exception ex)
        {
            if (LogLevel.Error >= this.LogLevel)
                Log(ex.ToString());
        }

        public void LogDebug(string message)
        {
            if (LogLevel.Debug >= this.LogLevel)
                Log(message);
        }

        private void Log(string text)
        {
            try
            {
                //if (File.Exists(this.LogFilePath))
                File.AppendAllText(this.LogFilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss - ") + text + Environment.NewLine);
            }
            catch { }
        }

    }
}
