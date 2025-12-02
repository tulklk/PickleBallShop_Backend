# Product Variants Setup Guide

## Tổng quan

Hệ thống Product Variants cho phép quản lý các biến thể của sản phẩm như:
- **Vợt (Paddles)**: Chia theo màu sắc và thông số (13mm, 15mm)
- **Giày (Shoes)**: Chia theo size (40, 41, 42, 43, etc.)
- **Bóng (Balls)**: Mua theo từng bóng (không cần variants)
- **Phụ kiện (Accessories)**: Mua theo từng món (không cần variants)

## Cấu trúc Database

### Bảng mới: `ProductVariants`

- `Id` (UUID, Primary Key)
- `ProductId` (UUID, Foreign Key → Products)
- `Color` (varchar(100), nullable) - Màu sắc (cho vợt)
- `Size` (varchar(50), nullable) - Size (cho giày)
- `Spec` (varchar(100), nullable) - Thông số (cho vợt: 13mm, 15mm)
- `Price` (decimal(18,2)) - Giá của variant
- `OriginalPrice` (decimal(18,2), nullable) - Giá gốc nếu có giảm giá
- `StockQuantity` (int) - Số lượng tồn kho riêng cho variant
- `SKU` (varchar(100), unique) - SKU riêng cho variant
- `IsActive` (boolean) - Trạng thái active
- `CreatedAt`, `UpdatedAt` (timestamptz)

### Cập nhật bảng `Products`

- Thêm cột `HasVariants` (boolean, default: false) - Đánh dấu sản phẩm có variants hay không

### Cập nhật bảng `CartItems`

- Thêm cột `ProductVariantId` (UUID, nullable) - Variant đã chọn trong cart
- Cập nhật unique index để bao gồm `ProductVariantId`

### Cập nhật bảng `OrderItems`

- Thêm cột `ProductVariantId` (UUID, nullable) - Variant đã chọn trong order
- Thêm cột `VariantInfoSnapshot` (varchar(500), nullable) - Lưu thông tin variant như snapshot

## Cách chạy Migration

### Phương pháp 1: Sử dụng SQL Script trực tiếp

1. Kết nối đến database PostgreSQL của bạn
2. Chạy file SQL:
   ```sql
   \i add_product_variants_migration.sql
   ```
   hoặc copy nội dung file `add_product_variants_migration.sql` và chạy trực tiếp trong pgAdmin/DBeaver.

### Phương pháp 2: Sử dụng Entity Framework Migrations

1. Tạo migration mới:
   ```bash
   dotnet ef migrations add AddProductVariants --project PickleBall.Infrastructure --startup-project PickleBall
   ```

2. Xem migration được tạo ra:
   ```bash
   dotnet ef migrations list --project PickleBall.Infrastructure --startup-project PickleBall
   ```

3. Áp dụng migration:
   ```bash
   dotnet ef database update --project PickleBall.Infrastructure --startup-project PickleBall
   ```

### Phương pháp 3: Sử dụng PowerShell Script

1. Mở PowerShell trong thư mục project
2. Chạy script:
   ```powershell
   .\run_product_variants_migration.ps1 -ConnectionString "Host=localhost;Database=pickleball_db;Username=your_user;Password=your_password"
   ```

## Cách sử dụng

### 1. Tạo sản phẩm không có variants (Bóng, Phụ kiện)

```csharp
var product = new Product
{
    Name = "Bóng Pickleball Franklin",
    CategoryId = categoryId,
    Price = 150000,
    StockQuantity = 100,
    SKU = "BALL-001",
    HasVariants = false, // Không có variants
    IsActive = true
};
```

### 2. Tạo sản phẩm có variants - Vợt (theo màu và thông số)

```csharp
var product = new Product
{
    Name = "Vợt Pickleball Proton Project Peacock Series 3",
    CategoryId = categoryId,
    Price = 6990000, // Giá base (sẽ được override bởi variants)
    StockQuantity = 0, // Stock sẽ quản lý ở variant level
    SKU = "PADDLE-001",
    HasVariants = true, // Có variants
    IsActive = true
};

// Variant 1: Màu đỏ, 13mm
var variant1 = new ProductVariant
{
    ProductId = product.Id,
    Color = "Đỏ",
    Spec = "13mm",
    Price = 6990000,
    StockQuantity = 50,
    SKU = "PADDLE-001-RED-13MM",
    IsActive = true
};

// Variant 2: Màu xanh, 13mm
var variant2 = new ProductVariant
{
    ProductId = product.Id,
    Color = "Xanh",
    Spec = "13mm",
    Price = 6990000,
    StockQuantity = 30,
    SKU = "PADDLE-001-BLUE-13MM",
    IsActive = true
};

// Variant 3: Màu đỏ, 15mm
var variant3 = new ProductVariant
{
    ProductId = product.Id,
    Color = "Đỏ",
    Spec = "15mm",
    Price = 7500000,
    StockQuantity = 20,
    SKU = "PADDLE-001-RED-15MM",
    IsActive = true
};
```

### 3. Tạo sản phẩm có variants - Giày (theo size)

```csharp
var product = new Product
{
    Name = "Giày Pickleball ASICS Court FF",
    CategoryId = categoryId,
    Price = 2500000,
    StockQuantity = 0, // Stock sẽ quản lý ở variant level
    SKU = "SHOE-001",
    HasVariants = true, // Có variants
    IsActive = true
};

// Variant các size
var sizes = new[] { "40", "41", "42", "43", "44" };
foreach (var size in sizes)
{
    var variant = new ProductVariant
    {
        ProductId = product.Id,
        Size = size,
        Price = 2500000,
        StockQuantity = 10,
        SKU = $"SHOE-001-{size}",
        IsActive = true
    };
    product.Variants.Add(variant);
}
```

## Logic xử lý

### Khi thêm vào Cart

```csharp
// Sản phẩm không có variants
var cartItem = new CartItem
{
    ProductId = product.Id,
    ProductVariantId = null, // Không có variant
    Quantity = 1,
    UnitPriceSnapshot = product.Price
};

// Sản phẩm có variants
var cartItem = new CartItem
{
    ProductId = product.Id,
    ProductVariantId = variant.Id, // Chọn variant cụ thể
    Quantity = 1,
    UnitPriceSnapshot = variant.Price
};
```

### Khi tạo Order

Tương tự như Cart, nhưng thêm `VariantInfoSnapshot` để lưu thông tin variant:

```csharp
var orderItem = new OrderItem
{
    ProductId = product.Id,
    ProductVariantId = variant?.Id,
    ProductNameSnapshot = product.Name,
    VariantInfoSnapshot = variant != null ? $"Màu: {variant.Color}, Thông số: {variant.Spec}" : null,
    UnitPrice = variant?.Price ?? product.Price,
    Quantity = 1,
    LineTotal = (variant?.Price ?? product.Price) * 1
};
```

## Lưu ý quan trọng

1. **Sản phẩm có variants**: `HasVariants = true`, giá và stock được quản lý ở variant level
2. **Sản phẩm không có variants**: `HasVariants = false`, giá và stock được quản lý ở product level
3. **SKU phải unique** cho cả Product và ProductVariant
4. **Cart và Order** cần lưu `ProductVariantId` nếu có variant được chọn
5. **Stock management**: Khi check stock, cần check ở variant level nếu có variant

## API Changes (cần implement sau)

Các API endpoints cần được cập nhật:
- `GET /api/products/{id}` - Trả về variants nếu có
- `POST /api/products` - Hỗ trợ tạo product với variants
- `PUT /api/products/{id}` - Hỗ trợ cập nhật variants
- `POST /api/cart` - Hỗ trợ thêm variant vào cart
- `POST /api/orders` - Hỗ trợ tạo order với variants

## Next Steps

1. ✅ Tạo entity ProductVariant
2. ✅ Cập nhật Product, CartItem, OrderItem
3. ✅ Tạo migration script
4. ⏳ Chạy migration trên database
5. ⏳ Cập nhật ProductService để hỗ trợ variants
6. ⏳ Cập nhật CartService để hỗ trợ variants
7. ⏳ Cập nhật OrderService để hỗ trợ variants
8. ⏳ Cập nhật DTOs và Controllers

