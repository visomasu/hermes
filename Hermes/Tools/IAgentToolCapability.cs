namespace Hermes.Tools
{
	/// <summary>
	/// Represents a single capability or operation exposed by an agent tool.
	/// </summary>
	public interface IAgentToolCapability<TInput>
		where TInput : class
	{
		/// <summary>
		/// Unique name of the capability/operation.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Short description of what this capability does.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Executes this capability with the given, strongly-typed input model.
		/// </summary>
		/// <param name="input">Input model that extends ToolCapabilityInputBase.</param>
		/// <returns>Result as JSON string.</returns>
		Task<string> ExecuteAsync(TInput input);
	}
}
