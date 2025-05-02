using System.Data.Entity;
using Shop.EF;

namespace Shop.EF
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext() : base("DefaultConnection")
        {
        }

        public DbSet<Message> Messages { get; set; }
        public DbSet<AspNetUser> AspNetUsers { get; set; }

        public DbSet<AspNetUserRole> AspNetUserRoles { get; set;}
    }
}