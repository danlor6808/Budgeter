using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Budgeter.Models
{
    public class MyBaseController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                ViewBag.DisplayName = user.DisplayName;
                ViewBag.FirstName = user.FirstName;
                ViewBag.Icon = user.Icon;
                ViewBag.LastName = user.LastName;
                ViewBag.Invitations = db.Invitations.Where(u => u.InvitedUserId == user.Id).Where(b => b.Accepted == false).Count();
                base.OnActionExecuting(filterContext);
            }
        }
    }
}