using Hermes.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Hermes.Tests.Configuration
{
	public class SchedulingConfigurationTests
	{
		[Fact]
		public void SchedulingConfiguration_DefaultValues_AreCorrect()
		{
			// Arrange & Act
			var config = new SchedulingConfiguration();

			// Assert
			Assert.True(config.EnableScheduler);
			Assert.NotNull(config.Jobs);
			Assert.Empty(config.Jobs);
		}

		[Fact]
		public void SchedulingConfiguration_BindsFromConfiguration()
		{
			// Arrange
			var configData = new Dictionary<string, string>
			{
				{ "Scheduling:EnableScheduler", "false" },
				{ "Scheduling:Jobs:0:JobName", "TestJob" },
				{ "Scheduling:Jobs:0:JobType", "Test" },
				{ "Scheduling:Jobs:0:Enabled", "true" },
				{ "Scheduling:Jobs:0:CronExpression", "0 0 9 * * ?" },
				{ "Scheduling:Jobs:0:TimeZone", "UTC" }
			};

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(configData!)
				.Build();

			var schedulingConfig = new SchedulingConfiguration();

			// Act
			configuration.GetSection("Scheduling").Bind(schedulingConfig);

			// Assert
			Assert.False(schedulingConfig.EnableScheduler);
			Assert.Single(schedulingConfig.Jobs);
			Assert.Equal("TestJob", schedulingConfig.Jobs[0].JobName);
			Assert.Equal("Test", schedulingConfig.Jobs[0].JobType);
			Assert.True(schedulingConfig.Jobs[0].Enabled);
			Assert.Equal("0 0 9 * * ?", schedulingConfig.Jobs[0].CronExpression);
			Assert.Equal("UTC", schedulingConfig.Jobs[0].TimeZone);
		}

		[Fact]
		public void JobConfiguration_DefaultValues_AreCorrect()
		{
			// Arrange & Act
			var config = new JobConfiguration();

			// Assert
			Assert.Equal(string.Empty, config.JobName);
			Assert.Equal(string.Empty, config.JobType);
			Assert.True(config.Enabled);
			Assert.Equal(string.Empty, config.CronExpression);
			Assert.Equal("America/Los_Angeles", config.TimeZone);
			Assert.NotNull(config.Parameters);
			Assert.Empty(config.Parameters);
		}

		[Fact]
		public void JobConfiguration_CanSetAllProperties()
		{
			// Arrange
			var config = new JobConfiguration
			{
				JobName = "MyJob",
				JobType = "MyType",
				Enabled = false,
				CronExpression = "0 0 12 * * ?",
				TimeZone = "America/New_York",
				Parameters = new Dictionary<string, string>
				{
					{ "key1", "value1" },
					{ "key2", "value2" }
				}
			};

			// Assert
			Assert.Equal("MyJob", config.JobName);
			Assert.Equal("MyType", config.JobType);
			Assert.False(config.Enabled);
			Assert.Equal("0 0 12 * * ?", config.CronExpression);
			Assert.Equal("America/New_York", config.TimeZone);
			Assert.Equal(2, config.Parameters.Count);
			Assert.Equal("value1", config.Parameters["key1"]);
			Assert.Equal("value2", config.Parameters["key2"]);
		}
	}
}
