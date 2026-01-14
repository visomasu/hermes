namespace Hermes.Tools.Models
{
    /// <summary>
    /// Base input model for agent tool capabilities, providing common metadata and context
    /// that can be shared across specific capability input types.
    /// </summary>
    public abstract class ToolCapabilityInputBase
    {
        /// <summary>
        /// Co-relation Id.
        /// </summary>
        string CorrelationId { get; set; } = string.Empty;
    }
}
