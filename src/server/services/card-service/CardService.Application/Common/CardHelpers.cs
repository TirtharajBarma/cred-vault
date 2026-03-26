using CardService.Domain.Entities;

namespace CardService.Application.Common;

public static class CardHelpers
{
    public static bool IsValidExpiry(int expMonth, int expYear)
    {
        if (expMonth is < 1 or > 12)
        {
            return false;
        }

        if (expYear < 2000 || expYear > 2100)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var expiry = new DateTime(expYear, expMonth, 1).AddMonths(1).AddDays(-1);
        return expiry >= new DateTime(now.Year, now.Month, 1);
    }

    public static string DigitsOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    // Luhn check (basic sanity for card numbers).
    public static bool PassesLuhn(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits) || digits.Any(ch => ch < '0' || ch > '9'))
        {
            return false;
        }

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    public static bool IsValidBillingCycleStartDay(int day)
    {
        return day is >= 1 and <= 31;
    }

    public static CardNetwork DetectNetwork(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits) || digits.Any(ch => ch < '0' || ch > '9'))
        {
            return CardNetwork.Unknown;
        }

        // Visa: starts with 4, length 13/16/19
        if (digits[0] == '4' && (digits.Length == 13 || digits.Length == 16 || digits.Length == 19))
        {
            return CardNetwork.Visa;
        }

        // Mastercard: length 16, IIN 51-55 or 2221-2720
        if (digits.Length == 16)
        {
            if (digits.Length >= 2 && int.TryParse(digits[..2], out var first2) && first2 is >= 51 and <= 55)
            {
                return CardNetwork.Mastercard;
            }

            if (digits.Length >= 4 && int.TryParse(digits[..4], out var first4) && first4 is >= 2221 and <= 2720)
            {
                return CardNetwork.Mastercard;
            }
        }

        return CardNetwork.Unknown;
    }

    public static string MaskCardNumber(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;

        // Store a consistent masked representation (never store PAN).
        return $"**** **** **** {last4}";
    }
}
