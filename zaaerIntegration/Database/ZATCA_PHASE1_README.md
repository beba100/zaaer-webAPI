# ZATCA Phase 1 — Foundation

## Apply schema

Run on each tenant database:

```sql
-- File: Database/ZatcaIntegration_Phase1.sql
```

Or let the API apply it automatically via `ZatcaIntegrationSchemaEnsurer` on first job run.

## Seller settings (`zatca_details`)

- `api_environment`: `sandbox` (start) → `simulation` (compliance) → `production`
- `device_uuid`: EGS unit UUID from Fatoora onboarding
- Tax number, CR, full national address fields (UBL requires structured address)

Example seller (from production reference):

| Field | Value |
|-------|--------|
| VAT | 300581803400003 |
| CR | 2050002700 |
| Company | شركة مجموعة السيوف التجارية |

## Standard vs Simplified

Resolved from `reservations.reservation_type`:

- `Corporate` → **standard** → clearance API
- `Individual` (or no reservation) → **simplified** → reporting API

## Job

```http
POST /api/jobs/ZatcaAutoSendJob/zatca-auto-send?apiKey={Jobs:ZatcaAutoSend:ApiKey}
```

`appsettings.json` → `Jobs:ZatcaAutoSend`

**Manual send (PMS):** set `Jobs:ZatcaAutoSend:Enabled` to `false` so the background worker does not auto-submit; operators send from reservation **الفواتير** via `POST /api/v1/pms/integrations/zatca/send-document`.

## Device onboarding (automatic)

PMS UI: **Integrations → ZATCA → Register device (OTP)**

API:

```http
POST /api/v1/pms/integrations/zatca/onboard
{ "otp": "123456", "apiEnvironment": "sandbox" }

POST /api/v1/pms/integrations/zatca/production-csid
GET  /api/v1/pms/integrations/zatca/device
```

Compliance CSID + secret + encrypted private key are saved in `zatca_devices` automatically (NuGet `Zatca.EInvoice` CSR + API client).

## Debit notes

Table `debit_notes` added for ZATCA compliance type (3) and (6). Required even if not used in daily operations.

## Phase 2 (next)

- `ZatcaUblBuilder` — UBL 2.1 + ECDSA + QR TLV
- `ZatcaComplianceService.RunAllSixAsync` — simulation compliance
- PMS hook: on invoice create → `zatca_status = pending`
