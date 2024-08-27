using DocumentFormat.OpenXml.Bibliography;
using Kotova.CommonClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

public class Department_inNotification
{
    public string ChiefId { get; set; }
    public string ConnectionId { get; set; }
}

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ChiefsManager _chiefsManager;

        public NotificationHub(ChiefsManager chiefsManager)
        {
            _chiefsManager = chiefsManager;
        }

        public override async Task OnConnectedAsync()
        {
            var chiefId = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
            string? departmentId_string = Context.User?.FindFirst("department_id")?.Value;

            if (departmentId_string == null || chiefId == null || !int.TryParse(departmentId_string, out var departmentId))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Не удалось определить номер отдела или идентифицировать начальника.");
                Context.Abort();
                return;
            }

            if (!_chiefsManager.TrySignInChief(departmentId, chiefId, Context.ConnectionId))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Другой начальник департамента уже авторизирован.");
                Context.Abort();
                return;
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string? departmentId_string = Context.User?.FindFirst("department_id")?.Value;

            if (departmentId_string != null && int.TryParse(departmentId_string, out var departmentId))
            {
                _chiefsManager.TrySignOutChief(departmentId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }

}
