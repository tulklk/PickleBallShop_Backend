# Chat / SignalR trên production (Vercel + Fly.io)

## Đã xử lý phía backend

1. **`UseForwardedHeaders`** – Fly.io gửi `X-Forwarded-Proto: https`. Không có middleware này, app có thể nghĩ request là `http` và gây lỗi với auth/SignalR.
2. **`UnauthorizedAccessException` → 401** – Trước đây một số lỗi quyền bị bọc thành **500**; FE hiểu nhầm là “server lỗi”.
3. **`GetMessagesAsync`** – Tránh truy vấn EF phức tạp (double `OrderBy`) và xử lý **Sender** null.

## Việc bạn cần kiểm tra trên Fly.io

- **`ConnectionStrings__DefaultConnection`** – Chuỗi PostgreSQL (Supabase) đúng, có thể cần `SSL Mode=Require` tùy pooler.
- **`Jwt__SecretKey`** – Trùng với cấu hình FE dùng khi gọi API (ít nhất 32 ký tự). Không trùng secret → token fail.
- **`FRONTEND_URL`** (tùy chọn) – Ví dụ `https://pickleball-shop-frontend.vercel.app` để CORS thêm origin mà không cần sửa `appsettings.json`.

## CORS

- FE production phải khớp **chính xác** một origin trong `Cors:AllowedOrigins` hoặc `Email:FrontendUrl` / `FRONTEND_URL`.
- Mỗi **preview** Vercel (`xxx.vercel.app`) là origin **khác** – cần thêm vào CORS hoặc chỉ test trên URL production chính.

## Sau khi deploy

Redeploy backend (`fly deploy`), hard refresh FE, đăng nhập lại để lấy JWT mới nếu đổi secret.
