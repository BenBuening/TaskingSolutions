using System.Collections.Generic;
using TaskingSolutions.Data.Entities;

namespace Dashboard.Models
{
    public class DashboardModel
    {

        public List<DashboardInfo> Upcoming { get; set; }
        public List<DashboardInfo> Running { get; set; }
        public List<DashboardInfo> RecentlyCompleted { get; set; }

    }
}