using Integrations.AzureDevOps;
using System.Text.Json;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using System.Reflection;

namespace Hermes.Tools.AzureDevOps
{
    /// <summary>
    /// Agent tool for Azure DevOps, supporting multiple capabilities.
    /// </summary>
    public class AzureDevOpsTool : IAgentTool
    {
        private readonly IAzureDevOpsWorkItemClient _client;

        private readonly IAgentToolCapability<GetWorkItemTreeCapabilityInput> _getWorkItemTreeCapability;
        private readonly IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput> _getWorkItemsByAreaPathCapability;
        private readonly IAgentToolCapability<GetParentHierarchyCapabilityInput> _getParentHierarchyCapability;
        private readonly IAgentToolCapability<GetFullHierarchyCapabilityInput> _getFullHierarchyCapability;
        private readonly int _defaultDepth = 2;

        // Static mapping of work item type to fields
        private static readonly Dictionary<string, List<string>> FieldsByType = new()
        {
            { "Feature", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "Custom.PrivatePreviewDate", "Custom.PublicPreviewDate", "Custom.GAdate", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate", "Custom.CurrentStatus", "Custom.RiskAssessmentComment" } },
            { "User Story", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "Custom.RiskAssessmentComment", "Custom.StoryField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate" } },
            { "Task", new List<string> { "System.Id", "System.Title", "System.State", "System.WorkItemType", "System.Description", "System.AssignedTo", "Custom.TaskField1", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.TargetDate", "Microsoft.VSTS.Scheduling.FinishDate" } }
        };

        /// <summary>
        /// Initializes a new instance of <see cref="AzureDevOpsTool"/>.
        /// </summary>
        /// <param name="client">The Azure DevOps work item client.</param>
        /// <param name="getWorkItemTreeCapability">Capability implementation for GetWorkItemTree.</param>
        /// <param name="getWorkItemsByAreaPathCapability">Capability implementation for GetWorkItemsByAreaPath.</param>
        /// <param name="getParentHierarchyCapability">Capability implementation for GetParentHierarchy.</param>
        /// <param name="getFullHierarchyCapability">Capability implementation for GetFullHierarchy.</param>
        public AzureDevOpsTool(
            IAzureDevOpsWorkItemClient client,
            IAgentToolCapability<GetWorkItemTreeCapabilityInput> getWorkItemTreeCapability,
            IAgentToolCapability<GetWorkItemsByAreaPathCapabilityInput> getWorkItemsByAreaPathCapability,
            IAgentToolCapability<GetParentHierarchyCapabilityInput> getParentHierarchyCapability,
            IAgentToolCapability<GetFullHierarchyCapabilityInput> getFullHierarchyCapability)
        {
            _client = client;

            _getWorkItemTreeCapability = getWorkItemTreeCapability;
            _getWorkItemsByAreaPathCapability = getWorkItemsByAreaPathCapability;
            _getParentHierarchyCapability = getParentHierarchyCapability;
            _getFullHierarchyCapability = getFullHierarchyCapability;
        }

        /// <inheritdoc/>
        public string Name => "AzureDevOpsTool";

        /// <inheritdoc/>
        public string Description => "Provides Azure DevOps capabilities such as retrieving work item trees, parent hierarchies and more.";

        /// <inheritdoc/>
        public IReadOnlyList<string> Capabilities => new[] { "GetWorkItemTree", "GetWorkItemsByAreaPath", "GetParentHierarchy", "GetFullHierarchy" };

        /// <inheritdoc/>
        public string GetMetadata()
        {
            var getWorkItemTreeInput = BuildInputSchemaDescription(typeof(GetWorkItemTreeCapabilityInput));
            var getWorkItemsByAreaPathInput = BuildInputSchemaDescription(typeof(GetWorkItemsByAreaPathCapabilityInput));
            var getParentHierarchyInput = BuildInputSchemaDescription(typeof(GetParentHierarchyCapabilityInput));
            var getFullHierarchyInput = BuildInputSchemaDescription(typeof(GetFullHierarchyCapabilityInput));

            return
                "Capabilities: [GetWorkItemTree, GetWorkItemsByAreaPath, GetParentHierarchy, GetFullHierarchy] | " +
                $"Input (GetWorkItemTree): {getWorkItemTreeInput} | " +
                $"Input (GetWorkItemsByAreaPath): {getWorkItemsByAreaPathInput} | " +
                $"Input (GetParentHierarchy): {getParentHierarchyInput} | " +
                $"Input (GetFullHierarchy): {getFullHierarchyInput} | " +
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
            return operation switch
            {
                "GetWorkItemTree" => await ExecuteGetWorkItemTreeAsync(input),
                "GetWorkItemsByAreaPath" => await ExecuteGetWorkItemsByAreaPathAsync(input),
                "GetParentHierarchy" => await ExecuteGetParentHierarchyAsync(input),
                "GetFullHierarchy" => await ExecuteGetFullHierarchyAsync(input),
                _ => throw new NotSupportedException($"Operation '{operation}' is not supported."),
            };
        }

        #region WorkItemTree

        private async Task<string> ExecuteGetWorkItemTreeAsync(string input)
        {
            // Use the capability for the actual implementation; keep JSON contract the same.
            var model = JsonSerializer.Deserialize<GetWorkItemTreeCapabilityInput>(input)
                ?? throw new ArgumentException("Invalid input for GetWorkItemTree.");

            return await _getWorkItemTreeCapability.ExecuteAsync(model);
        }

        #endregion

        #region ParentHierarchy

        private async Task<string> ExecuteGetParentHierarchyAsync(string input)
        {
            // Use the capability for the actual implementation; keep JSON contract the same.
            var model = JsonSerializer.Deserialize<GetParentHierarchyCapabilityInput>(input)
                ?? throw new ArgumentException("Invalid input for GetParentHierarchy.");

            return await _getParentHierarchyCapability.ExecuteAsync(model);
        }

        #endregion

        #region FullHierarchy

        private async Task<string> ExecuteGetFullHierarchyAsync(string input)
        {
            // Use the capability for the actual implementation; keep JSON contract the same.
            var model = JsonSerializer.Deserialize<GetFullHierarchyCapabilityInput>(input)
                ?? throw new ArgumentException("Invalid input for GetFullHierarchy.");

            return await _getFullHierarchyCapability.ExecuteAsync(model);
        }

        #endregion

        #region Area-Path

        private async Task<string> ExecuteGetWorkItemsByAreaPathAsync(string input)
        {
            var model = JsonSerializer.Deserialize<GetWorkItemsByAreaPathCapabilityInput>(input)
                ?? throw new ArgumentException("Invalid input for GetWorkItemsByAreaPath.");

            return await _getWorkItemsByAreaPathCapability.ExecuteAsync(model);
        }

        #endregion
    }
}
