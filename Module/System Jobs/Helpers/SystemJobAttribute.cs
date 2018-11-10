using System;

namespace TaskingSolutions.Module.System_Jobs
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal class SystemJobAttribute  : Attribute
    {
    }
}
