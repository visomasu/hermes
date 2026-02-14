namespace Hermes.Storage.Repositories.TeamConfiguration.Models
{
	/// <summary>
	/// Model for deserializing team configuration from appsettings.json.
	/// </summary>
	public class TeamSettings
	{
		/// <summary>
		/// Unique identifier for the team (e.g., "contact-center-ai").
		/// </summary>
		public string TeamId { get; set; } = string.Empty;

		/// <summary>
		/// Display name of the team (e.g., "Contact Center AI").
		/// </summary>
		public string TeamName { get; set; } = string.Empty;

		/// <summary>
		/// Current iteration path for the team (e.g., "OneCRM\\FY26\\Q3\\1Wk\\1Wk33").
		/// </summary>
		public string IterationPath { get; set; } = string.Empty;

		/// <summary>
		/// Area paths that belong to this team (e.g., ["OneCRM\\AI\\ContactCenter"]).
		/// </summary>
		public List<string> AreaPaths { get; set; } = new();

		/// <summary>
		/// Team-specific SLA rule overrides (e.g., {"Task": 3}).
		/// </summary>
		public Dictionary<string, int> SlaOverrides { get; set; } = new();
	}
}
