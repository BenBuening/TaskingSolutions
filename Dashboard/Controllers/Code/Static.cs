using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Dashboard.Controllers
{
    public static class Static
    {

        public static string Format(TimeSpan ts)
        {
            if (ts.Days > 0)
                return string.Format("{3}d {2}h {1}m {0:00}s", ts.Seconds, ts.Minutes, ts.Hours, ts.Days);

            else if (ts.Hours > 0)
                return string.Format("{2}h {1}m {0:00}s", ts.Seconds, ts.Minutes, ts.Hours, ts.Days);

            else
                return string.Format("{1}m {0:00}s", ts.Seconds, ts.Minutes, ts.Hours, ts.Days);
        }

    }
}