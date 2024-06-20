using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class ChiefsManager
    {
        private Dictionary<int, ChiefSession> sessions = new Dictionary<int, ChiefSession>();

        public void PingChief(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                sessions[chiefId].ChiefPinged();
            }
            else
            {
                // If chief is not already registered, register and ping
                var session = new ChiefSession(chiefId);
                sessions.Add(chiefId, session);
                session.ChiefPinged();
            }
        }

        public bool IsChiefOnline(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                return sessions[chiefId].IsChiefOnline;
            }
            return false;
        }

        public void EndChiefSession(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                sessions[chiefId].EndSession();
                sessions.Remove(chiefId);
            }
        }
    }
}
