using System.Net.WebSockets;
using System.Threading.Tasks;

using DetAct.Data.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

//src: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-5.0
namespace DetAct.Data.Controller
{
    [ApiController]
    public class WebSocketController : ControllerBase
    {
        private readonly ILogger _logger;

        private readonly WebSocketService _webSocketService;

        public WebSocketController(WebSocketService webSocketService, ILogger<WebSocketController> logger)
            => (_webSocketService, _logger) = (webSocketService, logger);

        [Route("/ws/{category}/{name?}/{*pageRoute}")]
        [HttpGet]
        public async Task<IActionResult> Get(string category, string name, string pageRoute)
        {
            if(HttpContext.WebSockets.IsWebSocketRequest) {
                if(pageRoute is not null || name is null)
                    return NotFound();

                await HandleWebSocketRequest(category, name);
                _logger.LogDebug(message: $"{HttpContext.Connection.Id}-{HttpContext.Connection.RemoteIpAddress}:{HttpContext.Connection.RemotePort}=> closed WebSocket");

                return new EmptyResult();
            }
            else {
                _logger.LogDebug(message: $"{HttpContext.Connection.Id}-{HttpContext.Connection.RemoteIpAddress}:{HttpContext.Connection.RemotePort}=> got invalid request [{HttpContext.Request.Scheme}]");

                return LocalRedirect(localUrl: "/ws");
            }
        }

        private async Task HandleWebSocketRequest(string category, string name)
        {
            _logger.LogDebug(message: $"{HttpContext.Connection.Id}-{HttpContext.Connection.RemoteIpAddress}:{HttpContext.Connection.RemotePort}=> received WebSocket-request");
            await _webSocketService.AcceptWebSocketAsync(category, name, HttpContext);
        }
    }
}