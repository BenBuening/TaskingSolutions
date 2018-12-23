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

            using (SqlConnection con = new SqlConnection(SQL.ConStr))
            {
                con.Open();
                SqlTransaction txn = con.BeginTransaction();

                try
                {
                    using (SqlCommand cmd = new SqlCommand(null, con))
                    {
                        cmd.Transaction = txn;
                        results.Add(Insert(cmd, job));

                        results.AddRange(new JobSchedulesAccessor().Insert(cmd, schedules));
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
