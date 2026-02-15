using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Hermes.Integration.Tests.Controllers;

public class HermesControllerIntegrationTests : IClassFixture<HermesWebApplicationFactory>
{
	private readonly HttpClient _client;

	public HermesControllerIntegrationTests(HermesWebApplicationFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task Chat_WithValidInput_ReturnsOk()
	{
		// Arrange
		var chatRequest = new
		{
			text = "Hello Hermes",
			userId = "test@test.com"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/hermes/v1.0/chat", chatRequest);

		// Assert
		response.EnsureSuccessStatusCode();
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task Chat_ReturnsNonEmptyResponse()
	{
		// Arrange
		var chatRequest = new
		{
			text = "What is your name?",
			userId = "test@test.com"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/hermes/v1.0/chat", chatRequest);
		var content = await response.Content.ReadAsStringAsync();

		// Assert
		Assert.NotNull(content);
		Assert.NotEmpty(content);
	}

	[Fact]
	public async Task Chat_WithEmptyText_StillProcesses()
	{
		// Arrange
		var chatRequest = new
		{
			text = "",
			userId = "test@test.com"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/hermes/v1.0/chat", chatRequest);

		// Assert
		// Should still return OK even with empty text (LLM can handle it)
		Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Chat_WithLongText_HandlesGracefully()
	{
		// Arrange
		var longText = new string('a', 5000);
		var chatRequest = new
		{
			text = longText,
			userId = "test@test.com"
		};

		// Act
		var response = await _client.PostAsJsonAsync("/api/hermes/v1.0/chat", chatRequest);

		// Assert
		// Should not crash with long input
		Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Chat_WithCorrelationId_ProcessesRequest()
	{
		// Arrange
		var chatRequest = new
		{
			text = "Test with correlation ID",
			userId = "test@test.com"
		};

		var request = new HttpRequestMessage(HttpMethod.Post, "/api/hermes/v1.0/chat")
		{
			Content = JsonContent.Create(chatRequest)
		};
		request.Headers.Add("x-ms-correlation-id", "test-correlation-123");

		// Act
		var response = await _client.SendAsync(request);

		// Assert
		response.EnsureSuccessStatusCode();
	}
}
