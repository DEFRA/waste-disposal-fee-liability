using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WasteDisposalFeeLiability.Billing.Clients;
using WasteDisposalFeeLiability.Billing.Config;

namespace WasteDisposalFeeLiability.Test.Billing.Clients;

public class EprCalculatorApiClientTest
{
    private readonly ILogger<EprCalculatorApiClient> logger = Substitute.For<ILogger<EprCalculatorApiClient>>();

    [Fact]
    public async Task DownloadBillingFile_returns_streamed_content_with_headers_from_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const int runId = 42;
        var expectedBytes = "{\"field\":\"value\"}"u8.ToArray();

        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "billing-42.json",
            };
            return response;
        });

        var client = CreateClient(handler);

        var result = await client.DownloadBillingFile(runId, cancellationToken);

        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("billing-42.json", result.FileName);
        Assert.Null(result.ContentEncoding);

        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync(cancellationToken);
        Assert.Equal("{\"field\":\"value\"}", content);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal(
            new Uri($"https://calculator-api.test/v1/downloadBillingJson/{runId}"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task DownloadBillingFile_throws_file_not_found_when_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => client.DownloadBillingFile(99, cancellationToken));
    }

    [Fact]
    public async Task DownloadBillingFile_throws_http_request_exception_on_server_error()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DownloadBillingFile(1, cancellationToken));
    }

    [Fact]
    public async Task DownloadBillingFile_falls_back_to_default_file_name_when_no_content_disposition()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const int runId = 7;

        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("{}"u8.ToArray()),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        var client = CreateClient(handler);

        var result = await client.DownloadBillingFile(runId, cancellationToken);

        Assert.Equal($"billing-{runId}.json", result.FileName);
    }

    [Fact]
    public async Task DownloadBillingFile_disposing_stream_disposes_underlying_content_stream()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var innerStream = new MemoryStream("hello world"u8.ToArray());

        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(innerStream),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        var client = CreateClient(handler);

        var result = await client.DownloadBillingFile(1, cancellationToken);
        await result.Content.DisposeAsync();

        Assert.False(innerStream.CanRead);
    }

    [Fact]
    public async Task DownloadBillingFile_requests_gzip_and_passes_it_through_when_caller_accepts_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("compressed"u8.ToArray()),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response.Content.Headers.ContentEncoding.Add("gzip");
            return response;
        });

        var client = CreateClient(handler, callerAcceptEncoding: "gzip");

        var result = await client.DownloadBillingFile(1, cancellationToken);

        Assert.Equal("gzip", result.ContentEncoding);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains(
            handler.LastRequest.Headers.AcceptEncoding,
            v => v.Value.Equals("gzip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DownloadBillingFile_does_not_request_gzip_when_caller_does_not_accept_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("{}"u8.ToArray()),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        var client = CreateClient(handler, callerAcceptEncoding: null);

        await client.DownloadBillingFile(1, cancellationToken);

        Assert.NotNull(handler.LastRequest);
        Assert.Empty(handler.LastRequest.Headers.AcceptEncoding);
    }

    private EprCalculatorApiClient CreateClient(HttpMessageHandler handler, string? callerAcceptEncoding = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://calculator-api.test/"),
        };

        var settings = Options.Create(new EprCalculatorApiSettings { BaseUrl = "https://calculator-api.test" });

        var httpContext = new DefaultHttpContext();
        if (callerAcceptEncoding is not null)
        {
            httpContext.Request.Headers.AcceptEncoding = callerAcceptEncoding;
        }

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        return new EprCalculatorApiClient(httpClient, settings, httpContextAccessor, logger);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
