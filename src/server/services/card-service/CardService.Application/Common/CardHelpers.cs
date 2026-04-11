using CardService.Domain.Entities;
using Shared.Contracts.Enums;

namespace CardService.Application.Common;

/// <summary>
/// Static helper class containing utility methods for credit card operations.
/// Provides card number validation, network detection, and masking.
/// </summary>
public static class CardHelpers
{
    /// <summary>
    /// Extracts only digits from a string (removes spaces, dashes, etc).
    /// Used to clean card numbers before processing.
    /// </summary>
    /// <param name="value">Raw card number string</param>
    /// <returns>String containing only digits</returns>
    public static string DigitsOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    /// <summary>
    /// Detects card network (Visa/Mastercard) from card number digits.
    /// Uses IIN (Issuer Identification Number) ranges:
    /// - Visa: starts with 4, length 13, 16, or 19
    /// - Mastercard: first 2 digits 51-55 OR first 4 digits 2221-2720
    /// </summary>
    /// <param name="digits">Card number digits only</param>
    /// <returns>CardNetwork enum (Visa, Mastercard, or Unknown)</returns>
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
            // [..2] -> take first 2 then validate -> [51, 52, 53, 54, 55]
            if (int.TryParse(digits[..2], out var first2) && first2 is >= 51 and <= 55)
            {
                return CardNetwork.Mastercard;
            }

            // [..4] -> take first 4 then range [2221 -> 2720]
            if (int.TryParse(digits[..4], out var first4) && first4 is >= 2221 and <= 2720)
            {
                return CardNetwork.Mastercard;
            }
        }

        return CardNetwork.Unknown;
    }

    /// <summary>
    /// Masks card number showing only last 4 digits.
    /// Format: "**** **** **** 1234"
    /// </summary>
    /// <param name="digits">Card number digits</param>
    /// <returns>Masked string</returns>
    public static string MaskCardNumber(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        return $"**** **** **** {last4}";
    }

    /// <summary>
    /// Validates billing cycle start day is between 1 and 31.
    /// </summary>
    /// <param name="day">Day of month</param>
    /// <returns>True if valid (1-31)</returns>
    public static bool IsValidBillingCycleStartDay(int day)
    {
        return day is >= 1 and <= 31;
    }
}
