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

namespace Budgeter.Controllers
{
    [Authorize]
    [RequireHttps]
    public class HouseholdController : MyBaseController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Household
        public ActionResult Index(HouseholdCreateJoinMV model)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            model.user = user;
            model.household = db.Household.Find(user.HouseholdId);
            model.invitations = db.Invitations.Where(u => u.InvitedUserId == user.Id).ToList();
            if (model.household != null)
            {
                if (model.household.Invitations != null)
                {
                    ViewBag.Pending = model.household.Invitations.Where(u => u.Accepted == false && !model.household.Users.Any(b => b.Email == u.Email)).DistinctBy(x => x.Email).ToList();
                }
            }
            return View(model);
        }

        // GET: Household
        public ActionResult Invitations()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
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
        [HttpPost, ActionName("Index")]
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
                return RedirectToAction("Index");
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
            return RedirectToAction("Index");
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
                    Name = "Reconciliation (+)",
                    AccountId = account.id,
                    Expense = true
                };

                db.Categories.Add(Food);
                db.Categories.Add(Bills);
                db.Categories.Add(Check);
                db.Categories.Add(ReconcileIncome);
                db.Categories.Add(ReconcileExpense);
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, errorMessage = "Please check your input fields and try again!" });
        }

        [HttpPost]
        public ActionResult DeleteAccount(int id)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var account = db.Account.Find(id);
            //Checks if account is actually valid
            if (account != null)
            {
                //Checks if the account belongs to your household
                if (account.HouseholdId == user.HouseholdId)
                {
                    db.Account.Remove(account);
                    db.SaveChanges();
                    var data = new
                    {
                        success = true
                    };
                    return Json(data);
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
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
                    if (!account.Transactions.Any(u => u.Category.Name == "Reconciliation (+)"))
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
                    if (!account.Transactions.Any(u => u.Category.Name == "Reconciliation (-)"))
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
                var oldList = db.Transaction.AsNoTracking();
                var oldtransaction = oldList.FirstOrDefault(u => u.id == transaction.id);
                db.Transaction.Attach(transaction);
                db.Entry(transaction).State = EntityState.Modified;
                transaction.UserUpdateId = User.Identity.GetUserId();
                transaction.AuthorId = oldtransaction.AuthorId;
                transaction.Created = oldtransaction.Created;
                transaction.Void = false;
                var category = db.Categories.Find(transaction.CategoryId);
                var oldCategory = db.Categories.Find(oldtransaction.CategoryId);
                decimal amount = oldtransaction.Amount;
                if (oldCategory.Expense == false)
                {
                    amount = (oldtransaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;

                amount = transaction.Amount;
                if (category.Expense == true)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                transaction.Balance = account.Balance;
                db.SaveChanges();
                return Json(new { success = true});
            }
            return Json(new { success=false, errorMessage = "Please check your input fields and try again!" });
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
                if (category.Expense == true)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                foreach (var entry in account.Transactions.Where(u => u.id > transaction.id))
                {
                    entry.Balance = entry.Balance + amount;
                }
                transaction.Void = false;
                //Set reconciled to true for all other transactions prior to this reconciled transaction
                if (transaction.Category.Name.Contains("Reconciliation"))
                {
                    var currentReconcile = db.Reconcile.FirstOrDefault(u => u.TransactionId == transaction.id);
                    var lastreconcile = account.ReconcileDate.LastOrDefault(u => u.id < currentReconcile.id);
                    if (lastreconcile == null)
                    {
                        foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                        {
                            entry.Reconciled = true;
                        }
                    }
                    else
                    {
                        var lastRtransaction = db.Transaction.Find(lastreconcile.TransactionId);
                        if (lastRtransaction != null)
                        {
                            foreach (var entry in account.Transactions.Where(u => u.id > lastRtransaction.id && u.id < transaction.id))
                            {
                                entry.Reconciled = true;
                            }
                        }
                        else
                        {
                            foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                            {
                                entry.Reconciled = true;
                            }
                        }
                    }
                }
                db.SaveChanges();
            }
            else
            {
                if (category.Expense == false)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                foreach (var entry in account.Transactions.Where(u => u.id > transaction.id))
                {
                    entry.Balance = entry.Balance + amount;
                }
                transaction.Void = true;
                //Set reconciled to false for all other transactions prior to this reconciled transaction
                if (transaction.Category.Name.Contains("Reconciliation"))
                {
                    var currentReconcile = db.Reconcile.FirstOrDefault(u => u.TransactionId == transaction.id);
                    var lastreconcile = account.ReconcileDate.LastOrDefault(u => u.id < currentReconcile.id);
                    if (lastreconcile == null)
                    {
                        foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                        {
                            entry.Reconciled = false;
                        }
                    }
                    else
                    {
                        var lastRtransaction = db.Transaction.Find(lastreconcile.TransactionId);
                        if (lastRtransaction != null)
                        {
                            foreach (var entry in account.Transactions.Where(u => u.id > lastRtransaction.id && u.id < transaction.id))
                            {
                                entry.Reconciled = false;
                            }
                        }
                        else
                        {
                            foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                            {
                                entry.Reconciled = false;
                            }
                        }
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
            if (transaction.Void != true)
            {
                decimal amount = transaction.Amount;
                if (category.Expense == false)
                {
                    amount = (transaction.Amount * -1);
                }
                account.Balance = account.Balance + amount;
                transaction.Balance = account.Balance;
            }
            if (transaction.Category.Name.Contains("Reconciliation"))
            {
                var currentReconcile = db.Reconcile.FirstOrDefault(u => u.TransactionId == transaction.id);
                var lastreconcile = account.ReconcileDate.LastOrDefault(u => u.id < currentReconcile.id);
                if (lastreconcile == null)
                {
                    foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                    {
                        entry.Reconciled = false;
                    }
                }
                else
                {
                    var lastRtransaction = db.Transaction.Find(lastreconcile.TransactionId);
                    if (lastRtransaction != null)
                    {
                        foreach (var entry in account.Transactions.Where(u => u.id > lastRtransaction.id && u.id < transaction.id))
                        {
                            entry.Reconciled = false;
                        }
                    }
                    else
                    {
                        foreach (var entry in account.Transactions.Where(u => u.id < transaction.id))
                        {
                            entry.Reconciled = false;
                        }
                    }
                }
            }
            db.Transaction.Remove(transaction);
            db.SaveChanges();
            return Json(new { success = true });
        }
    }
}
