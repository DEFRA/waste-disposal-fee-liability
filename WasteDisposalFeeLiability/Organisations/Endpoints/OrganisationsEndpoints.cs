using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using WasteDisposalFeeLiability.Organisations.Clients;
using WasteDisposalFeeLiability.Organisations.Models;

namespace WasteDisposalFeeLiability.Organisations.Endpoints;

public static class OrganisationsEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/organisations-details", GetOrganisationsDetails);

        return app;
    }

    private static async Task<Results<Ok<OrganisationsDetailsResponse>, BadRequest<ApiError>>> GetOrganisationsDetails(
        string? approvedAfter,
        string? financialYear,
        IEprCommonDataApiClient eprCommonDataApiClient,
        CancellationToken cancellationToken)
    {
        var (parsedApprovedAfter, approvedAfterError) = ParseOptional(approvedAfter, nameof(approvedAfter),
            (string s, out DateTimeOffset r) => DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out r));
        var (parsedFinancialYear, financialYearError) = ParseOptional<FinancialYear>(financialYear, nameof(financialYear), FinancialYear.TryParse);

        var errors = new[] { approvedAfterError, financialYearError }.OfType<string>().ToList();

        if (errors.Count > 0)
        {
            return TypedResults.BadRequest(new ApiError
            {
                Error = "Bad Request",
                Message = $"The request was malformed or invalid - {string.Join(", ", errors)}",
                StatusCode = StatusCodes.Status400BadRequest,
                ErrorCode = "invalid_request",
                Description = "The request did not conform to the required format."
            });
        }

        var organisationsDetails = await eprCommonDataApiClient.GetBillingOrganisations(
            parsedApprovedAfter,
            parsedFinancialYear,
            cancellationToken);

        return TypedResults.Ok(new OrganisationsDetailsResponse
        {
            OrganisationsDetails = organisationsDetails,
        });
    }

    private delegate bool TryParser<T>(string value, out T result);

    private static (T? Value, string? Error) ParseOptional<T>(string? value, string name, TryParser<T> tryParse)
        where T : struct
    {
        if (string.IsNullOrEmpty(value))
            return (null, null);

        if (tryParse(value, out var result))
            return (result, null);

        return (null, $"The value '{name}' is invalid.");
    }
}
