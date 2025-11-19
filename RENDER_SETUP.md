# 🚀 Hướng dẫn Deploy lên Render - Quick Start

## Bước 1: Push code lên GitHub
```bash
git add .
git commit -m "Add Dockerfile and deployment config"
git push origin main
```

## Bước 2: Tạo Web Service trên Render

1. Vào [Render Dashboard](https://dashboard.render.com)
2. Click **"New +"** → **"Web Service"**
3. Kết nối GitHub repository: `tulklk/PickleBallShop_Backend`
4. Chọn repository

## Bước 3: Cấu hình Build Settings

### Thông tin cơ bản:
- **Name**: `PickleBallShop_Backend`
- **Environment**: `Docker` ⚠️ **QUAN TRỌNG: Chọn Docker**
- **Region**: Chọn region gần bạn (ví dụ: Singapore)
- **Branch**: `main`
- **Root Directory**: Để **TRỐNG** (không điền gì)
- **Dockerfile Path**: `Dockerfile` (hoặc để trống)
- **Docker Context**: Để **TRỐNG**

### ⚠️ LƯU Ý QUAN TRỌNG:
- **KHÔNG** điền Root Directory
- **KHÔNG** điền Dockerfile Path nếu Dockerfile ở root
- Chọn **Environment = Docker**

## Bước 4: Cấu hình Environment Variables

Thêm các biến sau trong tab **"Environment"**:

### 1. Database (Supabase)
```
ConnectionStrings__DefaultConnection
```
Giá trị: `Host=your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password;SSL Mode=Require;Trust Server Certificate=true`

### 2. JWT
```
Jwt__SecretKey
```
Giá trị: Một chuỗi bí mật dài ít nhất 32 ký tự (ví dụ: `MySuperSecretKeyForJWTTokenGeneration123456789!`)

```
Jwt__Issuer
```
Giá trị: `PickleBallShop`

```
Jwt__Audience
```
Giá trị: `PickleBallShop`

```
Jwt__ExpiryMinutes
```
Giá trị: `60`

### 3. ASP.NET Core
```
ASPNETCORE_ENVIRONMENT
```
Giá trị: `Production`

**Lưu ý**: Render tự động set `PORT`, không cần config thủ công.

### 4. Payment Gateways (Tùy chọn)
Nếu sử dụng MoMo hoặc VNPay, thêm các biến tương ứng (xem DEPLOY.md)

## Bước 5: Deploy

1. Click **"Create Web Service"**
2. Đợi build (5-10 phút)
3. Sau khi thành công, bạn sẽ có URL: `https://pickleballshop-backend.onrender.com`

## Bước 6: Chạy Migrations

Sau khi deploy thành công:

1. Vào service → Tab **"Shell"**
2. Chạy lệnh:
```bash
cd PickleBall
dotnet ef database update --project PickleBall.Infrastructure --startup-project PickleBall
```

**Lưu ý**: Cần cài EF Core Tools trước:
```bash
dotnet tool install --global dotnet-ef
```

## Kiểm tra Deployment

1. Truy cập: `https://your-app-name.onrender.com/swagger`
2. Nếu thấy Swagger UI → ✅ Thành công!

## Troubleshooting

### ❌ Lỗi: "Dockerfile not found"
**Giải pháp**: 
- Kiểm tra Dockerfile có ở root repository không
- Đảm bảo "Root Directory" để TRỐNG
- Kiểm tra "Dockerfile Path" để TRỐNG hoặc `Dockerfile`

### ❌ Lỗi: "Connection string not found"
**Giải pháp**: 
- Kiểm tra environment variable `ConnectionStrings__DefaultConnection` đã được set
- Đảm bảo dùng double underscore `__` (không phải `:`)

### ❌ Lỗi: "Port binding failed"
**Giải pháp**: 
- Render tự động set PORT, không cần config
- Đảm bảo ứng dụng listen trên port từ environment variable

### ❌ Build thành công nhưng app không chạy
**Giải pháp**:
- Kiểm tra logs trong Render Dashboard
- Đảm bảo database connection string đúng
- Kiểm tra migrations đã chạy chưa

## 📝 Checklist trước khi Deploy

- [ ] Code đã được push lên GitHub
- [ ] Dockerfile ở root repository
- [ ] Environment variables đã được set
- [ ] Supabase database đã được tạo
- [ ] Connection string đã được test

## 🎉 Sau khi Deploy thành công

1. Test API endpoints qua Swagger
2. Test authentication flow
3. Test database connection
4. Monitor logs trong Render Dashboard

---

**Cần hỗ trợ?** Xem file `DEPLOY.md` để biết thêm chi tiết.

