using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vetra.Protocol;

/// <summary>
/// Envelope for all Vetra bridge messages.
/// </summary>
public sealed class VetraMessage
{
    /// <summary>Protocol version.</summary>
    [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

    /// <summary>Message type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Correlation identifier for request/response pairs.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Command or event name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Invoke arguments.</summary>
    [JsonPropertyName("args")]
    public JsonElement? Args { get; set; }

    /// <summary>Result or event payload.</summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>Structured error payload.</summary>
    [JsonPropertyName("error")]
    public VetraErrorPayload? Error { get; set; }
}

/// <summary>
/// Structured error returned to JavaScript.
/// </summary>
public sealed class VetraErrorPayload
{
    /// <summary>Machine-readable error code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional details.</summary>
    [JsonPropertyName("details")]
    public object? Details { get; set; }
}
