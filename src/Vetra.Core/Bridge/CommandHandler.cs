using System.Text.Json;
using Vetra.Protocol;

namespace Vetra.Bridge;

internal sealed class CommandHandler
{
    private readonly Func<JsonElement?, CancellationToken, Task<object?>> _execute;
    private readonly int _argumentCount;

    private CommandHandler(int argumentCount, Func<JsonElement?, CancellationToken, Task<object?>> execute)
    {
        _argumentCount = argumentCount;
        _execute = execute;
    }

    public static CommandHandler Create(Func<object?> handler)
    {
        return new CommandHandler(0, (_, _) => Task.FromResult(handler()));
    }

    /// <summary>
    /// Creates a handler that ignores argument shape (used for capability-disabled stubs).
    /// </summary>
    public static CommandHandler CreateIgnoringArgs(Func<object?> handler)
    {
        return new CommandHandler(-1, (_, _) => Task.FromResult(handler()));
    }

    public static CommandHandler Create<T1>(Func<T1, object?> handler)
    {
        return new CommandHandler(1, (args, _) =>
        {
            var values = ReadArgs(args, 1);
            var a1 = DeserializeArg<T1>(values[0], 0);
            return Task.FromResult(handler(a1));
        });
    }

    public static CommandHandler Create<T1, T2>(Func<T1, T2, object?> handler)
    {
        return new CommandHandler(2, (args, _) =>
        {
            var values = ReadArgs(args, 2);
            var a1 = DeserializeArg<T1>(values[0], 0);
            var a2 = DeserializeArg<T2>(values[1], 1);
            return Task.FromResult(handler(a1, a2));
        });
    }

    public static CommandHandler Create<T1, T2, T3>(Func<T1, T2, T3, object?> handler)
    {
        return new CommandHandler(3, (args, _) =>
        {
            var values = ReadArgs(args, 3);
            var a1 = DeserializeArg<T1>(values[0], 0);
            var a2 = DeserializeArg<T2>(values[1], 1);
            var a3 = DeserializeArg<T3>(values[2], 2);
            return Task.FromResult(handler(a1, a2, a3));
        });
    }

    public static CommandHandler CreateAsync(Func<CancellationToken, Task<object?>> handler)
    {
        return new CommandHandler(0, (_, ct) => handler(ct));
    }

    public static CommandHandler CreateAsync<T1>(Func<T1, CancellationToken, Task<object?>> handler)
    {
        return new CommandHandler(1, (args, ct) =>
        {
            var values = ReadArgs(args, 1);
            var a1 = DeserializeArg<T1>(values[0], 0);
            return handler(a1, ct);
        });
    }

    public static CommandHandler CreateAsync<T1, T2>(Func<T1, T2, CancellationToken, Task<object?>> handler)
    {
        return new CommandHandler(2, (args, ct) =>
        {
            var values = ReadArgs(args, 2);
            var a1 = DeserializeArg<T1>(values[0], 0);
            var a2 = DeserializeArg<T2>(values[1], 1);
            return handler(a1, a2, ct);
        });
    }

    public static CommandHandler CreateAsync<T1, T2, T3>(Func<T1, T2, T3, CancellationToken, Task<object?>> handler)
    {
        return new CommandHandler(3, (args, ct) =>
        {
            var values = ReadArgs(args, 3);
            var a1 = DeserializeArg<T1>(values[0], 0);
            var a2 = DeserializeArg<T2>(values[1], 1);
            var a3 = DeserializeArg<T3>(values[2], 2);
            return handler(a1, a2, a3, ct);
        });
    }

    public Task<object?> ExecuteAsync(JsonElement? args, CancellationToken cancellationToken)
        => _execute(args, cancellationToken);

    private static JsonElement[] ReadArgs(JsonElement? args, int expectedCount)
    {
        if (expectedCount < 0)
        {
            return Array.Empty<JsonElement>();
        }

        if (expectedCount == 0)
        {
            if (args is null || args.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return Array.Empty<JsonElement>();
            }

            if (args.Value.ValueKind == JsonValueKind.Array && args.Value.GetArrayLength() == 0)
            {
                return Array.Empty<JsonElement>();
            }

            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                $"Command expects no arguments but received {Describe(args.Value)}.");
        }

        if (args is null || args.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                $"Command expects {expectedCount} argument(s) but none were provided.");
        }

        if (args.Value.ValueKind != JsonValueKind.Array)
        {
            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                "Command arguments must be a JSON array.");
        }

        var length = args.Value.GetArrayLength();
        if (length != expectedCount)
        {
            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                $"Command expects {expectedCount} argument(s) but received {length}.");
        }

        var values = new JsonElement[length];
        var index = 0;
        foreach (var element in args.Value.EnumerateArray())
        {
            values[index++] = element;
        }

        return values;
    }

    private static T DeserializeArg<T>(JsonElement element, int index)
    {
        try
        {
            if (typeof(T) == typeof(JsonElement))
            {
                return (T)(object)element;
            }

            var value = VetraJson.Deserialize<T>(element);
            if (value is null && Nullable.GetUnderlyingType(typeof(T)) is null && typeof(T).IsValueType)
            {
                throw new VetraException(
                    VetraErrorCodes.InvalidArguments,
                    $"Argument at index {index} could not be converted to {typeof(T).Name}.");
            }

            return value!;
        }
        catch (VetraException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                $"Argument at index {index} could not be converted to {typeof(T).Name}.",
                innerException: ex);
        }
    }

    private static string Describe(JsonElement element) => element.ValueKind.ToString();
}
