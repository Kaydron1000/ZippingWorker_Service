using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;
using ZippingWorkerService.Services;

namespace ZippingWorkerService.Tests.Services;

public class DriveLetterResolverTests
{
    private readonly Mock<ILogger<DriveLetterResolver>> _loggerMock;
    private readonly DriveLetterResolver _resolver;

    public DriveLetterResolverTests()
    {
        _loggerMock = new Mock<ILogger<DriveLetterResolver>>();
        _resolver = new DriveLetterResolver(_loggerMock.Object);
    }

    [Fact]
    public void ResolvePath_WithNoMappings_ShouldReturnOriginalPath()
    {
        // Arrange
        var originalPath = @"C:\TestData\file.txt";

        // Act
        var result = _resolver.ResolvePath(originalPath, null);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void ResolvePath_WithEmptyMappings_ShouldReturnOriginalPath()
    {
        // Arrange
        var originalPath = @"C:\TestData\file.txt";
        var mappings = new Dictionary<string, string>();

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void ResolvePath_WithMatchingMapping_ShouldReplaceRoot()
    {
        // Arrange
        var originalPath = @"C:\TestData\file.txt";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C:", @"E:\MappedData" }
        };

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(@"E:\MappedData\TestData\file.txt");
    }

    [Fact]
    public void ResolvePath_WithMultipleMappings_ShouldUseCorrectMapping()
    {
        // Arrange
        var originalPath = @"D:\OtherData\subfolder\file.txt";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C:", @"E:\MappedC" },
            { "D:", @"E:\MappedD" },
            { "E:", @"C:\MappedE" }
        };

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(@"E:\MappedD\OtherData\subfolder\file.txt");
    }

    [Fact]
    public void ResolvePath_WithCaseInsensitiveDriveLetter_ShouldMatch()
    {
        // Arrange
        var originalPath = @"c:\TestData\file.txt";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C:", @"E:\MappedData" }
        };

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(@"E:\MappedData\TestData\file.txt");
    }

    [Fact]
    public void ResolvePath_WithNoMatchingMapping_ShouldReturnOriginalPath()
    {
        // Arrange
        var originalPath = @"X:\TestData\file.txt";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C:", @"E:\MappedC" },
            { "D:", @"E:\MappedD" }
        };

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void ResolvePath_WithRelativePath_ShouldReturnOriginalPath()
    {
        // Arrange
        var originalPath = @"relative\path\file.txt";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C:", @"E:\MappedData" }
        };

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(originalPath);
    }

    [Fact]
    public void ResolvePath_WithUNCPath_ShouldReturnOriginalPath()
    {
        // Arrange
        var originalPath = @"\\server\share\file.txt";
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C:", @"E:\MappedData" }
        };

        // Act
        var result = _resolver.ResolvePath(originalPath, mappings);

        // Assert
        result.Should().Be(originalPath);
    }
}
