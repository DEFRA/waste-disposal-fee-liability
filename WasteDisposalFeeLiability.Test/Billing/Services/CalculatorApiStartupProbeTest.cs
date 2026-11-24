using System.Net;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WasteDisposalFeeLiability.Billing.Clients;
using WasteDisposalFeeLiability.Billing.Config;
using WasteDisposalFeeLiability.Billing.Services;

namespace WasteDisposalFeeLiability.Test.Billing.Services;

public class CalculatorApiStartupProbeTest
{
    private readonly IEprCalculatorApiClient mockEprCalculatorApiClient = Substitute.For<IEprCalculatorApiClient>();
    private readonly FakeLogger<CalculatorApiStartupProbe> fakeLogger = new();
    private readonly CalculatorApiStartupProbe probe;

    public CalculatorApiStartupProbeTest()
    {
        probe = new CalculatorApiStartupProbe(
            mockEprCalculatorApiClient, fakeLogger, TimeSpan.FromSeconds(1), new HeaderPropagationValues());
    }

    [Fact]
    public async Task ProbeAsync_ReachesCalculatorApiThroughHeaderPropagationOutsideAnHttpRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestSent = false;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.Configure<EprCalculatorApiSettings>(settings => settings.BaseUrl = "https://calculator-api.test");
        services.AddHeaderPropagation(options => options.Headers.Add("x-cdp-request-id"));
        services
            .AddHttpClient<IEprCalculatorApiClient, EprCalculatorApiClient>()
            .AddHeaderPropagation()
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(() =>
            {
                requestSent = true;
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }));
        await using var serviceProvider = services.BuildServiceProvider();

        var realClientProbe = new CalculatorApiStartupProbe(
            serviceProvider.GetRequiredService<IEprCalculatorApiClient>(),
            fakeLogger,
            TimeSpan.Zero,
            serviceProvider.GetRequiredService<HeaderPropagationValues>());

        await realClientProbe.ProbeAsync(cancellationToken);

        Assert.True(requestSent);
        var message = Assert.Single(fakeLogger.LoggedMessages);
        Assert.Contains("correctly reported no billing file", message);
    }

    [Fact]
    public async Task ProbeAsync_WhenCalculatorApiReportsFileNotFound_LogsInformationAndDoesNotThrow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        mockEprCalculatorApiClient
            .DownloadBillingFile(-1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException());

        await probe.ProbeAsync(cancellationToken);

        Assert.Single(fakeLogger.LoggedLevels, LogLevel.Information);
    }

    [Fact]
    public async Task ExecuteAsync_WaitsForStartupDelayThenRunsProbe()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        mockEprCalculatorApiClient
            .DownloadBillingFile(-1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException());

        await probe.StartAsync(cancellationToken);
        await probe.ExecuteTask!;

        Assert.Single(fakeLogger.LoggedLevels, LogLevel.Information);
    }

    [Fact]
    public async Task ProbeAsync_WhenCallFails_LogsInformationAndDoesNotThrow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        mockEprCalculatorApiClient
            .DownloadBillingFile(-1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("unauthorized"));

        await probe.ProbeAsync(cancellationToken);

        Assert.Single(fakeLogger.LoggedLevels, LogLevel.Information);
    }

    [Fact]
    public async Task ProbeAsync_WhenCalculatorApiUnexpectedlyReturnsContent_DisposesStreamAndLogsInformation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var innerStream = new MemoryStream("{}"u8.ToArray());

        mockEprCalculatorApiClient
            .DownloadBillingFile(-1, Arg.Any<CancellationToken>())
            .Returns(new BillingFileDownload(innerStream, "application/json", "billing.json"));

        await probe.ProbeAsync(cancellationToken);

        Assert.Single(fakeLogger.LoggedLevels, LogLevel.Information);
        Assert.False(innerStream.CanRead, "the response stream should have been disposed");
    }

    [Fact]
    public async Task ProbeAsync_PassesProbeRunIdAndCancellationTokenThrough()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        mockEprCalculatorApiClient
            .DownloadBillingFile(-1, cancellationTokenSource.Token)
            .ThrowsAsync(new FileNotFoundException());

        await probe.ProbeAsync(cancellationTokenSource.Token);

        await mockEprCalculatorApiClient.Received(1).DownloadBillingFile(-1, cancellationTokenSource.Token);
    }

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<LogLevel> LoggedLevels { get; } = [];

        public List<string> LoggedMessages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LoggedLevels.Add(logLevel);
            LoggedMessages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
