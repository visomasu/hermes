using Xunit;
using Hermes.Storage.Core.Exceptions;

namespace Hermes.Tests.Storage.Core.Exceptions
{
	public class StorageExceptionTests
	{
		[Fact]
		public void StorageException_DefaultErrorCode_IsUnknown()
		{
			var ex = new StorageException();
			Xunit.Assert.Equal(StorageExceptionTypes.ErrorCode.Unknown, ex.ErrorCode);
		}

		[Fact]
		public void StorageException_WithErrorCode_SetsErrorCode()
		{
			var ex = new StorageException("error", StorageExceptionTypes.ErrorCode.NotFound);
			Xunit.Assert.Equal(StorageExceptionTypes.ErrorCode.NotFound, ex.ErrorCode);
		}
	}
}
