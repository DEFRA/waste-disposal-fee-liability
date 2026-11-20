using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WasteDisposalFeeLiability.Billing.Clients;

namespace WasteDisposalFeeLiability.Test.Billing.Endpoints;

public class BillingEndpointsTest
{
    [Fact]
    public async Task Get_returns_billing_file_when_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var content = "{\"field\":\"value\"}"u8.ToArray();
        factory.MockEprCalculatorApiClient
            .DownloadBillingFile(42, Arg.Any<CancellationToken>())
            .Returns(new BillingFileDownload(new MemoryStream(content), "application/json", "42billing.json"));

        var response = await client.GetAsync("/api/Billing/billingDetails?calculatorRunId=42", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_passes_through_content_encoding_when_present()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var content = "compressed"u8.ToArray();
        factory.MockEprCalculatorApiClient
            .DownloadBillingFile(42, Arg.Any<CancellationToken>())
            .Returns(new BillingFileDownload(new MemoryStream(content), "application/json", "42billing.json", "gzip"));

        var response = await client.GetAsync("/api/Billing/billingDetails?calculatorRunId=42", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Get_returns_bad_request_for_non_positive_run_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/Billing/billingDetails?calculatorRunId=0", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Run ID must be greater than 0.", problem.Detail);
    }

    [Fact]
    public async Task Get_returns_not_found_when_billing_file_is_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        factory.MockEprCalculatorApiClient
            .DownloadBillingFile(42, Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException());

        var response = await client.GetAsync("/api/Billing/billingDetails?calculatorRunId=42", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public readonly IEprCalculatorApiClient MockEprCalculatorApiClient = Substitute.For<IEprCalculatorApiClient>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEprCalculatorApiClient>();
                services.AddSingleton(MockEprCalculatorApiClient);
            });
        }
    }
}
