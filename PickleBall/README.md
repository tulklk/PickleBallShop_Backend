# PickleBall Shop Backend API

A production-ready ASP.NET Core 8.0 Web API backend for an e-commerce website specializing in pickleball products.

## Features

- **Authentication & Authorization**
  - JWT Bearer authentication with refresh token mechanism
  - Role-based access control (Customer, Admin)
  - Secure password hashing with BCrypt

- **Product Management**
  - Browse products with filtering, sorting, and pagination
  - Search functionality
  - Product categories with hierarchical structure
  - Product reviews and ratings
  - Featured products

- **Shopping Cart**
  - Add, update, and remove cart items
  - Cart persistence for authenticated users

- **Order Management**
  - Create orders from cart
  - Order history for customers
  - Order status tracking

- **Payment Integration**
  - MoMo payment gateway integration
  - VNPay payment gateway integration
  - Payment callback and IPN handling
  - Payment status tracking

- **Admin Features**
  - Product CRUD operations
  - Category management
  - Order management and status updates

## Tech Stack

- **Framework**: ASP.NET Core 8.0 Web API
- **Database**: Supabase PostgreSQL
- **ORM**: Entity Framework Core 8.0
- **Authentication**: JWT Bearer Tokens
- **Mapping**: AutoMapper
- **Validation**: FluentValidation
- **Documentation**: Swagger/OpenAPI

## Project Structure

```
PickleBall/
├── PickleBall.Domain/          # Domain entities and enums
├── PickleBall.Application/     # DTOs, interfaces, services, mapping
├── PickleBall.Infrastructure/  # EF Core, repositories, payment services
└── PickleBall/                 # API controllers and configuration
```

## Setup Instructions

### 1. Prerequisites

- .NET 8.0 SDK
- Supabase PostgreSQL database
- PostgreSQL client tools (for migrations)

### 2. Database Configuration

1. Create a Supabase project at https://supabase.com
2. Get your PostgreSQL connection string from Supabase dashboard
3. Update `appsettings.json` with your connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### 3. JWT Configuration

Update the JWT settings in `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyForJWTTokenGenerationMustBeAtLeast32CharactersLong!",
    "Issuer": "PickleBallShop",
    "Audience": "PickleBallShop",
    "ExpiryMinutes": "60"
  }
}
```

**Important**: In production, store the JWT secret key in environment variables or user secrets, not in appsettings.json.

### 4. Payment Gateway Configuration

#### MoMo Payment

Update MoMo settings in `appsettings.json`:

```json
{
  "Payment": {
    "MoMo": {
      "PartnerCode": "YOUR_MOMO_PARTNER_CODE",
      "AccessKey": "YOUR_MOMO_ACCESS_KEY",
      "SecretKey": "YOUR_MOMO_SECRET_KEY",
      "Endpoint": "https://test-payment.momo.vn/v2/gateway/api/create",
      "ReturnUrl": "https://your-domain.com/api/payments/momo/return",
      "IpnUrl": "https://your-domain.com/api/payments/momo/ipn"
    }
  }
}
```

Get MoMo test credentials from: https://developers.momo.vn/

#### VNPay Payment

Update VNPay settings in `appsettings.json`:

```json
{
  "Payment": {
    "VNPay": {
      "TmnCode": "YOUR_VNPAY_TMN_CODE",
      "HashSecret": "YOUR_VNPAY_HASH_SECRET",
      "Endpoint": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
      "ReturnUrl": "https://your-domain.com/api/payments/vnpay/return",
      "IpnUrl": "https://your-domain.com/api/payments/vnpay/ipn"
    }
  }
}
```

Get VNPay test credentials from: https://sandbox.vnpayment.vn/

### 5. Run Migrations

Apply database migrations to create the schema:

```bash
# From the solution root directory
dotnet ef database update --project PickleBall.Infrastructure --startup-project PickleBall
```

Or create a new migration:

```bash
dotnet ef migrations add MigrationName --project PickleBall.Infrastructure --startup-project PickleBall
```

### 6. Seed Data (Optional)

The seed data method is included in `PickleBall.Infrastructure.Data.SeedData`. To seed the database:

1. Uncomment the seed data call in `Program.cs` (or create a separate seeding script)
2. Run the application once to seed initial data

Default admin credentials:
- Email: `admin@pickleballshop.com`
- Password: `Admin@123`

**Important**: Change the admin password after first login!

### 7. Run the Application

```bash
dotnet run --project PickleBall
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000` (configured as root)

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login and get access token
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Logout (revoke refresh token)

### Products
- `GET /api/products` - Get products with filtering, sorting, pagination
- `GET /api/products/{id}` - Get product by ID
- `GET /api/products/slug/{slug}` - Get product by slug
- `GET /api/products/featured` - Get featured products
- `GET /api/products/search?q={term}` - Search products

### Categories
- `GET /api/categories` - Get all categories
- `GET /api/categories/{id}` - Get category by ID
- `GET /api/categories/slug/{slug}` - Get category by slug

### Cart (Authenticated)
- `GET /api/cart` - Get user's cart
- `POST /api/cart/items` - Add item to cart
- `PUT /api/cart/items/{id}` - Update cart item quantity
- `DELETE /api/cart/items/{id}` - Remove cart item
- `DELETE /api/cart` - Clear cart

### Orders (Authenticated)
- `POST /api/orders` - Create order from cart
- `GET /api/orders/my` - Get user's orders
- `GET /api/orders/{id}` - Get order details
- `POST /api/orders/{id}/payment` - Initiate payment for order

### Payments
- `GET /api/payments/momo/return` - MoMo payment return callback
- `POST /api/payments/momo/ipn` - MoMo payment IPN callback
- `GET /api/payments/vnpay/return` - VNPay payment return callback
- `POST /api/payments/vnpay/ipn` - VNPay payment IPN callback

### Admin (Requires Admin Role)
- `POST /api/admin/products` - Create product
- `PUT /api/admin/products/{id}` - Update product
- `DELETE /api/admin/products/{id}` - Delete product
- `POST /api/admin/categories` - Create category
- `PUT /api/admin/categories/{id}` - Update category
- `DELETE /api/admin/categories/{id}` - Delete category
- `GET /api/admin/orders` - Get all orders (with filters)
- `PUT /api/admin/orders/{id}/status` - Update order status

## Testing with Swagger

1. Start the application
2. Navigate to `http://localhost:5000` (Swagger UI)
3. Use the "Authorize" button to authenticate with a JWT token
4. Test endpoints directly from Swagger UI

## Security Notes

- Always use HTTPS in production
- Store sensitive configuration (JWT secret, payment keys) in environment variables or Azure Key Vault
- Regularly rotate JWT secret keys
- Implement rate limiting for production
- Use CORS policies appropriate for your frontend domain
- Enable SQL injection protection (already handled by EF Core parameterized queries)

## License

This project is proprietary and confidential.

