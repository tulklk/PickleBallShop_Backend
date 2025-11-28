# Email Verification Setup Guide

## Tổng quan
Hệ thống email verification đã được tích hợp vào quy trình đăng ký. Khi user đăng ký tài khoản mới:
1. Tài khoản được tạo nhưng email chưa được verify
2. Email verification được gửi đến địa chỉ email đã đăng ký
3. User click vào link trong email để verify
4. Sau khi verify, user mới có thể đăng nhập

## Cấu hình Email SMTP

### 1. Cấu hình trong `appsettings.json`

Thêm các thông tin SMTP vào file `appsettings.json`:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@pickleballshop.com",
    "FromName": "PickleBall Shop",
    "EnableSsl": true,
    "FrontendUrl": "https://pickleball-shop-frontend.vercel.app"
  }
}
```

### 2. Sử dụng Gmail SMTP

Nếu bạn sử dụng Gmail:

1. **Bật 2-Step Verification** cho tài khoản Gmail của bạn
2. **Tạo App Password**:
   - Vào [Google Account Security](https://myaccount.google.com/security)
   - Chọn "2-Step Verification" → "App passwords"
   - Tạo app password mới cho "Mail"
   - Sử dụng password này trong `SmtpPassword`

**Lưu ý:** Không sử dụng mật khẩu Gmail thông thường, phải dùng App Password.

### 3. Cấu hình trên Render (Production)

Thêm các biến môi trường trên Render Dashboard:

```
EMAIL__SMTPHOST=smtp.gmail.com
EMAIL__SMTPPORT=587
EMAIL__SMTPUSERNAME=your-email@gmail.com
EMAIL__SMTPPASSWORD=your-app-password
EMAIL__FROMEMAIL=noreply@pickleballshop.com
EMAIL__FROMNAME=PickleBall Shop
EMAIL__ENABLESSL=true
EMAIL__FRONTENDURL=https://pickleball-shop-frontend.vercel.app
```

**Lưu ý:** Dấu `__` (double underscore) được sử dụng để phân cấp trong environment variables trên Render.

### 4. Development Mode

Nếu không cấu hình email, hệ thống sẽ:
- Log email content ra console
- Không thực sự gửi email
- Vẫn cho phép development tiếp tục

Điều này cho phép bạn phát triển mà không cần cấu hình email ngay lập tức.

## API Endpoints

### 1. Register
```
POST /api/auth/register
```

**Request:**
```json
{
  "email": "user@example.com",
  "password": "password123",
  "confirmPassword": "password123",
  "fullName": "John Doe",
  "phoneNumber": "0123456789"
}
```

**Response:**
```json
{
  "message": "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản.",
  "email": "user@example.com"
}
```

### 2. Verify Email
```
POST /api/auth/verify-email
```

**Request:**
```json
{
  "token": "verification-token-from-email"
}
```

**Response:**
```json
{
  "message": "Email verified successfully"
}
```

### 3. Login
```
POST /api/auth/login
```

**Lưu ý:** Login sẽ fail nếu email chưa được verify:
```json
{
  "message": "Please verify your email before logging in"
}
```

## Database Migration

Chạy SQL script `add_email_verification_migration.sql` trong Supabase SQL Editor để:
1. Thêm column `EmailVerified` vào bảng `Users`
2. Tạo bảng `EmailVerificationTokens`

## Email Template

Email verification sẽ được gửi với:
- Subject: "Xác nhận email đăng ký tài khoản PickleBall Shop"
- HTML format với button để click verify
- Link verify có format: `{FrontendUrl}/auth/verify-email?token={token}`
- Token hết hạn sau 24 giờ

## Google Login

Users đăng nhập bằng Google **tự động được verified** vì Google đã xác thực email của họ.

## Troubleshooting

### Email không được gửi
1. Kiểm tra logs để xem có lỗi gì không
2. Đảm bảo SMTP credentials đúng
3. Kiểm tra firewall/security settings
4. Đối với Gmail, đảm bảo đã dùng App Password

### Token hết hạn
- Token verification chỉ có hiệu lực trong 24 giờ
- User cần đăng ký lại hoặc có thể thêm tính năng resend verification email

### Frontend Integration
Frontend cần:
1. Nhận response từ register endpoint
2. Hiển thị thông báo "Vui lòng kiểm tra email"
3. Tạo page `/auth/verify-email` để nhận token từ query parameter
4. Gọi API `POST /api/auth/verify-email` với token
5. Redirect đến login page sau khi verify thành công

