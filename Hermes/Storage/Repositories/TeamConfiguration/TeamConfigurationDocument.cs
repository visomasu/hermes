using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.TeamConfiguration
{
	/// <summary>
	/// Stores team-specific configuration for SLA notifications and newsletter generation.
	/// Each team has independent iteration windows, area paths, and SLA rule overrides.
	/// This document has no TTL and persists until explicitly deleted.
	/// </summary>
	public class TeamConfigurationDocument : Document
	{
		/// <summary>
		/// Unique identifier for the team (e.g., "contact-center-ai").
		/// Used as both Id and PartitionKey for single-document partition.
		/// </summary>
		public string TeamId { get; set; } = string.Empty;

		/// <summary>
		/// Display name of the team (e.g., "Contact Center AI").
		/// </summary>
		public string TeamName { get; set; } = string.Empty;

		/// <summary>
		/// Current iteration path for the team (e.g., "OneCRM\\FY26\\Q3\\1Wk\\1Wk33").
		/// Determines milestone window (weekly vs monthly) and current sprint.
		/// </summary>
		public string IterationPath { get; set; } = string.Empty;

		/// <summary>
		/// Area paths that belong to this team (e.g., ["OneCRM\\AI\\ContactCenter"]).
		/// Used for work item queries and team detection.
		/// </summary>
		public List<string> AreaPaths { get; set; } = new();

		/// <summary>
		/// Team-specific SLA rule overrides (e.g., {"Task": 3}).
		/// Merged with global defaults during SLA evaluation.
		/// </summary>
		public Dictionary<string, int> SlaOverrides { get; set; } = new();

		/// <summary>
		/// When this configuration was first created.
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// When this configuration was last updated.
		/// </summary>
		public DateTime? UpdatedAt { get; set; }

		/// <summary>
		/// No TTL - team configuration persists indefinitely.
		/// </summary>
		public new int? TTL { get; set; } = null;
	}
}
