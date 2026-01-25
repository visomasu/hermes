using Hermes.Domain.WorkItemSla.Models;
using Hermes.Notifications.WorkItemSla;
using Xunit;

namespace Hermes.Tests.Notifications.WorkItemSla
{
	public class WorkItemUpdateSlaMessageComposerTests
	{
		private readonly WorkItemUpdateSlaMessageComposer _composer;

		public WorkItemUpdateSlaMessageComposerTests()
		{
			_composer = new WorkItemUpdateSlaMessageComposer();
		}

		[Fact]
		public void ComposeDigestMessage_NullViolations_ReturnsEmptyString()
		{
			// Act
			var result = _composer.ComposeDigestMessage(null!);

			// Assert
			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void ComposeDigestMessage_EmptyViolations_ReturnsEmptyString()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>();

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void ComposeDigestMessage_SingleViolation_ContainsAllDetails()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 12345,
					Title = "Fix login bug",
					WorkItemType = "Bug",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/12345"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("⚠️ SLA Violation Alert", result);
			Assert.Contains("1 work item", result);
			Assert.Contains("🐛 Bug #12345: Fix login bug", result);
			Assert.Contains("5 days ago", result);
			Assert.Contains("SLA: 2 days", result);
			Assert.Contains("https://dev.azure.com/test/12345", result);
			Assert.Contains("Please review and update", result);
		}

		[Fact]
		public void ComposeDigestMessage_MultipleViolations_ContainsCount()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Bug 1",
					WorkItemType = "Bug",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/1"
				},
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 2,
					Title = "Task 1",
					WorkItemType = "Task",
					DaysSinceUpdate = 10,
					SlaThresholdDays = 5,
					Url = "https://dev.azure.com/test/2"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("2 work items", result);
			Assert.Contains("Bug #1", result);
			Assert.Contains("Task #2", result);
		}

		[Fact]
		public void ComposeDigestMessage_SortsViolationsByDaysSinceUpdate_MostOverdueFirst()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Less overdue",
					WorkItemType = "Bug",
					DaysSinceUpdate = 3,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/1"
				},
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 2,
					Title = "Most overdue",
					WorkItemType = "Bug",
					DaysSinceUpdate = 10,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/2"
				},
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 3,
					Title = "Moderately overdue",
					WorkItemType = "Bug",
					DaysSinceUpdate = 6,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/3"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			var indexMostOverdue = result.IndexOf("Most overdue");
			var indexModeratelyOverdue = result.IndexOf("Moderately overdue");
			var indexLessOverdue = result.IndexOf("Less overdue");

			Assert.True(indexMostOverdue < indexModeratelyOverdue);
			Assert.True(indexModeratelyOverdue < indexLessOverdue);
		}

		[Fact]
		public void ComposeDigestMessage_MoreThan20Violations_TruncatesWithFooter()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>();
			for (int i = 1; i <= 25; i++)
			{
				violations.Add(new WorkItemUpdateSlaViolation
				{
					WorkItemId = i,
					Title = $"Work Item {i}",
					WorkItemType = "Bug",
					DaysSinceUpdate = i,
					SlaThresholdDays = 2,
					Url = $"https://dev.azure.com/test/{i}"
				});
			}

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("25 work items", result);
			Assert.Contains("...and 5 more violations", result);
			Assert.Contains("Work Item 25", result); // Most overdue should be shown
			Assert.DoesNotContain("Bug #1:", result); // Least overdue should be truncated
		}

		[Fact]
		public void ComposeDigestMessage_UsesCorrectEmojis_ForDifferentWorkItemTypes()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation { WorkItemId = 1, Title = "Bug", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url" },
				new WorkItemUpdateSlaViolation { WorkItemId = 2, Title = "Task", WorkItemType = "Task", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url" },
				new WorkItemUpdateSlaViolation { WorkItemId = 3, Title = "Story", WorkItemType = "User Story", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url" },
				new WorkItemUpdateSlaViolation { WorkItemId = 4, Title = "Feature", WorkItemType = "Feature", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url" }
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("🐛 Bug", result);
			Assert.Contains("📋 Task", result);
			Assert.Contains("📖 User Story", result);
			Assert.Contains("✨ Feature", result);
		}

		[Fact]
		public void ComposeDigestMessage_UnknownWorkItemType_UsesFallbackEmoji()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Unknown Type",
					WorkItemType = "Epic",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 2,
					Url = "url"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("📌 Epic", result);
		}

		[Fact]
		public void ComposeDigestMessage_SingularDay_UsesSingularForm()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Test",
					WorkItemType = "Bug",
					DaysSinceUpdate = 1,
					SlaThresholdDays = 1,
					Url = "url"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("1 day ago", result);
			Assert.Contains("SLA: 1 day", result);
		}

		[Fact]
		public void ComposeDigestMessage_PluralDays_UsesPluralForm()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Test",
					WorkItemType = "Bug",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 3,
					Url = "url"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessage(violations);

			// Assert
			Assert.Contains("5 days ago", result);
			Assert.Contains("SLA: 3 days", result);
		}
	}
}
