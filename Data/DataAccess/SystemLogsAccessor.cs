using System;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface ISystemLogsAccessor
    {
        SystemLog LogMessage(string message);
    }


    internal partial class SystemLogsAccessor : ISystemLogsAccessor
    {

        public SystemLog LogMessage(string message)
        {
            SystemLog log = new SystemLog();
            log.Timestamp = DateTime.UtcNow;
            log.Message = message;

            Insert(log);

            return log;
        }

    }

}
