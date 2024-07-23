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
        [HttpGet("ping-is-online/{chiefId}")] 
        public async Task<IActionResult> PingIsOnline(int chiefId)
        {
            await _chiefsManager.PingChiefAsync(chiefId); //PingChief обычный не сработал. А точнее - не работает получение статуса. Как-будто sessions некорректно создаются или что-то. Поэтому с базой данных.
            return Ok("Ping online received for chief " + chiefId);
        }
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("ping-is-offline/{chiefId}")] 
        public async Task<IActionResult> PingIsOffline(int chiefId)
        {
            await _chiefsManager.PingOfflineChiefAsync(chiefId);
            return Ok("Ping offline received for chief " + chiefId);
        }
        /*[Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("status/{chiefId}")]
        public async Task<IActionResult> CheckStatus(int chiefId)
        {
            bool isOnline = await _chiefsManager.IsChiefOnlineAsync(chiefId);
            return Ok(isOnline ? "Chief is online." : "Chief is offline.");
        }*/
    }
}
