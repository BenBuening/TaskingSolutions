using System;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IErrorLogsAccessor
    {
        ErrorLog LogException(Exception ex);
    }


    internal partial class ErrorLogsAccessor : IErrorLogsAccessor
    {

        public ErrorLog LogException(Exception ex)
        {
            ErrorLog log = new ErrorLog();
            log.TimeStamp = DateTime.UtcNow;
            log.Message = ex.Message;
            log.Exception = ex.ToString();

            Insert(log);

            return log;
        }

    }

}
