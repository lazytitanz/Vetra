using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vetra.Protocol;

/// <summary>
/// Shared JSON serializer options for the Vetra protocol.
/// </summary>
public static class VetraJson
{
    /// <summary>
    /// Gets the serializer options used for bridge messages.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>
    /// Serializes a value to JSON text.
    /// </summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// Deserializes JSON text to <typeparamref name="T"/>.
    /// </summary>
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    /// <summary>
    /// Deserializes a <see cref="JsonElement"/> to <typeparamref name="T"/>.
    /// </summary>
    public static T? Deserialize<T>(JsonElement element) => element.Deserialize<T>(Options);
}
