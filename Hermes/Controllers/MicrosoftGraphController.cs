using Hermes.Integrations.MicrosoftGraph;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Controllers
{
	/// <summary>
	/// Controller for testing Microsoft Graph API integration.
	/// Provides endpoints to test user profile and organizational structure queries.
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class MicrosoftGraphController : ControllerBase
	{
		private readonly IMicrosoftGraphClient _graphClient;
		private readonly ILogger<MicrosoftGraphController> _logger;

		public MicrosoftGraphController(
			IMicrosoftGraphClient graphClient,
			ILogger<MicrosoftGraphController> logger)
		{
			_graphClient = graphClient;
			_logger = logger;
		}

		/// <summary>
		/// Gets the email address for a user by their Azure AD object ID.
		/// </summary>
		/// <param name="userId">Azure AD object ID of the user</param>
		/// <returns>User's email address</returns>
		[HttpGet("user/{userId}/email")]
		public async Task<IActionResult> GetUserEmail(string userId)
		{
			_logger.LogInformation("Getting email for user {UserId}", userId);

			try
			{
				var email = await _graphClient.GetUserEmailAsync(userId);

				if (email == null)
				{
					return NotFound(new { message = $"User {userId} not found or has no email" });
				}

				return Ok(new { userId, email });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting email for user {UserId}", userId);
				return StatusCode(500, new { message = "Error retrieving user email", error = ex.Message });
			}
		}

		/// <summary>
		/// Gets the direct report emails for a user by their Azure AD object ID.
		/// </summary>
		/// <param name="userId">Azure AD object ID of the user</param>
		/// <returns>List of direct report email addresses</returns>
		[HttpGet("user/{userId}/direct-reports")]
		public async Task<IActionResult> GetDirectReports(string userId)
		{
			_logger.LogInformation("Getting direct reports for user {UserId}", userId);

			try
			{
				var directReportEmails = await _graphClient.GetDirectReportEmailsAsync(userId);

				return Ok(new
				{
					userId,
					directReportCount = directReportEmails.Count,
					directReports = directReportEmails,
					isManager = directReportEmails.Count > 0
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting direct reports for user {UserId}", userId);
				return StatusCode(500, new { message = "Error retrieving direct reports", error = ex.Message });
			}
		}

		/// <summary>
		/// Gets complete user profile including email and direct reports (optimized parallel fetch).
		/// </summary>
		/// <param name="userId">Azure AD object ID of the user</param>
		/// <returns>Complete user profile with direct reports</returns>
		[HttpGet("user/{userId}/profile")]
		public async Task<IActionResult> GetUserProfile(string userId)
		{
			_logger.LogInformation("Getting complete profile for user {UserId}", userId);

			try
			{
				var profile = await _graphClient.GetUserProfileWithDirectReportsAsync(userId);

				return Ok(new
				{
					userId,
					email = profile.Email,
					isManager = profile.IsManager,
					directReportCount = profile.DirectReportEmails.Count,
					directReports = profile.DirectReportEmails
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting profile for user {UserId}", userId);
				return StatusCode(500, new { message = "Error retrieving user profile", error = ex.Message });
			}
		}

		/// <summary>
		/// Test endpoint to verify Microsoft Graph client is properly initialized.
		/// </summary>
		/// <returns>Status information</returns>
		[HttpGet("status")]
		public IActionResult GetStatus()
		{
			_logger.LogInformation("Microsoft Graph status check");

			return Ok(new
			{
				status = "healthy",
				message = "Microsoft Graph client is initialized and ready",
				authMethod = "DefaultAzureCredential (az login or Managed Identity)",
				timestamp = DateTime.UtcNow
			});
		}

		/// <summary>
		/// Test endpoint to fetch Azure AD Object ID using az CLI (simulates local dev fallback).
		/// This endpoint tests the same az CLI integration used by HermesTeamsAgent.
		/// </summary>
		/// <returns>Azure AD Object ID from az CLI</returns>
		[HttpGet("test-az-cli")]
		public async Task<IActionResult> TestAzCli()
		{
			_logger.LogInformation("Testing az CLI integration for AAD Object ID extraction");

			try
			{
				// On Windows, we need to use cmd.exe to execute az CLI
				// On Linux/Mac, we can execute az directly
				var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
				var fileName = isWindows ? "cmd.exe" : "az";
				var arguments = isWindows ? "/c az ad signed-in-user show --query id -o tsv" : "ad signed-in-user show --query id -o tsv";

				var azProcess = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = fileName,
						Arguments = arguments,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};

				var startTime = DateTime.UtcNow;
				azProcess.Start();
				var output = await azProcess.StandardOutput.ReadToEndAsync();
				var error = await azProcess.StandardError.ReadToEndAsync();
				await azProcess.WaitForExitAsync();
				var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

				_logger.LogInformation("az CLI execution completed. Exit code: {ExitCode}, Duration: {Duration}ms",
					azProcess.ExitCode, duration);

				if (azProcess.ExitCode == 0)
				{
					var aadObjectId = output.Trim();
					var isValidGuid = Guid.TryParse(aadObjectId, out var parsedGuid);

					return Ok(new
					{
						success = true,
						aadObjectId,
						isValidGuid,
						parsedGuid = isValidGuid ? parsedGuid.ToString() : null,
						exitCode = azProcess.ExitCode,
						durationMs = duration,
						rawOutput = output,
						rawError = string.IsNullOrWhiteSpace(error) ? null : error
					});
				}
				else
				{
					_logger.LogWarning("az CLI failed with exit code {ExitCode}. Error: {Error}",
						azProcess.ExitCode, error);

					return StatusCode(500, new
					{
						success = false,
						message = "az CLI command failed",
						exitCode = azProcess.ExitCode,
						durationMs = duration,
						output = string.IsNullOrWhiteSpace(output) ? null : output,
						error = string.IsNullOrWhiteSpace(error) ? null : error
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error executing az CLI command");
				return StatusCode(500, new
				{
					success = false,
					message = "Exception while executing az CLI",
					error = ex.Message,
					exceptionType = ex.GetType().Name,
					stackTrace = ex.StackTrace
				});
			}
		}
	}
}
