
using System.Xml.Linq;

namespace ZippingWorker_Service.Configuration
{
    using System;
    using System.Xml.Linq;
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

    }
}
