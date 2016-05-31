using Budgeter.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static Budgeter.Models.Extensions;

namespace Budgeter.Controllers
{

    [RequireHttps]
    [Authorize]
    public class HomeController : MyBaseController
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult CreateJoinHousehold()
        {
            return View();
        }

        public ActionResult GetChart()
        {
            var data = new[]
            {
                new { label = "Number of Households", value = db.Household.Count() },
                new { label = "Number of Users", value = db.Users.Count() },
                new { label = "Number of Categories", value = db.Categories.Count() },
                new { label = "Number of Invitations Sent", value = db.Invitations.Count() }
            };
            return Content(JsonConvert.SerializeObject(data), "application/json");
        }
    }
}