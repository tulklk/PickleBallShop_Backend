# Frontend changes required (Vercel app)

The backend hardening changes some API contracts. Deploy these frontend changes together with the backend, or the
affected flows will break. (The frontend repo was not part of this audit — these are derived from the API changes.)

## 1. New page: `/reset-password` (required — SEC-006)
Forgot-password no longer emails a password. It emails a link:

```
{Email:FrontendUrl}/reset-password?token=<token>&email=<email>
```

The page reads `token` and `email` from the query string, asks for the new password twice and calls:

```http
POST /api/auth/reset-password
{ "email": "...", "token": "...", "newPassword": "...", "confirmNewPassword": "..." }
```
- `200` → show success, redirect to login (all sessions were revoked).
- `400` with `message` → link invalid/expired/used or password too weak; offer "request a new link".
- Password rules: 8–128 characters, at least one letter and one digit, not a common password.
- `POST /api/auth/forgot-password` always answers the same success message; show it regardless of whether the account exists.

## 2. Guest order lookup (required — SEC-010)
- `GET /api/guest-orders/lookup?email=…&orderNumber=…` — **both** required. Without `orderNumber` it returns 400. Result is a list with **one** order (or 404).
- `GET /api/guest-orders/status/{orderNumber}?email=…` — email required.
- `GET /api/guest-orders/{id}?email=…` and `POST /api/guest-orders/{id}/payment?email=…` — email required.
- Remove any "list my orders by email" UI. Show the order number on the checkout success page and in the confirmation email so the customer can look it up.

## 3. Guest checkout / voucher payloads (SEC-004)
- Send only `{ productId, productVariantId?, quantity }` per item. Prices/totals from the client are ignored.
- Quantity must be 1–100, at most 50 lines, valid phone (`8–20` digits/`+ - ( )`), valid email, a valid `paymentMethod`.
- Display the **server-returned** totals; do not compute the payable amount in the browser.
- `POST /api/orders/apply-voucher` (logged-in) now only needs `{ code }`; the server prices the cart.
- `POST /api/guest-orders/apply-voucher` needs `{ email, code, items:[…] }`.

- Cart items now return a correct `unitPrice` (previously always `0`; `lineTotal` was already right). If the UI worked around the zero (for example by dividing `lineTotal`), switch to `unitPrice`.

## 4. Payment return pages (SEC-002, SEC-011)
- `/payments/payos/return` must **not** trust `status=PAID` from the URL. Call `POST /api/payments/payos/confirm` with `{ orderCode }` (include the `Authorization` header when the user is logged in) and show the result the API returns. Registered users' orders can only be confirmed by their owner.
- Handle `403` (not your order) and `404` (unknown order) gracefully.

## 5. Auth flows (SEC-016)
- `POST /api/auth/register` returns the same success message whether or not the email exists — show "check your email to verify". Do not auto-login after register.
- `verify-email` / `resend-verification` return generic messages; do not display "email not found / already verified".
- Login failures are generic ("email or password incorrect").
- Refresh tokens rotate: always store the **new** refresh token returned by `/api/auth/refresh`.
- After password change/reset or admin deactivation the old refresh tokens stop working — redirect to login on `401` from refresh.
- Handle `429` (rate limited): show "too many attempts" and respect the `Retry-After` header.

## 6. Reviews (SEC-008)
Public review objects no longer contain `phoneNumber` / `email`. Remove any UI that displays them. Review image URLs must be `http(s)`.

## 7. Profile (SEC-016)
`avatarUrl` must be an `http(s)` URL ≤ 2048 chars.

## 8. Removed endpoints
`/api/auth/test-email`, `/api/orders/test-email`, `/api/chat/test`, and Swagger in production. Remove any usage.

## 9. Frontend hardening to apply on Vercel (not testable from the backend repo)
Add to `vercel.json` (adjust the API origin):

```json
{
  "headers": [{
    "source": "/(.*)",
    "headers": [
      { "key": "X-Content-Type-Options", "value": "nosniff" },
      { "key": "X-Frame-Options", "value": "DENY" },
      { "key": "Referrer-Policy", "value": "strict-origin-when-cross-origin" },
      { "key": "Permissions-Policy", "value": "camera=(), microphone=(), geolocation=()" },
      { "key": "Strict-Transport-Security", "value": "max-age=63072000; includeSubDomains" },
      { "key": "Content-Security-Policy", "value": "default-src 'self'; img-src 'self' https: data:; connect-src 'self' https://<your-api-host>; frame-ancestors 'none'; base-uri 'self'; form-action 'self'" }
    ]
  }]
}
```
Also: never use `dangerouslySetInnerHTML` / `v-html` with product, review, comment or chat content; keep the access token in memory (or at least avoid exposing it to third-party scripts); do not ship any API keys in `NEXT_PUBLIC_*` / `VITE_*` variables except public identifiers (Google client ID); disable production source maps if they are not needed; check `npm audit`.
