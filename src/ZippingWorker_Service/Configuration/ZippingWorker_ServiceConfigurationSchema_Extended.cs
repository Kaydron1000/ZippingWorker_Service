using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace ZippingWorker_Service.Configuration
{
    public partial class ZippingWorker_ServiceConfigurationType
    {
        private string? _ResolvedSevenZipExePath;
        private string? _ResolvedTempDir_SymLink;
        private string? _ResolvedTempDir_ZipStaging;
        [XmlIgnore]
        public string ResolvedTempDir_ZipStaging
        {
            get
            {
                if (_ResolvedTempDir_ZipStaging == null)
                    _ResolvedTempDir_ZipStaging = ResolveTempDir_ZipStaging();
                return _ResolvedTempDir_ZipStaging;
            }
        }
        [XmlIgnore]
        public string ResolvedTempDir_SymLink
        {
            get
            {
                if (_ResolvedTempDir_SymLink == null)
                    _ResolvedTempDir_SymLink = ResolveTempDir_SymLink();
                return _ResolvedTempDir_SymLink;
            }
        }
        [XmlIgnore]
        public string ResolvedSevenZipExePath
        {
            get
            {
                if (_ResolvedSevenZipExePath == null)
                    _ResolvedSevenZipExePath = ResolveSevenZipExePath();
                return _ResolvedSevenZipExePath;
            }
        }
        private void ZippingWorker_ServiceConfigurationType_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.tempdir_symlink)) 
                _ResolvedTempDir_SymLink = null;
            else if (e.PropertyName == nameof(this.sevenzipexepath))
                _ResolvedSevenZipExePath = null;
            else if (e.PropertyName == nameof(this.tempdir_zipstaging))
                _ResolvedTempDir_ZipStaging = null;
        }

        private string ResolveTempDir_SymLink()
        {
            string appdir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string retStrg = Environment.ExpandEnvironmentVariables(this.tempdir_symlink).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
            if (System.IO.Directory.Exists(retStrg))
                return retStrg;
            else // If defined configuration does not work fallback to defaults
            {
                Type type = typeof(ZippingWorker_ServiceConfigurationType);
                System.Reflection.PropertyInfo prop = type.GetProperties().Where(o => o.Name == nameof(ZippingWorker_ServiceConfigurationType.tempdir_symlink)).FirstOrDefault();
                System.ComponentModel.DefaultValueAttribute att = (System.ComponentModel.DefaultValueAttribute)prop.GetCustomAttribute(typeof(System.ComponentModel.DefaultValueAttribute));
                retStrg = Environment.ExpandEnvironmentVariables(this.tempdir_symlink).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
                return retStrg;
            }
        }
        private string ResolveSevenZipExePath()
        {
            string appdir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string retStrg = Environment.ExpandEnvironmentVariables(this.sevenzipexepath).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
            if (System.IO.File.Exists(retStrg))
                return retStrg;
            else // If defined configuration does not work fallback to defaults
            {
                Type type = typeof(ZippingWorker_ServiceConfigurationType);
                System.Reflection.PropertyInfo prop = type.GetProperties().Where(o => o.Name == nameof(ZippingWorker_ServiceConfigurationType.sevenzipexepath)).FirstOrDefault();
                System.ComponentModel.DefaultValueAttribute att = (System.ComponentModel.DefaultValueAttribute)prop.GetCustomAttribute(typeof(System.ComponentModel.DefaultValueAttribute));
                retStrg = Environment.ExpandEnvironmentVariables(att.Value.ToString()).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
                return retStrg;
            }
        }
        private string ResolveTempDir_ZipStaging()
        {
            string appdir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string retStrg = Environment.ExpandEnvironmentVariables(this.tempdir_zipstaging).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
            if (System.IO.Directory.Exists(retStrg))
                return retStrg;
            else // If defined configuration does not work fallback to defaults
            {
                Type type = typeof(ZippingWorker_ServiceConfigurationType);
                System.Reflection.PropertyInfo prop = type.GetProperties().Where(o => o.Name == nameof(ZippingWorker_ServiceConfigurationType.tempdir_zipstaging)).FirstOrDefault();
                System.ComponentModel.DefaultValueAttribute att = (System.ComponentModel.DefaultValueAttribute)prop.GetCustomAttribute(typeof(System.ComponentModel.DefaultValueAttribute));
                retStrg = Environment.ExpandEnvironmentVariables(att.Value.ToString()).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
                return retStrg;
            }
        }
        [OnDeserialized]
        public void PostDeserializer()
        {
            this.tempdir_symlink = this.tempdir_symlink.Trim();
            this.tempdir_zipstaging = this.tempdir_zipstaging.Trim();
            this.sevenzipexepath = this.sevenzipexepath.Trim();
            this.PropertyChanged += ZippingWorker_ServiceConfigurationType_PropertyChanged;
        }
        public void PostLoad(ILogger<ConfigurationData> logger)
        {
            if (this.tempdir_symlink_createIfNotExist)
            {
                if (!Directory.Exists(this.ResolvedTempDir_SymLink))
                {
                    try
                    {
                        Directory.CreateDirectory(this.ResolvedTempDir_SymLink);

                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, $"Failed to create directory for tempdir_symlink at '{this.ResolvedTempDir_SymLink}'");
                    }
                }
            }
            if (this.tempdir_zipstaging_createIfNotExist)
            {
                if (!Directory.Exists(this.ResolvedTempDir_ZipStaging))
                {
                    try
                    {
                        Directory.CreateDirectory(this.ResolvedTempDir_ZipStaging);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, $"Failed to create directory for tempdir_zipstaging at '{this.ResolvedTempDir_ZipStaging}'");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a deep copy of the configuration for capturing a snapshot.
        /// This ensures in-flight requests continue with their original configuration.
        /// </summary>
        public ZippingWorker_ServiceConfigurationType DeepCopy()
        {
            var copy = new ZippingWorker_ServiceConfigurationType
            {
                serviceport = this.serviceport,
                sevenzipexepath = this.sevenzipexepath,
                tempdir_symlink = this.tempdir_symlink,
                tempdir_symlink_createIfNotExist = this.tempdir_symlink_createIfNotExist,
                tempdir_zipstaging = this.tempdir_zipstaging,
                tempdir_zipstaging_createIfNotExist = this.tempdir_zipstaging_createIfNotExist,
                usestaging = this.usestaging,
                archiver = this.archiver,
                compressionlevel = this.compressionlevel
            };

            // Copy metadata if present
            if (this.metadatalogging != null && this.metadatalogging.Length > 0)
            {
                copy.metadatalogging = this.metadatalogging.Select(d => new MetaDataType
                {
                    key = d.key,
                    value = d.value
                }).ToArray();
            }

            return copy;
        }
    }

}
