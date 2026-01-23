using Hermes.Notifications.WorkItemSla.Models;
using System.Text;

namespace Hermes.Notifications.WorkItemSla
{
	/// <summary>
	/// Composes notification messages for work item update SLA violations.
	/// </summary>
	public class WorkItemUpdateSlaMessageComposer
	{
		private const int MaxViolationsToDisplay = 20;

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
