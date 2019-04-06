using System;

namespace TaskingSolutions.Data.Entities
{
    public class DashboardInfo : EntityBase
    {
        public int JobId { get; set; }
        public string JobName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? ErrorCount { get; set; }
    }
}
