using System.ComponentModel.DataAnnotations;

namespace WasteDisposalFeeLiability.Billing.Config;

public class EprCalculatorApiSettings
{
    public const string SectionName = "EprCalculatorApi";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
