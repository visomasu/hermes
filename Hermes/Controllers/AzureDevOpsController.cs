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

		/// <summary>
		/// Retrieves the current iteration path for a given team based on the current date.
		/// </summary>
		/// <param name="teamName">The team name to query (e.g., "OneCRM Team").</param>
		/// <returns>The current iteration path if found, otherwise NotFound.</returns>
		[HttpGet("current-iteration/{teamName}")]
		public async Task<ActionResult<string>> GetCurrentIteration(string teamName)
		{
			_logger.LogInformation("[{ClassName}] Entry: GetCurrentIteration endpoint called for team {TeamName}.", nameof(AzureDevOpsController), teamName);
			try
			{
				var iterationPath = await _workItemClient.GetCurrentIterationPathAsync(teamName);

				if (string.IsNullOrEmpty(iterationPath))
				{
					_logger.LogWarning("No current iteration found for team {TeamName}.", teamName);
					return NotFound(new { error = $"No current iteration found for team '{teamName}'." });
				}

				_logger.LogInformation("Current iteration for team {TeamName}: {IterationPath}", teamName, iterationPath);
				return Ok(new { teamName, iterationPath, timestamp = DateTime.UtcNow });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving current iteration for team {TeamName}.", teamName);
				return StatusCode(500, new { error = "Error retrieving current iteration.", details = ex.Message });
			}
		}
	}
}
