using System.Xml;

namespace Nalu.Maui.Navigation.SourceGenerators;

/// <summary>
/// The root-element lead extracted from a .xaml AdditionalFile: <c>x:Class</c> plus the root
/// type reference (either a clr-namespace or an xmlns URI to resolve via XmlnsDefinition).
/// </summary>
internal sealed record XamlLead(string ClassMetadataName, string RootName, string? RootClrNamespace, string? RootXmlnsUri);

/// <summary>
/// Extracts the discovery lead from a .xaml file by reading ONLY the root element:
/// <c>x:Class</c> (the page type) and the root type reference (its base). MAUI injects every
/// <c>MauiXaml</c> item as an AdditionalFile, so this works with every inflator — including
/// the XAML source generator, whose generated x:Class partial no other generator can see.
/// </summary>
internal static class XamlLeadParser
{
    private const string Xaml2009 = "http://schemas.microsoft.com/winfx/2009/xaml";
    private const string Xaml2006 = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string ClrNamespacePrefix = "clr-namespace:";

    public static XamlLead? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        try
        {
            using var stringReader = new StringReader(text);

            using var reader = XmlReader.Create(
                stringReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    IgnoreProcessingInstructions = true
                }
            );

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                var rootName = reader.LocalName;
                var rootUri = reader.NamespaceURI;
                string? className = null;
                var isGenericRoot = false;

                while (reader.MoveToNextAttribute())
                {
                    if (reader.NamespaceURI is Xaml2009 or Xaml2006)
                    {
                        if (reader.LocalName == "Class")
                        {
                            className = reader.Value;
                        }
                        else if (reader.LocalName == "TypeArguments")
                        {
                            isGenericRoot = true;
                        }
                    }
                }

                // Only the root element matters; generic roots mirror the IsGenericType skip.
                if (string.IsNullOrEmpty(className) || isGenericRoot)
                {
                    return null;
                }

                if (rootUri.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
                {
                    var body = rootUri.Substring(ClrNamespacePrefix.Length);
                    var end = body.IndexOf(';');
                    var clrNamespace = end < 0 ? body : body.Substring(0, end);

                    return new XamlLead(className!, rootName, clrNamespace, null);
                }

                return new XamlLead(className!, rootName, null, rootUri);
            }
        }
        catch (XmlException)
        {
            // Malformed XAML: the real XAML toolchain will report it; discovery just skips.
        }

        return null;
    }
}
