using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class AccountBudget
    {
        public AccountBudget()
        {
            this.Category = new HashSet<Category>();
        }
        public int id { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public bool Expense { get; set; }
        public int Frequency { get; set; }
        public decimal Leftover { get; set; }
        public decimal Collected { get; set; }
        public string AuthorId { get; set; }
        public int AccountId { get; set; }
        public int? BudgetId { get; set; }
        public DateTimeOffset Created { get; set; }
        public virtual Account Account { get; set; }
        public virtual ApplicationUser Author { get; set; }
        public virtual ICollection<Category> Category { get; set; }
    }
}