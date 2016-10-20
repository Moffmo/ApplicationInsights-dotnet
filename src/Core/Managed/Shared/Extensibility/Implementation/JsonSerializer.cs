﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.External;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Platform;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

    /// <summary>
    /// Serializes and compress the telemetry items into a JSON string. Compression will be done using GZIP, for Windows Phone 8 compression will be disabled because there
    /// is API support for it. 
    /// </summary>    
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class JsonSerializer
    {   
        private static readonly UTF8Encoding TransmissionEncoding = new UTF8Encoding(false);

        /// <summary>
        /// Gets the compression type used by the serializer. 
        /// </summary>
        public static string CompressionType
        {
            get
            {
                return "gzip";
            }
        }

        /// <summary>
        /// Gets the content type used by the serializer. 
        /// </summary>
        public static string ContentType
        {
            get
            {
                return "application/x-json-stream";
            }
        }

        /// <summary>
        /// Serializes and compress the telemetry items into a JSON string. Each JSON object is separated by a new line. 
        /// </summary>
        /// <param name="telemetryItems">The list of telemetry items to serialize.</param>
        /// <param name="compress">Should serialization also perform compression.</param>
        /// <returns>The compressed and serialized telemetry items.</returns>
        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Disposing a MemoryStream multiple times is harmless.")]
        public static byte[] Serialize(IEnumerable<ITelemetry> telemetryItems, bool compress = true)
        {
            var memoryStream = new MemoryStream();
            using (Stream compressedStream = compress ? CreateCompressedStream(memoryStream) : memoryStream)
            {
                using (var streamWriter = new StreamWriter(compressedStream, TransmissionEncoding))
                {
                    SerializeToStream(telemetryItems, streamWriter);
                }
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Converts serialized telemetry items to a byte array.
        /// </summary>
        /// <param name="telemetryItems">Serialized telemetry items.</param>
        /// <param name="compress">Should serialization also perform compression.</param>
        /// <returns>The compressed and serialized telemetry items.</returns>
        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Disposing a MemoryStream multiple times is harmless.")]
        public static byte[] ConvertToByteArray(string telemetryItems, bool compress = true)
        {
            if (string.IsNullOrEmpty(telemetryItems))
            {
                throw new ArgumentNullException("telemetryItems");
            }

            var memoryStream = new MemoryStream();
            using (Stream compressedStream = compress ? CreateCompressedStream(memoryStream) : memoryStream)
            using (var streamWriter = new StreamWriter(compressedStream, TransmissionEncoding))
            {
                streamWriter.Write(telemetryItems);
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Deserializes and decompress the telemetry items into a JSON string.
        /// </summary>
        /// <param name="telemetryItemsData">Serialized telemetry items.</param>
        /// <param name="compress">Should deserialization also perform decompression.</param>
        /// <returns>Telemetry items serialized as a string.</returns>
        public static string Deserialize(byte[] telemetryItemsData, bool compress = true)
        {
            var memoryStream = new MemoryStream(telemetryItemsData);
            
            using (Stream decompressedStream = compress ? (Stream)new GZipStream(memoryStream, CompressionMode.Decompress) : memoryStream)
            {
                using (MemoryStream str = new MemoryStream())
                {
                    decompressedStream.CopyTo(str);
                    byte[] output = str.ToArray();
                    return Encoding.UTF8.GetString(output, 0, output.Length);
                }
            }
        }

        /// <summary>
        ///  Serialize and compress a telemetry item. 
        /// </summary>
        /// <param name="telemetryItem">A telemetry item.</param>
        /// <param name="compress">Should serialization also perform compression.</param>
        /// <returns>The compressed and serialized telemetry item.</returns>
        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Disposing a MemoryStream multiple times is harmless.")]
        internal static byte[] Serialize(ITelemetry telemetryItem, bool compress = true)
        {
            return Serialize(new ITelemetry[] { telemetryItem }, compress);
        }

        /// <summary>
        /// Serializes <paramref name="telemetryItems"/> into a JSON string. Each JSON object is separated by a new line. 
        /// </summary>
        /// <param name="telemetryItems">The list of telemetry items to serialize.</param>
        /// <returns>A JSON string of all the serialized items.</returns>
        internal static string SerializeAsString(IEnumerable<ITelemetry> telemetryItems)
        {
            var stringBuilder = new StringBuilder();
            using (StringWriter stringWriter = new StringWriter(stringBuilder, CultureInfo.InvariantCulture))
            {
                SerializeToStream(telemetryItems, stringWriter);
                return stringBuilder.ToString();
            }
        }

        /// <summary>
        /// Serializes a <paramref name="telemetry"/> into a JSON string. 
        /// </summary>
        /// <param name="telemetry">The telemetry to serialize.</param>
        /// <returns>A JSON string of the serialized telemetry.</returns>
        internal static string SerializeAsString(ITelemetry telemetry)
        {
            return SerializeAsString(new ITelemetry[] { telemetry });
        }

        #region Exception Serializer helper

        private static void SerializeExceptions(IEnumerable<ExceptionDetails> exceptions, IJsonWriter writer)
        {
            int exceptionArrayIndex = 0;

            foreach (ExceptionDetails exceptionDetails in exceptions)
            {
                if (exceptionArrayIndex++ != 0)
                {
                    writer.WriteComma();
                }

                writer.WriteStartObject();
                writer.WriteProperty("id", exceptionDetails.id);
                if (exceptionDetails.outerId != 0)
                {
                    writer.WriteProperty("outerId", exceptionDetails.outerId);
                }

                writer.WriteProperty(
                    "typeName",
                    Utils.PopulateRequiredStringValue(exceptionDetails.typeName, "typeName", typeof(ExceptionTelemetry).FullName));
                writer.WriteProperty(
                    "message",
                    Utils.PopulateRequiredStringValue(exceptionDetails.message, "message", typeof(ExceptionTelemetry).FullName));

                if (exceptionDetails.hasFullStack)
                {
                    writer.WriteProperty("hasFullStack", exceptionDetails.hasFullStack);
                }

                writer.WriteProperty("stack", exceptionDetails.stack);

                if (exceptionDetails.parsedStack.Count > 0)
                {
                    writer.WritePropertyName("parsedStack");

                    writer.WriteStartArray();

                    int stackFrameArrayIndex = 0;

                    foreach (StackFrame frame in exceptionDetails.parsedStack)
                    {
                        if (stackFrameArrayIndex++ != 0)
                        {
                            writer.WriteComma();
                        }

                        writer.WriteStartObject();
                        SerializeStackFrame(frame, writer);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }
        }

        private static void SerializeStackFrame(StackFrame frame, IJsonWriter writer)
        {
            writer.WriteProperty("level", frame.level);
            writer.WriteProperty(
                "method",
                Utils.PopulateRequiredStringValue(frame.method, "StackFrameMethod", typeof(ExceptionTelemetry).FullName));
            writer.WriteProperty("assembly", frame.assembly);
            writer.WriteProperty("fileName", frame.fileName);

            // 0 means it is unavailable
            if (frame.line != 0)
            {
                writer.WriteProperty("line", frame.line);
            }
        }

        #endregion Exception Serializer helper

        /// <summary>
        /// Creates a GZIP compression stream that wraps <paramref name="stream"/>. For windows phone 8.0 it returns <paramref name="stream"/>. 
        /// </summary>
        private static Stream CreateCompressedStream(Stream stream)
        {
            return new GZipStream(stream, CompressionMode.Compress);
        }

        private static void SerializeTelemetryItem(ITelemetry telemetryItem, JsonWriter jsonWriter)
        {
            if (telemetryItem is EventTelemetry)
            {
                EventTelemetry eventTelemetry = telemetryItem as EventTelemetry;
                SerializeEventTelemetry(eventTelemetry, jsonWriter);
            }
            else if (telemetryItem is ExceptionTelemetry)
            {
                ExceptionTelemetry exceptionTelemetry = telemetryItem as ExceptionTelemetry;
                SerializeExceptionTelemetry(exceptionTelemetry, jsonWriter);
            }
            else if (telemetryItem is MetricTelemetry)
            {
                MetricTelemetry metricTelemetry = telemetryItem as MetricTelemetry;
                SerializeMetricTelemetry(metricTelemetry, jsonWriter);
            }
            else if (telemetryItem is PageViewTelemetry)
            {
                PageViewTelemetry pageViewTelemetry = telemetryItem as PageViewTelemetry;
                SerializePageViewTelemetry(pageViewTelemetry, jsonWriter);
            }
            else if (telemetryItem is DependencyTelemetry)
            {
                DependencyTelemetry remoteDependencyTelemetry = telemetryItem as DependencyTelemetry;
                SerializeDependencyTelemetry(remoteDependencyTelemetry, jsonWriter);
            }
            else if (telemetryItem is RequestTelemetry)
            {
                RequestTelemetry requestTelemetry = telemetryItem as RequestTelemetry;
                SerializeRequestTelemetry(requestTelemetry, jsonWriter);
            }
#pragma warning disable 618
            else if (telemetryItem is SessionStateTelemetry)
            {
                EventTelemetry telemetry = (telemetryItem as SessionStateTelemetry).Data;
                SerializeEventTelemetry(telemetry, jsonWriter);
            }
#pragma warning restore 618
            else if (telemetryItem is TraceTelemetry)
            {
                TraceTelemetry traceTelemetry = telemetryItem as TraceTelemetry;
                SerializeTraceTelemetry(traceTelemetry, jsonWriter);
            }
#pragma warning disable 618
            else if (telemetryItem is PerformanceCounterTelemetry)
            {
                MetricTelemetry telemetry = (telemetryItem as PerformanceCounterTelemetry).Data;
                SerializeMetricTelemetry(telemetry, jsonWriter);
            }
            else if (telemetryItem is AvailabilityTelemetry)
            {
                AvailabilityTelemetry availabilityTelemetry = telemetryItem as AvailabilityTelemetry;
                SerializeAvailability(availabilityTelemetry, jsonWriter);
            }
            else
            {
                string msg = string.Format(CultureInfo.InvariantCulture, "Unknown telemtry type: {0}", telemetryItem.GetType());                
                CoreEventSource.Log.LogVerbose(msg);
            }
        }

        /// <summary>
        /// Serializes <paramref name="telemetryItems"/> and write the response to <paramref name="streamWriter"/>.
        /// </summary>
        private static void SerializeToStream(IEnumerable<ITelemetry> telemetryItems, TextWriter streamWriter)
        {
            JsonWriter jsonWriter = new JsonWriter(streamWriter);

            int telemetryCount = 0;
            foreach (ITelemetry telemetryItem in telemetryItems)
            {
                if (telemetryCount++ > 0)
                {
                    streamWriter.Write(Environment.NewLine);
                }

                telemetryItem.Sanitize();
                SerializeTelemetryItem(telemetryItem, jsonWriter);
            }
        }

        #region Serialize methods for each ITelemetry implementation

        private static void SerializeEventTelemetry(EventTelemetry eventTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            eventTelemetry.WriteTelemetryName(writer, EventTelemetry.TelemetryName);
            eventTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                writer.WriteProperty("baseType", eventTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    EventDataSerializer.Serialize(eventTelemetry.Data, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void SerializeExceptionTelemetry(ExceptionTelemetry exceptionTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            exceptionTelemetry.WriteTelemetryName(writer, ExceptionTelemetry.TelemetryName);
            exceptionTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                writer.WriteProperty("baseType", exceptionTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    writer.WriteStartObject();

                    writer.WriteProperty("ver", exceptionTelemetry.Data.ver);
                    writer.WriteProperty("properties", exceptionTelemetry.Data.properties);
                    writer.WriteProperty("measurements", exceptionTelemetry.Data.measurements);
                    writer.WritePropertyName("exceptions");
                    {
                        writer.WriteStartArray();

                        SerializeExceptions(exceptionTelemetry.Exceptions, writer);

                        writer.WriteEndArray();
                    }

                    if (exceptionTelemetry.Data.severityLevel.HasValue)
                    {
                        writer.WriteProperty("severityLevel", exceptionTelemetry.Data.severityLevel.Value.ToString());
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void SerializeMetricTelemetry(MetricTelemetry metricTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            metricTelemetry.WriteTelemetryName(writer, MetricTelemetry.TelemetryName);
            metricTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                // TODO: MetricTelemetry should write type as this.data.baseType once Common Schema 2.0 compliant.
                writer.WriteProperty("baseType", metricTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    MetricDataSerializer.Serialize(metricTelemetry.Data, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void SerializePageViewTelemetry(PageViewTelemetry pageViewTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            pageViewTelemetry.WriteTelemetryName(writer, PageViewTelemetry.TelemetryName);
            pageViewTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                writer.WriteProperty("baseType", pageViewTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    PageViewDataSerializer.Serialize(pageViewTelemetry.Data, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void SerializeDependencyTelemetry(DependencyTelemetry dependencyTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            dependencyTelemetry.WriteTelemetryName(writer, DependencyTelemetry.TelemetryName);
            dependencyTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                writer.WriteProperty("baseType", dependencyTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    RemoteDependencyDataSerializer.Serialize(dependencyTelemetry.InternalData, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void SerializeRequestTelemetry(RequestTelemetry requestTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            requestTelemetry.WriteTelemetryName(writer, RequestTelemetry.TelemetryName);
            requestTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                writer.WriteProperty("baseType", requestTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    RequestDataSerializer.Serialize(requestTelemetry.Data, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void SerializeTraceTelemetry(TraceTelemetry traceTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            traceTelemetry.WriteTelemetryName(writer, TraceTelemetry.TelemetryName);
            traceTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                // TODO: MetricTelemetry should write type as this.data.baseType once Common Schema 2.0 compliant.
                writer.WriteProperty("baseType", traceTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    MessageDataSerializer.Serialize(traceTelemetry.Data, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Serializes this object in JSON format.
        /// </summary>
        private static void SerializeAvailability(AvailabilityTelemetry availabilityTelemetry, JsonWriter writer)
        {
            writer.WriteStartObject();

            availabilityTelemetry.WriteTelemetryName(writer, AvailabilityTelemetry.TelemetryName);
            availabilityTelemetry.WriteEnvelopeProperties(writer);
            writer.WritePropertyName("data");
            {
                writer.WriteStartObject();

                writer.WriteProperty("baseType", availabilityTelemetry.BaseType);
                writer.WritePropertyName("baseData");
                {
                    AvailabilityDataSerializer.Serialize(availabilityTelemetry.Data, writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        #endregion Serialize methods for each ITelemetry implementation
    }
}
