using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobSchedulesAccessor
    {
        List<ScheduleAndJob> GetDueSchedules();
    }


    internal partial class JobSchedulesAccessor : IJobSchedulesAccessor
    {

        public List<ScheduleAndJob> GetDueSchedules()
        {
            string sql = @"
select
	js.Id, js.JobId, js.InitialTriggerTime, js.RecurrenceType, js.RecurrenceInterval, js.TimesToRecur, js.RecurUntil, TimesTriggered, js.NextTriggerTime,
	j.Name, j.IsSystemJob, j.CanRunConcurrent, j.QueueMultipleInstances, j.JobQueuePriority, j.AlertsEmailList, j.AlertIfNotRunForXMinutes, j.DotNetType, j.IsDotNetTypeMissing
from dbo.JobSchedules js
join dbo.Jobs j on js.JobId = j.Id
where j.IsDotNetTypeMissing = 0 and j.IsDisabled = 0
    and js.NextTriggerTime <= GETUTCDATE()
order by j.JobQueuePriority, js.NextTriggerTime;
";

            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                con.Open();
                cmd.CommandType = CommandType.Text;

                List<ScheduleAndJob> result = new List<ScheduleAndJob>();

                using (SqlDataReader reader = cmd.ExecuteReader())
                    if (reader.HasRows)
                    {
                        int IdIndex = reader.GetOrdinal("Id");
                        int JobIdIndex = reader.GetOrdinal("JobId");
                        int InitialTriggerTimeIndex = reader.GetOrdinal("InitialTriggerTime");
                        int RecurrenceTypeIndex = reader.GetOrdinal("RecurrenceType");
                        int RecurrenceIntervalIndex = reader.GetOrdinal("RecurrenceInterval");
                        int TimesToRecurIndex = reader.GetOrdinal("TimesToRecur");
                        int RecurUntilIndex = reader.GetOrdinal("RecurUntil");
                        int TimesTriggeredIndex = reader.GetOrdinal("TimesTriggered");
                        int NextTriggerTimeIndex = reader.GetOrdinal("NextTriggerTime");

                        int NameIndex = reader.GetOrdinal("Name");
                        int IsSystemJobIndex = reader.GetOrdinal("IsSystemJob");
                        int CanRunConcurrentIndex = reader.GetOrdinal("CanRunConcurrent");
                        int QueueMultipleInstancesIndex = reader.GetOrdinal("QueueMultipleInstances");
                        int JobQueuePriorityIndex = reader.GetOrdinal("JobQueuePriority");
                        int AlertsEmailListIndex = reader.GetOrdinal("AlertsEmailList");
                        int AlertIfNotRunForXMinutesIndex = reader.GetOrdinal("AlertIfNotRunForXMinutes");
                        int DotNetTypeIndex = reader.GetOrdinal("DotNetType");
                        int IsDotNetTypeMissingIndex = reader.GetOrdinal("IsDotNetTypeMissing");

                        while (reader.Read())
                        {
                            JobSchedule schedule = new JobSchedule();
                            schedule.IsNew = false;
                            schedule.Id = reader.GetInt32(IdIndex);
                            schedule.JobId = reader.GetInt32(JobIdIndex);
                            if (!reader.IsDBNull(InitialTriggerTimeIndex)) schedule.InitialTriggerTime = reader.GetDateTime(InitialTriggerTimeIndex);
                            schedule.RecurrenceType = (RecurranceType)reader.GetInt16(RecurrenceTypeIndex);
                            if (!reader.IsDBNull(RecurrenceIntervalIndex)) schedule.RecurrenceInterval = reader.GetInt32(RecurrenceIntervalIndex);
                            if (!reader.IsDBNull(TimesToRecurIndex)) schedule.TimesToRecur = reader.GetInt32(TimesToRecurIndex);
                            if (!reader.IsDBNull(RecurUntilIndex)) schedule.RecurUntil = reader.GetDateTime(RecurUntilIndex);
                            schedule.TimesTriggered = reader.GetInt32(TimesTriggeredIndex);
                            if (!reader.IsDBNull(NextTriggerTimeIndex)) schedule.NextTriggerTime = reader.GetDateTime(NextTriggerTimeIndex);

                            Job job = new Job();
                            job.IsNew = false;
                            job.Id = schedule.JobId;
                            job.Name = reader.GetString(NameIndex).Trim();
                            job.IsSystemJob = reader.GetBoolean(IsSystemJobIndex);
                            job.CanRunConcurrent = reader.GetBoolean(CanRunConcurrentIndex);
                            job.QueueMultipleInstances = reader.GetBoolean(QueueMultipleInstancesIndex);
                            job.JobQueuePriority = (TaskingSolutions.Interfaces.JobQueuePriority)reader.GetByte(JobQueuePriorityIndex);
                            if (!reader.IsDBNull(AlertsEmailListIndex)) job.AlertsEmailList = reader.GetString(AlertsEmailListIndex).Trim();
                            if (!reader.IsDBNull(AlertIfNotRunForXMinutesIndex)) job.AlertIfNotRunForXMinutes = reader.GetDecimal(AlertIfNotRunForXMinutesIndex);
                            job.DotNetType = reader.GetString(DotNetTypeIndex).Trim();
                            job.IsDotNetTypeMissing = reader.GetBoolean(IsDotNetTypeMissingIndex);

                            result.Add(new ScheduleAndJob() { Job = job, JobSchedule = schedule });
                        }
                    }

                return result;
            }
        }

    }

}
