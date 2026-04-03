using CardService.Domain.Entities;
using Shared.Contracts.Enums;

namespace CardService.Application.Common;

public static class CardHelpers
{
    public static string DigitsOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    public static CardNetwork DetectNetwork(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits) || digits.Any(ch => ch < '0' || ch > '9'))
        {
            return CardNetwork.Unknown;
        }

        if (digits[0] == '4' && (digits.Length == 13 || digits.Length == 16 || digits.Length == 19))
        {
            return CardNetwork.Visa;
        }

        if (digits.Length == 16)
        {
            if (int.TryParse(digits[..2], out var first2) && first2 is >= 51 and <= 55)
            {
                return CardNetwork.Mastercard;
            }

            if (int.TryParse(digits[..4], out var first4) && first4 is >= 2221 and <= 2720)
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
        return $"**** **** **** {last4}";
    }

    public static bool IsValidBillingCycleStartDay(int day)
    {
        return day is >= 1 and <= 31;
    }
}
