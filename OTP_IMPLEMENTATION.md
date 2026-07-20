# OTP (One-Time Password) Login Implementation

## Overview
Added passwordless login via OTP (one-time password) codes sent to user email. Users can now choose to sign in with either:
1. Traditional password login (`/login`)
2. OTP email code login (`/otp-login`)

## What Was Implemented

### 1. Database Model
- **[Models/OtpCode.cs](Models/OtpCode.cs)**: New entity to store OTP records
  - 6-digit code (expires after 10 minutes)
  - Tracks creation time, expiration, usage, and failed attempts
  - Max 5 failed verification attempts before code is locked
  - Automatically invalidates previous unused OTPs when new one is sent

### 2. OTP Service
- **[Services/IOtpService.cs](Services/IOtpService.cs)**: Interface definition
- **[Services/OtpService.cs](Services/OtpService.cs)**: Implementation with:
  - `GenerateAndSendOtpAsync()`: Generates 6-digit code and emails it
  - `VerifyOtpAsync()`: Validates code (checks expiration, attempts, usage)
  - `CanRequestNewOtpAsync()`: Rate limiting (60-second wait between requests)
  - Logging for all operations

### 3. Controller Endpoints
Updated [Controllers/AccountController.cs](Controllers/AccountController.cs) with new routes:
- `GET /otp-login`: Display email entry form
- `POST /otp-login`: Generate and send OTP code
- `GET /otp-verify`: Display OTP code entry form
- `POST /otp-verify`: Verify code and sign in user
- Includes reCAPTCHA protection and approval checks

### 4. ViewModels
- **[ViewModels/Account/OtpLoginViewModel.cs](ViewModels/Account/OtpLoginViewModel.cs)**: Email input
- **[ViewModels/Account/OtpVerifyViewModel.cs](ViewModels/Account/OtpVerifyViewModel.cs)**: Code input with RememberMe option

### 5. Views
- **[Views/Account/OtpLogin.cshtml](Views/Account/OtpLogin.cshtml)**: Email entry page
- **[Views/Account/OtpVerify.cshtml](Views/Account/OtpVerify.cshtml)**: Code verification page
  - Auto-numeric-only input on code field
  - 6-digit code display with letter-spacing
  - Option to request new code

### 6. Integration
- Updated [Views/Account/Login.cshtml](Views/Account/Login.cshtml) with "Sign in with Email Code" button
- Registered `IOtpService` in [Program.cs](Program.cs)
- Added `OtpCodes` DbSet to [Data/AppDbContext.cs](Data/AppDbContext.cs)

### 7. Database Migration
- Created migration: `20260719235848_AddOtpCodes`
- Creates `OtpCodes` table with proper indexes and foreign keys

## Security Features
✅ Rate limiting: 60-second wait between code requests  
✅ Code expiration: 10-minute window  
✅ Lockout: 5 failed attempts locks the code  
✅ reCAPTCHA protection on initial request  
✅ Account approval check  
✅ Previous codes invalidated on new request  

## User Flow

### Sign In with Email Code
1. Click "Sign in with Email Code" on login page
2. Enter email → Click "Send Login Code"
3. Check email for 6-digit code
4. Return to form, enter code → Click "Verify Code"
5. Automatically signed in and redirected to dashboard

### Features
- "Remember me" checkbox for persistent login
- Rate limiting feedback to user
- Expired code detection
- Link to request new code from verification page

## Testing
```bash
# Build project
dotnet build

# Run migrations (first time use)
dotnet ef database update

# Application automatically runs migrations on startup
```

## Notes
- Uses existing SMTP configuration from `appsettings.json`
- Email template uses PMES branding (orange accents)
- All OTP operations are logged
- No changes to existing password login flow
- Fully compatible with existing authentication system
