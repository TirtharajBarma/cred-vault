namespace NotificationService.Application.Services;

public static class EmailTemplates
{
    private const string BrandColor = "#2563EB";
    private const string BrandColorLight = "#EFF6FF";
    private const string TextColor = "#1F2937";
    private const string MutedColor = "#6B7280";
    private const string BorderColor = "#E5E7EB";
    private const string SuccessColor = "#059669";
    private const string WarningColor = "#D97706";
    private const string ErrorColor = "#DC2626";

    private static DateTime ConvertToIst(DateTime dateTime)
    {
        try
        {
            // TimeZoneInfo for India Standard Time
            var istZone = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime.Kind == DateTimeKind.Unspecified ? dateTime : dateTime.ToUniversalTime(), istZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback
            return dateTime.AddHours(5).AddMinutes(30);
        }
    }

    public static string BaseTemplate(string title, string subtitle, string content, string? footerNote = null)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(title)}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #F3F4F6; line-height: 1.5;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #F3F4F6; padding: 20px 10px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #FFFFFF; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, {BrandColor} 0%, #1D4ED8 100%); padding: 24px 32px; text-align: center;"">
                            <h1 style=""margin: 0; color: #FFFFFF; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;"">
                                💳 CredVault
                            </h1>
                            <p style=""margin: 4px 0 0 0; color: rgba(255,255,255,0.9); font-size: 14px;"">
                                Your Trusted Credit Card Companion
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 32px;"">
                            <h2 style=""margin: 0 0 8px 0; color: {TextColor}; font-size: 20px; font-weight: 600;"">
                                {EscapeHtml(title)}
                            </h2>
                            {(string.IsNullOrEmpty(subtitle) ? "" : $@"<p style=""margin: 0 0 20px 0; color: {MutedColor}; font-size: 15px;"">
                                {EscapeHtml(subtitle)}
                            </p>")}
                            
                            {content}
                            
                            {(string.IsNullOrEmpty(footerNote) ? "" : $@"<div style=""margin-top: 24px; padding: 12px 16px; background-color: #F9FAFB; border-radius: 8px; border-left: 4px solid {BrandColor};"">
                                <p style=""margin: 0; color: {MutedColor}; font-size: 13px;"">
                                    {footerNote}
                                </p>
                            </div>")}
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #F9FAFB; padding: 16px 32px; border-top: 1px solid {BorderColor};"">
                            <p style=""margin: 0 0 8px 0; color: {MutedColor}; font-size: 12px; text-align: center;"">
                                This is an automated message from CredVault. Please do not reply to this email.
                            </p>
                            <p style=""margin: 0; color: {MutedColor}; font-size: 12px; text-align: center;"">
                                © {DateTime.UtcNow:yyyy} CredVault. All rights reserved.<br>
                                Securing your financial journey.
                            </p>
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
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Welcome to CredVault! 🎉 Your account has been successfully created.
                            </p>
                            
                            <div style=""background-color: {BrandColorLight}; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <h3 style=""margin: 0 0 12px 0; color: {TextColor}; font-size: 16px;"">Your Account Details</h3>
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding: 4px 0; color: {MutedColor}; font-size: 14px;"">Email</td>
                                        <td style=""padding: 4px 0; color: {TextColor}; font-size: 14px; font-weight: 600; text-align: right;"">{EscapeHtml(email)}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <p style=""margin: 0 0 12px 0; color: {TextColor}; font-size: 15px;"">
                                <strong>Next Steps:</strong>
                            </p>
                            <ul style=""margin: 0 0 20px 0; padding-left: 20px; color: {TextColor}; font-size: 15px;"">
                                <li style=""margin-bottom: 4px;"">Verify your email address using the OTP sent to your inbox</li>
                                <li style=""margin-bottom: 4px;"">Add your credit cards to start tracking</li>
                                <li style=""margin-bottom: 4px;"">Set up bill payment reminders</li>
                            </ul>";
        return BaseTemplate(
            "Welcome to CredVault!",
            "Your account is ready",
            content,
            "If you did not create this account, please contact our support team immediately."
        );
    }

    public static string EmailVerificationOtp(string fullName, string otpCode, string purpose, DateTime expiresAt)
    {
        var expiresAtIst = ConvertToIst(expiresAt);
        var content = $@"
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Your verification code is:
                            </p>
                            
                            <div style=""text-align: center; margin: 24px 0;"">
                                <div style=""display: inline-block; background: linear-gradient(135deg, {BrandColor} 0%, #1D4ED8 100%); padding: 16px 32px; border-radius: 8px;"">
                                    <span style=""font-size: 28px; font-weight: 700; color: #FFFFFF; letter-spacing: 6px; font-family: 'Courier New', monospace;"">{EscapeHtml(otpCode)}</span>
                                </div>
                            </div>
                            
                            <div style=""background-color: #FEF3C7; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px;"">
                                <p style=""margin: 0; color: {WarningColor}; font-size: 13px;"">
                                    <strong>Purpose:</strong> {EscapeHtml(purpose)}<br>
                                    <strong>Expires:</strong> {expiresAtIst:MMMM dd, yyyy 'at' hh:mm tt} IST
                                </p>
                            </div>
                            
                            <p style=""margin: 0; color: {MutedColor}; font-size: 13px;"">
                                This code is valid for a single use and will expire at the time specified above.
                            </p>";
        return BaseTemplate(
            "Email Verification Code",
            "Please use this code to verify your email",
            content
        );
    }

    public static string PasswordResetOtp(string fullName, string otpCode, DateTime expiresAt)
    {
        var expiresAtIst = ConvertToIst(expiresAt);
        var content = $@"
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                We received a request to reset your password. Use the code below:
                            </p>
                            
                            <div style=""text-align: center; margin: 24px 0;"">
                                <div style=""display: inline-block; background: linear-gradient(135deg, {ErrorColor} 0%, #B91C1C 100%); padding: 16px 32px; border-radius: 8px;"">
                                    <span style=""font-size: 28px; font-weight: 700; color: #FFFFFF; letter-spacing: 6px; font-family: 'Courier New', monospace;"">{EscapeHtml(otpCode)}</span>
                                </div>
                            </div>
                            
                            <div style=""background-color: #FEE2E2; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px;"">
                                <p style=""margin: 0; color: {ErrorColor}; font-size: 13px;"">
                                    <strong>Expires:</strong> {expiresAtIst:MMMM dd, yyyy 'at' hh:mm tt} IST
                                </p>
                            </div>
                            
                            <div style=""background-color: #FEF3C7; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px;"">
                                <p style=""margin: 0; color: {WarningColor}; font-size: 13px;"">
                                    <strong>⚠️ Security Notice:</strong> If you didn't request this, please ignore this email. Your password will remain unchanged.
                                </p>
                            </div>";
        return BaseTemplate(
            "Password Reset Request",
            "Someone requested a password reset for your account",
            content
        );
    }

    public static string PaymentOtp(string fullName, decimal amount, string otpCode, DateTime expiresAt)
    {
        var expiresAtIst = ConvertToIst(expiresAt);
        var content = $@"
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Please enter the following code to confirm your payment:
                            </p>
                            
                            <div style=""background-color: {BrandColorLight}; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding: 4px 0; color: {MutedColor}; font-size: 14px;"">Amount</td>
                                        <td style=""padding: 4px 0; color: {TextColor}; font-size: 20px; font-weight: 700; text-align: right;"">₹{amount:N2}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <div style=""text-align: center; margin: 24px 0;"">
                                <div style=""display: inline-block; background: linear-gradient(135deg, {BrandColor} 0%, #1D4ED8 100%); padding: 16px 32px; border-radius: 8px;"">
                                    <span style=""font-size: 28px; font-weight: 700; color: #FFFFFF; letter-spacing: 6px; font-family: 'Courier New', monospace;"">{EscapeHtml(otpCode)}</span>
                                </div>
                            </div>
                            
                            <div style=""background-color: #FEF3C7; border-radius: 8px; padding: 12px 16px;"">
                                <p style=""margin: 0; color: {WarningColor}; font-size: 13px;"">
                                    <strong>Expires:</strong> {expiresAtIst:MMMM dd, yyyy 'at' hh:mm tt} IST
                                </p>
                            </div>";
        return BaseTemplate(
            "Payment Verification Required",
            $"Confirm payment of ₹{amount:N2}",
            content
        );
    }

    public static string PaymentCompleted(string fullName, decimal amount, decimal amountPaid, decimal rewardsRedeemed, string paymentId)
    {
        var content = $@"
                            <div style=""text-align: center; margin: 16px 0;"">
                                <div style=""display: inline-block; width: 64px; height: 64px; background-color: #D1FAE5; border-radius: 50%; text-align: center; line-height: 64px; margin-bottom: 16px;"">
                                    <span style=""font-size: 32px;"">✅</span>
                                </div>
                                <h2 style=""margin: 0; color: {SuccessColor}; font-size: 24px;"">Payment Successful!</h2>
                            </div>
                            
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px; text-align: center;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>, your payment has been processed successfully.
                            </p>
                            
                            <div style=""background-color: #D1FAE5; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding: 4px 0; color: {SuccessColor}; font-size: 13px; font-weight: 600;"">Payment ID</td>
                                        <td style=""padding: 4px 0; color: {TextColor}; font-size: 13px; text-align: right; font-family: monospace;"">{EscapeHtml(paymentId)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 4px 0; color: {SuccessColor}; font-size: 13px; font-weight: 600;"">Amount Paid</td>
                                        <td style=""padding: 4px 0; color: {TextColor}; font-size: 18px; font-weight: 700; text-align: right;"">₹{amountPaid:N2}</td>
                                    </tr>
                                    {(rewardsRedeemed > 0 ? $@"<tr>
                                        <td style=""padding: 4px 0; color: {MutedColor}; font-size: 13px;"">Rewards Used</td>
                                        <td style=""padding: 4px 0; color: {MutedColor}; font-size: 13px; text-align: right;"">₹{rewardsRedeemed:N2}</td>
                                    </tr>" : "")}
                                    <tr style=""border-top: 1px solid {SuccessColor};"">
                                        <td style=""padding: 8px 0 0 0; color: {TextColor}; font-size: 15px; font-weight: 600;"">Total Bill Amount</td>
                                        <td style=""padding: 8px 0 0 0; color: {TextColor}; font-size: 15px; font-weight: 600; text-align: right;"">₹{amount:N2}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <p style=""margin: 0; color: {MutedColor}; font-size: 13px; text-align: center;"">
                                Thank you for your payment! A receipt has been sent to your registered email.
                            </p>";
        return BaseTemplate(
            "Payment Confirmed",
            $"Payment of ₹{amountPaid:N2} completed",
            content,
            "Keep this receipt for your records. For any queries, contact our support team."
        );
    }

    public static string PaymentFailed(string fullName, decimal amount, string reason, string paymentId)
    {
        var content = $@"
                            <div style=""text-align: center; margin: 16px 0;"">
                                <div style=""display: inline-block; width: 64px; height: 64px; background-color: #FEE2E2; border-radius: 50%; text-align: center; line-height: 64px; margin-bottom: 16px;"">
                                    <span style=""font-size: 32px;"">❌</span>
                                </div>
                                <h2 style=""margin: 0; color: {ErrorColor}; font-size: 24px;"">Payment Failed</h2>
                            </div>
                            
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px; text-align: center;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>, unfortunately your payment could not be processed.
                            </p>
                            
                            <div style=""background-color: #FEE2E2; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding: 4px 0; color: {MutedColor}; font-size: 13px;"">Payment ID</td>
                                        <td style=""padding: 4px 0; color: {TextColor}; font-size: 13px; text-align: right; font-family: monospace;"">{EscapeHtml(paymentId)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 4px 0; color: {MutedColor}; font-size: 13px;"">Amount</td>
                                        <td style=""padding: 4px 0; color: {TextColor}; font-size: 18px; font-weight: 700; text-align: right;"">₹{amount:N2}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0 0 0; color: {ErrorColor}; font-size: 13px; font-weight: 600;"">Reason</td>
                                        <td style=""padding: 8px 0 0 0; color: {ErrorColor}; font-size: 13px; text-align: right;"">{EscapeHtml(reason)}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <div style=""background-color: {BrandColorLight}; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <h3 style=""margin: 0 0 8px 0; color: {TextColor}; font-size: 15px;"">What can you do?</h3>
                                <ul style=""margin: 0; padding-left: 20px; color: {TextColor}; font-size: 14px;"">
                                    <li style=""margin-bottom: 4px;"">Check your card details and try again</li>
                                    <li style=""margin-bottom: 4px;"">Ensure you have sufficient credit limit</li>
                                    <li style=""margin-bottom: 4px;"">Contact your bank if the issue persists</li>
                                    <li>Try with a different payment method</li>
                                </ul>
                            </div>";
        return BaseTemplate(
            "Payment Failed",
            $"Payment of ₹{amount:N2} could not be processed",
            content,
            "Your bill remains unpaid. Please retry or contact support for assistance."
        );
    }

    public static string BillGenerated(string fullName, decimal amount, DateTime dueDate, string billId)
    {
        var daysUntilDue = (dueDate - DateTime.UtcNow).Days;
        var urgencyColor = daysUntilDue <= 5 ? ErrorColor : daysUntilDue <= 10 ? WarningColor : MutedColor;
        var dueDateIst = ConvertToIst(dueDate);
        
        var content = $@"
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px;"">
                                Your new bill statement is ready. Please review the details below.
                            </p>
                            
                            <div style=""background: linear-gradient(135deg, {BrandColor} 0%, #1D4ED8 100%); border-radius: 8px; padding: 20px; margin-bottom: 20px; color: #FFFFFF;"">
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding: 4px 0; font-size: 13px; opacity: 0.9;"">Bill ID</td>
                                        <td style=""padding: 4px 0; font-size: 13px; text-align: right; font-family: monospace;"">{EscapeHtml(billId)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 4px 0; font-size: 13px; opacity: 0.9;"">Total Amount Due</td>
                                        <td style=""padding: 4px 0; font-size: 24px; font-weight: 700; text-align: right;"">₹{amount:N2}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0 0 0; font-size: 13px; opacity: 0.9;"">Due Date</td>
                                        <td style=""padding: 8px 0 0 0; font-size: 15px; font-weight: 600; text-align: right;"">{dueDateIst:MMMM dd, yyyy}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <div style=""background-color: {(daysUntilDue <= 5 ? "#FEE2E2" : daysUntilDue <= 10 ? "#FEF3C7" : "#F3F4F6")}; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; border-left: 4px solid {urgencyColor};"">
                                <p style=""margin: 0; color: {urgencyColor}; font-size: 13px;"">
                                    <strong>{daysUntilDue} day{(daysUntilDue != 1 ? "s" : "")} until due date</strong>
                                    {(daysUntilDue <= 5 ? " — Please pay soon to avoid late fees!" : "")}
                                </p>
                            </div>
                            
                            <div style=""background-color: {BrandColorLight}; border-radius: 8px; padding: 20px;"">
                                <h3 style=""margin: 0 0 8px 0; color: {TextColor}; font-size: 15px;"">Quick Actions</h3>
                                <p style=""margin: 0; color: {MutedColor}; font-size: 13px;"">
                                    Log in to CredVault to view your full statement, transaction history, and make payments.
                                </p>
                            </div>";
        return BaseTemplate(
            "Your Bill is Ready",
            $"Amount due: ₹{amount:N2} by {dueDateIst:MMM dd, yyyy}",
            content,
            "Pay your bill on time to maintain a good credit score and avoid late fees."
        );
    }

    public static string CardAdded(string fullName, string cardLast4, string cardHolderName, DateTime addedAt)
    {
        var addedAtIst = ConvertToIst(addedAt);
        var content = $@"
                            <div style=""text-align: center; margin: 16px 0;"">
                                <div style=""display: inline-block; width: 64px; height: 64px; background-color: {BrandColorLight}; border-radius: 50%; text-align: center; line-height: 64px; margin-bottom: 16px;"">
                                    <span style=""font-size: 32px;"">💳</span>
                                </div>
                                <h2 style=""margin: 0; color: {BrandColor}; font-size: 24px;"">New Card Added</h2>
                            </div>
                            
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px; text-align: center;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>, a new credit card has been added to your account.
                            </p>
                            
                            <div style=""background: linear-gradient(135deg, #1F2937 0%, #374151 100%); border-radius: 12px; padding: 24px; margin: 20px 0; color: #FFFFFF;"">
                                <div style=""margin-bottom: 20px;"">
                                    <p style=""margin: 0 0 4px 0; font-size: 11px; opacity: 0.7; text-transform: uppercase; letter-spacing: 2px;"">Card Number</p>
                                    <p style=""margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 4px;"">
                                        •••• •••• •••• {EscapeHtml(cardLast4)}
                                    </p>
                                </div>
                                <div style=""display: table; width: 100%;"">
                                    <div style=""display: table-cell; width: 50%;"">
                                        <p style=""margin: 0 0 4px 0; font-size: 11px; opacity: 0.7; text-transform: uppercase; letter-spacing: 2px;"">Card Holder</p>
                                        <p style=""margin: 0; font-size: 13px; font-weight: 500;"">{EscapeHtml(cardHolderName)}</p>
                                    </div>
                                    <div style=""display: table-cell; width: 50%;"">
                                        <p style=""margin: 0 0 4px 0; font-size: 11px; opacity: 0.7; text-transform: uppercase; letter-spacing: 2px;"">Added On</p>
                                        <p style=""margin: 0; font-size: 13px; font-weight: 500;"">{addedAtIst:MMM dd, yyyy}</p>
                                    </div>
                                </div>
                            </div>
                            
                            <div style=""background-color: {BrandColorLight}; border-radius: 8px; padding: 12px 16px;"">
                                <p style=""margin: 0; color: {MutedColor}; font-size: 13px;"">
                                    Your new card is now active and ready to use. Start tracking your transactions and payments with CredVault.
                                </p>
                            </div>";
        return BaseTemplate(
            "New Card Added to Your Account",
            "Your credit card is now linked",
            content,
            "If you did not add this card, please contact support immediately."
        );
    }

    public static string OtpVerificationFailed(string fullName, string reason)
    {
        var content = $@"
                            <div style=""text-align: center; margin: 16px 0;"">
                                <div style=""display: inline-block; width: 64px; height: 64px; background-color: #FEE2E2; border-radius: 50%; text-align: center; line-height: 64px; margin-bottom: 16px;"">
                                    <span style=""font-size: 32px;"">🔐</span>
                                </div>
                                <h2 style=""margin: 0; color: {ErrorColor}; font-size: 24px;"">Verification Failed</h2>
                            </div>
                            
                            <p style=""margin: 0 0 20px 0; color: {TextColor}; font-size: 15px; text-align: center;"">
                                Hello <strong>{EscapeHtml(fullName)}</strong>,
                            </p>
                            
                            <div style=""background-color: #FEE2E2; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
                                <p style=""margin: 0 0 4px 0; color: {ErrorColor}; font-size: 13px; font-weight: 600;"">Reason:</p>
                                <p style=""margin: 0; color: {TextColor}; font-size: 15px;"">{EscapeHtml(reason)}</p>
                            </div>
                            
                            <div style=""background-color: {BrandColorLight}; border-radius: 8px; padding: 16px; margin-bottom: 20px;"">
                                <h3 style=""margin: 0 0 8px 0; color: {TextColor}; font-size: 15px;"">What happened?</h3>
                                <p style=""margin: 0; color: {MutedColor}; font-size: 13px;"">
                                    Your OTP verification failed. This could be due to:
                                </p>
                                <ul style=""margin: 8px 0 0 0; padding-left: 20px; color: {MutedColor}; font-size: 13px;"">
                                    <li style=""margin-bottom: 4px;"">Incorrect OTP code entered</li>
                                    <li style=""margin-bottom: 4px;"">OTP has expired (valid for 5 minutes)</li>
                                    <li>Too many failed attempts</li>
                                </ul>
                            </div>
                            
                            <p style=""margin: 0; color: {TextColor}; font-size: 15px;"">
                                Please request a new OTP and try again.
                            </p>";
        return BaseTemplate(
            "OTP Verification Failed",
            "Please try again with a new code",
            content
        );
    }

    private static string EscapeHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
