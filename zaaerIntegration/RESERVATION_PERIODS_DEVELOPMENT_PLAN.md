# خطة تطوير `reservation_periods` — تحويل يومي ↔ شهري بدون المساس بالماضي

> **الهدف:** حجز واحد (REV0001) يمكن أن يمر بعدة فترات تسعير (يومي / شهري / …) مع الحفاظ على الفترات المُقفَلة والـ day rates التاريخية، وتوليد الليالي المستقبلية فقط من الفترة النشطة.

> **الحالة (2026-06-06):** Phase 0–3 backend + API + JS service hooks **مُنفَّذ**. Phase 4 UI popup **قادم**.

| Phase | الحالة |
|-------|--------|
| 0 | ✅ enum, feature flag, SQL status check |
| 1 | ✅ GET/POST initial, periods في detail DTO |
| 2 | ✅ POST append + day rate regen + rollup |
| 3 | ✅ PATCH rental guard |
| 4 | ⏳ UI popup |
| 6 | ⏳ backfill SQL (script in plan) |

---

## 1. مبادئ التصميم (لا تُخالَف)

| # | المبدأ |
|---|--------|
| P1 | **`reservation_periods` = خطة تسعير** — ليس حجزاً جديداً ولا استبدالاً لـ `reservation_units`. |
| P2 | **`reservation_unit_day_rates` = دفتر التنفيذ** — المحاسبة، الفواتير، التقارير، Zaaer. |
| P3 | **إغلاق الماضي:** فترة `Closed` لا تُعدَّل `from_date` / `to_date` / `gross_rate` إلا بصلاحية admin + audit. |
| P4 | **توليد انتقائي:** عند append/update فترة → حذف/إعادة توليد day rates **داخل نطاق الفترة النشطة فقط** (أو المستقبلية)، مع **عدم لمس** ليالي الفترات `Closed`. |
| P5 | **`reservations.rental_type`** = **الوضع الحالي / الفترة النشطة** (mirror للـ UX)، وليس «نوع الحجز التاريخي الوحيد». |
| P6 | **Toggle يومي/شهري في الهيدر** يُعطَّل أو يُحوَّل إلى «إضافة فترة» بعد Phase 3 — لا يعيد تسعير الحجز بالكامل. |
| P7 | **KSA dates:** تواريخ الفترة `date` فقط (بدون UTC shift) — نفس قواعد `KsaTime` و `yyyy-MM-dd` في الفرونت. |

---

## 2. نموذج البيانات (تأكيد + تحسينات طفيفة)

### 2.1 جدول موجود — `reservation_periods`

```
period_id, reservation_id, unit_id?, rental_type, from_date, to_date,
gross_rate, tax_included, status, created_at, updated_at
```

### 2.2 قيم `status` (توحيد في enum + DB check)

| Status | المعنى |
|--------|--------|
| `Active` | الفترة الحالية — يُسمح بتمديد `to_date` وتوليد day rates |
| `Closed` | منتهية — read-only للتسعير |
| `Cancelled` | أُلغيت قبل التطبيق (خطأ موظف) — لا day rates |

### 2.3 قيم `rental_type` (storage)

`daily` | `monthly` | `yearly` | `inhour` — نفس `NormalizeRentalTypeForStorage` في `ReservationDetailService`.

### 2.4 SQL إضافي (Phase 0)

- `Database/AlterReservationPeriods_AddStatusCheck.sql` — CHECK على status
- `Database/AlterReservationPeriods_AddPeriodNo.sql` (اختياري) — `period_seq int` لترتيب الفترات على نفس الحجز/الوحدة
- `Database/BackfillReservationPeriodsFromExisting.sql` — سكربت backfill (Phase 6)

### 2.5 علاقة بالوحدات

- **`unit_id`** = `reservation_units.unit_id` (PK) — **ليس** `apartment_id`.
- فترة بدون `unit_id` = تطبق على **كل** وحدات الحجز (حجز single-unit شائع).
- Multi-unit: فترة لكل `unit_id` أو policy «فترة واحدة لكل الوحدات» — **Phase 1: single-unit + optional null unit_id**؛ Phase 5: multi-unit صريح.

### 2.6 Day rates — مفتاح التوليد

- احترام `GetDayRateReservationIdRefs` / `DayRateRowMatchesUnit` الموجودين في `ReservationDetailService`.
- **Monthly period:** سطر واحد (أو lump) على `from_date` — نفس `BuildDayRateItems` الحالي.
- **Daily period:** سطر لكل ليلة `[from_date, to_date)` hotel nights.

---

## 3. معمارية الخدمات

```
ReservationDetailController
    └── IReservationPeriodService          ← جديد (أو methods على IReservationDetailService)
            ├── GetPeriodsAsync
            ├── AppendPeriodAsync          ← السيناريو الرئيسي (monthly → daily)
            ├── CloseActivePeriodAsync
            └── RegenerateDayRatesFromPeriodsAsync
    └── ReservationDetailService (existing)
            ├── GetByZaaerOrReservationIdAsync  ← + periods في DTO
            ├── PatchReservationAsync           ← guard: لا rental_type wholesale regen
            └── SaveUnitDayRatesAsync           ← respect Closed period nights
```

**ملفات جديدة مقترحة:**

| ملف | الغرض |
|-----|--------|
| `Services/Interfaces/IReservationPeriodService.cs` | عقد الفترات |
| `Services/Implementations/ReservationPeriodService.cs` | منطق append/close/regenerate |
| `Utilities/ReservationPeriodDayRateGenerator.cs` | pure functions: periods → day rate rows |
| `DTOs/Pms/ReservationDetail/ReservationPeriodDtos.cs` | DTOs |
| `Controllers/Pms/ReservationPeriodsController.cs` | REST (أو endpoints على ReservationDetailController) |
| `Database/BackfillReservationPeriodsFromExisting.sql` | migration بيانات |

---

## 4. مراحل التطوير

### Phase 0 — تجهيز (½ يوم)

**Deliverables**

- [ ] تشغيل `CreateReservationPeriodsTable.sql` على كل tenant DB (إن لم يُشغَّل)
- [ ] إضافة `ReservationPeriodStatus` enum + constants
- [ ] توثيق status values في `ReservationPeriod.cs` XML
- [ ] Feature flag في `appsettings.json`: `PmsFeatures:ReservationPeriodsEnabled` (default `false`)

**Acceptance:** build ينجح؛ الجدول موجود؛ flag يُقرأ من config.

---

### Phase 1 — Core backend: قراءة + إنشاء فترة أولى (1–2 يوم)

**1.1 DTOs**

```csharp
ReservationPeriodDto
ReservationPeriodAppendRequestDto   // rentalType, fromDate, toDate, newCheckOut?, grossRate?, unitId?
ReservationPeriodListResponseDto
```

**1.2 API**

| Method | Route | وصف |
|--------|-------|-----|
| GET | `/api/v1/pms/reservations/{id}/periods` | قائمة الفترات مرتبة |
| POST | `/api/v1/pms/reservations/{id}/periods/initial` | إنشاء Period 1 من الحجز الحالي (draft/backfill helper) |

**1.3 Service logic — `CreateInitialPeriodFromReservation`**

- Input: reservation + units
- Output: period واحدة `Active` أو `Closed` إذا `check_out < today`
- `rental_type` من `reservations.rental_type`
- `from_date` = check-in date
- `to_date` = check-out date (date only)
- `gross_rate` = `reservation_units.total_amount` أو sum day rates

**1.4 GET detail**

- `ReservationDetailDto` + `IReadOnlyList<ReservationPeriodDto> Periods`
- `ReservationDetailDateDto` + `HasMixedRentalPeriods bool`, `ActivePeriodRentalType`

**Acceptance:** GET periods يرجع فترة واحدة بعد initial؛ لا يغيّر day rates.

---

### Phase 2 — Append period (السيناريو: شهري → يومي) (2–3 أيام)

**2.1 API**

```
POST /api/v1/pms/reservations/{id}/periods/append
Body: {
  rentalType: "daily",
  fromDate: "2026-06-04",      // default: activePeriod.toDate + 1
  toDate: "2026-06-10",        // أو newCheckOutDate
  grossRate: null,             // default من room_type_rates
  unitId: null,
  closePreviousPeriod: true    // default true
}
```

**2.2 Algorithm `AppendPeriodAsync` (transaction)**

1. Load reservation + units؛ validate hotel scope + permissions (`ReservationPermissionGuard`).
2. Load periods؛ find active period(s).
3. If `closePreviousPeriod`: set previous `Active` → `Closed` (لا تغيير dates/rate).
4. Validate `fromDate` = previous `to_date + 1 day` (أو allow gap with flag `allowGapDays` + audit note).
5. Resolve `grossRate` (daily: nightly gross؛ monthly: monthly rate from room_type_rates).
6. Insert new period `Active`.
7. Extend `reservations.check_out_date` + `reservation_units.check_out` to `toDate` (+ وقت المغادرة 18:00 KSA).
8. Set `reservations.rental_type` = new period rental type.
9. Call `RegenerateDayRatesForPeriodAsync(newPeriod)` — **only** nights in range.
10. `FinalizeReservationTotalsWithExtrasAsync`.
11. Activity log / `reservation_notes` auto note: «تحويل من شهري إلى يومي من 04/06/2026».

**2.3 `RegenerateDayRatesForPeriodAsync`**

```
FOR each night in period range:
  IF exists day_rate for (unit, night) AND night covered by Closed period:
    SKIP (never delete)
  ELSE IF night in Active period range:
    UPSERT day rate from period rules
DELETE stale day_rates WHERE night in Active period range only AND not is_manual AND not in Closed coverage
```

**2.4 Monthly lump rule**

- إذا `rental_type == monthly`: upsert **one** row on `from_date` with full `gross_rate` (EWA/VAT split).

**Acceptance (REV0001):**

- Period 1: monthly 04/05–03/06, 2000, Closed
- Period 2: daily 04/06–10/06, nightly rate
- Day rate 04/05/2000 **unchanged**
- New rows 04/06..09/06 (nights)
- Total = 2000 + (nights × daily)

---

### Phase 3 — حماية المسارات القديمة (1–2 يوم)

**3.1 `PatchReservationAsync`**

- إذا `ReservationPeriodsEnabled` ويوجد periods:
  - تغيير `rentalType` في PATCH **مرفوض** أو ي require `usePeriodAppend=true`
  - تغيير `checkOut` بدون append **يوسّع** الفترة النشطة فقط

**3.2 `SaveUnitDayRatesAsync`**

- رفض edit/delete لـ night_date داخل `Closed` period (409 + message i18n key)
- Allow manual override داخل `Active` period

**3.3 `ZaaerReservationService.GenerateDayRatesAsync`**

- إذا periods exist → delegate to `ReservationPeriodDayRateGenerator`
- Else → legacy `BuildDayRateItems` (backward compatible)

**3.4 Frontend guard**

- `reservation-detail.js`: عند `periods.length > 0` — disable rental ButtonGroup toggle؛ show badge «فترات متعددة»
- Redirect to «إضافة فترة» popup

**Acceptance:** toggling rental type لا يمس Closed pricing.

---

### Phase 4 — UI «إضافة فترة إيجار» (2–3 أيام)

**4.1 Popup `openAppendRentalPeriodPopup`**

- Fields: rental type, from date (read-only default), to date / nights, gross preview, unit (if multi)
- Preview: جدول mini لليالي الجديدة + delta على الإجمالي
- i18n: `reservationDetail.periods.*` في ar.js / en.js

**4.2 Section في تفاصيل الحجز (optional tab)**

- Grid compact: period #, type, from, to, gross, status
- Actions: append (if last closed), view day rates

**4.3 Service JS**

- `reservation-detail-service.js`: `getPeriods`, `appendPeriod`
- Cache bust reservation-detail.js

**Acceptance:** موظف ينفّذ REV0001 scenario من UI بدون SQL.

---

### Phase 5 — Multi-unit + unit switch (2 يوم)

- Append period per `unit_id` عند multi-room
- `ZaaerReservationUnitSwitchService`: on switch — close period at effective date + new period on new unit
- Room board: show «mixed» indicator if periods differ by unit

---

### Phase 6 — Backfill + rollout (1–2 يوم)

**6.1 SQL backfill**

```sql
-- لكل reservation بدون periods:
INSERT reservation_periods (...)
SELECT reservation_id, NULL, rental_type, check_in_date, check_out_date, total_amount, ...
FROM reservations r
WHERE NOT EXISTS (SELECT 1 FROM reservation_periods p WHERE p.reservation_id = r.reservation_id)
  AND r.status NOT IN ('cancelled', ...)
```

- Monthly: gross من unit total أو day rate lump
- Mark `Closed` if check_out < today

**6.2 Rollout**

1. Deploy backend + flag `false`
2. Run backfill per tenant
3. Enable flag per hotel / globally
4. Monitor logs: `RegenerateDayRatesForPeriod`

**6.3 `.http` tests**

- `RESERVATION_PERIODS_TEST.http` — append monthly→daily, assert totals

---

### Phase 7 — Zaaer / Queue / Reports (1–2 يوم، optional)

- Queue handler: sync periods to Zaaer if contract exists
- Invoice PDF: show period breakdown if mixed
- Balady stay calculator: use period rental type per date range

---

## 5. API Summary (نهائي)

| Method | Route |
|--------|-------|
| GET | `/api/v1/pms/reservations/{id}/periods` |
| POST | `/api/v1/pms/reservations/{id}/periods/initial` |
| POST | `/api/v1/pms/reservations/{id}/periods/append` |
| POST | `/api/v1/pms/reservations/{id}/periods/{periodId}/close` |
| POST | `/api/v1/pms/reservations/{id}/periods/regenerate-day-rates` (admin) |

---

## 6. اختبارات (minimum)

| Test | نوع |
|------|-----|
| Append daily after closed monthly preserves lump row | Integration |
| Append rejects overlapping dates | Unit |
| Closed period night → SaveUnitDayRates 409 | Integration |
| Regenerate skips Closed nights | Unit |
| Total roll-up = sum day rates + extras | Integration |
| fromDate = toDate previous + 1 default | Unit |

---

## 7. مخاطر و mitigations

| Risk | Mitigation |
|------|------------|
| `ReplaceRatesAsync` wipes all | **Never** call full replace when periods enabled |
| reservation_id vs zaaer_id | Reuse `GetDayRateReservationIdRefs` everywhere |
| unit_id vs apartment_id in day rates | Reuse `DayRateRowMatchesUnit` |
| 3 أيام gap (متأخر) | `fromDate` default = last period end + 1؛ optional `allowGapDays` |
| Toggle rental in UI | Disable when periods.length > 0 |
| is_manual on auto rows | Set `is_manual = false` for generated; true only on manual popup edit |

---

## 8. ترتيب التنفيذ الم recommended

```
Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 6 → Phase 5 → Phase 7
                              ↑
                         REV0001 يعمل هنا (API)
```

**MVP للإنتاج:** Phase 0–4 + Phase 6 backfill = **~8–12 يوم عمل**.

---

## 9. مثال REV0001 (مرجع QA)

| Step | Action |
|------|--------|
| 1 | Backfill Period 1: monthly, 2026-05-04 → 2026-06-03, gross 2000, Closed |
| 2 | POST append: daily, 2026-06-04 → 2026-06-10, gross from daily rate |
| 3 | Verify day_rates: 1 row 2026-05-04 @ 2000 + 6 nightly rows |
| 4 | Verify reservation total & balance |
| 5 | UI shows 2 periods; rental type = daily |

---

## 10. Checklist قبل Merge

- [ ] Feature flag documented
- [ ] KsaTime on all timestamps
- [ ] Permission: `reservations.pricing.edit` or existing guard extended
- [ ] i18n AR/EN
- [ ] No UNIQUE constraint changes on zaaer_id
- [ ] Transaction wraps append + day rates + totals
- [ ] Logging: period_id, reservation_id, night range regenerated

---

*آخر تحديث: 2026-06-06 — aligned with existing `ReservationPeriod` model, `ReservationDetailService` day-rate pipeline, and PMS reservation-detail UI.*
