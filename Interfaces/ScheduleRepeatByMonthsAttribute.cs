using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ScheduleRepeatByMonthsAttribute : Attribute
    {

        public string FirstRunDateTime { get; set; }
        public int Months { get; set; }
        public int? EndAfterTimesTriggered { get; set; }
        public string EndAfterDateTime { get; set; }

        public ScheduleRepeatByMonthsAttribute(string firstRunDateTime, int months)
        {
            this.FirstRunDateTime = firstRunDateTime;
            this.Months = months;
        }

    }
}
