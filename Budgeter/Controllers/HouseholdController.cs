using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Budgeter.Models;
using Microsoft.AspNet.Identity;
using static Budgeter.Models.Extensions;
using Microsoft.AspNet.Identity.Owin;
using System.Threading.Tasks;
using SendGrid;
using System.Text;
using MoreLinq;
using FluentDate;
using FluentDateTime;
using FluentDateTimeOffset;

namespace Budgeter.Controllers
{
    [Authorize]
    [RequireHttps]
    public class HouseholdController : MyBaseController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public ActionResult _householdMsg(int? id)
        {
            var model = new HouseholdCreateJoinMV();
            model.household = db.Household.Find(id);
            return PartialView("_householdMsg", model);
        }

        // GET: Household
        public ActionResult Index(HouseholdCreateJoinMV model)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            model.user = user;
            model.household = db.Household.Find(user.HouseholdId);
            model.invitations = db.Invitations.Where(u => u.InvitedUserId == user.Id).ToList();
            if (model.household != null)
            {
                if (model.household.Accounts.Count > 0)
                {
                    var eList = new List<Transactions>();
                    var iList = new List<Transactions>();
                    foreach (var n in model.household.Accounts.Where(u => u.isDeleted == false))
                    {
                        if (n.Transactions.Count > 0)
                        {
                            var eTransactions = n.Transactions.Where(u => u.Category.Expense == true && u.Void == false && u.isDeleted == false).ToList();
                            eList.AddRange(eTransactions);
                            var iTransactions = n.Transactions.Where(u => u.Category.Expense == false && u.Void == false && u.isDeleted == false).ToList();
                            iList.AddRange(iTransactions);
                            ViewBag.householdExpense = eList.Sum(u => u.Amount);
                            ViewBag.householdIncome = iList.Sum(u => u.Amount);
                        }
                        else
                        {
                            ViewBag.householdExpense = 0;
                            ViewBag.householdIncome = 0;
                        }
                    }
                }
            }
            return View(model);
        }

        // GET: Household
        public ActionResult Invitations()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var household = db.Household.Find(user.HouseholdId);
            if (household != null)
            {
                ViewBag.householdId = household.id;
                if (household.Invitations != null)
                {
                    ViewBag.Pending = household.Invitations.Where(u => u.Accepted == false && !household.Users.Any(b => b.Email == u.Email)).DistinctBy(x => x.Email).ToList();
                }
            }
            else
            {
                ViewBag.householdId = 0;
            }
            var inv = db.Invitations.Where(u => u.InvitedUserId == user.Id).Where(b => b.Accepted == false).OrderByDescending(o => o.Date).ToList();

            return View(inv);
        }

        // GET: Household/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Household household = db.Household.Find(id);
            if (household == null)
            {
                return HttpNotFound();
            }
            return View(household);
        }

        // POST: Household/Details/Send Invitation
        [HttpPost, ActionName("Invitations")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendInvitation(HouseholdCreateJoinMV model, int id, string email)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            model.user = user;
            model.household = db.Household.Find(user.HouseholdId);
            model.invitations = db.Invitations.Where(u => u.InvitedUserId == user.Id).ToList();
            ViewBag.Pending = model.household.Invitations.Where(u => u.Accepted == false).ToList();
            string invitedId = null;

            //Checks if you're trying to send it to your own email
            if (user.Email == email)
            {
                TempData["Error"] = "You cannot send yourself an invitation!";
                return RedirectToAction("Invitations");
            }
            //Checks if email is existing in the database
            if (db.Users.Any(u => u.Email == email))
            {
                //if email exists, set properly set the invitedId to the correct user
                invitedId = db.Users.FirstOrDefault(u => u.Email == email).Id;
            }

            //Creates unique Guid code for each invitation
            var code = Guid.NewGuid().ToString();
            while (db.Invitations.Any(u => u.Code == code))
            {
                code = Guid.NewGuid().ToString();
            }

            //Create and add invitation object
            Invitations inv = new Invitations
            {
                Date = DateTimeOffset.Now,
                HouseholdId = id,
                InviterId = user.Id,
                InvitedUserId = invitedId,
                Email = email,
                Code = code,
                Accepted = false
            };

            //Create and send email
            var callBackUrl = Url.Action("JoinHousehold", "Household", new { code = code }, protocol: Request.Url.Scheme);
            var bodytext = new StringBuilder();
            bodytext.AppendFormat("{0} {1}, has invited you to join his/her household!", user.FirstName, user.LastName);
            bodytext.Append("<br><br>");
            bodytext.Append("If interested, please click <a href =\"" + callBackUrl + "\">here</a> to proceed.");

            var EmailInvitation = new IdentityMessage
            {
                Body = bodytext.ToString(),
                Subject = string.Format("Household Invitation"),
                Destination = email,
            };
            var SendService = new EmailService();
            await SendService.SendAsync(EmailInvitation);

            TempData["Success"] = "Invitation successfully sent!";
            db.Invitations.Add(inv);
            db.SaveChanges();
            return RedirectToAction("Invitations");
        }

        public ActionResult Decline(string code)
        {
            var inv = db.Invitations.FirstOrDefault(u => u.Code == code);
            if (inv != null)
            {
                if (inv.Accepted == true)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                //Checks if user is the one who was invited
                if (inv.InvitedUserId != User.Identity.GetUserId())
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                inv.Accepted = true;
                db.SaveChanges();
                return RedirectToAction("Invitations");
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        // GET: Household/Join/5
        public ActionResult JoinHousehold(string code)
        {
            //define and check the unique invitation code is valid
            var inv = db.Invitations.FirstOrDefault(u => u.Code == code);
            var user = db.Users.Find(User.Identity.GetUserId());
            var household = db.Household.Find(inv.HouseholdId);
            if (inv != null)
            {
                //Checks if invitation has already been accepted
                if (inv.Accepted == true)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                //Makes sure if user was invited
                if (inv.InvitedUserId != user.Id)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                //Checks if user is already apart of household he/she is trying to join
                if (household.Users.Any(u => u.Id == user.Id))
                {
                    ViewBag.StatusError = "You are already apart of this household!";
                    inv.Accepted = true;
                    db.SaveChanges();
                    return View(inv);
                }
                return View(inv);
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        // POST: Household/Join/5
        [HttpPost, ActionName("JoinHousehold")]
        [ValidateAntiForgeryToken]
        public ActionResult JoinHouseholdConfirm([Bind(Include = "id")] Invitations model)
        {
            //define invitation
            var inv = db.Invitations.Find(model.id);

            //define current user; check if current household id is null 
            //if not it'll use it as the previous household id
            var user = db.Users.Find(User.Identity.GetUserId());
            if (user.HouseholdId != null)
            {
                user.PreviousHouseholdId = user.HouseholdId;
            }
            user.HouseholdId = inv.HouseholdId;

            //Checks if previous household is empty, if so set it's "soft delete" to be true
            if (user.PreviousHouseholdId != null)
            {
                var household = db.Household.Find(user.PreviousHouseholdId);
                var uList = db.Users.Where(u => u.Id != user.Id);
                if (!uList.Any(u => u.HouseholdId == user.PreviousHouseholdId))
                {
                    household.isDeleted = true;
                }
            }

            //Change invitation Accepted status to "true"
            inv.Accepted = true;
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // GET: Household/Leave/5
        public ActionResult LeaveHousehold()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            if (user.HouseholdId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            return View(user.Household);
        }

        // POST: Household/Leave/5
        [HttpPost, ActionName("LeaveHousehold")]
        [ValidateAntiForgeryToken]
        public ActionResult LeaveHouseholdConfirmed()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var household = db.Household.Find(user.HouseholdId);
            var uList = db.Users.Where(u => u.Id != user.Id);
            user.PreviousHouseholdId = user.HouseholdId;
            user.HouseholdId = null;
            if (!uList.Any(u => u.HouseholdId == household.id))
            {
                household.isDeleted = true;
            }
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejoinHousehold()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            int? prevId = null;
            if (user.PreviousHouseholdId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            if (user.HouseholdId != null)
            {
                prevId = user.HouseholdId;
            }
            user.HouseholdId = user.PreviousHouseholdId;
            user.PreviousHouseholdId = prevId;
            var household = db.Household.Find(prevId);
            var uList = db.Users.Where(u => u.Id != user.Id);
            if (!uList.Any(u => u.HouseholdId == prevId))
            {
                household.isDeleted = true;
            }
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // GET: Household/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Household/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id,Name")] Household household)
        {
            if (ModelState.IsValid)
            {
                household.Created = DateTimeOffset.Now;
                db.Household.Add(household);
                var user = db.Users.Find(User.Identity.GetUserId());
                user.HouseholdId = household.id;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(household);
        }

        // GET: Household/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Household household = db.Household.Find(id);
            if (household == null)
            {
                return HttpNotFound();
            }
            return View(household);
        }

        // POST: Household/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id,Name")] Household household)
        {
            if (ModelState.IsValid)
            {
                //db.Entry(household).State = EntityState.Modified;
                db.Household.Attach(household);
                db.Entry(household).Property("Name").IsModified = true;
                household.Updated = DateTimeOffset.Now;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(household);
        }

        // GET: Household/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Household household = db.Household.Find(id);
            if (household == null)
            {
                return HttpNotFound();
            }
            return View(household);
        }

        // POST: Household/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Household household = db.Household.Find(id);
            if (household.Users.Count == 0)
            {
                household.isDeleted = true;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult CreateAccount(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Household household = db.Household.Find(id);
            if (household == null)
            {
                return HttpNotFound();
            }
            var user = db.Users.Find(User.Identity.GetUserId());
            if (user.HouseholdId != household.id)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            return View(household);
        }

        [HttpPost]
        public ActionResult CreateAccount(Account account)
        {
            if (ModelState.IsValid)
            {
                account.Created = DateTimeOffset.Now;
                account.isDeleted = false;
                db.Account.Add(account);
                //default categories upon account creation
                var Food = new Category
                {
                    Name = "Food",
                    AccountId = account.id,
                    Expense = true
                };
                var Bills = new Category
                {
                    Name = "Bills",
                    AccountId = account.id,
                    Expense = true
                };

                var Check = new Category
                {
                    Name = "Check",
                    AccountId = account.id,
                    Expense = false
                };

                var ReconcileIncome = new Category
                {
                    Name = "Reconciliation (+)",
                    AccountId = account.id,
                    Expense = false
                };

                var ReconcileExpense = new Category
                {
                    Name = "Reconciliation (-)",
                    AccountId = account.id,
                    Expense = true
                };

                db.Categories.Add(Food);
                db.Categories.Add(Bills);
                db.Categories.Add(Check);
                db.Categories.Add(ReconcileIncome);
                db.Categories.Add(ReconcileExpense);
                db.SaveChanges();

                //reconciled transaction for initial balance
                var InitialTransaction = new Transactions
                {
                    AccountId = account.id,
                    AuthorId = User.Identity.GetUserId(),
                    Amount = account.Balance,
                    Balance = account.Balance,
                    Description = "Initial Balance",
                    Reconciled = true,
                    Void = false,
                    Created = DateTimeOffset.Now,
                    TransactionDate = DateTimeOffset.Now,
                    isDeleted = false,
                    CategoryId = db.Categories.First(u => u.AccountId == account.id && u.Name.Contains("(+)")).id
                };

                var newReconcile = new Reconcile
                {
                    AccountId = account.id,
                    date = DateTimeOffset.Now,
                    TransactionId = InitialTransaction.id
                };

                db.Reconcile.Add(newReconcile);
                db.Transaction.Add(InitialTransaction);
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check your input fields and try again!" });
        }

        public ActionResult DeleteAccount(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var account = db.Account.Find(id);
            //Checks if account is actually valid
            if (account != null)
            {
                return View(account);
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost]
        public ActionResult ConfirmDeleteAccount(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var account = db.Account.Find(id);
            //Checks if account is actually valid
            if (account != null)
            {
                //Checks if the account belongs to your household
                if (account.HouseholdId == user.HouseholdId)
                {
                    account.isDeleted = true;
                    db.SaveChanges();
                    return Json(new { success = true });
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        // GET: Household/Edit/5
        public ActionResult EditAccount(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var account = db.Account.Find(id);
            if (account == null)
            {
                return HttpNotFound();
            }
            return View(account);
        }

        [HttpPost]
        public ActionResult EditAccount([Bind(Include = "id,Name")] Account account)
        {
            if (ModelState.IsValid)
            {
                //db.Entry(household).State = EntityState.Modified;
                db.Account.Attach(account);
                db.Entry(account).Property("Name").IsModified = true;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return Json(new { success = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        /// 
        /// Transactions section
        /// 
        /// 

        public ActionResult _transactionmsg(int? id)
        {
            var account = db.Account.Find(id);
            return PartialView("_transactionmsg", account);
        }

        public ActionResult Transactions(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var account = db.Account.Find(id);
            if (account == null)
            {
                return HttpNotFound();
            }
            return View(account);
        }

        public ActionResult ReconcileAccount(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            if (id != null)
            {
                var account = db.Account.Find(id);
                if (account == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                if (account.HouseholdId != user.HouseholdId)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                else
                {
                    var list = new List<Account>();
                    list.Add(account);
                    ViewBag.accountlist = new SelectList(list, "id", "Name", account.id);
                    ViewBag.householdid = account.HouseholdId;
                    ViewBag.accountid = account.id;
                    return View();
                }
            }
            else
            {
                var account = db.Account.Where(u => u.HouseholdId == user.HouseholdId).ToList();
                ViewBag.accountlist = new SelectList(account, "id", "Name");
                ViewBag.householdid = user.HouseholdId;
                return View();
            }
        }

        [HttpPost]
        public ActionResult ReconcileAccount(Transactions transaction)
        {
            if (ModelState.IsValid)
            {
                var account = db.Account.Find(transaction.AccountId);
                decimal difference;
                transaction.AuthorId = User.Identity.GetUserId();
                transaction.Created = DateTimeOffset.Now;
                transaction.TransactionDate = DateTimeOffset.Now;
                transaction.Description = "";

                if (transaction.Amount >= account.Balance)
                {
                    if (!account.Transactions.Any(u => u.Category.Name.Contains("(+)")))
                    {
                        var reconcile = new Category
                        {
                            Name = "Reconciliation (+)",
                            AccountId = account.id,
                            Expense = false
                        };
                        db.Categories.Add(reconcile);
                        db.SaveChanges();
                    }
                    transaction.CategoryId = db.Categories.FirstOrDefault(u => u.Name.Contains("Reconciliation (+)") && u.AccountId == account.id).id;
                    difference = transaction.Amount - account.Balance;
                }
                else
                {
                    if (!account.Transactions.Any(u => u.Category.Name.Contains("(-)")))
                    {
                        var reconcile = new Category
                        {
                            Name = "Reconciliation (-)",
                            AccountId = account.id,
                            Expense = true
                        };
                        db.Categories.Add(reconcile);
                        db.SaveChanges();
                    }
                    transaction.CategoryId = db.Categories.FirstOrDefault(u => u.Name.Contains("Reconciliation (-)") && u.AccountId == account.id).id;
                    difference = account.Balance - transaction.Amount;
                }
                account.Balance = transaction.Amount;
                transaction.Amount = difference;
                transaction.Balance = account.Balance;
                transaction.Reconciled = true;
                transaction.Void = false;
                foreach (var entry in account.Transactions)
                {
                    entry.Reconciled = true;
                }
                var newReconcile = new Reconcile
                {
                    AccountId = account.id,
                    date = DateTimeOffset.Now,
                    TransactionId = transaction.id
                };
                db.Reconcile.Add(newReconcile);
                db.Transaction.Add(transaction);
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check your input fields and try again!" });
        }

        public ActionResult ReconcileTransaction(int? id)
        {
            if (id != null)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                var household = db.Household.Find(user.HouseholdId);
                if (household != null)
                {
                    var transaction = db.Transaction.Find(id);
                    if (transaction != null)
                    {
                        var account = db.Account.Find(transaction.AccountId);
                        if (household.id == account.HouseholdId)
                        {
                            return View(transaction);
                        }
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost]
        public ActionResult ReconcileTransaction(Transactions transaction)
        {
            var account = db.Account.Find(transaction.AccountId);
            if (ModelState.IsValid)
            {
                var oldTransaction = db.Transaction.AsNoTracking().First(u => u.id == transaction.id);
                db.Transaction.Attach(transaction);
                db.Entry(transaction).Property("Reconciled").IsModified = true;
                db.Entry(transaction).Property("RecTransactionId").IsModified = true;
                transaction.Reconciled = true;

                var childTransaction = new Transactions
                {
                    AccountId = transaction.AccountId,
                    AuthorId = User.Identity.GetUserId(),
                    Description = oldTransaction.Description,
                    Created = DateTimeOffset.Now,
                    TransactionDate = DateTimeOffset.Now,
                    Void = false,
                    isDeleted = false,
                    Reconciled = true
                };

                if (oldTransaction.Category.Expense == true)
                {
                    //if reconciled amount is bigger than old transaction amount
                    if (oldTransaction.Amount < transaction.Amount)
                    {
                        childTransaction.CategoryId = db.Categories.FirstOrDefault(u => u.AccountId == transaction.AccountId && u.Name.Contains("(-)")).id;
                        childTransaction.Amount = transaction.Amount - oldTransaction.Amount;
                        childTransaction.Balance = account.Balance - childTransaction.Amount;
                        account.Balance = childTransaction.Balance;
                        var childCategory = db.Categories.Find(childTransaction.CategoryId);
                        if (oldTransaction.Category.AccountBudgetId != null)
                        {
                            var budget = db.AccountBudget.Find(oldTransaction.Category.AccountBudgetId);
                            if (budget.Expense == true)
                            {
                                budget.Leftover -= childTransaction.Amount;
                            }
                            else
                            {
                                budget.Collected += childTransaction.Amount;
                            }
                        }
                    }
                    else
                    {
                        childTransaction.CategoryId = db.Categories.FirstOrDefault(u => u.AccountId == transaction.AccountId && u.Name.Contains("(+)")).id;
                        childTransaction.Amount = oldTransaction.Amount - transaction.Amount;
                        childTransaction.Balance = account.Balance + childTransaction.Amount;
                        account.Balance = childTransaction.Balance;
                        var childCategory = db.Categories.Find(childTransaction.CategoryId);
                        if (oldTransaction.Category.AccountBudgetId != null)
                        {
                            var budget = db.AccountBudget.Find(oldTransaction.Category.AccountBudgetId);
                            if (budget.Expense == true)
                            {
                                budget.Leftover += childTransaction.Amount;
                            }
                            else
                            {
                                budget.Collected -= childTransaction.Amount;
                            }
                        }
                    }
                }
                else
                {
                    //if reconciled amount is bigger than old transaction amount
                    if (oldTransaction.Amount < transaction.Amount)
                    {
                        childTransaction.CategoryId = db.Categories.FirstOrDefault(u => u.AccountId == transaction.AccountId && u.Name.Contains("(+)")).id;
                        childTransaction.Amount = transaction.Amount - oldTransaction.Amount;
                        childTransaction.Balance = account.Balance + childTransaction.Amount;
                        account.Balance = childTransaction.Balance;
                        var childCategory = db.Categories.Find(childTransaction.CategoryId);
                        if (oldTransaction.Category.AccountBudgetId != null)
                        {
                            var budget = db.AccountBudget.Find(oldTransaction.Category.AccountBudgetId);
                            if (budget.Expense == true)
                            {
                                budget.Leftover -= childTransaction.Amount;
                            }
                            else
                            {
                                budget.Collected += childTransaction.Amount;
                            }
                        }
                    }
                    else
                    {
                        childTransaction.CategoryId = db.Categories.FirstOrDefault(u => u.AccountId == transaction.AccountId && u.Name.Contains("(-)")).id;
                        childTransaction.Amount = oldTransaction.Amount - transaction.Amount;
                        childTransaction.Balance = account.Balance - childTransaction.Amount;
                        account.Balance = childTransaction.Balance;
                        var childCategory = db.Categories.Find(childTransaction.CategoryId);
                        if (oldTransaction.Category.AccountBudgetId != null)
                        {
                            var budget = db.AccountBudget.Find(oldTransaction.Category.AccountBudgetId);
                            if (budget.Expense == true)
                            {
                                budget.Leftover += childTransaction.Amount;
                            }
                            else
                            {
                                budget.Collected -= childTransaction.Amount;
                            }
                        }
                    }
                }

                db.Transaction.Add(childTransaction);
                db.SaveChanges();
                var newReconcile = new Reconcile
                {
                    AccountId = account.id,
                    date = DateTimeOffset.Now,
                    TransactionId = childTransaction.id
                };
                db.Reconcile.Add(newReconcile);
                transaction.RecTransactionId = childTransaction.id;
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check your input fields and try again!" });
        }


        public ActionResult LoadAccounts(HouseholdCreateJoinMV model, int? id)
        {
            model.household = db.Household.Find(id);
            return PartialView("_Accounts", model);
        }

        public ActionResult LoadTransactions(int? id)
        {
            var account = db.Account.Find(id);
            return PartialView("_Transactions", account);
        }

        public ActionResult _loadDetails(int? id, int? month)
        {
            var account = db.Account.Find(id);
            if (month != null)
            {
                ViewBag.expense = account.Transactions.Where(u => u.Category.Expense == true && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == month+1).Sum(t => t.Amount);
                ViewBag.income = account.Transactions.Where(u => u.Category.Expense == false && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == month+1).Sum(t => t.Amount);
                ViewBag.reconciled = account.Transactions.Where(u => u.Reconciled == true && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == month+1).Sum(t => t.Amount);
            }
            else
            {
            ViewBag.expense = account.Transactions.Where(u => u.Category.Expense == true && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == DateTime.Now.Month).Sum(t => t.Amount);
            ViewBag.income = account.Transactions.Where(u => u.Category.Expense == false && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == DateTime.Now.Month).Sum(t => t.Amount);
            ViewBag.reconciled = account.Transactions.Where(u => u.Reconciled == true && u.Void == false && u.isDeleted == false && u.TransactionDate.Month == DateTime.Now.Month).Sum(t => t.Amount);
            }
            return PartialView("_loadDetails", account);
        }

        public ActionResult _LatestTransactions(int? id)
        {
            if (id != null)
            {
                var household = db.Household.Find(id);
                if (household != null)
                {
                    var tList = new List<Transactions>();
                    foreach (var n in household.Accounts.Where(u => u.isDeleted == false))
                    {
                        var transactions = n.Transactions.Where(u => u.isDeleted == false).ToList();
                        tList.AddRange(transactions);
                    }
                    ViewBag.transactions = tList.OrderByDescending(u => u.id).Take(5);
                    return PartialView("_LatestTransactions");
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult Categorylist(int? id)
        {
            var categories = db.Categories.Where(u => u.AccountId == id).ToArray();
            object[] list = new object[categories.Length];
            int num = 0;
            foreach (var type in categories)
            {
                list[num] = new { id = type.id, name = type.Name };
                num++;
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        //Get
        public ActionResult EditTransactions(int? id)
        {
            var transaction = db.Transaction.Find(id);
            var category = new List<Category>();
            if (transaction.Category.Name.Contains("Reconciliation"))
            {
                category = db.Categories.Where(u => u.Name.Contains("Reconciliation") && u.AccountId == transaction.AccountId).ToList();
            }
            else
            {
                category = db.Categories.Where(u => u.AccountId == transaction.AccountId).ToList();
                foreach (var entry in category.ToList())
                {
                    if (entry.Name.Contains("Reconciliation"))
                    {
                        category.Remove(entry);
                    }
                }
            }
            ViewBag.categoryList = new SelectList(category, "id", "Name", transaction.CategoryId);
            return View(transaction);
        }

        //Post for edit transactions
        [HttpPost]
        public ActionResult SubmitChanges(Transactions transaction)
        {
            var account = db.Account.Find(transaction.AccountId);
            if (ModelState.IsValid)
            {
                var category = db.Categories.Find(transaction.CategoryId);
                if (category.Name.Contains("Reconciliation"))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                else
                {
                    var oldtransaction = db.Transaction.AsNoTracking().FirstOrDefault(u => u.id == transaction.id);
                    db.Transaction.Attach(transaction);
                    db.Entry(transaction).State = EntityState.Modified;
                    transaction.UserUpdateId = User.Identity.GetUserId();
                    transaction.AuthorId = oldtransaction.AuthorId;
                    transaction.Created = oldtransaction.Created;
                    transaction.Void = false;
                    var oldCategory = db.Categories.Find(oldtransaction.CategoryId);
                    decimal amount = oldtransaction.Amount;

                    if (oldCategory.Expense == false)
                    {
                        amount = (oldtransaction.Amount * -1);
                    }
                    account.Balance = account.Balance + amount;

                    if (oldCategory.AccountBudgetId != null)
                    {
                        var budget = db.AccountBudget.Find(oldCategory.AccountBudgetId);
                        if (budget.Expense == true)
                        {
                            budget.Leftover += amount;
                        }
                        else
                        {
                            budget.Collected += amount;
                        }
                    }


                    amount = transaction.Amount;
                    if (category.Expense == true)
                    {
                        amount = (transaction.Amount * -1);
                    }

                    if (category.AccountBudgetId != null)
                    {
                        var budget = db.AccountBudget.Find(category.AccountBudgetId);
                        if (budget.Expense == true)
                        {
                            budget.Leftover += amount;
                        }
                        else
                        {
                            budget.Collected += amount;
                        }
                    }

                    account.Balance = account.Balance + amount;
                    transaction.Balance = account.Balance;
                    db.SaveChanges();
                    return Json(new { success = true });
                }
            }
            return Json(new { success = false, errorMessage = "Please check your input fields and try again!" });
        }

        public ActionResult AddTransaction(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var account = db.Account.Find(id);
            if (account == null)
            {
                return HttpNotFound();
            }
            var category = db.Categories.Where(u => u.AccountId == account.id).ToList();
            foreach (var entry in category.ToList())
            {
                if (entry.Name.Contains("Reconciliation"))
                {
                    category.Remove(entry);
                }
            }
            ViewBag.CategoryList = new SelectList(category, "id", "Name");
            return View("AddTransaction", account);
        }

        [HttpPost]
        public ActionResult AddTransaction(Transactions transaction)
        {
            object data;
            if (ModelState.IsValid)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                transaction.AuthorId = User.Identity.GetUserId();
                transaction.Created = DateTimeOffset.Now;
                transaction.Reconciled = false;
                transaction.Void = false;
                db.Transaction.Add(transaction);
                var account = db.Account.Find(transaction.AccountId);
                var category = db.Categories.Find(transaction.CategoryId);
                decimal amount = transaction.Amount;
                if (category.Expense == true)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                transaction.Balance = account.Balance;
                if (category.AccountBudgetId != null)
                {
                    var budget = db.AccountBudget.Find(category.AccountBudgetId);
                    if (budget.Expense == true)
                    {
                        budget.Leftover += amount;
                    }
                    else
                    {
                        budget.Collected += amount;
                    }
                }

                db.SaveChanges();
                //Create anonymous object to respond back with Json
                return Json(new { success = true });
            }
            data = new
            {
                success = false,
                errorMessage = "Please check your input fields and try again!"
            };
            return Json(new { success = false });
        }


        public ActionResult VoidTransaction(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var transaction = db.Transaction.Find(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }
            var account = db.Account.Find(transaction.AccountId);
            var user = db.Users.Find(User.Identity.GetUserId());
            if (user.HouseholdId != account.HouseholdId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            return View(transaction);
        }

        [HttpPost]
        public ActionResult VoidTransaction(int id)
        {
            var transaction = db.Transaction.Find(id);
            var account = db.Account.Find(transaction.AccountId);
            var user = db.Users.Find(User.Identity.GetUserId());
            var category = db.Categories.Find(transaction.CategoryId);
            decimal amount = transaction.Amount;
            if (transaction.Void == true)
            {
                transaction.Void = false;
                //Set reconciled to true for all other transactions prior to this reconciled transaction
                if (transaction.Category.Name.Contains("Reconciliation"))
                {
                    //var currentReconcile = db.Reconcile.FirstOrDefault(u => u.TransactionId == transaction.id);
                    //var lastreconcile = account.ReconcileDate.LastOrDefault(u => u.TransactionId < currentReconcile.TransactionId);
                    //if (lastreconcile == null)
                    //{
                    //    foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                    //    {
                    //        entry.Reconciled = true;
                    //    }
                    //}
                    //else
                    //{
                    //    var lastRtransaction = db.Transaction.Find(lastreconcile.TransactionId);
                    //    if (lastRtransaction != null)
                    //    {
                    //        foreach (var entry in account.Transactions.Where(u => u.id > lastRtransaction.id && u.id < transaction.id))
                    //        {
                    //            entry.Reconciled = true;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                    //        {
                    //            entry.Reconciled = true;
                    //        }
                    //    }
                    //}
                    var parentTransaction = db.Transaction.FirstOrDefault(u => u.RecTransactionId == transaction.id);
                    parentTransaction.RecTransactionId = null;
                    parentTransaction.Reconciled = false;
                }
                if (category.Expense == true)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                foreach (var entry in account.Transactions.Where(u => u.id > transaction.id))
                {
                    entry.Balance = entry.Balance + amount;
                }

                if (category.AccountBudgetId != null)
                {
                    var budget = db.AccountBudget.Find(category.AccountBudgetId);
                    if (budget.Expense == true)
                    {
                        budget.Leftover += amount;
                    }
                    else
                    {
                        budget.Collected += amount;
                    }
                }

                db.SaveChanges();
            }
            else
            {
                transaction.Void = true;
                //Set reconciled to false for all other transactions prior to this reconciled transaction
                if (transaction.Category.Name.Contains("Reconciliation"))
                {
                    //var currentReconcile = db.Reconcile.FirstOrDefault(u => u.TransactionId == transaction.id);
                    //var lastreconcile = account.ReconcileDate.LastOrDefault(u => u.TransactionId < currentReconcile.TransactionId);
                    //if (lastreconcile == null)
                    //{
                    //    foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                    //    {
                    //        entry.Reconciled = false;
                    //    }
                    //}
                    //else
                    //{
                    //    var lastRtransaction = db.Transaction.Find(lastreconcile.TransactionId);
                    //    if (lastRtransaction != null)
                    //    {
                    //        foreach (var entry in account.Transactions.Where(u => u.id > lastRtransaction.id && u.id < transaction.id))
                    //        {
                    //            entry.Reconciled = false;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                    //        {
                    //            entry.Reconciled = false;
                    //        }
                    //    }
                    //}
                    var parentTransaction = db.Transaction.FirstOrDefault(u => u.RecTransactionId == transaction.id);
                    parentTransaction.RecTransactionId = transaction.id;
                    parentTransaction.Reconciled = true;
                }
                if (category.Expense == false)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                foreach (var entry in account.Transactions.Where(u => u.id > transaction.id))
                {
                    entry.Balance = entry.Balance + amount;
                }

                if (category.AccountBudgetId != null)
                {
                    var budget = db.AccountBudget.Find(category.AccountBudgetId);
                    if (budget.Expense == true)
                    {
                        budget.Leftover += amount;
                    }
                    else
                    {
                        budget.Collected += amount;
                    }
                }
                db.SaveChanges();
            }
            return Json(new { success = true });
        }

        public ActionResult DeleteTransaction(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var transaction = db.Transaction.Find(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }
            if (transaction.isDeleted == true)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            return View(transaction);
        }

        [HttpPost]
        public ActionResult DeleteConfirm(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var transaction = db.Transaction.Find(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }
            var account = db.Account.Find(transaction.AccountId);
            var category = db.Categories.Find(transaction.CategoryId);
            if (transaction.Void == false)
            {
                decimal amount = transaction.Amount;
                if (category.Expense == false)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                transaction.Balance = account.Balance;
                foreach (var entry in account.Transactions.Where(u => u.id > transaction.id))
                {
                    entry.Balance = entry.Balance + amount;
                }

                if (category.AccountBudgetId != null)
                {
                    var budget = db.AccountBudget.Find(category.AccountBudgetId);
                    if (budget.Expense == true)
                    {
                        budget.Leftover += amount;
                    }
                    else
                    {
                        budget.Collected += amount;
                    }
                }
            }
            if (transaction.Category.Name.Contains("Reconciliation"))
            {
                //var currentReconcile = db.Reconcile.FirstOrDefault(u => u.TransactionId == transaction.id);
                //var lastreconcile = account.ReconcileDate.LastOrDefault(u => u.TransactionId < currentReconcile.TransactionId);
                //if (lastreconcile == null)
                //{
                //    foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                //    {
                //        entry.Reconciled = false;
                //    }
                //}
                //else
                //{
                //    var lastRtransaction = db.Transaction.Find(lastreconcile.TransactionId);
                //    if (lastRtransaction != null)
                //    {
                //        foreach (var entry in account.Transactions.Where(u => u.id > lastRtransaction.id && u.id < transaction.id))
                //        {
                //            entry.Reconciled = false;
                //        }
                //    }
                //    else
                //    {
                //        foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                //        {
                //            entry.Reconciled = false;
                //        }
                //    }
                //}
                var parentTransaction = db.Transaction.FirstOrDefault(u => u.RecTransactionId == transaction.id);
                if (parentTransaction != null)
                {
                    parentTransaction.RecTransactionId = null;
                    parentTransaction.Reconciled = false;

                    if (parentTransaction.Category.AccountBudgetId != null)
                    {
                        decimal amount = transaction.Amount;
                        if (category.Expense == false)
                        {
                            amount = (transaction.Amount * -1);
                        }

                        var budget = db.AccountBudget.Find(parentTransaction.Category.AccountBudgetId);
                        if (budget.Expense == true)
                        {
                            budget.Leftover += amount;
                        }
                        else
                        {
                            budget.Collected += amount;
                        }
                    }

                }
            }
            transaction.isDeleted = true;
            db.SaveChanges();
            return Json(new { success = true });
        }

        /// 
        /// Categories section
        /// 
        /// 

        public ActionResult _LoadCategories(int? id)
        {
            var account = db.Account.Find(id);
            var catList = db.Categories.Where(u => u.AccountId == account.id && !u.Name.Contains("Reconciliation"));
            return PartialView(catList);
        }

        public ActionResult EditCategories(int? id)
        {
            if (id != null)
            {
                var account = db.Account.Find(id);
                if (account != null)
                {
                    var catList = db.Categories.Where(u => u.AccountId == account.id && !u.Name.Contains("Reconciliation"));
                    ViewBag.AccountId = id;
                    return View(catList);
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost]
        public ActionResult EditCategory(Category category)
        {
            if (ModelState.IsValid)
            {
                var oldCategory = db.Categories.AsNoTracking().FirstOrDefault(u => u.id == category.id);
                var account = db.Account.Find(category.AccountId);
                db.Categories.Attach(category);
                db.Entry(category).Property("Name").IsModified = true;
                db.Entry(category).Property("Expense").IsModified = true;

                if (category.Expense != oldCategory.Expense)
                {
                    foreach (var entry in account.Transactions.Where(u => u.CategoryId == category.id))
                    {
                        decimal amount = entry.Amount;
                        if (category.Expense == true)
                        {
                            amount = (entry.Amount * -1);
                        }
                        account.Balance = account.Balance + amount;
                        entry.Balance = account.Balance;
                        if (entry.Void == false)
                        {
                            foreach (var n in account.Transactions.Where(u => u.id > entry.id))
                            {
                                n.Balance = entry.Balance + amount;
                            }
                        }
                    }
                }
                db.SaveChanges();
                return Json(new { success = true });
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult DeleteCategory(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var category = db.Categories.Find(id);
            if (category == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var account = db.Account.Find(category.AccountId);
            if (user.HouseholdId != account.HouseholdId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            return View(category);
        }

        [HttpPost]
        public ActionResult ConfirmCategoryDelete(int? id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var category = db.Categories.Find(id);
            var account = db.Account.Find(category.AccountId);
            if (category == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            if (user.HouseholdId != account.HouseholdId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var checkCategory = db.Transaction.FirstOrDefault(u => u.CategoryId == category.id);
            if (checkCategory != null)
            {
                return Json(new { success = false, errorMessage = "Please make sure this category is not being used by any transaction before deleting!" });
            }
            db.Categories.Remove(category);
            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult AddCategory(Category category)
        {
            if (ModelState.IsValid)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                var account = db.Account.Find(category.AccountId);
                if (account == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                if (user.HouseholdId != account.HouseholdId)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                var nameCheck = db.Categories.FirstOrDefault(u => u.Name == category.Name && u.AccountId == category.AccountId);
                if (nameCheck != null)
                {
                    return Json(new { success = false, errorMessage = "Please make sure your category name is unique." });
                }
                else
                {
                    db.Categories.Add(category);
                    db.SaveChanges();
                    return Json(new { success = true });
                }
            }
            return Json(new { success = false, errorMessage = "Modelstate is not valid." });
        }

        /// 
        /// Household Budget section
        /// 
        /// 

        public ActionResult _loadBudget(int? id)
        {
            if (id != null)
            {
                var household = db.Household.Find(id);
                if (household != null)
                {
                    //object[] listObject = new object[household.Budgets.Count];
                    //int counter = 0;
                    //foreach(var i in household.Budgets)
                    //{
                    //    decimal accountSum = 0;
                    //    if (i.AccountBudget.Any())
                    //    {
                    //       accountSum = i.AccountBudget.Sum(u => u.Amount);
                    //    }
                    //    listObject[counter] = new { budget = i, leftover = i.Amount - accountSum };
                    //    counter++;
                    //}
                    //ViewBag.budgetlist = listObject;
                    var list = new List<HouseholdBudgetVM.Details>();
                    foreach (var budget in household.Budgets.ToList())
                    {
                        decimal Sum = 0;
                        foreach (var ab in budget.AccountBudget.ToList())
                        {
                            Sum += ab.Amount;
                        }
                        list.Add(new HouseholdBudgetVM.Details { id = budget.id, value = budget.Amount - Sum });
                    }
                    var model = new HouseholdBudgetVM
                    {
                        Household = household,
                        List = list
                    };
                    return PartialView("_loadBudget", model);
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult CreateHouseholdBudget(int? id)
        {
            if (id != null)
            {
                var household = db.Household.Find(id);
                if (household != null)
                {
                    var user = db.Users.Find(User.Identity.GetUserId());
                    if (user.HouseholdId == household.id)
                    {
                        ViewBag.householdid = household.id;
                        return View();
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }


        [HttpPost]
        public ActionResult CreateHouseholdBudget(Budget budget)
        {
            if (ModelState.IsValid)
            {
                budget.Created = DateTimeOffset.Now;
                budget.AuthorId = User.Identity.GetUserId();
                budget.Leftover = budget.Amount;
                db.Budget.Add(budget);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult EditHouseholdBudget(int? id)
        {
            if (id != null)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                var budget = db.Budget.Find(id);
                if (budget != null)
                {
                    if (user.HouseholdId != budget.HouseholdId)
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        return View(budget);
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost]
        public ActionResult EditHouseholdBudget(Budget budget)
        {
            if (ModelState.IsValid)
            {
                db.Budget.Attach(budget);
                db.Entry(budget).Property("Amount").IsModified = true;
                db.Entry(budget).Property("Leftover").IsModified = true;
                db.Entry(budget).Property("Description").IsModified = true;
                budget.Leftover = budget.Amount;
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check your fields!" });
        }

        public ActionResult DeleteHouseholdBudget(int? id)
        {
            if (id != null)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                var budget = db.Budget.Find(id);
                if (budget != null)
                {
                    if (user.HouseholdId != budget.HouseholdId)
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        return View(budget);
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost, ActionName("DeleteHouseholdBudget")]
        public ActionResult Confirm_DeleteHouseholdBudget(int? id)
        {
            var budget = db.Budget.Find(id);
            db.Budget.Remove(budget);
            db.SaveChanges();
            return Json(new { success = true });
        }

        /// 
        /// Account Budget section
        /// 
        /// 

        public ActionResult _loadAccountChart(int? id)
        {
            var account = db.Account.Find(id);
            if (account != null)
            {
                if (!account.AccountBudget.Any())
                {
                    ViewBag.budgetmsg = true;
                }
                return PartialView("_loadAccountChart", account);
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }


        public ActionResult _loadAccountDonut(int? id)
        {
            var account = db.Account.Find(id);
            if (account != null)
            {
                return PartialView("_loadAccountDonut", account);
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult _loadAccountBudget(int? id, int? month)
        {
            if (id != null)
            {
                var account = db.Account.Find(id);
                if (account != null)
                {
                    var list = new List<AccountBudgetVM.Details>();
                    var budgetlist = db.AccountBudget.Where(u => u.AccountId == account.id);
                    foreach (var budget in budgetlist.ToList())
                    {
                        decimal Sum = 0;
                        foreach (var cat in budget.Category)
                        {
                            var trans = new List<Transactions>();
                            if (month != null)
                            {
                                trans = db.Transaction.Where(u => u.CategoryId == cat.id &&
                                u.isDeleted == false &&
                                u.Void == false &&
                                u.TransactionDate.Month == month+1)
                                .ToList();
                            }
                            else
                            {
                                trans = db.Transaction.Where(u => u.CategoryId == cat.id &&
                                u.isDeleted == false &&
                                u.Void == false &&
                                u.TransactionDate.Month == DateTime.Now.Month)
                                .ToList();
                            }
                            var childtrans = trans.Where(u => u.RecTransactionId != null).ToList();
                            foreach (var t in childtrans)
                            {
                                var d = db.Transaction.FirstOrDefault(u => u.id == t.RecTransactionId);
                                if (d.Category.Expense == true)
                                {
                                    Sum -= d.Amount;
                                }
                                else
                                {
                                    Sum += d.Amount;
                                }
                            }
                            foreach (var t in trans)
                            {
                                if (t.Category.Expense == true)
                                {
                                    Sum -= t.Amount;
                                }
                                else
                                {
                                    Sum += t.Amount;
                                }
                            }

                            //Sum += trans.Sum(u => u.Amount);
                        }
                        list.Add(new AccountBudgetVM.Details { id = budget.id, value = Sum });
                    }
                    var model = new AccountBudgetVM
                    {
                        Account = account,
                        List = list
                    };
                    return PartialView("_loadAccountBudget", model);
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        public ActionResult CreateAccountBudget(int? id)
        {
            if (id != null)
            {
                var account = db.Account.Find(id);
                if (account != null)
                {
                    var user = db.Users.Find(User.Identity.GetUserId());
                    if (user.HouseholdId == account.HouseholdId)
                    {
                        SelectList budgetlist = new SelectList(db.Budget.Where(u => u.HouseholdId == account.HouseholdId), "id", "Description");
                        MultiSelectList categorylist = new MultiSelectList(db.Categories.Where(u => u.AccountId == account.id && !u.Name.Contains("Reconciliation") && u.AccountBudgetId == null), "id", "Name");
                        ViewBag.budgetlist = budgetlist;
                        ViewBag.categorylist = categorylist;
                        return View(account);
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost]
        public ActionResult CreateAccountBudget(AccountBudget budget, List<int>SelectCategory, int? SelectBudget)
        {
            if (ModelState.IsValid)
            { 
                budget.Created = DateTimeOffset.Now;
                budget.AuthorId = User.Identity.GetUserId();
                if (SelectBudget != null)
                {
                    var parentBudget = db.Budget.Find(SelectBudget);
                    decimal leftover = 0;
                    decimal totalAmountLeft = 0;
                    var childList = db.AccountBudget.Where(u => u.BudgetId == parentBudget.id);
                    if (childList.Any())
                    {
                        totalAmountLeft = childList.Sum(t => t.Amount);
                    }
                    if (totalAmountLeft >= parentBudget.Amount)
                    {
                        return Json(new { success = false, errorMessage = "Not enough leftover money to budget from the chosen source!" });
                    }
                    else
                    {
                        leftover = parentBudget.Amount - totalAmountLeft;
                        if (budget.Amount >= leftover)
                        {
                            return Json(new { success = false, errorMessage = ("Not enough leftover money to budget from the chosen source! Please try to keep the amount under " + leftover) });
                        }
                    }
                    budget.BudgetId = SelectBudget;
                    budget.Expense = parentBudget.Expense;
                }
                budget.Leftover = budget.Amount;
                db.AccountBudget.Add(budget);
                //Assigns categories to the account budget to be monitored
                if (SelectCategory.Any())
                {
                    foreach (var entry in SelectCategory)
                    {
                        if (entry != 0)
                        {
                        var category = db.Categories.Find(entry);
                        category.AccountBudgetId = budget.id;
                        var transactions = db.Transaction.Where(u => u.CategoryId == category.id && u.isDeleted == false && u.Void == false && u.TransactionDate.Month == DateTime.Now.Month).ToList();
                        budget.Collected += transactions.Sum(u => u.Amount);
                        budget.Leftover -= transactions.Sum(u => u.Amount);

                        var childlist = new List<Transactions>();
                        foreach (var t in transactions)
                        {
                            if (t.RecTransactionId != null)
                            {
                                var child = db.Transaction.Find(t.RecTransactionId);
                                childlist.Add(child);
                            }
                        }
                        budget.Collected += childlist.Sum(u => u.Amount);
                        budget.Leftover -= childlist.Sum(u => u.Amount);
                        }
                        else
                        {
                            return Json(new { success = false, errorMessage = "Can't create a budget without transactions to monitor!" });
                        }
                    }
                }
                else
                {
                    return Json(new { success = false, errorMessage = "Can't create a budget without transactions to monitor!" });
                }
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check all of your fields!" });
        }

        public ActionResult EditAccountBudget(int? id)
        {
            if (id != null)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                var budget = db.AccountBudget.Find(id);
                if (budget != null)
                {
                    var account = db.Account.Find(budget.AccountId);
                    if (user.HouseholdId != account.HouseholdId)
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        var sourcelist = db.Budget.Where(u => u.HouseholdId == account.HouseholdId).ToList();
                        sourcelist.Add(new Budget { id = 0, Description = "Personal" });
                        SelectList budgetlist = new SelectList(sourcelist.OrderBy(u => u.id), "id", "Description", budget.BudgetId);
                        var selected = db.Categories.Where(u => u.AccountBudgetId == budget.id);
                        var selectedlist = new List<int>();
                        foreach (var category in selected)
                        {
                            selectedlist.Add(category.id);
                        }
                        MultiSelectList categorylist = new MultiSelectList(db.Categories.Where(u => u.AccountId == account.id && !u.Name.Contains("Reconciliation")), "id", "Name", selectedlist);
                        ViewBag.budgetlist = budgetlist;
                        ViewBag.categorylist = categorylist;
                        return View(budget);
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost]
        public ActionResult EditAccountBudget(AccountBudget budget, List<int> SelectCategory, int? SelectBudget)
        {
            if (ModelState.IsValid)
            {
                db.AccountBudget.Attach(budget);
                var oldAccountBudget = db.AccountBudget.AsNoTracking().FirstOrDefault(u => u.id == budget.id);
                var categorylist = db.Categories.Where(u => u.AccountBudgetId == budget.id);

                foreach (var n in categorylist.ToList())
                {
                    n.AccountBudgetId = null;
                }

                foreach (var n in SelectCategory)
                {
                    var category = db.Categories.Find(n);
                    category.AccountBudgetId = budget.id;
                }

                if (SelectBudget != oldAccountBudget.BudgetId)
                {
                    var parentBudget = db.Budget.Find(SelectBudget);
                    if (parentBudget != null)
                    {
                        decimal leftover = 0;
                        decimal totalAmountLeft = 0;
                        var childList = db.AccountBudget.Where(u => u.BudgetId == parentBudget.id);
                        if (childList.Any())
                        {
                            totalAmountLeft = childList.Sum(t => t.Amount);
                        }
                        if (totalAmountLeft >= parentBudget.Amount)
                        {
                            return Json(new { success = false, errorMessage = "Not enough leftover money to budget from the chosen source!" });
                        }
                        else
                        {
                            leftover = parentBudget.Amount - totalAmountLeft;
                            if (budget.Amount >= leftover)
                            {
                                return Json(new { success = false, errorMessage = ("You're not allowed to budget more than what is available from the household source! Please try to keep the amount under " + leftover) });
                            }
                        }
                        budget.BudgetId = SelectBudget;
                        budget.Expense = parentBudget.Expense;
                    }
                    else
                    {
                        budget.BudgetId = null;
                    }
                }

                db.Entry(budget).Property("Amount").IsModified = true;
                db.Entry(budget).Property("Expense").IsModified = true;
                db.Entry(budget).Property("Description").IsModified = true;
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check your fields!" });
        }

        public ActionResult DeleteAccountBudget(int? id)
        {
            if (id != null)
            {
                var user = db.Users.Find(User.Identity.GetUserId());
                var budget = db.AccountBudget.Find(id);
                if (budget != null)
                {
                    var account = db.Account.Find(budget.AccountId);
                    if (user.HouseholdId != account.HouseholdId)
                    {
                        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        return View(budget);
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }

        [HttpPost, ActionName("DeleteAccountBudget")]
        public ActionResult Confirm_DeleteAccountBudget(int? id)
        {
            var budget = db.AccountBudget.Find(id);
            var categorylist = db.Categories.Where(u => u.AccountBudgetId == budget.id);
            foreach (var n in categorylist.ToList())
            {
                n.AccountBudgetId = null;
            }
            db.AccountBudget.Remove(budget);
            db.SaveChanges();
            return Json(new { success = true });
        }
    }
}
