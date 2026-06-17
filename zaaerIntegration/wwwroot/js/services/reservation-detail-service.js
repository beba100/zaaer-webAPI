(function (window) {
    "use strict";

    const base = "/api/v1/pms/reservations";

    /**
     * jQuery may leave the body as a string if Content-Type is wrong, or callers may pass text.
     */
    function parseJsonIfNeeded(raw) {
        if (raw == null || typeof raw !== "string") {
            return raw;
        }

        const s = raw.replace(/^\uFEFF/, "").trim();
        if (!s) {
            return null;
        }

        try {
            return JSON.parse(s);
        } catch {
            return raw;
        }
    }

    /**
     * Normalize ASP.NET / jQuery payloads: { data }, { Data }, or nested envelope; support PascalCase DTOs.
     */
    function unwrapPayload(raw) {
        let cur = parseJsonIfNeeded(raw);
        if (cur == null) {
            return null;
        }

        if (typeof cur !== "object" || Array.isArray(cur)) {
            return cur;
        }

        for (let i = 0; i < 3; i += 1) {
            if (cur == null || typeof cur !== "object") {
                break;
            }

            const next =
                cur.data !== undefined
                    ? cur.data
                    : cur.Data !== undefined
                      ? cur.Data
                      : null;

            if (next == null) {
                break;
            }

            cur = next;
        }

        return cur;
    }

    function pick(obj, camelKey) {
        if (!obj || typeof obj !== "object") {
            return undefined;
        }

        const pascal = camelKey.charAt(0).toUpperCase() + camelKey.slice(1);
        return obj[camelKey] !== undefined ? obj[camelKey] : obj[pascal];
    }

    function mapHeader(h) {
        const x = h || {};
        return {
            reservationNo: pick(x, "reservationNo") ?? "",
            source: pick(x, "source") ?? null,
            mainGuestName: pick(x, "mainGuestName") ?? null,
            actualArrival: pick(x, "actualArrival") ?? null,
            status: pick(x, "status") ?? ""
        };
    }

    function mapGeneral(g) {
        const x = g || {};
        return {
            reservationType: pick(x, "reservationType") ?? "",
            visitPurposeId: pick(x, "visitPurposeId"),
            visitPurposeName: pick(x, "visitPurposeName"),
            visitPurposeNameAr: pick(x, "visitPurposeNameAr"),
            source: pick(x, "source"),
            cmBookingNo: pick(x, "cmBookingNo")
        };
    }

    function mapDates(dt) {
        const x = dt || {};
        return {
            rentalType: pick(x, "rentalType") ?? "",
            checkInDate: pick(x, "checkInDate"),
            checkOutDate: pick(x, "checkOutDate"),
            departureDate: pick(x, "departureDate"),
            numberOfMonths: pick(x, "numberOfMonths"),
            totalNights: pick(x, "totalNights"),
            monthlyCalendarMode: pick(x, "monthlyCalendarMode"),
            isAutoExtend: pick(x, "isAutoExtend"),
            reservationDate: pick(x, "reservationDate")
        };
    }

    function mapFinancial(f) {
        const x = f || {};
        return {
            balanceAmount: pick(x, "balanceAmount"),
            totalAmount: pick(x, "totalAmount"),
            amountPaid: pick(x, "amountPaid"),
            subtotal: pick(x, "subtotal"),
            totalTaxAmount: pick(x, "totalTaxAmount"),
            totalExtra: pick(x, "totalExtra"),
            totalPenalties: pick(x, "totalPenalties"),
            totalDiscounts: pick(x, "totalDiscounts")
        };
    }

    function mapDiscountLine(d) {
        const x = d || {};
        return {
            discountId: pick(x, "discountId"),
            unitId: pick(x, "unitId"),
            unitLabel: pick(x, "unitLabel"),
            applyScope: pick(x, "applyScope") ?? "",
            applyOn: pick(x, "applyOn") ?? "",
            calculationMethod: pick(x, "calculationMethod"),
            calculationValue: pick(x, "calculationValue"),
            discountAmount: pick(x, "discountAmount"),
            description: pick(x, "description"),
            appliedDate: pick(x, "appliedDate"),
            isActive: pick(x, "isActive") !== false
        };
    }

    function mapDiscountMutationResult(data) {
        const x = data || {};
        const discountsRaw = pick(x, "discounts");
        return {
            discount: mapDiscountLine(pick(x, "discount")),
            discounts: Array.isArray(discountsRaw) ? discountsRaw.map(mapDiscountLine) : [],
            financial: mapFinancial(pick(x, "financial"))
        };
    }

    function mapNote(n) {
        const x = n || {};
        const attachmentPath = pick(x, "attachmentPath");
        return {
            noteId: pick(x, "noteId"),
            noteType: pick(x, "noteType") ?? "internal",
            noteText: pick(x, "noteText") ?? "",
            attachmentPath: attachmentPath || null,
            attachmentOriginalName: pick(x, "attachmentOriginalName") || null,
            attachmentContentType: pick(x, "attachmentContentType") || null,
            attachmentFileSize: pick(x, "attachmentFileSize"),
            hasAttachment: pick(x, "hasAttachment") === true || !!attachmentPath,
            createdByUserId: pick(x, "createdByUserId"),
            createdByDisplayName: pick(x, "createdByDisplayName"),
            createdAt: pick(x, "createdAt"),
            updatedAt: pick(x, "updatedAt"),
            canEdit: pick(x, "canEdit") === true,
            canDelete: pick(x, "canDelete") === true
        };
    }

    function buildReservationNoteFormData(payload, file, options) {
        const opts = options || {};
        const fd = new FormData();
        fd.append("reservationId", String(payload.reservationId));
        if (payload.hotelId != null && payload.hotelId !== "") {
            fd.append("hotelId", String(payload.hotelId));
        }
        fd.append("noteType", payload.noteType || "internal");
        fd.append("noteText", payload.noteText != null ? String(payload.noteText) : "");
        if (opts.removeAttachment) {
            fd.append("removeAttachment", "true");
        }
        if (file) {
            fd.append("attachment", file);
        }
        return fd;
    }

    function mapNotesList(data) {
        const x = data || {};
        if (Array.isArray(x)) {
            const notes = x.map(mapNote);
            return { count: notes.length, notes };
        }

        const nestedList = pick(x, "list");
        if (nestedList && typeof nestedList === "object" && !Array.isArray(nestedList)) {
            return mapNotesList(nestedList);
        }

        const notesRaw = pick(x, "notes");
        const notes = Array.isArray(notesRaw) ? notesRaw.map(mapNote) : [];
        const countRaw = pick(x, "count");
        return {
            count: countRaw != null && countRaw !== "" ? Number(countRaw) : notes.length,
            notes
        };
    }

    function unwrapNotesMutation(data) {
        const x = data || {};
        const listPayload = pick(x, "list");
        const list = listPayload != null ? mapNotesList(listPayload) : mapNotesList(x);
        return {
            note: mapNote(pick(x, "note")),
            list
        };
    }

    function mapPricingTax(p) {
        const x = p || {};
        if (!x || typeof x !== "object") {
            return null;
        }

        const vr = x.vatRate ?? x.VatRate;
        const er = x.ewaRate ?? x.EwaRate;
        if (vr == null && er == null) {
            return null;
        }

        return {
            vatRate: Number(vr) || 0,
            ewaRate: Number(er) || 0,
            vatTaxIncluded: (x.vatTaxIncluded ?? x.VatTaxIncluded) !== false,
            lodgingTaxIncluded: (x.lodgingTaxIncluded ?? x.LodgingTaxIncluded) !== false
        };
    }

    function mapUnitDayRateSummary(s) {
        const x = s || {};
        return {
            vatRate: pick(x, "vatRate") ?? 0,
            ewaRate: pick(x, "ewaRate") ?? 0,
            taxIncluded: pick(x, "taxIncluded") !== false,
            subtotal: pick(x, "subtotal") ?? 0,
            ewaAmount: pick(x, "ewaAmount") ?? 0,
            vatAmount: pick(x, "vatAmount") ?? 0,
            total: pick(x, "total") ?? 0
        };
    }

    function mapUnitDayRate(r) {
        const x = r || {};
        return {
            rateId: pick(x, "rateId"),
            reservationId: pick(x, "reservationId"),
            unitId: pick(x, "unitId"),
            nightDate: pick(x, "nightDate"),
            grossRate: pick(x, "grossRate") ?? 0,
            ewaAmount: pick(x, "ewaAmount") ?? 0,
            vatAmount: pick(x, "vatAmount") ?? 0,
            netAmount: pick(x, "netAmount") ?? 0,
            isManual: !!pick(x, "isManual")
        };
    }

    function mapUnitDayRatesResponse(payload) {
        const x = unwrapPayload(payload) || {};
        const rows = pick(x, "items");
        return {
            reservationId: pick(x, "reservationId"),
            unitId: pick(x, "unitId"),
            summary: mapUnitDayRateSummary(pick(x, "summary")),
            items: Array.isArray(rows) ? rows.map(mapUnitDayRate) : []
        };
    }

    function mapCompany(c) {
        if (!c || typeof c !== "object") {
            return null;
        }

        return {
            corporateId: pick(c, "corporateId"),
            corporateZaaerId: pick(c, "corporateZaaerId"),
            corporateName: pick(c, "corporateName") ?? "",
            corNo: pick(c, "corNo"),
            country: pick(c, "country"),
            countryAr: pick(c, "countryAr"),
            city: pick(c, "city"),
            cityAr: pick(c, "cityAr"),
            postalCode: pick(c, "postalCode"),
            address: pick(c, "address"),
            addressAr: pick(c, "addressAr"),
            vatRegistrationNo: pick(c, "vatRegistrationNo"),
            commercialRegistrationNo: pick(c, "commercialRegistrationNo"),
            discountMethod: pick(c, "discountMethod"),
            discountValue: pick(c, "discountValue"),
            corporatePhone: pick(c, "corporatePhone"),
            email: pick(c, "email"),
            contactPersonName: pick(c, "contactPersonName"),
            contactPersonPhone: pick(c, "contactPersonPhone"),
            notes: pick(c, "notes")
        };
    }

    function mapGuest(g) {
        const x = g || {};
        return {
            customerId: pick(x, "customerId"),
            customerZaaerId: pick(x, "customerZaaerId"),
            isPrimary: !!pick(x, "isPrimary"),
            customerName: pick(x, "customerName") ?? "",
            idTypeName: pick(x, "idTypeName"),
            idTypeNameAr: pick(x, "idTypeNameAr"),
            idNumber: pick(x, "idNumber"),
            birthDate: pick(x, "birthDate"),
            nationalityName: pick(x, "nationalityName"),
            nationalityNameAr: pick(x, "nationalityNameAr"),
            mobileNo: pick(x, "mobileNo"),
            email: pick(x, "email"),
            gender: pick(x, "gender"),
            gtypeId: pick(x, "gtypeId"),
            nationalityId: pick(x, "nationalityId")
        };
    }

    /** Same shape as companion rows on reservation-detail page (unit + relationship when persisted). */
    function mapCompanion(c) {
        const x = c || {};
        return {
            rowKey: pick(x, "rowKey"),
            customerId: pick(x, "customerId"),
            customerZaaerId: pick(x, "customerZaaerId"),
            customerName: pick(x, "customerName") ?? "",
            idTypeName: pick(x, "idTypeName"),
            idTypeNameAr: pick(x, "idTypeNameAr"),
            idNumber: pick(x, "idNumber"),
            birthDate: pick(x, "birthDate"),
            nationalityName: pick(x, "nationalityName"),
            nationalityNameAr: pick(x, "nationalityNameAr"),
            mobileNo: pick(x, "mobileNo"),
            email: pick(x, "email"),
            unitId: pick(x, "unitId"),
            relationId: pick(x, "relationId")
        };
    }

    function mapExtra(e) {
        const x = e || {};
        return {
            extraId: pick(x, "extraId"),
            reservationId: pick(x, "reservationId"),
            unitId: pick(x, "unitId"),
            roomLabel: pick(x, "roomLabel"),
            packageId: pick(x, "packageId"),
            itemName: pick(x, "itemName") ?? "",
            postingRule: pick(x, "postingRule") ?? "OnCheckIn",
            serviceDate: pick(x, "serviceDate"),
            guestCount: pick(x, "guestCount"),
            nightCount: pick(x, "nightCount"),
            unitPrice: pick(x, "unitPrice"),
            subtotal: pick(x, "subtotal"),
            taxAmount: pick(x, "taxAmount"),
            totalAmount: pick(x, "totalAmount"),
            createdBy: pick(x, "createdBy"),
            createdAt: pick(x, "createdAt")
        };
    }

    function mapPackage(p) {
        const x = p || {};
        return {
            packageId: pick(x, "packageId"),
            hotelId: pick(x, "hotelId"),
            name: pick(x, "name") ?? "",
            nameAr: pick(x, "nameAr"),
            description: pick(x, "description"),
            unitPrice: pick(x, "unitPrice"),
            isActive: pick(x, "isActive") !== false,
            sortOrder: pick(x, "sortOrder")
        };
    }

    function mapPenaltyCatalog(p) {
        const x = p || {};
        return {
            penaltyId: pick(x, "penaltyId"),
            hotelId: pick(x, "hotelId"),
            reservationId: pick(x, "reservationId"),
            penaltyType: pick(x, "penaltyType") ?? "Other",
            penaltyName: pick(x, "penaltyName") ?? "",
            penaltyNameAr: pick(x, "penaltyNameAr"),
            description: pick(x, "description"),
            baseAmount: pick(x, "baseAmount"),
            isActive: pick(x, "isActive") !== false
        };
    }

    function mapUnit(u) {
        const x = u || {};
        return {
            unitId: pick(x, "unitId"),
            unitZaaerId: pick(x, "unitZaaerId"),
            apartmentId: pick(x, "apartmentId"),
            apartmentZaaerId: pick(x, "apartmentZaaerId"),
            apartmentCode: pick(x, "apartmentCode"),
            apartmentLabel: pick(x, "apartmentLabel") ?? "",
            roomTypeName: pick(x, "roomTypeName"),
            buildingName: pick(x, "buildingName"),
            floorName: pick(x, "floorName"),
            checkInDate: pick(x, "checkInDate"),
            checkOutDate: pick(x, "checkOutDate"),
            departureDate: pick(x, "departureDate"),
            unitStatus: pick(x, "unitStatus") ?? "",
            defaultGrossRate: pick(x, "defaultGrossRate"),
            defaultGrossRateSource: pick(x, "defaultGrossRateSource"),
            rentAmount: pick(x, "rentAmount"),
            totalAmount: pick(x, "totalAmount")
        };
    }

    function normalizeReservationDetail(detail) {
        const d = unwrapPayload(detail);
        if (!d || typeof d !== "object") {
            return d;
        }

        const unitsRaw = pick(d, "units");
        const guestsRaw = pick(d, "guests");
        const companionsRaw = pick(d, "companions");
        const extrasRaw = pick(d, "extras");
        const discountsRaw = pick(d, "discounts");
        const units = Array.isArray(unitsRaw) ? unitsRaw.map(mapUnit) : [];
        const guests = Array.isArray(guestsRaw) ? guestsRaw.map(mapGuest) : [];
        const companions = Array.isArray(companionsRaw) ? companionsRaw.map(mapCompanion) : [];
        const extras = Array.isArray(extrasRaw) ? extrasRaw.map(mapExtra) : [];
        const discounts = Array.isArray(discountsRaw) ? discountsRaw.map(mapDiscountLine) : [];

        return {
            reservationId: pick(d, "reservationId"),
            zaaerId: pick(d, "zaaerId"),
            hotelId: pick(d, "hotelId"),
            hotelCode: pick(d, "hotelCode"),
            customerId: pick(d, "customerId"),
            corporateId: pick(d, "corporateId"),
            header: mapHeader(pick(d, "header")),
            general: mapGeneral(pick(d, "general")),
            dates: mapDates(pick(d, "dates")),
            units,
            company: mapCompany(pick(d, "company")),
            guests,
            companions,
            extras,
            discounts,
            notesCount: Number(pick(d, "notesCount")) || 0,
            financial: mapFinancial(pick(d, "financial")),
            pricingTax: mapPricingTax(pick(d, "pricingTax")),
            periods: mapPeriodList(pick(d, "periods"))
        };
    }

    async function loadById(reservationId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get(`${base}/${encodeURIComponent(reservationId)}`, params);
        const rawTop = unwrapPayload(response);

        if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
            const msg =
                rawTop.message || rawTop.Message || "Request failed.";
            throw new Error(msg);
        }

        const normalized = normalizeReservationDetail(rawTop);
        if (!normalized || typeof normalized !== "object") {
            const msg =
                (response && (response.message || response.Message)) ||
                "Reservation payload missing or invalid.";
            throw new Error(msg);
        }

        const hasId = normalized.reservationId != null || normalized.zaaerId != null;
        const hasNo = normalized.header && `${pick(normalized.header, "reservationNo") || ""}`.length > 0;

        if (!hasId && !hasNo) {
            const msg =
                (response && (response.message || response.Message)) ||
                "Reservation not found.";
            throw new Error(msg);
        }

        return normalized;
    }

    async function patchReservation(reservationId, body, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.patch(`${base}/${encodeURIComponent(reservationId)}`, body, params);
        return normalizeReservationDetail(unwrapPayload(response));
    }

    /**
     * Create a reservation with guest + editor payload in one request (replaces legacy draft + PATCH).
     */
    async function createReservation(apartmentId, patchBody) {
        const body = Object.assign({ apartmentId: Number(apartmentId) }, patchBody || {});
        const response = await window.Zaaer.ApiService.post(`${base}`, body);

        const rawTop = unwrapPayload(response);
        if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
            throw new Error(localizeCheckoutMessage(rawTop.message || rawTop.Message || "Create failed."));
        }

        const normalized = normalizeReservationDetail(rawTop);
        if (!normalized || normalized.reservationId == null || !Number.isFinite(Number(normalized.reservationId))) {
            throw new Error("Reservation create response is missing reservationId.");
        }

        return normalized;
    }

    /** @deprecated Use createReservation — draft without guest is no longer supported. */
    async function createReservationDraft(apartmentId) {
        return createReservation(apartmentId, {});
    }

    function localizeCheckoutMessage(raw) {
        const s = raw == null ? "" : String(raw).trim();
        if (!s) {
            return "Checkout failed.";
        }
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        const tFn = loc && typeof loc.t === "function" ? loc.t.bind(loc) : null;
        if (tFn && /^reservationDetail\./.test(s)) {
            const tr = tFn(s);
            if (tr && tr !== s) {
                return tr;
            }
        }
        return s;
    }

    async function cancelReservation(reservationId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        try {
            const response = await window.Zaaer.ApiService.post(
                `${base}/${encodeURIComponent(reservationId)}/cancel`,
                {},
                params
            );
            const rawTop = unwrapPayload(response);
            if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
                const m = rawTop.message || rawTop.Message || "Cancel failed.";
                throw new Error(localizeCheckoutMessage(m));
            }
            return normalizeReservationDetail(rawTop);
        } catch (err) {
            const xhr = err && err.responseJSON ? err.responseJSON : null;
            const raw =
                (xhr && (xhr.message || xhr.Message)) ||
                (err && err.message) ||
                "Cancel failed.";
            throw new Error(localizeCheckoutMessage(raw));
        }
    }

    async function checkoutReservation(reservationId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        try {
            const response = await window.Zaaer.ApiService.post(
                `${base}/${encodeURIComponent(reservationId)}/checkout`,
                {},
                params
            );
            const rawTop = unwrapPayload(response);
            if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
                const m = rawTop.message || rawTop.Message || "Checkout failed.";
                throw new Error(localizeCheckoutMessage(m));
            }
            return normalizeReservationDetail(rawTop);
        } catch (err) {
            const xhr = err && err.responseJSON ? err.responseJSON : null;
            const raw =
                (xhr && (xhr.message || xhr.Message)) ||
                (err && err.message) ||
                "Checkout failed.";
            throw new Error(localizeCheckoutMessage(raw));
        }
    }

    async function checkoutReservationUnit(reservationId, unitId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        try {
            const response = await window.Zaaer.ApiService.post(
                `${base}/${encodeURIComponent(reservationId)}/units/${encodeURIComponent(unitId)}/checkout`,
                {},
                params
            );
            const rawTop = unwrapPayload(response);
            if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
                const m = rawTop.message || rawTop.Message || "Unit checkout failed.";
                throw new Error(localizeCheckoutMessage(m));
            }
            return normalizeReservationDetail(rawTop);
        } catch (err) {
            const xhr = err && err.responseJSON ? err.responseJSON : null;
            const raw =
                (xhr && (xhr.message || xhr.Message)) ||
                (err && err.message) ||
                "Unit checkout failed.";
            throw new Error(localizeCheckoutMessage(raw));
        }
    }

    async function reopenReservationAfterCheckout(reservationId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        try {
            const response = await window.Zaaer.ApiService.post(
                `${base}/${encodeURIComponent(reservationId)}/reopen-checkin`,
                {},
                params
            );
            const rawTop = unwrapPayload(response);
            if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
                const m = rawTop.message || rawTop.Message || "Re-check-in failed.";
                throw new Error(localizeCheckoutMessage(m));
            }
            return normalizeReservationDetail(rawTop);
        } catch (err) {
            const xhr = err && err.responseJSON ? err.responseJSON : null;
            const permissionCode = xhr && (xhr.permissionCode || xhr.PermissionCode);
            const raw =
                (permissionCode && `${xhr.error || xhr.Error || "ليس لديك صلاحية لهذا الإجراء."} ${permissionCode}`) ||
                (xhr && (xhr.error || xhr.Error)) ||
                (xhr && (xhr.message || xhr.Message)) ||
                (err && err.message) ||
                "Re-check-in failed.";
            throw new Error(localizeCheckoutMessage(raw));
        }
    }

    function localizeUnitSwapMessage(raw) {
        const s = raw == null ? "" : String(raw).trim();
        if (!s) {
            return "Unit swap failed.";
        }
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        const tFn = loc && typeof loc.t === "function" ? loc.t.bind(loc) : null;

        const legacyToKey = {
            "Reservation line (unit) not found for this reservation.": "reservationDetail.units.transferErrUnitNotFound",
            "This reservation unit cannot be transferred (e.g. already checked out or cancelled).":
                "reservationDetail.units.transferErrSourceNotAllowed",
            "Invalid apply mode.": "reservationDetail.units.transferErrInvalidApplyMode",
            "Current apartment could not be resolved.": "reservationDetail.units.transferErrFromApartmentNotFound",
            "Target apartment not found.": "reservationDetail.units.transferErrTargetNotFound",
            "Target apartment must be vacant or available before transfer.":
                "reservationDetail.units.transferErrTargetNotVacant",
            "Target apartment must differ from the current apartment.": "reservationDetail.units.transferErrSameApartment"
        };

        let key = s;
        if (legacyToKey[s]) {
            key = legacyToKey[s];
        }

        if (tFn && key.indexOf("reservationDetail.units.transferErr") === 0) {
            const tr = tFn(key);
            if (tr && tr !== key) {
                return tr;
            }
        }
        return s;
    }

    async function swapReservationUnit(reservationId, body, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        try {
            const response = await window.Zaaer.ApiService.post(
                `${base}/${encodeURIComponent(reservationId)}/unit-swap`,
                body || {},
                params
            );
            const rawTop = unwrapPayload(response);
            if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
                const m = rawTop.message || rawTop.Message || "Unit swap failed.";
                throw new Error(localizeUnitSwapMessage(m));
            }
            return normalizeReservationDetail(rawTop);
        } catch (err) {
            const xhr = err && err.responseJSON ? err.responseJSON : null;
            const raw =
                (xhr && (xhr.message || xhr.Message)) ||
                (err && err.message) ||
                "Unit swap failed.";
            throw new Error(localizeUnitSwapMessage(raw));
        }
    }

    async function loadUnitDayRates(reservationId, unitId, hotelId) {
        const params = {};
        if (unitId !== undefined && unitId !== null && unitId !== "") {
            params.unitId = unitId;
        }
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get(`${base}/${encodeURIComponent(reservationId)}/unit-day-rates`, params);
        return mapUnitDayRatesResponse(response);
    }

    async function saveUnitDayRates(reservationId, payload, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.put(`${base}/${encodeURIComponent(reservationId)}/unit-day-rates`, payload || {}, params);
        return mapUnitDayRatesResponse(response);
    }

    async function updateHallRent(reservationId, hallRentAmount, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.put(
            `${base}/${encodeURIComponent(reservationId)}/hall-rent`,
            { hallRentAmount: Number(hallRentAmount) || 0 },
            params
        );
        return normalizeReservationDetail(unwrapPayload(response));
    }

    function mapPeriodList(response) {
        if (!response) {
            return { reservationId: null, hasMixedRentalPeriods: false, activeRentalType: null, items: [] };
        }

        const data = (response.items || response.Items)
            ? response
            : unwrapPayload(response);
        if (!data || typeof data !== "object") {
            return { reservationId: null, hasMixedRentalPeriods: false, activeRentalType: null, items: [] };
        }

        const items = Array.isArray(data.items) ? data.items : Array.isArray(data.Items) ? data.Items : [];
        return {
            reservationId: pick(data, "reservationId"),
            hasMixedRentalPeriods: !!(pick(data, "hasMixedRentalPeriods") ?? pick(data, "HasMixedRentalPeriods")),
            activeRentalType: pick(data, "activeRentalType") ?? pick(data, "ActiveRentalType") ?? null,
            items: items.map((p) => ({
                periodId: pick(p, "periodId"),
                reservationId: pick(p, "reservationId"),
                unitId: pick(p, "unitId"),
                rentalType: pick(p, "rentalType") || "",
                fromDate: pick(p, "fromDate"),
                toDate: pick(p, "toDate"),
                grossRate: pick(p, "grossRate"),
                taxIncluded: pick(p, "taxIncluded"),
                status: pick(p, "status") || "",
                createdAt: pick(p, "createdAt"),
                updatedAt: pick(p, "updatedAt")
            }))
        };
    }

    async function loadReservationPeriods(reservationId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get(`${base}/${encodeURIComponent(reservationId)}/periods`, params);
        const data = unwrapPayload(response);
        return mapPeriodList(data);
    }

    async function createInitialReservationPeriod(reservationId, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.post(
            `${base}/${encodeURIComponent(reservationId)}/periods/initial`,
            {},
            params
        );
        return mapPeriodList(response);
    }

    async function appendReservationPeriod(reservationId, payload, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.post(
            `${base}/${encodeURIComponent(reservationId)}/periods/append`,
            payload || {},
            params
        );
        const data = unwrapPayload(response);
        return mapPeriodMutationResult(data);
    }

    async function updateReservationPeriod(reservationId, periodId, payload, hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.patch(
            `${base}/${encodeURIComponent(reservationId)}/periods/${encodeURIComponent(periodId)}`,
            payload || {},
            params
        );
        const data = unwrapPayload(response);
        return mapPeriodMutationResult(data);
    }

    function mapPeriodMutationResult(data) {
        return {
            period: data && (data.period || data.Period) ? (data.period || data.Period) : null,
            reservation: data && (data.reservation || data.Reservation)
                ? normalizeReservationDetail(data.reservation || data.Reservation)
                : null
        };
    }

    async function loadVisitPurposes() {
        const response = await window.Zaaer.ApiService.get("/api/v1/pms/lookups/visit-purposes");
        const data = unwrapPayload(response);
        const rows = Array.isArray(data) ? data : [];
        return rows.map((p) => ({
            id: pick(p, "id"),
            name: pick(p, "name"),
            nameAr: pick(p, "nameAr")
        }));
    }

    async function loadReservationSources() {
        const response = await window.Zaaer.ApiService.get("/api/v1/pms/lookups/reservation-sources");
        const data = unwrapPayload(response);
        const rows = Array.isArray(data) ? data : [];
        const mapped = rows.map((p) => ({
            code: pick(p, "code"),
            name: pick(p, "name"),
            nameAr: pick(p, "nameAr")
        }));
        const rx = mapped.findIndex((x) => `${x.code || ""}`.trim().toLowerCase() === "reception");
        if (rx > 0) {
            const [first] = mapped.splice(rx, 1);
            mapped.unshift(first);
        }

        return mapped;
    }

    async function loadLookupArray(path) {
        const response = await window.Zaaer.ApiService.get(path);
        const data = unwrapPayload(response);
        const rows = Array.isArray(data) ? data : [];
        return rows.map((p) => ({
            id: pick(p, "id"),
            name: pick(p, "name"),
            nameAr: pick(p, "nameAr"),
            codePrefix: pick(p, "codePrefix")
        }));
    }

    async function loadCustomerRelations() {
        return loadLookupArray("/api/v1/pms/lookups/customer-relations");
    }

    async function loadReservationPackages(hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get("/api/v1/pms/reservation-packages", params);
        const data = unwrapPayload(response);
        const rows = Array.isArray(data) ? data : [];
        return rows.map(mapPackage);
    }

    async function createReservationPackage(payload) {
        const response = await window.Zaaer.ApiService.post("/api/v1/pms/reservation-packages", payload || {});
        return mapPackage(unwrapPayload(response));
    }

    async function loadPenaltyCatalog(hotelId) {
        const params = {};
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get("/api/v1/pms/reservation-penalties/catalog", params);
        const data = unwrapPayload(response);
        const rows = Array.isArray(data) ? data : [];
        return rows.map(mapPenaltyCatalog);
    }

    async function createPenaltyCatalog(payload) {
        const response = await window.Zaaer.ApiService.post("/api/v1/pms/reservation-penalties/catalog", payload || {});
        return mapPenaltyCatalog(unwrapPayload(response));
    }

    async function applyDiscount(payload) {
        const response = await window.Zaaer.ApiService.post("/api/v1/pms/reservation-discounts", payload || {});
        return mapDiscountMutationResult(unwrapDiscountResponse(response));
    }

    async function updateDiscount(discountId, payload) {
        const response = await window.Zaaer.ApiService.put(
            `/api/v1/pms/reservation-discounts/${encodeURIComponent(discountId)}`,
            payload || {}
        );
        return mapDiscountMutationResult(unwrapDiscountResponse(response));
    }

    async function deleteDiscount(discountId, reservationId, hotelId) {
        const params = { reservationId };
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.delete(
            `/api/v1/pms/reservation-discounts/${encodeURIComponent(discountId)}`,
            params
        );
        return mapDiscountMutationResult(unwrapDiscountResponse(response));
    }

    const notesBase = "/api/v1/pms/reservation-notes";

    function unwrapNotesResponse(response) {
        const rawTop = unwrapPayload(response);
        if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
            throw new Error(rawTop.message || rawTop.Message || "Request failed.");
        }

        return unwrapPayload(response);
    }

    async function loadReservationNotes(reservationId, hotelId) {
        const params = { reservationId };
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get(notesBase, params);
        return mapNotesList(unwrapNotesResponse(response));
    }

    async function countReservationNotes(reservationId, hotelId) {
        const params = { reservationId };
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.get(`${notesBase}/count`, params);
        const data = unwrapNotesResponse(response);
        return Number(pick(data, "count")) || 0;
    }

    async function createReservationNote(payload, file) {
        const fd = buildReservationNoteFormData(payload || {}, file);
        const response = await window.Zaaer.ApiService.postForm(notesBase, fd);
        return unwrapNotesMutation(unwrapNotesResponse(response));
    }

    async function updateReservationNote(noteId, payload, file, options) {
        const fd = buildReservationNoteFormData(payload || {}, file, options);
        const response = await window.Zaaer.ApiService.putForm(
            `${notesBase}/${encodeURIComponent(noteId)}`,
            fd
        );
        return unwrapNotesMutation(unwrapNotesResponse(response));
    }

    async function deleteReservationNote(noteId, reservationId, hotelId) {
        const params = { reservationId };
        if (hotelId !== undefined && hotelId !== null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        const response = await window.Zaaer.ApiService.delete(
            `${notesBase}/${encodeURIComponent(noteId)}`,
            params
        );
        return mapNotesList(unwrapNotesResponse(response));
    }

    function unwrapDiscountResponse(response) {
        const rawTop = unwrapPayload(response);
        if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
            throw new Error(rawTop.message || rawTop.Message || "Request failed.");
        }

        const data = unwrapPayload(response);
        if (!data || typeof data !== "object") {
            throw new Error("Invalid discount response.");
        }

        return data;
    }

    async function loadGuestFormLookups() {
        const [guestTypes, idTypes, nationalities, guestCategories, customerRelations] = await Promise.all([
            loadLookupArray("/api/v1/pms/lookups/guest-types"),
            loadLookupArray("/api/v1/pms/lookups/id-types"),
            loadLookupArray("/api/v1/pms/lookups/nationalities"),
            loadLookupArray("/api/v1/pms/lookups/guest-categories"),
            loadCustomerRelations().catch(() => [])
        ]);

        return { guestTypes, idTypes, nationalities, guestCategories, customerRelations };
    }

    function mapPaymentReceiptRow(row) {
        const x = row || {};
        const receiptNo = pick(x, "receiptNo") ?? pick(x, "number") ?? "";
        const receiptDate = pick(x, "receiptDate") ?? pick(x, "date");
        const rawAmount = Number(pick(x, "amountPaid") ?? pick(x, "amount")) || 0;
        const amountPaid = Math.abs(rawAmount);
        const receiptStatus = pick(x, "receiptStatus") ?? pick(x, "status") ?? "";
        const paymentMethod =
            pick(x, "paymentMethod") ?? pick(x, "paymentMethodName") ?? "";

        const receiptId = pick(x, "receiptId") ?? pick(x, "id");
        const zaaerId = pick(x, "zaaerId");
        const orderId = pick(x, "orderId");
        const rawVoucherCode = pick(x, "voucherCode") ?? "";
        const voucherCode =
            Number(orderId) > 0 && String(rawVoucherCode).toLowerCase() === "receipt"
                ? "service_receipt"
                : rawVoucherCode;
        const gridKey =
            receiptId != null && receiptId !== ""
                ? receiptId
                : zaaerId != null && zaaerId !== ""
                  ? `z-${zaaerId}`
                  : `r-${receiptNo || "0"}`;

        return {
            receiptId,
            id: gridKey,
            zaaerId,
            receiptNo,
            receiptDate,
            amountPaid,
            paymentMethod,
            paymentMethodId: pick(x, "paymentMethodId"),
            voucherCode,
            orderId: orderId != null ? Number(orderId) : null,
            receiptStatus,
            receiptFrom: pick(x, "receiptFrom"),
            receiptTo: pick(x, "receiptTo"),
            receiptType: pick(x, "receiptType") ?? "",
            notes: pick(x, "notes") ?? "",
            reason: pick(x, "reason") ?? "",
            bankId: pick(x, "bankId"),
            transactionNo: pick(x, "transactionNo") ?? "",
            hotelId: pick(x, "hotelId"),
            reservationId: pick(x, "reservationId"),
            customerId: pick(x, "customerId"),
            unitId: pick(x, "unitId"),
            isBuildingGuardRent: !!(
                pick(x, "isBuildingGuardRent") ?? pick(x, "IsBuildingGuardRent")
            ),
            number: receiptNo,
            date: receiptDate,
            amount: amountPaid,
            status: receiptStatus,
            paymentMethodName: paymentMethod
        };
    }

    function unwrapPaymentReceiptMutation(response) {
        const rawTop = unwrapPayload(response);
        if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
            throw new Error(rawTop.message || rawTop.Message || "Request failed.");
        }

        const data = unwrapPayload(response);
        if (!data || typeof data !== "object") {
            throw new Error("Invalid payment receipt response.");
        }

        return data;
    }

    function mapPromissoryRow(row) {
        if (!row || typeof row !== "object") {
            return row;
        }

        const amount = Number(row.amount != null ? row.amount : row.Amount) || 0;
        const amountCollected =
            Number(row.amountCollected != null ? row.amountCollected : row.AmountCollected) || 0;
        const dueAmount =
            Number(row.dueAmount != null ? row.dueAmount : row.DueAmount) ||
            Math.max(0, amount - amountCollected);
        const promissoryNo = row.promissoryNo || row.PromissoryNo || row.number || "";
        const createdAt = row.createdAt || row.CreatedAt || row.date || null;
        const maturityDate = row.maturityDate || row.MaturityDate || null;

        return {
            id: row.promissoryNoteId || row.PromissoryNoteId || row.id,
            promissoryNoteId: row.promissoryNoteId || row.PromissoryNoteId,
            zaaerId: row.zaaerId != null ? row.zaaerId : row.ZaaerId,
            number: promissoryNo,
            promissoryNo: promissoryNo,
            date: createdAt,
            createdAt: createdAt,
            maturityDate: maturityDate,
            amount: amount,
            amountCollected: amountCollected,
            dueAmount: dueAmount,
            status: row.status || row.Status || "open",
            payableTo: row.payableTo || row.PayableTo || "",
            reason: row.reason || row.Reason || "",
            placeOfMaturity: row.placeOfMaturity || row.PlaceOfMaturity || "",
            notes: row.notes || row.Notes || "",
            paymentLinkSent: !!(row.paymentLinkSent || row.PaymentLinkSent),
            collectionReceiptId: row.collectionReceiptId || row.CollectionReceiptId || null,
            collectionReceiptNo: row.collectionReceiptNo || row.CollectionReceiptNo || ""
        };
    }

    async function loadPromissoryRows(options) {
        const reservationId = options && options.reservationId;
        if (!reservationId) {
            return [];
        }

        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/promissory-notes/reservation/${encodeURIComponent(reservationId)}`
        );
        const raw = unwrapPayload(response);
        const list = Array.isArray(raw) ? raw : Array.isArray(raw && raw.data) ? raw.data : [];
        return list.map(mapPromissoryRow);
    }

    function unwrapPromissoryMutation(response) {
        const rawTop = unwrapPayload(response);
        if (rawTop && typeof rawTop === "object" && (rawTop.success === false || rawTop.Success === false)) {
            throw new Error(rawTop.message || rawTop.Message || "Request failed.");
        }

        const data = unwrapPayload(response);
        if (!data || typeof data !== "object") {
            throw new Error("Invalid promissory note response.");
        }

        return mapPromissoryRow(data);
    }

    async function createPromissoryNote(payload) {
        const response = await window.Zaaer.ApiService.post(
            "/api/v1/pms/promissory-notes",
            payload || {}
        );
        return unwrapPromissoryMutation(response);
    }

    async function updatePromissoryNote(zaaerId, payload) {
        const response = await window.Zaaer.ApiService.put(
            `/api/v1/pms/promissory-notes/by-zaaer/${encodeURIComponent(zaaerId)}`,
            payload || {}
        );
        return unwrapPromissoryMutation(response);
    }

    async function cancelPromissoryNote(zaaerId, payload) {
        const response = await window.Zaaer.ApiService.post(
            `/api/v1/pms/promissory-notes/by-zaaer/${encodeURIComponent(zaaerId)}/cancel`,
            payload || {}
        );
        return unwrapPromissoryMutation(response);
    }

    async function loadPaymentRows(options) {
        const kind = options && options.kind ? String(options.kind) : "receipts";
        const reservationId = options && options.reservationId;

        if (!reservationId) {
            return [];
        }

        if (kind === "invoices") {
            const invResponse = await window.Zaaer.ApiService.get(
                `/api/v1/pms/invoices/reservation/${encodeURIComponent(reservationId)}`
            );
            const invRaw = unwrapPayload(invResponse);
            const invList = Array.isArray(invRaw) ? invRaw : Array.isArray(invRaw && invRaw.data) ? invRaw.data : [];
            return invList.map(mapInvoiceRow);
        }

        if (kind === "credit_notes") {
            const cnResponse = await window.Zaaer.ApiService.get(
                `/api/v1/pms/credit-notes/reservation/${encodeURIComponent(reservationId)}`
            );
            const cnRaw = unwrapPayload(cnResponse);
            const cnList = Array.isArray(cnRaw) ? cnRaw : Array.isArray(cnRaw && cnRaw.data) ? cnRaw.data : [];
            return cnList.map(mapCreditNoteReservationRow);
        }

        if (kind !== "receipts" && kind !== "disbursements" && kind !== "promissory") {
            return [];
        }

        if (kind === "promissory") {
            return loadPromissoryRows({ reservationId: reservationId });
        }

        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/payment-receipts/reservation/${encodeURIComponent(reservationId)}`,
            { kind }
        );
        const raw = unwrapPayload(response);
        const list = Array.isArray(raw) ? raw : Array.isArray(raw && raw.data) ? raw.data : [];
        return list.map(mapPaymentReceiptRow);
    }

    async function loadPaymentReceiptByZaaerId(zaaerId) {
        const id = Number(zaaerId);
        if (!Number.isFinite(id) || id <= 0) {
            return null;
        }

        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/payment-receipts/by-zaaer/${encodeURIComponent(id)}`
        );
        const raw = unwrapPayload(response);
        const row = raw && typeof raw === "object" && raw.data != null ? raw.data : raw;
        if (!row || typeof row !== "object") {
            return null;
        }

        return mapPaymentReceiptRow(row);
    }

    function mapCreditNoteReservationRow(row) {
        const x = row || {};
        const creditNoteId = pick(x, "creditNoteId") ?? pick(x, "documentId");
        const zaaerId = pick(x, "zaaerId");
        const gridKey =
            creditNoteId != null && creditNoteId !== ""
                ? creditNoteId
                : zaaerId != null && zaaerId !== ""
                  ? `z-${zaaerId}`
                  : `cn-${pick(x, "creditNoteNo") || "0"}`;

        return {
            id: gridKey,
            creditNoteId,
            zaaerId,
            documentId: creditNoteId,
            documentNo: pick(x, "creditNoteNo") ?? pick(x, "documentNo") ?? "",
            creditNoteNo: pick(x, "creditNoteNo") ?? pick(x, "documentNo") ?? "",
            creditNoteDate: pick(x, "creditNoteDate") ?? pick(x, "documentDate"),
            documentDate: pick(x, "creditNoteDate") ?? pick(x, "documentDate"),
            creditAmount: Number(pick(x, "creditAmount") ?? pick(x, "amount")) || 0,
            amount: Number(pick(x, "creditAmount") ?? pick(x, "amount")) || 0,
            invoiceId: pick(x, "invoiceId"),
            invoiceNo: pick(x, "invoiceNo") ?? "",
            zatcaStatus: pick(x, "zatcaStatus") ?? "pending",
            reason: pick(x, "reason") ?? "",
            kind: "credit_note"
        };
    }

    async function loadLastRentReceipt(reservationId) {
        if (!reservationId) {
            return null;
        }

        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/payment-receipts/reservation/${encodeURIComponent(reservationId)}/last-rent`
        );
        const raw = unwrapPayload(response);
        const data =
            raw && typeof raw === "object" && raw.data && typeof raw.data === "object"
                ? raw.data
                : raw && typeof raw === "object"
                  ? raw
                  : null;

        if (!data || !data.receiptFrom || !data.receiptTo) {
            return null;
        }

        return {
            receiptNo: pick(data, "receiptNo") ?? "",
            receiptFrom: pick(data, "receiptFrom"),
            receiptTo: pick(data, "receiptTo"),
            receiptDate: pick(data, "receiptDate")
        };
    }

    async function countCreditNotesByReservation(reservationId) {
        if (!reservationId) {
            return 0;
        }

        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/credit-notes/reservation/${encodeURIComponent(reservationId)}/count`
        );
        const raw = unwrapPayload(response);
        const payload =
            raw && typeof raw === "object" && raw.data && typeof raw.data === "object"
                ? raw.data
                : raw && typeof raw === "object"
                  ? raw
                  : {};

        return Number(pick(payload, "count")) || 0;
    }

    async function createPaymentReceipt(payload) {
        const response = await window.Zaaer.ApiService.post(
            "/api/v1/pms/payment-receipts",
            payload || {}
        );
        return unwrapPaymentReceiptMutation(response);
    }

    async function updatePaymentReceipt(zaaerId, payload) {
        const response = await window.Zaaer.ApiService.put(
            `/api/v1/pms/payment-receipts/by-zaaer/${encodeURIComponent(zaaerId)}`,
            payload || {}
        );
        return unwrapPaymentReceiptMutation(response);
    }

    async function cancelPaymentReceipt(zaaerId, payload) {
        const response = await window.Zaaer.ApiService.post(
            `/api/v1/pms/payment-receipts/by-zaaer/${encodeURIComponent(zaaerId)}/cancel`,
            payload || {}
        );
        return unwrapPaymentReceiptMutation(response);
    }

    function mapInvoiceRow(row) {
        const x = row || {};
        const invoiceId = pick(x, "invoiceId");
        const zaaerId = pick(x, "zaaerId");
        const gridKey =
            zaaerId != null && zaaerId !== ""
                ? zaaerId
                : invoiceId != null && invoiceId !== ""
                  ? invoiceId
                  : null;
        return {
            id: gridKey,
            invoiceId: invoiceId,
            zaaerId: zaaerId,
            invoiceNo: pick(x, "invoiceNo") || pick(x, "number") || "",
            invoiceDate: pick(x, "invoiceDate") || pick(x, "date"),
            totalAmount: pick(x, "totalAmount") != null ? pick(x, "totalAmount") : pick(x, "amount"),
            periodFrom: pick(x, "periodFrom"),
            periodTo: pick(x, "periodTo"),
            zatcaStatus: pick(x, "zatcaStatus") || pick(x, "status") || "pending",
            customerName: pick(x, "customerName"),
            customerId: pick(x, "customerId"),
            hotelId: pick(x, "hotelId"),
            reservationId: pick(x, "reservationId"),
            notes: pick(x, "notes"),
            paymentStatus: pick(x, "paymentStatus"),
            parentZatcaSubmitted: !!pick(x, "parentZatcaSubmitted"),
            relatedAdjustmentCount:
                pick(x, "relatedAdjustmentCount") != null
                    ? Number(pick(x, "relatedAdjustmentCount")) || 0
                    : 0,
            relatedCreditNoteCount:
                pick(x, "relatedCreditNoteCount") != null
                    ? Number(pick(x, "relatedCreditNoteCount")) || 0
                    : 0,
            adjustmentRemainingAmount:
                pick(x, "adjustmentRemainingAmount") != null
                    ? Number(pick(x, "adjustmentRemainingAmount")) || 0
                    : null,
            number: pick(x, "invoiceNo") || pick(x, "number") || "",
            date: pick(x, "invoiceDate") || pick(x, "date"),
            amount: pick(x, "totalAmount") != null ? pick(x, "totalAmount") : pick(x, "amount"),
            status: pick(x, "zatcaStatus") || pick(x, "status") || "pending"
        };
    }

    function mapAdjustmentRow(row) {
        const x = row || {};
        return {
            kind: pick(x, "kind") || "",
            documentId: pick(x, "documentId"),
            zaaerId: pick(x, "zaaerId"),
            documentNo: pick(x, "documentNo") || "",
            documentDate: pick(x, "documentDate"),
            amount: pick(x, "amount"),
            zatcaStatus: pick(x, "zatcaStatus") || "pending",
            reason: pick(x, "reason")
        };
    }

    async function loadInvoiceCreateContext(reservationId) {
        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/invoices/reservation/${encodeURIComponent(reservationId)}/create-context`
        );
        return mapInvoiceCreateContext(unwrapPayload(response));
    }

    function mapCheckoutSnapshot(raw) {
        const x = raw || {};
        return {
            reservationId: pick(x, "reservationId"),
            zaaerId: pick(x, "zaaerId"),
            rentTotal: Number(pick(x, "rentTotal")) || 0,
            extrasTotal: Number(pick(x, "extrasTotal")) || 0,
            penaltiesTotal: Number(pick(x, "penaltiesTotal")) || 0,
            discountsTotal: Number(pick(x, "discountsTotal")) || 0,
            totalAmount: Number(pick(x, "totalAmount")) || 0,
            amountPaid: Number(pick(x, "amountPaid")) || 0,
            balanceAmount: Number(pick(x, "balanceAmount")) || 0,
            grossInvoicedTotal: Number(pick(x, "grossInvoicedTotal")) || 0,
            creditNotesTotal: Number(pick(x, "creditNotesTotal")) || 0,
            debitNotesTotal: Number(pick(x, "debitNotesTotal")) || 0,
            netInvoicedTotal: Number(pick(x, "netInvoicedTotal")) || 0,
            invoicedTotal: Number(pick(x, "netInvoicedTotal") ?? pick(x, "invoicedTotal")) || 0,
            invoiceRequiredAmount: Number(pick(x, "invoiceRequiredAmount")) || 0,
            invoiceRemaining: Number(pick(x, "invoiceRemaining")) || 0
        };
    }

    function mapInvoiceCreateContext(raw) {
        const x = raw || {};
        const invoiceRemaining =
            Number(
                pick(x, "invoiceRemainingAmount") ??
                    pick(x, "balanceAmount")
            ) || 0;
        return {
            reservationId: pick(x, "reservationId"),
            hotelId: pick(x, "hotelId"),
            paymentBalanceAmount: Number(pick(x, "paymentBalanceAmount")) || 0,
            invoiceRemainingAmount: invoiceRemaining,
            grossInvoicedAmount: Number(pick(x, "grossInvoicedAmount")) || 0,
            netInvoicedAmount: Number(pick(x, "netInvoicedAmount")) || 0,
            creditNotesTotal: Number(pick(x, "creditNotesTotal")) || 0,
            invoiceRequiredAmount: Number(pick(x, "invoiceRequiredAmount")) || 0,
            balanceAmount: invoiceRemaining,
            defaultPeriodFrom: pick(x, "defaultPeriodFrom"),
            defaultPeriodTo: pick(x, "defaultPeriodTo"),
            vatRate: pick(x, "vatRate"),
            lodgingTaxRate: pick(x, "lodgingTaxRate"),
            lastInvoice: pick(x, "lastInvoice")
        };
    }

    async function loadCheckoutSnapshot(reservationId, hotelId) {
        const params = hotelId != null && hotelId !== "" ? { hotelId } : undefined;
        const response = await window.Zaaer.ApiService.get(
            `${base}/${encodeURIComponent(reservationId)}/checkout-snapshot`,
            params
        );
        return mapCheckoutSnapshot(unwrapPayload(response));
    }

    async function createInvoice(payload) {
        const response = await window.Zaaer.ApiService.post("/api/v1/pms/invoices", payload || {});
        const raw = unwrapPayload(response);
        return mapInvoiceRow(raw);
    }

    async function createCreditNote(payload) {
        const response = await window.Zaaer.ApiService.post("/api/v1/pms/credit-notes", payload || {});
        return mapAdjustmentRow(unwrapPayload(response));
    }

    async function createDebitNote(payload) {
        const response = await window.Zaaer.ApiService.post("/api/v1/pms/debit-notes", payload || {});
        return mapAdjustmentRow(unwrapPayload(response));
    }

    async function loadInvoiceAdjustments(invoiceId) {
        const response = await window.Zaaer.ApiService.get(
            `/api/v1/pms/invoices/${encodeURIComponent(invoiceId)}/adjustments`
        );
        const raw = unwrapPayload(response);
        const list = Array.isArray(raw) ? raw : Array.isArray(raw && raw.data) ? raw.data : [];
        return list.map(mapAdjustmentRow);
    }

    async function sendZatcaDocument(documentKind, documentId) {
        const response = await window.Zaaer.ApiService.post(
            "/api/v1/pms/integrations/zatca/send-document",
            { documentKind: documentKind, documentId: documentId }
        );
        return unwrapPayload(response);
    }

    async function loadPaymentMethods() {
        return loadLookupArray("/api/v1/pms/lookups/payment-methods");
    }

    async function loadBanks() {
        return loadLookupArray("/api/v1/pms/lookups/banks");
    }

    async function loadApartmentsForPicker(hotelId) {
        const response = await window.Zaaer.ApiService.get("/api/v1/pms/apartments/for-picker", { hotelId });
        const rawTop = unwrapPayload(response);

        if (
            rawTop &&
            typeof rawTop === "object" &&
            !Array.isArray(rawTop) &&
            (rawTop.success === false || rawTop.Success === false)
        ) {
            throw new Error(rawTop.message || rawTop.Message || "Request failed.");
        }

        if (Array.isArray(rawTop)) {
            return rawTop;
        }

        const inner = rawTop && typeof rawTop === "object" ? rawTop.data ?? rawTop.Data : null;
        return Array.isArray(inner) ? inner : [];
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ReservationDetailService = {
        loadById,
        patchReservation,
        createReservation,
        createReservationDraft,
        cancelReservation,
        checkoutReservation,
        checkoutReservationUnit,
        reopenReservationAfterCheckout,
        swapReservationUnit,
        loadUnitDayRates,
        saveUnitDayRates,
        updateHallRent,
        loadReservationPeriods,
        createInitialReservationPeriod,
        appendReservationPeriod,
        updateReservationPeriod,
        loadVisitPurposes,
        loadReservationSources,
        loadGuestFormLookups,
        loadCustomerRelations,
        loadReservationPackages,
        createReservationPackage,
        loadPenaltyCatalog,
        createPenaltyCatalog,
        applyDiscount,
        updateDiscount,
        deleteDiscount,
        loadReservationNotes,
        countReservationNotes,
        createReservationNote,
        updateReservationNote,
        deleteReservationNote,
        loadPaymentRows,
        loadPaymentReceiptByZaaerId,
        loadLastRentReceipt,
        countCreditNotesByReservation,
        loadCheckoutSnapshot,
        loadInvoiceCreateContext,
        createInvoice,
        createCreditNote,
        createDebitNote,
        loadInvoiceAdjustments,
        sendZatcaDocument,
        loadPromissoryRows,
        createPromissoryNote,
        updatePromissoryNote,
        cancelPromissoryNote,
        createPaymentReceipt,
        updatePaymentReceipt,
        cancelPaymentReceipt,
        loadPaymentMethods,
        loadBanks,
        loadApartmentsForPicker,
        loadReservationActivityLogs
    };

    function loadReservationActivityLogs(reservationId, hotelId, options) {
        const opts = options || {};
        const skip = opts.skip != null ? opts.skip : 0;
        const take = opts.take != null ? opts.take : 50;
        const params = { skip, take };
        if (hotelId != null && hotelId !== "") {
            params.hotelId = hotelId;
        }

        return window.Zaaer.ApiService.get(
            `${base}/${reservationId}/activity-logs`,
            params
        ).then((raw) => {
            const top = parseJsonIfNeeded(raw);
            if (
                top &&
                typeof top === "object" &&
                !Array.isArray(top) &&
                (top.success === false || top.Success === false)
            ) {
                throw new Error(top.message || top.Message || "Request failed.");
            }
            const inner = unwrapPayload(top);
            return Array.isArray(inner) ? inner : [];
        });
    }

    function searchActivityLogs(query) {
        const q = query || {};
        return window.Zaaer.ApiService.get("/api/v1/pms/activity-logs", q).then((raw) => {
            const top = parseJsonIfNeeded(raw);
            const inner = unwrapPayload(top);
            return Array.isArray(inner) ? inner : [];
        });
    }

    window.Zaaer.ReservationDetailService.searchActivityLogs = searchActivityLogs;
})(window);
