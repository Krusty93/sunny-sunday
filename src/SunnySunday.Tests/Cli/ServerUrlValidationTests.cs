using SunnySunday.Cli.Infrastructure;

namespace SunnySunday.Tests.Cli;

public sealed class ServerUrlValidationTests
{
    [Fact]
    public void Validate_NullValue_ReturnsMissing()
    {
        var result = ServerUrlValidator.Validate(null, out var uri);

        Assert.Equal(ServerUrlValidator.ValidationResult.Missing, result);
        Assert.Null(uri);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsMissing()
    {
        var result = ServerUrlValidator.Validate(string.Empty, out var uri);

        Assert.Equal(ServerUrlValidator.ValidationResult.Missing, result);
        Assert.Null(uri);
    }

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsMissing()
    {
        var result = ServerUrlValidator.Validate("   ", out var uri);

        Assert.Equal(ServerUrlValidator.ValidationResult.Missing, result);
        Assert.Null(uri);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("//missing-scheme")]
    [InlineData("file:///local/path")]
    public void Validate_MalformedOrNonHttpUrl_ReturnsMalformed(string value)
    {
        var result = ServerUrlValidator.Validate(value, out var uri);

        Assert.Equal(ServerUrlValidator.ValidationResult.Malformed, result);
        Assert.Null(uri);
    }

    [Theory]
    [InlineData("http://192.168.1.10:8080")]
    [InlineData("http://localhost:5000")]
    [InlineData("https://sunny.example.com")]
    [InlineData("http://sunny.example.com/prefix")]
    public void Validate_ValidHttpUrl_ReturnsValidWithUri(string value)
    {
        var result = ServerUrlValidator.Validate(value, out var uri);

        Assert.Equal(ServerUrlValidator.ValidationResult.Valid, result);
        Assert.NotNull(uri);
        Assert.Equal(value, uri.ToString().TrimEnd('/'));
    }
}
