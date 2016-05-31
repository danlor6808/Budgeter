using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class Transactions
    {
        public int id { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public bool Reconciled { get; set; }
        public bool Void { get; set; }
        public DateTimeOffset TransactionDate { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public int AccountId { get; set; }
        public string AuthorId { get; set; }
        public string UserUpdateId { get; set; }
        public int? CategoryId { get; set; }

        public virtual Account Account { get; set; }
        public virtual Category Category { get; set; }
        public virtual ApplicationUser Author { get; set; }
        public virtual ApplicationUser UserUpdate { get; set; }
    }
}