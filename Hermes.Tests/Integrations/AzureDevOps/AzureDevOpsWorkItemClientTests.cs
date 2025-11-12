using Xunit;
using Integrations.AzureDevOps;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Hermes.Tests.Integrations.AzureDevOps
{
	public class AzureDevOpsWorkItemClientTests
	{
		[Fact]
		public async Task GetWorkItemAsync_ThrowsIntegrationException_OnError()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemAsync(-1));
		}

		[Fact]
		public async Task GetWorkItemsAsync_ThrowsIntegrationException_OnNullIds()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsAsync(null!, null));
		}

		[Fact]
		public async Task GetWorkItemsAsync_ThrowsIntegrationException_OnEmptyIds()
		{
			var client = new AzureDevOpsWorkItemClient("invalidOrg", "invalidProject", "invalidPat");
			await Assert.ThrowsAsync<IntegrationException>(() => client.GetWorkItemsAsync(new int[0], null));
		}
	}
}
