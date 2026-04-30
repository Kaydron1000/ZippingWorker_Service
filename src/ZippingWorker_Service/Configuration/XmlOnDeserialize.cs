
namespace ZippingWorker_Service.Configuration
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    /// <summary>
    /// A tag for XmlOnDeserializedSerializer to determine if a method should be called after Deserialization has occured.
    /// This occurs after System.Runtime.Serialization.OnDeserialized and DefaultComplexTypes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class OnDeserialized : Attribute
    { }
    /// <summary>
    /// A tag for XmlOnDeserializedSerializer to determine if a class should create default complex types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultComplexTypes : Attribute
    {
        public bool Recursively { get; set; }
        public DefaultComplexTypes(bool recursively = false)
        {
            Recursively = recursively;
        }
    }
    /// <summary>
    /// Creates a deserializer that allows the use of attributes DefaultComplexTypes and OnDeserialized.
    /// </summary>
    public class XmlOnDeserializedSerializer : XmlSerializer
    {
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type)"/>
        public XmlOnDeserializedSerializer(Type type) : base(type)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (XmlTypeMapping)"/>
        public XmlOnDeserializedSerializer(XmlTypeMapping xmlTypeMapping) : base(xmlTypeMapping)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type, string)"/>
        public XmlOnDeserializedSerializer(Type type, string defaultNamespace) : base(type, defaultNamespace)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type, Typell)"/>
        public XmlOnDeserializedSerializer(Type type, Type[] extraTypes) : base(type, extraTypes)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type, XmlAttributeOverrides)"/>
        public XmlOnDeserializedSerializer(Type type, XmlAttributeOverrides overrides) : base(type, overrides)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type, XmlRootAttribute)"/>
        public XmlOnDeserializedSerializer(Type type, XmlRootAttribute root) : base(type, root)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type, XmlAttributeOverrides, Type[], XmlRootAttribute, string)"/>
        public XmlOnDeserializedSerializer(Type type, XmlAttributeOverrides overrides, Type[] extraTypes,
        XmlRootAttribute root, string defaultNamespace) : base(type, overrides, extraTypes, root, defaultNamespace)
        {
        }
        /// <inheritdoc cref="XmlSerializer.XmlSerializer (Type, XmlAttributeOverrides, Type[], XmlRootAttribute, string, string)"/>
        public XmlOnDeserializedSerializer(Type type, XmlAttributeOverrides overrides, Type[] extraTypes,
        XmlRootAttribute root, string defaultNamespace, string location)
        : base(type, overrides, extraTypes, root, defaultNamespace, location)
        {
        }

        /// <inheritdoc cref="XmlSerializer.Deserialize(Stream)"/>
        public new object Deserialize(Stream stream)
        {
            var result = base.Deserialize(stream);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }
        /// <inheritdoc cref="XmlSerializer.Deserialize (TextReader)"/>
        public new object Deserialize(TextReader textReader)
        {
            var result = base.Deserialize(textReader);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }
        /// <inheritdoc cref="XmlSerializer.Deserialize (XmlReader)"/>
        public new object Deserialize(XmlReader xmlReader)
        {
            var result = base.Deserialize(xmlReader);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }
        /// <inheritdoc cref="XmlSerializer.Deserialize (XmlSerializationReader)"/>
        public new object Deserialize(XmlSerializationReader reader)
        {
            var result = base.Deserialize(reader);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }
        /// <inheritdoc cref="XmlSerializer.Deserialize (XmlReader, string)"/>
        public new object Deserialize(XmlReader xmlReader, string encodingStyle)
        {
            var result = base.Deserialize(xmlReader, encodingStyle);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }
        /// <inheritdoc cref="XmlSerializer.Deserialize (XmlReader, XmlDeserializationEvents)"/>
        public new object Deserialize(XmlReader xmlReader, XmlDeserializationEvents events)
        {
            var result = base.Deserialize(xmlReader, events);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }
        /// <inheritdoc cref="XmlSerializer.Deserialize (XmlReader, string, XmlDeserializationEvents)"/>
        public new object Deserialize(XmlReader xmlReader, string encodingStyle, XmlDeserializationEvents events)
        {
            var result = base.Deserialize(xmlReader, encodingStyle, events);
            ProcessDefaultComplexTypes(result, false);
            ProcessOnDeserialized(result);
            return result;
        }

        /// <summary>
        /// Validates and deserializes an XML file using an XSD schema file.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlFilePath">File path to the XML file to validate and deserialize.</param>
        /// <param name="xsdFilePath">File path to the XSD schema file.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>Deserialized object if validation succeeds; null if validation fails.</returns>
        public object ValidateAndDeserialize(
            string xmlFilePath,
            string xsdFilePath,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            errorList = new List<ValidationEventArgs>();
            warningList = new List<ValidationEventArgs>();

            try
            {
                using (XmlReader xmlReader = XmlReader.Create(xmlFilePath))
                using (XmlReader xsdReader = XmlReader.Create(xsdFilePath))
                {
                    return ValidateAndDeserializeCore(xmlReader, xsdReader, out errorList, out warningList);
                }
            }
            catch (Exception)
            {
                // Errors are already captured in the out parameters
            }

            return null;
        }

        /// <summary>
        /// Validates and deserializes an XML file using the type's root attribute to infer schema location.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlFilePath">File path to the XML file to validate and deserialize.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>Deserialized object if validation succeeds; null if validation fails.</returns>
        public object ValidateAndDeserialize(
            string xmlFilePath,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            errorList = new List<ValidationEventArgs>();
            warningList = new List<ValidationEventArgs>();

            try
            {
                using (XmlReader xmlReader = XmlReader.Create(xmlFilePath))
                {
                    return ValidateAndDeserializeCore(xmlReader, null, out errorList, out warningList);
                }
            }
            catch (Exception)
            {
                // Errors are already captured in the out parameters
            }

            return null;
        }

        /// <summary>
        /// Validates and deserializes an XML reader using an XSD schema file.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlReader">XmlReader containing the XML to validate and deserialize.</param>
        /// <param name="xsdFilePath">File path to the XSD schema file.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>Deserialized object if validation succeeds; null if validation fails.</returns>
        public object ValidateAndDeserialize(
            XmlReader xmlReader,
            string xsdFilePath,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            errorList = new List<ValidationEventArgs>();
            warningList = new List<ValidationEventArgs>();

            try
            {
                using (XmlReader xsdReader = XmlReader.Create(xsdFilePath))
                {
                    return ValidateAndDeserializeCore(xmlReader, xsdReader, out errorList, out warningList);
                }
            }
            catch (Exception)
            {
                // Errors are already captured in the out parameters
            }

            return null;
        }

        /// <summary>
        /// Validates and deserializes an XML reader using an XSD schema reader.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlReader">XmlReader containing the XML to validate and deserialize.</param>
        /// <param name="xsdReader">XmlReader containing the XSD schema to validate against.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>Deserialized object if validation succeeds; null if validation fails.</returns>
        public object ValidateAndDeserialize(
            XmlReader xmlReader,
            XmlReader xsdReader,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            return ValidateAndDeserializeCore(xmlReader, xsdReader, out errorList, out warningList);
        }

        /// <summary>
        /// Core validation and deserialization logic used by all overloads.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlReader">XmlReader containing the XML to validate and deserialize.</param>
        /// <param name="xsdReader">XmlReader containing the XSD schema, or null to skip validation.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>Deserialized object if validation succeeds; null if validation fails.</returns>
        private object ValidateAndDeserializeCore(
            XmlReader xmlReader,
            XmlReader xsdReader,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            errorList = new List<ValidationEventArgs>();
            warningList = new List<ValidationEventArgs>();

            try
            {
                // Load XML document
                XDocument xmlDoc = XDocument.Load(xmlReader);
                var lowerDoc = new XDocument(xmlDoc.Root.ToLowerCaseNamesAndRemoveCommentsAndTrueFalseValues());

                XmlReader validatedReader;

                // Validate against schema if provided
                if (xsdReader != null)
                {
                    validatedReader = ValidateAgainstSchemaIgnoreCaseAndRootLoc(
                        lowerDoc.CreateReader(),
                        xsdReader,
                        out errorList,
                        out warningList);

                    // Only deserialize if validation succeeded (no errors)
                    if (errorList.Count > 0 || validatedReader == null)
                    {
                        return null;
                    }
                }
                else
                {
                    // No validation, just use the lowercased document
                    validatedReader = lowerDoc.CreateReader();
                }

                // Deserialize
                var result = base.Deserialize(validatedReader);
                ProcessDefaultComplexTypes(result, false);
                ProcessOnDeserialized(result);
                return result;
            }
            catch (Exception)
            {
                // Errors are already captured in the out parameters by ValidateAgainstSchema
                // or will be null if exception occurred before that
            }

            return null;
        }
        /// <summary>
        /// Validates an XML reader against a schema reader.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlReader">XmlReader containing the XML to validate.</param>
        /// <param name="xsdReader">XmlReader containing the XSD schema.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>XmlReader for the validated XML document.</returns>
        public XmlReader ValidateAgainstSchemaIgnoreCaseAndRootLoc(
            XmlReader xmlReader,
            XmlReader xsdReader,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            XmlSchemaSet schemas = LoadSchemaSet(xsdReader);
            return ValidateAgainstSchemaIgnoreCaseAndRootLoc(xmlReader, schemas, out errorList, out warningList);
        }

        /// <summary>
        /// Validates an XML reader against a schema reader.
        /// The validation is case-insensitive, ignores comments, and can find the schema's root element anywhere in the XML.
        /// </summary>
        /// <param name="xmlReader">XmlReader containing the XML to validate.</param>
        /// <param name="xmlSchemaSet">XmlSchemaSet containing the XSD schema.</param>
        /// <param name="errorList">Output list of validation errors encountered.</param>
        /// <param name="warningList">Output list of validation warnings encountered.</param>
        /// <returns>XmlReader for the validated XML document.</returns>
        public XmlReader ValidateAgainstSchemaIgnoreCaseAndRootLoc(
            XmlReader xmlReader,
            XmlSchemaSet xmlSchemaSet,
            out List<ValidationEventArgs> errorList,
            out List<ValidationEventArgs> warningList)
        {
            var localErrorList = new List<ValidationEventArgs>();
            var localWarningList = new List<ValidationEventArgs>();

            // Load XML document from reader
            XDocument xmlDoc = XDocument.Load(xmlReader);

            // Load schema set
            XmlSchemaSet schemas = xmlSchemaSet;
            XmlSchema firstSchema = schemas.Schemas().Cast<XmlSchema>().FirstOrDefault();
            XNamespace ns = firstSchema.TargetNamespace;
            string rootElement = firstSchema.Elements.Names.OfType<XmlQualifiedName>().Last().Name;
            XName fullRoot = XName.Get(rootElement, ns.NamespaceName);

            // Find the root element (qualified or unqualified)
            XElement baseAppEle = xmlDoc.Root.DescendantsAndSelf(fullRoot).SingleOrDefault();
            if (baseAppEle == null)
            {
                // Check if unqualified root exists
                baseAppEle = xmlDoc.Root.DescendantsAndSelf(rootElement).SingleOrDefault();

                // If unqualified exists, add qualified name to root only
                if (baseAppEle != null)
                {
                    XName qualName = ns + baseAppEle.Name.LocalName;
                    baseAppEle.Name = qualName;

                    // Check if schema uses elementFormDefault="unqualified"
                    // If so, remove namespace from all child elements
                    if (firstSchema.ElementFormDefault == XmlSchemaForm.Unqualified)
                    {
                        RemoveNamespaceFromDescendants(baseAppEle);
                    }
                }
            }

            if (baseAppEle == null)
            {
                // Root element not found - this is a structural issue, throw exception
                throw new InvalidOperationException($"Root element '{rootElement}' not found in XML.");
            }

            XDocument newDoc = new XDocument(baseAppEle);

            // Validate against schema
            newDoc.Validate(schemas, (sender, e) =>
            {
                switch (e.Severity)
                {
                    case XmlSeverityType.Error:
                        localErrorList.Add(e);
                        break;
                    case XmlSeverityType.Warning:
                        localWarningList.Add(e);
                        break;
                }
            });

            errorList = localErrorList;
            warningList = localWarningList;

            return newDoc.CreateReader();
        }

        /// <summary>
        /// Removes namespace from all descendant elements (but not the root element itself).
        /// Used when elementFormDefault="unqualified" in the schema.
        /// </summary>
        /// <param name="element">The root element whose descendants should have namespaces removed.</param>
        private static void RemoveNamespaceFromDescendants(XElement element)
        {
            foreach (var descendant in element.Descendants())
            {
                // Explicitly set to no namespace
                descendant.Name = XNamespace.None + descendant.Name.LocalName;

                // Also remove namespace from attributes (if any have namespaces other than xmlns)
                var attributesToChange = descendant.Attributes()
                    .Where(a => !a.IsNamespaceDeclaration && a.Name.Namespace != XNamespace.None)
                    .ToList();

                foreach (var attr in attributesToChange)
                {
                    var newAttr = new XAttribute(XNamespace.None + attr.Name.LocalName, attr.Value);
                    attr.Remove();
                    descendant.Add(newAttr);
                }
            }
        }

        /// <summary>
        /// Loads the schema from an XSD reader.
        /// </summary>
        /// <param name="xsdReader">XmlReader containing the XSD schema.</param>
        /// <returns>Loaded and compiled schema set.</returns>
        private static XmlSchemaSet LoadSchemaSet(XmlReader xsdReader)
        {
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            XmlSchema aschema = schemaSet.Add(null, xsdReader);

            // Note: Schema includes cannot be automatically resolved from a reader
            // The caller should provide a pre-compiled schema or use the file path overload

            schemaSet.Compile();
            return schemaSet;
        }

        /// <summary>
        /// Call methods that have the attribute OnDeserialized added to a method in the deserailized class.
        /// This occurs after System.Runtime.Serialization.OnDeserialized and DefaultComplexTypes.
        /// </summary>
        /// <param name="_result">The deserailized class.</param>
        private static void ProcessOnDeserialized(object _result)
        {
            var type = _result != null ? _result.GetType() : null;
            var methods = type != null ? type.GetMethods().Where(_ => _.GetCustomAttributes(true).Any(m => m is OnDeserialized)) : null;
            if (methods != null)
            {
                foreach (var mi in methods)
                {
                    mi.Invoke(_result, null);
                }
            }
            var properties = type != null ? type.GetProperties().Where(_ => _.GetCustomAttributes(true).All(_m => !(_m is XmlIgnoreAttribute))) : null;
            if (properties != null)
            {
                properties = properties.Where(o => o.GetMethod.ReturnParameter.ParameterType.IsArray
                                                || (o.GetMethod.ReturnParameter.ParameterType.IsClass
                                                && !o.GetMethod.ReturnParameter.ParameterType.IsAbstract
                                                && o.GetMethod.ReturnParameter.ParameterType != typeof(string)));
                foreach (var prop in properties)
                {
                    var obj = prop.GetValue(_result, null);
                    var enumeration = obj as IEnumerable;
                    if (obj is IEnumerable)
                    {
                        foreach (var item in enumeration)
                        {
                            ProcessOnDeserialized(item);
                        }
                    }
                    else
                    {
                        ProcessOnDeserialized(obj);
                    }
                }
            }
        }

        /// <summary>
        /// If class has attribute DefaultComplexTypes then this method will create a default type for the complex type represented by classes.
        /// The default values mentioned in the XSD complex type will be initiated when creating this default type.
        /// </summary>
        /// <param name="_result">The deserailized class.</param>
        private static void ProcessDefaultComplexTypes(object _result, bool forceDefault)
        {
            Type type = _result != null ? _result.GetType() : null;
            DefaultComplexTypes MyAttributes = (DefaultComplexTypes)Attribute.GetCustomAttribute(type, typeof(DefaultComplexTypes));
            if ((forceDefault || MyAttributes != null) && !type.IsArray)
            {
                if (!forceDefault && MyAttributes != null)
                    forceDefault = MyAttributes.Recursively;
                System.Reflection.PropertyInfo[] props = type.GetProperties();
                System.Reflection.PropertyInfo[] properties = type.GetProperties().Where(o => o.CustomAttributes.All(q => q.AttributeType != typeof(XmlIgnoreAttribute)))
                                                                                  .Where(o => o.GetMethod.ReturnParameter.ParameterType.IsArray
                                                                                           || (o.GetMethod.ReturnParameter.ParameterType.IsClass
                                                                                           && !o.GetMethod.ReturnParameter.ParameterType.IsAbstract
                                                                                           && o.GetMethod.ReturnParameter.ParameterType != typeof(string))).ToArray();
                foreach (var prop in properties)
                {
                    // If property is null create default. Otherwise check if element requests default values.
                    if (prop.GetValue(_result) == null)
                    {
                        if (prop.GetMethod.ReturnParameter.ParameterType.IsArray)
                        {
                            Type eleType = prop.GetMethod.ReturnParameter.ParameterType.GetElementType();
                            // If arry element is not a value type or string then check if element requests default values. Otherwise create a empty/zero index array of value/string type.
                            if (!(eleType.IsValueType || eleType != typeof(string)))
                            {
                                // Check if element requests default values.
                                DefaultComplexTypes arryEleAttributes = (DefaultComplexTypes)Attribute.GetCustomAttribute(eleType, typeof(DefaultComplexTypes));
                                // If Default values requested populate first index of array with a defaulted object. Otherwise create a empty/zero index array.
                                if (arryEleAttributes != null)
                                {
                                    object newObjarr = Activator.CreateInstance(eleType);
                                    ProcessDefaultComplexTypes(newObjarr, arryEleAttributes.Recursively);
                                    object array = Activator.CreateInstance(eleType.MakeArrayType(), 1);
                                    (array as Array).SetValue(newObjarr, (array as Array).GetLowerBound(0));
                                    prop.SetValue(_result, array);
                                }
                                else
                                {
                                    object array = Activator.CreateInstance(eleType.MakeArrayType(), 0);
                                    prop.SetValue(_result, array);
                                }
                            }
                            else
                            {
                                // Create a empty/zero index array of value/string type.
                                // Note: If schema requests a default value for array item should an element get created?
                                // Impossible to do xsd does not create a property attribute for default value for an array element.
                                object array = Activator.CreateInstance(eleType.MakeArrayType(), 0);
                                prop.SetValue(_result, array);
                            }
                        }
                        else
                        {
                            object newObj = Activator.CreateInstance(prop.GetMethod.ReturnParameter.ParameterType);
                            prop.SetValue(_result, newObj);
                            // After newly created object put into property then go into it and check if its own properties need default values
                            ProcessDefaultComplexTypes(prop.GetValue(_result), forceDefault);
                        }
                    }
                    else
                    {
                        // If property is not null go into it and check if its own properties need default values.
                        ProcessDefaultComplexTypes(prop.GetValue(_result), forceDefault);
                    }
                }
                var fields = type.GetFields().Where(o => o.CustomAttributes.All(q => q.AttributeType != typeof(XmlIgnoreAttribute)))
                .Where(o => o.FieldType.IsArray
                || (o.FieldType.IsClass
                && !o.FieldType.IsAbstract
                && o.FieldType != typeof(string)));

                foreach (var field in fields)
                {
                    if (field.GetValue(_result) == null)
                    {
                        if (field.FieldType.IsArray)
                        {
                            Type eleType = field.FieldType.GetElementType();
                            // If arry element is not a value type or string then check if element requests default values. Otherwise create a empty/zero index array of value/string type.
                            if (!(eleType.IsValueType || eleType != typeof(string)))
                            {
                                // Check if element requests default values.
                                DefaultComplexTypes arryEleAttributes = (DefaultComplexTypes)Attribute.GetCustomAttribute(eleType, typeof(DefaultComplexTypes));
                                // If Default values requested populate first index of array with a defaulted object. Otherwise create a empty/zero index array.
                                if (arryEleAttributes != null)
                                {
                                    object newObjarr = Activator.CreateInstance(eleType);
                                    ProcessDefaultComplexTypes(newObjarr, arryEleAttributes.Recursively);

                                    object array = Activator.CreateInstance(eleType.MakeArrayType(), 1);
                                    (array as Array).SetValue(newObjarr, (array as Array).GetLowerBound(0));

                                    field.SetValue(_result, array);
                                }
                                else
                                {
                                    object array = Activator.CreateInstance(eleType.MakeArrayType(), 0);

                                    field.SetValue(_result, array);
                                }
                            }
                            else
                            {
                                // Create a empty/zero index array of value/string type.
                                // Note: If schema requests a default value for array item should an element get created?
                                // Impossible to do xsd does not create a field attribute for default value for an array element.
                                object array = Activator.CreateInstance(eleType.MakeArrayType(), 0);

                                field.SetValue(_result, array);
                            }
                        }
                        else
                        {
                            object newObj = Activator.CreateInstance(field.FieldType);
                            field.SetValue(_result, newObj);
                            // After newly created object put into field then go into it and check if its own properties need default values
                            ProcessDefaultComplexTypes(field.GetValue(_result), forceDefault);
                        }
                    }
                    else
                    {
                        // If field is not null go into it and check if its own properties need default values.
                        ProcessDefaultComplexTypes(field.GetValue(_result), forceDefault);
                    }
                }
            }
        }
    }

    public static class XmlExtensions
    {
        public static XElement ToLowerCaseNamesAndRemoveCommentsAndTrueFalseValues(this XElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            // element name: lower-case local name, preserve namespace
            XName newName = element.Name.Namespace + element.Name.LocalName.ToLowerInvariant();
            var normalized = new XElement(newName);
            // attributes: lower-case local name, preserve namespace; keep xmlns declarations unchanged
            foreach (var attr in element.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                {
                    normalized.Add(attr);
                    continue;
                }
                XName newAttrName = attr.Name.Namespace + attr.Name.LocalName.ToLowerInvariant();
                string newAttrValue = NormalizeTrueFalse(attr.Value);
                normalized.Add(new XAttribute(newAttrName, newAttrValue));
            }
            // nodes: recurse for elements; normalize text nodes if TRUE/FALSE
            foreach (var node in element.Nodes())
            {
                if (node is XElement childElement)
                {
                    normalized.Add(ToLowerCaseNamesAndRemoveCommentsAndTrueFalseValues(childElement));
                }
                else if (node is XText text)
                {
                    normalized.Add(new XText(NormalizeTrueFalse(text.Value)));
                }
                else if (node is XComment comm)
                {

                }
                else
                {
                    // comments, CDATA, processing instructions, etc.
                    normalized.Add(node);
                }
            }

            return normalized;
        }

        private static string NormalizeTrueFalse(string value)
        {
            if (value == null) return value;
            // If the entire value is TRUE/FALSE (case-insensitive), normalize to lowercase.
            // Trim handles " TRUE " cases.
            var trimmed = value.Trim();
            if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return "true";
            if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return "false";
            // Otherwise return original unchanged
            return value;
        }

        /// <summary>
        /// Loads the schema provided by the EMBEDEDXSDNAME in assembly ASSEMBLYNAME.
        /// </summary>
        /// <returns>Loaded and compiled schema set of the EMBEDEDXSDNAME.</returns>
        public static XmlSchemaSet LoadSchemaSet(string ASSEMBLYNAME, string EMBEDEDXSDNAME)
        {
            Assembly ConfigurationAssembly;
            string[] resourceNames;

            if (!String.IsNullOrEmpty(ASSEMBLYNAME))
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
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
    }
}


