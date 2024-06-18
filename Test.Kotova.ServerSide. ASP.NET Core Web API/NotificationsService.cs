using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    public class NotificationsService
    {
        private readonly ApplicationDBContextGeneralConstr _context;

        /*
        public LegacyAuthenticationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        */
        public NotificationsService(ApplicationDBContextGeneralConstr context)
        {
            _context = context;
        }

    }
}
