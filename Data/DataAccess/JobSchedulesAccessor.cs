using System.Collections.Generic;
using System.Data.SqlClient;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobSchedulesAccessor
    {
        IList<IOutputValueBinder> Insert(SqlCommand cmd, IList<JobSchedule> schedules);
    }


    internal partial class JobSchedulesAccessor : IJobSchedulesAccessor
    {

        public IList<IOutputValueBinder> Insert(SqlCommand cmd, IList<JobSchedule> schedules)
        {
            List<IOutputValueBinder> results = new List<IOutputValueBinder>(schedules.Count);

            foreach (var schedule in schedules)
                results.Add(Insert(cmd, schedule));

            return results;
        }

    }

}
