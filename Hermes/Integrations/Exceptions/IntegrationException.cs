using System;

namespace Exceptions
{
	/// <summary>
	/// Exception type for integration errors with external services.
	/// </summary>
	public class IntegrationException : Exception
	{
		/// <summary>
		/// Error codes for integration exceptions.
		/// </summary>
		public enum ErrorCode
		{
			/// <summary>Service returned an error.</summary>
			ServiceError,
			/// <summary>Authentication failed.</summary>
			AuthenticationError,
			/// <summary>An unexpected error occurred.</summary>
			UnexpectedError
		}

		/// <summary>
		/// Gets the error code for the integration exception.
		/// </summary>
		public ErrorCode Code { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IntegrationException"/> class with a specified error message and error code.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="code">The error code.</param>
		public IntegrationException(string message, ErrorCode code)
			: base(message)
		{
			Code = code;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IntegrationException"/> class with a specified error message, error code, and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="code">The error code.</param>
		/// <param name="innerException">The inner exception.</param>
		public IntegrationException(string message, ErrorCode code, Exception innerException)
			: base(message, innerException)
		{
			Code = code;
		}
	}
}
