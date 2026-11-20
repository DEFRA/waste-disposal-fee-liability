using System.ComponentModel.DataAnnotations;

namespace WasteDisposalFeeLiability.Organisations.Config;

public class EprCommonDataApiSettings
{
    public const string SectionName = "EprCommonDataApi";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
