using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaskingSolutions.Data;
using TaskingSolutions.Data.DataAccess;
using TaskingSolutions.Data.Entities;
using TaskingSolutions.Interfaces;

namespace TaskingSolutions.Module.System_Jobs
{
    internal class JobReconciler
    {

        private readonly IJobsAccessor _jobsAccessor;

        private void HandleJobExists(Job job, Type jobInfo)
        {
            bool update = false;

            if (job.DotNetType != jobInfo.FullName)
            {
                job.DotNetType = jobInfo.FullName;
                update = true;
            }

            if (job.IsDotNetTypeMissing)
            {
                job.IsDotNetTypeMissing = false;
                update = true;
            }

            if (update)
                _jobsAccessor.Update(job);
        }

        private void HandleNewJob(Type jobInfo, string jobName)
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
            job.Name = jobName;
            job.IsDotNetTypeMissing = false;

            var jobDefaults = jobInfo.GetCustomAttribute<JobDefaultMetadataAttribute>();
            if (jobDefaults != null)
            {
                job.AlertIfNotRunForXMinutes = jobDefaults.AlertIfNotRunForXMinutes;
                job.AlertsEmailList = jobDefaults.AlertsEmailList;
                job.CanRunConcurrentlyWithOtherJobs = jobDefaults.CanRunConcurrentlyWithOtherJobs;
                job.JobQueuePriority = jobDefaults.JobQueuePriority;
                job.AllowSimultaneousInstances = jobDefaults.AllowSimultaneousInstances;
                job.QueueMultipleInstances = jobDefaults.QueueMultipleInstancesWhenNotSimultaneous;
                //jobDefaults.OnShutdown
            }


            _jobsAccessor.Create(job, schedules);
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

        private void HandleMissingJobs(ICollection<Job> jobs)
        {
            List<Job> jobsToUpdate = new List<Job>(jobs.Count);

            foreach (var job in jobs)
                if (!job.IsDotNetTypeMissing)
                {
                    job.IsDotNetTypeMissing = true;
                    jobsToUpdate.Add(job);
                }

            _jobsAccessor.Update(jobsToUpdate);
        }

        private List<Type> GetJobsFromLoadedAssemblies()
        {
            Type jobType = typeof(IJob);
            Type sysJobType = typeof(SystemJobAttribute);
            List<Type> jobInfoItems = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                jobInfoItems.AddRange(assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && jobType.IsAssignableFrom(x) && !x.IsDefined(sysJobType)));

            return jobInfoItems;
        }


        public JobReconciler()
        {
            _jobsAccessor = new DataAccessFactory().GetJobsAccessor();
        }

        public void Start()
        {
            var diskJobs = GetJobsFromLoadedAssemblies();
            var jobs = _jobsAccessor.GetAll().ToDictionary(x => x.Name.ToLower());

            foreach (var jobInfo in diskJobs)
            {
                string jobName = jobInfo.GetCustomAttribute<JobNameAttribute>()?.JobName ?? jobInfo.FullName;
                string jobNameAsKey = jobName.ToLower();

                if (jobs.TryGetValue(jobNameAsKey, out Job job))
                {
                    jobs.Remove(jobNameAsKey);
                    HandleJobExists(job, jobInfo);
                }
                else
                    HandleNewJob(jobInfo, jobName);
            }

            HandleMissingJobs(jobs.Values);
        }

    }
}
