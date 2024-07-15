using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBContextGeneralConstr : ApplicationDBContextBase
    {
        public ApplicationDBContextGeneralConstr(DbContextOptions<ApplicationDBContextGeneralConstr> options)
            : base(options)
        {
        }
    }
}