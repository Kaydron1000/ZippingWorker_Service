using FluentAssertions;
using Xunit;
using ZippingWorker_Service.Services;
using ZippingWorker_Service.Zipping;

namespace ZippingWorker_Service.Tests.Services;

public class ZipRequestQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldAddRequestToQueue()
    {
        // Arrange
        var queue = new ZipRequestQueue();
        var request = new ZipRequest
        {
            OutputArchivePath = @"C:\output\test.zip",
            Files = new List<ZipFileEntry>
            {
                new ZipFileEntry { SourcePath = @"C:\input\file1.txt", ArchivePath = "file1.txt" }
            },
            CompressionLevel = ArchiveCompressionLevel.normal,
            DeleteInputFiles = false
        };

        // Act
        await queue.EnqueueAsync(request);

        // Assert
        // Queue should accept the request without throwing
    }

    [Fact]
    public async Task DequeueAsync_ShouldReturnEnqueuedRequest()
    {
        // Arrange
        var queue = new ZipRequestQueue();
        var request = new ZipRequest
        {
            OutputArchivePath = @"C:\output\test.zip",
            Files = new List<ZipFileEntry>
            {
                new ZipFileEntry { SourcePath = @"C:\input\file1.txt", ArchivePath = "file1.txt" }
            },
            CompressionLevel = ArchiveCompressionLevel.normal,
            DeleteInputFiles = false
        };

        // Act
        await queue.EnqueueAsync(request);
        var result = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OutputArchivePath.Should().Be(request.OutputArchivePath);
        result.Files.Should().HaveCount(1);
        result.Files[0].SourcePath.Should().Be(@"C:\input\file1.txt");
    }

    [Fact]
    public async Task DequeueAsync_WithMultipleItems_ShouldReturnInFIFOOrder()
    {
        // Arrange
        var queue = new ZipRequestQueue();
        var request1 = new ZipRequest { OutputArchivePath = @"C:\output\first.zip", Files = new List<ZipFileEntry>(), CompressionLevel = ArchiveCompressionLevel.normal };
        var request2 = new ZipRequest { OutputArchivePath = @"C:\output\second.zip", Files = new List<ZipFileEntry>(), CompressionLevel = ArchiveCompressionLevel.normal };
        var request3 = new ZipRequest { OutputArchivePath = @"C:\output\third.zip", Files = new List<ZipFileEntry>(), CompressionLevel = ArchiveCompressionLevel.normal };

        // Act
        await queue.EnqueueAsync(request1);
        await queue.EnqueueAsync(request2);
        await queue.EnqueueAsync(request3);

        var result1 = await queue.DequeueAsync(CancellationToken.None);
        var result2 = await queue.DequeueAsync(CancellationToken.None);
        var result3 = await queue.DequeueAsync(CancellationToken.None);

        // Assert
        result1.OutputArchivePath.Should().Be(@"C:\output\first.zip");
        result2.OutputArchivePath.Should().Be(@"C:\output\second.zip");
        result3.OutputArchivePath.Should().Be(@"C:\output\third.zip");
    }

    [Fact]
    public async Task ZipRequest_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var request = new ZipRequest();

        // Assert
        request.Files.Should().NotBeNull();
        request.Files.Should().BeEmpty();
        request.OutputArchivePath.Should().Be(string.Empty);
        request.CompressionLevel.Should().Be(ArchiveCompressionLevel.ultra);
        request.ValidateZipping.Should().BeTrue();
        request.DeleteInputFiles.Should().BeFalse();
    }

    [Fact]
    public void ZipFileEntry_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var entry = new ZipFileEntry();

        // Assert
        entry.SourcePath.Should().Be(string.Empty);
        entry.ArchivePath.Should().Be(string.Empty);
        entry.Hash.Should().BeNull();
    }
}
