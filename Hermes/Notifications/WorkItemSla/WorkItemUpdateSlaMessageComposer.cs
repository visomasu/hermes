using Hermes.Domain.WorkItemSla.Models;
using System.Text;

namespace Hermes.Notifications.WorkItemSla
{
	/// <summary>
	/// Composes notification messages for work item update SLA violations.
	/// </summary>
	public class WorkItemUpdateSlaMessageComposer
	{
		private const int MaxViolationsToDisplay = 20;
		private const int MaxDirectReportsToShowDetails = 5;
		private const int MaxViolationsPerDirectReport = 10;

		/// <summary>
		/// Composes a digest message listing all SLA violations for a user.
		/// </summary>
		/// <param name="violations">List of SLA violations.</param>
		/// <returns>Formatted message string.</returns>
		public string ComposeDigestMessage(List<WorkItemUpdateSlaViolation> violations)
		{
			if (violations == null || violations.Count == 0)
			{
				return string.Empty;
			}

			var sb = new StringBuilder();
			sb.AppendLine("⚠️ SLA Violation Alert");
			sb.AppendLine();
			sb.AppendLine($"You have {violations.Count} work item{(violations.Count == 1 ? "" : "s")} that haven't been updated within SLA thresholds:");
			sb.AppendLine();

			// Sort by days since update (most overdue first)
			var sortedViolations = violations
				.OrderByDescending(v => v.DaysSinceUpdate)
				.ToList();

			// Display up to MaxViolationsToDisplay violations
			var displayCount = Math.Min(sortedViolations.Count, MaxViolationsToDisplay);

			for (int i = 0; i < displayCount; i++)
			{
				var violation = sortedViolations[i];
				var emoji = _GetWorkItemTypeEmoji(violation.WorkItemType);

				sb.AppendLine($"{emoji} {violation.WorkItemType} #{violation.WorkItemId}: {violation.Title}");
				sb.AppendLine($"   - Last updated: {violation.DaysSinceUpdate} day{(violation.DaysSinceUpdate == 1 ? "" : "s")} ago (SLA: {violation.SlaThresholdDays} day{(violation.SlaThresholdDays == 1 ? "" : "s")})");
				sb.AppendLine($"   - View: {violation.Url}");
				sb.AppendLine();
			}

			// If there are more violations, add a footer
			if (sortedViolations.Count > MaxViolationsToDisplay)
			{
				var remaining = sortedViolations.Count - MaxViolationsToDisplay;
				sb.AppendLine($"...and {remaining} more violation{(remaining == 1 ? "" : "s")}.");
				sb.AppendLine();
			}

			sb.AppendLine("Please review and update these items to meet SLA requirements.");

			return sb.ToString();
		}

		/// <summary>
		/// Composes a manager digest message showing violations across the team.
		/// </summary>
		/// <param name="violationsByOwner">Dictionary mapping owner email to their violations.</param>
		/// <param name="managerEmail">The manager's email address.</param>
		/// <returns>Formatted message string.</returns>
		public string ComposeManagerDigestMessage(
			Dictionary<string, List<WorkItemUpdateSlaViolation>> violationsByOwner,
			string managerEmail)
		{
			if (violationsByOwner == null || violationsByOwner.Count == 0)
			{
				return string.Empty;
			}

			var sb = new StringBuilder();
			sb.AppendLine("📊 **Manager SLA Violation Report**");
			sb.AppendLine();

			// Calculate totals
			var totalViolations = violationsByOwner.Sum(kvp => kvp.Value.Count);
			var managerViolations = violationsByOwner.ContainsKey(managerEmail)
				? violationsByOwner[managerEmail]
				: new List<WorkItemUpdateSlaViolation>();

			var directReportViolations = violationsByOwner
				.Where(kvp => kvp.Key != managerEmail)
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			// Summary section
			sb.AppendLine("**Summary:**");
			sb.AppendLine($"- Total violations: **{totalViolations}**");
			sb.AppendLine($"- Your violations: **{managerViolations.Count}**");
			sb.AppendLine($"- Direct reports with violations: **{directReportViolations.Count}**");
			sb.AppendLine();

			// Manager's own violations (if any)
			if (managerViolations.Count > 0)
			{
				sb.AppendLine("### 👤 Your Violations");
				sb.AppendLine();
				_AppendViolationList(sb, managerViolations, MaxViolationsToDisplay);
				sb.AppendLine();
			}

			// Team violations
			if (directReportViolations.Count > 0)
			{
				sb.AppendLine("### 👥 Team Member Violations");
				sb.AppendLine();

				// Detailed view if ≤5 directs, aggregated if >5
				if (directReportViolations.Count <= MaxDirectReportsToShowDetails)
				{
					// Show detailed breakdown per person
					foreach (var (email, violations) in directReportViolations.OrderByDescending(kvp => kvp.Value.Count))
					{
						sb.AppendLine($"**{email}** ({violations.Count} violation{(violations.Count > 1 ? "s" : "")}):");
						_AppendViolationList(sb, violations, MaxViolationsPerDirectReport);
						sb.AppendLine();
					}
				}
				else
				{
					// Aggregate view for large teams
					sb.AppendLine("**Team Summary** (showing counts only due to team size):");
					sb.AppendLine();

					foreach (var (email, violations) in directReportViolations.OrderByDescending(kvp => kvp.Value.Count))
					{
						sb.AppendLine($"- **{email}**: {violations.Count} violation{(violations.Count > 1 ? "s" : "")}");
					}

					sb.AppendLine();
					sb.AppendLine("💡 *For detailed information, ask me to check SLA violations for specific team members.*");
				}
			}

			sb.AppendLine();
			sb.AppendLine("---");
			sb.AppendLine("💡 *Tip: You can ask me to check SLA violations anytime with \"check my SLA violations\"*");

			return sb.ToString();
		}

		/// <summary>
		/// Appends a list of violations to the string builder.
		/// </summary>
		private void _AppendViolationList(
			StringBuilder sb,
			List<WorkItemUpdateSlaViolation> violations,
			int maxToShow)
		{
			var sortedViolations = violations.OrderByDescending(v => v.DaysSinceUpdate).ToList();
			var toDisplay = sortedViolations.Take(maxToShow).ToList();

			foreach (var violation in toDisplay)
			{
				var emoji = _GetWorkItemTypeEmoji(violation.WorkItemType);
				sb.AppendLine($"{emoji} **{violation.WorkItemType} #{violation.WorkItemId}**: {violation.Title}");
				sb.AppendLine($"   - Last updated: **{violation.DaysSinceUpdate} days** ago (SLA: {violation.SlaThresholdDays} days)");
				sb.AppendLine($"   - [View work item]({violation.Url})");
			}

			if (sortedViolations.Count > maxToShow)
			{
				sb.AppendLine($"   - *...and {sortedViolations.Count - maxToShow} more*");
			}
		}

		/// <summary>
		/// Gets an emoji representing the work item type.
		/// </summary>
		private string _GetWorkItemTypeEmoji(string workItemType)
		{
			return workItemType.ToLowerInvariant() switch
			{
				"bug" => "🐛",
				"task" => "📋",
				"user story" => "📖",
				"feature" => "✨",
				_ => "📌"
			};
		}
	}
}
