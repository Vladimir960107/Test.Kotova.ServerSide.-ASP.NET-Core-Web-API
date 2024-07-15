using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBContextTechnicalDepartment : ApplicationDBContextBase
    {
        public ApplicationDBContextTechnicalDepartment(DbContextOptions<ApplicationDBContextTechnicalDepartment> options)
            : base(options)
        {
        }
    }
}