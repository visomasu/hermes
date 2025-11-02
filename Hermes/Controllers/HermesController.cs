using Hermes.Controllers.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;

namespace Hermes.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HermesController : ControllerBase
    {
        private readonly ILogger<HermesController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HermesController"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        public HermesController(ILogger<HermesController> logger)
        {
            this.logger = logger;
        }

        [HttpPost("v1.0/chat")]
        public IActionResult Chat([FromHeader(Name = "x-ms-correlation-id")] string correlationId, [FromBody] ChatInput input)
        {
            this.logger.LogInformation("[{ClassName}] New chat requested.", nameof(HermesController));

            // For now, just echo the input text
            return Ok(new { reply = $"You said: {input.Text}" });
        }

        /// <summary>
        /// WebSocket endpoint for persistent chat connection.
        /// </summary>
        [HttpGet("ws")] // Typically accessed via ws:// or wss://
        public async Task<IActionResult> WebSocketEndpoint()
        {
            this.logger.LogInformation("[{ClassName}] WebSocket connection requested.", nameof(HermesController));

            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await EchoLoop(webSocket);
                return new EmptyResult();
            }
            else
            {
                return BadRequest("WebSocket connection expected.");
            }
        }

        /// <summary>
        /// Simple echo loop for WebSocket messages.
        /// </summary>
        private async Task EchoLoop(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                else
                {
                    var receivedText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var replyText = $"You said: {receivedText}";
                    var replyBuffer = Encoding.UTF8.GetBytes(replyText);
                    await webSocket.SendAsync(new ArraySegment<byte>(replyBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}
