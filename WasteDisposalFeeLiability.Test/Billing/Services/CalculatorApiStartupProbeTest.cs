using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WasteDisposalFeeLiability.Billing.Clients;
using WasteDisposalFeeLiability.Billing.Services;

namespace WasteDisposalFeeLiability.Test.Billing.Services;

public class CalculatorApiStartupProbeTest
{
    private readonly IEprCalculatorApiClient mockEprCalculatorApiClient = Substitute.For<IEprCalculatorApiClient>();
    private readonly FakeLogger<CalculatorApiStartupProbe> fakeLogger = new();
    private readonly CalculatorApiStartupProbe probe;

    public CalculatorApiStartupProbeTest()
    {
        probe = new CalculatorApiStartupProbe(mockEprCalculatorApiClient, fakeLogger, TimeSpan.FromSeconds(1));
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

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<LogLevel> LoggedLevels { get; } = [];

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
