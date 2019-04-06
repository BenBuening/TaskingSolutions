using Dashboard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TaskingSolutions.Data.DataAccess;

namespace Dashboard.Controllers
{
    public class DashboardController : Controller
    {

        public ActionResult Index()
        {
            var jobRunsAccessor = new DataAccessFactory().GetJobRunsAccessor();

            DashboardModel model = new DashboardModel();
            model.RecentlyCompleted = jobRunsAccessor.GetRecentlyCompleted();
            model.Running = jobRunsAccessor.GetRunning();
            model.Upcoming = jobRunsAccessor.GetUpcoming();

            return View(model);
        }

        public ActionResult Jobs()
        {
            return View();
        }

        

    }
}