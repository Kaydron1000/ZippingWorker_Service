using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;
using ZippingWorkerService.Configuration;
using ZippingWorkerService.Services;
using ZippingWorkerService.Zipping;

namespace ZippingWorkerService.Tests.Services;

public class ArchiverFactoryTests
{
    private readonly Mock<ILogger<ArchiverFactory>> _loggerMock;

    public ArchiverFactoryTests()
    {
        _loggerMock = new Mock<ILogger<ArchiverFactory>>();
    }

    [Fact]
    public void CreateArchiver_WithSevenZipConfiguration_ShouldReturnSevenZipAdapter()
    {
        // Arrange
        var config = new ZippingWorkerServiceConfigurationType
        {
            archiver = ArchiverEnumType.sevenzip,
            sevenzipexepath = @"C:\Program Files\7-Zip\7z.exe",
            tempdir_symlink = @"C:\temp\symlink",
            tempdir_zipstaging = @"C:\temp\staging"
        };
        var factory = new ArchiverFactory(config, _loggerMock.Object);

        // Act
        var archiver = factory.CreateArchiver();

        // Assert
        archiver.Should().NotBeNull();
        // Note: SevenZipArchiverAdapter is internal, so we can only verify it's not null and implements IArchiver
        archiver.Should().BeAssignableTo<IArchiver>();
    }

    [Fact]
    public void CreateArchiver_WithDotNetZipConfiguration_ShouldReturnDotNetZipAdapter()
    {
        // Arrange
        var config = new ZippingWorkerServiceConfigurationType
        {
            archiver = ArchiverEnumType.dotnetzip,
            tempdir_zipstaging = @"C:\temp\staging"
        };
        var factory = new ArchiverFactory(config, _loggerMock.Object);

        // Act
        var archiver = factory.CreateArchiver();

        // Assert
        archiver.Should().NotBeNull();
        archiver.Should().BeAssignableTo<IArchiver>();
    }
}
