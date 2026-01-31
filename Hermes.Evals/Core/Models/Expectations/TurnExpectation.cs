namespace Hermes.Evals.Core.Models.Expectations;

/// <summary>
/// Defines expected outcomes for a single conversation turn.
/// Each dimension is evaluated independently and weighted to produce a final score.
/// </summary>
public class TurnExpectation
{
    /// <summary>
    /// Expected tool selection (tool name and capability).
    /// Evaluates: Did the LLM select the correct tool and capability?
    /// Weight: 30%
    /// </summary>
    public ToolSelectionExpectation? ToolSelection { get; set; }

    /// <summary>
    /// Expected parameter extraction from natural language.
    /// Evaluates: Were all parameters correctly extracted?
    /// Weight: 30%
    /// </summary>
    public ParameterExtractionExpectation? ParameterExtraction { get; set; }

    /// <summary>
    /// Expected context retention across turns.
    /// Evaluates: Did the LLM remember information from previous turns?
    /// Weight: 25%
    /// </summary>
    public ContextRetentionExpectation? ContextRetention { get; set; }

    /// <summary>
    /// Expected response quality (content, structure, formatting).
    /// Evaluates: Is the response complete and well-formatted?
    /// Weight: 15%
    /// </summary>
    public ResponseQualityExpectation? ResponseQuality { get; set; }
}
