# EnterpriseHotelPms — Publish Checklist (Sprints 1–6)

Use this checklist when deploying to MonsterASP or any production host.

## 1. Build artifact

```powershell
dotnet publish zaaerIntegration/zaaerIntegration.csproj -c Release -o release-publish --no-self-contained
```

Upload the contents of `release-publish/` (not the old `publish-out/` folder).

## 2. Environment variables (MonsterASP)

| Variable | Required | Notes |
|----------|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `ConnectionStrings__MasterDb` | Yes | Master SQL connection |
| `TenantDatabase__Server` | Yes | Tenant SQL server |
| `TenantDatabase__UserId` | Yes | |
| `TenantDatabase__Password` | Yes | |
| `Jwt__SecretKey` | Yes | 32+ chars; rotate forces re-login |
| `Cors__AllowedOrigins__0` | Yes | e.g. `https://al3oery.tryasp.net` |
| `IntegrationSecrets__MasterKey` | Yes | |
| `DevExtreme__LicenseKey` | Yes | |
| `ResortTickets__QrSigningKey` | If resort | |
| `Jobs__ZatcaAutoSend__ApiKey` | If ZATCA job | |
| `Jobs__NumberingAuditReconciliation__ApiKey` | If numbering job | |
| `Jobs__AllowedCallerIps__0` | Optional | Restrict job callers by IP |
| `WhatsApp__*` | Optional | |

Secrets must **not** be committed to `appsettings.json` in production.

## 3. Master DB — SQL scripts (run once if not already applied)

Run in order on **Master DB**:

1. `Database/HybridRbac_MasterDB.sql` (if RBAC tables missing)
2. `Database/HybridRbac_SeedPermissions.sql`
3. `Database/AddEnterpriseSessionManagement.sql` — **Sprint 1 sessions**
4. `Database/AddLegacyCrossTenantReportsPermission.sql` — **Sprint 4B**
5. `Database/SyncNavMenuSystemPermissions.sql` (if nav menus updated)
6. Grant `admin.legacy_reports.view` to admin roles that need legacy cross-tenant reports

## 4. Tenant DBs — optional performance (Sprint 5)

- `Database/AddPmsCriticalPerformanceIndexes.sql` per tenant, or
- `Database/Tenant_ApplyPmsCriticalPerformanceIndexes_AllDatabases.sql`

## 5. Post-deploy verification

- [ ] Login (new JWT + session `sid`/`sv` required)
- [ ] Room board loads + hotel picker
- [ ] Guest / corporate pickers (`/api/v1/pms/customers`)
- [ ] Reservation detail save
- [ ] Hotel reports hub
- [ ] Legacy `/api/Expense` returns **410** in Production
- [ ] `/api/reports-test` returns **404** in Production
- [ ] ZATCA job URL still works with API key (if scheduled)

## 6. Sprint completion summary

| Sprint | Status |
|--------|--------|
| 1 Enterprise sessions | Included |
| 2 Security (RBAC, JWT, CORS, HotelScope) | Included |
| 3A VoM removal | Included |
| 3B Zaaer/Queue removal | Included |
| 3C PMS API migration | Included |
| 4A Critical security fixes | Included |
| 4B Legacy lockdown + Jobs hardening | Included |
| 5 Performance (cache + slow logging) | Included |
| 6 PMS UI (pms-grid-compact) | Included |
| 7 Security hardening (sessions, CSP, XSS) | Included |

## 8. Sprint 7 — security notes (post-deploy)

- Changing a user's assigned hotels **revokes all their sessions** — they must log in again.
- `guest-lookup` is rate-limited (default 20/min per IP) — tune via `Security__GuestLookupRateLimitPerMinute`.
- Production responses include **CSP**, **HSTS**, **X-Frame-Options** — DevExtreme requires inline scripts (configured in middleware).
- After deploy, users with old JWT after hotel reassignment will be forced to re-login (expected).

## 7. Git branches

- **main** — production-ready (merged from `feature/security-hardening`)
- `feature/enterprise-session-management` — already merged into security-hardening; no separate deploy needed
