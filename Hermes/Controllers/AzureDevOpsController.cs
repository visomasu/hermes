using Integrations.AzureDevOps;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Hermes.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AzureDevOpsController : ControllerBase
	{
		private readonly IAzureDevOpsWorkItemClient _workItemClient;
		private readonly ILogger<AzureDevOpsController> _logger;

        // Mandatory fields: Id, Title, Description
        private static readonly IEnumerable<string> fieldsToQuery = new[]
        {
            "Custom.RiskAssessmentComment",
            "dynamicscrm.SREAgilev2.Summary",
        };

        public AzureDevOpsController(IAzureDevOpsWorkItemClient workItemClient, ILogger<AzureDevOpsController> logger)
		{
			_workItemClient = workItemClient;
			_logger = logger;
		}

		/// <summary>
		/// Retrieves a work item by its ID from Azure DevOps.
		/// </summary>
		/// <param name="id">The work item ID.</param>
		/// <returns>The work item details if found, otherwise NotFound.</returns>
		[HttpGet("workitem/{id}")]
		public async Task<ActionResult<string>> GetWorkItem(int id)
		{
			_logger.LogInformation("[{ClassName}] Entry: GetWorkItem endpoint called for id {Id}.", nameof(AzureDevOpsController), id);
			try
			{
				var workItemJson = await _workItemClient.GetWorkItemAsync(id, fieldsToQuery);

				if (string.IsNullOrEmpty(workItemJson))
				{
                    return NotFound();
                }

				return Ok(workItemJson);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving work item {Id}.", id);
				return StatusCode(500, "Error retrieving work item.");
			}
		}
	}
}
