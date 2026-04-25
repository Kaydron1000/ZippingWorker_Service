
namespace ZippingWorkerService.Configuration
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Linq;
    using System.Xml;
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
}


