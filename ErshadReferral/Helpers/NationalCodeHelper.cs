namespace ErshadReferral.Helpers;

public static class NationalCodeHelper
{
    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var s = code.Trim();
        if (s.Length != 10) return false;
        if (!s.All(char.IsDigit)) return false;
        if (s.Distinct().Count() == 1) return false;

        var digits = s.Select(c => c - '0').ToArray();
        int sum = 0;
        for (int i = 0; i < 9; i++) sum += digits[i] * (10 - i);
        int remainder = sum % 11;
        int control = digits[9];
        if (remainder < 2) return control == remainder;
        return control == (11 - remainder);
    }
}
