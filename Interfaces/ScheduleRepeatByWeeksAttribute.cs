using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ScheduleRepeatByWeeksAttribute : Attribute
    {

        public string FirstRunDateTime { get; set; }
        public int Weeks { get; set; }
        public int? EndAfterTimesTriggered { get; set; }
        public string EndAfterDateTime { get; set; }

        public ScheduleRepeatByWeeksAttribute(string firstRunDateTime, int weeks)
        {
            this.FirstRunDateTime = firstRunDateTime;
            this.Weeks = weeks;
        }

    }
}
