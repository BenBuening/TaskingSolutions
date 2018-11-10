using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace TaskingSolutions.Data.DataAccess
{

    internal static class SQL
    {
        static SQL() { /* todo: Put ConStr initialization somewhere */ }

        public static string ConStr = "";

        public static SqlParameter Parameter(string parameterName, SqlDbType type, object value)
        {
            SqlParameter result = new SqlParameter(parameterName, type);
            result.Value = value ?? DBNull.Value;
            return result;
        }

        public static SqlParameter OutputParameter(string parameterName, SqlDbType type)
        {
            SqlParameter result = new SqlParameter(parameterName, type);
            result.Direction = ParameterDirection.Output;
            return result;
        }
    }

}
