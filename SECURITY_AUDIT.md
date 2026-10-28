# SECURITY AUDIT — Pickleball Shop

| | |
|---|---|
| Date | 2026-10-28 |
| Scope | ASP.NET Core 8 backend (`e:\Web\PickleBallShop_backend\PickleBallShop`), production frontend `https://pickleball-shop-frontend.vercel.app` (owner-authorised) |
| Method | White-box code review, local black-box tests against the real pipeline (xUnit + `WebApplicationFactory`, EF InMemory), dependency audit |
| Constraints respected | No DoS, brute force, data mutation or mass creation on production. Every exploit was reproduced **locally / by automated test**, never against production. |
| Result | 22 findings (4 Critical, 7 High, 8 Medium, 3 Low). All 22 are FIXED in source; 1 still needs **owner action** (SEC-001 secret rotation, see `SECRET_ROTATION_AND_HISTORY_CLEANUP.md`). 104 regression tests pass (2 PostgreSQL concurrency tests run when a database is provided and in CI); Release build has 0 errors; `dotnet list package --vulnerable` is clean. |

> **Production was not probed.** The deployed Fly.io API was not tested (blocked by the safety rules for the
> engagement). Everything marked FIXED is fixed **in the repository** and verified locally; it is not live until you
> deploy. See "Manual production verification" at the end.

---

## 1. Attack surface

| Area | Entry points |
|---|---|
| Public API | `/api/auth/*`, `/api/products*`, `/api/categories*`, `/api/vouchers*`, `/api/guest-orders/*`, `/api/chat`, product reviews/comments, `/api/payments/{momo,vnpay,payos}/*` (return, IPN, webhook, confirm) |
| Authenticated | `/api/users/*`, `/api/orders/*`, `/api/cart/*`, wishlist, reviews |
| Admin | `/api/admin/*` (`AdminOrders`, `AdminUsers`, products, vouchers …) |
| Realtime | SignalR hubs (JWT via `access_token` query string) |
| External | PayOS, MoMo, VNPay, Google Sign-In, Gemini, SMTP, Supabase Postgres |
| Infra | Fly.io (`fly.toml`, `Dockerfile`), Render (`render.yaml`) |

Not applicable (checked): CSRF (no cookie auth, bearer tokens only), file upload, SSRF, path traversal, raw SQL / command
execution sinks (none found; all data access is EF Core LINQ), prototype pollution / ReDoS (backend only; no user-supplied
regex), SQL injection.

Trust model that the fixes enforce: **the browser is never trusted for money, price, stock, identity, role or payment state.**

---

## 2. Findings summary

| ID | Title | Severity | Status |
|---|---|---|---|
| SEC-001 | Production secrets committed in `appsettings.json` | Critical | FIXED in repo — **NEEDS SECRET ROTATION** |
| SEC-002 | PayOS return/confirm marks orders paid from browser parameters | Critical | FIXED |
| SEC-003 | Hard-coded default admin credentials in seed | Critical | FIXED — **rotate if ever deployed** |
| SEC-004 | Guest checkout trusted client prices / unvalidated input | Critical | FIXED |
| SEC-005 | Unauthenticated `test-email` endpoints (mail relay / info leak) | High | FIXED |
| SEC-006 | Forgot-password emailed a plaintext password; enumeration | High | FIXED |
| SEC-007 | Stock / voucher race conditions, negative or huge quantities | High | FIXED |
| SEC-008 | Reviewer phone and email exposed publicly | High | FIXED |
| SEC-009 | `bin/`, `obj/`, `.vs/` and build artefacts committed | Medium | FIXED |
| SEC-010 | Guest order lookup by email only (list all orders; IDOR) | High | FIXED |
| SEC-011 | Payment callbacks: no amount/idempotency/signature rigor, empty secrets accepted | High | FIXED |
| SEC-012 | No rate limiting on any endpoint | High | FIXED |
| SEC-013 | `X-Forwarded-For` trusted from anyone (IP spoofing) | Medium | FIXED |
| SEC-014 | Exception messages / stack details leaked to clients | Medium | FIXED |
| SEC-015 | Refresh tokens stored in clear; no revocation; stale role/active state in JWT | Medium | FIXED |
| SEC-016 | Account enumeration, unverified Google email, verify-email activated accounts | Medium | FIXED |
| SEC-017 | Swagger exposed in production | Medium | FIXED |
| SEC-018 | HTML injection in transactional emails | Medium | FIXED |
| SEC-019 | Missing security headers, API responses cacheable | Low | FIXED |
| SEC-020 | Over-permissive CORS (any header/method) | Low | FIXED |
| SEC-021 | Vulnerable dependencies (MailKit/MimeKit, AutoMapper) | Medium | FIXED |
| SEC-022 | Container ran as root; chat endpoint unbounded (cost abuse) | Low | FIXED |

---

## 3. Findings

### [SEC-001] Production secrets committed in `appsettings.json`
- **Severity:** Critical — **CVSS 9.8** (AV:N/AC:L/PR:N/UI:N/C:H/I:H/A:H) — **CWE-798, CWE-540** — **OWASP A02/A05**
- **Affected:** `PickleBall/appsettings.json` (and git history)
- **Prerequisites:** read access to the repo (or any leak of it)
- **Problem:** JWT signing key, DB connection string, SMTP password, PayOS/MoMo/VNPay keys, Gemini and Google keys were stored in the tracked file.
- **Impact:** forge any JWT (including admin), read/write the database, mint valid payment signatures, send mail as the shop.
- **Evidence:** values present in the file prior to the fix (not reproduced here).
- **Root cause:** secrets in source-controlled config.
- **Fix:** all values blanked; `.env.example` documents required variables; `.gitignore` hardened; startup **fails fast** if `Jwt:SecretKey` is missing, short (<32) or a placeholder (`UnitSecurityTests`).
- **Status:** **FIXED in repo. NEEDS SECRET ROTATION** — the old values live in git history and must be treated as compromised: JWT key, Supabase DB password, SMTP password, PayOS ClientId/ApiKey/ChecksumKey, MoMo and VNPay secrets, Gemini key, Google client secret. Rewriting history alone is not sufficient.

### [SEC-002] PayOS return/confirm marks orders paid from browser parameters
- **Severity:** Critical — **CVSS 9.1** — **CWE-345, CWE-602** — **OWASP A04/A08**
- **Affected:** `PaymentsController.PayOSReturn`, `PayOSConfirm`, `OrderService`
- **Prerequisites:** none (anonymous); knowledge of an order identifier
- **Problem:** the redirect/confirm path accepted `code=00&status=PAID` from the query/body as proof of payment.
- **Impact:** get goods without paying.
- **Evidence:** `PaymentSecurityTests.Forged_payos_return_redirect_does_not_mark_the_order_paid`, `Forged_payos_confirm_does_not_mark_the_order_paid` (fail before, pass after).
- **Root cause:** client-supplied state treated as authoritative.
- **Fix:** return/confirm now only use the order code as a hint and ask PayOS server-to-server (`GetPaymentInfoAsync`) for the real state; order is paid only if PayOS says `PAID` **and** the amount equals `Order.TotalAmount`. `confirm` enforces ownership (registered orders refuse anonymous/other users). Orders get a server-generated, persisted `PayOSOrderCode` (migration `20261028000000_SecurityHardening`).
- **Status:** FIXED

### [SEC-003] Hard-coded default admin credentials
- **Severity:** Critical — **CVSS 9.8** — **CWE-798, CWE-1392** — **OWASP A07**
- **Affected:** `PickleBall.Infrastructure/Data/SeedData.cs`
- **Problem:** an admin user with a well-known password (`Admin@123`) was created on every start if none existed.
- **Impact:** full admin takeover of any fresh/empty deployment.
- **Fix:** no default account. Admin is created only from `ADMIN_SEED_EMAIL` + `ADMIN_SEED_PASSWORD` (env), only if no admin exists, password must satisfy `PasswordPolicy` and is not in the common-password denylist; BCrypt-hashed. Tests: `StartupAndSeedTests`.
- **Status:** FIXED. If any environment ever ran the old seed, **change that admin password / delete the account now.**

### [SEC-004] Guest checkout trusted client data
- **Severity:** Critical — **CVSS 8.6** — **CWE-472, CWE-20** — **OWASP A04**
- **Affected:** `GuestOrdersController`, `OrderService.CreateGuestOrderAsync`, voucher preview
- **Problem:** prices/totals/quantities from the request were used; no validators for orders, addresses, quantities.
- **Impact:** buy at arbitrary price, negative quantity to reduce totals or inflate stock, oversized payloads.
- **Evidence:** `OrderLogicTests.Guest_order_total_is_computed_by_the_server_and_ignores_client_prices`, `Guest_order_rejects_invalid_quantities_and_does_not_touch_stock` (−1, 0, 101, int.Max), `Duplicate_lines_are_merged_and_the_merged_quantity_is_capped`, `Inactive_products_cannot_be_ordered`.
- **Fix:** DTOs carry only product/variant/quantity; server re-prices from the catalog, merges duplicate lines, caps quantity (1..100), items (1..50), validates address/phone/email/payment method (`OrderRequestValidators`). Voucher preview (guest and cart) re-prices server-side.
- **Status:** FIXED

### [SEC-005] Unauthenticated `test-email` endpoints
- **Severity:** High — **CVSS 7.5** — **CWE-749, CWE-306** — **OWASP A01/A05**
- **Affected:** `AuthController`, `OrdersController`, `ChatController` (`/test`)
- **Impact:** spam relay through the shop's SMTP, reputation damage, config disclosure.
- **Fix:** endpoints deleted. Test: `AuthSecurityTests.Debug_test_email_*`.
- **Status:** FIXED

### [SEC-006] Forgot-password emailed a plaintext password
- **Severity:** High — **CVSS 8.1** — **CWE-640, CWE-522** — **OWASP A07**
- **Affected:** `AuthService.ForgotPasswordAsync`, `AuthController`, `IEmailService`
- **Problem:** a new password was generated and mailed in clear; any person could trigger it for any email (account lock-out) and response differed for unknown accounts.
- **Fix (per your decision):** token-link flow. 32-byte random token, only its **SHA-256** is stored with a 30-minute expiry, single use, constant-time compare; new endpoint `POST /api/auth/reset-password`; 1-minute throttle per account; uniform response; all refresh tokens revoked on reset. Tests: `AuthSecurityTests` (reset, replay, wrong token, other user's token, expiry, no enumeration).
- **Status:** FIXED — **needs the frontend `/reset-password` page** (see `FRONTEND_CHANGES_REQUIRED.md`).

### [SEC-007] Stock / voucher races and bad quantities
- **Severity:** High — **CVSS 7.5** — **CWE-362, CWE-840** — **OWASP A04**
- **Problem:** read-modify-write on stock and voucher usage let concurrent checkouts oversell or exceed voucher limits.
- **Fix:** stock decrement is a single guarded `UPDATE … WHERE Stock >= qty` (`ExecuteUpdateAsync`), voucher usage consumed inside a transaction using `CreateExecutionStrategy()`; both fall back safely on non-relational providers.
- **Evidence:** `OrderLogicTests.Cannot_order_more_than_the_stock_and_stock_is_left_unchanged`. The true concurrency guarantee depends on PostgreSQL; the InMemory provider cannot prove it (honest limitation — verify on staging with parallel requests).
- **Status:** FIXED

### [SEC-008] Reviewer phone / email publicly exposed
- **Severity:** High — **CVSS 7.5** — **CWE-359, CWE-200** — **OWASP A01**
- **Fix:** `ReviewDto` no longer contains `PhoneNumber`/`Email`. Test: `DataExposureTests.Public_review_listing_never_exposes_reviewer_phone_or_email`. User payloads never contain hash/reset token (`User_payload_never_contains_password_hash_or_reset_token`).
- **Status:** FIXED. Optional hygiene: stop collecting those columns (still stored).

### [SEC-009] Build artefacts committed
- **Severity:** Medium — **CWE-200, CWE-540**
- **Fix:** `.gitignore` rewritten; `bin/`, `obj/`, `.vs/` untracked; main project excludes nested projects from compile (fixed 1291 duplicate-type warnings).
- **Status:** FIXED. Old binaries remain in history (they may embed old config — covered by SEC-001 rotation).

### [SEC-010] Guest order lookup by email (IDOR / enumeration)
- **Severity:** High — **CVSS 7.5** — **CWE-639, CWE-200** — **OWASP A01**
- **Problem:** anyone knowing an email could list that customer's orders (names, addresses, phones).
- **Fix (per your decision):** lookup needs **order number + email**, returns exactly one order. `status/{orderNumber}`, `GET {id}` and `POST {id}/payment` also require the matching email (case-insensitive, guest orders only). Order numbers are non-sequential. Tests: `Guest_lookup_requires_both_*`, `Guest_order_status_and_detail_require_the_matching_email`, `Guest_payment_cannot_be_initiated_for_somebody_elses_order`, `Order_numbers_are_not_sequential_or_guessable`.
- **Status:** FIXED

### [SEC-011] Payment callback hardening (MoMo / VNPay / PayOS webhook)
- **Severity:** High — **CVSS 8.2** — **CWE-347, CWE-367, CWE-345**
- **Problem:** signatures compared non-constant-time, empty secrets accepted, amounts not checked, replays and late callbacks could change state, unsigned envelope fields trusted.
- **Fix:** constant-time signature comparison (`CryptoUtil.SignatureEquals`), gateways **refuse** callbacks when their secret is not configured, only **signed** fields are used (`verifiedData.Code`), amount and currency must equal the order total, monotonic idempotent transitions (Paid/Refunded never downgraded, cancelled orders never resurrected). Webhook always replies 200 so the gateway does not retry forever. Tests: `PaymentSecurityTests` (wrong signature, tampered amount, unsigned envelope flip, replay, cancelled order, unconfigured MoMo/VNPay secrets).
- **Status:** FIXED. A signed *failure* webhook can still set `PaymentStatus=Failed` on a pending order; that is intended and a later signed success can still pay it.

### [SEC-012] No rate limiting
- **Severity:** High — **CWE-307, CWE-770** — **OWASP A04/A07**
- **Fix:** `Microsoft.AspNetCore.RateLimiting` fixed-window per client IP: login 10/min, register 5/10 min, forgot/reset/resend 5/15 min, voucher 30/min, guest lookup 20/min, guest order 10/10 min, chat 15/min, writes 20/10 min, payment confirm 30/min, global 600/min. 429 with `Retry-After`. Tests: `RateLimitTests`.
- **Status:** FIXED. Limits are per instance (in-memory); with several Fly machines add an edge/WAF limit (NEEDS INFRA CHANGE, optional).

### [SEC-013] Spoofable client IP (`X-Forwarded-For`)
- **Severity:** Medium — **CWE-348**
- **Fix:** forwarded headers are accepted only from loopback/private proxy ranges (and `Security:TrustedProxies`); `ForwardLimit=1`. Test: `Spoofed_forwarded_for_header_does_not_bypass_the_limit` (TestServer now presents a public remote IP so the test mirrors Kestrel).
- **Status:** FIXED. Confirm on Fly that the proxy address falls in the trusted ranges (Fly uses `fdaa::/16`, included) — otherwise rate limiting would key on the proxy address.

### [SEC-014] Verbose errors
- **Severity:** Medium — **CWE-209, CWE-497**
- **Fix:** global handler returns generic messages, `error` details only in Development; framework exception messages are never echoed; controllers (`UsersController`, payments) return fixed text. Tests: `PlatformSecurityTests.Unhandled_exceptions_*`, `Framework_exception_messages_*`, `Malformed_json_returns_a_clean_400`, `Payos_confirm_error_responses_do_not_echo_internals`.
- **Status:** FIXED

### [SEC-015] Refresh-token and session-state weaknesses
- **Severity:** Medium — **CWE-922, CWE-613**
- **Fix:** refresh tokens stored as SHA-256 (migration rehashes existing rows), rotation on use, revoked on password change/reset, deactivation and role change; refresh refused for deactivated users; JWT `OnTokenValidated` re-checks `IsActive` and role in the DB (cached `Security:AuthStateCacheSeconds`, default 30s, 0 disables). Tests: `AccessControlTests.Access_token_of_deactivated_user_is_rejected_immediately`, `Access_token_is_rejected_when_the_role_changed_after_issuance`, `Forged_jwt_*`, `AuthSecurityTests.Refresh_*`.
- **Status:** FIXED. Residual: a revoked access token stays valid up to the cache TTL (≤30 s) and up to its natural expiry only if the DB check is disabled.

### [SEC-016] Account enumeration and weak identity checks
- **Severity:** Medium — **CWE-203, CWE-287, CWE-640**
- **Fix:** register, login, forgot, resend and verify return uniform responses/timing (dummy BCrypt for unknown users); emails normalised to lower case; role always `Customer` on registration (mass-assignment test); Google login requires `email_verified`, refuses re-linking to a different Google account; `verify-email` no longer re-activates deactivated accounts; password policy 8–128 chars, letter + digit, common-password denylist, applied to register/change/reset/seed. Tests: `AuthSecurityTests`, `AccessControlTests.Role_cannot_be_escalated_by_mass_assignment`, `UnitSecurityTests`.
- **Status:** FIXED. Policy is intentionally modest (no symbol requirement); raise `MinLength` to 10–12 if desired.

### [SEC-017] Swagger in production
- **Severity:** Medium — **CWE-200** — **Fix:** enabled only in Development. Test: `Swagger_is_not_exposed_in_production`. **Status:** FIXED

### [SEC-018] HTML injection in emails
- **Severity:** Medium — **CWE-79 / CWE-80**
- **Fix:** every user-controlled value in templates is HTML-encoded; image and reset links must be http(s) URLs. SMTP failures no longer leak server details.
- **Status:** FIXED

### [SEC-019] Security headers / caching
- **Severity:** Low — **Fix:** `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Resource-Policy`, CSP `default-src 'none'; frame-ancestors 'none'` (not on `/swagger`), HSTS (non-dev, https), `Cache-Control: no-store` on API responses. Test: `Responses_carry_security_headers_and_are_not_cacheable`. **Status:** FIXED
- The Vercel **frontend** headers (CSP etc.) were not in scope of this repo — see frontend doc.

### [SEC-020] Over-permissive CORS
- **Severity:** Low — **Fix:** explicit origins, methods (`GET/POST/PUT/PATCH/DELETE/OPTIONS`) and headers only; credentials only for listed origins; `null` origin refused. Tests: `Cors_only_allows_the_configured_frontend_origin`, `Unsupported_http_methods_are_rejected`. **Status:** FIXED
- Make sure `Cors:AllowedOrigins` (or equivalent) in production lists exactly your Vercel domain(s).

### [SEC-021] Vulnerable dependencies
- **Severity:** Medium
- **Fix:** MailKit and MimeKit upgraded to 4.18.2.
- **AutoMapper 12.0.1 — GHSA-rvv3-g6hj-g44x (high, DoS via deeply nested mapping)**: fixed upstream only in 15.x (commercial licence); 14.0.0 did not clear the advisory. AutoMapper was therefore **removed**. All entity→DTO mapping is now explicit code in `PickleBall.Application/Mapping/DtoMappers.cs` behind a small `IMapper` facade (`AppMapper`), with no reflection-based convention mapping: a field reaches the API only if it is written in a mapper, which also makes PII leaks (SEC-008) structurally harder. 10 `MappingTests` cover every mapping used by the services (secrets never serialised, image/variant/spec rules, order and cart totals, voucher create/update, list mapping, unknown mapping fails loudly).
- **Result:** `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages; CI fails the build if one appears.
- **Behaviour note:** `CartItemDto.UnitPrice` is now filled from the cart price snapshot. Under AutoMapper naming conventions it was never mapped (always `0`) while `LineTotal` was correct.
- **Status:** FIXED

### [SEC-022] Container / chat hardening
- **Severity:** Low — **Fix:** `USER app` in both Dockerfiles; chat input bounded (message ≤1000, history ≤50, content ≤4000, rate-limited), missing Gemini key returns a generic message instead of a configuration error. Tests: `Chat_rejects_oversized_messages_and_does_not_reveal_configuration`. **Status:** FIXED

---

## 4. Verification

| Check | Result |
|---|---|
| `dotnet build PickleBallShop.sln -c Release` | Succeeded, 0 errors |
| `dotnet test -c Release` | **104 passed, 0 failed, 2 skipped** (the 2 skipped are the PostgreSQL concurrency tests; they need `TEST_POSTGRES_CONNECTION`) |
| PostgreSQL concurrency tests (SEC-007) | Written; run automatically in CI against a `postgres:16` service container. **Not executed locally** (Docker daemon was not running and no database credentials were available). Check the first CI run. |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages |
| CI (`.github/workflows/ci.yml`) | Build, test, NuGet advisory gate, gitleaks on every push/PR, weekly schedule; Dependabot for NuGet, Docker, Actions |
| Static review of sinks | No raw SQL, `Process.Start`, file I/O on user paths, outbound requests with user URLs |

Test suite: `tests/PickleBall.Tests` — `AccessControlTests`, `AuthSecurityTests`, `OrderLogicTests`, `PaymentSecurityTests`, `PlatformSecurityTests`, `RateLimitTests`, `DataExposureTests`, `UnitSecurityTests`, `MappingTests`, `ConcurrencyPostgresTests`.

Run the concurrency tests locally (they create and drop a throwaway database, and refuse non-local hosts unless
`TEST_POSTGRES_ALLOW_REMOTE=1`):

```powershell
$env:TEST_POSTGRES_CONNECTION = "Host=localhost;Port=5432;Username=postgres;Password=<yours>;Database=postgres"
dotnet test tests/PickleBall.Tests --filter ConcurrencyPostgresTests
```

## 5. Residual risk / not testable here

1. **Secret rotation** (SEC-001) — owner action, highest priority. Steps and the optional history purge are in `SECRET_ROTATION_AND_HISTORY_CLEANUP.md`; CI gitleaks and GitHub push protection prevent recurrence.
2. **Concurrency of stock/voucher** is guaranteed by SQL; the PostgreSQL tests exist but have not been observed passing yet (see Verification).
3. **Rate limits are per instance.**
4. **Real gateway behaviour** (PayOS lookup, MoMo, VNPay signatures) was tested with local signatures/fakes, not against the live providers.
5. **Frontend** (Vercel) was not part of this repo; items in `FRONTEND_CHANGES_REQUIRED.md` are required for the new flows to work and for header hardening.
6. **Reviewer PII still stored.** Reviews keep `PhoneNumber` (required) and `Email` columns although the API never returns them. Dropping them is an irreversible data change and needs a frontend change (the review form), so it was deliberately not done; decide whether the shop still needs them.
7. Database migration `20261028000000_SecurityHardening` must be applied (adds `PayOSOrderCode`, password-reset columns, rehashes refresh tokens — **existing sessions stay valid**, they are hashed, not revoked).

## 6. Manual production verification (after deploy)

Do these yourself on your own site, one request each, no loops:

```text
GET  https://<api>/swagger                         -> 404
GET  https://<api>/api/products                    -> headers: X-Content-Type-Options, X-Frame-Options, Cache-Control: no-store
POST https://<api>/api/auth/test-email             -> 404 / 405
POST https://<api>/api/auth/forgot-password        -> same 200 body for a real and a fake email
GET  https://<api>/api/guest-orders/lookup?email=x -> 400 (needs orderNumber)
OPTIONS with Origin: https://evil.example           -> no Access-Control-Allow-Origin
```
Then rotate secrets, log in with the new admin from `ADMIN_SEED_*`, remove those variables, and place one real test order through PayOS to confirm the webhook marks it paid.
