using System;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IDebugLogsAccessor
    {
        DebugLog LogMessage(string message);
    }


    internal partial class DebugLogsAccessor : IDebugLogsAccessor
    {

        public DebugLog LogMessage(string message)
        {
            DebugLog log = new DebugLog();
            log.Timestamp = DateTime.UtcNow;
            log.Message = message;

            Insert(log);

            return log;
        }

    }

}
