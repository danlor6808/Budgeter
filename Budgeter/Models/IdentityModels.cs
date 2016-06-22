using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Collections.Generic;

namespace Budgeter.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            userIdentity.AddClaim(new Claim("HouseholdId", HouseholdId.ToString()));

            return userIdentity;
        }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }
        public int? HouseholdId { get; set; }
        public int? PreviousHouseholdId { get; set; }       
        public virtual Household Household { get; set; }

    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
        public DbSet<Household> Household { get; set; }
        public DbSet<Account> Account { get; set; }
        public DbSet<Transactions> Transaction { get; set; }
        public DbSet<Budget> Budget { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Invitations> Invitations { get; set; }
        public DbSet<Reconcile> Reconcile { get; set; }
        public DbSet<AccountBudget> AccountBudget { get; set; }

    }
}