using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class HouseholdCreateJoinMV
    {
        public ApplicationUser user { get; set; }
        public Household household { get; set; }
        public IEnumerable<Invitations> invitations { get; set; }
    }

    public class AccountBudgetVM
    {
        public class Details
        {
            public int id { get; set; }
            public decimal value { get; set; }
        }
        public Account Account { get; set; }
        public IEnumerable<Details> List { get; set; }
    }

    public class HouseholdBudgetVM
    {
        public class Details
        {
            public int id { get; set; }
            public decimal value { get; set; }
        }
        public Household Household { get; set; }
        public IEnumerable<Details> List { get; set; }
    }
}