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

		#region Manager Digest Tests

		[Fact]
		public void ComposeManagerDigestMessage_EmptyViolations_ReturnsEmptyString()
		{
			// Arrange
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>();

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, "manager@test.com");

			// Assert
			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_NullViolations_ReturnsEmptyString()
		{
			// Act
			var result = _composer.ComposeManagerDigestMessage(null!, "manager@test.com");

			// Assert
			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_ManagerWithOwnViolations_ShowsSeparateSection()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					managerEmail,
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 100,
							Title = "Manager's bug",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/100"
						}
					}
				},
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 200,
							Title = "Direct's bug",
							WorkItemType = "Bug",
							DaysSinceUpdate = 3,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/200"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("📊 **Manager SLA Violation Report**", result);
			Assert.Contains("**Summary:**", result);
			Assert.Contains("Total violations: **2**", result);
			Assert.Contains("Your violations: **1**", result);
			Assert.Contains("Direct reports with violations: **1**", result);
			Assert.Contains("### 👤 Your Violations", result);
			Assert.Contains("Manager's bug", result);
			Assert.Contains("### 👥 Team Member Violations", result);
			Assert.Contains("Direct's bug", result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_FewDirects_ShowsDetailedView()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 1,
							Title = "Bug 1",
							WorkItemType = "Bug",
							DaysSinceUpdate = 10,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/1"
						}
					}
				},
				{
					"direct2@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 2,
							Title = "Bug 2",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/2"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("**direct1@test.com** (1 violation):", result);
			Assert.Contains("**direct2@test.com** (1 violation):", result);
			Assert.Contains("Bug 1", result);
			Assert.Contains("Bug 2", result);
			Assert.Contains("[View work item]", result);
			Assert.DoesNotContain("showing counts only due to team size", result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_ManyDirects_ShowsAggregatedView()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>();

			// Create 7 direct reports (more than the threshold of 5)
			for (int i = 1; i <= 7; i++)
			{
				violationsByOwner[$"direct{i}@test.com"] = new List<WorkItemUpdateSlaViolation>
				{
					new WorkItemUpdateSlaViolation
					{
						WorkItemId = i,
						Title = $"Bug {i}",
						WorkItemType = "Bug",
						DaysSinceUpdate = i,
						SlaThresholdDays = 2,
						Url = $"https://dev.azure.com/test/{i}"
					}
				};
			}

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("**Team Summary** (showing counts only due to team size):", result);
			Assert.Contains("- **direct1@test.com**: 1 violation", result);
			Assert.Contains("💡 *For detailed information, ask me to check SLA violations for specific team members.*", result);
			// Detailed work item info should NOT be shown in aggregated view
			Assert.DoesNotContain("[View work item]", result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_NoManagerViolations_OnlyShowsTeam()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 1,
							Title = "Direct's bug",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/1"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("Your violations: **0**", result);
			Assert.DoesNotContain("### 👤 Your Violations", result);
			Assert.Contains("### 👥 Team Member Violations", result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_NoDirectViolations_OnlyShowsManager()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					managerEmail,
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 100,
							Title = "Manager's bug",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/100"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("Your violations: **1**", result);
			Assert.Contains("Direct reports with violations: **0**", result);
			Assert.Contains("### 👤 Your Violations", result);
			Assert.DoesNotContain("### 👥 Team Member Violations", result);
		}

		[Fact]
		public void ComposeManagerDigestMessage_TruncatesLongLists_ShowsEllipsis()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violations = new List<WorkItemUpdateSlaViolation>();

			// Create more than 10 violations for a single direct report
			for (int i = 1; i <= 15; i++)
			{
				violations.Add(new WorkItemUpdateSlaViolation
				{
					WorkItemId = i,
					Title = $"Bug {i}",
					WorkItemType = "Bug",
					DaysSinceUpdate = i,
					SlaThresholdDays = 2,
					Url = $"https://dev.azure.com/test/{i}"
				});
			}

			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{ "direct1@test.com", violations }
			};

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("*...and 5 more*", result);
			Assert.Contains("Bug 15", result); // Most overdue should be shown
			Assert.DoesNotContain("Bug #1:", result); // Least overdue should be truncated
		}

		[Fact]
		public void ComposeManagerDigestMessage_SortsDirectsByViolationCount_CorrectOrder()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation { WorkItemId = 1, Title = "Bug 1", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url1" }
					}
				},
				{
					"direct2@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation { WorkItemId = 2, Title = "Bug 2", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url2" },
						new WorkItemUpdateSlaViolation { WorkItemId = 3, Title = "Bug 3", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url3" },
						new WorkItemUpdateSlaViolation { WorkItemId = 4, Title = "Bug 4", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url4" }
					}
				},
				{
					"direct3@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation { WorkItemId = 5, Title = "Bug 5", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url5" },
						new WorkItemUpdateSlaViolation { WorkItemId = 6, Title = "Bug 6", WorkItemType = "Bug", DaysSinceUpdate = 5, SlaThresholdDays = 2, Url = "url6" }
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessage(violationsByOwner, managerEmail);

			// Assert
			// direct2 (3 violations) should appear before direct3 (2 violations), which should appear before direct1 (1 violation)
			var indexDirect2 = result.IndexOf("direct2@test.com");
			var indexDirect3 = result.IndexOf("direct3@test.com");
			var indexDirect1 = result.IndexOf("direct1@test.com");

			Assert.True(indexDirect2 < indexDirect3, "direct2 should appear before direct3");
			Assert.True(indexDirect3 < indexDirect1, "direct3 should appear before direct1");
		}

		#endregion

		#region Team-Separated Message Tests

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_SingleTeam_ShowsTeamBadgeOnly()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					managerEmail,
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 100,
							Title = "Manager's bug",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/100",
							TeamId = "team-alpha",
							TeamName = "Team Alpha"
						}
					}
				},
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 200,
							Title = "Direct's bug",
							WorkItemType = "Bug",
							DaysSinceUpdate = 3,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/200",
							TeamId = "team-alpha",
							TeamName = "Team Alpha"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("- Team: Team Alpha", result);
			Assert.DoesNotContain("## 🎯", result); // Should not have team section headers
			Assert.Contains("### 👤 Your Violations", result);
			Assert.Contains("### 👥 Team Member Violations", result);
		}

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_TwoTeams_ShowsTeamSections()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					managerEmail,
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 100,
							Title = "Manager's bug in Alpha",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/100",
							TeamId = "team-alpha",
							TeamName = "Team Alpha"
						},
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 101,
							Title = "Manager's bug in Beta",
							WorkItemType = "Bug",
							DaysSinceUpdate = 6,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/101",
							TeamId = "team-beta",
							TeamName = "Team Beta"
						}
					}
				},
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 200,
							Title = "Direct's bug in Alpha",
							WorkItemType = "Bug",
							DaysSinceUpdate = 3,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/200",
							TeamId = "team-alpha",
							TeamName = "Team Alpha"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("## 🎯 Team Alpha", result);
			Assert.Contains("## 🎯 Team Beta", result);
			Assert.Contains("Manager's bug in Alpha", result);
			Assert.Contains("Manager's bug in Beta", result);
			Assert.Contains("Direct's bug in Alpha", result);
			Assert.Contains("- Teams: Team Alpha, Team Beta", result);
		}

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_ThreeTeams_SortsAlphabetically()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 1,
							Title = "Bug in Zulu",
							WorkItemType = "Bug",
							DaysSinceUpdate = 3,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/1",
							TeamId = "team-zulu",
							TeamName = "Team Zulu"
						},
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 2,
							Title = "Bug in Alpha",
							WorkItemType = "Bug",
							DaysSinceUpdate = 4,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/2",
							TeamId = "team-alpha",
							TeamName = "Team Alpha"
						},
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 3,
							Title = "Bug in Beta",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/3",
							TeamId = "team-beta",
							TeamName = "Team Beta"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			var indexAlpha = result.IndexOf("## 🎯 Team Alpha");
			var indexBeta = result.IndexOf("## 🎯 Team Beta");
			var indexZulu = result.IndexOf("## 🎯 Team Zulu");

			Assert.True(indexAlpha > 0, "Team Alpha section should be present");
			Assert.True(indexBeta > 0, "Team Beta section should be present");
			Assert.True(indexZulu > 0, "Team Zulu section should be present");
			Assert.True(indexAlpha < indexBeta, "Team Alpha should appear before Team Beta");
			Assert.True(indexBeta < indexZulu, "Team Beta should appear before Team Zulu");
		}

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_TeamWithLargeDirectReports_ShowsAggregated()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>();

			// Team Alpha: 7 direct reports (exceeds threshold of 5)
			for (int i = 1; i <= 7; i++)
			{
				violationsByOwner[$"alpha-direct{i}@test.com"] = new List<WorkItemUpdateSlaViolation>
				{
					new WorkItemUpdateSlaViolation
					{
						WorkItemId = i,
						Title = $"Alpha Bug {i}",
						WorkItemType = "Bug",
						DaysSinceUpdate = i,
						SlaThresholdDays = 2,
						Url = $"https://dev.azure.com/test/{i}",
						TeamId = "team-alpha",
						TeamName = "Team Alpha"
					}
				};
			}

			// Team Beta: 2 direct reports (within threshold)
			violationsByOwner["beta-direct1@test.com"] = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 100,
					Title = "Beta Bug 1",
					WorkItemType = "Bug",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/100",
					TeamId = "team-beta",
					TeamName = "Team Beta"
				}
			};

			violationsByOwner["beta-direct2@test.com"] = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 101,
					Title = "Beta Bug 2",
					WorkItemType = "Bug",
					DaysSinceUpdate = 3,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/101",
					TeamId = "team-beta",
					TeamName = "Team Beta"
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			// Team Alpha should show aggregated view
			var alphaSection = result.Substring(result.IndexOf("## 🎯 Team Alpha"), result.IndexOf("## 🎯 Team Beta") - result.IndexOf("## 🎯 Team Alpha"));
			Assert.Contains("**Team Summary** (showing counts only due to team size):", alphaSection);
			Assert.DoesNotContain("[View work item]", alphaSection);

			// Team Beta should show detailed view
			var betaSection = result.Substring(result.IndexOf("## 🎯 Team Beta"));
			Assert.Contains("Beta Bug 1", betaSection);
			Assert.Contains("Beta Bug 2", betaSection);
			Assert.Contains("[View work item]", betaSection);
		}

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_TeamWithNoManagerViolations_OnlyShowsTeamSection()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					managerEmail,
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 100,
							Title = "Manager's bug in Alpha",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/100",
							TeamId = "team-alpha",
							TeamName = "Team Alpha"
						}
					}
				},
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 200,
							Title = "Direct's bug in Beta",
							WorkItemType = "Bug",
							DaysSinceUpdate = 3,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/200",
							TeamId = "team-beta",
							TeamName = "Team Beta"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			// Team Alpha should have manager's violations section
			var alphaSection = result.Substring(result.IndexOf("## 🎯 Team Alpha"), result.IndexOf("## 🎯 Team Beta") - result.IndexOf("## 🎯 Team Alpha"));
			Assert.Contains("### 👤 Your Violations", alphaSection);
			Assert.DoesNotContain("### 👥 Team Member Violations", alphaSection);

			// Team Beta should only have team member violations section
			var betaSection = result.Substring(result.IndexOf("## 🎯 Team Beta"));
			Assert.DoesNotContain("### 👤 Your Violations", betaSection);
			Assert.Contains("### 👥 Team Member Violations", betaSection);
		}

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_TruncatesWithinTeam()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violations = new List<WorkItemUpdateSlaViolation>();

			// Create 15 violations for a single direct report (exceeds max of 10 per direct)
			for (int i = 1; i <= 15; i++)
			{
				violations.Add(new WorkItemUpdateSlaViolation
				{
					WorkItemId = i,
					Title = $"Bug {i}",
					WorkItemType = "Bug",
					DaysSinceUpdate = i,
					SlaThresholdDays = 2,
					Url = $"https://dev.azure.com/test/{i}",
					TeamId = "team-alpha",
					TeamName = "Team Alpha"
				});
			}

			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{ "direct1@test.com", violations }
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			Assert.Contains("*...and 5 more*", result);
			Assert.Contains("Bug 15", result); // Most overdue should be shown
			Assert.DoesNotContain("Bug #1:", result); // Least overdue should be truncated
		}

		[Fact]
		public void ComposeDigestMessageWithTeams_MultiTeamIC_GroupsByTeam()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Bug in Alpha",
					WorkItemType = "Bug",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/1",
					TeamId = "team-alpha",
					TeamName = "Team Alpha"
				},
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 2,
					Title = "Task in Beta",
					WorkItemType = "Task",
					DaysSinceUpdate = 7,
					SlaThresholdDays = 5,
					Url = "https://dev.azure.com/test/2",
					TeamId = "team-beta",
					TeamName = "Team Beta"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessageWithTeams(violations);

			// Assert
			Assert.Contains("across 2 teams", result);
			Assert.Contains("🎯 **Team Alpha**", result);
			Assert.Contains("🎯 **Team Beta**", result);
			Assert.Contains("Bug in Alpha", result);
			Assert.Contains("Task in Beta", result);
		}

		[Fact]
		public void ComposeDigestMessageWithTeams_SingleTeamIC_UsesFallback()
		{
			// Arrange
			var violations = new List<WorkItemUpdateSlaViolation>
			{
				new WorkItemUpdateSlaViolation
				{
					WorkItemId = 1,
					Title = "Bug in Alpha",
					WorkItemType = "Bug",
					DaysSinceUpdate = 5,
					SlaThresholdDays = 2,
					Url = "https://dev.azure.com/test/1",
					TeamId = "team-alpha",
					TeamName = "Team Alpha"
				}
			};

			// Act
			var result = _composer.ComposeDigestMessageWithTeams(violations);

			// Assert
			Assert.Contains("⚠️ SLA Violation Alert", result);
			Assert.Contains("1 work item", result);
			Assert.DoesNotContain("🎯 **Team", result); // Should not have team badges
			Assert.Contains("Bug in Alpha", result);
		}

		[Fact]
		public void ComposeManagerDigestMessageWithTeams_EmptyTeamName_HandlesGracefully()
		{
			// Arrange
			var managerEmail = "manager@test.com";
			var violationsByOwner = new Dictionary<string, List<WorkItemUpdateSlaViolation>>
			{
				{
					"direct1@test.com",
					new List<WorkItemUpdateSlaViolation>
					{
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 1,
							Title = "Bug with no team name",
							WorkItemType = "Bug",
							DaysSinceUpdate = 5,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/1",
							TeamId = "team-123",
							TeamName = string.Empty
						},
						new WorkItemUpdateSlaViolation
						{
							WorkItemId = 2,
							Title = "Bug with team name",
							WorkItemType = "Bug",
							DaysSinceUpdate = 3,
							SlaThresholdDays = 2,
							Url = "https://dev.azure.com/test/2",
							TeamId = "team-456",
							TeamName = "Team Beta"
						}
					}
				}
			};

			// Act
			var result = _composer.ComposeManagerDigestMessageWithTeams(violationsByOwner, managerEmail);

			// Assert
			// Should use TeamId as fallback when TeamName is empty
			Assert.Contains("## 🎯 team-123", result);
			Assert.Contains("## 🎯 Team Beta", result);
			Assert.Contains("Bug with no team name", result);
			Assert.Contains("Bug with team name", result);
		}

		#endregion
	}
}
