# Hướng dẫn cấu hình VietQR cho thanh toán chuyển khoản

## 1. Đăng ký tài khoản VietQR

1. Truy cập https://vietqr.io
2. Đăng ký tài khoản tại https://my.vietqr.io
3. Lấy `Client ID` và `API Key` từ dashboard

## 2. Cấu hình trong appsettings.json

```json
"Payment": {
  "VietQR": {
    "ClientId": "YOUR_VIETQR_CLIENT_ID",
    "ApiKey": "YOUR_VIETQR_API_KEY",
    "Endpoint": "https://api.vietqr.io/v2/generate",
    "AccountNo": "YOUR_BANK_ACCOUNT_NUMBER",
    "AccountName": "TEN_CHU_TAI_KHOAN",
    "AcqId": 970432,
    "Template": "compact2"
  }
}
```

### Giải thích các trường:

- **ClientId**: Client ID từ VietQR
- **ApiKey**: API Key từ VietQR
- **Endpoint**: URL API tạo mã QR (mặc định: https://api.vietqr.io/v2/generate)
- **AccountNo**: Số tài khoản ngân hàng nhận tiền (6-19 ký tự số)
- **AccountName**: Tên chủ tài khoản (viết IN HOA, không dấu, 5-50 ký tự)
- **AcqId**: Mã BIN ngân hàng (xem danh sách bên dưới)
- **Template**: Mẫu QR code trả về

### Mã BIN các ngân hàng phổ biến:

| Mã BIN | Ngân hàng |
|--------|-----------|
| 970415 | VietinBank |
| 970436 | Vietcombank |
| 970418 | BIDV |
| 970405 | Agribank |
| 970432 | VPBank |
| 970423 | TPBank |
| 970407 | Techcombank |
| 970422 | MB Bank |
| 970416 | ACB |
| 970448 | OCB |
| 970437 | HDBank |
| 970441 | VIB |
| 970443 | SHB |
| 970403 | Sacombank |
| 970440 | SeABank |

### Template options:

| Giá trị | Size | Mô tả |
|---------|------|-------|
| compact2 | 540x640 | Mã QR, các logo, thông tin chuyển khoản |
| compact | 540x540 | QR kèm logo VietQR, Napas, ngân hàng |
| qr_only | 480x480 | Chỉ ảnh QR đơn giản |
| print | 600x776 | Mã QR, các logo và đầy đủ thông tin |

## 3. Cấu hình trên Render (Production)

Thêm biến môi trường:

```
Payment__VietQR__ClientId=your_client_id
Payment__VietQR__ApiKey=your_api_key
Payment__VietQR__AccountNo=your_account_number
Payment__VietQR__AccountName=YOUR_ACCOUNT_NAME
Payment__VietQR__AcqId=970432
```

## 4. Sử dụng API

### Tạo đơn hàng với thanh toán chuyển khoản:

```json
POST /api/orders
{
  "paymentMethod": 3,
  "shippingAddress": {
    "fullName": "Nguyen Van A",
    "phoneNumber": "0901234567",
    "addressLine": "123 Nguyen Hue",
    "ward": "Ben Nghe",
    "district": "Quan 1",
    "city": "Ho Chi Minh"
  }
}
```

### Response:

```json
{
  "order": {
    "id": "...",
    "orderNumber": "ORD-20251126-ABC12345",
    "totalAmount": 500000,
    ...
  },
  "payment": {
    "orderId": "...",
    "orderNumber": "ORD-20251126-ABC12345",
    "paymentUrl": null,
    "isCod": false,
    "isBankTransfer": true,
    "bankTransferInfo": {
      "qrDataUrl": "data:image/png;base64,...",
      "qrCode": "...",
      "bankName": "VPBank",
      "accountNo": "1234567890",
      "accountName": "NGUYEN VAN A",
      "amount": 500000,
      "transferContent": "DH20251126ABC12345"
    }
  }
}
```

### Hiển thị QR trên frontend:

```html
<img src="${bankTransferInfo.qrDataUrl}" alt="QR Code" />

<p>Ngân hàng: ${bankTransferInfo.bankName}</p>
<p>Số tài khoản: ${bankTransferInfo.accountNo}</p>
<p>Chủ tài khoản: ${bankTransferInfo.accountName}</p>
<p>Số tiền: ${bankTransferInfo.amount.toLocaleString()}đ</p>
<p>Nội dung: ${bankTransferInfo.transferContent}</p>
```

## 5. PaymentMethod Enum

| Value | Name | Mô tả |
|-------|------|-------|
| 0 | COD | Thanh toán khi nhận hàng |
| 1 | MoMo | Thanh toán qua MoMo |
| 2 | VNPay | Thanh toán qua VNPay |
| 3 | BankTransfer | Chuyển khoản ngân hàng (VietQR) |

## 6. Lưu ý

- **Nội dung chuyển khoản**: Hệ thống tự động tạo nội dung theo format `DH{OrderNumber}` (max 25 ký tự)
- **Xác nhận thanh toán**: Hiện tại cần admin xác nhận thủ công sau khi nhận được tiền. Để tự động, cần tích hợp webhook từ ngân hàng/VietQR.
- **AccountName**: Phải viết IN HOA, không dấu, khớp với tên đăng ký tài khoản ngân hàng

