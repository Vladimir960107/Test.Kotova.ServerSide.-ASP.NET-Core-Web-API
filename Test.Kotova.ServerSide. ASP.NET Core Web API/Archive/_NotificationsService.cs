using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    public class _NotificationsService
    {
        private readonly ApplicationDBContextGeneralConstr _context;

        /*
        public LegacyAuthenticationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        */
        public _NotificationsService(ApplicationDBContextGeneralConstr context)
        {
            _context = context;
        }

    }
}
