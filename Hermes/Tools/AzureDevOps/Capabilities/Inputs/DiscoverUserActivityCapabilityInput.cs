using System.Text.Json.Serialization;
using Hermes.Tools.Models;

namespace Hermes.Tools.AzureDevOps.Capabilities.Inputs
{
	/// <summary>
	/// Input model for discovering user pull request activity in Azure DevOps.
	/// </summary>
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class DiscoverUserActivityCapabilityInput : ToolCapabilityInputBase
	{
		/// <summary>
		/// The email address of the user to discover activity for.
		/// </summary>
		[JsonPropertyName("userEmail")]
		public string UserEmail { get; init; } = string.Empty;

		/// <summary>
		/// Number of days to look back for activity. Default is 7.
		/// </summary>
		[JsonPropertyName("daysBack")]
		public int DaysBack { get; init; } = 7;

		/// <summary>
		/// Alias for DaysBack. Accepts "days" as an alternative parameter name.
		/// </summary>
		[JsonPropertyName("days")]
		public int Days
		{
			get => DaysBack;
			init => DaysBack = value > 0 ? value : DaysBack;
		}

		/// <summary>
		/// Alias for UserEmail. Accepts "author" as an alternative parameter name.
		/// </summary>
		[JsonPropertyName("author")]
		public string Author
		{
			get => UserEmail;
			init => UserEmail = !string.IsNullOrWhiteSpace(value) ? value : UserEmail;
		}

		/// <summary>
		/// Alias for UserEmail. Accepts "email" as an alternative parameter name.
		/// </summary>
		[JsonPropertyName("email")]
		public string Email
		{
			get => UserEmail;
			init => UserEmail = !string.IsNullOrWhiteSpace(value) ? value : UserEmail;
		}

		/// <summary>
		/// Alias for UserEmail. Accepts "user" as an alternative parameter name.
		/// </summary>
		[JsonPropertyName("user")]
		public string User
		{
			get => UserEmail;
			init => UserEmail = !string.IsNullOrWhiteSpace(value) ? value : UserEmail;
		}
	}
}
