using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ScheduleRepeatByHoursAttribute : Attribute
    {

        public string FirstRunDateTime { get; set; }
        public int Hours { get; set; }
        public int? EndAfterTimesTriggered { get; set; }
        public string EndAfterDateTime { get; set; }

        public ScheduleRepeatByHoursAttribute(string firstRunDateTime, int hours)
        {
            this.FirstRunDateTime = firstRunDateTime;
            this.Hours = hours;
        }

    }
}
