namespace WasteDisposalFeeLiability.Organisations.Models;

public record OrganisationsDetailsResponse
{
    public required IEnumerable<OrganisationDetails> OrganisationsDetails { get; init; }
}
