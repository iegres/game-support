using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameSupport.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameSupport.Models
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<ApplicationDbContext>>()))
            {
                var admins = context.Users.ToList();
                for (int i = 0; i < admins.Count; i++)
                {
                    if (context.Admin.Any(a => a.Mail == admins[i].Email)) continue;
                    context.Admin.Add(
                        new Admin
                        {
                            Name = admins[i].UserName,
                            Mail = admins[i].Email,
                            Password = admins[i].PasswordHash
                        });
                }
                context.SaveChanges();
            }
        }
    }
}
