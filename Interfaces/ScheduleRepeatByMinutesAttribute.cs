using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ScheduleRepeatByMinutesAttribute : Attribute
    {

        public string FirstRunDateTime { get; set; }
        public int Minutes { get; set; }
        public int? EndAfterTimesTriggered { get; set; }
        public string EndAfterDateTime { get; set; }

        public ScheduleRepeatByMinutesAttribute(string firstRunDateTime,int minutes)
        {
            this.FirstRunDateTime = firstRunDateTime;
            this.Minutes = minutes;
        }

    }
}
