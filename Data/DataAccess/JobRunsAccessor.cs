using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobRunsAccessor
    {
        void SaveJobTriggered(JobSchedule schedule, JobRun run);
        void SaveJobErrored(JobRun run, Exception ex);
        List<JobRun> GetAllUncompleted();
        List<JobRun> GetAllUncompleted(SqlCommand cmd);
    }

    internal partial class JobRunsAccessor
    {

        public void SaveJobTriggered(JobSchedule schedule, JobRun run)
        {
            IOutputValueBinder result;

            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            {
                con.Open();
                SqlTransaction txn = con.BeginTransaction();

                try
                {
                    using (SqlCommand cmd = new SqlCommand(null, con))
                    {
                        cmd.Transaction = txn;

                        result = Insert(cmd, run);
                        new JobSchedulesAccessor(this.ConnectionString).Update(cmd, schedule);
                    }

                    txn.Commit();
                }
                catch
                {
                    txn.Rollback();
                    throw;
                }

                result.Commit();
            }
        }

        public void SaveJobErrored(JobRun run, Exception ex)
        {
            if (run.IsNew) throw new ArgumentException("this method is update only");

            var errorLogAccessor = new JobRunErrorLogsAccessor(this.ConnectionString);
            JobRunErrorLog log = new JobRunErrorLog();
            log.JobRunId = run.Id;
            log.TimeStamp = DateTime.UtcNow;
            log.Message = ex.Message;
            log.Exception = ex.ToString();


            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            {
                con.Open();
                SqlTransaction txn = con.BeginTransaction();

                try
                {
                    using (SqlCommand cmd = new SqlCommand(null, con))
                    {
                        cmd.Transaction = txn;

                        errorLogAccessor.Insert(cmd, log);
                        Update(cmd, run);
                    }

                    txn.Commit();
                }
                catch
                {
                    txn.Rollback();
                    throw;
                }
            }
        }

        public List<JobRun> GetAllUncompleted()
        {
            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            {
                con.Open();

                using (SqlCommand cmd = new SqlCommand(null, con))
                    return GetAllUncompleted(cmd);
            }
        }

        public List<JobRun> GetAllUncompleted(SqlCommand cmd)
        {
            cmd.CommandText = "SELECT * FROM [dbo].[JobRuns] r WHERE r.EndTime is null and not exists(select 1 from JobRunErrorLogs where JobRunId = r.Id)";
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Clear();

            using (SqlDataReader reader = cmd.ExecuteReader())
                return ReadRecords(reader);
        }

    }

}
