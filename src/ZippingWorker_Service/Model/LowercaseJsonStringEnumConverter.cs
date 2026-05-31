using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZippingWorker_Service.Model
{
    /// <summary>
    /// Custom JSON converter that serializes enums as lowercase strings
    /// </summary>
    public class LowercaseJsonStringEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            if (value == null)
            {
                throw new JsonException("Enum value cannot be null");
            }

            // Try case-insensitive parsing
            if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
            {
                return result;
            }

            throw new JsonException($"Unable to convert \"{value}\" to {typeof(TEnum).Name}");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString().ToLowerInvariant());
        }
    }
}
