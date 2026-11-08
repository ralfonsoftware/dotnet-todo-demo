using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;

namespace TodoApi.Tests.Telemetry;

/// <summary>
/// Exports to a real in-process OTLP/HTTP receiver, proving that every signal
/// reaches its own <c>/v1/{signal}</c> path when the protocol is
/// <c>http/protobuf</c>.
/// </summary>
public sealed class OtlpExportTests
{
    [Fact]
    public async Task HttpProtobuf_ExportsEachSignalToItsSignalPath()
    {
        using var receiver = new OtlpHttpReceiver();
        var cancellationToken = TestContext.Current.CancellationToken;

        var factory = new HttpProtobufFactory(receiver.BaseUrl);
        try
        {
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "/todos", new { title = "Export me" }, cancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
            factory.Services.GetRequiredService<MeterProvider>().ForceFlush();
        }
        finally
        {
            // Disposing the host flushes the log exporter.
            await factory.DisposeAsync();
        }

        await receiver.WaitForPathsAsync(
            ["/v1/traces", "/v1/metrics", "/v1/logs"], TimeSpan.FromSeconds(10));

        receiver.Paths.ShouldContain("/v1/traces");
        receiver.Paths.ShouldContain("/v1/metrics");
        receiver.Paths.ShouldContain("/v1/logs");
    }

    private sealed class HttpProtobufFactory(Uri endpoint) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("OpenTelemetry:Enabled", "true");
            builder.UseSetting("OpenTelemetry:ExporterEnabled", "true");
            builder.UseSetting("OpenTelemetry:Protocol", "http/protobuf");
            builder.UseSetting("OpenTelemetry:Endpoint", endpoint.ToString());
        }
    }

    /// <summary>Accepts OTLP/HTTP posts on a free loopback port and records each request path.</summary>
    private sealed class OtlpHttpReceiver : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly ConcurrentQueue<string> _paths = new();
        private readonly Task _loop;

        public OtlpHttpReceiver()
        {
            var port = FreePort();
            BaseUrl = new Uri($"http://127.0.0.1:{port}");
            _listener.Prefixes.Add($"{BaseUrl}");
            _listener.Start();
            _loop = Task.Run(AcceptLoopAsync);
        }

        public Uri BaseUrl { get; }

        public IReadOnlyList<string> Paths => _paths.ToArray();

        public async Task WaitForPathsAsync(IReadOnlyList<string> expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !expected.All(Paths.Contains))
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                _paths.Enqueue(context.Request.Url?.AbsolutePath ?? string.Empty);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.Close();
            }
        }

        private static int FreePort()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }

        public void Dispose()
        {
            // Close alone: Stop followed by Close makes the managed listener
            // re-bind the port on teardown, which fails if it was reused.
            _listener.Close();
            _loop.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
