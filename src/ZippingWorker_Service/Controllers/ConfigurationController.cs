using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using ZippingWorker_Service.Configuration;

namespace ZippingWorker_Service.Controllers;

/// <summary>
/// Remote configuration management endpoint
/// Endpoint path matches XSD element name: /api/ZippingWorker_Serviceconfiguration
/// Query parameters match XSD attributes with same required/optional rules
/// </summary>
[ApiController]
[Route("api/ZippingWorker_Serviceconfiguration")]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> _logger;
    private readonly string _configPath;
    private readonly object _configLock = new();
    private readonly ZippingWorker_ServiceConfigurationType _config;

    public ConfigurationController(
        ILogger<ConfigurationController> logger,
        ZippingWorker_ServiceConfigurationType config)
    {
        _logger = logger;
        _config = config;
        _configPath = Path.Combine(AppContext.BaseDirectory, "config.xml");
    }

    /// <summary>
    /// GET current configuration
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GetConfiguration()
    {
        try
        {
            lock (_configLock)
            {
                if (!System.IO.File.Exists(_configPath))
                {
                    _logger.LogWarning("Configuration file not found at {ConfigPath}, returning defaults", _configPath);
                    var defaultConfig = new ZippingWorker_ServiceConfigurationType();
                    return Ok(MapToResponse(defaultConfig));
                }

                var configData = new ConfigurationData(_configPath);

                if (configData.XmlSchemaError)
                {
                    _logger.LogError("Configuration has schema errors");
                    return StatusCode(500, new { Error = "Configuration file has schema validation errors", Errors = configData.ErrorList.Select(e => e.Message) });
                }

                return Ok(MapToResponse(configData.ApplicationConfiguration));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration");
            return StatusCode(500, new { Error = "Failed to read configuration", Message = ex.Message });
        }
    }

    /// <summary>
    /// PUT/PATCH configuration - updates only provided attributes
    /// Query parameters match XSD attributes (all optional, only updates what's provided)
    /// </summary>
    /// <param name="serviceport">Service listening port (integer)</param>
    /// <param name="sevenzipexepath">Path to 7z.exe (string)</param>
    /// <param name="tempdir_symlink">Temporary directory for symlinks (string)</param>
    /// <param name="tempdir_symlink_createIfNotExist">Create symlink temp directory if it doesn't exist (boolean)</param>
    /// <param name="tempdir_zipstaging">Staging directory for zip creation (string)</param>
    /// <param name="tempdir_zipstaging_createIfNotExist">Create zip staging directory if it doesn't exist (boolean)</param>
    /// <param name="usestaging">Use staging directory for zip creation (boolean)</param>
    /// <param name="archiver">Archiver type: sevenzip or dotnetzip (string)</param>
    /// <param name="compressionlevel">Compression level: nocompression, fastest, fast, normal, maximum, ultra (string)</param>
    [HttpPut]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult UpdateConfiguration(
        [FromQuery] int? serviceport,
        [FromQuery] string? sevenzipexepath,
        [FromQuery] string? tempdir_symlink,
        [FromQuery] bool? tempdir_symlink_createIfNotExist,
        [FromQuery] string? tempdir_zipstaging,
        [FromQuery] bool? tempdir_zipstaging_createIfNotExist,
        [FromQuery] bool? usestaging,
        [FromQuery] string? archiver,
        [FromQuery] string? compressionlevel)
    {
        try
        {
            lock (_configLock)
            {
                // Load existing config or create default
                ZippingWorker_ServiceConfigurationType config;
                XDocument xmlDoc;

                if (System.IO.File.Exists(_configPath))
                {
                    var configData = new ConfigurationData(_configPath);
                    config = configData.ApplicationConfiguration ?? new ZippingWorker_ServiceConfigurationType();
                    xmlDoc = configData.XmlDoc;
                }
                else
                {
                    config = new ZippingWorker_ServiceConfigurationType();
                    xmlDoc = CreateDefaultXmlDocument();
                }

                // Track what was updated
                var updates = new List<string>();

                // Update only provided attributes (partial update)
                if (serviceport.HasValue)
                {
                    config.serviceport = serviceport.Value;
                    updates.Add($"serviceport={serviceport.Value}");
                }

                if (!string.IsNullOrWhiteSpace(sevenzipexepath))
                {
                    config.sevenzipexepath = sevenzipexepath;
                    updates.Add($"sevenzipexepath={sevenzipexepath}");
                }

                if (!string.IsNullOrWhiteSpace(tempdir_symlink))
                {
                    config.tempdir_symlink = tempdir_symlink;
                    updates.Add($"tempdir_symlink={tempdir_symlink}");
                }

                if (tempdir_symlink_createIfNotExist.HasValue)
                {
                    config.tempdir_symlink_createIfNotExist = tempdir_symlink_createIfNotExist.Value;
                    updates.Add($"tempdir_symlink_createIfNotExist={tempdir_symlink_createIfNotExist.Value}");
                }

                if (!string.IsNullOrWhiteSpace(tempdir_zipstaging))
                {
                    config.tempdir_zipstaging = tempdir_zipstaging;
                    updates.Add($"tempdir_zipstaging={tempdir_zipstaging}");
                }

                if (tempdir_zipstaging_createIfNotExist.HasValue)
                {
                    config.tempdir_zipstaging_createIfNotExist = tempdir_zipstaging_createIfNotExist.Value;
                    updates.Add($"tempdir_zipstaging_createIfNotExist={tempdir_zipstaging_createIfNotExist.Value}");
                }

                if (usestaging.HasValue)
                {
                    config.usestaging = usestaging.Value;
                    updates.Add($"usestaging={usestaging.Value}");
                }

                if (!string.IsNullOrWhiteSpace(archiver))
                {
                    if (Enum.TryParse<ArchiverEnumType>(archiver, true, out var archiverEnum))
                    {
                        config.archiver = archiverEnum;
                        updates.Add($"archiver={archiverEnum}");
                    }
                    else
                    {
                        return BadRequest(new { Error = $"Invalid archiver value: {archiver}. Valid values: sevenzip, dotnetzip" });
                    }
                }

                if (!string.IsNullOrWhiteSpace(compressionlevel))
                {
                    if (Enum.TryParse<Configuration.CompressionLevelEnumType>(compressionlevel, true, out var compressionEnum))
                    {
                        config.compressionlevel = compressionEnum;
                        updates.Add($"compressionlevel={compressionEnum}");
                    }
                    else
                    {
                        return BadRequest(new { Error = $"Invalid compressionlevel value: {compressionlevel}. Valid values: nocompression, fastest, fast, normal, maximum, ultra" });
                    }
                }

                if (updates.Count == 0)
                {
                    return BadRequest(new { Error = "No configuration attributes provided. Specify at least one query parameter to update." });
                }

                // Update XML document with new values
                UpdateXmlDocument(xmlDoc, config);

                // Save to file
                xmlDoc.Save(_configPath);

                // Apply changes to the running service's configuration singleton
                // New requests will immediately use these values
                ApplyConfigurationChanges(config);

                _logger.LogInformation("Configuration updated and applied: {Updates}", string.Join(", ", updates));

                return Ok(new
                {
                    Message = "Configuration updated successfully. Changes applied to new requests immediately. Note: serviceport changes require service restart.",
                    UpdatedAttributes = updates,
                    CurrentConfiguration = MapToResponse(config)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration");
            return StatusCode(500, new { Error = "Failed to update configuration", Message = ex.Message });
        }
    }

    private ConfigurationResponse MapToResponse(ZippingWorker_ServiceConfigurationType config)
    {
        return new ConfigurationResponse
        {
            ServicePort = config.serviceport,
            SevenZipExePath = config.sevenzipexepath ?? string.Empty,
            TempDir_SymLink = config.tempdir_symlink ?? string.Empty,
            TempDir_SymLink_CreateIfNotExist = config.tempdir_symlink_createIfNotExist,
            TempDir_ZipStaging = config.tempdir_zipstaging ?? string.Empty,
            TempDir_ZipStaging_CreateIfNotExist = config.tempdir_zipstaging_createIfNotExist,
            UseStaging = config.usestaging,
            Archiver = config.archiver.ToString(),
            CompressionLevel = config.compressionlevel.ToString(),
            ResolvedSevenZipExePath = config.ResolvedSevenZipExePath ?? string.Empty,
            ResolvedTempDir_SymLink = config.ResolvedTempDir_SymLink ?? string.Empty,
            ResolvedTempDir_ZipStaging = config.ResolvedTempDir_ZipStaging ?? string.Empty
        };
    }

    private XDocument CreateDefaultXmlDocument()
    {
        XNamespace ns = "http://tempuri.org/ZippingWorker_ServiceConfigurationSchema.xsd";
        var root = new XElement(ns + "ZippingWorker_Serviceconfiguration");
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private void UpdateXmlDocument(XDocument xmlDoc, ZippingWorker_ServiceConfigurationType config)
    {
        var root = xmlDoc.Root;
        if (root == null) return;

        // Update attributes
        SetAttribute(root, "serviceport", config.serviceport.ToString());
        SetAttribute(root, "sevenzipexepath", config.sevenzipexepath);
        SetAttribute(root, "tempdir_symlink", config.tempdir_symlink);
        SetAttribute(root, "tempdir_symlink_createIfNotExist", config.tempdir_symlink_createIfNotExist.ToString().ToLowerInvariant());
        SetAttribute(root, "tempdir_zipstaging", config.tempdir_zipstaging);
        SetAttribute(root, "tempdir_zipstaging_createIfNotExist", config.tempdir_zipstaging_createIfNotExist.ToString().ToLowerInvariant());
        SetAttribute(root, "usestaging", config.usestaging.ToString().ToLowerInvariant());
        SetAttribute(root, "archiver", config.archiver.ToString());
        SetAttribute(root, "compressionlevel", config.compressionlevel.ToString());
    }

    private void SetAttribute(XElement element, string name, string value)
    {
        var attr = element.Attribute(name);
        if (attr != null)
        {
            attr.Value = value;
        }
        else
        {
            element.Add(new XAttribute(name, value));
        }
    }

    /// <summary>
    /// Applies configuration changes to the running service's singleton instance.
    /// This allows new requests to immediately use updated configuration without restart.
    /// </summary>
    private void ApplyConfigurationChanges(ZippingWorker_ServiceConfigurationType source)
    {
        _config.serviceport = source.serviceport;
        _config.sevenzipexepath = source.sevenzipexepath;
        _config.tempdir_symlink = source.tempdir_symlink;
        _config.tempdir_symlink_createIfNotExist = source.tempdir_symlink_createIfNotExist;
        _config.tempdir_zipstaging = source.tempdir_zipstaging;
        _config.tempdir_zipstaging_createIfNotExist = source.tempdir_zipstaging_createIfNotExist;
        _config.usestaging = source.usestaging;
        _config.archiver = source.archiver;
        _config.compressionlevel = source.compressionlevel;

        // Copy metadata if present
        if (source.metadatalogging != null)
        {
            _config.metadatalogging = source.metadatalogging;
        }
    }
}

public class ConfigurationResponse
{
    public int ServicePort { get; set; }
    public string SevenZipExePath { get; set; } = string.Empty;
    public string TempDir_SymLink { get; set; } = string.Empty;
    public bool TempDir_SymLink_CreateIfNotExist { get; set; }
    public string TempDir_ZipStaging { get; set; } = string.Empty;
    public bool TempDir_ZipStaging_CreateIfNotExist { get; set; }
    public bool UseStaging { get; set; }
    public string Archiver { get; set; } = string.Empty;
    public string CompressionLevel { get; set; } = string.Empty;

    // Resolved paths (after environment variable expansion)
    public string ResolvedSevenZipExePath { get; set; } = string.Empty;
    public string ResolvedTempDir_SymLink { get; set; } = string.Empty;
    public string ResolvedTempDir_ZipStaging { get; set; } = string.Empty;
}
