using System;

namespace TaskingSolutions.Interfaces
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ScheduleNoRepeatAttribute : Attribute
    {

        public string FirstRunDateTime { get; set; }

        public ScheduleNoRepeatAttribute(string firstRunDateTime)
        {
            this.FirstRunDateTime = firstRunDateTime;
        }

    }
}
