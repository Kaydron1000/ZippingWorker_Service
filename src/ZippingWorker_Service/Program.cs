using ZippingWorker_Service;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Parse command-line arguments for log level
LogLevel logLevel = LogLevel.Information; // Default
if (args.Length > 0)
{
    foreach (var arg in args)
    {
        if (arg.StartsWith("--loglevel=", StringComparison.OrdinalIgnoreCase))
        {
            var levelString = arg.Substring("--loglevel=".Length);
            if (Enum.TryParse<LogLevel>(levelString, true, out var parsedLevel))
            {
                logLevel = parsedLevel;
                Console.WriteLine($"Log level set to: {logLevel}");
            }
            else
            {
                Console.WriteLine($"Warning: Invalid log level '{levelString}'. Using default: Information");
                Console.WriteLine($"Valid levels: Trace, Debug, Information, Warning, Error, Critical, None");
            }
        }
    }
}

// Configure logging with the specified level
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(logLevel);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Load configuration
// Create a temporary logger for configuration loading
using var loggerFactory = LoggerFactory.Create(logBuilder =>
{
    logBuilder.AddConsole();
    logBuilder.SetMinimumLevel(logLevel);
});
var configLogger = loggerFactory.CreateLogger<ConfigurationData>();

var configPath = Path.Combine(AppContext.BaseDirectory, "config.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"Warning: Configuration file not found at {configPath}. Using default settings.");
}

ConfigurationData configData = File.Exists(configPath) 
    ? new ConfigurationData(configPath, configLogger) 
    : new ConfigurationData(configLogger);

ZippingWorker_ServiceConfigurationType config = configData.ApplicationConfiguration;

// Configure service to listen on the configured port
builder.WebHost.UseUrls($"http://*:{config.serviceport}");

// Register services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IZipRequestQueue, ZipRequestQueue>();
builder.Services.AddSingleton<IArchiverFactory, ArchiverFactory>();
builder.Services.AddSingleton<IZipValidationService, ZipValidationService>();
builder.Services.AddSingleton<IDriveLetterResolver, DriveLetterResolver>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Ensure MetricsService is instantiated to register Prometheus metrics
_ = app.Services.GetRequiredService<IMetricsService>();

// Log the port the service is listening on
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ZippingWorker_Service starting on port {Port} with log level {LogLevel}", config.serviceport, logLevel);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

// Prometheus metrics endpoint
app.MapMetrics();

app.Run();
