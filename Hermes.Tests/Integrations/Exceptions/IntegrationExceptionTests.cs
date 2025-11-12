using Xunit;
using Exceptions;
using System;

namespace Hermes.Tests.Integrations.Exceptions
{
	public class IntegrationExceptionTests
	{
		[Fact]
		public void Constructor_SetsPropertiesCorrectly()
		{
			var ex = new IntegrationException("error", IntegrationException.ErrorCode.ServiceError);
			Assert.Equal("error", ex.Message);
			Assert.Equal(IntegrationException.ErrorCode.ServiceError, ex.Code);
		}

		[Fact]
		public void Constructor_WithInnerException_SetsInnerException()
		{
			var inner = new Exception("inner");
			var ex = new IntegrationException("error", IntegrationException.ErrorCode.AuthenticationError, inner);
			Assert.Equal(inner, ex.InnerException);
			Assert.Equal(IntegrationException.ErrorCode.AuthenticationError, ex.Code);
		}
	}
}
