using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class Category
    {
        public int id { get; set; }
        public string Name { get; set; }
        public bool Expense { get; set; }
        public int AccountId { get; set; }
        public virtual Account Account { get; set; }
        public int? AccountBudgetId { get; set; }
        public virtual AccountBudget AccountBudget { get; set; }
    }
}