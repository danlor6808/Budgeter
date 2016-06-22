using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Budgeter.Models
{
    public class Invitations
    {
        public int id { get; set; }
        public int HouseholdId { get; set; }
        public bool Accepted { get; set; }
        public string InviterId { get; set; }
        public string InvitedUserId { get; set; }
        public string Code { get; set; }
        public string Email { get; set; }
        public DateTimeOffset Date { get; set; }
        public virtual Household Household { get; set; }
        public virtual ApplicationUser Inviter { get; set; }
        public virtual ApplicationUser InvitedUser { get; set; }
    }
}