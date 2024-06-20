using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    public class PingController : ControllerBase
    {
        private static ChiefsManager _chiefsManager = new ChiefsManager();
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("ping/{chiefId}")] // НЕКРАСИВОЕ РЕШЕНИЕ С Ping/ping/chiefId, но вдруг chiefId == status/3 вместо ping, к примеру? хрен знает, вроде и пофиг, а вроде и уязвимость.
        public IActionResult Ping(int chiefId)
        {
            _chiefsManager.PingChief(chiefId);
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
