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
        List<DashboardInfo> GetRecentlyCompleted();
        List<DashboardInfo> GetRunning();
        List<DashboardInfo> GetUpcoming();
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
            cmd.CommandText = "SELECT * FROM [dbo].[JobRuns] r WHERE r.EndTime is null";
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Clear();

            using (SqlDataReader reader = cmd.ExecuteReader())
                return ReadRecords(reader);
        }

        public List<DashboardInfo> GetRecentlyCompleted()
        {
            string sql = @"
select top (20)
	j.Id as JobId,
	j.[Name] as JobName,
	r.StartTime,
	r.EndTime,
	isnull(counts.ErrorCount, 0) as ErrorCount

from [dbo].[JobRuns] r
join [dbo].[Jobs] j on r.JobId = j.Id
left join (select l.JobRunId, count(*) as ErrorCount from [dbo].[JobRunErrorLogs] l group by l.JobRunId) as counts on r.Id = counts.JobRunId

where r.EndTime is not null or counts.ErrorCount is not null

order by r.EndTime desc
";

            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                con.Open();
                cmd.CommandType = CommandType.Text;

                List<DashboardInfo> result = new List<DashboardInfo>();

                using (SqlDataReader reader = cmd.ExecuteReader())
                    if (reader.HasRows)
                    {
                        int JobIdIndex = reader.GetOrdinal("JobId");
                        int JobNameIndex = reader.GetOrdinal("JobName");
                        int StartTimeIndex = reader.GetOrdinal("StartTime");
                        int EndTimeIndex = reader.GetOrdinal("EndTime");
                        int ErrorCountIndex = reader.GetOrdinal("ErrorCount");

                        while (reader.Read())
                        {
                            DashboardInfo record = new DashboardInfo();
                            record.IsNew = false;
                            record.JobId = reader.GetInt32(JobIdIndex);
                            record.JobName = reader.GetString(JobNameIndex).Trim();
                            record.StartTime = reader.GetDateTime(StartTimeIndex);
                            if (!reader.IsDBNull(EndTimeIndex)) record.EndTime = reader.GetDateTime(EndTimeIndex);
                            record.ErrorCount = reader.GetInt32(ErrorCountIndex);

                            result.Add(record);
                        }
                    }

                return result;
            }
        }

        public List<DashboardInfo> GetRunning()
        {
            string sql = @"
select
	j.Id as JobId,
	j.[Name] as JobName,
	r.StartTime

from [dbo].[JobRuns] r
join [dbo].[Jobs] j on r.JobId = j.Id

where r.EndTime is null
and not exists(select 1 from JobRunErrorLogs e where e.JobRunId = r.Id)
";

            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                con.Open();
                cmd.CommandType = CommandType.Text;

                List<DashboardInfo> result = new List<DashboardInfo>();

                using (SqlDataReader reader = cmd.ExecuteReader())
                    if (reader.HasRows)
                    {
                        int JobIdIndex = reader.GetOrdinal("JobId");
                        int JobNameIndex = reader.GetOrdinal("JobName");
                        int StartTimeIndex = reader.GetOrdinal("StartTime");

                        while (reader.Read())
                        {
                            DashboardInfo record = new DashboardInfo();
                            record.IsNew = false;
                            record.JobId = reader.GetInt32(JobIdIndex);
                            record.JobName = reader.GetString(JobNameIndex).Trim();
                            record.StartTime = reader.GetDateTime(StartTimeIndex);

                            result.Add(record);
                        }
                    }

                return result;
            }
        }

        public List<DashboardInfo> GetUpcoming()
        {
            string sql = @"
select top (10)
	j.Id as JobId,
	j.[Name] as JobName,
	s.NextTriggerTime as StartTime

from dbo.JobSchedules s
join dbo.Jobs j on s.JobId = j.Id

order by s.NextTriggerTime
";

            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                con.Open();
                cmd.CommandType = CommandType.Text;

                List<DashboardInfo> result = new List<DashboardInfo>();

                using (SqlDataReader reader = cmd.ExecuteReader())
                    if (reader.HasRows)
                    {
                        int JobIdIndex = reader.GetOrdinal("JobId");
                        int JobNameIndex = reader.GetOrdinal("JobName");
                        int StartTimeIndex = reader.GetOrdinal("StartTime");

                        while (reader.Read())
                        {
                            DashboardInfo record = new DashboardInfo();
                            record.IsNew = false;
                            record.JobId = reader.GetInt32(JobIdIndex);
                            record.JobName = reader.GetString(JobNameIndex).Trim();
                            record.StartTime = reader.GetDateTime(StartTimeIndex);

                            result.Add(record);
                        }
                    }

                return result;
            }
        }

    }

}
