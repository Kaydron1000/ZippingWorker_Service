using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Model;
using ZippingWorker_Service.Services;

namespace ZippingWorker_Service.Tests.Services
{
    public class RequestPersistenceServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly RequestPersistenceService _service;
        private readonly Mock<ILogger<RequestPersistenceService>> _loggerMock;

        public RequestPersistenceServiceTests()
        {
            // Create a temporary test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), $"RequestPersistenceTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);

            // Setup configuration with test directory
            var config = new ZippingWorker_ServiceConfigurationType
            {
                storerequestsfolder = _testDirectory
            };

            _loggerMock = new Mock<ILogger<RequestPersistenceService>>();
            _service = new RequestPersistenceService(config, _loggerMock.Object);
        }

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public async Task SaveRequestAsync_ShouldCreateXmlFile()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var zipInfo = new ZipInfoType
            {
                zipfilename = "test.zip",
                zipfiledirectory = "C:\\test",
                zipcompressionlevel = ZippingWorker_Service.Model.CompressionLevelEnumType.ultra,
                validatezipping = ValidateEnumType.extract,
                deleteinputfiles = DeleteEnumType.none,
                zipfiles = new[]
                {
                    new FileInfoType
                    {
                        filelocation = "C:\\source\\file.txt",
                        internalziplocation = "file.txt"
                    }
                }
            };

            // Act
            var filePath = await _service.SaveRequestAsync(zipInfo, requestId);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            filePath.Should().EndWith($"{requestId}.xml");
        }

        [Fact]
        public async Task LoadRequestAsync_ShouldLoadSavedRequest()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var zipInfo = new ZipInfoType
            {
                zipfilename = "test.zip",
                zipfiledirectory = "C:\\test",
                zipcompressionlevel = ZippingWorker_Service.Model.CompressionLevelEnumType.normal,
                validatezipping = ValidateEnumType.none,
                deleteinputfiles = DeleteEnumType.delete,
                zipfiles = new[]
                {
                    new FileInfoType
                    {
                        filelocation = "C:\\source\\file.txt",
                        internalziplocation = "file.txt",
                        filehash = "abc123"
                    }
                }
            };

            await _service.SaveRequestAsync(zipInfo, requestId);

            // Act
            var loadedRequest = await _service.LoadRequestAsync(requestId);

            // Assert
            loadedRequest.Should().NotBeNull();
            loadedRequest!.id.Should().Be(requestId);
            loadedRequest.zippingfiles.Should().NotBeNull();
            loadedRequest.zippingfiles.zipfilename.Should().Be("test.zip");
            loadedRequest.zippingfiles.zipfiledirectory.Should().Be("C:\\test");
            loadedRequest.zippingfiles.zipfiles.Should().HaveCount(1);
            loadedRequest.zippingfiles.zipfiles[0].filelocation.Should().Be("C:\\source\\file.txt");
            loadedRequest.zippingfiles.zipfiles[0].filehash.Should().Be("abc123");
        }

        [Fact]
        public async Task LoadRequestAsync_ShouldReturnNull_WhenFileDoesNotExist()
        {
            // Arrange
            var requestId = "nonexistent";

            // Act
            var loadedRequest = await _service.LoadRequestAsync(requestId);

            // Assert
            loadedRequest.Should().BeNull();
        }

        [Fact]
        public async Task DeleteRequestAsync_ShouldDeleteFile()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var zipInfo = new ZipInfoType
            {
                zipfilename = "test.zip",
                zipfiledirectory = "C:\\test",
                zipcompressionlevel = ZippingWorker_Service.Model.CompressionLevelEnumType.fast,
                zipfiles = new[]
                {
                    new FileInfoType
                    {
                        filelocation = "C:\\source\\file.txt",
                        internalziplocation = "file.txt"
                    }
                }
            };

            var filePath = await _service.SaveRequestAsync(zipInfo, requestId);

            // Act
            var deleted = await _service.DeleteRequestAsync(requestId);

            // Assert
            deleted.Should().BeTrue();
            File.Exists(filePath).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteRequestAsync_ShouldReturnFalse_WhenFileDoesNotExist()
        {
            // Arrange
            var requestId = "nonexistent";

            // Act
            var deleted = await _service.DeleteRequestAsync(requestId);

            // Assert
            deleted.Should().BeFalse();
        }

        [Fact]
        public void GetRequestFilePath_ShouldSanitizeInvalidCharacters()
        {
            // Arrange
            var requestIdWithInvalidChars = "request<>id:with*/invalid|chars?";

            // Act
            var filePath = _service.GetRequestFilePath(requestIdWithInvalidChars);
            var fileName = Path.GetFileName(filePath);

            // Assert - invalid chars should be split and joined with underscore
            fileName.Should().Contain("_");
            fileName.Should().EndWith(".xml");
            fileName.Should().NotContain("<");
            fileName.Should().NotContain(">");
            fileName.Should().NotContain(":");
            fileName.Should().NotContain("*");
            fileName.Should().NotContain("?");
            fileName.Should().NotContain("|");
            fileName.Should().NotContain("/");
        }

        [Fact]
        public async Task SaveRequestAsync_ShouldThrow_WhenZipInfoIsNull()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.SaveRequestAsync(null!, requestId));
        }

        [Fact]
        public async Task SaveRequestAsync_ShouldThrow_WhenRequestIdIsEmpty()
        {
            // Arrange
            var zipInfo = new ZipInfoType
            {
                zipfilename = "test.zip",
                zipfiles = Array.Empty<FileInfoType>()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.SaveRequestAsync(zipInfo, ""));
        }
    }
}
