namespace Vetra.Protocol;

/// <summary>
/// Message kinds exchanged between JavaScript and .NET.
/// </summary>
public static class VetraMessageType
{
    /// <summary>JavaScript is invoking a .NET command.</summary>
    public const string Invoke = "invoke";

    /// <summary>A successful command result.</summary>
    public const string Result = "result";

    /// <summary>A command error result.</summary>
    public const string Error = "error";

    /// <summary>A .NET-emitted event for JavaScript listeners.</summary>
    public const string Event = "event";
}
