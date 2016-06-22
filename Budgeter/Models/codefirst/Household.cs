using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{ 
    public class Household
    {
        public Household()
        {
            this.Budgets = new HashSet<Budget>();
            this.Accounts = new HashSet<Account>();
            this.Users = new HashSet<ApplicationUser>();
            this.Invitations = new HashSet<Invitations>();
        }
        public int id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public bool isDeleted { get; set; }

        public virtual ICollection<Account> Accounts { get; set; }
        public virtual ICollection<Budget> Budgets { get; set; }
        public virtual ICollection<ApplicationUser> Users { get; set; }
        public virtual ICollection<Invitations> Invitations { get; set; }
    }
}