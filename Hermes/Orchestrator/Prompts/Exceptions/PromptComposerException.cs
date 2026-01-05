using System;

namespace Hermes.Orchestrator.Prompts.Exceptions
{
	/// <summary>
	/// Exception thrown when an agent prompt cannot be constructed from the underlying instruction files.
	/// </summary>
	public sealed class PromptComposerException : Exception
	{
		/// <summary>
		/// Gets the error code that describes the reason prompt composition failed.
		/// </summary>
		public PromptComposerErrorCode ErrorCode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PromptComposerException"/> class.
		/// </summary>
		/// <param name="message">A human-readable description of the failure.</param>
		/// <param name="errorCode">The error code describing why prompt composition failed.</param>
		/// <param name="innerException">Optional inner exception that triggered this failure.</param>
		public PromptComposerException(string message, PromptComposerErrorCode errorCode, Exception? innerException = null)
			: base(message, innerException)
		{
			ErrorCode = errorCode;
		}
	}

	/// <summary>
	/// Error codes representing known prompt composition failure reasons.
	/// </summary>
	public enum PromptComposerErrorCode
	{
		/// <summary>
		/// The agentspec.json file for the requested instruction type could not be found.
		/// </summary>
		AgentSpecNotFound = 1,

		/// <summary>
		/// The agentspec.json file could not be parsed into a valid AgentSpec.
		/// </summary>
		AgentSpecInvalid = 2,

		/// <summary>
		/// The agentspec.json file does not declare any capabilities for the instruction type.
		/// </summary>
		NoCapabilitiesDefined = 3
	}
}
