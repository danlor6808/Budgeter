using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class Reconcile
    {
        public int id { get; set; }
        public int AccountId { get; set; }
        public int? TransactionId { get; set; }
        public DateTimeOffset date { get; set; }
        public virtual Account Account { get; set; }
        public virtual Transactions Transaction { get; set; }
    }
}