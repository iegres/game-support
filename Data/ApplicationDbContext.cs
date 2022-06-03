using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameSupport.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<GameSupport.Models.Admin> Admin { get; set; }
        public DbSet<GameSupport.Models.Player> Player { get; set; }
        public DbSet<GameSupport.Models.Message> Message { get; set; }
    }
}
