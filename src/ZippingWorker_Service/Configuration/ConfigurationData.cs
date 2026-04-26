using System;
using System.Collections.Generic;
using System.Text;

namespace ZippingWorker_Service.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Schema;
    using System.Reflection;
    public class ConfigurationData
    {
        private const string ASSEMBLYNAME = "ZippingWorker_Service";
        private const string EMBEDEDXSDNAME = "ZippingWorker_ServiceConfigurationSchema.xsd";
        private List<ValidationEventArgs> _ErrorList;
        private List<ValidationEventArgs> _WarningList;
        private ZippingWorker_ServiceConfigurationType _ApplicationConfiguration;
        private XDocument _XmlDoc;
        private bool _XmlSchemaError = false;
        private bool _XmlSchemaWarning = false;
        public bool XmlSchemaError => _XmlSchemaError;
        public bool XmlSchemaWarning => _XmlSchemaWarning;
        public ZippingWorker_ServiceConfigurationType ApplicationConfiguration => _ApplicationConfiguration;
        public List<ValidationEventArgs> ErrorList => _ErrorList;
        public List<ValidationEventArgs> WarningList => _WarningList;
        public XDocument XmlDoc => _XmlDoc;
        public ConfigurationData()
        {
            _ErrorList = new List<ValidationEventArgs>();
            _WarningList = new List<ValidationEventArgs>();
        }
        public ConfigurationData(string xmlPath) : this()
        {
            ImportXml(xmlPath);
        }
        /// <summary>
        /// Imports the xml configuration. Only pulls in the data associated with the schema namespace dealing with this project.
        /// </summary>
        /// <param name="xmlPath">FilePath to the xml configuration.</param>
        /// <returns>True when xml contained content to import.</returns>
        public bool ImportXml(string xmlPath)
        {
            _XmlDoc = System.Xml.Linq.XDocument.Load(xmlPath);
            var lowerDoc = new XDocument(_XmlDoc.Root.ToLowerCaseNamesAndRemoveCommentsAndTrueFalseValues());

            XmlSchemaSet schemas = LoadSchemaSet();
            XmlSchema firstSchema = schemas.Schemas().Cast<XmlSchema>().FirstOrDefault();
            XNamespace ns = firstSchema.TargetNamespace;
            string rootElement = firstSchema.Elements.Names.OfType<XmlQualifiedName>().Last().Name;
            //string rootElement = schemaSet.GlobalElements.Names.OfType<XmlQualifiedName>().First().Name;
            XName FullRoot = XName.Get(rootElement, ns.NamespaceName);

            XElement baseAppEle = null;
            // Check if full Root Exists
            baseAppEle = lowerDoc.Root.DescendantsAndSelf(FullRoot).SingleOrDefault();
            if (baseAppEle == null)
            {
                // Check if unQualified Root Exists
                baseAppEle = lowerDoc.Root.DescendantsAndSelf(rootElement).SingleOrDefault();

                // if unqualified exists add qualified name
                if (baseAppEle != null)
                {
                    XName qualName = ns + baseAppEle.Name.LocalName;
                    baseAppEle.Name = qualName;
                }
            }
            XDocument newDoc = new XDocument(baseAppEle);

            try
            {
                newDoc.Validate(schemas, ValidationHandler);
            }
            catch (Exception e)
            {
                _ApplicationConfiguration = default(ZippingWorker_ServiceConfigurationType);
                throw e;
            }
            try

            {
                XmlOnDeserializedSerializer serializer = new XmlOnDeserializedSerializer(typeof(ZippingWorker_ServiceConfigurationType));
                _ApplicationConfiguration = (ZippingWorker_ServiceConfigurationType)serializer.Deserialize(newDoc.CreateReader());

            }

            catch (Exception e)

            {
                throw e;

            }

            return !XmlSchemaError;

        }
        /// <summary>
        /// Loads the schema provided by the EMBEDEDXSDNAME in assembly ASSEMBLYNAME.
        /// </summary>
        /// <returns>Loaded and compiled schema set of the EMBEDEDXSDNAME.</returns>
        private XmlSchemaSet LoadSchemaSet()
        {
            Assembly ConfigurationAssembly;
            string[] resourceNames;

            if (!String.IsNullOrEmpty(ASSEMBLYNAME))
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                //_Logger.Verbose ("LOADED ASSEMBLIES: \n" + String.Join("\n", assemblies.Select(o => o.GetName () .Name)));
                ConfigurationAssembly = assemblies.SingleOrDefault(o => o.GetName().Name == ASSEMBLYNAME);
                resourceNames = ConfigurationAssembly?.GetManifestResourceNames();
            }
            else
            {
                ConfigurationAssembly = Assembly.GetExecutingAssembly();

                //Getting internal resource of the Schema File.
                resourceNames = ConfigurationAssembly?.GetManifestResourceNames();
            }

            string schemaPath = resourceNames.SingleOrDefault(o => o.EndsWith(EMBEDEDXSDNAME));

            //Setting up the Schema for the incoming XML
            XmlSchemaSet schemaSet = new XmlSchemaSet();

            XmlReader xmlreader = XmlReader.Create(ConfigurationAssembly.GetManifestResourceStream(schemaPath));
            XmlSchema aschema = schemaSet.Add(null, xmlreader);

            // Importing all included schemas
            foreach (XmlSchemaInclude inc in aschema.Includes)
            {
                schemaPath = resourceNames.Single(o => o.EndsWith(Path.GetFileName(inc.SchemaLocation)));
                xmlreader = XmlReader.Create(ConfigurationAssembly.GetManifestResourceStream(schemaPath));
                aschema = schemaSet.Add(null, xmlreader);
            }
            schemaSet.Compile();
            return schemaSet;
        }

        /// <summary>
        /// Validation handler used for xml validation.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Validation event arguments.</param>
        private void ValidationHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    _XmlSchemaError = true;
                    _ErrorList.Add(e);
                    break;
                case XmlSeverityType.Warning:
                    _XmlSchemaWarning = true;
                    _WarningList.Add(e);
                    break;
                default:
                    _XmlSchemaError = true;
                    break;
            }
        }

    }
}
