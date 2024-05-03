using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{


    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options){ }

        public DbSet<User> Users { get; set; }
    }

    public class ApplicationDBNotificationContext : DbContext
    {
        public ApplicationDBNotificationContext(DbContextOptions<ApplicationDBNotificationContext> option)
            : base(option) { }

        public DbSet<NotificationFromDB> notifications_FromDB { get; set; }
    } 
}
