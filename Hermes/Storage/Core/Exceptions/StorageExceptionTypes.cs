namespace Hermes.Storage.Core.Exceptions
{
	/// <summary>
	/// Common set of exception error codes for storage operations.
	/// </summary>
	public static class StorageExceptionTypes
	{
		public enum ErrorCode
		{
			Unknown,
			NotFound,
			AlreadyExists,
			InvalidInput,
			ConnectionFailed,
			Timeout,
			PermissionDenied,
			SerializationError,
			DeserializationError,
			OperationFailed
		}
	}
}
