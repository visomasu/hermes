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
		/// Composes a team-separated manager digest message showing violations grouped by team and owner.
		/// Automatically adapts display based on team count (single-team vs multi-team).
		/// </summary>
		/// <param name="violationsByOwner">Dictionary mapping owner email to their violations.</param>
		/// <param name="managerEmail">The manager's email address.</param>
		/// <returns>Formatted message string.</returns>
		public string ComposeManagerDigestMessageWithTeams(
			Dictionary<string, List<WorkItemUpdateSlaViolation>> violationsByOwner,
			string managerEmail)
		{
			if (violationsByOwner == null || violationsByOwner.Count == 0)
			{
				return string.Empty;
			}

			// Flatten violations and detect unique teams
			var allViolations = violationsByOwner.SelectMany(kvp => kvp.Value).ToList();
			var uniqueTeams = allViolations
				.Select(v => new { v.TeamId, v.TeamName })
				.Where(t => !string.IsNullOrEmpty(t.TeamId))
				.Distinct()
				.ToList();

			// Single team: Use simplified format
			if (uniqueTeams.Count <= 1)
			{
				return _ComposeSingleTeamManagerMessage(violationsByOwner, managerEmail, uniqueTeams.FirstOrDefault()?.TeamName);
			}

			// Multi-team: Use team-separated format
			return _ComposeMultiTeamManagerMessage(violationsByOwner, managerEmail, allViolations);
		}

		/// <summary>
		/// Composes a digest message for an individual contributor with team grouping if multiple teams.
		/// </summary>
		/// <param name="violations">List of SLA violations.</param>
		/// <returns>Formatted message string.</returns>
		public string ComposeDigestMessageWithTeams(List<WorkItemUpdateSlaViolation> violations)
		{
			if (violations == null || violations.Count == 0)
			{
				return string.Empty;
			}

			var uniqueTeamCount = violations
				.Select(v => v.TeamId)
				.Where(id => !string.IsNullOrEmpty(id))
				.Distinct()
				.Count();

			// Single team: Use existing format
			if (uniqueTeamCount <= 1)
			{
				return ComposeDigestMessage(violations);
			}

			// Multi-team: Group by team
			return _ComposeMultiTeamICMessage(violations);
		}

		/// <summary>
		/// Composes single-team manager message (backwards compatible with team badge).
		/// </summary>
		private string _ComposeSingleTeamManagerMessage(
			Dictionary<string, List<WorkItemUpdateSlaViolation>> violationsByOwner,
			string managerEmail,
			string? teamName)
		{
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

			// Add team badge if team name is provided
			if (!string.IsNullOrEmpty(teamName))
			{
				sb.AppendLine($"- Team: {teamName}");
			}

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
		/// Composes multi-team manager message with team sections.
		/// </summary>
		private string _ComposeMultiTeamManagerMessage(
			Dictionary<string, List<WorkItemUpdateSlaViolation>> violationsByOwner,
			string managerEmail,
			List<WorkItemUpdateSlaViolation> allViolations)
		{
			var sb = new StringBuilder();
			sb.AppendLine("📊 **Manager SLA Violation Report**");
			sb.AppendLine();

			// Calculate totals
			var totalViolations = allViolations.Count;
			var managerViolations = violationsByOwner.ContainsKey(managerEmail)
				? violationsByOwner[managerEmail]
				: new List<WorkItemUpdateSlaViolation>();

			var directReportViolations = violationsByOwner
				.Where(kvp => kvp.Key != managerEmail)
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			// Get unique team names for summary
			var teamGroups = _GroupByTeam(allViolations);
			var teamNames = string.Join(", ", teamGroups.Select(t => t.TeamName));

			// Summary section
			sb.AppendLine("**Summary:**");
			sb.AppendLine($"- Total violations: **{totalViolations}**");
			sb.AppendLine($"- Your violations: **{managerViolations.Count}**");
			sb.AppendLine($"- Direct reports with violations: **{directReportViolations.Count}**");
			sb.AppendLine($"- Teams: {teamNames}");
			sb.AppendLine();

			// Group violations by team
			foreach (var (teamId, teamName, teamViolations) in teamGroups)
			{
				sb.AppendLine("---");
				sb.AppendLine();
				sb.AppendLine($"## 🎯 {teamName} ({teamViolations.Count} violation{(teamViolations.Count > 1 ? "s" : "")})");
				sb.AppendLine();

				// Manager's violations for this team
				var managerTeamViolations = teamViolations
					.Where(v => violationsByOwner.ContainsKey(managerEmail) &&
					           violationsByOwner[managerEmail].Contains(v))
					.ToList();

				if (managerTeamViolations.Count > 0)
				{
					sb.AppendLine($"### 👤 Your Violations ({managerTeamViolations.Count})");
					sb.AppendLine();
					_AppendViolationList(sb, managerTeamViolations, MaxViolationsToDisplay);
					sb.AppendLine();
				}

				// Direct reports' violations for this team
				var directTeamViolations = new Dictionary<string, List<WorkItemUpdateSlaViolation>>();
				foreach (var (email, violations) in directReportViolations)
				{
					var emailTeamViolations = violations
						.Where(v => v.TeamId == teamId)
						.ToList();

					if (emailTeamViolations.Count > 0)
					{
						directTeamViolations[email] = emailTeamViolations;
					}
				}

				if (directTeamViolations.Count > 0)
				{
					sb.AppendLine("### 👥 Team Member Violations");
					sb.AppendLine();

					// Detailed view if ≤5 directs, aggregated if >5
					if (directTeamViolations.Count <= MaxDirectReportsToShowDetails)
					{
						// Show detailed breakdown per person
						foreach (var (email, violations) in directTeamViolations.OrderByDescending(kvp => kvp.Value.Count))
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

						foreach (var (email, violations) in directTeamViolations.OrderByDescending(kvp => kvp.Value.Count))
						{
							sb.AppendLine($"- **{email}**: {violations.Count} violation{(violations.Count > 1 ? "s" : "")}");
						}

						sb.AppendLine();
					}
				}
			}

			sb.AppendLine();
			sb.AppendLine("---");
			sb.AppendLine("💡 *Tip: You can ask me to check SLA violations anytime with \"check my SLA violations\"*");

			return sb.ToString();
		}

		/// <summary>
		/// Composes multi-team IC message with team badges.
		/// </summary>
		private string _ComposeMultiTeamICMessage(List<WorkItemUpdateSlaViolation> violations)
		{
			var sb = new StringBuilder();
			sb.AppendLine("⚠️ SLA Violation Alert");
			sb.AppendLine();

			var teamGroups = _GroupByTeam(violations);
			var totalCount = violations.Count;

			sb.AppendLine($"You have {totalCount} work item{(totalCount == 1 ? "" : "s")} across {teamGroups.Count} team{(teamGroups.Count == 1 ? "" : "s")} that haven't been updated within SLA thresholds:");
			sb.AppendLine();

			// Display violations grouped by team
			foreach (var (teamId, teamName, teamViolations) in teamGroups)
			{
				sb.AppendLine($"🎯 **{teamName}**");
				sb.AppendLine();

				// Sort by days since update (most overdue first)
				var sortedViolations = teamViolations
					.OrderByDescending(v => v.DaysSinceUpdate)
					.ToList();

				// Display up to MaxViolationsToDisplay violations per team
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

				// If there are more violations in this team, add a footer
				if (sortedViolations.Count > MaxViolationsToDisplay)
				{
					var remaining = sortedViolations.Count - MaxViolationsToDisplay;
					sb.AppendLine($"...and {remaining} more violation{(remaining == 1 ? "" : "s")} in this team.");
					sb.AppendLine();
				}
			}

			sb.AppendLine("Please review and update these items to meet SLA requirements.");

			return sb.ToString();
		}

		/// <summary>
		/// Extracts team information from violations and groups by team.
		/// </summary>
		private List<(string TeamId, string TeamName, List<WorkItemUpdateSlaViolation> Violations)> _GroupByTeam(
			List<WorkItemUpdateSlaViolation> violations)
		{
			return violations
				.GroupBy(v => new { TeamId = v.TeamId ?? string.Empty, TeamName = string.IsNullOrEmpty(v.TeamName) ? (string.IsNullOrEmpty(v.TeamId) ? "Unknown Team" : v.TeamId) : v.TeamName })
				.Select(g => (g.Key.TeamId, g.Key.TeamName, g.ToList()))
				.OrderBy(t => t.TeamName)
				.ToList();
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
