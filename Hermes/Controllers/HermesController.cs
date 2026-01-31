using Hermes.Controllers.Models;
using Hermes.Controllers.Models.Instructions;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using Hermes.Orchestrator;
using Hermes.Storage.Repositories.HermesInstructions;

namespace Hermes.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class HermesController : ControllerBase
	{
		private readonly ILogger<HermesController> logger;
		private readonly IAgentOrchestrator _orchestrator;
		private readonly IHermesInstructionsRepository _instructionsRepository;

		/// <summary>
		/// Initializes a new instance of the <see cref="HermesController"/> class.
		/// </summary>
		/// <param name="logger">Logger.</param>
		/// <param name="orchestrator">Hermes orchestrator.</param>
		/// <param name="instructionsRepository">Hermes instructions repository.</param>
		public HermesController(ILogger<HermesController> logger, IAgentOrchestrator orchestrator, IHermesInstructionsRepository instructionsRepository)
		{
			this.logger = logger;
			this._orchestrator = orchestrator;
			this._instructionsRepository = instructionsRepository;
		}

		[HttpPost("v1.0/chat")]
		public async Task<IActionResult> Chat([FromHeader(Name = "x-ms-correlation-id")] string correlationId, [FromBody] ChatInput input)
		{
			this.logger.LogInformation("[{ClassName}] New chat requested.", nameof(HermesController));

			// If userId is provided, encode it in the sessionId for now (format: userId|sessionId)
			// This allows the orchestrator to extract user context without breaking existing interfaces
			// Use provided sessionId or generate a new one
			var actualSessionId = !string.IsNullOrWhiteSpace(input.SessionId)
				? input.SessionId
				: Guid.NewGuid().ToString();

			var sessionId = !string.IsNullOrWhiteSpace(input.UserId)
				? $"{input.UserId}|{actualSessionId}"
				: actualSessionId;

			var result = await _orchestrator.OrchestrateAsync(sessionId, input.Text);
			return Ok(result);
		}

		#region Instructions Endpoints

		/// <summary>
		/// Create a new Hermes instruction.
		/// </summary>
		[HttpPost("v1.0/instructions")]
		public async Task<IActionResult> CreateInstruction([FromBody] CreateInstructionRequest request)
		{
			await _instructionsRepository.CreateInstructionAsync(request.Instruction, request.InstructionType, request.Version);
			return Ok();
		}

		/// <summary>
		/// Update an existing Hermes instruction.
		/// </summary>
		[HttpPut("v1.0/instructions")]
		public async Task<IActionResult> UpdateInstruction([FromBody] UpdateInstructionRequest request)
		{
			await _instructionsRepository.UpdateInstructionAsync(request.InstructionType, request.NewInstruction, request.Version);
			return Ok();
		}

		/// <summary>
		/// Retrieve a Hermes instruction by type and optional version.
		/// </summary>
		[HttpGet("v1.0/instructions/{instructionType}")]
		public async Task<IActionResult> GetInstruction(HermesInstructionType instructionType, [FromQuery] int? version)
		{
			var result = await _instructionsRepository.GetByInstructionTypeAsync(instructionType, version);
			if (result == null)
				return NotFound();
			return Ok(result);
		}

		/// <summary>
		/// Delete a Hermes instruction by type and version.
		/// </summary>
		[HttpDelete("v1.0/instructions/{instructionType}")]
		public async Task<IActionResult> DeleteInstruction(HermesInstructionType instructionType, [FromQuery] int version)
		{
			await _instructionsRepository.DeleteInstructionAsync(instructionType, version);
			return Ok();
		}

		#endregion

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
				return Ok(); // Placeholder
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
