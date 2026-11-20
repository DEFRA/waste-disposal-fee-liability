namespace WasteDisposalFeeLiability.Organisations.Models;

public readonly record struct FinancialYear(string Value)
{
    public override string ToString() => Value;

    public static bool TryParse(string? value, out FinancialYear result)
    {
        if (value != null && IsValidFormat(value))
        {
            result = new FinancialYear(value);
            return true;
        }

        result = default;
        return false;
    }

    private static bool IsValidFormat(string value) =>
        value.Length == 7 &&
        value[4] == '-' &&
        int.TryParse(value[..4], out var start) &&
        int.TryParse(value[5..], out var end) &&
        start > 2000 &&
        end == (start + 1) % 100;
}
