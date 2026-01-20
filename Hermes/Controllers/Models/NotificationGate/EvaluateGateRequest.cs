namespace Hermes.Controllers.Models.NotificationGate
{
	/// <summary>
	/// Request model for evaluating the gate.
	/// </summary>
	public class EvaluateGateRequest
	{
		public string TeamsUserId { get; set; } = string.Empty;
	}
}
