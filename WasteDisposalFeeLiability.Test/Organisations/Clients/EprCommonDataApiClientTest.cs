using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WasteDisposalFeeLiability.Organisations.Clients;
using WasteDisposalFeeLiability.Organisations.Config;
using WasteDisposalFeeLiability.Organisations.Models;

namespace WasteDisposalFeeLiability.Test.Organisations.Clients;

public class EprCommonDataApiClientTest
{
    [Fact]
    public async Task GetBillingOrganisations_returns_organisations_from_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var organisation = new OrganisationDetails
        {
            OrganisationId = "123",
            OrganisationName = "Acme",
            OrganisationTradingName = null,
            FinancialYear = "2024-25",
            ApprovedDate = new DateTime(2024, 1, 1),
            CompaniesHouseNumber = null,
            HomeNationCode = null,
            ServiceOfNoticeAddrLine1 = null,
            ServiceOfNoticeAddrLine2 = null,
            ServiceOfNoticeAddrCity = null,
            ServiceOfNoticeAddrCounty = null,
            ServiceOfNoticeAddrCountry = null,
            ServiceOfNoticeAddrPostcode = null,
            ServiceOfNoticeAddrPhoneNumber = null,
            SoleTraderFirstName = null,
            SoleTraderLastName = null,
            SoleTraderPhoneNumber = null,
            SoleTraderEmail = null,
            PrimaryContactPersonFirstName = null,
            PrimaryContactPersonLastName = null,
            PrimaryContactPersonPhoneNumber = null,
            PrimaryContactPersonEmail = null,
        };

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new OrganisationsDetailsResponse
            {
                OrganisationsDetails = [organisation],
            }),
        });

        var client = CreateClient(handler);

        var result = await client.GetBillingOrganisations(null, null, cancellationToken);

        Assert.Single(result);
        Assert.Equal("123", result.Single().OrganisationId);
    }

    [Fact]
    public async Task GetBillingOrganisations_includes_query_parameters_when_provided()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new OrganisationsDetailsResponse { OrganisationsDetails = [] }),
        });

        var client = CreateClient(handler);

        await client.GetBillingOrganisations(
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new FinancialYear("2024-25"),
            cancellationToken);

        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest.RequestUri!.Query;
        Assert.Contains("financialYear=2024-25", query);
        Assert.Contains("approvedAfter=", query);
    }

    [Fact]
    public async Task GetBillingOrganisations_throws_when_response_body_is_empty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetBillingOrganisations(null, null, cancellationToken));
    }

    private static EprCommonDataApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://common-data-api.test/"),
        };

        var settings = Options.Create(new EprCommonDataApiSettings { BaseUrl = "https://common-data-api.test" });

        return new EprCommonDataApiClient(httpClient, settings);
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
