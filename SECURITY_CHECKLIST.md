# SECURITY CHECKLIST — Pickleball Shop

Legend: ✅ done/verified (test or code) · ⚠️ partial / owner action · ➖ not applicable

## Deploy / operations (owner)
- [ ] ⚠️ **Rotate every secret that was in `appsettings.json` history**: JWT key, DB password, SMTP, PayOS (ClientId/ApiKey/ChecksumKey), MoMo, VNPay, Gemini, Google client secret. Step-by-step: `SECRET_ROTATION_AND_HISTORY_CLEANUP.md`.
- [ ] Enable GitHub secret scanning + push protection; confirm the first CI run is green (including the PostgreSQL concurrency tests).
- [ ] Merge branch `security/hardening` (not pushed yet).
- [ ] Set secrets only as Fly/Render/Vercel environment variables (see `PickleBall/.env.example`).
- [ ] `Jwt__SecretKey` ≥ 32 random chars (app refuses to start otherwise).
- [ ] Set `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` for the first start only, then **remove** them.
- [ ] Apply migration `20261028000000_SecurityHardening`.
- [ ] Set `Email__FrontendUrl` (reset-link base) and CORS allowed origins to the exact Vercel domain.
- [ ] Register the PayOS webhook URL `https://<api>/api/payments/payos/webhook` in the PayOS dashboard.
- [ ] Confirm Fly proxy addresses are trusted (`Security__TrustedProxies` if client IPs look wrong in logs).
- [ ] Optional: edge rate limit / WAF when running more than one instance.
- [ ] Deploy the frontend changes in `FRONTEND_CHANGES_REQUIRED.md`.

## Application controls
| # | Control | State |
|---|---|---|
| 1 | No secrets in repo / `.gitignore` / artefacts untracked | ✅ (rotation ⚠️) |
| 2 | JWT key fail-fast, HS256, validated issuer/audience/lifetime | ✅ |
| 3 | No default admin, env-only seed with policy | ✅ |
| 4 | Password policy + denylist, BCrypt, timing-equalised login | ✅ |
| 5 | Password reset via hashed, single-use, expiring token | ✅ |
| 6 | Refresh tokens hashed, rotated, revoked on credential/role/active change | ✅ |
| 7 | Access token re-checked against DB (active/role) | ✅ |
| 8 | Uniform responses (no account enumeration) | ✅ |
| 9 | Google login requires verified email | ✅ |
| 10 | Role not assignable via registration/mass assignment | ✅ |
| 11 | Admin endpoints require Admin role; paging clamped | ✅ |
| 12 | Self-deletion of admin blocked | ✅ |
| 13 | Orders: per-user ownership enforced | ✅ |
| 14 | Guest orders require order number + email | ✅ |
| 15 | Server-side pricing, quantity limits, stock atomicity | ✅ |
| 16 | Voucher validated/consumed server-side, transactional | ✅ |
| 17 | Payments: only gateway-signed or server-verified state; amount/currency checked | ✅ |
| 18 | Webhook/IPN idempotent and monotonic | ✅ |
| 19 | Empty gateway secrets rejected | ✅ |
| 20 | Rate limiting on auth, guest, voucher, chat, payment-confirm, writes | ✅ |
| 21 | Forwarded headers trusted only from proxies | ✅ |
| 22 | Generic error responses, no stack traces | ✅ |
| 23 | Public DTOs free of PII / hashes / tokens | ✅ |
| 24 | HTML-encoded email templates, validated links | ✅ |
| 25 | Security headers, `no-store` | ✅ |
| 26 | CORS allow-list (origins, methods, headers) | ✅ |
| 27 | Swagger disabled outside Development | ✅ |
| 28 | Debug/test endpoints removed | ✅ |
| 29 | Input validators (auth, orders, cart, chat, profile, reviews) | ✅ |
| 30 | Container runs as non-root | ✅ |
| 31 | Dependency audit clean (MailKit/MimeKit upgraded, AutoMapper removed) | ✅ |
| 35 | CI: build, tests, NuGet advisory gate, gitleaks, Dependabot | ✅ (first run to be confirmed) |
| 36 | Explicit DTO mapping (no convention mapping that could leak new entity fields) | ✅ |
| 37 | Stock/voucher concurrency tests on PostgreSQL | ⚠️ in CI; not yet observed passing |
| 32 | CSRF | ➖ bearer tokens, no auth cookies |
| 33 | File upload / SSRF / path traversal / raw SQL / command exec | ➖ none present |
| 34 | Automated regression tests (104 + 2 PostgreSQL) | ✅ |

## Recurring
- [ ] `dotnet list package --vulnerable --include-transitive` monthly / in CI.
- [ ] `dotnet test` in CI on every PR (`tests/PickleBall.Tests`).
- [ ] Review new endpoints for: auth attribute, ownership check, validator, rate-limit policy, DTO without PII.
