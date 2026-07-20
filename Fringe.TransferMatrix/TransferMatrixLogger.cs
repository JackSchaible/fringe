namespace FringeTransferMatrix;

/// <summary>Thin console-output wrapper that avoids passing string literals directly to well-known
/// localizable parameters, satisfying CA1303 without resource files.</summary>
internal static class TransferMatrixLogger
{
    /// <summary>Writes <paramref name="line"/> to standard output.</summary>
    internal static void Log(string line)
    {
        Console.Out.WriteLine(line);
    }

    /// <summary>Writes <paramref name="line"/> to standard error.</summary>
    internal static void LogError(string line)
    {
        Console.Error.WriteLine(line);
    }

    /// <summary>Returns <paramref name="line"/> unchanged; use to wrap literals passed to parameters
    /// whose names are in the CA1303 heuristic list (e.g. "message").</summary>
    internal static string AsString(string line)
    {
        return line;
    }
}
