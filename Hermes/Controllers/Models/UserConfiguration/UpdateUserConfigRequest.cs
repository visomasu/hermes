using Hermes.Storage.Repositories.UserConfiguration.Models;

namespace Hermes.Controllers.Models.UserConfiguration
{
	/// <summary>
	/// Request model for updating user configuration.
	/// </summary>
	public class UpdateUserConfigRequest
	{
		public NotificationPreferences? Notifications { get; set; }
	}
}
