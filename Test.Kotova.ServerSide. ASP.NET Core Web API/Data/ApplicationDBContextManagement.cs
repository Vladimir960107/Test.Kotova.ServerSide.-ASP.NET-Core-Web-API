using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBContextManagement : ApplicationDBContextBase
    {
        public ApplicationDBContextManagement(DbContextOptions<ApplicationDBContextManagement> options)
            : base(options)
        {
        }
    }
}
