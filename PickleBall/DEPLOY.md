# Hướng dẫn Deploy lên Render

## Cách 1: Sử dụng Dockerfile (Khuyến nghị)

### Bước 1: Tạo Web Service trên Render

1. Đăng nhập vào [Render Dashboard](https://dashboard.render.com)
2. Click "New +" → "Web Service"
3. Kết nối repository GitHub của bạn
4. Chọn repository `PickleBallShop_Backend`

### Bước 2: Cấu hình Build Settings

- **Name**: `PickleBallShop_Backend` (hoặc tên bạn muốn)
- **Environment**: `Docker`
- **Region**: Chọn region gần bạn nhất
- **Branch**: `main` (hoặc branch bạn muốn deploy)
- **Root Directory**: Để trống hoặc `PickleBall` (nếu code ở trong thư mục con)
- **Dockerfile Path**: `PickleBall/Dockerfile` (nếu root directory là `PickleBall`) hoặc `Dockerfile` (nếu ở root)
- **Docker Context**: Để trống hoặc `.`

### Bước 3: Cấu hình Environment Variables

Thêm các biến môi trường sau trong phần "Environment":

#### Database Connection
```
ConnectionStrings__DefaultConnection=Host=your-supabase-host.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password;SSL Mode=Require;Trust Server Certificate=true
```

#### JWT Settings
```
Jwt__SecretKey=YourSuperSecretKeyForJWTTokenGenerationMustBeAtLeast32CharactersLong!
Jwt__Issuer=PickleBallShop
Jwt__Audience=PickleBallShop
Jwt__ExpiryMinutes=60
```

#### MoMo Payment (nếu sử dụng)
```
Payment__MoMo__PartnerCode=YOUR_MOMO_PARTNER_CODE
Payment__MoMo__AccessKey=YOUR_MOMO_ACCESS_KEY
Payment__MoMo__SecretKey=YOUR_MOMO_SECRET_KEY
Payment__MoMo__Endpoint=https://test-payment.momo.vn/v2/gateway/api/create
Payment__MoMo__ReturnUrl=https://your-render-url.onrender.com/api/payments/momo/return
Payment__MoMo__IpnUrl=https://your-render-url.onrender.com/api/payments/momo/ipn
```

#### VNPay Payment (nếu sử dụng)
```
Payment__VNPay__TmnCode=YOUR_VNPAY_TMN_CODE
Payment__VNPay__HashSecret=YOUR_VNPAY_HASH_SECRET
Payment__VNPay__Endpoint=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
Payment__VNPay__ReturnUrl=https://your-render-url.onrender.com/api/payments/vnpay/return
Payment__VNPay__IpnUrl=https://your-render-url.onrender.com/api/payments/vnpay/ipn
```

#### ASP.NET Core Settings
```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:10000
```

**Lưu ý**: Render tự động set biến `PORT`, ứng dụng sẽ tự động sử dụng port đó.

### Bước 4: Deploy

1. Click "Create Web Service"
2. Render sẽ tự động build và deploy
3. Đợi quá trình build hoàn tất (thường mất 5-10 phút)
4. Sau khi deploy thành công, bạn sẽ có URL dạng: `https://your-app-name.onrender.com`

## Cách 2: Sử dụng Build Command (Không dùng Docker)

Nếu không muốn dùng Docker, bạn có thể cấu hình như sau:

### Build Settings:
- **Environment**: `Node` hoặc `Docker` (Render sẽ tự detect)
- **Build Command**: 
  ```
  cd PickleBall && dotnet restore && dotnet publish -c Release -o ./publish
  ```
- **Start Command**: 
  ```
  cd PickleBall && dotnet ./publish/PickleBall.dll
  ```

## Cách 3: Sử dụng render.yaml (Infrastructure as Code)

1. File `render.yaml` đã được tạo sẵn trong project
2. Trên Render Dashboard, chọn "New +" → "Blueprint"
3. Kết nối repository và chọn file `render.yaml`
4. Render sẽ tự động tạo service với cấu hình từ file

## Chạy Migrations sau khi Deploy

Sau khi deploy thành công, bạn cần chạy migrations để tạo database schema:

### Option 1: Sử dụng Render Shell
1. Vào service → "Shell"
2. Chạy lệnh:
   ```bash
   cd PickleBall
   dotnet ef database update --project PickleBall.Infrastructure --startup-project PickleBall
   ```

### Option 2: Tạo Database Migration Script
Tạo một script migration riêng hoặc chạy migrations tự động khi app start (không khuyến nghị cho production).

## Troubleshooting

### Lỗi: "Dockerfile not found"
- Đảm bảo Dockerfile nằm đúng vị trí
- Kiểm tra "Root Directory" và "Dockerfile Path" trong cấu hình

### Lỗi: "Connection string not found"
- Kiểm tra environment variable `ConnectionStrings__DefaultConnection` đã được set chưa
- Đảm bảo format đúng với double underscore `__`

### Lỗi: "Port binding failed"
- Render tự động set PORT, không cần config thủ công
- Đảm bảo ứng dụng listen trên port từ environment variable

### Lỗi: "Database connection failed"
- Kiểm tra Supabase connection string
- Đảm bảo Supabase database đã được tạo và accessible
- Kiểm tra firewall settings trên Supabase

## Kiểm tra Deployment

Sau khi deploy thành công:
1. Truy cập: `https://your-app-name.onrender.com/swagger`
2. Kiểm tra API endpoints hoạt động
3. Xem logs trong Render Dashboard nếu có lỗi

## Lưu ý quan trọng

1. **Free Tier**: Render free tier sẽ spin down sau 15 phút không hoạt động, request đầu tiên sau khi spin down sẽ mất ~50 giây
2. **Environment Variables**: Không commit sensitive data vào git, chỉ dùng environment variables trên Render
3. **Database**: Đảm bảo Supabase database đã được setup và migrations đã chạy
4. **HTTPS**: Render tự động cung cấp HTTPS certificate

