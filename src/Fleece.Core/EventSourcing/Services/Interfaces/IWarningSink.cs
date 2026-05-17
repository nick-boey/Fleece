namespace Fleece.Core.EventSourcing.Services.Interfaces;

/// <summary>
/// Diagnostic destination for warnings emitted by replay-time invariants
/// (e.g. parallel change files falling through to GUID-alphabetical ordering).
/// Injectable so test runs can capture warnings deterministically instead of
/// polluting <see cref="Console.Error"/>.
/// </summary>
public interface IWarningSink
{
    void Warn(string message);
}

/// <summary>Default sink: one line per warning to <see cref="Console.Error"/>.</summary>
public sealed class ConsoleWarningSink : IWarningSink
{
    public static readonly ConsoleWarningSink Instance = new();
    public void Warn(string message) => Console.Error.WriteLine("warning: " + message);
}

/// <summary>Silent sink. Useful as a default for non-CLI consumers and unit tests.</summary>
public sealed class NullWarningSink : IWarningSink
{
    public static readonly NullWarningSink Instance = new();
    public void Warn(string message)
    {
    }
}
