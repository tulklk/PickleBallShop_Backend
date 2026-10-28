# Secret rotation and git history cleanup

Old secrets are still present in the git history of this repository (17 commits touched `PickleBall/appsettings.json`,
and `bin/`, `obj/`, `.vs/` were committed). **Rotating the secrets is mandatory. Rewriting history is optional hygiene
and does not make leaked secrets safe by itself.**

Repository: `https://github.com/tulklk/PickleBallShop_Backend`. If it is public (or ever was), assume every secret has
already been harvested: rotate first, today.

## 1. Rotate (do this first)

Generate each new value, set it in the platform, redeploy, then revoke the old one.

| Secret | Where to rotate | Where to set |
|---|---|---|
| `Jwt__SecretKey` | `openssl rand -base64 48` (invalidates all access tokens; users log in again) | `fly secrets set Jwt__SecretKey=...` |
| Supabase DB password | Supabase dashboard, Project Settings, Database, reset password | `ConnectionStrings__DefaultConnection` |
| SMTP password | Google Account, Security, App passwords: revoke old, create new | `Email__SmtpPassword` |
| PayOS ClientId / ApiKey / ChecksumKey | PayOS dashboard (regenerate key set) | `Payment__PayOS__*` |
| MoMo AccessKey / SecretKey | MoMo business portal | `Payment__MoMo__*` |
| VNPay HashSecret | VNPay merchant portal | `Payment__VNPay__HashSecret` |
| Gemini API key | Google AI Studio: delete old key, create new | `Google__Gemini__ApiKey` |
| Google OAuth client secret | Google Cloud Console, Credentials | `Google__ClientSecret` |
| Seeded admin password | If any environment ever used the old seed (`Admin@123`): change it or delete the account | n/a |

Checklist
- [ ] New values set as secrets on Fly (`fly secrets set KEY=value`), never committed.
- [ ] Deploy; verify login, checkout, PayOS webhook and email still work.
- [ ] Old keys revoked/deleted at each provider.
- [ ] Check provider logs (Supabase, PayOS, SMTP, Gemini billing) for activity you do not recognise.
- [ ] Remove `ADMIN_SEED_*` variables after the first admin exists.

## 2. Stop it happening again
- [ ] Merge the `security/hardening` branch (`.gitignore`, blanked `appsettings.json`, `.env.example`).
- [ ] GitHub, Settings, Code security: enable **Secret scanning** and **Push protection**.
- [ ] CI already runs gitleaks on every push/PR (`.github/workflows/ci.yml`).

## 3. Optional: purge old values from history

Only after rotation. This rewrites every commit hash; everyone must re-clone afterwards, and open PRs/forks keep the old
history. GitHub may still serve old commits by hash until you contact support to purge caches.

```powershell
# 0. Work on a fresh mirror clone, never on your working copy
git clone --mirror https://github.com/tulklk/PickleBallShop_Backend.git pbs-mirror.git
cd pbs-mirror.git

# 1. Install git-filter-repo (https://github.com/newren/git-filter-repo), e.g.  pip install git-filter-repo

# 2. Remove the files that contained secrets / build output from ALL history
git filter-repo --invert-paths `
  --path PickleBall/appsettings.json `
  --path-glob "**/bin/*" --path-glob "**/obj/*" --path-glob ".vs/*" --path-glob "**/.vs/*"

# 3. Re-add the sanitised appsettings.json in a normal commit afterwards (it is blank in the new branch)

# 4. Verify nothing sensitive is left (replace with real fragments of OLD secrets, do not paste them anywhere)
git log --all -S"<fragment-of-old-secret>" --oneline

# 5. Push (this is the destructive step; the remote must allow force pushes)
git push --force --mirror
```

Alternative: BFG Repo-Cleaner (`bfg --delete-files appsettings.json --delete-folders "{bin,obj,.vs}"`).

After the rewrite
- [ ] Everyone re-clones; delete old local clones.
- [ ] Re-open the `security/hardening` work against the new history (or cherry-pick).
- [ ] Re-run CI.

The tool runs against your remote, so this repo's automation does **not** execute it for you.
