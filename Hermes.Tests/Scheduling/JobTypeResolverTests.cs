using Hermes.Scheduling;
using Hermes.Scheduling.Jobs;
using Quartz;
using Xunit;

namespace Hermes.Tests.Scheduling
{
	public class JobTypeResolverTests
	{
		[Fact]
		public void GetJobType_ValidJobType_ReturnsCorrectType()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act
			var jobType = resolver.GetJobType("Sample");

			// Assert
			Assert.Equal(typeof(SampleJob), jobType);
		}

		[Fact]
		public void GetJobType_CaseInsensitive_ReturnsCorrectType()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act
			var jobType = resolver.GetJobType("SAMPLE");

			// Assert
			Assert.Equal(typeof(SampleJob), jobType);
		}

		[Fact]
		public void GetJobType_UnknownJobType_ThrowsArgumentException()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() => resolver.GetJobType("UnknownJob"));
			Assert.Contains("Unknown job type", exception.Message);
			Assert.Contains("UnknownJob", exception.Message);
		}

		[Fact]
		public void GetJobType_NullJobTypeName_ThrowsArgumentException()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() => resolver.GetJobType(null!));
			Assert.Contains("cannot be null or empty", exception.Message);
		}

		[Fact]
		public void GetJobType_EmptyJobTypeName_ThrowsArgumentException()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() => resolver.GetJobType(string.Empty));
			Assert.Contains("cannot be null or empty", exception.Message);
		}

		[Fact]
		public void RegisterJobType_ValidType_RegistersSuccessfully()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act
			resolver.RegisterJobType("TestJob", typeof(SampleJob));
			var jobType = resolver.GetJobType("TestJob");

			// Assert
			Assert.Equal(typeof(SampleJob), jobType);
		}

		[Fact]
		public void RegisterJobType_NonIJobType_ThrowsArgumentException()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() =>
				resolver.RegisterJobType("InvalidJob", typeof(string)));
			Assert.Contains("must implement IJob interface", exception.Message);
		}

		[Fact]
		public void RegisterJobType_NullJobType_ThrowsArgumentNullException()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act & Assert
			Assert.Throws<ArgumentNullException>(() =>
				resolver.RegisterJobType("TestJob", null!));
		}

		[Fact]
		public void RegisterJobType_NullJobTypeName_ThrowsArgumentException()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() =>
				resolver.RegisterJobType(null!, typeof(SampleJob)));
			Assert.Contains("cannot be null or empty", exception.Message);
		}

		[Fact]
		public void GetRegisteredJobTypes_ReturnsAllRegisteredTypes()
		{
			// Arrange
			var resolver = new JobTypeResolver();

			// Act
			var registeredTypes = resolver.GetRegisteredJobTypes();

			// Assert
			Assert.Contains("Sample", registeredTypes);
		}
	}
}
