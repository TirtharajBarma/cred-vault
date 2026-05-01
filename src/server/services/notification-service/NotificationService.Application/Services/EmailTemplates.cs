using System.Net;

namespace NotificationService.Application.Services;

public static class EmailTemplates
{
    private const string BrandColor   = "#8a5100"; // CredVault amber
    private const string BrandDark    = "#6b3f00";
    private const string BrandLight   = "#fef3e2";
    private const string TextPrimary  = "#1b1c1c";
    private const string TextMuted    = "#615e5c";
    private const string BorderColor  = "#e8ddd4";
    private const string SuccessColor = "#16a34a";
    private const string ErrorColor   = "#dc2626";
    private const string WarningColor = "#d97706";
    private const string BgPage       = "#f0edeb";
    private const string BgCard       = "#ffffff";

    private static DateTime ConvertToIst(DateTime dateTime)
    {
        try
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime.Kind == DateTimeKind.Unspecified ? dateTime : dateTime.ToUniversalTime(), istZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return dateTime.AddHours(5).AddMinutes(30);
        }
    }

    public static string BaseTemplate(string title, string subtitle, string content, string? footerNote = null)
    {
        var subtitleHtml = string.IsNullOrEmpty(subtitle) ? "" : $@"<p style=""margin: 4px 0 20px 0; color: {TextMuted}; font-size: 14px; line-height: 1.5;"">{EscapeHtml(subtitle)}</p>";
        var footerNoteHtml = string.IsNullOrEmpty(footerNote) ? "" : $@"<div style=""margin-top: 20px; padding: 14px 16px; background-color: {BrandLight}; border-radius: 10px; border-left: 3px solid {BrandColor};""><p style=""margin: 0; color: {BrandDark}; font-size: 13px; line-height: 1.5;"">{footerNote}</p></div>";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(title)}</title>
    <style>
        @media only screen and (max-width: 600px) {{
            .main-table {{ width: 100% !important; border-radius: 0 !important; }}
            .content-padding {{ padding: 20px !important; }}
        }}
    </style>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: {BgPage}; color: {TextPrimary}; -webkit-font-smoothing: antialiased;"">
    <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {BgPage}; padding: 28px 12px;"">
        <tr>
            <td align=""center"">
                <table class=""main-table"" width=""560"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {BgCard}; border-radius: 20px; overflow: hidden; box-shadow: 0 4px 24px rgba(0,0,0,0.07); border: 1px solid {BorderColor};"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, {BrandColor} 0%, {BrandDark} 100%); padding: 20px 28px; text-align: center;"">
                            <span style=""color: #FFFFFF; font-size: 20px; font-weight: 800; letter-spacing: -0.3px;"">💳 CredVault</span>
                            <p style=""margin: 4px 0 0 0; color: rgba(255,255,255,0.75); font-size: 11px; font-weight: 500; text-transform: uppercase; letter-spacing: 2px;"">Premium Credit Management</p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td class=""content-padding"" style=""padding: 28px 32px;"">
                            <h2 style=""margin: 0 0 4px 0; color: {TextPrimary}; font-size: 20px; font-weight: 700; letter-spacing: -0.3px;"">{EscapeHtml(title)}</h2>
                            {subtitleHtml}
                            {content}
                            {footerNoteHtml}
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: {BgPage}; padding: 16px 32px; border-top: 1px solid {BorderColor}; text-align: center;"">
                            <p style=""margin: 0 0 4px 0; color: {TextMuted}; font-size: 11px;"">This is an automated security notification from CredVault.</p>
                            <p style=""margin: 0; color: {TextMuted}; font-size: 11px; opacity: 0.7;"">© {DateTime.UtcNow:yyyy} CredVault Systems · Intelligent Credit Tracking &amp; Protection</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string UserWelcome(string fullName, string email)
    {
        var content = $@"
            <p style=""margin: 0 0 24px 0; font-size: 16px;"">Hello <strong>{EscapeHtml(fullName)}</strong>,</p>
            <p style=""margin: 0 0 32px 0; font-size: 16px; color: {TextPrimary};"">We're thrilled to have you join CredVault! Your account is now active, and you're ready to take control of your financial journey.</p>
            <div style=""background-color: #FFFFFF; border: 1px solid {BorderColor}; border-radius: 12px; padding: 24px; margin-bottom: 32px;"">
                <h3 style=""margin: 0 0 16px 0; color: {TextPrimary}; font-size: 16px; font-weight: 600;"">Account Overview</h3>
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                    <tr><td style=""padding: 8px 0; color: {TextMuted}; font-size: 14px;"">Registered Email</td><td style=""padding: 8px 0; color: {TextPrimary}; font-size: 14px; font-weight: 600; text-align: right;"">{EscapeHtml(email)}</td></tr>
                    <tr><td style=""padding: 8px 0; color: {TextMuted}; font-size: 14px;"">Member Since</td><td style=""padding: 8px 0; color: {TextPrimary}; font-size: 14px; font-weight: 600; text-align: right;"">{DateTime.UtcNow:MMMM yyyy}</td></tr>
                </table>
            </div>
            <h3 style=""margin: 0 0 16px 0; color: {TextPrimary}; font-size: 16px; font-weight: 600;"">Get Started in 3 Steps:</h3>
            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom: 16px;"">
                <tr><td width=""40"" valign=""top"" style=""padding-bottom: 16px;""><div style=""width: 24px; height: 24px; background-color: {BrandLight}; color: {BrandColor}; border-radius: 50%; text-align: center; line-height: 24px; font-size: 12px; font-weight: 700;"">1</div></td><td style=""padding-bottom: 16px;""><p style=""margin: 0; font-size: 15px; font-weight: 600;"">Link your cards</p><p style=""margin: 4px 0 0 0; font-size: 14px; color: {TextMuted};"">Add your credit cards to see all transactions in one place.</p></td></tr>
                <tr><td width=""40"" valign=""top"" style=""padding-bottom: 16px;""><div style=""width: 24px; height: 24px; background-color: {BrandLight}; color: {BrandColor}; border-radius: 50%; text-align: center; line-height: 24px; font-size: 12px; font-weight: 700;"">2</div></td><td style=""padding-bottom: 16px;""><p style=""margin: 0; font-size: 15px; font-weight: 600;"">Set up reminders</p><p style=""margin: 4px 0 0 0; font-size: 14px; color: {TextMuted};"">Never miss a due date with smart automated alerts.</p></td></tr>
                <tr><td width=""40"" valign=""top""><div style=""width: 24px; height: 24px; background-color: {BrandLight}; color: {BrandColor}; border-radius: 50%; text-align: center; line-height: 24px; font-size: 12px; font-weight: 700;"">3</div></td><td><p style=""margin: 0; font-size: 15px; font-weight: 600;"">Maximize rewards</p><p style=""margin: 4px 0 0 0; font-size: 14px; color: {TextMuted};"">Discover which card to use for every purchase to get top points.</p></td></tr>
            </table>";

        return BaseTemplate("Welcome to CredVault!", "Your financial management, elevated.", content, "Security Tip: We will never ask for your password via email. Always log in directly at credvault.app.");
    }

    public static string EmailVerificationOtp(string fullName, string otpCode, string purpose, DateTime expiresAt)
    {
        var expiresAtIst = ConvertToIst(expiresAt);
        var content = $@"
            <p style=""margin: 0 0 24px 0; font-size: 16px;"">Hello <strong>{EscapeHtml(fullName)}</strong>,</p>
            <p style=""margin: 0 0 32px 0; font-size: 16px; color: {TextPrimary};"">To complete your <strong>{EscapeHtml(purpose)}</strong>, please use the following secure verification code:</p>
            <div style=""text-align: center; margin: 40px 0;"">
                <div style=""display: inline-block; background-color: {BgPage}; border: 2px dashed {BrandColor}; padding: 24px 48px; border-radius: 16px;"">
                    <span style=""font-size: 36px; font-weight: 800; color: {BrandColor}; letter-spacing: 8px; font-family: 'Courier New', monospace;"">{EscapeHtml(otpCode)}</span>
                </div>
            </div>
            <div style=""background-color: #FFFBEB; border-radius: 12px; padding: 16px 20px; margin-bottom: 24px; border: 1px solid #FEF3C7;"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                    <tr><td width=""24"" valign=""top"" style=""padding-top: 2px;"">⚠️</td><td style=""padding-left: 12px;""><p style=""margin: 0; color: #92400E; font-size: 14px; line-height: 1.4;""><strong>Security Code Expiry:</strong><br>This code will expire on <strong>{expiresAtIst:MMM dd, yyyy}</strong> at <strong>{expiresAtIst:hh:mm tt} IST</strong>.</p></td></tr>
                </table>
            </div>
            <p style=""margin: 0; color: {TextMuted}; font-size: 14px; text-align: center;"">If you didn't request this code, you can safely ignore this email.</p>";

        return BaseTemplate("Verification Code", "Secure your account with this one-time password.", content);
    }

    public static string PasswordResetOtp(string fullName, string otpCode, DateTime expiresAt)
    {
        var expiresAtIst = ConvertToIst(expiresAt);
        var content = $@"
            <p style=""margin: 0 0 24px 0; font-size: 16px;"">Hello <strong>{EscapeHtml(fullName)}</strong>,</p>
            <p style=""margin: 0 0 32px 0; font-size: 16px; color: {TextPrimary};"">We received a request to reset your password. If this was you, use the code below to proceed:</p>
            <div style=""text-align: center; margin: 40px 0;"">
                <div style=""display: inline-block; background-color: #FEF2F2; border: 2px solid {ErrorColor}; padding: 24px 48px; border-radius: 16px;"">
                    <span style=""font-size: 36px; font-weight: 800; color: {ErrorColor}; letter-spacing: 8px; font-family: 'Courier New', monospace;"">{EscapeHtml(otpCode)}</span>
                </div>
            </div>
            <div style=""background-color: #FEF2F2; border-radius: 12px; padding: 20px; margin-bottom: 24px;"">
                <p style=""margin: 0 0 8px 0; color: {ErrorColor}; font-size: 14px; font-weight: 700;"">CRITICAL SECURITY NOTICE</p>
                <p style=""margin: 0; color: {ErrorColor}; font-size: 14px; opacity: 0.9;"">This code is valid until {expiresAtIst:hh:mm tt} IST. If you did not initiate this request, your account may be at risk. Please log in and change your password immediately.</p>
            </div>";

        return BaseTemplate("Password Reset Request", "Secure authorization for account recovery.", content);
    }

    public static string PaymentOtp(string fullName, decimal amount, string otpCode, DateTime expiresAt)
    {
        var expiresAtIst = ConvertToIst(expiresAt);
        var content = $@"
            <p style=""margin: 0 0 24px 0; font-size: 16px;"">Hello <strong>{EscapeHtml(fullName)}</strong>,</p>
            <p style=""margin: 0 0 24px 0; font-size: 16px;"">A payment request for <strong>₹{amount:N2}</strong> requires your authorization.</p>
            <div style=""background-color: {BgPage}; border-radius: 16px; padding: 32px; margin-bottom: 32px; text-align: center; border: 1px solid {BorderColor};"">
                <p style=""margin: 0 0 8px 0; color: {TextMuted}; font-size: 14px; text-transform: uppercase; letter-spacing: 1px;"">Transaction Amount</p>
                <p style=""margin: 0; color: {TextPrimary}; font-size: 36px; font-weight: 800;"">₹{amount:N2}</p>
            </div>
            <div style=""text-align: center; margin: 32px 0;"">
                <p style=""margin: 0 0 16px 0; color: {TextMuted}; font-size: 14px;"">Verification Code</p>
                <div style=""display: inline-block; background-color: {BrandColor}; padding: 20px 40px; border-radius: 12px; box-shadow: 0 4px 12px rgba(37, 99, 235, 0.2);"">
                    <span style=""font-size: 32px; font-weight: 800; color: #FFFFFF; letter-spacing: 6px; font-family: 'Courier New', monospace;"">{EscapeHtml(otpCode)}</span>
                </div>
                <p style=""margin: 16px 0 0 0; color: {TextMuted}; font-size: 13px;"">Expires at {expiresAtIst:hh:mm tt} IST</p>
            </div>";

        return BaseTemplate("Payment Authorization", "Final step to complete your transaction.", content, "Always verify the amount and merchant before entering your OTP.");
    }

    public static string PaymentCompleted(string fullName, decimal amount, decimal amountPaid, decimal rewardsRedeemed, string paymentId)
    {
        var rewardsHtml = rewardsRedeemed > 0 ? $@"<tr><td style=""padding: 8px 0; color: {TextMuted}; font-size: 14px;"">Rewards Applied</td><td style=""padding: 8px 0; color: {SuccessColor}; font-size: 14px; font-weight: 600; text-align: right;"">-₹{rewardsRedeemed:N2}</td></tr>" : "";
        var content = $@"
            <div style=""text-align: center; margin-bottom: 32px;"">
                <div style=""display: inline-block; width: 80px; height: 80px; background-color: #ECFDF5; border-radius: 50%; text-align: center; line-height: 80px; margin-bottom: 24px;""><span style=""font-size: 40px;"">✅</span></div>
                <h2 style=""margin: 0; color: {SuccessColor}; font-size: 26px; font-weight: 800;"">Payment Successful</h2>
                <p style=""margin: 8px 0 0 0; color: {TextMuted}; font-size: 15px;"">Transaction ID: {EscapeHtml(paymentId)}</p>
            </div>
            <div style=""background-color: #FFFFFF; border: 1px solid {BorderColor}; border-radius: 16px; padding: 24px; margin-bottom: 32px;"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                    <tr><td style=""padding: 12px 0; color: {TextMuted}; font-size: 14px;"">Amount Paid</td><td style=""padding: 12px 0; color: {TextPrimary}; font-size: 20px; font-weight: 700; text-align: right;"">₹{amountPaid:N2}</td></tr>
                    {rewardsHtml}
                    <tr style=""border-top: 1px solid {BorderColor};""><td style=""padding: 16px 0 0 0; color: {TextPrimary}; font-size: 16px; font-weight: 600;"">Total Bill Settled</td><td style=""padding: 16px 0 0 0; color: {TextPrimary}; font-size: 16px; font-weight: 600; text-align: right;"">₹{amount:N2}</td></tr>
                </table>
            </div>
            <p style=""margin: 0; color: {TextPrimary}; font-size: 15px; text-align: center;"">Thank you for choosing CredVault. Your payment has been updated across your dashboard.</p>";

        return BaseTemplate("Payment Confirmed", "Your transaction was processed successfully.", content, "A detailed receipt is available in your account dashboard under 'Transactions'.");
    }

    public static string PaymentFailed(string fullName, decimal amount, string reason, string paymentId)
    {
        var content = $@"
            <div style=""text-align: center; margin-bottom: 32px;"">
                <div style=""display: inline-block; width: 80px; height: 80px; background-color: #FEF2F2; border-radius: 50%; text-align: center; line-height: 80px; margin-bottom: 24px;""><span style=""font-size: 40px;"">❌</span></div>
                <h2 style=""margin: 0; color: {ErrorColor}; font-size: 26px; font-weight: 800;"">Payment Failed</h2>
                <p style=""margin: 8px 0 0 0; color: {TextMuted}; font-size: 15px;"">Reference: {EscapeHtml(paymentId)}</p>
            </div>
            <div style=""background-color: #FEF2F2; border-radius: 16px; padding: 24px; margin-bottom: 32px; border: 1px solid #FEE2E2;"">
                <p style=""margin: 0 0 8px 0; color: {ErrorColor}; font-size: 14px; font-weight: 700; text-transform: uppercase;"">Failure Reason</p>
                <p style=""margin: 0; color: {TextPrimary}; font-size: 16px; font-weight: 500;"">{EscapeHtml(reason)}</p>
            </div>
            <div style=""background-color: #FFFFFF; border: 1px solid {BorderColor}; border-radius: 16px; padding: 24px; margin-bottom: 32px;"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                    <tr><td style=""color: {TextMuted}; font-size: 14px;"">Attempted Amount</td><td style=""color: {TextPrimary}; font-size: 18px; font-weight: 700; text-align: right;"">₹{amount:N2}</td></tr>
                </table>
            </div>
            <h3 style=""margin: 0 0 16px 0; color: {TextPrimary}; font-size: 16px; font-weight: 600;"">What to do next?</h3>
            <ul style=""margin: 0; padding-left: 20px; color: {TextMuted}; font-size: 14px;"">
                <li style=""margin-bottom: 8px;"">Verify your card limit and available balance.</li>
                <li style=""margin-bottom: 8px;"">Check if your bank has blocked online transactions.</li>
                <li>Ensure your OTP was entered correctly.</li>
            </ul>";

        return BaseTemplate("Payment Unsuccessful", "We couldn't process your payment at this time.", content, "Your credit score is unaffected by failed attempts, but late payments may incur fees.");
    }

    public static string BillGenerated(string fullName, decimal amount, DateTime dueDate, string billId)
    {
        var daysUntilDue = (dueDate - DateTime.UtcNow).Days;
        var dueDateIst = ConvertToIst(dueDate);
        var content = $@"
            <p style=""margin: 0 0 24px 0; font-size: 16px;"">Hello <strong>{EscapeHtml(fullName)}</strong>,</p>
            <p style=""margin: 0 0 32px 0; font-size: 16px; color: {TextPrimary};"">Your latest credit card statement has been generated. Here's a summary of what's due:</p>
            <div style=""background: linear-gradient(135deg, {BrandColor} 0%, {BrandDark} 100%); border-radius: 20px; padding: 40px; margin-bottom: 32px; color: #FFFFFF; box-shadow: 0 20px 25px -5px rgba(37, 99, 235, 0.1);"">
                <p style=""margin: 0 0 8px 0; font-size: 13px; opacity: 0.8; text-transform: uppercase; letter-spacing: 1.5px;"">Total Amount Due</p>
                <p style=""margin: 0 0 24px 0; font-size: 42px; font-weight: 800;"">₹{amount:N2}</p>
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""border-top: 1px solid rgba(255,255,255,0.2); padding-top: 20px;"">
                    <tr><td><p style=""margin: 0; font-size: 12px; opacity: 0.8; text-transform: uppercase;"">Due Date</p><p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600;"">{dueDateIst:MMMM dd, yyyy}</p></td><td align=""right""><div style=""display: inline-block; background: rgba(255,255,255,0.2); padding: 6px 14px; border-radius: 20px; font-size: 12px; font-weight: 700;"">{daysUntilDue} DAYS LEFT</div></td></tr>
                </table>
            </div>
            <div style=""text-align: center; margin-bottom: 32px;"">
                <a href=""#"" style=""display: inline-block; background-color: {BrandColor}; color: #FFFFFF; padding: 16px 40px; border-radius: 12px; font-size: 16px; font-weight: 700; text-decoration: none;"">Pay Bill Now</a>
            </div>";

        return BaseTemplate("New Bill Statement", $"Your statement for {dueDateIst:MMMM} is ready.", content, "Early payments can help boost your credit score and increase your limit.");
    }

    public static string CardAdded(string fullName, string cardLast4, string cardHolderName, DateTime addedAt)
    {
        var addedAtIst = ConvertToIst(addedAt);
        var content = $@"
            <div style=""text-align: center; margin-bottom: 32px;"">
                <div style=""display: inline-block; width: 80px; height: 80px; background-color: {BrandLight}; border-radius: 50%; text-align: center; line-height: 80px; margin-bottom: 24px;""><span style=""font-size: 40px;"">🛡️</span></div>
                <h2 style=""margin: 0; color: {TextPrimary}; font-size: 24px; font-weight: 800;"">Security Alert: Card Linked</h2>
            </div>
            <p style=""margin: 0 0 32px 0; font-size: 16px; text-align: center;"">Hello <strong>{EscapeHtml(fullName)}</strong>, a new card was successfully added to your secure wallet.</p>
            <div style=""background: linear-gradient(135deg, #1E293B 0%, #0F172A 100%); border-radius: 20px; padding: 32px; margin-bottom: 32px; color: #FFFFFF;"">
                <p style=""margin: 0 0 24px 0; font-size: 11px; opacity: 0.6; text-transform: uppercase; letter-spacing: 2px;"">Linked Credit Card</p>
                <p style=""margin: 0 0 32px 0; font-size: 24px; font-weight: 600; letter-spacing: 4px;"">•••• •••• •••• {EscapeHtml(cardLast4)}</p>
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                    <tr>
                        <td><p style=""margin: 0; font-size: 10px; opacity: 0.6; text-transform: uppercase;"">Card Holder</p><p style=""margin: 4px 0 0 0; font-size: 14px; font-weight: 500;"">{EscapeHtml(cardHolderName)}</p></td>
                        <td align=""right""><p style=""margin: 0; font-size: 10px; opacity: 0.6; text-transform: uppercase;"">Added On</p><p style=""margin: 4px 0 0 0; font-size: 14px; font-weight: 500;"">{addedAtIst:MMM dd, yyyy}</p></td>
                    </tr>
                </table>
            </div>";

        return BaseTemplate("New Card Linked", "Your wallet has been updated.", content, "Security Notice: If you did not perform this action, please lock your account immediately through the app.");
    }

    public static string OtpVerificationFailed(string fullName, string reason)
    {
        var content = $@"
            <div style=""text-align: center; margin-bottom: 32px;"">
                <div style=""display: inline-block; width: 80px; height: 80px; background-color: #FFF7ED; border-radius: 50%; text-align: center; line-height: 80px; margin-bottom: 24px;""><span style=""font-size: 40px;"">🔒</span></div>
                <h2 style=""margin: 0; color: {WarningColor}; font-size: 24px; font-weight: 800;"">Verification Failed</h2>
            </div>
            <div style=""background-color: {BgPage}; border-radius: 16px; padding: 24px; margin-bottom: 32px; border: 1px solid {BorderColor};"">
                <p style=""margin: 0 0 8px 0; color: {TextMuted}; font-size: 14px; font-weight: 700; text-transform: uppercase;"">Error Detail</p>
                <p style=""margin: 0; color: {TextPrimary}; font-size: 16px; font-weight: 500;"">{EscapeHtml(reason)}</p>
            </div>
            <p style=""margin: 0 0 24px 0; font-size: 15px; color: {TextPrimary};"">For your security, we've blocked this verification attempt. This usually happens when the code has expired or was entered incorrectly too many times.</p>
            <div style=""background-color: {BrandLight}; border-radius: 12px; padding: 16px 20px;"">
                <p style=""margin: 0; color: {BrandDark}; font-size: 14px;""><strong>How to resolve:</strong> Please return to the app and request a fresh OTP code.</p>
            </div>";

        return BaseTemplate("Security Alert", "An unsuccessful verification attempt was detected.", content);
    }

    private static string EscapeHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return WebUtility.HtmlEncode(input);
    }
}
