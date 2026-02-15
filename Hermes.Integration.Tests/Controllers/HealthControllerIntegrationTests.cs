using System.Net;
using Xunit;

namespace Hermes.Integration.Tests.Controllers;

public class HealthControllerIntegrationTests : IClassFixture<HermesWebApplicationFactory>
{
	private readonly HttpClient _client;

	public HealthControllerIntegrationTests(HermesWebApplicationFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task Health_ReturnsSuccessStatusCode()
	{
		// Act
		var response = await _client.GetAsync("/health");

		// Assert
		response.EnsureSuccessStatusCode();
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task Health_ReturnsHealthyResponse()
	{
		// Act
		var response = await _client.GetAsync("/health");
		var content = await response.Content.ReadAsStringAsync();

		// Assert
		Assert.NotNull(content);
		Assert.Contains("Healthy", content);
	}

	[Fact]
	public async Task Health_ReturnsValidJson()
	{
		// Act
		var response = await _client.GetAsync("/health");
		var content = await response.Content.ReadAsStringAsync();

		// Assert
		Assert.StartsWith("{", content);
		Assert.EndsWith("}", content);
	}
}
