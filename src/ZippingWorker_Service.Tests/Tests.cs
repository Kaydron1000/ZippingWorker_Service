using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;
using ZippingWorker_Service;

using ZippingWorker_Service.Zipping;
using ZippingWorker_Service.Controllers;
using ZippingWorker_Service.Services;
using System.Runtime.Serialization.DataContracts;

namespace ZippingWorker_Service.Tests
{
    
    public class Tests
    {
        [Fact]
        public void SampleTest()
        {
            Moq.IMock<IZipRequestQueue> zipQueueMock = new Moq.Mock<IZipRequestQueue>();
            //zipQueueMock.Awaiting(q => q.Object.EnqueueAsync(It.IsAny<ZipRequest>(), It.IsAny<CancellationToken>())).Should().NotThrow();
            Moq.IMock<IDriveLetterResolver> driveResolverMock = new Moq.Mock<IDriveLetterResolver>();
            Moq.IMock<IMetricsService> metricsMock = new Moq.Mock<IMetricsService>();
            Moq.IMock<Microsoft.Extensions.Logging.ILogger<Controllers.ZipInfoController>> loggerMock = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<Controllers.ZipInfoController>>();
            var config = new Configuration.ZippingWorker_ServiceConfigurationType();
            Controllers.ZipInfoController zipInfoController = new Controllers.ZipInfoController(zipQueueMock.Object, driveResolverMock.Object, metricsMock.Object, loggerMock.Object, config);
            string xmldata = 
                @"<ZipInfoType>
                    <CompressionLevel>Fastest</CompressionLevel>
                    <SourcePath>C:\\Source</SourcePath>
                    <DestinationPath>C:\\Destination.zip</DestinationPath>
                  </ZipInfoType>";
            zipInfoController.Request.Body = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(xmldata));
            zipInfoController.SubmitZipRequestBinary();
            // This is a placeholder test to ensure the test project is set up correctly.
            // Replace with actual tests for your services and components.
            // Arrange
            int expected = 5;
            // Act
            int actual = 2 + 3;
            // Assert
            actual.Should().Be(expected);
        }
    }
}
