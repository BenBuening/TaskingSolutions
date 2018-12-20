using TaskingSolutions.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaskingSolutions.Data.DataAccess;
using TaskingSolutions.Data.Entities;

namespace TaskingSolutions.Module.System_Jobs
{
    [SystemJob]
    class JobRunnerInitializer : IJob
    {

        public string WorkFolderPath { get; set; }

        public void Start(ISystemServices services)
        {
            if (string.IsNullOrEmpty(this.WorkFolderPath))
                throw new ArgumentException("WorkFolderPath not set", nameof(this.WorkFolderPath));

            /*
            * 
            * installer calls home & verifies license. calls home with machine unique id & returns hash of machine id and key
            * on startup, validate hash
            * 
            * 
            * */


            foreach (var filePath in Directory.EnumerateFiles(this.WorkFolderPath, "*.dll", SearchOption.TopDirectoryOnly))
                if (Path.GetFileName(filePath) != "JobRunnerInterfaces.dll")
                    Assembly.LoadFile(filePath);


            // insert job run into db
            //new DataAccessFactory().GetJobRunsAccessor().Insert(new JobRun() { JobId = 0, StartTime = DateTime.UtcNow });




            //new JobReconciler().Start();


            // todo: start job. exception handling in job pipeline
            //  start job
            // when exception... option to send email...
        }
    }
}
