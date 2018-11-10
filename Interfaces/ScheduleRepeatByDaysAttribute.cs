using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ScheduleRepeatByDaysAttribute : Attribute
    {

        public string FirstRunDateTime { get; set; }
        public int Days { get; set; }
        public int? EndAfterTimesTriggered { get; set; }
        public string EndAfterDateTime { get; set; }

        public ScheduleRepeatByDaysAttribute(string firstRunDateTime, int days)
        {
            this.FirstRunDateTime = firstRunDateTime;
            this.Days = days;
        }

    }
}
