using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using ZippingWorker_Service.Controllers;

namespace ZippingWorker_Service.Tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Ping_ShouldReturnPong()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Ping() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        result.Value.Should().Be("pong");
    }

    [Fact]
    public void Ping_ShouldReturnOkResult()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Ping();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
