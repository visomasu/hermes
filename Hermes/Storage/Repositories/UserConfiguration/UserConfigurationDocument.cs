using Hermes.Storage.Core.Models;
using Hermes.Storage.Repositories.UserConfiguration.Models;

namespace Hermes.Storage.Repositories.UserConfiguration
{
	/// <summary>
	/// Stores user-specific configuration and preferences.
	/// This document has no TTL and persists until explicitly deleted.
	/// </summary>
	public class UserConfigurationDocument : Document
	{
		/// <summary>
		/// The Teams user ID.
		/// Used as both Id and PartitionKey for single-document partition.
		/// </summary>
		public string TeamsUserId { get; set; } = string.Empty;

		/// <summary>
		/// Notification preferences for this user.
		/// </summary>
		public NotificationPreferences Notifications { get; set; } = new();

		/// <summary>
		/// Work item update SLA notification registration profile.
		/// Null indicates user has never registered for SLA notifications.
		/// IsRegistered=false indicates user unregistered but data is preserved.
		/// </summary>
		public WorkItemUpdateSlaRegistrationProfile? SlaRegistration { get; set; }

		/// <summary>
		/// When this configuration was first created.
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// When this configuration was last updated.
		/// </summary>
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// No TTL - user configuration persists indefinitely.
		/// </summary>
		public new int? TTL { get; set; } = null;
	}
}
