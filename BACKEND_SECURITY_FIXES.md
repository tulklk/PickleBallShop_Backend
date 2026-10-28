# Backend security fixes — change log & operations guide

All fixes are in this repository. Details and evidence: `SECURITY_AUDIT.md`.

## Database migration
`PickleBall.Infrastructure/Migrations/20261028000000_SecurityHardening`
- `Orders.PayOSOrderCode` (bigint, unique index, nullable)
- `Users.PasswordResetTokenHash`, `Users.PasswordResetExpiresAt`
- Re-hashes existing refresh tokens to SHA-256 (existing sessions remain valid).

Apply before/with the deploy (`dotnet ef database update` or your normal startup migration).

## Configuration (environment variables; double underscore = `:`)

| Variable | Purpose |
|---|---|
| `Jwt__SecretKey` | ≥ 32 random chars. App refuses to start otherwise |
| `Jwt__Issuer`, `Jwt__Audience` | as before |
| `ConnectionStrings__DefaultConnection` | Supabase/Postgres |
| `ADMIN_SEED_EMAIL`, `ADMIN_SEED_PASSWORD` | first admin only; remove after first start |
| `Email__SmtpUsername/SmtpPassword/FromEmail`, `Email__FrontendUrl` | mail + reset-link base |
| `Cors__AllowedOrigins__0`, `FRONTEND_URL` | allowed browser origins |
| `Payment__PayOS__ClientId/ApiKey/ChecksumKey` | PayOS (empty ChecksumKey → webhooks rejected) |
| `Payment__MoMo__*`, `Payment__VNPay__*` | gateways (empty secret → callbacks rejected) |
| `Google__Gemini__ApiKey`, `Google__ClientId`, `Google__ClientSecret` | chat / Google sign-in |
| `Security__RateLimiting__Enabled` | default true |
| `Security__AuthStateCacheSeconds` | default 30; 0 = DB check on every request |
| `Security__TrustProxyHeaders` | default true (loopback + private + Fly `fdaa::/16`) |
| `Security__TrustedProxies` | extra proxy IPs (array) |

## Behaviour changes (API)
- Removed: `POST /api/auth/test-email`, `POST /api/orders/test-email`, `/api/chat/test`.
- Added: `POST /api/auth/reset-password`.
- Guest lookup now needs `orderNumber` + `email` and returns one order.
- `register` / `resend-verification` / `forgot-password` / `verify-email` return uniform messages.
- Review DTO without phone/email.
- `429` with `Retry-After` when rate limited.
- Swagger only in Development.

## Operational notes
- Fly proxy forwarding: if all clients appear to share one IP in logs, add the proxy address to `Security__TrustedProxies`.
- Rate limits are per process. Add edge limits when scaling out.
- AutoMapper was removed (advisory GHSA-rvv3-g6hj-g44x). DTO mapping is explicit in `PickleBall.Application/Mapping/DtoMappers.cs`; new DTO fields must be added there. `CartItemDto.UnitPrice` now carries the cart price snapshot (it was always 0 before).
- Optional DB-level concurrency tests: set `TEST_POSTGRES_CONNECTION` (local host only) and run `dotnet test --filter ConcurrencyPostgresTests`.
- Secret rotation / history cleanup: `SECRET_ROTATION_AND_HISTORY_CLEANUP.md`.
- `PickleBall.http` and `PickleBall.Migrate` were not modified.

## Tests
`dotnet test PickleBallShop.sln` → `tests/PickleBall.Tests` (104 tests + 2 PostgreSQL concurrency tests: access control, auth, orders, payments, platform headers/CORS/errors/Swagger, rate limiting, data exposure, password/crypto/seed/startup, DTO mapping).

CI (`.github/workflows/ci.yml`) runs build, tests (with a Postgres service), a NuGet vulnerability gate and gitleaks.
