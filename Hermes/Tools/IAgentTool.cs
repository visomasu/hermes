namespace Hermes.Tools
{
	/// <summary>
	/// Interface for agent tools, supporting capability discovery and invocation.
	/// </summary>
	public interface IAgentTool
	{
		/// <summary>
		/// Unique name of the tool.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Short description of the tool's purpose.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// List of operations/capabilities this tool exposes.
		/// </summary>
		IReadOnlyList<string> Capabilities { get; }

		/// <summary>
		/// Returns metadata describing the tool's input/output schemas or usage.
		/// </summary>
		string GetMetadata();

		/// <summary>
		/// Executes the tool with the given input (usually JSON or DTO).
		/// </summary>
		/// <param name="operation">The capability/operation to invoke.</param>
		/// <param name="input">Input parameters as JSON string.</param>
		/// <returns>Result as JSON string.</returns>
		Task<string> ExecuteAsync(string operation, string input);
	}
}
