using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using WasteDisposalFeeLiability.Organisations.Clients;
using WasteDisposalFeeLiability.Organisations.Models;

namespace WasteDisposalFeeLiability.Test.Organisations.Endpoints;

public class OrganisationsEndpointsTest
{
    [Fact]
    public async Task Get_returns_organisations_from_common_data_api()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var organisations = new List<OrganisationDetails>
        {
            new()
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
            },
        };

        factory.MockEprCommonDataApiClient
            .GetBillingOrganisations(Arg.Any<DateTimeOffset?>(), Arg.Any<FinancialYear?>(), Arg.Any<CancellationToken>())
            .Returns(organisations.AsReadOnly());

        var response = await client.GetAsync("/api/v1/organisations-details", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OrganisationsDetailsResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Single(body.OrganisationsDetails);
    }

    [Fact]
    public async Task Get_passes_parsed_query_parameters_through_to_client()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        factory.MockEprCommonDataApiClient
            .GetBillingOrganisations(Arg.Any<DateTimeOffset?>(), Arg.Any<FinancialYear?>(), Arg.Any<CancellationToken>())
            .Returns(new List<OrganisationDetails>().AsReadOnly());

        var response = await client.GetAsync(
            "/api/v1/organisations-details?approvedAfter=2024-01-01T00:00:00Z&financialYear=2024-25",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await factory.MockEprCommonDataApiClient.Received().GetBillingOrganisations(
            Arg.Is<DateTimeOffset?>(d => d == new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Arg.Is<FinancialYear?>(y => y == new FinancialYear("2024-25")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_returns_bad_request_for_invalid_financial_year()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/organisations-details?financialYear=not-a-year",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_bad_request_for_invalid_approved_after()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/organisations-details?approvedAfter=not-a-date",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public readonly IEprCommonDataApiClient MockEprCommonDataApiClient = Substitute.For<IEprCommonDataApiClient>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEprCommonDataApiClient>();
                services.AddSingleton(MockEprCommonDataApiClient);
            });
        }
    }
}
