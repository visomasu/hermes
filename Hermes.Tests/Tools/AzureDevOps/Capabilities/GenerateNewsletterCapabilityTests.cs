using Hermes.Tools;
using Hermes.Tools.AzureDevOps.Capabilities;
using Hermes.Tools.AzureDevOps.Capabilities.Inputs;
using Hermes.Orchestrator.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using System.Text.Json;
using Xunit;

namespace Hermes.Tests.Tools.AzureDevOps.Capabilities
{
	public class GenerateNewsletterCapabilityTests
	{
		private readonly Mock<IAgentToolCapability<GetWorkItemTreeCapabilityInput>> _mockTreeCapability;
		private readonly Mock<IModelSelector> _mockModelSelector;
		private readonly Mock<ChatClient> _mockChatClient;
		private readonly Mock<ILogger<GenerateNewsletterCapability>> _mockLogger;

		public GenerateNewsletterCapabilityTests()
		{
			_mockTreeCapability = new Mock<IAgentToolCapability<GetWorkItemTreeCapabilityInput>>();
			_mockModelSelector = new Mock<IModelSelector>();
			_mockChatClient = new Mock<ChatClient>();
			_mockLogger = new Mock<ILogger<GenerateNewsletterCapability>>();
		}

		[Fact]
		public void Name_ReturnsGenerateNewsletter()
		{
			// Arrange
			var capability = new GenerateNewsletterCapability(
				_mockTreeCapability.Object,
				_mockModelSelector.Object,
				_mockLogger.Object);

			// Act
			var name = capability.Name;

			// Assert
			Assert.Equal("GenerateNewsletter", name);
		}

		[Fact]
		public void Description_ReturnsExpectedValue()
		{
			// Arrange
			var capability = new GenerateNewsletterCapability(
				_mockTreeCapability.Object,
				_mockModelSelector.Object,
				_mockLogger.Object);

			// Act
			var description = capability.Description;

			// Assert
			Assert.Contains("newsletter", description.ToLowerInvariant());
			Assert.Contains("executive", description.ToLowerInvariant());
		}

		[Fact]
		public async Task ExecuteAsync_CallsGetWorkItemTreeCapability()
		{
			// Arrange
			var hierarchyJson = "{\"workItem\": {\"id\": 123, \"fields\": {\"System.Title\": \"Test Feature\"}}}";
			_mockTreeCapability
				.Setup(x => x.ExecuteAsync(It.IsAny<GetWorkItemTreeCapabilityInput>()))
				.ReturnsAsync(hierarchyJson);

			// Mock the ChatClient return (simplified since ChatClient is sealed)
			// In real scenario, this would require more complex mocking or integration testing
			var capability = CreateCapabilityWithMockedChat();

			var input = new GenerateNewsletterCapabilityInput
			{
				WorkItemId = 123,
				Format = "Executive",
				Depth = 3
			};

			// Act
			try
			{
				await capability.ExecuteAsync(input);
			}
			catch
			{
				// Expected to fail at ChatClient.CompleteChatAsync since we can't fully mock sealed classes
				// But we can still verify the tree capability was called
			}

			// Assert
			_mockTreeCapability.Verify(
				x => x.ExecuteAsync(It.Is<GetWorkItemTreeCapabilityInput>(
					i => i.WorkItemId == 123 && i.Depth == 3)),
				Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_UsesDefaultDepthWhenZero()
		{
			// Arrange
			var hierarchyJson = "{\"workItem\": {\"id\": 123}}";
			_mockTreeCapability
				.Setup(x => x.ExecuteAsync(It.IsAny<GetWorkItemTreeCapabilityInput>()))
				.ReturnsAsync(hierarchyJson);

			var capability = CreateCapabilityWithMockedChat();

			var input = new GenerateNewsletterCapabilityInput
			{
				WorkItemId = 123,
				Depth = 0 // Should default to 3
			};

			// Act
			try
			{
				await capability.ExecuteAsync(input);
			}
			catch
			{
				// Expected - see note in previous test
			}

			// Assert - Should use depth of 3 when input depth is 0
			_mockTreeCapability.Verify(
				x => x.ExecuteAsync(It.Is<GetWorkItemTreeCapabilityInput>(
					i => i.WorkItemId == 123 && i.Depth == 3)),
				Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_CallsModelSelectorForNewsletterGeneration()
		{
			// Arrange
			var hierarchyJson = "{\"workItem\": {\"id\": 123}}";
			_mockTreeCapability
				.Setup(x => x.ExecuteAsync(It.IsAny<GetWorkItemTreeCapabilityInput>()))
				.ReturnsAsync(hierarchyJson);

			_mockModelSelector
				.Setup(x => x.GetModelForOperation("NewsletterGeneration"))
				.Returns("gpt-4o");

			var capability = CreateCapabilityWithMockedChat();

			var input = new GenerateNewsletterCapabilityInput { WorkItemId = 123 };

			// Act
			try
			{
				await capability.ExecuteAsync(input);
			}
			catch
			{
				// Expected - see note in previous test
			}

			// Assert
			_mockModelSelector.Verify(
				x => x.GetChatClientForOperation("NewsletterGeneration"),
				Times.Once);

			_mockModelSelector.Verify(
				x => x.GetModelForOperation("NewsletterGeneration"),
				Times.AtLeastOnce);
		}

		[Fact]
		public async Task ExecuteAsync_LogsNewsletterGenerationStart()
		{
			// Arrange
			var hierarchyJson = "{\"workItem\": {\"id\": 123}}";
			_mockTreeCapability
				.Setup(x => x.ExecuteAsync(It.IsAny<GetWorkItemTreeCapabilityInput>()))
				.ReturnsAsync(hierarchyJson);

			var capability = CreateCapabilityWithMockedChat();

			var input = new GenerateNewsletterCapabilityInput
			{
				WorkItemId = 123,
				Format = "Executive"
			};

			// Act
			try
			{
				await capability.ExecuteAsync(input);
			}
			catch
			{
				// Expected - see note in previous test
			}

			// Assert - Verify Information log was called
			_mockLogger.Verify(
				x => x.Log(
					LogLevel.Information,
					It.IsAny<EventId>(),
					It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting newsletter generation")),
					It.IsAny<Exception>(),
					It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
				Times.AtLeastOnce);
		}

		[Fact]
		public void Constructor_ThrowsWhenTreeCapabilityIsNull()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() =>
				new GenerateNewsletterCapability(
					null!,
					_mockModelSelector.Object,
					_mockLogger.Object));
		}

		[Fact]
		public void Constructor_ThrowsWhenModelSelectorIsNull()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() =>
				new GenerateNewsletterCapability(
					_mockTreeCapability.Object,
					null!,
					_mockLogger.Object));
		}

		[Fact]
		public void Constructor_ThrowsWhenLoggerIsNull()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() =>
				new GenerateNewsletterCapability(
					_mockTreeCapability.Object,
					_mockModelSelector.Object,
					null!));
		}

		/// <summary>
		/// Helper to create capability with mocked ChatClient.
		/// Note: Full ChatClient mocking is complex due to sealed class.
		/// For comprehensive testing, integration tests are recommended.
		/// </summary>
		private GenerateNewsletterCapability CreateCapabilityWithMockedChat()
		{
			// Setup ModelSelector to return the mock ChatClient
			_mockModelSelector
				.Setup(x => x.GetChatClientForOperation("NewsletterGeneration"))
				.Returns(_mockChatClient.Object);

			_mockModelSelector
				.Setup(x => x.GetModelForOperation("NewsletterGeneration"))
				.Returns("gpt-4o");

			return new GenerateNewsletterCapability(
				_mockTreeCapability.Object,
				_mockModelSelector.Object,
				_mockLogger.Object);
		}
	}
}
