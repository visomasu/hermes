using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;

namespace Hermes.Authentication
{
	/// <summary>
	/// No-op access token provider for local development with Agents Playground.
	/// This provider is dynamically loaded by the SDK's ConfigurationConnections
	/// and returns empty tokens for any serviceUrl.
	/// </summary>
	public class LocalDevelopmentAuthentication : IAccessTokenProvider
	{
		/// <summary>
		/// Constructor required by SDK's AuthModuleLoader for dynamic instantiation.
		/// The SDK passes IServiceProvider and IConfiguration when loading from config.
		/// </summary>
		public LocalDevelopmentAuthentication(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			// No-op constructor for local development
			// SDK will pass service provider and configuration from DI
		}

		public ImmutableConnectionSettings? ConnectionSettings => null;

		public Task<string> GetAccessTokenAsync(string resource, bool forceRefresh = false)
		{
			// Return empty token for local development
			return Task.FromResult(string.Empty);
		}

		public Task<string> GetAccessTokenAsync(string resource, IList<string> scopes, bool forceRefresh = false)
		{
			// Return empty token for local development
			return Task.FromResult(string.Empty);
		}

		public TokenCredential? GetTokenCredential()
		{
			// Return null for local development
			return null;
		}
	}
}
