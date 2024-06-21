using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    public class PingController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private ChiefsManager _chiefsManager;
        public PingController(IConfiguration configuration)
        {
            _configuration = configuration;

            _chiefsManager = new ChiefsManager(_configuration);
        }
        
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("ping/{chiefId}")] // НЕКРАСИВОЕ РЕШЕНИЕ С Ping/ping/chiefId, но вдруг chiefId == status/3 вместо ping, к примеру? хрен знает, вроде и пофиг, а вроде и уязвимость.
        public async Task<IActionResult> Ping(int chiefId)
        {
            await _chiefsManager.PingChiefAsync(chiefId); //PingChief обычный не сработал. А точнее - не работает получение статуса. Как-будто sessions некорректно создаются или что-то. Поэтому с базой данных.
            return Ok("Ping received for chief " + chiefId);
        }
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("status/{chiefId}")]
        public IActionResult CheckStatus(int chiefId)
        {
            bool isOnline = _chiefsManager.IsChiefOnline(chiefId);
            return Ok(isOnline ? "Chief is online." : "Chief is offline.");
        }
    }
}
