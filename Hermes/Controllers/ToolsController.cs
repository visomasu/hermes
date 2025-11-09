using Microsoft.AspNetCore.Mvc;
using Hermes.Tools.AzureDevOps;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hermes.Controllers
{
	[ApiController]
	[Route("api/tools")]
	public class ToolsController : ControllerBase
	{
		private readonly AzureDevOpsTool _azureDevOpsTool;

		public ToolsController(AzureDevOpsTool azureDevOpsTool)
		{
			_azureDevOpsTool = azureDevOpsTool;
		}

		/// <summary>
		/// Retrieves the work item tree from Azure DevOps.
		/// </summary>
		/// <param name="rootId">Root work item ID.</param>
		/// <param name="depth">Depth of tree to retrieve.</param>
		/// <returns>JSON representation of the work item tree.</returns>
		[HttpGet("workitemtree")]
		public async Task<IActionResult> GetWorkItemTree([FromQuery] int rootId, [FromQuery] int depth)
		{
			var input = JsonSerializer.Serialize(new { rootId, depth });
			var result = await _azureDevOpsTool.ExecuteAsync("GetWorkItemTree", input);
			return Content(result, "application/json");
		}
	}
}
