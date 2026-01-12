using System.Buffers;
using Microsoft.Extensions.DependencyInjection;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace MockSmtpServer;

public sealed class MockSmtpServerHost : IAsyncDisposable
{
    private readonly SmtpServer.SmtpServer _server;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;

    public string Host { get; }
    public int Port { get; }
    public string OutputDir { get; }

    private MockSmtpServerHost(string host, int port, string outputDir)
    {
        Host = host;
        Port = port;
        OutputDir = outputDir;
        _cts = new CancellationTokenSource();

        Directory.CreateDirectory(outputDir);

        var options = new SmtpServerOptionsBuilder()
            .ServerName("FlowerShop.MockSmtp")
            .Endpoint(builder =>
            {
                builder.Port(port);
                builder.AllowUnsecureAuthentication(true);
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IMessageStore>(new FileMessageStore(outputDir))
            .BuildServiceProvider();

        _server = new SmtpServer.SmtpServer(options, services);
        _runTask = _server.StartAsync(_cts.Token);
    }

    public static MockSmtpServerHost Start(string host, int port, string outputDir)
    {
        return new MockSmtpServerHost(host, port, outputDir);
    }

    public Task WaitForShutdownAsync() => _runTask;

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        var completed = await Task.WhenAny(_runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completed != _runTask)
        {
            _cts.Dispose();
            return;
        }
        _cts.Dispose();
    }

    private sealed class FileMessageStore : MessageStore
    {
        private readonly string _outputDir;

        public FileMessageStore(string outputDir)
        {
            _outputDir = outputDir;
        }

        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext context,
            IMessageTransaction transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var fileName = $"{timestamp}_{Guid.NewGuid():N}.eml";
            var path = Path.Combine(_outputDir, fileName);
            await File.WriteAllBytesAsync(path, buffer.ToArray(), cancellationToken);
            return SmtpResponse.Ok;
        }
    }
}
