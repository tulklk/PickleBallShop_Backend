# 🏓 PickleBall Shop - E-Commerce Backend API

Hệ thống Backend API hoàn chỉnh cho nền tảng thương mại điện tử chuyên bán các sản phẩm PickleBall (vợt, bóng, giày, phụ kiện). Dự án được xây dựng với kiến trúc Clean Architecture, tuân thủ các best practices của .NET và có đầy đủ tính năng e-commerce hiện đại.

## 📋 Mục Lục

- [Tổng Quan](#tổng-quan)
- [Kiến Trúc Hệ Thống](#kiến-trúc-hệ-thống)
- [Công Nghệ Sử Dụng](#công-nghệ-sử-dụng)
- [Tính Năng Chính](#tính-năng-chính)
- [Cấu Trúc Project](#cấu-trúc-project)
- [Cài Đặt & Chạy Project](#cài-đặt--chạy-project)
- [API Documentation](#api-documentation)
- [Database Schema](#database-schema)
- [Authentication & Authorization](#authentication--authorization)
- [Deployment](#deployment)
- [Contributing](#contributing)

---

## 🎯 Tổng Quan

PickleBall Shop là một hệ thống e-commerce backend được thiết kế và phát triển với mục tiêu cung cấp API đầy đủ cho việc quản lý sản phẩm, đơn hàng, thanh toán và người dùng. Hệ thống hỗ trợ đa dạng phương thức thanh toán phổ biến tại Việt Nam, quản lý sản phẩm với variants (màu sắc, size, thông số), và tích hợp email service để gửi thông báo tự động.

### Điểm Nổi Bật

- ✅ **Clean Architecture** - Tách biệt rõ ràng các layers, dễ maintain và scale
- ✅ **RESTful API** - API design chuẩn, có Swagger documentation đầy đủ
- ✅ **Multi-Payment Gateway** - Hỗ trợ MoMo, VNPay, PayOS, COD, Bank Transfer
- ✅ **Product Variants** - Quản lý sản phẩm với variants (color, size, specs)
- ✅ **JWT Authentication** - Bảo mật với JWT tokens và refresh tokens
- ✅ **Email Integration** - Gửi email xác nhận đơn hàng, reset password, verify email
- ✅ **Inventory Management** - Quản lý tồn kho tự động với inventory transactions
- ✅ **Role-Based Access Control** - Phân quyền Admin/Customer
- ✅ **Google OAuth** - Đăng nhập bằng tài khoản Google

---

## 🏗️ Kiến Trúc Hệ Thống

Project được xây dựng theo **Clean Architecture** với 4 layers chính:

```
┌─────────────────────────────────────────────┐
│         Presentation Layer                  │
│         (Controllers)                       │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│         Application Layer                   │
│    (DTOs, Interfaces, Mapping, Validators)  │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│       Infrastructure Layer                  │
│  (Services, Data Access, External Services) │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│          Domain Layer                       │
│      (Entities, Enums, Business Logic)      │
└─────────────────────────────────────────────┘
```

### Layers Chi Tiết

1. **Domain Layer** (`PickleBall.Domain`)
   - Entities: User, Product, Order, Cart, Payment, etc.
   - Enums: Role, PaymentStatus, OrderStatus, PaymentMethod
   - Business logic cốt lõi, không phụ thuộc vào frameworks

2. **Application Layer** (`PickleBall.Application`)
   - DTOs (Data Transfer Objects) cho các operations
   - Interfaces cho services (Dependency Inversion)
   - AutoMapper profiles
   - FluentValidation validators

3. **Infrastructure Layer** (`PickleBall.Infrastructure`)
   - Entity Framework Core configurations
   - Database context (PostgreSQL)
   - Service implementations
   - External services integration (Email, Payment gateways)
   - Middleware (Error handling)

4. **Presentation Layer** (`PickleBall`)
   - API Controllers
   - Startup configuration
   - Dependency Injection setup

---

## 🛠️ Công Nghệ Sử Dụng

### Core Framework
- **.NET 8.0** - Framework chính
- **ASP.NET Core Web API** - RESTful API framework
- **C# 12** - Ngôn ngữ lập trình

### Database & ORM
- **PostgreSQL** - Hệ quản trị cơ sở dữ liệu
- **Entity Framework Core 8.0.11** - ORM framework
- **Npgsql.EntityFrameworkCore.PostgreSQL** - PostgreSQL provider

### Authentication & Security
- **JWT (JSON Web Tokens)** - Authentication mechanism
- **BCrypt.Net-Next 4.0.3** - Password hashing
- **Google.Apis.Auth 1.68.0** - Google OAuth integration
- **Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0** - JWT middleware

### Validation & Mapping
- **FluentValidation.AspNetCore 11.3.1** - Input validation
- **AutoMapper 12.0.1** - Object-to-object mapping

### External Services
- **MailKit 4.14.1** & **MimeKit 4.14.0** - Email sending
- Payment Gateway APIs:
  - MoMo Payment Gateway
  - VNPay Payment Gateway
  - PayOS Payment Gateway

### API Documentation
- **Swashbuckle.AspNetCore 6.6.2** - Swagger/OpenAPI documentation

### Development Tools
- **Docker** - Containerization
- **Entity Framework Core Tools** - Database migrations

---

## ✨ Tính Năng Chính

### 1. Authentication & Authorization
- 🔐 Đăng ký tài khoản với email verification
- 🔑 Đăng nhập với email/password (JWT tokens)
- 🔄 Refresh token mechanism
- 🌐 Google OAuth login
- 🔒 Password reset qua email
- 👤 Role-based access control (Admin/Customer)
- ✅ Email verification system

### 2. Product Management
- 📦 CRUD operations cho sản phẩm
- 🎨 Product variants (màu sắc, size, thông số kỹ thuật)
- 🖼️ Multiple images per product
- 📊 Product categories và filtering
- 🔍 Advanced search với pagination
- ⭐ Product reviews và ratings
- 💰 Pricing với original price & sale price
- 📈 Inventory management tự động
- 🏷️ SKU management

### 3. Shopping Cart
- 🛒 Add/Remove/Update cart items
- 📝 Cart persistence
- 💵 Price snapshot (lưu giá tại thời điểm thêm vào giỏ)
- 🔄 Variant selection trong cart
- ✅ Stock validation

### 4. Wishlist
- ❤️ Thêm/xóa sản phẩm yêu thích
- 📋 Xem danh sách wishlist
- ✅ Kiểm tra sản phẩm có trong wishlist

### 5. Order Management
- 📦 Tạo đơn hàng từ cart
- 📋 Xem lịch sử đơn hàng
- 🔄 Order status tracking (Pending → Confirmed → Shipping → Completed)
- 📧 Email xác nhận đơn hàng tự động
- 📦 Variant information snapshot trong order items
- 🏪 Shipping address management

### 6. Payment Integration
- 💳 **MoMo Payment** - Thanh toán qua ví điện tử MoMo
- 🏦 **VNPay** - Thanh toán qua VNPay gateway
- 💰 **PayOS** - Thanh toán qua PayOS (Bank Transfer)
- 💵 **COD (Cash on Delivery)** - Thanh toán khi nhận hàng
- 📊 **Bank Transfer** - Chuyển khoản ngân hàng với QR code
- ✅ Payment status tracking
- 🔔 Payment webhook/callback handling

### 7. Email Service
- 📧 Order confirmation emails (responsive HTML)
- 🔐 Password reset emails
- ✅ Email verification
- 📨 Professional email templates

### 8. Admin Panel APIs
- 👥 User management (active/inactive status)
- 📦 Product CRUD với variants
- 📂 Category management
- 📋 Order management và status updates
- 📊 Admin dashboard data

### 9. Additional Features
- 🔍 Health check endpoint
- 📝 Comprehensive error handling middleware
- 🚀 Request/Response logging
- 🔄 Database migrations
- 🌍 CORS configuration
- 📱 Mobile-responsive email templates

---

## 📁 Cấu Trúc Project

```
PickleBall/
│
├── PickleBall.Domain/                    # Domain Layer
│   └── Entities/
│       ├── User.cs
│       ├── Product.cs
│       ├── ProductVariant.cs
│       ├── Order.cs
│       ├── Cart.cs
│       ├── Wishlist.cs
│       ├── Payment.cs
│       └── ...
│
├── PickleBall.Application/               # Application Layer
│   ├── DTOs/
│   │   ├── Auth/
│   │   ├── Products/
│   │   ├── Orders/
│   │   ├── Payments/
│   │   └── ...
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── IProductService.cs
│   │   └── ...
│   ├── Mapping/
│   │   └── MappingProfile.cs
│   └── Validators/
│
├── PickleBall.Infrastructure/            # Infrastructure Layer
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   ├── Configurations/
│   │   ├── Migrations/
│   │   └── SeedData.cs
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── ProductService.cs
│   │   ├── OrderService.cs
│   │   ├── EmailService.cs
│   │   ├── MoMoPaymentService.cs
│   │   └── ...
│   └── Middleware/
│       └── ErrorHandlingMiddleware.cs
│
├── Controllers/                          # Presentation Layer
│   ├── AuthController.cs
│   ├── ProductsController.cs
│   ├── OrdersController.cs
│   ├── CartController.cs
│   ├── WishlistController.cs
│   ├── PaymentsController.cs
│   └── Admin/
│       ├── AdminProductsController.cs
│       ├── AdminOrdersController.cs
│       └── ...
│
├── Program.cs                            # Startup configuration
├── appsettings.json                      # Configuration
├── Dockerfile                            # Docker configuration
└── render.yaml                           # Render.com deployment config
```

---

## 🚀 Cài Đặt & Chạy Project

### Yêu Cầu Hệ Thống

- .NET 8.0 SDK
- PostgreSQL 12+ (hoặc database server tương thích)
- Git

### Bước 1: Clone Repository

```bash
git clone <repository-url>
cd PickleBallShop_backend/PickleBallShop/PickleBall
```

### Bước 2: Cấu Hình Database

Cập nhật `appsettings.json` hoặc `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=PickleBallShop;Username=postgres;Password=your_password"
  }
}
```

### Bước 3: Cấu Hình JWT

Thêm JWT settings vào `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-minimum-32-characters-long",
    "Issuer": "PickleBallShop",
    "Audience": "PickleBallShop",
    "ExpiryMinutes": 60
  }
}
```

### Bước 4: Cấu Hình Email (Optional)

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "IKA PickleBall Shop",
    "EnableSsl": true,
    "FrontendUrl": "http://localhost:3000"
  }
}
```

### Bước 5: Cấu Hình Payment Gateways (Optional)

Thêm payment settings vào `appsettings.json` (xem `render.yaml` để biết các keys cần thiết).

### Bước 6: Chạy Database Migrations

```bash
# Tạo migration (nếu cần)
dotnet ef migrations add InitialCreate --project PickleBall.Infrastructure --startup-project PickleBall

# Áp dụng migrations
dotnet ef database update --project PickleBall.Infrastructure --startup-project PickleBall
```

Hoặc sử dụng PowerShell script:

```powershell
.\run_product_variants_migration.ps1
```

### Bước 7: Chạy Application

```bash
dotnet restore
dotnet build
dotnet run --project PickleBall
```

Application sẽ chạy tại: `https://localhost:5001` hoặc `http://localhost:5000`

### Bước 8: Truy Cập Swagger Documentation

Mở browser và truy cập: `https://localhost:5001/swagger`

---

## 📚 API Documentation

### Authentication Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/api/auth/register` | Đăng ký tài khoản mới | ❌ |
| POST | `/api/auth/login` | Đăng nhập | ❌ |
| POST | `/api/auth/google-login` | Đăng nhập bằng Google | ❌ |
| POST | `/api/auth/refresh-token` | Refresh access token | ❌ |
| POST | `/api/auth/verify-email` | Xác thực email | ❌ |
| POST | `/api/auth/forgot-password` | Quên mật khẩu | ❌ |
| POST | `/api/auth/change-password` | Đổi mật khẩu | ✅ |

### Product Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/api/products` | Lấy danh sách sản phẩm (có pagination, filter) | ❌ |
| GET | `/api/products/{id}` | Lấy chi tiết sản phẩm | ❌ |
| GET | `/api/products/featured` | Lấy sản phẩm nổi bật | ❌ |
| GET | `/api/products/search?q={term}` | Tìm kiếm sản phẩm | ❌ |

### Cart Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/api/cart` | Lấy giỏ hàng | ✅ |
| POST | `/api/cart/items` | Thêm sản phẩm vào giỏ | ✅ |
| PUT | `/api/cart/items/{id}` | Cập nhật số lượng | ✅ |
| DELETE | `/api/cart/items/{id}` | Xóa sản phẩm khỏi giỏ | ✅ |
| DELETE | `/api/cart` | Xóa toàn bộ giỏ hàng | ✅ |

### Wishlist Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/api/wishlist` | Lấy danh sách yêu thích | ✅ |
| POST | `/api/wishlist/{productId}` | Thêm vào wishlist | ✅ |
| DELETE | `/api/wishlist/{productId}` | Xóa khỏi wishlist | ✅ |
| GET | `/api/wishlist/{productId}/check` | Kiểm tra có trong wishlist | ✅ |

### Order Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/api/orders` | Tạo đơn hàng mới | ✅ |
| GET | `/api/orders` | Lấy danh sách đơn hàng của user | ✅ |
| GET | `/api/orders/{id}` | Lấy chi tiết đơn hàng | ✅ |

### Payment Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/api/payments/momo/return` | MoMo payment callback | ❌ |
| GET | `/api/payments/vnpay/return` | VNPay payment callback | ❌ |
| POST | `/api/payments/payos/webhook` | PayOS webhook | ❌ |

### Admin Endpoints

Tất cả admin endpoints yêu cầu role `Admin`:

- `/api/admin/products` - Quản lý sản phẩm
- `/api/admin/orders` - Quản lý đơn hàng
- `/api/admin/users` - Quản lý người dùng
- `/api/admin/categories` - Quản lý danh mục

**Xem đầy đủ API documentation tại:** `https://your-api-url/swagger`

---

## 🗄️ Database Schema

### Core Entities

- **Users** - Thông tin người dùng (Customer/Admin)
- **Products** - Sản phẩm
- **ProductVariants** - Biến thể sản phẩm (màu, size, specs)
- **ProductImages** - Hình ảnh sản phẩm
- **ProductCategories** - Danh mục sản phẩm
- **ProductReviews** - Đánh giá sản phẩm
- **Carts** - Giỏ hàng
- **CartItems** - Sản phẩm trong giỏ hàng
- **Wishlists** - Danh sách yêu thích
- **Orders** - Đơn hàng
- **OrderItems** - Sản phẩm trong đơn hàng
- **Payments** - Thanh toán
- **InventoryTransactions** - Lịch sử nhập/xuất kho
- **RefreshTokens** - JWT refresh tokens
- **EmailVerificationTokens** - Email verification tokens

### Relationships

- User 1 → N Orders
- User 1 → N Wishlists
- User 1 → 1 Cart (1 → N CartItems)
- Product 1 → N ProductVariants
- Product 1 → N ProductImages
- Product 1 → N CartItems
- Order 1 → N OrderItems
- Order 1 → 1 Payment

---

## 🔐 Authentication & Authorization

### JWT Token Flow

1. User đăng nhập với email/password
2. Server trả về:
   - `accessToken` (JWT, expire sau 60 phút)
   - `refreshToken` (expire sau 7 ngày)
   - `expiresAt`
3. Client gửi `accessToken` trong header mỗi request: `Authorization: Bearer {token}`
4. Khi `accessToken` hết hạn, dùng `refreshToken` để lấy token mới

### Roles

- **Customer** - Người dùng thông thường
- **Admin** - Quản trị viên (có quyền truy cập admin endpoints)

### Protected Endpoints

Tất cả endpoints (trừ auth và public) yêu cầu JWT token trong header:
```
Authorization: Bearer {your-access-token}
```

---

## 🐳 Deployment

### Docker Deployment

Build Docker image:

```bash
docker build -t pickleball-shop-api .
```

Run container:

```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="your-connection-string" \
  -e Jwt__SecretKey="your-secret-key" \
  pickleball-shop-api
```

### Render.com Deployment

Project đã được cấu hình sẵn với `render.yaml`. Chỉ cần:

1. Kết nối GitHub repository với Render
2. Render sẽ tự động detect `render.yaml` và deploy
3. Cấu hình environment variables trong Render dashboard

### Environment Variables

Các biến môi trường cần thiết (xem `render.yaml` để biết chi tiết):

- `ConnectionStrings__DefaultConnection`
- `Jwt__SecretKey`
- `Email__SmtpUsername`, `Email__SmtpPassword`
- `Payment__MoMo__*`
- `Payment__VNPay__*`
- `Payment__PayOS__*`
- `Google__ClientId`, `Google__ClientSecret`

---

## 🎨 Best Practices Đã Áp Dụng

- ✅ **Clean Architecture** - Tách biệt layers rõ ràng
- ✅ **Dependency Injection** - Sử dụng DI container
- ✅ **Repository Pattern** - Qua Entity Framework Core
- ✅ **DTO Pattern** - Tách biệt API contracts và domain models
- ✅ **AutoMapper** - Mapping tự động giữa entities và DTOs
- ✅ **FluentValidation** - Input validation
- ✅ **Async/Await** - Non-blocking operations
- ✅ **Error Handling** - Global error handling middleware
- ✅ **Logging** - Structured logging
- ✅ **Database Migrations** - Version control cho database schema
- ✅ **CORS** - Cross-origin resource sharing
- ✅ **Swagger Documentation** - API documentation tự động

---

## 📝 License

This project is private and proprietary.

---

## 👨‍💻 Author

**Your Name**
- Email: tulklk@gmail.com
- LinkedIn:(https://www.linkedin.com/in/phan-th%C3%A0nh-t%C3%BA-21a13b36a )
- GitHub: (https://github.com/tulklk)

---

## 🙏 Acknowledgments

- ASP.NET Core team
- Entity Framework Core team
- All open-source contributors

---

**Made with ❤️ using .NET 8**

