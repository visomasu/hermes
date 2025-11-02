using System;

namespace Hermes.Storage.Core.Exceptions
{
	/// <summary>
	/// Represents errors that occur during storage operations.
	/// </summary>
	public class StorageException : Exception
	{
		/// <summary>
		/// Gets the error code associated with this storage exception.
		/// </summary>
		public StorageExceptionTypes.ErrorCode ErrorCode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageException"/> class with the default error code.
		/// </summary>
		public StorageException() : this(StorageExceptionTypes.ErrorCode.Unknown) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message and the default error code.
		/// </summary>
		/// <param name="message">The error message.</param>
		public StorageException(string message) : this(message, StorageExceptionTypes.ErrorCode.Unknown) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message, inner exception, and the default error code.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		public StorageException(string message, Exception innerException) : this(message, innerException, StorageExceptionTypes.ErrorCode.Unknown) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageException"/> class with a specified error code.
		/// </summary>
		/// <param name="errorCode">The error code.</param>
		public StorageException(StorageExceptionTypes.ErrorCode errorCode) : base(errorCode.ToString())
		{
			ErrorCode = errorCode;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message and error code.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="errorCode">The error code.</param>
		public StorageException(string message, StorageExceptionTypes.ErrorCode errorCode) : base(message)
		{
			ErrorCode = errorCode;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message, inner exception, and error code.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <param name="errorCode">The error code.</param>
		public StorageException(string message, Exception innerException, StorageExceptionTypes.ErrorCode errorCode) : base(message, innerException)
		{
			ErrorCode = errorCode;
		}
	}
}
