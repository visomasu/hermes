using Hermes.Storage.Repositories;
using Hermes.Storage.Repositories.Sample;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class StorageController : ControllerBase
	{
		private readonly IRepository<SampleRepositoryModel> _repository;
		private readonly ILogger<StorageController> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageController"/> class.
		/// </summary>
		/// <param name="repository">The sample repository.</param>
		/// <param name="logger">The logger instance.</param>
		public StorageController(IRepository<SampleRepositoryModel> repository, ILogger<StorageController> logger)
		{
			_repository = repository;
			_logger = logger;
		}

		/// <summary>
		/// Creates a new sample repository model.
		/// </summary>
		/// <param name="model">The model to create.</param>
		/// <returns>HTTP200 OK if successful.</returns>
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] SampleRepositoryModel model)
		{
			_logger.LogInformation("[{ClassName}] Entry: Create endpoint called.", nameof(StorageController));

			await _repository.CreateAsync(model);
			return Ok();
		}

		/// <summary>
		/// Reads a sample repository model by id.
		/// </summary>
		/// <param name="id">The id of the model to read.</param>
		/// <returns>The model if found, otherwise HTTP404 Not Found.</returns>
		[HttpGet("{id}")]
		public async Task<ActionResult<SampleRepositoryModel?>> Read(string id)
		{
			_logger.LogInformation("[{ClassName}] Entry: Read endpoint called for id {Id}.", nameof(StorageController), id);

			var result = await _repository.ReadAsync(id);
			if (result == null) return NotFound();
			return Ok(result);
		}

		/// <summary>
		/// Updates a sample repository model by id.
		/// </summary>
		/// <param name="id">The id of the model to update.</param>
		/// <param name="model">The updated model.</param>
		/// <returns>HTTP200 OK if successful.</returns>
		[HttpPut("{id}")]
		public async Task<IActionResult> Update(string id, [FromBody] SampleRepositoryModel model)
		{
			_logger.LogInformation("[{ClassName}] Entry: Update endpoint called for id {Id}.", nameof(StorageController), id);

			await _repository.UpdateAsync(id, model);
			return Ok();
		}

		/// <summary>
		/// Deletes a sample repository model by id.
		/// </summary>
		/// <param name="id">The id of the model to delete.</param>
		/// <returns>HTTP200 OK if successful.</returns>
		[HttpDelete("{id}")]
		public async Task<IActionResult> Delete(string id)
		{
			_logger.LogInformation("[{ClassName}] Entry: Delete endpoint called for id {Id}.", nameof(StorageController), id);

			await _repository.DeleteAsync(id);
			return Ok();
		}
	}
}
