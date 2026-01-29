using System.Reflection;
using System.Text.Json;
using Hermes.Tools.UserManagement.Capabilities;
using Hermes.Tools.UserManagement.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.UserManagement
{
	/// <summary>
	/// Agent tool for user management, supporting SLA notification registration and profile management.
	/// </summary>
	public class UserManagementTool : IAgentTool
	{
		private readonly IAgentToolCapability<RegisterSlaNotificationsCapabilityInput> _registerCapability;
		private readonly IAgentToolCapability<UnregisterSlaNotificationsCapabilityInput> _unregisterCapability;
		private readonly ILogger<UserManagementTool> _logger;

		// Capability aliases for flexible operation name matching
		private static readonly IReadOnlyDictionary<string, string[]> CapabilityAliases = new Dictionary<string, string[]>
		{
			{ "RegisterSlaNotifications", new[] { "RegisterSLA", "RegisterForSlaNotifications", "RegisterForSLA", "Register" } },
			{ "UnregisterSlaNotifications", new[] { "UnregisterSLA", "UnregisterForSlaNotifications", "UnregisterForSLA", "Unregister" } }
		};

		/// <summary>
		/// Initializes a new instance of <see cref="UserManagementTool"/>.
		/// </summary>
		/// <param name="registerCapability">Capability implementation for RegisterSlaNotifications.</param>
		/// <param name="unregisterCapability">Capability implementation for UnregisterSlaNotifications.</param>
		/// <param name="logger">Logger instance.</param>
		public UserManagementTool(
			IAgentToolCapability<RegisterSlaNotificationsCapabilityInput> registerCapability,
			IAgentToolCapability<UnregisterSlaNotificationsCapabilityInput> unregisterCapability,
			ILogger<UserManagementTool> logger)
		{
			_registerCapability = registerCapability;
			_unregisterCapability = unregisterCapability;
			_logger = logger;
		}

		/// <inheritdoc/>
		public string Name => "UserManagementTool";

		/// <inheritdoc/>
		public string Description => "Provides user management capabilities such as registering for SLA notifications and managing user preferences.";

		/// <inheritdoc/>
		public IReadOnlyList<string> Capabilities => new[]
		{
			_registerCapability.Name,
			_unregisterCapability.Name
		};

		/// <inheritdoc/>
		public string GetMetadata()
		{
			var registerInput = BuildInputSchemaDescription(typeof(RegisterSlaNotificationsCapabilityInput));
			var unregisterInput = BuildInputSchemaDescription(typeof(UnregisterSlaNotificationsCapabilityInput));

			return
				"Capabilities: [RegisterSlaNotifications, UnregisterSlaNotifications] | " +
				$"Input (RegisterSlaNotifications): {registerInput} | " +
				$"Input (UnregisterSlaNotifications): {unregisterInput} | " +
				"Output: JSON";
		}

		private static string BuildInputSchemaDescription(Type inputType)
		{
			// Render a JSON-like shape from public properties on the input type, using camelCase names.
			var properties = inputType
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead)
				.Select(p => $"'{ToCamelCase(p.Name)}': {MapTypeToSchemaName(p.PropertyType)}");

			return "{" + string.Join(", ", properties) + "}";
		}

		private static string ToCamelCase(string name)
		{
			if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
			{
				return name;
			}

			if (name.Length == 1)
			{
				return name.ToLowerInvariant();
			}

			return char.ToLowerInvariant(name[0]) + name[1..];
		}

		private static string MapTypeToSchemaName(Type type)
		{
			if (type == typeof(int) || type == typeof(int?)) return "int";
			if (type == typeof(string)) return "string";
			if (type.IsArray)
			{
				var elementType = type.GetElementType() ?? typeof(object);
				return MapTypeToSchemaName(elementType) + "[]";
			}
			return "object";
		}

		/// <inheritdoc/>
		public virtual async Task<string> ExecuteAsync(string operation, string input)
		{
			_logger.LogInformation("Executing UserManagementTool operation: {Operation}", operation);

			// Use CapabilityMatcher for flexible operation name resolution
			if (!CapabilityMatcher.TryResolve(operation, CapabilityAliases, out var canonicalName))
			{
				throw new NotSupportedException(
					CapabilityMatcher.FormatNotSupportedError(operation, Name, CapabilityAliases.Keys));
			}

			return canonicalName switch
			{
				"RegisterSlaNotifications" => await ExecuteRegisterAsync(input),
				"UnregisterSlaNotifications" => await ExecuteUnregisterAsync(input),
				_ => throw new InvalidOperationException($"Unhandled canonical operation: {canonicalName}"),
			};
		}

		private async Task<string> ExecuteRegisterAsync(string input)
		{
			var model = JsonSerializer.Deserialize<RegisterSlaNotificationsCapabilityInput>(input)
				?? throw new ArgumentException("Invalid input for RegisterSlaNotifications.");

			return await _registerCapability.ExecuteAsync(model);
		}

		private async Task<string> ExecuteUnregisterAsync(string input)
		{
			var model = JsonSerializer.Deserialize<UnregisterSlaNotificationsCapabilityInput>(input)
				?? throw new ArgumentException("Invalid input for UnregisterSlaNotifications.");

			return await _unregisterCapability.ExecuteAsync(model);
		}
	}
}
