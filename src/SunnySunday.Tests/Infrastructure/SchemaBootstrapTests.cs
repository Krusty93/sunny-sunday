using SunnySunday.Server.Infrastructure.Database;

namespace SunnySunday.Tests.Infrastructure;

public class SchemaBootstrapTests
{
    [Fact]
    public async Task ApplyAsync_WhenRunTwice_DoesNotThrow()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"sunny-sunday-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var dbPath = Path.Combine(testDirectory, "sunny.db");

        try
        {
            var schemaBootstrap = new SchemaBootstrap();

            await schemaBootstrap.ApplyAsync(dbPath);
            var secondRunException = await Record.ExceptionAsync(() => schemaBootstrap.ApplyAsync(dbPath));

            Assert.Null(secondRunException);
            Assert.True(File.Exists(dbPath));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}
