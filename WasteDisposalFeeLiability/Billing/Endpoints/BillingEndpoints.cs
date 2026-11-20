using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WasteDisposalFeeLiability.Billing.Clients;

namespace WasteDisposalFeeLiability.Billing.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/Billing/billingDetails", GetBillingDetails);

        return app;
    }

    private static async Task<Results<FileStreamHttpResult, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> GetBillingDetails(
        int calculatorRunId,
        HttpContext httpContext,
        IEprCalculatorApiClient eprCalculatorApiClient,
        CancellationToken cancellationToken)
    {
        if (calculatorRunId <= 0)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = "Run ID must be greater than 0.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var billingData = await eprCalculatorApiClient.DownloadBillingFile(calculatorRunId, cancellationToken);

            if (billingData.ContentEncoding is not null)
            {
                httpContext.Response.Headers.ContentEncoding = billingData.ContentEncoding;
                httpContext.Response.Headers.Vary = HeaderNames.AcceptEncoding;
            }

            return TypedResults.Stream(billingData.Content, billingData.ContentType);
        }
        catch (FileNotFoundException)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "The requested resource could not be found.",
                Detail = "The resource you requested does not exist.",
                Status = StatusCodes.Status404NotFound,
            });
        }
    }
}
