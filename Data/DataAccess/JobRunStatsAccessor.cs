using System.Data;
using System.Data.SqlClient;

namespace TaskingSolutions.Data.DataAccess
{

    public partial interface IJobRunStatsAccessor
    {

        void Insert(int jobRunId, string key, string value);
        IOutputValueBinder Insert(SqlCommand cmd, int jobRunId, string key, string value);

    }


    internal partial class JobRunStatsAccessor : IJobRunStatsAccessor
    {

        public void Insert(int jobRunId, string key, string value)
        {
            using (SqlConnection con = new SqlConnection(this.ConnectionString))
            {
                con.Open();
                SqlTransaction txn = con.BeginTransaction();

                try
                {
                    using (SqlCommand cmd = new SqlCommand(null, con))
                    {
                        cmd.Transaction = txn;
                        Insert(cmd, jobRunId, key, value);
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

        public IOutputValueBinder Insert(SqlCommand cmd, int jobRunId, string key, string value)
        {

            cmd.CommandText = @"
declare @JobRunStatTypeId int;
select @JobRunStatTypeId = Id from [dbo].[JobRunStatTypes] where [Description] = @Description;
if @JobRunStatTypeId is null
begin
	insert into [dbo].[JobRunStatTypes] ([Description]) values (@Description);
	set @JobRunStatTypeId = SCOPE_IDENTITY();
end
insert into [dbo].[JobRunStats] ([JobRunId], [JobRunStatTypeId], [Value]) values (@JobRunId, @JobRunStatTypeId, @Value)
";
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Clear();
            SqlParameter JobRunIdParam = cmd.Parameters.Add(Parameter("@JobRunId", SqlDbType.Int, jobRunId));
            SqlParameter DescriptionParam = cmd.Parameters.Add(Parameter("@Description", SqlDbType.VarChar, key));
            SqlParameter ValueParam = cmd.Parameters.Add(Parameter("@Value", SqlDbType.VarChar, value));

            cmd.ExecuteNonQuery();


            return new OutputValueBinder(null);
        }

    }
}
