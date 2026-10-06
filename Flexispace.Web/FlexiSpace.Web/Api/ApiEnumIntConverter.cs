using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flexispace.Web.Api;

/// <summary>
/// FlexiSpace 1 serializes enums as names (JsonStringEnumConverter).
/// The original API sent numbers. Accept both on read; write numbers
/// (the API still allows integer enum values).
/// </summary>
internal sealed class ApiEnumIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
            return n;

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            if (int.TryParse(text, out var parsed))
                return parsed;

            return text.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant() switch
            {
                "centremanager" or "centermanager" => 0,
                "staff" => 1,
                "administrator" => 2,
                "client" => 3,
                "pending" => 0,
                "cancelled" or "canceled" => 1,
                "completed" => 2,
                "confirmed" => 3,
                "available" => 0,
                "maintenance" => 1,
                "unavailable" => 2,
                _ => 0
            };
        }

        throw new JsonException($"Expected a number or enum name, got {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
