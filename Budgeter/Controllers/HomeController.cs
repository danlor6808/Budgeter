using Budgeter.Models;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static Budgeter.Models.Extensions;
using FluentDateTimeOffset;
using FluentDateTime;
using FluentDate;
using System.Globalization;

namespace Budgeter.Controllers
{

    [RequireHttps]
    public class HomeController : MyBaseController
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        public ActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Household");
            }
            else
            {
                return View("Index");
            }
        }

        public ActionResult GetChart(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var household = db.Household.Find(user.HouseholdId);
            var account = db.Account.Find(id);
            var eTransactions = account.Transactions.Where(u => u.Category.Expense == true && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == DateTime.Now.Month).Sum(t => t.Amount);
            var iTransactions = account.Transactions.Where(u => u.Category.Expense == false && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == DateTime.Now.Month).Sum(t => t.Amount);
            var rTransactions = account.Transactions.Where(u => u.Reconciled == true && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == DateTime.Now.Month).Sum(t => t.Amount);
            object[] data = new object[4];
            data[0] = new { label = "Account Balance", value = account.Balance };
            data[1] = new { label = "Total Reconciled", value = rTransactions };
            data[2] = new { label = "Total Expense", value = eTransactions };
            data[3] = new { label = "Total Income", value = iTransactions };
            return Content(JsonConvert.SerializeObject(data), "application/json");
        }

        public ActionResult GetBarChart()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var household = db.Household.Find(user.HouseholdId);
            var accountCount = household.Accounts.Where(u => u.isDeleted == false).Count();
            if (accountCount > 0)
            {
                object[] data = new object[12];
                var allincometrans = db.Transaction.Where(u => u.Account.HouseholdId == household.id && u.Account.isDeleted == false && u.TransactionDate.Year == DateTime.Now.Year && u.Void == false && u.isDeleted == false && u.Category.Expense == false).ToList();
                for (int i = 0; i < 12; i++)
                {
                    var tempList = allincometrans.Where(x => x.TransactionDate.Month == i + 1).ToList();
                    if (tempList.Any())
                    {
                        data[i] = new { month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i + 1), value = tempList.Sum(u => u.Amount) };
                    }
                    else
                    {
                        data[i] = new { month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i + 1), value = 0 };
                    }
                }

                object[] data2 = new object[12];
                var allexpensetrans = db.Transaction.Where(u => u.Account.HouseholdId == household.id && u.Account.isDeleted == false && u.TransactionDate.Year == DateTime.Now.Year && u.Void == false && u.isDeleted == false && u.Category.Expense == true).ToList();
                for (int i = 0; i < 12; i++)
                {
                    var tempList = allexpensetrans.Where(x => x.TransactionDate.Month == i+1).ToList();
                    if (tempList.Any())
                    {
                        data2[i] = new { month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i+1), value = tempList.Sum(u => u.Amount) };
                    }
                    else
                    {
                        data2[i] = new { month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i+1), value = 0 };
                    }
                }

                object[] model = new object[1];
                model[0] = new { data, data2 };
                return Content(JsonConvert.SerializeObject(model), "application/json");
            }
            else
            {
                object[] model = new object[1];
                object[] data = new object[1];
                data[0] = new {  };
                object[] data2 = new object[1];
                data2[0] = new { };
                model[0] = new { data, data2};
                return Content(JsonConvert.SerializeObject(data), "application/json");
            }
        }

        //public ActionResult GetAccountBarChart(int? id)
        //{
        //    var user = db.Users.Find(User.Identity.GetUserId());
        //    var account = db.Account.Find(id);
        //    var transactionCount = account.Transactions.Where(u => u.isDeleted == false && u.Void == false && u.Created.Month == DateTime.Now.Month);
        //    if (transactionCount.Count() > 0)
        //    {
        //        var eList = new List<Transactions>();
        //        var iList = new List<Transactions>();
        //        var Texpense = transactionCount.Where(u => u.Category.Expense == true).Sum(t => t.Amount);
        //        var Tincome = transactionCount.Where(u => u.Category.Expense == false).Sum(t => t.Amount);
        //        var data = new[]
        //        {
        //            new { name = account.Name, income = Tincome, expense = Texpense }
        //        };
        //        return Content(JsonConvert.SerializeObject(data), "application/json");
        //    }
        //    else
        //    {
        //        var data = new { name = account.Name, income = 0, expense = 0 };
        //        return Content(JsonConvert.SerializeObject(data), "application/json");
        //    }
        //}

        public ActionResult GetAccountBarChart(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var account = db.Account.Find(id);
            int counter = 0;
            //Goes through each account budget 
            if (account.AccountBudget.Any())
            {
                //object[] data = new object[account.AccountBudget.Where(u => u.Expense == true).Count()];
                object[] data = new object[12];
                var month = new List<object>();
                for (int b = 0; b < 12; b++)
                {
                    foreach (var n in account.AccountBudget.Where(u => u.Expense == true))
                    {
                        decimal accountBudgetSum = 0;
                        //list of categories that have been assigned to current account budget
                        var categorylist = db.Categories.Where(u => u.AccountBudgetId == n.id).ToList();
                        //goes through each category and gets the budget sum of each

                        foreach (var i in categorylist)
                        {
                            var categorySum = account.Transactions.Where(u => u.CategoryId == i.id && u.isDeleted == false && u.Void == false && u.TransactionDate.Month == b+1 && u.Category.Expense == true).Sum(t => t.Amount);
                            var categoryEx = account.Transactions.Where(u => u.CategoryId == i.id && u.isDeleted == false && u.Void == false && u.TransactionDate.Month == b+1 && u.Category.Expense == false).Sum(t => t.Amount);
                            accountBudgetSum += categorySum;
                            accountBudgetSum -= categoryEx;
                        }
                        decimal leftover = n.Amount - accountBudgetSum;
                        month.Add(new { name = n.Description, total = accountBudgetSum, baseAmount = n.Amount, leftover = leftover, });
                        accountBudgetSum = 0;
                    }
                    data[counter] = month.ToArray();
                    counter++;
                    month.Clear();
                }

                counter = 0;
                object[] data2 = new object[12];
                for (int b = 0; b < 12; b++)
                {
                    foreach (var n in account.AccountBudget.Where(u => u.Expense == false))
                    {
                        decimal accountBudgetSum = 0;
                        //list of categories that have been assigned to current account budget
                        var categorylist = db.Categories.Where(u => u.AccountBudgetId == n.id).ToList();
                        //goes through each category and gets the budget sum of each
                        foreach (var i in categorylist)
                        {
                            var categorySum = account.Transactions.Where(u => u.CategoryId == i.id && u.isDeleted == false && u.Void == false && u.TransactionDate.Month == b+1 && u.Category.Expense == true).Sum(t => t.Amount);
                            var categoryEx = account.Transactions.Where(u => u.CategoryId == i.id && u.isDeleted == false && u.Void == false && u.TransactionDate.Month == b+1 && u.Category.Expense == false).Sum(t => t.Amount);
                            accountBudgetSum -= categorySum;
                            accountBudgetSum += categoryEx;
                        }
                        decimal leftover = n.Amount - accountBudgetSum;
                        if (leftover <= 0)
                        {
                            leftover = 0;
                        }
                        month.Add(new { name = n.Description, total = accountBudgetSum, baseAmount = n.Amount, leftover = leftover});
                        accountBudgetSum = 0;
                    }
                    data2[counter] = month.ToArray();
                    counter++;
                    month.Clear();
                }
                object[] model = new object[1];
                model[0] = new { data, data2 };
                return Content(JsonConvert.SerializeObject(model), "application/json");
            }
            else
            {
                object[] data = new object[1];
                data[0] = new { name = "Empty", total = 0, baseAmount = 0, leftover = 0 };
                return Content(JsonConvert.SerializeObject(data), "application/json");
            }
        }
    }
}