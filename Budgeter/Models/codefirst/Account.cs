using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class Account
    {
        public Account()
        {
            this.Transactions = new HashSet<Transactions>();
        }
        public int id { get; set; }
        public string Name { get; set; }
        public decimal Balance { get; set; }
        public int HouseholdId { get; set; }

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public virtual ICollection<Reconcile> ReconcileDate { get; set; }

        public virtual Household Household { get; set; }
        public virtual ICollection<Transactions> Transactions { get; set; }
    }
}