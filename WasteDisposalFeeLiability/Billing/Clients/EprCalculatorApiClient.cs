using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using WasteDisposalFeeLiability.Billing.Config;

namespace WasteDisposalFeeLiability.Billing.Clients;

public sealed record BillingFileDownload(Stream Content, string ContentType, string FileName, string? ContentEncoding = null);

public interface IEprCalculatorApiClient
{
    /// <summary>
    /// Streams the billing JSON file for the given calculator run from the EPR Calculator API.
    /// </summary>
    /// <param name="runId">The calculator run ID to download the billing file for.</param>
    /// <param name="cancellationToken">The cancellation token for the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the download result.</returns>
    Task<BillingFileDownload> DownloadBillingFile(int runId, CancellationToken cancellationToken);
}

public class EprCalculatorApiClient(
    HttpClient httpClient,
    IOptions<EprCalculatorApiSettings> eprCalculatorApiSettings,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EprCalculatorApiClient> logger)
    : IEprCalculatorApiClient
{
    public async Task<BillingFileDownload> DownloadBillingFile(int runId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{eprCalculatorApiSettings.Value.BaseUrl.TrimEnd('/')}/v1/downloadBillingJson/{runId}");

        // Ask for gzip only when our caller accepts it, so the compressed bytes can be passed straight through.
        if (AcceptsGzip(httpContextAccessor.HttpContext?.Request.Headers.AcceptEncoding ?? StringValues.Empty))
        {
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
        }

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            logger.LogWarning("Billing file not found for calculator run {RunId}.", runId);
            throw new FileNotFoundException($"Billing file not found for calculator run {runId}.");
        }

        response.EnsureSuccessStatusCode();

        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"billing-{runId}.json";

        return new BillingFileDownload(
            new HttpResponseContentStream(response, contentStream),
            contentType,
            fileName,
            response.Content.Headers.ContentEncoding.SingleOrDefault());
    }

    private static bool AcceptsGzip(StringValues acceptEncoding) =>
        StringWithQualityHeaderValue.TryParseList(acceptEncoding, out var accepted) &&
        accepted.Any(a => a.Value.Equals("gzip", StringComparison.OrdinalIgnoreCase) && a.Quality is not 0);

    /// <summary>
    /// Wraps the response content stream so that disposing it also disposes the owning
    /// <see cref="HttpResponseMessage"/>, releasing the underlying connection once streaming completes.
    /// </summary>
    private sealed class HttpResponseContentStream(HttpResponseMessage response, Stream innerStream) : Stream
    {
        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => innerStream.CanSeek;

        public override bool CanWrite => innerStream.CanWrite;

        public override long Length => innerStream.Length;

        public override long Position
        {
            get => innerStream.Position;
            set => innerStream.Position = value;
        }

        public override void Flush() => innerStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            innerStream.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);

        public override void SetLength(long value) => innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                innerStream.Dispose();
                response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
