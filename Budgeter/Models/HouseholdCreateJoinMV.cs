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
}