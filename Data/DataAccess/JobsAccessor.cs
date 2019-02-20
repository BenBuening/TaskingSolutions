using System.Collections.Generic;
using System.Data.SqlClient;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobsAccessor
    {
        void Create(Job job, IList<JobSchedule> schedules);
    }


    internal partial class JobsAccessor : IJobsAccessor
    {

        public void Create(Job job, IList<JobSchedule> schedules)
        {
            List<IOutputValueBinder> results = new List<IOutputValueBinder>(schedules.Count + 1);

            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            {
                con.Open();
                SqlTransaction txn = con.BeginTransaction();

                try
                {
                    using (SqlCommand cmd = new SqlCommand(null, con))
                    {
                        cmd.Transaction = txn;
                        var binder = Insert(cmd, job);
                        results.Add(binder);

                        int insertedId = (int)binder.PeekAtBoundValue(nameof(job.Id));
                        foreach (var schedule in schedules)
                            schedule.JobId = insertedId;

                        var schAcc = new JobSchedulesAccessor(this.ConnectionString);
                        foreach (var schedule in schedules)
                            results.Add(schAcc.Insert(cmd, schedule));
                    }

                    txn.Commit();
                }
                catch
                {
                    txn.Rollback();
                    throw;
                }

                foreach (var result in results)
                    result.Commit();
            }
        }

    }

}
