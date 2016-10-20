//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Schema Generator
//
// Changes to this file may cause incorrect behavior and will be lost when
// the code is regenerated.
// <auto-generated />
//------------------------------------------------------------------------------


// suppress "Missing XML comment for publicly visible type or member"
#pragma warning disable 1591


#region ReSharper warnings
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable RedundantNameQualifier
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable UnusedParameter.Local
// ReSharper disable RedundantUsingDirective
#endregion

namespace Microsoft.ApplicationInsights.Extensibility.Implementation.External
{
    using Microsoft.ApplicationInsights.DataContracts;
    
    internal static class RequestDataSerializer
    {
        public static void Serialize(RequestData item, JsonWriter writer)
        {
            writer.WriteStartObject();
            SerializeFields(item, writer);
            writer.WriteEndObject();
        }
        
        public static void SerializeFields(RequestData item, JsonWriter writer)
        {
            DomainSerializer.SerializeFields(item, writer);
            {
                writer.WriteRequiredProperty("ver", item.ver);
                writer.WriteRequiredProperty("id", item.id, 128);
                writer.WriteRequiredProperty("duration", item.duration);
                writer.WriteRequiredProperty("responseCode", item.responseCode, 1024);
                writer.WriteRequiredProperty("success", item.success);
                writer.WriteProperty("source", item.source, 1024);
                writer.WriteProperty("name", item.name, 1024);
                writer.WriteProperty("url", item.url, 2048);
                writer.WriteProperty("properties", item.properties, 150, 8192);
                writer.WriteProperty("measurements", item.measurements, 150);
            }
        }
    }
}
