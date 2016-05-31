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
        public int Frequency { get; set; }
        public string AuthorId { get; set; }
        public string UpdateUserId { get; set; }
        public int? CategoryId { get; set; }
        public int HouseholdId { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Updated { get; set; }

        public virtual Category Category { get; set; }
        public virtual Household Household { get; set; }
        public virtual ApplicationUser Author { get; set; }
        public virtual ApplicationUser UpdateUser { get; set; }


    }
}