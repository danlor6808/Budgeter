namespace Budgeter.Migrations
{
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using Models;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Budgeter.Models.ApplicationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
        }

        protected override void Seed(Budgeter.Models.ApplicationDbContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data. E.g.
            //
            //    context.People.AddOrUpdate(
            //      p => p.FullName,
            //      new Person { FullName = "Andrew Peters" },
            //      new Person { FullName = "Brice Lambson" },
            //      new Person { FullName = "Rowan Miller" }
            //    );
            //
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));

            if (!context.Roles.Any(r => r.Name == "Administrator"))
            {
                roleManager.Create(new IdentityRole { Name = "Administrator" });
            }

            if (!context.Roles.Any(r => r.Name == "User"))
            {
                roleManager.Create(new IdentityRole { Name = "User" });
            }

            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));

            
            if (!context.Users.Any(u => u.Email == "danlor6808@gmail.com"))
            {
                userManager.Create(new ApplicationUser
                {
                    UserName = "danlor6808@gmail.com",
                    Email = "danlor6808@gmail.com",
                    FirstName = "Danny",
                    LastName = "Lorn",
                    DisplayName = "Danlor6808"
                }, "Password1");
            }

            var userId_SuperUser = userManager.FindByEmail("danlor6808@gmail.com").Id;
            userManager.AddToRole(userId_SuperUser, "Administrator");


            if (!context.Users.Any(u => u.Email == "guest@guest.com"))
            {
                userManager.Create(new ApplicationUser
                {
                    UserName = "guest@guest.com",
                    Email = "guest@guest.com",
                    FirstName = "guest",
                    LastName = "account",
                    DisplayName = "Guest"
                }, "Password1");
            }

            var userId = userManager.FindByEmail("guest@guest.com").Id;
            userManager.AddToRole(userId, "User");
        }
    }
}
