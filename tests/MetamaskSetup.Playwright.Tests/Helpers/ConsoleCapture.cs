namespace MetamaskSetup.Playwright.Tests.Helpers;

/// <summary>
/// Redirects Console.Out for the duration of the using block, allowing tests to
/// inspect output written by MetaMaskSetupService (e.g. "Context saved to cache").
/// </summary>
public sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _original;
    private readonly StringWriter _writer;

    public ConsoleCapture()
    {
        _original = Console.Out;
        _writer = new StringWriter();
        Console.SetOut(_writer);
    }

    /// <summary>All text written to Console.Out since capture started.</summary>
    public string Output => _writer.ToString();

    public bool Contains(string value) =>
        Output.Contains(value, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        Console.SetOut(_original);
        _writer.Dispose();
    }
}
