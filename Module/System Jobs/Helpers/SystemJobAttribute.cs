using System;

namespace JobRunnerModule.System_Jobs
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal class SystemJobAttribute  : Attribute
    {
    }
}
