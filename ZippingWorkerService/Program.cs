using ZippingWorkerService;
using ZippingWorkerService.Configuration;
using ZippingWorkerService.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Load configuration
var configPath = Path.Combine(AppContext.BaseDirectory, "config.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"Warning: Configuration file not found at {configPath}. Using default settings.");
}

ConfigurationData configData = File.Exists(configPath) 
    ? new ConfigurationData(configPath) 
    : new ConfigurationData();

ZippingWorkerServiceConfigurationType config = configData.ApplicationConfiguration ?? new ZippingWorkerServiceConfigurationType();

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

// Log the port the service is listening on
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ZippingWorkerService starting on port {Port}", config.serviceport);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

// Prometheus metrics endpoint
app.MapMetrics();

app.Run();
