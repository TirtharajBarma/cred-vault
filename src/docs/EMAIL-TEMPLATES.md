# Email Templates Reference

Professional HTML email templates for CredVault notifications.

## Last Updated
**Date:** 2026-04-12

---

## Overview

All emails use a consistent, professional design with:
- CredVault branding with gradient header
- Mobile-responsive layout
- Clear visual hierarchy
- Action-oriented content

---

## Email Types

| Type | Trigger | Subject |
|------|---------|---------|
| Welcome | User registration | "Welcome to CredVault! 🎉" |
| Email Verification | OTP for email verification | "Verify Your Email Address 📧" |
| Password Reset | OTP for password reset | "Password Reset Code 🔑" |
| Payment OTP | OTP for payment verification | "Payment Verification Required 🔐" |
| Payment Success | Payment completed | "✅ Payment Successful!" |
| Payment Failed | Payment failed | "❌ Payment Failed" |
| Bill Generated | New bill created | "Your Bill is Ready" |
| Card Added | New card linked | "New Card Added to Your Account 💳" |
| OTP Failed | OTP verification failed | "⚠️ OTP Verification Failed" |

---

## Template Designs

### 1. Welcome Email
**Trigger:** New user registration
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
│  Your Trusted Credit Card Companion             │
├─────────────────────────────────────────────────┤
│                                                 │
│  Welcome to CredVault!                          │
│  Your account is ready                          │
│                                                 │
│  Hello {FullName},                             │
│                                                 │
│  Welcome to CredVault! 🎉 Your account has      │
│  been successfully created.                     │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Your Account Details                     │   │
│  │ Email      user@example.com              │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Next Steps:                                    │
│  • Verify your email address...                │
│  • Add your credit cards...                    │
│  • Set up bill payment reminders...            │
│                                                 │
│  If you did not create this account, please     │
│  contact our support team immediately.          │
└─────────────────────────────────────────────────┘
```

### 2. Email Verification OTP
**Trigger:** User requests email verification
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│  Email Verification Code                        │
│  Please use this code to verify your email      │
│                                                 │
│  Hello {FullName},                             │
│  Your verification code is:                     │
│                                                 │
│        ┌─────────────────────┐                  │
│        │   123456           │  (large OTP)     │
│        └─────────────────────┘                  │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Purpose: EmailVerification               │   │
│  │ Expires: April 12, 2026 at 03:00 PM UTC│   │
│  └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 3. Password Reset OTP
**Trigger:** User requests password reset
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│  Password Reset Request                         │
│  Someone requested a password reset...          │
│                                                 │
│  Hello {FullName},                             │
│  We received a request to reset your password.  │
│  Use the code below:                           │
│                                                 │
│        ┌─────────────────────┐                  │
│        │   123456           │  (red gradient)   │
│        └─────────────────────┘                  │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ ⚠️ Security Notice: If you didn't       │   │
│  │ request this, please ignore this email. │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 4. Payment OTP
**Trigger:** User initiates payment
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│  Payment Verification Required                   │
│  Confirm payment of ₹4,200.00                   │
│                                                 │
│  Hello {FullName},                             │
│  Please enter the following code to confirm:   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Amount              ₹4,200.00           │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│        ┌─────────────────────┐                  │
│        │   123456           │  (blue gradient)  │
│        └─────────────────────┘                  │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Expires: April 12, 2026 at 03:05 PM UTC│   │
│  └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 5. Payment Successful
**Trigger:** Payment completed successfully
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│                    ✅                          │
│           Payment Successful!                   │
│                                                 │
│  Hello {FullName}, your payment has been       │
│  processed successfully.                        │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Payment ID    PAY-1234567890            │   │
│  │ ─────────────────────────────────────  │   │
│  │ Amount Paid   ₹4,000.00                │   │
│  │ Rewards Used  ₹200.00                  │   │
│  │ ─────────────────────────────────────  │   │
│  │ Total Bill    ₹4,200.00                │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Thank you for your payment!                    │
│  Keep this receipt for your records.            │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 6. Payment Failed
**Trigger:** Payment failed
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│                    ❌                          │
│            Payment Failed                       │
│                                                 │
│  Hello {FullName}, unfortunately your payment   │
│  could not be processed.                        │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Payment ID    PAY-1234567890            │   │
│  │ Amount        ₹4,200.00                │   │
│  │ Reason        Insufficient credit limit │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ What can you do?                        │   │
│  │ • Check your card details and try again │   │
│  │ • Ensure you have sufficient credit    │   │
│  │ • Contact your bank if issue persists   │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Your bill remains unpaid. Please retry...      │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 7. Bill Generated
**Trigger:** New bill statement generated
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│  Your Bill is Ready                             │
│  Amount due: ₹4,200.00 by Apr 20, 2026        │
│                                                 │
│  Hello {FullName},                             │
│  Your new bill statement is ready.              │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ (blue gradient)                         │   │
│  │ Bill ID        BILL-1234567890          │   │
│  │                                         │   │
│  │ Total Amount Due                        │   │
│  │              ₹4,200.00                 │   │
│  │                                         │   │
│  │ Due Date                              │   │
│  │          April 20, 2026               │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ 8 days until due date                   │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Pay your bill on time to maintain a good      │
│  credit score and avoid late fees.              │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 8. Card Added
**Trigger:** New credit card added
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│              💳                                │
│       New Card Added                           │
│                                                 │
│  Hello {FullName},                             │
│  A new credit card has been added.             │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ (dark card gradient)                     │   │
│  │                                         │   │
│  │  •••• •••• •••• 4532                   │   │
│  │                                         │   │
│  │  Card Holder     Added On               │   │
│  │  John Doe       Apr 12, 2026           │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Your new card is now active and ready to use.  │
│                                                 │
│  If you did not add this card, contact support. │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 9. OTP Verification Failed
**Trigger:** OTP verification failed
```
┌─────────────────────────────────────────────────┐
│  💳 CredVault                                  │
├─────────────────────────────────────────────────┤
│                                                 │
│              🔐                                │
│        Verification Failed                       │
│                                                 │
│  Hello {FullName},                             │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ Reason: Incorrect OTP code entered       │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ What happened?                          │   │
│  │ • Incorrect OTP code entered             │   │
│  │ • OTP has expired (valid for 5 min)    │   │
│  │ • Too many failed attempts             │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Please request a new OTP and try again.        │
│                                                 │
└─────────────────────────────────────────────────┘
```

---

## Implementation

### Template Location
```
NotificationService.Application/Services/EmailTemplates.cs
```

### Usage
```csharp
var emailHtml = EmailTemplates.UserWelcome("John Doe", "john@example.com");
var emailHtml = EmailTemplates.PaymentCompleted(fullName, amount, paid, rewards, id);
```

### Base Template Structure
```csharp
public static string BaseTemplate(string title, string subtitle, string content, string? footerNote = null)
```

---

## Design Guidelines

### Color Palette
| Purpose | Color | Hex |
|---------|-------|-----|
| Brand Primary | Blue | `#2563EB` |
| Brand Light | Light Blue | `#EFF6FF` |
| Text Primary | Dark Gray | `#1F2937` |
| Text Muted | Gray | `#6B7280` |
| Border | Light Gray | `#E5E7EB` |
| Success | Green | `#059669` |
| Warning | Amber | `#D97706` |
| Error | Red | `#DC2626` |

### Typography
- **Headings:** Segoe UI, 24-28px
- **Body:** Segoe UI, 14-16px
- **OTP Code:** Courier New, 36px

### Responsive
- Max width: 600px
- Border radius: 12-16px
- Padding: 24-40px

---

## Future Enhancements

- [ ] Add email preference settings (allow users to opt out)
- [ ] Add unsubscribe link to emails
- [ ] Add email tracking (open/click analytics)
- [ ] Add dark mode variant
- [ ] Add localization support
- [ ] Add dynamic content blocks
