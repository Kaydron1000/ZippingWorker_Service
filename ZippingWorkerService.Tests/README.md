# ZippingWorkerService Unit Tests

## Test Project Setup

**Framework**: xUnit (most modern and widely adopted for .NET)  
**Target**: .NET 10  
**Test Libraries**:
- `xunit` - Testing framework
- `xunit.runner.visualstudio` - Visual Studio test adapter
- `Moq 4.20.72` - Mocking framework for dependencies
- `FluentAssertions 8.9.0` - Expressive assertions

## Test Coverage

### MetricsServiceTests (8 tests)
Tests for Prometheus metrics recording:
- ✅ `RecordZipRequestQueued_ShouldIncrementCounter`
- ✅ `RecordZipRequestStarted_ShouldIncrementCounter`
- ✅ `RecordZipRequestCompleted_ShouldAcceptSuccessAndFailureStatus` (Theory: true/false)
- ✅ `RecordZipValidation_ShouldAcceptPassedAndFailedResults` (Theory: true/false)
- ✅ `SetQueueDepth_ShouldAcceptValidDepth`
- ✅ `RecordFileDeletion_ShouldAcceptSuccessAndFailureCounts` (Theory: multiple scenarios)
- ✅ `RecordCopyVerification_ShouldAcceptSuccessAndFailureResults` (Theory: true/false)

### DriveLetterResolverTests (8 tests)
Tests for path resolution and drive letter mapping:
- ✅ `ResolvePath_WithNoMappings_ShouldReturnOriginalPath`
- ✅ `ResolvePath_WithEmptyMappings_ShouldReturnOriginalPath`
- ✅ `ResolvePath_WithMatchingMapping_ShouldReplaceRoot`
- ✅ `ResolvePath_WithMultipleMappings_ShouldUseCorrectMapping`
- ✅ `ResolvePath_WithCaseInsensitiveDriveLetter_ShouldMatch`
- ✅ `ResolvePath_WithNoMatchingMapping_ShouldReturnOriginalPath`
- ✅ `ResolvePath_WithRelativePath_ShouldReturnOriginalPath`
- ✅ `ResolvePath_WithUNCPath_ShouldReturnOriginalPath`

### ZipRequestQueueTests (5 tests)
Tests for queue operations and FIFO behavior:
- ✅ `EnqueueAsync_ShouldAddRequestToQueue`
- ✅ `DequeueAsync_ShouldReturnEnqueuedRequest`
- ✅ `DequeueAsync_WithMultipleItems_ShouldReturnInFIFOOrder`
- ✅ `ZipRequest_DefaultValues_ShouldBeSet`
- ✅ `ZipFileEntry_DefaultValues_ShouldBeSet`

### ArchiverFactoryTests (2 tests)
Tests for archiver creation based on configuration:
- ✅ `CreateArchiver_WithSevenZipConfiguration_ShouldReturnSevenZipAdapter`
- ✅ `CreateArchiver_WithDotNetZipConfiguration_ShouldReturnDotNetZipAdapter`

## Running Tests

### Command Line
```bash
# Run all tests
dotnet test ZippingWorkerService.Tests\ZippingWorkerService.Tests.csproj

# Run with detailed output
dotnet test ZippingWorkerService.Tests\ZippingWorkerService.Tests.csproj --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~DriveLetterResolverTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~ResolvePath_WithMatchingMapping_ShouldReplaceRoot"
```

### Visual Studio
- **Test Explorer**: View → Test Explorer (Ctrl+E, T)
- **Run All Tests**: Click "Run All" or Ctrl+R, A
- **Run Specific Test**: Right-click test → Run
- **Debug Test**: Right-click test → Debug

## Test Patterns Used

### Arrange-Act-Assert (AAA)
All tests follow the AAA pattern:
```csharp
[Fact]
public void TestMethod()
{
    // Arrange - Set up test data and dependencies
    var service = new Service();

    // Act - Execute the method under test
    var result = service.DoSomething();

    // Assert - Verify the outcome
    result.Should().Be(expectedValue);
}
```

### Theory Tests
For testing multiple scenarios:
```csharp
[Theory]
[InlineData(true)]
[InlineData(false)]
public void TestWithMultipleInputs(bool input)
{
    // Test runs twice with different inputs
}
```

### Mocking Dependencies
Using Moq to mock ILogger dependencies:
```csharp
var mockLogger = new Mock<ILogger<DriveLetterResolver>>();
var resolver = new DriveLetterResolver(mockLogger.Object);
```

### FluentAssertions
Expressive assertion syntax:
```csharp
result.Should().Be(expected);
result.Should().NotBeNull();
result.Should().HaveCount(3);
collection.Should().BeEquivalentTo(expectedCollection);
```

## Next Steps for Expanding Tests

### High Priority
1. **Worker.cs Tests** - Core background service logic
   - Test `ProcessZipRequestAsync` workflow
   - Test `DeleteInputFilesAsync` with various scenarios
   - Test `GetNextAvailableFilename` collision handling
   - Test cancellation token handling

2. **ZipInfoController Tests** - API endpoint testing
   - Test XML/binary deserialization
   - Test drive letter mapping processing
   - Test error handling
   - Test metrics recording

3. **ZipValidationService Tests** - Hash verification
   - Test hash calculation
   - Test zip validation
   - Test parallel hash operations

### Medium Priority
4. **ArchiverFactory Tests** - Expand coverage
   - Test invalid archiver types
   - Test configuration edge cases

5. **Integration Tests**
   - End-to-end workflow tests
   - Test actual 7-Zip integration
   - Test file I/O operations

### Low Priority
6. **Configuration Tests**
   - Test XML deserialization
   - Test resolved path properties
   - Test environment variable expansion

## Test Execution Results

**Latest Run**: ✅ All 27 tests passed  
**Duration**: ~2.9 seconds  
**Build**: Succeeded with warnings (mostly from generated code)

## Continuous Integration

To integrate with CI/CD pipelines:

```yaml
# Azure DevOps example
- task: DotNetCoreCLI@2
  displayName: 'Run Unit Tests'
  inputs:
    command: test
    projects: '**/*Tests.csproj'
    arguments: '--configuration Release --collect:"XPlat Code Coverage"'
```

```yaml
# GitHub Actions example
- name: Run tests
  run: dotnet test --configuration Release --verbosity normal
```

## Coverage Goals

- **Current**: Basic service layer coverage (MetricsService, DriveLetterResolver, ZipRequestQueue, ArchiverFactory)
- **Target**: 70%+ code coverage across critical paths
- **Focus Areas**: Worker.cs, Controllers, Validation logic

## Notes

- Tests use mocked ILogger to avoid dependency on logging infrastructure
- FluentAssertions provides clear, readable test failures
- Moq enables testing without concrete dependencies
- Theory tests reduce code duplication for similar scenarios
- All tests are fast (< 3 seconds total) - no external dependencies or I/O
