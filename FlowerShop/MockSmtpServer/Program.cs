using MockSmtpServer;

var host = GetOption(args, "--host", "MOCK_SMTP_HOST", "127.0.0.1");
var port = GetPortOption(args, "--port", "MOCK_SMTP_PORT", 8025);
var outputDir = GetOption(args, "--output", "MOCK_SMTP_OUTPUT_DIR", Path.Combine(AppContext.BaseDirectory, "mock-emails"));

Directory.CreateDirectory(outputDir);

Console.WriteLine($"Mock SMTP server: {host}:{port}");
Console.WriteLine($"Storing emails in: {outputDir}");

await using var serverHost = MockSmtpServerHost.Start(host, port, outputDir);

Console.CancelKeyPress += async (_, e) =>
{
    e.Cancel = true;
    await serverHost.DisposeAsync();
};

await serverHost.WaitForShutdownAsync();

static string GetOption(string[] args, string flag, string envName, string defaultValue)
{
    var envValue = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(envValue))
    {
        return envValue;
    }

    var index = Array.FindIndex(args, arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    if (index >= 0 && index + 1 < args.Length)
    {
        return args[index + 1];
    }

    return defaultValue;
}

static int GetPortOption(string[] args, string flag, string envName, int defaultValue)
{
    var value = GetOption(args, flag, envName, defaultValue.ToString());
    return int.TryParse(value, out var port) ? port : defaultValue;
}
