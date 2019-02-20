using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobRunsAccessor
    {
        void SaveJobTriggered(JobSchedule schedule, JobRun run);
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
                        new JobSchedulesAccessor(this.ConnectionString).Update(schedule);
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
            cmd.CommandText = "SELECT * FROM [dbo].[JobRuns] WHERE [EndTime] IS NULL";
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Clear();

            using (SqlDataReader reader = cmd.ExecuteReader())
                return ReadRecords(reader);
        }

    }

}
