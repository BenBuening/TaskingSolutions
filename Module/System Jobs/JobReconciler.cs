using JobRunner.Data;
using JobRunner.Data.DataAccess;
using JobRunner.Data.Entities;
using JobRunner.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JobRunnerModule.System_Jobs
{
    internal class JobReconciler
    {

        private void HandleJobExists(Job job, Type jobInfo)
        {
            bool update = false;

            var jobName = GetJobName(jobInfo);
            if (job.Name != jobName)
            {
                job.Name = jobName;
                update = true;
            }

            if (job.IsDotNetTypeMissing)
            {
                job.IsDotNetTypeMissing = false;
                update = true;
            }

            if (update)
                JobsAccessor.Update(job);
        }

        private void HandleNewJob(Type jobInfo)
        {
            List<JobSchedule> schedules = new List<JobSchedule>();

            foreach (var attr in jobInfo.GetCustomAttributes<ScheduleNoRepeatAttribute>())
                if (DateTime.TryParse(attr.FirstRunDateTime, out DateTime triggerTime))
                    schedules.Add(new JobSchedule() { NextTriggerTime = triggerTime, InitialTriggerTime = triggerTime });

            foreach (var attr in jobInfo.GetCustomAttributes<ScheduleRepeatByDaysAttribute>())
                if (DateTime.TryParse(attr.FirstRunDateTime, out DateTime triggerTime))
                    schedules.Add(BuildSchedule(triggerTime, RecurranceType.Daily, attr.Days, attr.EndAfterTimesTriggered, attr.EndAfterDateTime));

            foreach (var attr in jobInfo.GetCustomAttributes<ScheduleRepeatByHoursAttribute>())
                if (DateTime.TryParse(attr.FirstRunDateTime, out DateTime triggerTime))
                    schedules.Add(BuildSchedule(triggerTime, RecurranceType.Hourly, attr.Hours, attr.EndAfterTimesTriggered, attr.EndAfterDateTime));

            foreach (var attr in jobInfo.GetCustomAttributes<ScheduleRepeatByMinutesAttribute>())
                if (DateTime.TryParse(attr.FirstRunDateTime, out DateTime triggerTime))
                    schedules.Add(BuildSchedule(triggerTime, RecurranceType.Minutely, attr.Minutes, attr.EndAfterTimesTriggered, attr.EndAfterDateTime));

            foreach (var attr in jobInfo.GetCustomAttributes<ScheduleRepeatByMonthsAttribute>())
                if (DateTime.TryParse(attr.FirstRunDateTime, out DateTime triggerTime))
                    schedules.Add(BuildSchedule(triggerTime, RecurranceType.Monthly, attr.Months, attr.EndAfterTimesTriggered, attr.EndAfterDateTime));

            foreach (var attr in jobInfo.GetCustomAttributes<ScheduleRepeatByWeeksAttribute>())
                if (DateTime.TryParse(attr.FirstRunDateTime, out DateTime triggerTime))
                    schedules.Add(BuildSchedule(triggerTime, RecurranceType.Weekly, attr.Weeks, attr.EndAfterTimesTriggered, attr.EndAfterDateTime));


            Job job = new Job();
            job.DotNetType = jobInfo.FullName;
            job.Name = GetJobName(jobInfo);
            job.IsDotNetTypeMissing = false;
            if (schedules.Count > 0)
                job.NextJobScheduleId = schedules.OrderBy(x => x.InitialTriggerTime).FirstOrDefault()?.JobScheduleId;


            JobsAccessor.Create(job, schedules);
        }

        private JobSchedule BuildSchedule(DateTime triggerTime, RecurranceType recurrenceType, int interval, int? timesToRecur, string recurUntil)
        {
            JobSchedule schedule = new JobSchedule();
            schedule.NextTriggerTime = schedule.InitialTriggerTime = triggerTime;
            schedule.RecurrenceType = recurrenceType;
            schedule.RecurrenceInterval = interval;
            schedule.TimesToRecur = timesToRecur;
            if (DateTime.TryParse(recurUntil, out DateTime endTime))
                schedule.RecurUntil = endTime;
            return schedule;
        }

        private void HandleMissingJobs(IEnumerable<Job> jobs)
        {
            foreach (var job in jobs)
                if (!job.IsDotNetTypeMissing)
                {
                    job.IsDotNetTypeMissing = true;
                    JobsAccessor.Update(job);
                }
        }

        private string GetJobName(Type jobInfo)
        {
            return jobInfo.GetCustomAttribute<JobNameAttribute>()?.JobName ?? jobInfo.Name;
        }

        private List<Type> GetJobsFromLoadedAssemblies()
        {
            Type jobType = typeof(IJob);
            Type sysJobType = typeof(SystemJobAttribute);
            List<Type> jobInfoItems = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                jobInfoItems.AddRange(assembly.GetTypes().Where(x => x.IsClass && jobType.IsAssignableFrom(x) && !x.IsDefined(sysJobType)));

            return jobInfoItems;
        }


        public void Start()
        {
            var diskJobs = GetJobsFromLoadedAssemblies();
            var jobs = JobsAccessor.Get().ToDictionary(x => x.DotNetType);

            foreach (var jobInfo in diskJobs)
            {
                if (jobs.TryGetValue(jobInfo.FullName, out Job job))
                {
                    jobs.Remove(jobInfo.FullName);
                    HandleJobExists(job, jobInfo);
                }
                else
                    HandleNewJob(jobInfo);
            }

            HandleMissingJobs(jobs.Values);
        }

    }
}
