using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class Budget
    {
        public int id { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public bool Expense { get; set; }
        public string AuthorId { get; set; }
        public int HouseholdId { get; set; }
        public decimal Leftover { get; set; }
        public decimal Collected { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public virtual Household Household { get; set; }
        public virtual ApplicationUser Author { get; set; }
        public virtual ICollection<AccountBudget> AccountBudget { get; set; }
    }
}