// Compat shim for .NET 4.0: the frozen sources apply System.Text.Json.Serialization
// attributes ([JsonIgnore] / [JsonPropertyName]). On net40 there is no System.Text.Json,
// so we provide inert attribute substitutes. The Windows clients serialize credentials via
// XML (XmlSerializer), so these attributes only need to exist for compilation.
using System;

namespace System.Text.Json.Serialization
{
    /// <summary>Shim of System.Text.Json.Serialization.JsonIgnoreAttribute (no runtime effect).</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class JsonIgnoreAttribute : Attribute
    {
    }

    /// <summary>Shim of System.Text.Json.Serialization.JsonPropertyNameAttribute (no runtime effect).</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonPropertyNameAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the JSON property name.</summary>
        public JsonPropertyNameAttribute(string name)
        {
            Name = name;
        }

        /// <summary>The JSON property name (informational only in this shim).</summary>
        public string Name { get; private set; }
    }
}
