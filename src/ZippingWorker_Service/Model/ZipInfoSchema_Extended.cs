using System.Reflection;
using System.Xml.Serialization;
using ZippingWorker_Service.Configuration;

namespace ZippingWorker_Service.Model
{
    public partial class ZipInfoType
    {
        private string? _ResolvedZipFileDirectory;
        [XmlIgnore]
        public string ResolvedZipFileDirectory
        {
            get
            {
                if (_ResolvedZipFileDirectory == null)
                    _ResolvedZipFileDirectory = ResolveZipFileDirectory();
                return _ResolvedZipFileDirectory;
            }
        }
        private string ResolveZipFileDirectory()
        {
            string appdir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string retStrg = Environment.ExpandEnvironmentVariables(this.zipfiledirectory).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
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
            this.zipfilename = this.zipfilename.Trim();
            this.zipfiledirectory = this.zipfiledirectory.Trim();
        }
        public List<MetaDataType> GetAllChildMetaData()
        {
            List<MetaDataType> retList = new List<MetaDataType>();
            if (this.metadata != null)
                retList.AddRange(this.metadata);
            if (this.zipfiles != null)
            {
                foreach (var file in this.zipfiles)
                {
                    if (file.metadata != null)
                        retList.AddRange(file.metadata);
                }
            }
            if (this.driveletters != null)
            {
                foreach (var drive in this.driveletters)
                {
                    if (drive.metadata != null)
                        retList.AddRange(drive.metadata);
                }
            }
            return retList;
        }
    }
    public partial class FileInfoType
    {
        private string? _ResolvedFileLocation;
        [XmlIgnore]
        public string ResolvedFileLocation
        {
            get
            {
                if (_ResolvedFileLocation == null)
                    _ResolvedFileLocation = ResolveFileLocation();
                return _ResolvedFileLocation;
            }
        }
        private string ResolveFileLocation()
        {
            string appdir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string retStrg = Environment.ExpandEnvironmentVariables(this.filelocation).Replace("%APPDIR%", appdir).Replace("%APPPATH%", appdir);
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
            this.filelocation = this.filelocation.Trim();
            this.internalziplocation = this.internalziplocation.Trim();
        }
    }
}
