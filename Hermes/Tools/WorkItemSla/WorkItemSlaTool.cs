using System.Reflection;
using System.Text.Json;
using Hermes.Tools.WorkItemSla.Capabilities;
using Hermes.Tools.WorkItemSla.Capabilities.Inputs;
using Microsoft.Extensions.Logging;

namespace Hermes.Tools.WorkItemSla
{
	/// <summary>
	/// Agent tool for work item SLA operations, providing on-demand violation checks.
	/// </summary>
	public class WorkItemSlaTool : IAgentTool
	{
		private readonly IAgentToolCapability<CheckSlaViolationsCapabilityInput> _checkViolationsCapability;
		private readonly ILogger<WorkItemSlaTool> _logger;

		/// <summary>
		/// Initializes a new instance of <see cref="WorkItemSlaTool"/>.
		/// </summary>
		/// <param name="checkViolationsCapability">Capability implementation for CheckSlaViolations.</param>
		/// <param name="logger">Logger instance.</param>
		public WorkItemSlaTool(
			IAgentToolCapability<CheckSlaViolationsCapabilityInput> checkViolationsCapability,
			ILogger<WorkItemSlaTool> logger)
		{
			_checkViolationsCapability = checkViolationsCapability;
			_logger = logger;
		}

		/// <inheritdoc/>
		public string Name => "WorkItemSlaTool";

		/// <inheritdoc/>
		public string Description => "Provides work item SLA capabilities such as checking violations on-demand for users and their teams.";

		/// <inheritdoc/>
		public IReadOnlyList<string> Capabilities => new[]
		{
			_checkViolationsCapability.Name
		};

		/// <inheritdoc/>
		public string GetMetadata()
		{
			var checkInput = BuildInputSchemaDescription(typeof(CheckSlaViolationsCapabilityInput));

			return
				"Capabilities: [CheckSlaViolations] | " +
				$"Input (CheckSlaViolations): {checkInput} | " +
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
			_logger.LogInformation("Executing WorkItemSlaTool operation: {Operation}", operation);

			return operation switch
			{
				"CheckSlaViolations" => await ExecuteCheckViolationsAsync(input),
				_ => throw new NotSupportedException($"Operation '{operation}' is not supported by {Name}."),
			};
		}

		private async Task<string> ExecuteCheckViolationsAsync(string input)
		{
			var model = JsonSerializer.Deserialize<CheckSlaViolationsCapabilityInput>(input)
				?? throw new ArgumentException("Invalid input for CheckSlaViolations.");

			return await _checkViolationsCapability.ExecuteAsync(model);
		}
	}
}
