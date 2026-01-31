using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;

namespace Hermes.Evals.Core.Evaluation;

/// <summary>
/// Evaluates the quality of the response (content, structure, completeness).
/// Weight: 15% (nice-to-have - content over format).
/// </summary>
public class ResponseQualityEvaluator : IEvaluator
{
    public string Name => "ResponseQuality";
    public double Weight => 0.15;

    public Task<TurnResult> EvaluateAsync(
        ConversationTurn turn,
        TurnExpectation expectation,
        Dictionary<string, object> capturedMetadata)
    {
        var result = new TurnResult
        {
            TurnNumber = turn.TurnNumber,
            EvaluatorName = Name
        };

        // If no response quality expectations, skip evaluation
        var qualityExpectation = expectation.ResponseQuality;
        if (qualityExpectation == null)
        {
            result.OverallScore = 1.0;
            result.Scores.ResponseQuality = 1.0;
            return Task.FromResult(result);
        }

        // Extract response text from metadata
        var responseText = capturedMetadata.GetValueOrDefault("responseText") as string;
        if (string.IsNullOrEmpty(responseText))
        {
            result.AddCheck("ResponsePresent", false, "No response text captured");
            result.OverallScore = 0.0;
            result.Scores.ResponseQuality = 0.0;
            result.Success = false;
            return Task.FromResult(result);
        }

        int passedChecks = 0;
        int totalChecks = 0;

        // Check: Required content (MustContain)
        if (qualityExpectation.MustContain != null)
        {
            foreach (var requiredText in qualityExpectation.MustContain)
            {
                totalChecks++;
                var contains = responseText.Contains(requiredText, StringComparison.OrdinalIgnoreCase);
                result.AddCheck($"Contains_{SanitizeCheckName(requiredText)}", contains,
                    contains ? "Present" : $"Missing required text: '{requiredText}'");
                if (contains) passedChecks++;
            }
        }

        // Check: Forbidden content (MustNotContain)
        if (qualityExpectation.MustNotContain != null)
        {
            foreach (var forbiddenText in qualityExpectation.MustNotContain)
            {
                totalChecks++;
                var absent = !responseText.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase);
                result.AddCheck($"DoesNotContain_{SanitizeCheckName(forbiddenText)}", absent,
                    absent ? "Correctly absent" : $"Incorrectly present: '{forbiddenText}'");
                if (absent) passedChecks++;
            }
        }

        // Check: Minimum length
        if (qualityExpectation.MinLength.HasValue)
        {
            totalChecks++;
            var meetsLength = responseText.Length >= qualityExpectation.MinLength.Value;
            result.AddCheck("MinimumLength", meetsLength,
                $"Length: {responseText.Length}, Required: {qualityExpectation.MinLength.Value}");
            if (meetsLength) passedChecks++;
        }

        // Check: Structure (presence of sections)
        if (qualityExpectation.Structure != null)
        {
            foreach (var section in qualityExpectation.Structure)
            {
                totalChecks++;
                var hasSection = responseText.Contains(section, StringComparison.OrdinalIgnoreCase);
                result.AddCheck($"Section_{SanitizeCheckName(section)}", hasSection,
                    hasSection ? "Present" : $"Missing section: '{section}'");
                if (hasSection) passedChecks++;
            }
        }

        // If no checks were defined, give full score
        if (totalChecks == 0)
        {
            result.OverallScore = 1.0;
            result.Scores.ResponseQuality = 1.0;
            result.Success = true;
        }
        else
        {
            result.OverallScore = (double)passedChecks / totalChecks;
            result.Scores.ResponseQuality = result.OverallScore;
            result.Success = result.OverallScore >= 0.5; // At least 50% of quality checks passed
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Sanitizes a string for use as a check name (removes special characters).
    /// </summary>
    private static string SanitizeCheckName(string text)
    {
        // Take first 30 characters and replace spaces/special chars with underscores
        var sanitized = text.Length > 30 ? text.Substring(0, 30) : text;
        return new string(sanitized.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }
}
