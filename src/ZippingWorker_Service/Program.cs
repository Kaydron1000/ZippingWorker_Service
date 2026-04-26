using ZippingWorker_Service;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Services;
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

ZippingWorker_ServiceConfigurationType config = configData.ApplicationConfiguration ?? new ZippingWorker_ServiceConfigurationType();

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
logger.LogInformation("ZippingWorker_Service starting on port {Port}", config.serviceport);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

// Prometheus metrics endpoint
app.MapMetrics();

app.Run();
