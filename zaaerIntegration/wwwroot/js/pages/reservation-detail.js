(function (window, $, DevExpress) {
    "use strict";

    const pageCtx = {
        routeId: null,
        hotelIdParam: null,
        detail: null,
        purposes: [],
        sources: [],
        isLocalNewReservation: false,
        isClientNewReservation: false,
        companions: [],
        companionKeySeq: 1,
        /** From GET detail.extras — reservation add-ons / extras lines */
        extras: [],
        extraKeySeq: 1,
        /** Active package catalog rows for the extras dropdown */
        reservationPackages: [],
        /** Active penalty catalog rows for the penalty dropdown */
        penaltyCatalog: [],
        /** Per pricing line key → gross rate (daily: `${unitId}_${yyyy-mm-dd}`, monthly: `${unitId}_m_${index}`) */
        pricingRateByLineKey: {},
        useLocalFinancialTotals: false,
        /** Last rental mode used to reset local pricing keys when switching Daily ↔ Monthly */
        _pricingRentalMode: "Daily",
        propertyMode: null,
        isHallProperty: false,
        isLodgingProperty: false,
        isHotelProperty: false,
        hallEvent: null,
        /** From GET /api/v1/pms/lookups/customer-relations */
        customerRelations: [],
        /** True until first successful save for a client-side new reservation — hides checkout until then */
        checkoutUiPendingFirstSave: false,
        /** While unit pricing popup is visible — stay/date changes refresh its grid */
        _unitPricingPopupActive: false,
        /** Tax summary from last opened pricing popup (for live refresh while open) */
        _unitPricingTaxConfig: null,
        /** Financial TabPanel panes already initialized in the current page layout */
        financialTabsLoaded: {},
        /** Payments workspace grids already initialized in the current page layout */
        /** Record counts shown on payment sub-tab badges */
        paymentTabCounts: {},
        notesCount: 0,
        /** Last server-confirmed reservation detail. Used to build permission-aware PATCH deltas. */
        persistedDetail: null,
        /** Blocks interaction while navigating away after cancel (prevents double-submit). */
        pageNavLocked: false
    };

    let suppressDateDurationSync = false;
    let suppressHallHijriSync = false;
    let activeReservationActionForm = null;

    const DAY_MS = 86400000;

    function splitDateTimeParts(dt) {
        if (!dt) {
            return { date: null, time: null };
        }

        const d = new Date(dt);
        if (Number.isNaN(d.getTime())) {
            return { date: null, time: null };
        }

        const dateOnly = new Date(d.getFullYear(), d.getMonth(), d.getDate());
        const timeOnly = new Date(1970, 0, 1, d.getHours(), d.getMinutes(), d.getSeconds(), d.getMilliseconds());
        return { date: dateOnly, time: timeOnly };
    }

    function combineDateTimeParts(datePart, timePart) {
        if (!datePart) {
            return null;
        }

        const d = new Date(datePart);
        const t = timePart ? new Date(timePart) : new Date(1970, 0, 1, 12, 0, 0, 0);
        return new Date(
            d.getFullYear(),
            d.getMonth(),
            d.getDate(),
            t.getHours(),
            t.getMinutes(),
            t.getSeconds(),
            t.getMilliseconds()
        );
    }

    function gridRowsSignature(rows) {
        try {
            return JSON.stringify(rows || []);
        } catch {
            return `${(rows || []).length}:${Date.now()}`;
        }
    }

    function setGridDataSourceIfChanged(grid, rows) {
        if (!grid) {
            return false;
        }

        const nextRows = Array.isArray(rows) ? rows.slice() : [];
        const nextSignature = gridRowsSignature(nextRows);
        if (grid.__reservationDetailDataSignature === nextSignature) {
            return false;
        }

        grid.__reservationDetailDataSignature = nextSignature;
        grid.option("dataSource", nextRows);
        return true;
    }

    function getReservationCheckInCombined() {
        const dInst = $("#resCheckInDate").dxDateBox("instance");
        if (!dInst) {
            return null;
        }

        const tInst = $("#resCheckInTime").dxDateBox("instance");
        return combineDateTimeParts(dInst.option("value"), tInst ? tInst.option("value") : null);
    }

    function getReservationCheckOutCombined() {
        const dInst = $("#resCheckOutDate").dxDateBox("instance");
        if (!dInst) {
            return null;
        }

        const tInst = $("#resCheckOutTime").dxDateBox("instance");
        return combineDateTimeParts(dInst.option("value"), tInst ? tInst.option("value") : null);
    }

    function setReservationCheckInFromDateTime(dt) {
        const { date, time } = splitDateTimeParts(dt);
        const dInst = $("#resCheckInDate").dxDateBox("instance");
        const tInst = $("#resCheckInTime").dxDateBox("instance");
        if (dInst) {
            dInst.option("value", date);
        }

        if (tInst) {
            tInst.option("value", time);
        }
    }

    function setReservationCheckOutFromDateTime(dt) {
        const { date, time } = splitDateTimeParts(dt);
        const dInst = $("#resCheckOutDate").dxDateBox("instance");
        const tInst = $("#resCheckOutTime").dxDateBox("instance");
        if (dInst) {
            dInst.option("value", date);
        }

        if (tInst) {
            tInst.option("value", time);
        }
    }

    /** Hotel night count: calendar check-out date minus calendar check-in date (12 AM → next day 6 PM = 1 night). */
    function hotelNightCount(checkIn, checkOut) {
        if (!checkIn || !checkOut) {
            return null;
        }

        const s = new Date(checkIn);
        const e = new Date(checkOut);
        if (Number.isNaN(s.getTime()) || Number.isNaN(e.getTime())) {
            return null;
        }

        const sd = new Date(s.getFullYear(), s.getMonth(), s.getDate());
        const ed = new Date(e.getFullYear(), e.getMonth(), e.getDate());
        const diff = Math.round((ed.getTime() - sd.getTime()) / DAY_MS);
        return Math.max(0, diff);
    }

    /**
     * Default daily checkout: calendar check-in date + `nights` hotel nights, time 18:00
     * (e.g. check-in 12:00 AM + 1 night → next calendar day 6:00 PM).
     */
    function defaultCheckOutFromCheckInAndNights(ci, nights) {
        const n = Math.max(1, Math.floor(Number(nights)) || 1);
        const base = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate(), 0, 0, 0, 0);
        return new Date(base.getFullYear(), base.getMonth(), base.getDate() + n, 18, 0, 0, 0);
    }

    /** Monthly checkout: +30 calendar days per month block, departure time 18:00 (same as daily). */
    function defaultCheckOutFromCheckInAndMonths(ci, months) {
        const m = Math.max(1, Math.floor(Number(months)) || 1);
        const base = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate(), 0, 0, 0, 0);
        return new Date(base.getFullYear(), base.getMonth(), base.getDate() + m * 30, 18, 0, 0, 0);
    }

    function nightDiff(checkIn, checkOut) {
        if (!checkIn || !checkOut) {
            return null;
        }

        const s = new Date(checkIn).getTime();
        const e = new Date(checkOut).getTime();
        if (Number.isNaN(s) || Number.isNaN(e)) {
            return null;
        }

        if (e <= s) {
            return 0;
        }

        return Math.max(0, Math.round((e - s) / DAY_MS));
    }

    /** Months in «شهري» mode: each month = 30 nights (aligned with stay-length pricing blocks). */
    function thirtyDayMonthsBetween(ci, co) {
        const n = nightDiff(ci, co);
        if (n === null) {
            return null;
        }

        return Math.max(0, Math.round(n / 30));
    }

    const MONTHLY_CALENDAR_THIRTY_DAY = "ThirtyDay";
    const MONTHLY_CALENDAR_ACTUAL = "Actual";
    const MONTHLY_CALENDAR_THIRTY_DAY_PERMISSION = "reservations.monthly_calendar_thirty_day";
    const MONTHLY_CALENDAR_ACTUAL_PERMISSION = "reservations.monthly_calendar_actual";

    function canUseMonthlyCalendarThirtyDay() {
        return hasPmsPermission(MONTHLY_CALENDAR_THIRTY_DAY_PERMISSION);
    }

    function canUseMonthlyCalendarActual() {
        return hasPmsPermission(MONTHLY_CALENDAR_ACTUAL_PERMISSION);
    }

    function canChooseMonthlyCalendarMode() {
        return canUseMonthlyCalendarThirtyDay() && canUseMonthlyCalendarActual();
    }

    function getSelectedMonthlyCalendarKey() {
        let inst = null;
        try {
            inst = $("#resCalendarGroup").dxButtonGroup("instance");
        } catch {
            inst = null;
        }

        if (!inst) {
            return MONTHLY_CALENDAR_THIRTY_DAY;
        }

        const keys = inst.option("selectedItemKeys") || [];
        return keys[0] === MONTHLY_CALENDAR_ACTUAL ? MONTHLY_CALENDAR_ACTUAL : MONTHLY_CALENDAR_THIRTY_DAY;
    }

    function resolveEffectiveMonthlyCalendarMode(preferred) {
        const pref = preferred || getSelectedMonthlyCalendarKey();
        const canThirty = canUseMonthlyCalendarThirtyDay();
        const canActual = canUseMonthlyCalendarActual();

        if (canThirty && canActual) {
            return pref === MONTHLY_CALENDAR_ACTUAL ? MONTHLY_CALENDAR_ACTUAL : MONTHLY_CALENDAR_THIRTY_DAY;
        }

        if (canActual && !canThirty) {
            return MONTHLY_CALENDAR_ACTUAL;
        }

        return MONTHLY_CALENDAR_THIRTY_DAY;
    }

    function isActualMonthlyCalendarMode() {
        return resolveEffectiveMonthlyCalendarMode() === MONTHLY_CALENDAR_ACTUAL;
    }

    function normMonthlyCalendarMode(value) {
        return `${value || ""}`.trim().toLowerCase() === "actual"
            ? MONTHLY_CALENDAR_ACTUAL
            : MONTHLY_CALENDAR_THIRTY_DAY;
    }

    function addCalendarMonthsToDate(baseDate, months) {
        const m = Math.max(1, Math.floor(Number(months)) || 1);
        const y = baseDate.getFullYear();
        const mo = baseDate.getMonth();
        const d = baseDate.getDate();
        const result = new Date(y, mo + m, 1, 18, 0, 0, 0);
        const lastDay = new Date(result.getFullYear(), result.getMonth() + 1, 0).getDate();
        result.setDate(Math.min(d, lastDay));
        return result;
    }

    /** Monthly checkout: add Gregorian calendar months from check-in date, departure 18:00. */
    function defaultCheckOutFromCheckInAndCalendarMonths(ci, months) {
        const base = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate(), 0, 0, 0, 0);
        return addCalendarMonthsToDate(base, months);
    }

    function defaultCheckOutFromCheckInAndMonthsByMode(ci, months) {
        if (isActualMonthlyCalendarMode()) {
            return defaultCheckOutFromCheckInAndCalendarMonths(ci, months);
        }

        return defaultCheckOutFromCheckInAndMonths(ci, months);
    }

    function calendarMonthsBetween(ci, co) {
        const start = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate());
        const end = new Date(co.getFullYear(), co.getMonth(), co.getDate());
        if (end <= start) {
            return 0;
        }

        let months = (end.getFullYear() - start.getFullYear()) * 12 + (end.getMonth() - start.getMonth());
        if (end.getDate() < start.getDate()) {
            months -= 1;
        }

        return Math.max(0, months);
    }

    function monthsBetweenCheckInAndCheckOut(ci, co) {
        if (isActualMonthlyCalendarMode()) {
            return calendarMonthsBetween(ci, co);
        }

        return thirtyDayMonthsBetween(ci, co);
    }

    function applyMonthlyCalendarVisibility() {
        const monthly = isMonthlyRentalMode();
        const showPicker = monthly && canChooseMonthlyCalendarMode();
        $("#res-date-cell-calendar").toggle(showPicker);
    }

    function syncMonthlyCalendarControlFromEffectiveMode(preferred) {
        const mode = resolveEffectiveMonthlyCalendarMode(preferred);
        trySetDevExtremeOption("#resCalendarGroup", "selectedItemKeys", [mode]);
    }

    function getSelectedRentalKey() {
        let inst = null;
        try {
            inst = $("#resRentalGroup").dxButtonGroup("instance");
        } catch {
            inst = null;
        }

        if (!inst) {
            return "Daily";
        }

        const keys = inst.option("selectedItemKeys") || [];
        return keys[0] === "Monthly" ? "Monthly" : "Daily";
    }

    function isMonthlyRentalMode() {
        return getSelectedRentalKey() === "Monthly";
    }

    function flashDatesSynced() {
        const $sec = $("#res-section-dates");
        $sec.removeClass("res-dates-sync-pulse");
        // reflow so repeated flashes animate
        void $sec[0].offsetWidth;
        $sec.addClass("res-dates-sync-pulse");
        window.clearTimeout(flashDatesSynced._t);
        flashDatesSynced._t = window.setTimeout(() => {
            $sec.removeClass("res-dates-sync-pulse");
        }, 700);
    }

    function applyRentalDurationVisibility() {
        const monthly = getSelectedRentalKey() === "Monthly";
        $("#res-date-cell-months").toggle(monthly);
        $("#res-date-cell-nights").toggle(!monthly);
        applyMonthlyCalendarVisibility();
    }

    const RENTAL_PERIODS_PERMISSION = "reservations.rental_periods";

    function canManageRentalPeriods() {
        return hasPmsPermission(RENTAL_PERIODS_PERMISSION);
    }

    function getDetailPeriodsPayload(detail) {
        const d = detail || pageCtx.detail || {};
        const periods = d.periods;
        if (!periods || typeof periods !== "object") {
            return { items: [], hasMixedRentalPeriods: false, activeRentalType: null };
        }

        const items = Array.isArray(periods.items) ? periods.items : [];
        return {
            items,
            hasMixedRentalPeriods: !!periods.hasMixedRentalPeriods,
            activeRentalType: periods.activeRentalType || null
        };
    }

    function hasReservationPricingPeriods(detail) {
        return getDetailPeriodsPayload(detail).items.length > 0;
    }

    function localizeApiMessage(msg) {
        const raw = `${msg || ""}`.trim();
        if (!raw) {
            return t("error.generic") || "Error";
        }

        const tr = t(raw);
        return tr !== raw ? tr : raw;
    }

    function formatPeriodStatusLabel(status) {
        const s = `${status || ""}`.trim().toLowerCase();
        if (s === "closed") {
            return t("reservationDetail.periods.statusClosed");
        }

        if (s === "cancelled" || s === "canceled") {
            return t("reservationDetail.periods.statusCancelled");
        }

        return t("reservationDetail.periods.statusActive");
    }

    function formatPeriodRentalLabel(rentalType) {
        return normRental(rentalType) === "Monthly"
            ? t("reservationDetail.rental.monthly")
            : t("reservationDetail.rental.daily");
    }

    function parsePeriodDepartureDate(value) {
        const d = parseDateOrNull(value);
        if (!d || Number.isNaN(d.getTime())) {
            return null;
        }

        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0);
    }

    function formatPeriodDurationLabel(row) {
        const from = parsePeriodDepartureDate(row && row.fromDate);
        const to = parsePeriodDepartureDate(row && row.toDate);
        if (!from || !to) {
            return "—";
        }

        if (normRental(row.rentalType) === "Monthly") {
            const n = Math.max(1, calendarMonthsBetween(from, to));
            if (n === 1) {
                return t("reservationDetail.periods.durationLabelMonth");
            }

            if (n === 2) {
                return t("reservationDetail.periods.durationLabelTwoMonths");
            }

            return t("reservationDetail.periods.durationLabelMonths").replace("{count}", String(n));
        }

        const n = Math.max(1, hotelNightCount(from, to));
        if (n === 1) {
            return t("reservationDetail.periods.durationLabelDay");
        }

        if (n === 2) {
            return t("reservationDetail.periods.durationLabelTwoDays");
        }

        return t("reservationDetail.periods.durationLabelDays").replace("{count}", String(n));
    }

    function computeDefaultAppendFromDate(detail) {
        const { items } = getDetailPeriodsPayload(detail);
        if (items.length) {
            const sorted = items.slice().sort((a, b) => {
                const ta = parsePeriodDepartureDate(a.toDate);
                const tb = parsePeriodDepartureDate(b.toDate);
                return (tb ? tb.getTime() : 0) - (ta ? ta.getTime() : 0);
            });
            const anchor = parsePeriodDepartureDate(sorted[0] && sorted[0].toDate);
            if (anchor) {
                return anchor;
            }
        }

        const co = getReservationCheckOutCombined();
        if (co) {
            return new Date(co.getFullYear(), co.getMonth(), co.getDate(), 0, 0, 0, 0);
        }

        return new Date();
    }

    async function refreshReservationPeriodsGridFromServer(hotelId) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || !svc.loadReservationPeriods || !pageCtx.routeId) {
            return;
        }

        try {
            const periods = await svc.loadReservationPeriods(pageCtx.routeId, hotelId);
            if (pageCtx.detail && periods) {
                pageCtx.detail.periods = periods;
                renderReservationPeriodsUi(pageCtx.detail);
            }
        } catch {
            /* keep last known grid */
        }
    }

    function buildPeriodGrossRateFormItem() {
        return {
            dataField: "grossRate",
            editorType: "dxNumberBox",
            label: { text: t("reservationDetail.periods.grossRateRequired") },
            isRequired: true,
            editorOptions: {
                min: 0.01,
                showClearButton: false,
                format: { type: "fixedPoint", precision: 2 }
            },
            validationRules: [
                {
                    type: "required",
                    message: t("reservationDetail.periods.grossRateRequiredValidation")
                },
                {
                    type: "custom",
                    message: t("reservationDetail.periods.grossRateRequiredValidation"),
                    validationCallback(e) {
                        const n = Number(e.value);
                        return Number.isFinite(n) && n > 0;
                    }
                }
            ]
        };
    }

    function readPeriodGrossRateFromForm(fd) {
        const n = Number(fd && fd.grossRate);
        if (!Number.isFinite(n) || n <= 0) {
            return null;
        }

        return Math.round(n * 100) / 100;
    }

    function isPeriodRowActive(row) {
        return `${(row && row.status) || ""}`.trim().toLowerCase() === "active";
    }

    function buildPeriodsGridColumns() {
        const editCol = {
            name: "periodEdit",
            type: "buttons",
            width: 48,
            minWidth: 48,
            fixed: true,
            fixedPosition: reservationGridActionFixedPosition(),
            visible: false,
            caption: "",
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: t("reservationDetail.periods.editHint"),
                    icon: "edit",
                    visible(e) {
                        return isPeriodRowActive(e.row && e.row.data);
                    },
                    onClick(e) {
                        if (e.event && typeof e.event.stopPropagation === "function") {
                            e.event.stopPropagation();
                        }

                        if (e.row && e.row.data) {
                            openEditRentalPeriodPopup(e.row.data);
                        }
                    }
                }
            ]
        };

        const dataCols = [
            {
                dataField: "rentalType",
                caption: t("reservationDetail.periods.rentalType"),
                width: 128,
                minWidth: 112,
                cssClass: "res-periods-col-rental",
                alignment: "center",
                customizeText(e) {
                    return formatPeriodRentalLabel(e.value);
                }
            },
            {
                dataField: "fromDate",
                caption: t("reservationDetail.periods.fromDate"),
                width: 108,
                minWidth: 100,
                cssClass: "res-periods-col-date",
                customizeText(e) {
                    return formatCheckoutDateOnly(e.value);
                }
            },
            {
                dataField: "toDate",
                caption: t("reservationDetail.periods.toDate"),
                width: 108,
                minWidth: 100,
                cssClass: "res-periods-col-date",
                customizeText(e) {
                    return formatCheckoutDateOnly(e.value);
                }
            },
            {
                caption: t("reservationDetail.periods.durationColumn"),
                width: 96,
                minWidth: 88,
                allowSorting: false,
                calculateCellValue(row) {
                    return formatPeriodDurationLabel(row);
                }
            },
            {
                dataField: "grossRate",
                caption: t("reservationDetail.periods.grossRate"),
                width: 96,
                minWidth: 88,
                alignment: "right",
                customizeText(e) {
                    return formatCheckoutMoney(e.value);
                }
            },
            {
                dataField: "status",
                caption: t("reservationDetail.periods.statusColumn"),
                width: 92,
                minWidth: 84,
                customizeText(e) {
                    return formatPeriodStatusLabel(e.value);
                }
            }
        ];

        return isArabic() ? [editCol, ...dataCols] : [...dataCols, editCol];
    }

    function refreshPeriodsGridColumns() {
        try {
            const grid = $("#resPeriodsGrid").dxDataGrid("instance");
            if (!grid) {
                return;
            }

            grid.option("columns", buildPeriodsGridColumns());
            grid.option("rtlEnabled", isArabic());
        } catch {
            /* grid not ready */
        }
    }

    function initReservationPeriodsGrid() {
        const $host = $("#resPeriodsGrid");
        if (!$host.length || $host.data("dxDataGrid")) {
            return;
        }

        $host.addClass("res-periods-grid-host").dxDataGrid(
            reservationSectionDataGridOptions({
                dataSource: [],
                showBorders: true,
                columnAutoWidth: false,
                wordWrapEnabled: false,
                searchPanel: { visible: false },
                paging: { pageSize: 10 },
                pager: { visible: false },
                scrolling: {
                    mode: "standard",
                    columnRenderingMode: "standard",
                    scrollByContent: true,
                    scrollByThumb: true,
                    showScrollbar: "always",
                    useNative: false
                },
                columns: buildPeriodsGridColumns()
            })
        );
    }

    function applyRentalTypeLockForPeriods(detail) {
        if (!hasReservationPricingPeriods(detail)) {
            return;
        }

        trySetDevExtremeOption("#resRentalGroup", "disabled", true);
        trySetDevExtremeOption("#resRentalGroup", "readOnly", true);
    }

    function initAppendRentalPeriodButtonIfNeeded() {
        const $btn = $("#btnAppendRentalPeriod");
        if (!$btn.length) {
            return null;
        }

        try {
            return $btn.dxButton("instance");
        } catch {
            /* not initialized */
        }

        $btn.dxButton({
            text: t("reservationDetail.periods.appendAction"),
            icon: "add",
            type: "default",
            stylingMode: "contained",
            visible: false,
            onClick() {
                openAppendRentalPeriodPopup();
            }
        });
        return $btn.dxButton("instance");
    }

    function canShowRentalPeriodsAppend() {
        return !!(canManageRentalPeriods() && pageCtx.routeId && !reservationGridsActionsDisabled());
    }

    function canShowRentalPeriodsGrid() {
        return !!(pageCtx.routeId && !reservationGridsActionsDisabled());
    }

    function renderReservationPeriodsUi(detail) {
        const canAppend = canShowRentalPeriodsAppend();
        const showGrid = canShowRentalPeriodsGrid();
        const $wrap = $("#res-periods-wrap");
        const appendBtn = initAppendRentalPeriodButtonIfNeeded();

        if (appendBtn) {
            appendBtn.option("visible", canAppend);
            appendBtn.option("disabled", !canAppend);
        }

        if (!$wrap.length) {
            applyRentalTypeLockForPeriods(detail);
            return;
        }

        const payload = getDetailPeriodsPayload(detail);
        const hasItems = payload.items.length > 0;

        if (!showGrid || !hasItems) {
            $wrap.addClass("res-periods-wrap--hidden");
            applyRentalTypeLockForPeriods(detail);
            return;
        }

        $wrap.removeClass("res-periods-wrap--hidden");

        const $badge = $("#resPeriodsBadge");
        if ($badge.length) {
            const showBadge = payload.hasMixedRentalPeriods || hasItems;
            $badge.toggle(showBadge);
            $badge.text(
                payload.hasMixedRentalPeriods
                    ? t("reservationDetail.periods.mixedBadge")
                    : hasItems
                      ? t("reservationDetail.periods.listTitle")
                      : ""
            );
        }

        initReservationPeriodsGrid();
        refreshPeriodsGridColumns();
        try {
            const grid = $("#resPeriodsGrid").dxDataGrid("instance");
            if (grid) {
                grid.option("dataSource", payload.items.slice());
                grid.option("visible", true);
                try {
                    grid.columnOption("periodEdit", "visible", canAppend);
                } catch {
                    /* column not ready */
                }
            }
        } catch {
            /* grid not ready */
        }

        applyRentalTypeLockForPeriods(detail);
    }

    function getAppendPeriodRentalKey(formInst, rentalInst) {
        if (rentalInst) {
            const keys = rentalInst.option("selectedItemKeys") || [];
            if (keys[0]) {
                return keys[0];
            }
        }

        const fd = formInst ? formInst.option("formData") : {};
        return fd.rentalType || "Daily";
    }

    function computeAppendPeriodToDate(fromDate, rentalKey, durationCount) {
        const from = fromDate instanceof Date ? fromDate : parseDateOrNull(fromDate);
        const count = Math.max(1, Math.floor(Number(durationCount)) || 1);
        if (!from || Number.isNaN(from.getTime())) {
            return null;
        }

        if (normRental(rentalKey) === "Monthly") {
            return addCalendarMonths(
                new Date(from.getFullYear(), from.getMonth(), from.getDate()),
                count
            );
        }

        const d = new Date(from.getFullYear(), from.getMonth(), from.getDate());
        d.setDate(d.getDate() + count);
        return d;
    }

    function syncAppendPeriodToDateFromDuration(formInst, rentalInst) {
        if (!formInst) {
            return;
        }

        const fd = formInst.option("formData") || {};
        if (!fd.fromDate || fd.durationCount == null || fd.durationCount === "") {
            return;
        }

        const to = computeAppendPeriodToDate(
            fd.fromDate,
            getAppendPeriodRentalKey(formInst, rentalInst),
            fd.durationCount
        );
        if (to) {
            formInst.updateData("toDate", to);
        }
    }

    function syncAppendPeriodDurationFromToDate(formInst, rentalInst) {
        if (!formInst) {
            return;
        }

        const fd = formInst.option("formData") || {};
        if (!fd.fromDate || !fd.toDate) {
            return;
        }

        const from = fd.fromDate instanceof Date ? fd.fromDate : parseDateOrNull(fd.fromDate);
        const to = fd.toDate instanceof Date ? fd.toDate : parseDateOrNull(fd.toDate);
        if (!from || !to || to <= from) {
            return;
        }

        let count;
        if (normRental(getAppendPeriodRentalKey(formInst, rentalInst)) === "Monthly") {
            count = Math.max(1, calendarMonthsBetween(from, to));
        } else {
            count = Math.max(1, hotelNightCount(from, to));
        }

        formInst.updateData("durationCount", count);
    }

    function openAppendRentalPeriodPopup() {
        if (!canManageRentalPeriods() || !pageCtx.routeId) {
            return;
        }

        const detail = pageCtx.detail || {};
        const defaultFrom = computeDefaultAppendFromDate(detail);
        const activeRental = getDetailPeriodsPayload(detail).activeRentalType;
        const oppositeRental =
            normRental(activeRental || getSelectedRentalKey()) === "Monthly" ? "Daily" : "Monthly";

        const $host = $("<div>").appendTo("body");
        let formInst = null;
        let rentalInst = null;

        $host.dxPopup({
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            title: t("reservationDetail.periods.appendTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty();
                const $form = $("<div>").addClass("res-extra-form res-periods-append-form").appendTo($content);
                $form.dxForm({
                    formData: {
                        rentalType: oppositeRental,
                        fromDate: defaultFrom,
                        durationCount: normRental(oppositeRental) === "Monthly" ? 1 : 1,
                        toDate: computeAppendPeriodToDate(defaultFrom, oppositeRental, 1),
                        grossRate: null
                    },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        {
                            dataField: "rentalType",
                            label: { text: t("reservationDetail.periods.rentalType") },
                            template: (data, itemElement) => {
                                const $g = $("<div>").appendTo(itemElement);
                                $g.dxButtonGroup({
                                    items: [
                                        { text: t("reservationDetail.rental.daily"), key: "Daily" },
                                        { text: t("reservationDetail.rental.monthly"), key: "Monthly" }
                                    ],
                                    keyExpr: "key",
                                    stylingMode: "outlined",
                                    selectedItemKeys: [data.component.option("formData").rentalType || "Daily"],
                                    selectionMode: "single",
                                    onSelectionChanged(e) {
                                        const keys = e.component.option("selectedItemKeys") || [];
                                        data.component.updateData("rentalType", keys[0] || "Daily");
                                        syncAppendPeriodToDateFromDuration(formInst, rentalInst);
                                    },
                                    onInitialized(e) {
                                        rentalInst = e.component;
                                    }
                                });
                            }
                        },
                        {
                            dataField: "fromDate",
                            editorType: "dxDateBox",
                            label: { text: t("reservationDetail.periods.fromDate") },
                            editorOptions: {
                                type: "date",
                                displayFormat: "dd/MM/yyyy",
                                openOnFieldClick: true,
                                readOnly: true
                            }
                        },
                        {
                            dataField: "durationCount",
                            editorType: "dxNumberBox",
                            label: {
                                text:
                                    normRental(oppositeRental) === "Monthly"
                                        ? t("reservationDetail.periods.durationMonths")
                                        : t("reservationDetail.periods.durationNights")
                            },
                            editorOptions: {
                                min: 1,
                                showSpinButtons: true,
                                format: "#0",
                                onValueChanged() {
                                    syncAppendPeriodToDateFromDuration(formInst, rentalInst);
                                }
                            }
                        },
                        {
                            dataField: "toDate",
                            editorType: "dxDateBox",
                            label: { text: t("reservationDetail.periods.toDate") },
                            editorOptions: {
                                type: "date",
                                displayFormat: "dd/MM/yyyy",
                                openOnFieldClick: true,
                                min: defaultFrom,
                                onValueChanged() {
                                    syncAppendPeriodDurationFromToDate(formInst, rentalInst);
                                }
                            },
                            validationRules: [
                                {
                                    type: "required",
                                    message: t("reservationDetail.periods.toDateRequired")
                                }
                            ]
                        },
                        buildPeriodGrossRateFormItem()
                    ]
                });
                formInst = $form.dxForm("instance");
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.periods.appendAction"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!formInst) {
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation.isValid) {
                                return;
                            }

                            const fd = formInst.option("formData") || {};
                            const rentalKeys = rentalInst ? rentalInst.option("selectedItemKeys") || [] : [];
                            const rentalType = rentalKeys[0] || fd.rentalType || "Daily";
                            const payload = {
                                rentalType,
                                fromDate: formatLocalDateParam(fd.fromDate),
                                toDate: formatLocalDateParam(fd.toDate),
                                closePreviousPeriod: true,
                                grossRate: readPeriodGrossRateFromForm(fd)
                            };

                            if (payload.grossRate == null) {
                                return;
                            }

                            const routeId = pageCtx.routeId;
                            const hotelId = detail.hotelId;
                            const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
                            const popupInst = $host.dxPopup("instance");

                            (svc && svc.appendReservationPeriod
                                ? svc.appendReservationPeriod(routeId, payload, hotelId)
                                : Promise.reject(new Error("Service unavailable"))
                            )
                                .then(async (result) => {
                                    if (result && result.reservation) {
                                        applyPostMutationReservationDetail(result.reservation);
                                    }

                                    await refreshReservationPeriodsGridFromServer(hotelId);

                                    DevExpress.ui.notify(
                                        t("reservationDetail.periods.appendSuccess"),
                                        "success",
                                        2800
                                    );
                                    popupInst.hide();
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        localizeApiMessage(err && err.message),
                                        "error",
                                        4200
                                    );
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function openEditRentalPeriodPopup(periodRow) {
        if (!canManageRentalPeriods() || !pageCtx.routeId || !periodRow || !isPeriodRowActive(periodRow)) {
            return;
        }

        const detail = pageCtx.detail || {};
        const periodId = periodRow.periodId || periodRow.PeriodId;
        if (!periodId) {
            return;
        }

        const fromDate = parseDateOrNull(periodRow.fromDate);
        const toDate = parseDateOrNull(periodRow.toDate);
        const rentalKey = normRental(periodRow.rentalType) === "Monthly" ? "Monthly" : "Daily";
        const durationCount =
            rentalKey === "Monthly"
                ? Math.max(1, calendarMonthsBetween(fromDate, toDate))
                : Math.max(1, hotelNightCount(fromDate, toDate));

        const $host = $("<div>").appendTo("body");
        let formInst = null;
        let rentalInst = null;

        $host.dxPopup({
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            title: t("reservationDetail.periods.editTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty();
                const $form = $("<div>").addClass("res-extra-form res-periods-append-form").appendTo($content);
                $form.dxForm({
                    formData: {
                        rentalType: rentalKey,
                        fromDate,
                        durationCount,
                        toDate,
                        grossRate: periodRow.grossRate != null ? Number(periodRow.grossRate) : null
                    },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        {
                            dataField: "rentalType",
                            label: { text: t("reservationDetail.periods.rentalType") },
                            template: (data, itemElement) => {
                                const $g = $("<div>").appendTo(itemElement);
                                $g.dxButtonGroup({
                                    items: [
                                        { text: t("reservationDetail.rental.daily"), key: "Daily" },
                                        { text: t("reservationDetail.rental.monthly"), key: "Monthly" }
                                    ],
                                    keyExpr: "key",
                                    stylingMode: "outlined",
                                    selectedItemKeys: [data.component.option("formData").rentalType || "Daily"],
                                    selectionMode: "single",
                                    onSelectionChanged(e) {
                                        const keys = e.component.option("selectedItemKeys") || [];
                                        data.component.updateData("rentalType", keys[0] || "Daily");
                                        syncAppendPeriodToDateFromDuration(formInst, rentalInst);
                                    },
                                    onInitialized(e) {
                                        rentalInst = e.component;
                                    }
                                });
                            }
                        },
                        {
                            dataField: "fromDate",
                            editorType: "dxDateBox",
                            label: { text: t("reservationDetail.periods.fromDate") },
                            editorOptions: {
                                type: "date",
                                displayFormat: "dd/MM/yyyy",
                                openOnFieldClick: true,
                                readOnly: true
                            }
                        },
                        {
                            dataField: "durationCount",
                            editorType: "dxNumberBox",
                            label: {
                                text:
                                    rentalKey === "Monthly"
                                        ? t("reservationDetail.periods.editDurationMonths")
                                        : t("reservationDetail.periods.editDurationNights")
                            },
                            editorOptions: {
                                min: 1,
                                showSpinButtons: true,
                                format: "#0",
                                onValueChanged() {
                                    syncAppendPeriodToDateFromDuration(formInst, rentalInst);
                                }
                            }
                        },
                        {
                            dataField: "toDate",
                            editorType: "dxDateBox",
                            label: { text: t("reservationDetail.periods.toDate") },
                            editorOptions: {
                                type: "date",
                                displayFormat: "dd/MM/yyyy",
                                openOnFieldClick: true,
                                min: fromDate,
                                onValueChanged() {
                                    syncAppendPeriodDurationFromToDate(formInst, rentalInst);
                                }
                            },
                            validationRules: [
                                {
                                    type: "required",
                                    message: t("reservationDetail.periods.toDateRequired")
                                }
                            ]
                        },
                        buildPeriodGrossRateFormItem()
                    ]
                });
                formInst = $form.dxForm("instance");
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.periods.editAction"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!formInst) {
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation.isValid) {
                                return;
                            }

                            const fd = formInst.option("formData") || {};
                            const rentalKeys = rentalInst ? rentalInst.option("selectedItemKeys") || [] : [];
                            const payload = {
                                rentalType: rentalKeys[0] || fd.rentalType || "Daily",
                                toDate: formatLocalDateParam(fd.toDate),
                                grossRate: readPeriodGrossRateFromForm(fd)
                            };

                            if (payload.grossRate == null) {
                                return;
                            }

                            const routeId = pageCtx.routeId;
                            const hotelId = detail.hotelId;
                            const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
                            const popupInst = $host.dxPopup("instance");

                            (svc && svc.updateReservationPeriod
                                ? svc.updateReservationPeriod(routeId, periodId, payload, hotelId)
                                : Promise.reject(new Error("Service unavailable"))
                            )
                                .then(async (result) => {
                                    if (result && result.reservation) {
                                        applyPostMutationReservationDetail(result.reservation);
                                    }

                                    await refreshReservationPeriodsGridFromServer(hotelId);

                                    DevExpress.ui.notify(
                                        t("reservationDetail.periods.editSuccess"),
                                        "success",
                                        2800
                                    );
                                    popupInst.hide();
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        localizeApiMessage(err && err.message) ||
                                            t("reservationDetail.periods.editFailed"),
                                        "error",
                                        4200
                                    );
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function syncDurationFieldsFromDates(options) {
        const flash = options && options.flash;
        const ci = getReservationCheckInCombined();
        const co = getReservationCheckOutCombined();
        if (!ci || !co) {
            return;
        }

        suppressDateDurationSync = true;
        try {
            if (getSelectedRentalKey() === "Monthly") {
                const m = monthsBetweenCheckInAndCheckOut(ci, co);
                $("#resMonths").dxNumberBox("instance").option("value", m);
            } else {
                const n = hotelNightCount(ci, co);
                $("#resNights").dxNumberBox("instance").option("value", n);
            }
        } finally {
            suppressDateDurationSync = false;
        }

        if (flash) {
            flashDatesSynced();
        }

        if (!(options && options.skipFinancialRecompute)) {
            onReservationStayDatesChanged();
        }
    }

    function applyCheckOutFromNights() {
        const ci = getReservationCheckInCombined();
        const n = $("#resNights").dxNumberBox("instance").option("value");
        if (!ci || n === undefined || n === null) {
            return;
        }

        const nights = Number(n);
        if (!Number.isFinite(nights)) {
            return;
        }

        suppressDateDurationSync = true;
        try {
            setReservationCheckOutFromDateTime(defaultCheckOutFromCheckInAndNights(ci, nights));
        } finally {
            suppressDateDurationSync = false;
        }

        flashDatesSynced();
        onReservationStayDatesChanged();
    }

    function applyCheckOutFromMonths() {
        const ci = getReservationCheckInCombined();
        const m = $("#resMonths").dxNumberBox("instance").option("value");
        if (!ci || m === undefined || m === null) {
            return;
        }

        const months = Number(m);
        if (!Number.isFinite(months) || months < 0) {
            return;
        }

        suppressDateDurationSync = true;
        try {
            setReservationCheckOutFromDateTime(defaultCheckOutFromCheckInAndMonthsByMode(ci, months));
        } finally {
            suppressDateDurationSync = false;
        }

        flashDatesSynced();
        onReservationStayDatesChanged();
    }

    function wireReservationDateDurationSync() {
        ["#resCheckInDate", "#resCheckInTime", "#resCheckOutDate", "#resCheckOutTime"].forEach((sel) => {
            const inst = $(sel).dxDateBox("instance");
            if (!inst) {
                return;
            }

            inst.option({
                onValueChanged() {
                    if (suppressDateDurationSync) {
                        return;
                    }

                    syncDurationFieldsFromDates({ flash: true });
                }
            });
        });

        $("#resNights").dxNumberBox("instance").option({
            onValueChanged(e) {
                if (suppressDateDurationSync || getSelectedRentalKey() !== "Daily") {
                    return;
                }

                if (e.value === undefined || e.value === null) {
                    return;
                }

                applyCheckOutFromNights();
            }
        });

        $("#resMonths").dxNumberBox("instance").option({
            onValueChanged(e) {
                if (suppressDateDurationSync || getSelectedRentalKey() !== "Monthly") {
                    return;
                }

                if (e.value === undefined || e.value === null) {
                    return;
                }

                applyCheckOutFromMonths();
            }
        });
    }

    function buildGuestGridColumns() {
        const primaryHint = pageCtx.isLocalNewReservation ? t("reservationDetail.guest.gridAddHint") : t("reservationDetail.actions.edit");
        const primaryIcon = pageCtx.isLocalNewReservation ? "plus" : "edit";

        const actionCol = {
            type: "buttons",
            name: "guestActions",
            width: 110,
            caption: t("reservationDetail.units.actions"),
            fixed: true,
            fixedPosition: reservationGridActionFixedPosition(),
            visible: !reservationGridsActionsDisabled(),
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: primaryHint,
                    icon: primaryIcon,
                    onClick(e) {
                        openGuestEdit(e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.actions.delete"),
                    icon: "trash",
                    onClick() {
                        DevExpress.ui.notify(t("reservationDetail.stub.removeGuest"), "warning", 3200);
                    }
                }
            ]
        };

        const dataCols = [
            {
                dataField: "customerName",
                caption: t("reservationDetail.guest.name"),
                width: 200,
                minWidth: 160
            },
            {
                name: "guestIdType",
                caption: t("reservationDetail.guest.idType"),
                width: 200,
                minWidth: 180,
                calculateCellValue: (row) =>
                    (isArabic() && row.idTypeNameAr ? row.idTypeNameAr : row.idTypeName) || "—"
            },
            {
                dataField: "idNumber",
                caption: t("reservationDetail.guest.idNo"),
                width: 130,
                minWidth: 110
            },
            {
                caption: t("reservationDetail.guest.birth"),
                width: 110,
                minWidth: 100,
                calculateCellValue: (row) => {
                    if (!row.birthDate) {
                        return "—";
                    }

                    const d = new Date(row.birthDate);
                    return Number.isNaN(d.getTime()) ? "—" : enD.format(d);
                }
            },
            {
                caption: t("reservationDetail.guest.nationality"),
                width: 100,
                minWidth: 88,
                calculateCellValue: (row) =>
                    (isArabic() && row.nationalityNameAr ? row.nationalityNameAr : row.nationalityName) || "—"
            },
            {
                dataField: "mobileNo",
                caption: t("reservationDetail.guest.phone"),
                width: 130,
                minWidth: 115
            },
            {
                dataField: "email",
                caption: t("reservationDetail.guest.email"),
                visible: false,
                showInColumnChooser: false,
                allowHiding: false
            }
        ];

        const full = isArabic() ? [actionCol, ...dataCols] : [...dataCols, actionCol];
        return pickResDetailMobileColumns(full, ["customerName", "guestIdType"], 3);
    }

    function refreshGuestGridColumns() {
        const grid = $("#guestsGrid").dxDataGrid("instance");
        if (grid) {
            grid.option("columns", buildGuestGridColumns());
            grid.option("rtlEnabled", isArabic());
        }
    }

    function t(key) {
        return window.Zaaer.LocalizationService.t(key);
    }

    function isArabic() {
        return window.Zaaer.LocalizationService.currentCulture() === "ar";
    }

    function hasPmsPermission(code) {
        const svc = window.Zaaer && window.Zaaer.ApiService;
        if (!svc || typeof svc.hasPermission !== "function") {
            return false;
        }

        return svc.hasPermission(code);
    }

    let lastReservationPermissionFingerprint = null;
    let lastPaymentPermissionFingerprint = null;

    function reservationPermissionFingerprint(permissionList) {
        const svc = window.Zaaer && window.Zaaer.ApiService;
        let list = permissionList;
        if (!list && svc && typeof svc.getEffectivePermissions === "function") {
            list = svc.getEffectivePermissions();
        }

        if (!Array.isArray(list)) {
            return "";
        }

        return list
            .map((p) => String(p).toLowerCase())
            .filter(Boolean)
            .sort()
            .join("\n");
    }

    function paymentPermissionFingerprint(fullFingerprint) {
        if (!fullFingerprint) {
            return "";
        }

        const prefixes = [
            "payments.",
            "finance.receipt_voucher.",
            "finance.refund_voucher.",
            "finance.disbursement_voucher.",
            "finance.invoice.",
            "finance.promissory.",
            "finance.promissory_note.",
            "finance.document_date"
        ];

        return fullFingerprint
            .split("\n")
            .filter((code) => prefixes.some((p) => code.indexOf(p) === 0))
            .join("\n");
    }

    /** Repaint row actions when payment permissions change — grids always show document date column. */
    function refreshPaymentGridsForPermissions() {
        $(".res-payment-grid").each(function () {
            try {
                const grid = $(this).dxDataGrid("instance");
                if (!grid) {
                    return;
                }

                if (typeof grid.repaintRows === "function") {
                    grid.repaintRows();
                }
            } catch {
                /* grid not ready */
            }
        });
        refreshPaymentsActionsMenu();
    }

    function refreshPaymentsActionsMenu() {
        const $host = $("#resPaymentsActions");
        if (!$host.length) {
            return;
        }

        try {
            const btn = $host.dxDropDownButton("instance");
            if (!btn) {
                return;
            }

            const items = paymentActionItems();
            btn.option("items", items);
            btn.option("visible", items.length > 0);
            btn.option("disabled", reservationGridsActionsDisabled());
        } catch {
            /* not initialized */
        }
    }

    function ensureFreshPermissions() {
        const svc = window.Zaaer && window.Zaaer.ApiService;
        if (!svc || typeof svc.refreshPermissions !== "function") {
            return Promise.resolve();
        }

        return Promise.resolve(svc.refreshPermissions()).then(function (list) {
            refreshReservationPermissionUi({ force: true, permissions: list });
        });
    }

    const PAYMENTS_REFUND_VOUCHER_CANCEL_PERMISSION = "payments.refund_voucher.cancel";
    const FINANCE_PROMISSORY_NOTE_CANCEL_PERMISSION = "finance.promissory_note.cancel";
    const FINANCE_INVOICE_CANCEL_PERMISSION = "finance.invoice.cancel";

    function canCancelPromissoryNoteVoucher() {
        return hasPmsPermission(FINANCE_PROMISSORY_NOTE_CANCEL_PERMISSION);
    }

    function canCancelInvoiceVoucher() {
        return hasPmsPermission(FINANCE_INVOICE_CANCEL_PERMISSION);
    }

    function canCancelPaymentVoucher(kind) {
        if (kind === "disbursements") {
            return hasPmsPermission(PAYMENTS_REFUND_VOUCHER_CANCEL_PERMISSION);
        }

        if (kind === "promissory") {
            return canCancelPromissoryNoteVoucher();
        }

        if (kind === "invoices") {
            return canCancelInvoiceVoucher();
        }

        return hasPmsPermission("payments.cancel");
    }

    function canEditPaymentReceiptVoucher() {
        return hasPmsPermission("payments.receipt_voucher.edit");
    }

    function canViewPaymentReceiptVoucher() {
        return hasPmsPermission("payments.view");
    }

    function canEditPaymentRefundVoucher() {
        return hasPmsPermission("payments.refund_voucher.edit");
    }

    function canViewPaymentRefundVoucher() {
        return hasPmsPermission("payments.view");
    }

    function canUseBuildingGuardRent() {
        return hasPmsPermission("payments.building_guard_rent");
    }

    function canEditPromissoryNoteVoucher() {
        return hasPmsPermission("finance.promissory_note.edit");
    }

    function canEditPaymentVoucherByKind(kind) {
        if (kind === "disbursements") {
            return canEditPaymentRefundVoucher();
        }

        if (kind === "promissory") {
            return canEditPromissoryNoteVoucher();
        }

        return canEditPaymentReceiptVoucher();
    }

    const FINANCE_RECEIPT_VOUCHER_DOCUMENT_DATE_PERMISSION = "finance.receipt_voucher.document_date";
    const FINANCE_REFUND_VOUCHER_DOCUMENT_DATE_PERMISSION = "finance.refund_voucher.document_date";
    /** @deprecated — was separate disbursement card; grants map to refund_voucher */
    const FINANCE_DISBURSEMENT_VOUCHER_DOCUMENT_DATE_PERMISSION_LEGACY =
        "finance.disbursement_voucher.document_date";
    const FINANCE_INVOICE_DOCUMENT_DATE_PERMISSION = "finance.invoice.document_date";
    const FINANCE_PROMISSORY_DOCUMENT_DATE_PERMISSION = "finance.promissory_note.document_date";
    /** @deprecated — migration grants receipt + disbursement; kept one release as UI fallback */
    const FINANCE_DOCUMENT_DATE_PERMISSION_LEGACY = "finance.document_date";

    /** Forms only (add/edit voucher). Grids always show document date regardless of this grant. */
    function canShowFinanceDocumentDate(scope) {
        if (scope === "invoice") {
            return hasPmsPermission(FINANCE_INVOICE_DOCUMENT_DATE_PERMISSION);
        }

        if (scope === "promissory") {
            return hasPmsPermission(FINANCE_PROMISSORY_DOCUMENT_DATE_PERMISSION);
        }

        if (scope === "disbursement") {
            return (
                hasPmsPermission(FINANCE_REFUND_VOUCHER_DOCUMENT_DATE_PERMISSION) ||
                hasPmsPermission(FINANCE_DISBURSEMENT_VOUCHER_DOCUMENT_DATE_PERMISSION_LEGACY) ||
                hasPmsPermission(FINANCE_DOCUMENT_DATE_PERMISSION_LEGACY)
            );
        }

        return (
            hasPmsPermission(FINANCE_RECEIPT_VOUCHER_DOCUMENT_DATE_PERMISSION) ||
            hasPmsPermission(FINANCE_DOCUMENT_DATE_PERMISSION_LEGACY)
        );
    }

    const RESERVATION_ADJUSTMENT_ACTIONS = [
        { id: "discount", permission: "reservations.discount" },
        { id: "penalty", permission: "reservations.penalty" },
        { id: "package", permission: "reservations.package" }
    ];

    function notifyForbidden() {
        DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
    }

    function requirePmsPermission(code) {
        if (hasPmsPermission(code)) {
            return true;
        }

        notifyForbidden();
        return false;
    }

    const STAY_DATES_AFTER_CHECKIN_PERMISSION = "reservations.edit_stay_dates_after_checkin";

    function isNewReservationForPricing() {
        return (
            !pageCtx.routeId ||
            pageCtx.isLocalNewReservation ||
            pageCtx.isClientNewReservation
        );
    }

    function hasAnyCheckedInUnit() {
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        return units.some((u) => isCheckedInUnit(u));
    }

    function isReservationTreatedAsAfterCheckInForPricing() {
        if (isCheckedInReservation() || hasAnyCheckedInUnit()) {
            return true;
        }

        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        if (header.actualArrival) {
            const arrivedAt = parseDateOrNull(header.actualArrival);
            if (arrivedAt && !Number.isNaN(arrivedAt.getTime())) {
                return true;
            }
        }

        if (guestArrivalValueFromHeaderStatus(header.status) === "arrived") {
            return true;
        }

        const arrivalSwitch = $("#resGeneralArrival").dxSwitch("instance");
        if (arrivalSwitch && arrivalSwitch.option("value")) {
            return true;
        }

        return false;
    }

    /** Persisted reservation after check-in / guest arrived — pricing_edit_after_checkin gates the button only. */
    function requiresUnitPricingAfterCheckinGrant() {
        if (isNewReservationForPricing()) {
            return false;
        }

        return isReservationTreatedAsAfterCheckInForPricing();
    }

    function canViewUnitPricing() {
        if (
            hasPmsPermission("reservations.pricing_view") ||
            hasPmsPermission("reservations.pricing_edit")
        ) {
            return true;
        }

        if (!requiresUnitPricingAfterCheckinGrant()) {
            return canSaveReservation();
        }

        return hasPmsPermission("reservations.pricing_edit_after_checkin");
    }

    function canEditUnitPricing() {
        if (hasPmsPermission("reservations.pricing_edit")) {
            return true;
        }

        if (!requiresUnitPricingAfterCheckinGrant()) {
            return canSaveReservation();
        }

        return hasPmsPermission("reservations.pricing_edit_after_checkin");
    }

    function canBulkEditUnitPricing() {
        return canApplyUnitPricingInContext();
    }

    function isNewReservationForUnitPricing() {
        return (
            !pageCtx.routeId ||
            pageCtx.isLocalNewReservation ||
            pageCtx.isClientNewReservation
        );
    }

    /** New reservation: pricing follows save permission; existing: reservations.pricing_edit (+ after check-in grant). */
    function canApplyUnitPricingInContext() {
        if (isNewReservationForUnitPricing()) {
            return canSaveReservation();
        }

        return canEditUnitPricing();
    }

    function canApplyUnitPricingFromPopup() {
        return canApplyUnitPricingInContext();
    }

    function canPriceBelowMinimum() {
        return hasPmsPermission("reservations.pricing_below_minimum");
    }

    function reservationPersistPermissionCode() {
        const isNew =
            !pageCtx.routeId ||
            pageCtx.isLocalNewReservation ||
            pageCtx.isClientNewReservation;
        return isNew ? "reservations.create" : "reservations.update";
    }

    function canSaveReservation() {
        return hasPmsPermission(reservationPersistPermissionCode());
    }

    function canPersistReservationChanges() {
        if (canSaveReservation()) {
            return true;
        }

        const granularCodes = [
            "reservations.discount",
            "reservations.penalty",
            "reservations.package",
            "reservations.unit_add",
            "reservations.unit_remove",
            "reservations.unit_change",
            "reservations.company_add",
            "reservations.pricing_edit",
            STAY_DATES_AFTER_CHECKIN_PERMISSION
        ];

        if (requiresUnitPricingAfterCheckinGrant()) {
            granularCodes.push("reservations.pricing_edit_after_checkin");
        }

        return granularCodes.some((code) => hasPmsPermission(code));
    }

    function isReservationFormFieldsLocked() {
        return reservationGridsActionsDisabled() || !canPersistReservationChanges();
    }

    function trySetDevExtremeOption(selector, option, value) {
        const $el = $(selector);
        if (!$el.length) {
            return;
        }

        const types = ["dxSelectBox", "dxDateBox", "dxTextBox", "dxNumberBox", "dxSwitch", "dxButtonGroup"];
        for (let i = 0; i < types.length; i++) {
            try {
                const inst = $el[types[i]]("instance");
                if (inst) {
                    inst.option(option, value);
                    return;
                }
            } catch {
                /* not this widget */
            }
        }
    }

    function applyReservationPermissionBanner() {
        let $banner = $("#resPermissionBanner");
        if (!$banner.length) {
            $banner = $("<div>")
                .attr({ id: "resPermissionBanner", class: "res-permission-banner", role: "status", hidden: true })
                .prependTo("#reservationMain");
        }

        const checkedOut = reservationGridsActionsDisabled();
        const noPersist = !canPersistReservationChanges();

        if (checkedOut) {
            $banner.text(t("reservationDetail.permissions.checkedOut")).prop("hidden", false);
            $("body").addClass("res-perms-readonly");
        } else if (noPersist) {
            $banner.text(t("reservationDetail.permissions.readOnly")).prop("hidden", false);
            $("body").addClass("res-perms-readonly");
        } else {
            $banner.prop("hidden", true).empty();
            $("body").removeClass("res-perms-readonly");
        }
    }

    function applyReservationEditorsPermissionState() {
        const checkedOut = reservationGridsActionsDisabled();
        const hasUpdate = canSaveReservation();

        const generalLocked = checkedOut || !hasUpdate;
        const stayDatesLocked = checkedOut || !canEditStayDatesSection();

        const generalSelectors = [
            "#resGeneralStatus",
            "#resGeneralKind",
            "#resGeneralPurpose",
            "#resGeneralSource",
            "#resCmBookingNo"
        ];

        generalSelectors.forEach((sel) => {
            trySetDevExtremeOption(sel, "readOnly", generalLocked);
        });

        [
            "#resCheckInDate",
            "#resCheckInTime",
            "#resCheckOutDate",
            "#resCheckOutTime",
            "#resNights",
            "#resMonths",
            "#resRentalGroup",
            "#resCalendarGroup"
        ].forEach((sel) => {
            trySetDevExtremeOption(sel, "readOnly", stayDatesLocked);
            trySetDevExtremeOption(sel, "disabled", stayDatesLocked);
        });

        trySetDevExtremeOption(
            "#resGeneralArrival",
            "disabled",
            generalLocked || isArrivalSwitchLockedByStayState()
        );

        const $autoHost = $(".res-dates-head-auto");
        if (shouldHideAutoExtendControl()) {
            $autoHost.hide();
            trySetDevExtremeOption("#resAutoExtend", "value", true);
        } else {
            $autoHost.show();
            trySetDevExtremeOption("#resAutoExtend", "disabled", checkedOut);
        }

        applyMonthlyCalendarVisibility();
        syncMonthlyCalendarControlFromEffectiveMode();

        applyRentalTypeLockForPeriods(pageCtx.detail);
        renderReservationPeriodsUi(pageCtx.detail);

        refreshReservationActionsMenu();
        refreshNotesBadge();
    }

    function canCheckoutReservation() {
        return hasPmsPermission("reservations.check_out");
    }

    function canRecheckinReservation() {
        return hasPmsPermission("reservations.reopen");
    }

    function canAddUnit() {
        return hasPmsPermission("reservations.unit_add");
    }

    /** Multi-select in unit picker (new and existing reservations); does not replace units unless user deletes them. */
    function canBulkAddReservationUnits() {
        return hasPmsPermission("reservations.bulk_create");
    }

    function canRemoveUnit() {
        return hasPmsPermission("reservations.unit_remove");
    }

    function canTransferUnit() {
        return hasPmsPermission("reservations.unit_change");
    }

    function canUnitCheckout() {
        return hasPmsPermission("reservations.unit_check_out");
    }

    function canManagePackage() {
        return hasPmsPermission("reservations.package");
    }

    function canManageDiscount() {
        return hasPmsPermission("reservations.discount");
    }

    function canManagePenalty() {
        return hasPmsPermission("reservations.penalty");
    }

    function canAddGuest() {
        return hasPmsPermission("guests.create");
    }

    function canEditGuest() {
        return hasPmsPermission("guests.update");
    }

    /** Unit / relationship assignment on companions — allowed for any role unless reservation is checked out. */
    function canEditCompanionAssignment() {
        return !reservationGridsActionsDisabled();
    }

    function canPersistCompanions() {
        if (!isPersistedReservation() || !pageCtx.routeId || reservationGridsActionsDisabled()) {
            return false;
        }

        return canSaveReservation() || canAddGuest() || canEditGuest();
    }

    function companionRowHasUnitAndRelation(row) {
        const r = row || {};
        const unitParsed = coerceGridLookupScalar(r.unitId);
        const relationParsed = coerceGridLookupScalar(r.relationId);
        return (
            unitParsed != null &&
            unitParsed > 0 &&
            relationParsed != null &&
            relationParsed > 0
        );
    }

    /** Every companion row must have unit + relationship before persisting to the server. */
    function companionsReadyForPersist(rows) {
        const list = Array.isArray(rows) ? rows : pageCtx.companions || [];
        if (!list.length) {
            return false;
        }

        return list.every(companionRowHasUnitAndRelation);
    }

    function notifyCompanionsUnitRelationRequired() {
        DevExpress.ui.notify(t("reservationDetail.validation.companionUnitRelationRequired"), "warning", 4000);
    }

    let companionPersistTimer = null;

    function schedulePersistReservationCompanions() {
        if (!canPersistCompanions() || !companionsReadyForPersist()) {
            return;
        }

        if (companionPersistTimer) {
            clearTimeout(companionPersistTimer);
        }

        companionPersistTimer = setTimeout(function () {
            companionPersistTimer = null;
            persistReservationCompanions({ silent: true }).catch(function (err) {
                console.error("reservation-detail: auto-persist companions failed", err);
            });
        }, 450);
    }

    function persistReservationCompanions(options) {
        const opts = options || {};
        if (!canPersistCompanions()) {
            if (!opts.silent) {
                DevExpress.ui.notify(t("reservationDetail.companion.persistNeedsReservation"), "warning", 3600);
            }
            return Promise.resolve(false);
        }

        if (!companionsReadyForPersist()) {
            if (!opts.silent) {
                notifyCompanionsUnitRelationRequired();
            }
            return Promise.resolve(false);
        }

        const list = buildCompanionsPatchPayload();
        if (!list.length) {
            if (!opts.silent) {
                notifyCompanionsUnitRelationRequired();
            }
            return Promise.resolve(false);
        }
        return window.Zaaer.ReservationDetailService.patchReservation(
            pageCtx.routeId,
            { companions: list },
            pageCtx.hotelIdParam
        )
            .then(function (data) {
                pageCtx.detail = data;
                markReservationBaseline(data);
                ingestCompanionsFromDetail(data);
                refreshCompanionsGrid();
                syncLodgingPartyCards();
                if (!opts.silent) {
                    DevExpress.ui.notify(t("reservationDetail.companion.savedOk"), "success", 2200);
                }
                return true;
            })
            .catch(function (err) {
                if (!opts.silent) {
                    DevExpress.ui.notify(
                        (err && err.message) || t("reservationDetail.companion.persistFailed"),
                        "error",
                        3600
                    );
                }
                throw err;
            });
    }

    function canAddCompany() {
        return hasPmsPermission("reservations.company_add");
    }

    function canViewFinancialSummary() {
        return hasPmsPermission("reservations.financial_summary_view");
    }

    function canEditStayDatesSection() {
        if (!isPersistedReservation()) {
            return canSaveReservation();
        }

        return hasPmsPermission(STAY_DATES_AFTER_CHECKIN_PERMISSION);
    }

    function canEditRentalType() {
        return canEditStayDatesSection();
    }

    function isNewReservationForDateRules() {
        return (
            !pageCtx.routeId ||
            pageCtx.isLocalNewReservation ||
            pageCtx.isClientNewReservation ||
            pageCtx._forceFullSavePayload
        );
    }

    function isPersistedReservation() {
        return !isNewReservationForDateRules();
    }

    /** Role has «السماح بالتمديد التلقائي» — hide switch; always save is_auto_extend=true. */
    function shouldHideAutoExtendControl() {
        return hasPmsPermission("reservations.auto_extend");
    }

    function resolveIsAutoExtendForSave() {
        if (shouldHideAutoExtendControl()) {
            return true;
        }

        const inst = $("#resAutoExtend").dxSwitch("instance");
        return inst ? !!inst.option("value") : false;
    }

    function canEditCheckInDateField() {
        return canEditStayDatesSection();
    }

    function canEditCheckInTimeField() {
        return canEditStayDatesSection();
    }

    function canEditCheckOutDateField() {
        return canEditStayDatesSection();
    }

    function canEditCheckOutTimeField() {
        return canEditStayDatesSection();
    }

    const RESERVATION_DETAIL_PERMISSION_MATRIX = {
        create: "reservations.create",
        update: "reservations.update",
        autoExtend: "reservations.auto_extend",
        rentalType: STAY_DATES_AFTER_CHECKIN_PERMISSION,
        stayDatesAfterCheckin: STAY_DATES_AFTER_CHECKIN_PERMISSION,
        company: "reservations.company_add",
        unitAdd: "reservations.unit_add",
        unitRemove: "reservations.unit_remove",
        package: "reservations.package"
    };

    function canPatchReservationField(field) {
        if (canSaveReservation()) {
            return true;
        }

        const code = RESERVATION_DETAIL_PERMISSION_MATRIX[field];
        return !!code && hasPmsPermission(code);
    }

    function canEditPriceForPricingRow(row) {
        return canApplyUnitPricingInContext();
    }

    function resolveMinimumGrossForPricingRow(row) {
        if (!row) {
            return null;
        }

        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        const rid = Number(row.unitId);
        const unit =
            units.find(
                (u) =>
                    Number(u.unitId) === rid ||
                    Number(u.apartmentId) === rid ||
                    Number(u.apartmentZaaerId) === rid
            ) || null;

        const fromUnit =
            unit && unit.defaultGrossRate != null ? Number(unit.defaultGrossRate) : NaN;
        if (Number.isFinite(fromUnit) && fromUnit > 0) {
            return fromUnit;
        }

        const hotelId = pageCtx.detail && pageCtx.detail.hotelId;
        const cached = pageCtx._pickerApartmentsCache;
        if (hotelId && cached && cached.hotelId === Number(hotelId) && Array.isArray(cached.rows)) {
            const match = matchPickerRowForUnit(unit || { unitId: row.unitId, apartmentId: row.unitId }, cached.rows);
            const monthly = isMonthlyRentalMode();
            const gross = suggestedGrossFromPickerRow(match, monthly);
            if (gross > 0) {
                return gross;
            }
        }

        return null;
    }

    function validateUnitPricingApplyRows(data) {
        if (canPriceBelowMinimum()) {
            return { ok: true };
        }

        const rows = Array.isArray(data) ? data : [];
        for (let i = 0; i < rows.length; i++) {
            const row = rows[i];
            const minGross = resolveMinimumGrossForPricingRow(row);
            const price = Number(row && row.unitPrice) || 0;
            if (minGross != null && price < minGross - 0.001) {
                return { ok: false, minGross, unitLabel: row.unitNumber || row.apartmentLabel || "" };
            }
        }

        return { ok: true };
    }

    function notifyPricingBelowMinimumDenied(minGross) {
        const minText = formatMoneyEn(minGross);
        DevExpress.ui.notify(
            t("reservationDetail.financial.pricingBelowMinimumDenied").replace("{0}", minText),
            "warning",
            4500
        );
    }

    function cloneReservationBaseline(detail) {
        if (!detail || typeof detail !== "object") {
            return null;
        }

        try {
            return JSON.parse(JSON.stringify(detail));
        } catch {
            return null;
        }
    }

    function markReservationBaseline(detail) {
        pageCtx.persistedDetail = cloneReservationBaseline(detail);
    }

    function reservationBaseline() {
        return pageCtx.persistedDetail || pageCtx.detail || {};
    }

    function normalizeScalar(value) {
        return value === undefined || value === null ? "" : `${value}`.trim();
    }

    function normalizeNumberOrNull(value) {
        if (value === undefined || value === null || value === "") {
            return null;
        }

        const n = Number(value);
        return Number.isFinite(n) ? n : null;
    }

    function sameNumberValue(a, b) {
        const na = normalizeNumberOrNull(a);
        const nb = normalizeNumberOrNull(b);
        return na === nb;
    }

    function dateValueMs(value) {
        if (!value) {
            return null;
        }

        const d = value instanceof Date ? value : new Date(value);
        const ms = d.getTime();
        return Number.isNaN(ms) ? null : ms;
    }

    function sameDateTimeValue(a, b) {
        const ma = dateValueMs(a);
        const mb = dateValueMs(b);
        if (ma === null && mb === null) {
            return true;
        }

        if (ma === null || mb === null) {
            return false;
        }

        return Math.abs(ma - mb) < 120000;
    }

    function sameStringValue(a, b) {
        return normalizeScalar(a).toLowerCase() === normalizeScalar(b).toLowerCase();
    }

    function unitPatchIdentity(row) {
        if (!row) {
            return null;
        }

        const unitId = normalizeNumberOrNull(row.unitId);
        const apartmentId = normalizeNumberOrNull(row.apartmentId);
        const apartmentZaaerId = normalizeNumberOrNull(row.apartmentZaaerId);
        return unitId || apartmentId || apartmentZaaerId;
    }

    function unitApartmentPatchKey(row) {
        if (!row) {
            return "";
        }

        const unitId = normalizeNumberOrNull(row.unitId);
        const apt = normalizeNumberOrNull(row.apartmentId);
        const aptZ = normalizeNumberOrNull(row.apartmentZaaerId);
        const aptKey = apt || aptZ || "";
        if (unitId) {
            return `u:${unitId}|a:${aptKey}`;
        }

        return aptKey ? `a:${aptKey}` : "";
    }

    function hasUnitMembershipChanges(unitsPatch) {
        if (!Array.isArray(unitsPatch) || !unitsPatch.length) {
            return false;
        }

        const baselineUnits = Array.isArray(reservationBaseline().units) ? reservationBaseline().units : [];
        const before = new Set(baselineUnits.map(unitPatchIdentity).filter(Boolean));
        const after = new Set(unitsPatch.map(unitPatchIdentity).filter(Boolean));

        if (before.size !== after.size) {
            return true;
        }

        for (const id of before) {
            if (!after.has(id)) {
                return true;
            }
        }

        return false;
    }

    /** Same unit lines but different apartment (picker replace room without transfer wizard). */
    function hasUnitApartmentAssignmentChanges(unitsPatch) {
        if (!Array.isArray(unitsPatch) || !unitsPatch.length) {
            return false;
        }

        const baselineUnits = Array.isArray(reservationBaseline().units) ? reservationBaseline().units : [];
        if (!baselineUnits.length) {
            return unitsPatch.some((row) => {
                const apt = normalizeNumberOrNull(row.apartmentId);
                const aptZ = normalizeNumberOrNull(row.apartmentZaaerId);
                return !!(apt || aptZ);
            });
        }

        const beforeByUnit = new Map();
        baselineUnits.forEach((row) => {
            const uid = normalizeNumberOrNull(row.unitId);
            if (uid) {
                beforeByUnit.set(uid, unitApartmentPatchKey(row));
            }
        });

        for (let i = 0; i < unitsPatch.length; i += 1) {
            const row = unitsPatch[i] || {};
            const uid = normalizeNumberOrNull(row.unitId);
            if (!uid || !beforeByUnit.has(uid)) {
                continue;
            }

            if (beforeByUnit.get(uid) !== unitApartmentPatchKey(row)) {
                return true;
            }
        }

        const beforeKeys = new Set(baselineUnits.map(unitApartmentPatchKey).filter(Boolean));
        const afterKeys = new Set(unitsPatch.map(unitApartmentPatchKey).filter(Boolean));
        if (beforeKeys.size !== afterKeys.size) {
            return true;
        }

        for (const k of beforeKeys) {
            if (!afterKeys.has(k)) {
                return true;
            }
        }

        return false;
    }

    function shouldSendUnitsInPatch(unitsPatch) {
        if (!unitsPatch || !unitsPatch.length) {
            return false;
        }

        return (
            hasUnitMembershipChanges(unitsPatch) ||
            hasUnitApartmentAssignmentChanges(unitsPatch)
        );
    }

    function hasPayloadValues(payload) {
        return payload && typeof payload === "object" && Object.keys(payload).length > 0;
    }

    function currentCorporatePatchValue(kind) {
        if (kind !== "company" || !pageCtx.detail) {
            return undefined;
        }

        const d = pageCtx.detail;
        const pms = window.Zaaer.PmsCorporateCustomerService;
        if (d.company && pms && typeof pms.reservationCorporateId === "function") {
            const z = pms.reservationCorporateId(d.company);
            if (z != null && Number(z) > 0) {
                return Number(z);
            }
        }

        if (d.corporateId != null && Number(d.corporateId) > 0) {
            return Number(d.corporateId);
        }

        return undefined;
    }

    function refreshReservationActionsMenu() {
        const $host = $("#reservationActions");
        if (!$host.length) {
            return;
        }

        try {
            const btn = $host.dxDropDownButton("instance");
            if (!btn) {
                return;
            }

            const locked = reservationGridsActionsDisabled();
            const items = reservationActionItems();
            btn.option("items", items);
            btn.option("visible", items.length > 0);
            btn.option("disabled", locked || items.length === 0);
        } catch {
            /* not initialized */
        }
    }

    function refreshReservationPermissionUi(options) {
        const force = !!(options && options.force);
        const fp = reservationPermissionFingerprint(options && options.permissions);
        const paymentFp = paymentPermissionFingerprint(fp);

        if (!force && lastReservationPermissionFingerprint === fp) {
            return;
        }

        const paymentPermsChanged =
            force || lastPaymentPermissionFingerprint !== paymentFp;
        lastReservationPermissionFingerprint = fp;
        lastPaymentPermissionFingerprint = paymentFp;

        if (paymentPermsChanged) {
            refreshPaymentGridsForPermissions();
            refreshPaymentsActionsMenu();
        }

        refreshReservationActionsMenu();
        refreshReservationOtherOptionsMenu();
        applyReservationPermissionBanner();
        applyReservationEditorsPermissionState();
        syncReservationDependentGridsChrome();
        initFooter();

        const $fin = $("#resFinGrid");
        if ($fin.length) {
            $fin.toggle(canViewFinancialSummary());
        }

        const showUnitPricing = canViewUnitPricing();
        if (pageCtx.isHallProperty) {
            syncHallUnitPricingButton();
            const unitPricingHost = $("#btnUnitPricing").closest(
                ".res-fin-tab-action-button, .res-fin-head-actions, .res-fin-head-action-btn"
            );
            if (unitPricingHost.length) {
                unitPricingHost.toggle(
                    canEditHallRent() || showUnitPricing || canSaveReservation()
                );
            }
        } else {
            const unitPricingHost = $("#btnUnitPricing").closest(
                ".res-fin-tab-action-button, .res-fin-head-actions, .res-fin-head-action-btn"
            );
            if (unitPricingHost.length) {
                unitPricingHost.toggle(showUnitPricing);
            }

            const unitPricingBtn = $("#btnUnitPricing").dxButton("instance");
            if (unitPricingBtn) {
                unitPricingBtn.option("visible", showUnitPricing);
                unitPricingBtn.option("disabled", reservationGridsActionsDisabled() || !showUnitPricing);
            }
        }

        renderReservationPeriodsUi(pageCtx.detail);
    }

    /** Pinned edge for action columns: matches unit grid — logical left in AR (RTL), logical right in EN (LTR). */
    function reservationGridActionFixedPosition() {
        return isArabic() ? "left" : "right";
    }

    /** Header alignment on the outer edge of the fixed actions column. */
    function paymentGridActionsColumnAlignment() {
        const rtl =
            document.documentElement.dir === "rtl" ||
            (document.body && document.body.classList.contains("dx-rtl"));
        return rtl ? "left" : "right";
    }

    /** Persisted reservation in checked-out state: grid row actions and companion cell edits are off. */
    function reservationGridsActionsDisabled() {
        return isCheckedOutReservation() && !pageCtx.isLocalNewReservation;
    }

    function hijriAwarePopupHideOnOutsideClick(event) {
        const hijri = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (hijri && typeof hijri.hideOnOutsideClickForPopup === "function") {
            return hijri.hideOnOutsideClickForPopup(event);
        }
        return true;
    }

    /** Keep parent dxPopup open when the user interacts with grid lookup overlays (SelectBox, etc.). */
    function dropdownAwarePopupHideOnOutsideClick(event) {
        const raw = event && (event.originalEvent || event);
        const target = raw && raw.target;
        if (target) {
            const $t = $(target);
            if (
                $t.closest(
                    ".dx-dropdowneditor-overlay, .dx-overlay-wrapper, .dx-popup-wrapper, .dx-selectbox-popup-wrapper, .dx-list-items, .dx-scrollable, .dx-datagrid-edit-form, .res-lodging-companion-lookup-overlay"
                ).length
            ) {
                return false;
            }
        }

        return hijriAwarePopupHideOnOutsideClick(event);
    }

    function companionLookupEditorOptions(extra) {
        return Object.assign(
            {
                searchEnabled: true,
                openOnFieldClick: true,
                dropDownOptions: {
                    hideOnParentScroll: false,
                    hideOnOutsideClick: false,
                    container: document.body,
                    wrapperAttr: { class: "res-lodging-companion-lookup-overlay" }
                }
            },
            extra || {}
        );
    }

    function closeOpenHijriPickers() {
        const hijri = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (hijri && typeof hijri.closeAllPickers === "function") {
            hijri.closeAllPickers();
        }
    }

    function syncReservationDependentGridsChrome() {
        const ro = reservationGridsActionsDisabled();
        const hasUpdate = canSaveReservation();
        const cg = $("#companionsGrid").dxDataGrid("instance");
        if (cg) {
            cg.option("editing.allowUpdating", canEditCompanionAssignment());
            cg.option("rtlEnabled", isArabic());
        }

        $(".res-lodging-companions-popup__grid").each(function () {
            const popupGrid = $(this).dxDataGrid("instance");
            if (popupGrid) {
                popupGrid.option("editing.allowUpdating", canEditCompanionAssignment());
                popupGrid.option("rtlEnabled", isArabic());
            }
        });

        const ug = $("#unitsGrid").dxDataGrid("instance");
        if (ug) {
            ug.option("columns", buildUnitsGridColumns());
            ug.option("rtlEnabled", isArabic());
            applyUnitsGridLayoutOptions();
        }

        const gg = $("#guestsGrid").dxDataGrid("instance");
        if (gg) {
            gg.option("rtlEnabled", isArabic());
        }

        const compG = $("#companyGrid").dxDataGrid("instance");
        if (compG) {
            compG.option("rtlEnabled", isArabic());
        }

        const addGuest = $("#btnAddGuest").dxButton("instance");
        if (addGuest) {
            addGuest.option("disabled", ro || !canAddGuest());
        }

        const addCompanion = $("#btnAddCompanion").dxButton("instance");
        if (addCompanion) {
            addCompanion.option("disabled", ro || !canAddGuest());
        }

        const addUnit = $("#btnAddUnit").dxButton("instance");
        if (addUnit) {
            addUnit.option("disabled", ro || !canAddUnit());
        }

        const pickCompany = $("#btnPickCompany").dxButton("instance");
        if (pickCompany) {
            const kindInst = $("#resGeneralKind").dxSelectBox("instance");
            const isIndividual = !kindInst || kindInst.option("value") === "individual";
            pickCompany.option("disabled", ro || !canAddCompany() || isIndividual);
        }

        const showUnitPricing = canViewUnitPricing();
        if (pageCtx.isHallProperty) {
            syncHallUnitPricingButton();
            const unitPricingHost = $("#btnUnitPricing").closest(
                ".res-fin-tab-action-button, .res-fin-head-actions, .res-fin-head-action-btn"
            );
            if (unitPricingHost.length) {
                unitPricingHost.toggle(
                    canEditHallRent() || showUnitPricing || canSaveReservation()
                );
            }
        } else {
            const unitPricing = $("#btnUnitPricing").dxButton("instance");
            if (unitPricing) {
                unitPricing.option("visible", showUnitPricing);
                unitPricing.option("disabled", ro || !showUnitPricing);
            }

            const unitPricingHost = $("#btnUnitPricing").closest(
                ".res-fin-tab-action-button, .res-fin-head-actions, .res-fin-head-action-btn"
            );
            if (unitPricingHost.length) {
                unitPricingHost.toggle(showUnitPricing);
            }
        }

        const addExtra = $("#btnAddExtra").dxButton("instance");
        if (addExtra) {
            addExtra.option("disabled", ro || !canManagePackage());
        }

        const payActions = $("#resPaymentsActions").dxDropDownButton("instance");
        if (payActions) {
            payActions.option("disabled", ro);
        }

        syncHallMainGuestEditButton();

        const xg = $("#extrasGrid").dxDataGrid("instance");
        if (xg) {
            xg.option("editing.allowUpdating", !ro && canManagePackage());
            xg.option("editing.allowDeleting", false);
            xg.option("columns", buildExtrasGridColumns());
            xg.option("rtlEnabled", isArabic());
        }
    }

    function sourceDisplayName(source) {
        if (!source) {
            return "";
        }

        if (typeof source === "object") {
            return [source.code, source.name, source.nameAr].filter(Boolean).join(" ");
        }

        const raw = `${source}`.trim();
        const matched = (pageCtx.sources || []).find((x) => x.code === raw || x.name === raw || x.nameAr === raw);
        return matched ? sourceDisplayName(matched) : raw;
    }

    function isReceptionReservationSource(source) {
        const text = sourceDisplayName(source).toLowerCase();
        return text.includes("reception") || text.includes("استقبال");
    }

    function syncCmBookingVisibility(source) {
        // Show whenever source is not Reception (including brand-new reservations).
        const visible = !isReceptionReservationSource(source);
        const editor = $("#resCmBookingNo").dxTextBox("instance");

        if (editor) {
            editor.option("visible", visible);
        }

        $("#resCmBookingNo").toggleClass("res-field-hidden", !visible);
    }

    const enDt = new Intl.DateTimeFormat("en-GB", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        hour12: true
    });

    const enD = new Intl.DateTimeFormat("en-GB", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric"
    });

    function getCompanionUnitLookupRows() {
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        return units.map((u) => {
            const uid = Number(u.unitId);
            const lbl = u.apartmentLabel != null && `${u.apartmentLabel}`.trim() !== "" ? String(u.apartmentLabel).trim() : "";
            const code = u.apartmentCode != null && `${u.apartmentCode}`.trim() !== "" ? String(u.apartmentCode).trim() : "";
            const label = lbl && code && lbl !== code && !lbl.includes(code) ? `${code} — ${lbl}` : lbl || code;
            return {
                unitId: Number.isFinite(uid) ? uid : u.unitId,
                label: label || (Number.isFinite(uid) ? String(uid) : "")
            };
        });
    }

    function syncCompanionGridFieldFromEditor(rowKey, field, rawValue) {
        if (rowKey == null || !field) {
            return;
        }

        const coerced = coerceGridLookupScalar(rawValue);
        const val =
            coerced != null ? coerced : rawValue === undefined || rawValue === "" ? null : rawValue;
        const idx = (pageCtx.companions || []).findIndex((x) => x.rowKey === rowKey);
        if (idx < 0) {
            return;
        }

        pageCtx.companions[idx][field] = val;

        if (field === "unitId" || field === "relationId") {
            schedulePersistReservationCompanions();
        }

        if (typeof pageCtx._syncCompanionsPopupFooter === "function") {
            pageCtx._syncCompanionsPopupFooter();
        }
    }

    function companionRelationDisplayExpr(item) {
        if (!item) {
            return "";
        }

        return isArabic() ? item.nameAr || item.name || "" : item.name || item.nameAr || "";
    }

    function buildCompanionGridColumns() {
        const unitDs = getCompanionUnitLookupRows();
        const relDs = pageCtx.customerRelations || [];

        const dataCols = [
            { dataField: "customerName", caption: t("reservationDetail.guest.name"), allowEditing: false },
            {
                dataField: "unitId",
                caption: t("reservationDetail.companion.unit"),
                allowEditing: true,
                lookup: {
                    dataSource: unitDs,
                    valueExpr: "unitId",
                    displayExpr: "label"
                },
                editorOptions: companionLookupEditorOptions({
                    placeholder: t("reservationDetail.companion.selectUnit")
                })
            },
            {
                dataField: "relationId",
                caption: t("reservationDetail.companion.relation"),
                allowEditing: true,
                lookup: {
                    dataSource: relDs,
                    valueExpr: "id",
                    displayExpr: companionRelationDisplayExpr
                },
                editorOptions: companionLookupEditorOptions({
                    placeholder: t("reservationDetail.companion.selectRelation"),
                    searchExpr: ["name", "nameAr"]
                })
            },
            {
                caption: t("reservationDetail.guest.idType"),
                allowEditing: false,
                calculateCellValue(row) {
                    const ar = row.idTypeNameAr;
                    const en = row.idTypeName;
                    return (isArabic() && ar ? ar : en || ar) || "—";
                }
            },
            { dataField: "idNumber", caption: t("reservationDetail.guest.idNo"), allowEditing: false },
            {
                caption: t("reservationDetail.guest.birth"),
                allowEditing: false,
                calculateCellValue(row) {
                    if (!row.birthDate) {
                        return "—";
                    }

                    const d = new Date(row.birthDate);
                    return Number.isNaN(d.getTime()) ? "—" : enD.format(d);
                }
            },
            {
                caption: t("reservationDetail.guest.nationality"),
                allowEditing: false,
                calculateCellValue(row) {
                    return (isArabic() && row.nationalityNameAr ? row.nationalityNameAr : row.nationalityName) || "—";
                }
            },
            { dataField: "mobileNo", caption: t("reservationDetail.guest.phone"), allowEditing: false }
        ];

        const actionCol = {
            type: "buttons",
            name: "companionActions",
            width: 110,
            caption: t("reservationDetail.units.actions"),
            fixed: true,
            fixedPosition: reservationGridActionFixedPosition(),
            visible: !reservationGridsActionsDisabled(),
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: t("reservationDetail.actions.edit"),
                    icon: "edit",
                    onClick(e) {
                        openGuestEdit(e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.actions.delete"),
                    icon: "trash",
                    onClick(e) {
                        const key = e.row.data.rowKey;
                        pageCtx.companions = (pageCtx.companions || []).filter((x) => x.rowKey !== key);
                        refreshCompanionsGrid();
                        schedulePersistReservationCompanions();
                    }
                }
            ]
        };

        const full = isArabic() ? [actionCol, ...dataCols] : [...dataCols, actionCol];
        return pickResDetailMobileColumns(full, ["customerName", "unitId", "relationId"], 3);
    }

    function buildExtrasGridColumns() {
        const fp = reservationGridActionFixedPosition();
        const actionsOff = reservationGridsActionsDisabled();

        const actionCol = {
            type: "buttons",
            name: "extrasActions",
            width: 96,
            caption: t("reservationDetail.units.actions"),
            fixed: true,
            fixedPosition: fp,
            visible: !actionsOff,
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: t("reservationDetail.extras.editRow"),
                    icon: "edit",
                    visible() {
                        return !actionsOff;
                    },
                    onClick(bt) {
                        openExtraPackagePopup(bt.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.actions.delete"),
                    icon: "trash",
                    visible() {
                        return !actionsOff;
                    },
                    onClick(bt) {
                        const key = bt.row && bt.row.data && bt.row.data.rowKey;
                        if (key == null) {
                            return;
                        }

                        DevExpress.ui.dialog
                            .confirm(
                                t("reservationDetail.extras.confirmDelete"),
                                t("reservationDetail.actions.delete")
                            )
                            .done((yes) => {
                                if (!yes) {
                                    return;
                                }

                                pageCtx.extras = (pageCtx.extras || []).filter((r) => r.rowKey !== key);
                                refreshExtrasGrid();
                                updateExtrasSectionVisibility();
                                if (pageCtx.detail) {
                                    syncFinancialUi({ skipFlash: true });
                                }
                            });
                    }
                }
            ]
        };

        const dataCols = [
            { dataField: "itemName", caption: t("reservationDetail.extras.colItem") },
            {
                dataField: "postingRule",
                caption: t("reservationDetail.extras.colPostingRule"),
                allowEditing: false,
                visible: false,
                calculateCellValue(row) {
                    return extraPostingRuleText(row && row.postingRule);
                },
                width: 150
            },
            {
                name: "extrasLineDate",
                caption: t("reservationDetail.extras.colDate"),
                allowEditing: false,
                dataType: "date",
                format: "shortDate",
                width: 120,
                calculateCellValue(row) {
                    return extraRowCalendarDate(row);
                }
            },
            {
                dataField: "roomLabel",
                caption: t("reservationDetail.extras.colRoom"),
                width: 160,
                allowEditing: false,
                visible: false
            },
            {
                dataField: "guestCount",
                caption: t("reservationDetail.extras.colGuests"),
                width: 100,
                visible: false
            },
            { dataField: "nightCount", caption: t("reservationDetail.extras.colNights"), width: 100, visible: false },
            {
                dataField: "unitId",
                caption: t("reservationDetail.extras.room"),
                width: 160,
                visible: false
            },
            {
                dataField: "unitPrice",
                caption: t("reservationDetail.extras.colUnitPrice"),
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                width: 110,
                visible: false
            },
            {
                dataField: "subtotal",
                caption: t("reservationDetail.extras.colSubtotal"),
                allowEditing: false,
                visible: false,
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                width: 110
            },
            {
                dataField: "taxAmount",
                caption: t("reservationDetail.extras.colTax"),
                allowEditing: false,
                visible: false,
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                width: 100
            },
            {
                dataField: "totalAmount",
                caption: t("reservationDetail.extras.colTotal"),
                allowEditing: false,
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                width: 110
            }
        ];

        const full = isArabic() ? [actionCol, ...dataCols] : [...dataCols, actionCol];
        return pickResDetailMobileColumns(full, ["itemName", "totalAmount"], 2);
    }

    function handleCompanionGridCellValueChanged(e) {
        const key = e.key != null && e.key !== "" ? e.key : e.data && e.data.rowKey;
        if (key == null) {
            return;
        }

        const idx = (pageCtx.companions || []).findIndex((x) => x.rowKey === key);
        if (idx < 0) {
            return;
        }

        Object.assign(pageCtx.companions[idx], e.data);
        if (e.column && e.column.dataField) {
            const coerced = coerceGridLookupScalar(e.value);
            pageCtx.companions[idx][e.column.dataField] =
                coerced != null ? coerced : e.value === undefined || e.value === "" ? null : e.value;
            if (e.column.dataField === "unitId" || e.column.dataField === "relationId") {
                schedulePersistReservationCompanions();
            }
        }
    }

    function buildCompanionsDataGridOptions(extra) {
        return reservationSectionDataGridOptions(
            Object.assign(
                {
                    keyExpr: "rowKey",
                    dataSource: pageCtx.companions,
                    height: 120,
                    noDataText: t("reservationDetail.companion.emptyGrid"),
                    searchPanel: { visible: false, width: 260 },
                    editing: {
                        mode: "cell",
                        allowUpdating: canEditCompanionAssignment(),
                        allowDeleting: false
                    },
                    onEditorPreparing(e) {
                        if (reservationGridsActionsDisabled()) {
                            e.cancel = true;
                            return;
                        }

                        if (e.parentType !== "dataRow") {
                            return;
                        }

                        if (e.dataField !== "unitId" && e.dataField !== "relationId") {
                            return;
                        }

                        // Merge — replacing editorOptions discards the grid's value binding (E1010 / empty cells).
                        Object.assign(
                            e.editorOptions,
                            companionLookupEditorOptions(
                                e.dataField === "unitId"
                                    ? { placeholder: t("reservationDetail.companion.selectUnit") }
                                    : {
                                          placeholder: t("reservationDetail.companion.selectRelation"),
                                          searchExpr: ["name", "nameAr"]
                                      }
                            )
                        );

                        if (e.dataField === "unitId") {
                            e.editorOptions.dataSource = getCompanionUnitLookupRows();
                        } else {
                            e.editorOptions.dataSource = pageCtx.customerRelations || [];
                        }

                        const field = e.dataField;
                        const rowKey = e.row && e.row.data && e.row.data.rowKey;
                        const prevOnValueChanged = e.editorOptions.onValueChanged;
                        e.editorOptions.onValueChanged = function (args) {
                            if (typeof prevOnValueChanged === "function") {
                                prevOnValueChanged.apply(this, arguments);
                            }

                            syncCompanionGridFieldFromEditor(rowKey, field, args.value);
                        };
                    },
                    onCellValueChanged(e) {
                        handleCompanionGridCellValueChanged(e);
                    },
                    columns: buildCompanionGridColumns()
                },
                extra || {}
            )
        );
    }

    function updateCompanionsGridShellVisibility() {
        const hasRows = (pageCtx.companions || []).length > 0;
        $("#companionsGridShell").toggleClass("companions-grid-shell--hidden", !hasRows);
    }

    function updateGuestsGridShellVisibility() {
        const $shell = $("#guestsGridShell");
        if (!$shell.length) {
            return;
        }

        const guests = (pageCtx.detail && pageCtx.detail.guests) || [];
        const hasRows = Array.isArray(guests) && guests.length > 0;
        const hideForNewEmpty = pageCtx.isLocalNewReservation && !hasRows;
        $shell.toggleClass("guests-grid-shell--hidden", hideForNewEmpty);
    }

    function refreshCompanionsGrid() {
        const g = $("#companionsGrid").dxDataGrid("instance");

        const unitIds = new Set((pageCtx.detail?.units || []).map((u) => Number(u.unitId)));
        pageCtx.companions = (pageCtx.companions || []).map((row) => {
            if (row.unitId != null && row.unitId !== "" && !unitIds.has(Number(row.unitId))) {
                return { ...row, unitId: null };
            }

            return row;
        });

        if (g) {
            g.option("columns", buildCompanionGridColumns());
            g.option("rtlEnabled", isArabic());
            setGridDataSourceIfChanged(g, pageCtx.companions);
        }

        $(".res-lodging-companions-popup__grid").each(function () {
            try {
                const popupGrid = $(this).dxDataGrid("instance");
                if (popupGrid) {
                    popupGrid.option("editing.allowUpdating", canEditCompanionAssignment());
                    setGridDataSourceIfChanged(popupGrid, pageCtx.companions);
                }
            } catch (_) {
                /* popup may not be initialized */
            }
        });

        updateCompanionsGridShellVisibility();
        if (typeof pageCtx._syncCompanionsPopupFooter === "function") {
            pageCtx._syncCompanionsPopupFooter();
        }
        if (pageCtx.isLodgingProperty) {
            syncLodgingPartyCards();
        }
    }

    /**
     * Populate pageCtx.companions from GET detail when API returns `companions` (optional until backend persists).
     */
    function ingestCompanionsFromDetail(detail) {
        const rows = detail && Array.isArray(detail.companions) ? detail.companions : [];
        if (!rows.length) {
            pageCtx.companions = [];
            pageCtx.companionKeySeq = 1;
            return;
        }

        pageCtx.companions = rows.map((r, idx) => {
            const rk = r.rowKey != null && r.rowKey !== "" ? Number(r.rowKey) : NaN;
            const rowKey = Number.isFinite(rk) ? rk : idx + 1;
            const unitParsed = coerceGridLookupScalar(r.unitId);
            const relationParsed = coerceGridLookupScalar(r.relationId);
            return {
                ...r,
                rowKey,
                unitId: unitParsed != null ? unitParsed : null,
                relationId: relationParsed != null ? relationParsed : null
            };
        });
        pageCtx.companionKeySeq =
            pageCtx.companions.reduce((max, r) => Math.max(max, Number(r.rowKey) || 0), 0) + 1;
    }

    function ingestExtrasFromDetail(detail) {
        const rows = detail && Array.isArray(detail.extras) ? detail.extras : [];
        pageCtx.extras = rows.map((r, idx) => {
            const n = normalizeExtraRow(r, idx);
            return buildLocalExtraRow(
                {
                    packageId: n.packageId,
                    itemName: n.itemName,
                    postingRule: n.postingRule,
                    serviceDate: n.serviceDate,
                    unitId: n.unitId,
                    guestCount: n.guestCount,
                    nightCount: n.nightCount,
                    unitPrice: n.unitPrice
                },
                n
            );
        });
        pageCtx.extraKeySeq =
            pageCtx.extras.reduce((max, r) => Math.max(max, Math.abs(Number(r.rowKey)) || 0), 0) + 1;
        updateExtrasSectionVisibility();
    }

    function ingestDiscountsFromDetail(detail) {
        const rows = detail && Array.isArray(detail.discounts) ? detail.discounts : [];
        pageCtx.discounts = rows.map((r) => ({ ...r }));
        updateDiscountsSectionVisibility();
    }

    function updateDiscountsSectionVisibility() {
        const has = Array.isArray(pageCtx.discounts) && pageCtx.discounts.length > 0;
        $("#resDiscountsWrap").toggleClass("res-discounts-wrap--hidden", !has);
    }

    function roundMoney(value) {
        const n = Number(value);
        return Number.isFinite(n) ? Math.round(n * 100) / 100 : 0;
    }

    function sumActiveDiscountsFromPage() {
        return roundMoney(
            (pageCtx.discounts || []).reduce((sum, row) => {
                if (row && row.isActive === false) {
                    return sum;
                }

                return sum + (Number(row.discountAmount) || 0);
            }, 0)
        );
    }

    function pruneDiscountsForRemovedUnit(unitId) {
        const uid = Number(unitId);
        if (!Number.isFinite(uid)) {
            return;
        }

        const next = (pageCtx.discounts || []).filter((row) => {
            const scope = row.applyScope || (row.applyOn === "Rent" ? "selectedUnits" : "reservation");
            if (scope !== "selectedUnits") {
                return true;
            }

            return Number(row.unitId) !== uid;
        });

        pageCtx.discounts = next;
        if (pageCtx.detail) {
            pageCtx.detail.discounts = next.slice();
        }
    }

    function applyFinancialSnapshotToDetail(snapshot) {
        if (!pageCtx.detail || !snapshot) {
            return;
        }

        pageCtx.detail.financial = {
            ...(pageCtx.detail.financial || {}),
            subtotal: snapshot.subtotal,
            totalTaxAmount: snapshot.tax,
            totalAmount: snapshot.total,
            balanceAmount: snapshot.balance,
            amountPaid: snapshot.paid,
            totalPenalties: snapshot.penalties,
            totalDiscounts: snapshot.discounts,
            totalExtra: snapshot.totalExtra
        };
    }

    /** Single source of truth for financial summary cards (rent, extras, penalties, discounts, balance). */
    function computeReservationFinancialSnapshot() {
        const fin = (pageCtx.detail && pageCtx.detail.financial) || {};
        const paid = roundMoney(fin.amountPaid);
        const penalties = roundMoney(fin.totalPenalties);
        const discounts = sumActiveDiscountsFromPage();
        const previewExtra = roundMoney(
            (pageCtx.extras || []).reduce((sum, row) => sum + (Number(row.totalAmount) || 0), 0)
        );
        const savedExtra = roundMoney(fin.totalExtra != null ? fin.totalExtra : 0);
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        const local = computeLocalFinancialTotals();
        const hasUnitRows = units.length > 0;
        const hasPositiveRates = Object.values(pageCtx.pricingRateByLineKey || {}).some((v) => Number(v) > 0);
        const hasPricingActivity =
            pageCtx.useLocalFinancialTotals || (hasPositiveRates && local.lineCount > 0);
        const savedSubtotal = roundMoney(fin.subtotal);
        const savedTax = roundMoney(fin.totalTaxAmount);
        const savedTotal = roundMoney(fin.totalAmount);
        const hasPeriods = hasReservationPricingPeriods(pageCtx.detail);
        const hasSavedFinancials = savedTotal > 0 || savedSubtotal > 0;
        const persistedUnitsTotal = sumPersistedUnitBillableTotals();

        if (!hasUnitRows) {
            const total = roundMoney(penalties);
            return {
                subtotal: 0,
                tax: 0,
                totalExtra: previewExtra,
                penalties,
                discounts,
                total,
                paid,
                balance: roundMoney(total - discounts - paid)
            };
        }

        // Unsaved extras/packages: adjust authoritative server total by delta (avoids double-count with seeded rent rates).
        const extrasDelta = roundMoney(previewExtra - savedExtra);
        if (hasSavedFinancials && Math.abs(extrasDelta) > 0.009) {
            const total = roundMoney(savedTotal + extrasDelta);
            return {
                subtotal: savedSubtotal,
                tax: savedTax,
                totalExtra: previewExtra,
                penalties,
                discounts,
                total,
                paid,
                balance: roundMoney(total - discounts - paid)
            };
        }

        const closedUnitsNeedSavedTotals =
            hasSavedFinancials &&
            reservationHasClosedUnitLines() &&
            !localPricingCoversAllBillableUnits();

        if (
            (pageCtx.useLocalFinancialTotals || hasPricingActivity) &&
            !(hasPeriods && hasSavedFinancials && !pageCtx.useLocalFinancialTotals) &&
            !closedUnitsNeedSavedTotals
        ) {
            const localTotal = roundMoney(local.rentAndExtrasTotal + penalties);
            if (localTotal > 0 || !hasSavedFinancials) {
                return {
                    subtotal: local.subtotal,
                    tax: local.tax,
                    totalExtra: previewExtra,
                    penalties,
                    discounts,
                    total: localTotal,
                    paid,
                    balance: roundMoney(localTotal - discounts - paid)
                };
            }
        }

        if (!hasSavedFinancials && persistedUnitsTotal > 0) {
            const total = roundMoney(persistedUnitsTotal + previewExtra + penalties);
            return {
                subtotal: roundMoney(fin.subtotal != null ? fin.subtotal : 0),
                tax: roundMoney(fin.totalTaxAmount != null ? fin.totalTaxAmount : 0),
                totalExtra: previewExtra,
                penalties,
                discounts,
                total,
                paid,
                balance: roundMoney(total - discounts - paid)
            };
        }

        const deltaEx = roundMoney(previewExtra - savedExtra);
        const total = roundMoney(savedTotal + deltaEx);
        return {
            subtotal: savedSubtotal,
            tax: savedTax,
            totalExtra: previewExtra,
            penalties,
            discounts,
            total,
            paid,
            balance: roundMoney(total - discounts - paid)
        };
    }

    function syncFinancialUi(options) {
        if (!pageCtx.detail) {
            return;
        }

        const snapshot = computeReservationFinancialSnapshot();
        applyFinancialSnapshotToDetail(snapshot);
        renderFinancialPanel(pageCtx.detail, snapshot);
        renderDiscountsList();
        updateDiscountsSectionVisibility();

        if (!(options && options.skipFlash)) {
            flashFinancialSection();
        }
    }

    function discountScopeLabel(scope) {
        return scope === "selectedUnits"
            ? t("reservationDetail.actions.scopeSelectedUnits")
            : t("reservationDetail.actions.scopeReservation");
    }

    function discountMethodLabel(method) {
        const m = `${method || ""}`.toLowerCase();
        return m === "percentage" || m === "percent" || m === "%"
            ? t("reservationDetail.actions.discountPercent")
            : t("reservationDetail.actions.discountAmount");
    }

    function discountUnitDisplayLabel(row) {
        const lbl = row && row.unitLabel != null ? `${row.unitLabel}`.trim() : "";
        if (lbl && !lbl.startsWith("#")) {
            return lbl;
        }

        const uid = row && row.unitId != null ? Number(row.unitId) : NaN;
        if (Number.isFinite(uid)) {
            const u = ((pageCtx.detail && pageCtx.detail.units) || []).find((x) => Number(x.unitId) === uid);
            if (u) {
                const code = unitGridNumberOnly(u);
                if (code && code !== "—") {
                    return code;
                }
            }
        }

        return lbl.startsWith("#") ? lbl.slice(1) : lbl;
    }

    function applyDiscountMutationToPage(result) {
        if (pageCtx.detail && result) {
            if (result.financial) {
                pageCtx.detail.financial = {
                    ...(pageCtx.detail.financial || {}),
                    ...result.financial
                };
            }

            pageCtx.detail.discounts = Array.isArray(result.discounts) ? result.discounts : [];
            ingestDiscountsFromDetail(pageCtx.detail);
        }

        syncFinancialUi();
    }

    function renderDiscountsList() {
        const $wrap = $("#resDiscountsWrap");
        if (!$wrap.length) {
            return;
        }

        const rows = pageCtx.discounts || [];
        updateDiscountsSectionVisibility();
        $wrap.empty();

        if (!rows.length) {
            return;
        }

        const $list = $("<div>").addClass("res-discounts-list").appendTo($wrap);
        rows.forEach((row) => {
            const scope = row.applyScope || (row.applyOn === "Rent" ? "selectedUnits" : "reservation");
            const method = row.calculationMethod || "Amount";
            const isPercent = `${method}`.toLowerCase() === "percentage";
            const valueText = isPercent
                ? `${Number(row.calculationValue) || 0}%`
                : formatMoneyEn(row.calculationValue);
            const unitDisplay =
                scope === "selectedUnits" ? discountUnitDisplayLabel(row) : "";
            const unitPart =
                scope === "selectedUnits" && unitDisplay
                    ? `${t("reservationDetail.discounts.unit")}: ${unitDisplay}`
                    : "";

            const $card = $("<div>").addClass("res-discount-card").appendTo($list);
            const $cardMain = $("<div>").addClass("res-discount-card-main").appendTo($card);
            $("<div>")
                .addClass("res-discount-card-meta")
                .append(
                    $("<span>").text(`${t("reservationDetail.actions.discountScope")}: ${discountScopeLabel(scope)}`),
                    unitPart ? $("<span>").text(unitPart) : null,
                    $("<span>").text(
                        `${discountMethodLabel(method)}: ${valueText} → ${formatMoneyEn(row.discountAmount)}`
                    )
                )
                .appendTo($cardMain);

            if (canManageDiscount()) {
                const $actions = $("<div>").addClass("res-discount-card-actions").appendTo($card);
                $("<div>")
                    .dxButton({
                        icon: "edit",
                        hint: t("reservationDetail.discounts.edit"),
                        stylingMode: "text",
                        type: "normal",
                        elementAttr: { class: "res-discount-icon-btn res-discount-icon-btn--edit" },
                        onClick() {
                            openReservationDiscountPopup(row);
                        }
                    })
                    .appendTo($actions);
                $("<div>")
                    .dxButton({
                        icon: "trash",
                        hint: t("reservationDetail.discounts.delete"),
                        stylingMode: "text",
                        type: "normal",
                        elementAttr: { class: "res-discount-icon-btn res-discount-icon-btn--delete" },
                        onClick() {
                            confirmDeleteDiscount(row);
                        }
                    })
                    .appendTo($actions);
            }
        });
    }

    function confirmDeleteDiscount(row) {
        if (!requirePmsPermission("reservations.discount")) {
            return;
        }

        const discountId = row && row.discountId != null ? Number(row.discountId) : NaN;
        if (!Number.isFinite(discountId) || discountId <= 0) {
            return;
        }

        DevExpress.ui.dialog.confirm(
            t("reservationDetail.discounts.confirmDelete"),
            t("reservationDetail.discounts.delete")
        ).done((ok) => {
            if (!ok) {
                return;
            }

            void deleteReservationDiscount(discountId);
        });
    }

    async function deleteReservationDiscount(discountId) {
        const routeId =
            pageCtx.routeId ||
            (pageCtx.detail && pageCtx.detail.zaaerId) ||
            (pageCtx.detail && pageCtx.detail.reservationId);
        if (routeId == null || routeId === "") {
            DevExpress.ui.notify(t("reservationDetail.missingId"), "warning", 3200);
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        lp.show();
        try {
            const result = await window.Zaaer.ReservationDetailService.deleteDiscount(
                discountId,
                Number(routeId),
                pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId)
            );
            applyDiscountMutationToPage(result);
            DevExpress.ui.notify(t("reservationDetail.discounts.deleted"), "success", 2800);
        } catch (err) {
            const msg = err && err.message ? String(err.message) : t("error.loadReservationDetail");
            DevExpress.ui.notify(msg, "error", 4200);
        } finally {
            lp.hide();
        }
    }

    function normalizeExtraRow(row, idx) {
        const r = row || {};
        const extraId = r.extraId != null && r.extraId !== "" ? Number(r.extraId) : null;
        const rowKey =
            r.rowKey != null && r.rowKey !== ""
                ? r.rowKey
                : extraId != null && Number.isFinite(extraId)
                  ? extraId
                  : `local-extra-${idx + 1}`;

        return {
            ...r,
            rowKey,
            extraId,
            postingRule: r.postingRule || "OnCheckIn",
            guestCount: Number(r.guestCount) || 1,
            nightCount: Number(r.nightCount) || 1,
            unitPrice: Number(r.unitPrice) || 0,
            subtotal: Number(r.subtotal) || 0,
            taxAmount: Number(r.taxAmount) || 0,
            totalAmount: Number(r.totalAmount) || 0
        };
    }

    function updateExtrasSectionVisibility() {
        const has = Array.isArray(pageCtx.extras) && pageCtx.extras.length > 0;
        const hallMode = !!pageCtx.isHallProperty;
        const hideForHotel = !!pageCtx.isHotelProperty;
        const showSection = !hideForHotel && (hallMode || has);
        const showAddPackage = !hideForHotel && (hallMode || has);

        const $root = $("#resGuestsExtrasRoot");
        if ($root.length) {
            $root.toggleClass("res-guests-extras--hotel-hidden", hideForHotel);
            if (!hideForHotel) {
                $root.toggleClass("res-guests-extras--empty", !showSection);
            }
        }

        const $shell = $("#extrasGridShell");
        if ($shell.length) {
            $shell.toggleClass("extras-grid-shell--hidden", !has);
        }

        const $addExtra = $("#btnAddExtra");
        if ($addExtra.length) {
            $addExtra.toggleClass("res-extras-add-btn--hidden", !showAddPackage);
            try {
                const addInst = $addExtra.dxButton("instance");
                if (addInst) {
                    addInst.option("visible", showAddPackage);
                }
            } catch {
                /* not initialized yet */
            }
        }
    }

    function refreshExtrasGrid() {
        const g = $("#extrasGrid").dxDataGrid("instance");
        if (g) {
            g.option("columns", buildExtrasGridColumns());
            setGridDataSourceIfChanged(g, pageCtx.extras || []);
        }
        updateExtrasSectionVisibility();
    }

    function buildExtrasSummaryPopupColumns() {
        return [
            {
                dataField: "itemName",
                caption: t("reservationDetail.extras.colItem"),
                minWidth: 180
            },
            {
                dataField: "postingRule",
                caption: t("reservationDetail.extras.colPostingRule"),
                width: 130,
                calculateCellValue(row) {
                    return extraPostingRuleText(row && row.postingRule);
                }
            },
            {
                name: "extrasLineDate",
                caption: t("reservationDetail.extras.colDate"),
                width: 110,
                calculateCellValue(row) {
                    return extraRowCalendarDate(row);
                },
                customizeText(cellInfo) {
                    const d = cellInfo.value;
                    if (!d) {
                        return "—";
                    }
                    const dt = d instanceof Date ? d : new Date(d);
                    return Number.isNaN(dt.getTime()) ? "—" : enD.format(dt);
                }
            },
            {
                dataField: "unitPrice",
                caption: t("reservationDetail.extras.colUnitPrice"),
                width: 110,
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                customizeText(cellInfo) {
                    return formatMoneyEn(cellInfo.value);
                }
            },
            {
                dataField: "taxAmount",
                caption: t("reservationDetail.extras.colTax"),
                width: 96,
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                customizeText(cellInfo) {
                    return formatMoneyEn(cellInfo.value);
                }
            },
            {
                dataField: "totalAmount",
                caption: t("reservationDetail.extras.colTotal"),
                width: 118,
                cssClass: "res-extras-summary-total-cell",
                dataType: "number",
                format: { type: "fixedPoint", precision: 2 },
                customizeText(cellInfo) {
                    return formatMoneyEn(cellInfo.value);
                }
            }
        ];
    }

    function openReservationExtrasSummaryPopup() {
        const rows = (pageCtx.extras || []).slice();
        if (!rows.length) {
            DevExpress.ui.notify(t("reservationDetail.extras.popupEmpty"), "info", 2600);
            return;
        }

        const totalExtra = sumLocalExtrasTotalFromPage();
        const canEdit = canManagePackage() && !reservationGridsActionsDisabled();
        const $host = $("<div class='res-extras-summary-popup'/>");

        $("<p class='res-extras-summary-popup__hint'/>")
            .text(t("reservationDetail.extras.popupHint"))
            .appendTo($host);

        $("<div class='res-extras-summary-popup__stats'/>")
            .append(
                $("<div class='res-extras-summary-popup__stat'/>")
                    .append(
                        $("<span class='res-extras-summary-popup__stat-label'/>").text(
                            t("reservationDetail.extras.popupCountLabel")
                        ),
                        $("<strong class='res-extras-summary-popup__stat-value'/>").text(String(rows.length))
                    ),
                $("<div class='res-extras-summary-popup__stat res-extras-summary-popup__stat--total'/>")
                    .append(
                        $("<span class='res-extras-summary-popup__stat-label'/>").text(
                            t("reservationDetail.extras.financialExtras")
                        ),
                        $("<strong class='res-extras-summary-popup__stat-value'/>").text(formatMoneyEn(totalExtra))
                    )
            )
            .appendTo($host);

        const $gridHost = $("<div class='res-extras-summary-popup__grid'/>").appendTo($host);

        if (canEdit) {
            $("<p class='res-extras-summary-popup__foot'/>")
                .text(t("reservationDetail.extras.popupEditHint"))
                .appendTo($host);
        }

        const $popup = $("<div/>").appendTo("body");
        $popup.dxPopup({
            title: t("reservationDetail.extras.popupTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            rtlEnabled: isArabic(),
            width: Math.min(920, Math.max(540, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "72vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "guest-picker-popup res-extras-summary-popup-wrap" },
            contentTemplate() {
                return $host;
            },
            onShown() {
                if ($gridHost.data("dxDataGrid")) {
                    $gridHost.dxDataGrid("instance").option("dataSource", rows.slice());
                    return;
                }

                $gridHost.dxDataGrid(
                    reservationSectionDataGridOptions({
                        dataSource: rows,
                        keyExpr: "rowKey",
                        height: Math.min(380, Math.max(200, rows.length * 36 + 72)),
                        noDataText: t("reservationDetail.extras.popupEmpty"),
                        searchPanel: { visible: true, width: 260 },
                        paging: { pageSize: 50 },
                        pager: {
                            visible: rows.length > 50,
                            showInfo: true,
                            showNavigationButtons: true
                        },
                        elementAttr: {
                            class: canEdit
                                ? "pms-grid-compact res-extras-summary-popup__grid--editable"
                                : "pms-grid-compact"
                        },
                        columns: buildExtrasSummaryPopupColumns(),
                        onRowClick(e) {
                            if (!canEdit || !e || !e.data) {
                                return;
                            }
                            $popup.dxPopup("instance").hide();
                            openExtraPackagePopup(e.data);
                        }
                    })
                );
            },
            onHidden() {
                $popup.remove();
            }
        });
    }

    function extraPostingRuleOptions() {
        return [
            { id: "OnCheckIn", text: t("reservationDetail.extras.postingOnCheckIn") },
            { id: "OnCheckOut", text: t("reservationDetail.extras.postingOnCheckOut") },
            { id: "Daily", text: t("reservationDetail.extras.postingDaily") },
            { id: "PerStay", text: t("reservationDetail.extras.postingPerStay") },
            { id: "OnCustomDate", text: t("reservationDetail.extras.postingOnCustomDate") }
        ];
    }

    function extraPostingRuleText(value) {
        const hit = extraPostingRuleOptions().find((x) => x.id === value);
        return hit ? hit.text : value || "";
    }

    /**
     * Calendar date for extras grid: date part of created_at (record time), not service date.
     */
    function extraRowCalendarDate(row) {
        if (!row) {
            return null;
        }

        const tryParse = (v) => {
            if (v == null || v === "") {
                return null;
            }

            const d = v instanceof Date ? v : new Date(v);
            return Number.isNaN(d.getTime()) ? null : d;
        };

        const asLocalDateOnly = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate());

        const created = tryParse(row.createdAt);
        if (created) {
            return asLocalDateOnly(created);
        }

        return null;
    }

    function getExtraUnitLabel(unitId) {
        if (unitId == null || unitId === "") {
            return "";
        }

        const id = Number(unitId);
        const row = getCompanionUnitLookupRows().find((x) => Number(x.unitId) === id);
        return row ? row.label : "";
    }

    function packageDisplayExpr(item) {
        if (!item) {
            return "";
        }

        if (item.packageId === "__ADD__") {
            return t("reservationDetail.extras.addNewPackageOption");
        }

        if (item.packageId === "__POS__") {
            return t("reservationDetail.extras.posOption");
        }

        return isArabic() ? item.nameAr || item.name || "" : item.name || item.nameAr || "";
    }

    function getReservationZaaerIdForPos() {
        if (!pageCtx.detail) {
            return null;
        }

        return preferZaaerRouteKey(pageCtx.detail) || getRouteId();
    }

    function canOpenReservationPos() {
        if (pageCtx.isLocalNewReservation || pageCtx.isClientNewReservation) {
            return false;
        }

        if (!getReservationZaaerIdForPos()) {
            return false;
        }

        return (
            hasPmsPermission("reservations.package") &&
            hasPmsPermission("pos.view") &&
            hasPmsPermission("pos.orders.create")
        );
    }

    function isPosOutletOpen(outlet) {
        const status = `${(outlet && (outlet.status || outlet.Status)) || ""}`.trim().toLowerCase();
        return status === "open";
    }

    let reservationPosOutletsFetchPromise = null;

    function normalizePosOpenOutlets(outlets) {
        return (outlets || []).filter((o) => o.isActive && isPosOutletOpen(o));
    }

    function prefetchReservationPosOutlets() {
        if (!canOpenReservationPos()) {
            return null;
        }

        if (pageCtx.posOpenOutlets) {
            return $.Deferred().resolve(pageCtx.posOpenOutlets).promise();
        }

        if (reservationPosOutletsFetchPromise) {
            return reservationPosOutletsFetchPromise;
        }

        const posSvc = window.Zaaer.PosService;
        if (!posSvc || typeof posSvc.listOutlets !== "function") {
            return null;
        }

        reservationPosOutletsFetchPromise = $.when(posSvc.listOutlets())
            .done((outlets) => {
                pageCtx.posOpenOutlets = normalizePosOpenOutlets(outlets);
                prefetchReservationPosDocuments(pageCtx.posOpenOutlets);
            })
            .fail(() => {
                reservationPosOutletsFetchPromise = null;
            });

        return reservationPosOutletsFetchPromise;
    }

    function getReservationPosOutletsPromise() {
        if (pageCtx.posOpenOutlets) {
            return $.Deferred().resolve(pageCtx.posOpenOutlets).promise();
        }

        const pending = prefetchReservationPosOutlets();
        return pending || $.Deferred().resolve([]).promise();
    }

    function prefetchReservationPosDocuments(outlets) {
        const resId = getReservationZaaerIdForPos();
        if (!resId || !outlets || !outlets.length) {
            return;
        }

        outlets.slice(0, 2).forEach((o) => {
            const url = buildReservationPosIframeUrl(o.outletId);
            if (document.querySelector(`link[data-res-pos-prefetch="${url}"]`)) {
                return;
            }

            const link = document.createElement("link");
            link.rel = "prefetch";
            link.href = url;
            link.setAttribute("data-res-pos-prefetch", url);
            document.head.appendChild(link);
        });
    }

    function hideReservationPosEmbedPopup() {
        const $popup = $("#resPosEmbedPopup");
        try {
            const inst = $popup.dxPopup("instance");
            if (inst) {
                inst.hide();
            }
        } catch {
            /* not open */
        }
    }

    function buildReservationPosIframeUrl(outletId) {
        const resId = getReservationZaaerIdForPos();
        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        const resNo = header.reservationNo || header.reservationNumber || "";
        const params = new URLSearchParams({
            outletId: String(outletId),
            reservationId: String(resId),
            embedded: "reservation"
        });

        if (resNo) {
            params.set("reservationNo", String(resNo));
        }

        const hotelCode = window.Zaaer.ApiService.getHotelCode();
        if (hotelCode) {
            params.set("hotelCode", hotelCode);
        }

        return `/pos.html?${params.toString()}`;
    }

    function openReservationPosEmbedPopup(outletId) {
        let $popupHost = $("#resPosEmbedPopup");
        if (!$popupHost.length) {
            $popupHost = $("<div>").attr("id", "resPosEmbedPopup").appendTo("body");
        }

        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        const resNo = header.reservationNo || header.reservationNumber || "";
        const titleBase = t("reservationDetail.extras.posTitle");
        const title = resNo
            ? `${titleBase} — ${t("reservationDetail.extras.posTitleReservation").replace("{0}", resNo)}`
            : titleBase;

        $popupHost.dxPopup({
            fullScreen: true,
            title,
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: false,
            dragEnabled: false,
            wrapperAttr: { class: "res-pos-embed-popup" },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "top",
                    location: "before",
                    options: {
                        icon: "back",
                        stylingMode: "text",
                        type: "default",
                        hint: t("reservationDetail.extras.posBackToReservation"),
                        text: t("reservationDetail.extras.posBackToReservation"),
                        onClick() {
                            hideReservationPosEmbedPopup();
                        }
                    }
                }
            ],
            contentTemplate(contentElem) {
                const $shell = $("<div>").addClass("res-pos-embed-shell").appendTo($(contentElem).empty());
                $("<div>")
                    .addClass("res-pos-embed-loading")
                    .append($("<div>").addClass("res-pos-embed-loading__spinner"))
                    .appendTo($shell);

                const $frame = $("<iframe>")
                    .addClass("res-pos-embed-frame res-pos-embed-frame--loading")
                    .attr({ title: titleBase })
                    .on("load", function onPosEmbedFrameLoad() {
                        $shell.find(".res-pos-embed-loading").remove();
                        $(this).removeClass("res-pos-embed-frame--loading");
                    })
                    .appendTo($shell);

                window.setTimeout(() => {
                    $frame.attr("src", buildReservationPosIframeUrl(outletId));
                }, 0);
            },
            onHidden() {
                try {
                    $popupHost.dxPopup("instance").dispose();
                } catch {
                    /* not initialized */
                }

                $popupHost.remove();
            }
        });
    }

    function openReservationPosOutletPicker(outlets) {
        const $host = $("<div>").appendTo("body");
        $host.dxPopup({
            width: Math.min(480, Math.max(320, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            title: t("reservationDetail.extras.posPickOutlet"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            wrapperAttr: { class: "res-extra-popup res-pos-outlet-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty();
                const $list = $("<div>").addClass("res-pos-outlet-pick-list").appendTo($content);
                (outlets || []).forEach((o) => {
                    const name = isArabic()
                        ? o.outletNameAr || o.outletName || ""
                        : o.outletName || o.outletNameAr || "";
                    $("<button>", { type: "button" })
                        .addClass("res-pos-outlet-pick-row")
                        .text(name)
                        .on("click", () => {
                            const inst = $host.dxPopup("instance");
                            if (inst) {
                                inst.hide();
                            }

                            openReservationPosEmbedPopup(o.outletId);
                        })
                        .appendTo($list);
                });
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function openReservationPosFromExtra(packagePopupInst) {
        if (!guardReservationModificationLocked()) {
            return;
        }

        if (!canOpenReservationPos()) {
            if (!hasPmsPermission("pos.view") || !hasPmsPermission("pos.orders.create")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            } else if (pageCtx.isLocalNewReservation || pageCtx.isClientNewReservation) {
                DevExpress.ui.notify(t("reservationDetail.extras.posSaveFirst"), "warning", 3200);
            }

            return;
        }

        const posSvc = window.Zaaer.PosService;
        if (!posSvc || typeof posSvc.listOutlets !== "function") {
            DevExpress.ui.notify(t("common.error"), "error", 3200);
            return;
        }

        if (packagePopupInst) {
            try {
                packagePopupInst.hide();
            } catch {
                /* popup may already be closing */
            }
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        const hasCachedOutlets = !!(pageCtx.posOpenOutlets && pageCtx.posOpenOutlets.length);
        if (!hasCachedOutlets && lp) {
            lp.show();
        }

        $.when(getReservationPosOutletsPromise())
            .done((active) => {
                const outlets = active || [];
                if (!outlets.length) {
                    DevExpress.ui.notify(t("reservationDetail.extras.posNoOutlets"), "warning", 3200);
                    return;
                }

                if (outlets.length === 1) {
                    openReservationPosEmbedPopup(outlets[0].outletId);
                    return;
                }

                openReservationPosOutletPicker(outlets);
            })
            .fail(() => DevExpress.ui.notify(t("common.error"), "error", 3200))
            .always(() => {
                if (lp) {
                    lp.hide();
                }
            });
    }

    function initReservationPosMessageListener() {
        if (window.__reservationPosMsgBound) {
            return;
        }

        window.__reservationPosMsgBound = true;
        window.addEventListener("message", (ev) => {
            if (!ev.data || ev.origin !== window.location.origin) {
                return;
            }

            if (ev.data.type === "pos-reservation-embed-close") {
                hideReservationPosEmbedPopup();
                return;
            }

            if (ev.data.type !== "pos-reservation-order-complete") {
                return;
            }

            hideReservationPosEmbedPopup();

            loadPage(false).then(() => {
                const orderNo = ev.data.orderNo || "";
                DevExpress.ui.notify(
                    t("reservationDetail.extras.posOrderAdded").replace("{0}", orderNo),
                    "success",
                    2800
                );
            });
        });
    }

    function packageSelectItems() {
        const packages = (pageCtx.reservationPackages || []).slice();
        if (canOpenReservationPos()) {
            packages.push({
                packageId: "__POS__",
                name: t("reservationDetail.extras.posOption"),
                nameAr: t("reservationDetail.extras.posOption"),
                unitPrice: 0,
                isPosOption: true
            });
        }

        packages.push({
            packageId: "__ADD__",
            name: t("reservationDetail.extras.addNewPackageOption"),
            nameAr: t("reservationDetail.extras.addNewPackageOption"),
            unitPrice: 0,
            isAddOption: true
        });
        return packages;
    }

    function findReservationPackage(packageId) {
        if (packageId == null || packageId === "" || packageId === "__ADD__" || packageId === "__POS__") {
            return null;
        }

        const id = Number(packageId);
        return (pageCtx.reservationPackages || []).find((p) => Number(p.packageId) === id) || null;
    }

    function penaltyDisplayExpr(item) {
        if (!item) {
            return "";
        }

        if (item.penaltyId === "__ADD__") {
            return t("reservationDetail.actions.addNewPenaltyOption");
        }

        return isArabic()
            ? item.penaltyNameAr || item.penaltyName || ""
            : item.penaltyName || item.penaltyNameAr || "";
    }

    function penaltySelectItems() {
        const penalties = (pageCtx.penaltyCatalog || []).slice();
        penalties.push({
            penaltyId: "__ADD__",
            penaltyName: t("reservationDetail.actions.addNewPenaltyOption"),
            penaltyNameAr: t("reservationDetail.actions.addNewPenaltyOption"),
            baseAmount: 0,
            isAddOption: true
        });
        return penalties;
    }

    function findPenaltyCatalog(penaltyId) {
        if (penaltyId == null || penaltyId === "" || penaltyId === "__ADD__") {
            return null;
        }

        const id = Number(penaltyId);
        return (pageCtx.penaltyCatalog || []).find((p) => Number(p.penaltyId) === id) || null;
    }

    function penaltyTypeOptions() {
        return [
            { id: "EarlyCheckIn", text: t("reservationDetail.actions.penaltyTypeEarlyCheckIn") },
            { id: "LateCheckOut", text: t("reservationDetail.actions.penaltyTypeLateCheckOut") },
            { id: "DamageFee", text: t("reservationDetail.actions.penaltyTypeDamageFee") },
            { id: "CancellationFee", text: t("reservationDetail.actions.penaltyTypeCancellationFee") },
            { id: "NoShow", text: t("reservationDetail.actions.penaltyTypeNoShow") },
            { id: "Other", text: t("reservationDetail.actions.penaltyTypeOther") }
        ];
    }

    function computeExtraExtendedFromFormData(fd) {
        const guestCount = Math.max(1, Number(fd.guestCount) || 1);
        const unitPrice = Math.max(0, Number(fd.unitPrice) || 0);
        const postingNorm = `${(fd.postingRule || "OnCheckIn")}`.trim().toLowerCase().replace(/[\s_-]+/g, "");
        const nightsDefault =
            pageCtx.detail && pageCtx.detail.dates && pageCtx.detail.dates.totalNights != null
                ? Math.max(1, Number(pageCtx.detail.dates.totalNights) || 1)
                : 1;
        const nights = Math.max(1, Number(fd.nightCount) || nightsDefault);
        let extended = unitPrice * guestCount;
        if (postingNorm === "daily" || postingNorm === "perstay") {
            extended = unitPrice * guestCount * nights;
        }

        return { extended, guestCount, nights, unitPrice, postingNorm, postingRule: fd.postingRule || "OnCheckIn" };
    }

    function buildLocalExtraRow(formData, preserve) {
        const fd = formData || {};
        const pkg = findReservationPackage(fd.packageId);
        const { extended, guestCount, nights, unitPrice, postingNorm, postingRule } = computeExtraExtendedFromFormData(fd);
        let itemName = fd.itemName != null && `${fd.itemName}`.trim() !== "" ? `${fd.itemName}`.trim() : "";
        if (!itemName && pkg) {
            itemName = packageDisplayExpr(pkg);
        }

        const calc = calculatePmsPricingAmounts(extended, getExtrasHotelTaxConfig());
        const taxAmount = Math.round((calc.ewa + calc.vat) * 100) / 100;

        const isNew =
            !preserve ||
            preserve.rowKey === undefined ||
            preserve.rowKey === null ||
            preserve.rowKey === "";
        const rowKey = isNew ? `local-extra-${pageCtx.extraKeySeq++}` : preserve.rowKey;

        return {
            rowKey,
            extraId: preserve && preserve.extraId != null ? preserve.extraId : null,
            reservationId: pageCtx.detail && pageCtx.detail.reservationId,
            unitId: fd.unitId || null,
            roomLabel: getExtraUnitLabel(fd.unitId),
            packageId: pkg ? pkg.packageId : fd.packageId != null && fd.packageId !== "__ADD__" ? fd.packageId : null,
            itemName,
            postingRule,
            serviceDate: postingNorm === "oncustomdate" ? fd.serviceDate || null : null,
            guestCount,
            nightCount: nights,
            unitPrice,
            subtotal: Math.round(calc.net * 100) / 100,
            taxAmount,
            totalAmount: Math.round(calc.total * 100) / 100,
            createdBy: preserve && preserve.createdBy != null ? preserve.createdBy : "",
            createdAt: preserve && preserve.createdAt ? preserve.createdAt : new Date().toISOString()
        };
    }

    function openCreateExtraPackagePopup(onCreated) {
        const $host = $("<div>").appendTo("body");
        let formInst = null;

        $host.dxPopup({
            width: Math.min(560, Math.max(340, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "75vh",
            title: t("reservationDetail.extras.createPackageTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            wrapperAttr: { class: "res-extra-popup" },
            contentTemplate(contentElem) {
                const hotelId = pageCtx.detail && pageCtx.detail.hotelId;
                const $content = $(contentElem).empty();
                const $form = $("<div>").addClass("res-extra-form").appendTo($content);
                $form.dxForm({
                    formData: {
                        hotelId: hotelId || null,
                        name: "",
                        nameAr: "",
                        description: "",
                        unitPrice: 0
                    },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        {
                            dataField: "name",
                            label: { text: t("reservationDetail.extras.packageName") },
                            validationRules: [
                                {
                                    type: "required",
                                    message: t("reservationDetail.extras.packageRequired")
                                }
                            ]
                        },
                        {
                            dataField: "nameAr",
                            label: { text: t("reservationDetail.extras.packageNameAr") }
                        },
                        {
                            dataField: "description",
                            editorType: "dxTextArea",
                            label: { text: t("reservationDetail.extras.packageDescription") },
                            editorOptions: {
                                minHeight: 72,
                                maxHeight: 110,
                                autoResizeEnabled: true
                            }
                        },
                        {
                            dataField: "unitPrice",
                            editorType: "dxNumberBox",
                            label: { text: t("reservationDetail.extras.unitPrice") },
                            editorOptions: {
                                min: 0,
                                format: { type: "fixedPoint", precision: 2 }
                            }
                        }
                    ]
                });
                formInst = $form.dxForm("instance");
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.extras.savePackage"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!formInst) {
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation || !validation.isValid) {
                                return;
                            }

                            const fd = formInst.option("formData") || {};
                            window.Zaaer.ReservationDetailService.createReservationPackage({
                                hotelId: fd.hotelId || null,
                                name: `${fd.name || ""}`.trim(),
                                nameAr: `${fd.nameAr || ""}`.trim() || null,
                                description: `${fd.description || ""}`.trim() || null,
                                unitPrice: Number(fd.unitPrice) || 0,
                                isActive: true,
                                sortOrder: 100
                            })
                                .then((created) => {
                                    pageCtx.reservationPackages = (pageCtx.reservationPackages || []).concat(created);
                                    DevExpress.ui.notify(t("reservationDetail.extras.packageCreated"), "success", 2200);
                                    if (typeof onCreated === "function") {
                                        onCreated(created);
                                    }
                                    $host.dxPopup("instance").hide();
                                })
                                .catch(() =>
                                    DevExpress.ui.notify(t("reservationDetail.extras.packageCreateFailed"), "error", 3200)
                                );
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function openCreatePenaltyPopup(onCreated) {
        const $host = $("<div>").appendTo("body");
        let formInst = null;

        $host.dxPopup({
            width: Math.min(560, Math.max(340, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "75vh",
            title: t("reservationDetail.actions.createPenaltyTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            wrapperAttr: { class: "res-extra-popup res-penalty-create-popup" },
            contentTemplate(contentElem) {
                const detail = pageCtx.detail || {};
                const $content = $(contentElem).empty();
                const $form = $("<div>").addClass("res-extra-form").appendTo($content);
                $form.dxForm({
                    formData: {
                        hotelId: detail.hotelId || null,
                        reservationId: detail.reservationId || null,
                        penaltyType: "Other",
                        penaltyName: "",
                        penaltyNameAr: "",
                        description: "",
                        baseAmount: 0
                    },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        {
                            dataField: "penaltyName",
                            label: { text: t("reservationDetail.actions.penaltyName") },
                            validationRules: [
                                {
                                    type: "required",
                                    message: t("reservationDetail.actions.penaltyNameRequired")
                                }
                            ]
                        },
                        {
                            dataField: "penaltyNameAr",
                            label: { text: t("reservationDetail.actions.penaltyNameAr") }
                        },
                        {
                            dataField: "penaltyType",
                            editorType: "dxSelectBox",
                            label: { text: t("reservationDetail.actions.penaltyType") },
                            editorOptions: {
                                dataSource: penaltyTypeOptions(),
                                valueExpr: "id",
                                displayExpr: "text",
                                searchEnabled: false,
                                openOnFieldClick: true
                            }
                        },
                        {
                            dataField: "baseAmount",
                            editorType: "dxNumberBox",
                            label: { text: t("reservationDetail.actions.penaltyDefaultAmount") },
                            editorOptions: {
                                min: 0,
                                format: { type: "fixedPoint", precision: 2 },
                                showSpinButtons: true
                            }
                        },
                        {
                            dataField: "description",
                            editorType: "dxTextArea",
                            label: { text: t("reservationDetail.extras.packageDescription") },
                            editorOptions: {
                                minHeight: 72,
                                maxHeight: 110,
                                autoResizeEnabled: true
                            }
                        }
                    ]
                });
                formInst = $form.dxForm("instance");
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.savePenalty"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!formInst) {
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation || !validation.isValid) {
                                return;
                            }

                            const fd = formInst.option("formData") || {};
                            window.Zaaer.ReservationDetailService.createPenaltyCatalog({
                                hotelId: fd.hotelId || null,
                                reservationId: fd.reservationId || null,
                                penaltyType: fd.penaltyType || "Other",
                                penaltyName: `${fd.penaltyName || ""}`.trim(),
                                penaltyNameAr: `${fd.penaltyNameAr || ""}`.trim() || null,
                                description: `${fd.description || ""}`.trim() || null,
                                baseAmount: Number(fd.baseAmount) || 0
                            })
                                .then((created) => {
                                    pageCtx.penaltyCatalog = (pageCtx.penaltyCatalog || []).concat(created);
                                    DevExpress.ui.notify(t("reservationDetail.actions.penaltyCreated"), "success", 2200);
                                    if (typeof onCreated === "function") {
                                        onCreated(created);
                                    }
                                    $host.dxPopup("instance").hide();
                                })
                                .catch(() =>
                                    DevExpress.ui.notify(t("reservationDetail.actions.penaltyCreateFailed"), "error", 3200)
                                );
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function openExtraPackagePopup(editRow) {
        if (!guardReservationModificationLocked()) {
            return;
        }

        if (!requirePmsPermission("reservations.package")) {
            return;
        }

        const $host = $("<div>").appendTo("body");
        let formInst = null;
        const editMode = !!editRow;
        const nightsDefault =
            pageCtx.detail && pageCtx.detail.dates && pageCtx.detail.dates.totalNights != null
                ? Math.max(1, Number(pageCtx.detail.dates.totalNights) || 1)
                : 1;

        $host.dxPopup({
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            title: editMode ? t("reservationDetail.extras.editTitle") : t("reservationDetail.extras.selectTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty();
                const initialData = editMode
                    ? {
                          packageId: editRow.packageId ?? null,
                          itemName: editRow.itemName || "",
                          postingRule: editRow.postingRule || "OnCheckIn",
                          serviceDate: editRow.serviceDate || null,
                          unitId: editRow.unitId ?? null,
                          guestCount: Math.max(1, Number(editRow.guestCount) || 1),
                          nightCount: Math.max(1, Number(editRow.nightCount) || nightsDefault),
                          unitPrice: Math.max(0, Number(editRow.unitPrice) || 0)
                      }
                    : {
                          packageId: null,
                          itemName: "",
                          postingRule: "OnCheckIn",
                          serviceDate: null,
                          unitId: null,
                          guestCount: 1,
                          nightCount: nightsDefault,
                          unitPrice: 0
                      };

                const $form = $("<div>").addClass("res-extra-form").appendTo($content);
                $form.dxForm({
                    formData: initialData,
                    labelLocation: "top",
                    colCount: 2,
                    items: [
                        {
                            dataField: "packageId",
                            editorType: "dxSelectBox",
                            colSpan: 2,
                            label: { text: t("reservationDetail.extras.packageName") },
                            editorOptions: {
                                dataSource: packageSelectItems(),
                                valueExpr: "packageId",
                                displayExpr: packageDisplayExpr,
                                searchEnabled: true,
                                searchExpr: ["name", "nameAr"],
                                placeholder: t("reservationDetail.extras.packageNamePlaceholder"),
                                noDataText: t("reservationDetail.extras.noPackages"),
                                openOnFieldClick: true,
                                onValueChanged(e) {
                                    if (!formInst) {
                                        return;
                                    }

                                    if (e.value === "__POS__") {
                                        e.component.option("value", null);
                                        const packagePopupInst = $host.dxPopup("instance");
                                        openReservationPosFromExtra(packagePopupInst);
                                        return;
                                    }

                                    if (e.value === "__ADD__") {
                                        e.component.option("value", null);
                                        openCreateExtraPackagePopup((created) => {
                                            const editor = formInst.getEditor("packageId");
                                            if (editor) {
                                                editor.option("dataSource", packageSelectItems());
                                                editor.option("value", created.packageId);
                                            }
                                            formInst.updateData("unitPrice", Number(created.unitPrice) || 0);
                                        });
                                        return;
                                    }

                                    const selected = findReservationPackage(e.value);
                                    if (selected) {
                                        formInst.updateData("unitPrice", Number(selected.unitPrice) || 0);
                                    }
                                }
                            },
                            validationRules: editMode
                                ? []
                                : [
                                      {
                                          type: "required",
                                          message: t("reservationDetail.extras.packageRequired")
                                      }
                                  ]
                        },
                        {
                            dataField: "itemName",
                            colSpan: 2,
                            label: { text: t("reservationDetail.extras.colItem") },
                            editorType: "dxTextBox",
                            editorOptions: {
                                maxLength: 400
                            }
                        },
                        {
                            dataField: "postingRule",
                            editorType: "dxSelectBox",
                            label: { text: t("reservationDetail.extras.postingRule") },
                            editorOptions: {
                                dataSource: extraPostingRuleOptions(),
                                valueExpr: "id",
                                displayExpr: "text",
                                searchEnabled: false,
                                openOnFieldClick: true,
                                onValueChanged(e) {
                                    if (!formInst) {
                                        return;
                                    }

                                    const showDate = e.value === "OnCustomDate";
                                    formInst.itemOption("serviceDate", "visible", showDate);
                                    if (!showDate) {
                                        formInst.updateData("serviceDate", null);
                                    } else if (!formInst.option("formData").serviceDate) {
                                        formInst.updateData("serviceDate", getReservationCheckInCombined() || new Date());
                                    }
                                }
                            }
                        },
                        {
                            dataField: "serviceDate",
                            editorType: "dxDateBox",
                            visible: false,
                            label: { text: t("reservationDetail.extras.serviceDate") },
                            editorOptions: {
                                type: "date",
                                displayFormat: "dd/MM/yyyy",
                                useMaskBehavior: true,
                                openOnFieldClick: true
                            }
                        },
                        {
                            dataField: "unitId",
                            editorType: "dxSelectBox",
                            label: { text: t("reservationDetail.extras.room") },
                            editorOptions: {
                                dataSource: getCompanionUnitLookupRows(),
                                valueExpr: "unitId",
                                displayExpr: "label",
                                searchEnabled: true,
                                showClearButton: true,
                                openOnFieldClick: true
                            }
                        },
                        {
                            dataField: "guestCount",
                            editorType: "dxNumberBox",
                            label: { text: t("reservationDetail.extras.guestCount") },
                            editorOptions: {
                                min: 1,
                                showSpinButtons: true,
                                format: "#0"
                            }
                        },
                        {
                            dataField: "nightCount",
                            editorType: "dxNumberBox",
                            label: { text: t("reservationDetail.extras.nightCount") },
                            editorOptions: {
                                min: 1,
                                showSpinButtons: true,
                                format: "#0"
                            }
                        },
                        {
                            dataField: "unitPrice",
                            editorType: "dxNumberBox",
                            label: { text: t("reservationDetail.extras.unitPrice") },
                            editorOptions: {
                                min: 0,
                                format: { type: "fixedPoint", precision: 2 }
                            }
                        }
                    ]
                });
                formInst = $form.dxForm("instance");
                if (editMode && (editRow.postingRule || "") === "OnCustomDate") {
                    formInst.itemOption("serviceDate", "visible", true);
                }
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: editMode ? t("reservationDetail.extras.confirmEdit") : t("reservationDetail.extras.confirmAdd"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!formInst) {
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation || !validation.isValid) {
                                return;
                            }

                            pageCtx.extras = pageCtx.extras || [];
                            const fd = formInst.option("formData");
                            if (editMode) {
                                const merged = buildLocalExtraRow(fd, editRow);
                                const idx = pageCtx.extras.findIndex((x) => x.rowKey === editRow.rowKey);
                                if (idx >= 0) {
                                    pageCtx.extras[idx] = merged;
                                }

                                DevExpress.ui.notify(t("reservationDetail.extras.updatedLocal"), "success", 2200);
                            } else {
                                pageCtx.extras.push(buildLocalExtraRow(fd));
                                DevExpress.ui.notify(t("reservationDetail.extras.addedLocal"), "success", 2200);
                            }

                            refreshExtrasGrid();
                            if (pageCtx.detail) {
                                syncFinancialUi({ skipFlash: true });
                            }

                            $host.dxPopup("instance").hide();
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function formatDateTimeEn(value) {
        if (!value) {
            return "—";
        }

        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? "—" : enDt.format(d);
    }

    function parseDateOrNull(value) {
        if (!value) {
            return null;
        }

        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? null : d;
    }

    /** DevExtreme lookups sometimes pass the whole item object instead of valueExpr. */
    function coerceGridLookupScalar(val) {
        if (val === undefined || val === null || val === "") {
            return null;
        }

        if (typeof val === "object") {
            if (val.unitId != null && val.unitId !== "") {
                const n = Number(val.unitId);
                return Number.isFinite(n) ? n : null;
            }

            if (val.id != null && val.id !== "") {
                const n = Number(val.id);
                return Number.isFinite(n) ? n : null;
            }

            if (val.key != null && val.key !== "") {
                const n = Number(val.key);
                return Number.isFinite(n) ? n : null;
            }
        }

        const n = Number(val);
        return Number.isFinite(n) ? n : null;
    }

    function renderUnitDateTextCell(container, cellInfo, dataField) {
        $(container).empty();
        const row = cellInfo && cellInfo.data;
        let raw = row && dataField ? row[dataField] : cellInfo && cellInfo.value;
        if (dataField === "checkOutDate" && isUnitLineCheckedOut(row) && row && row.departureDate) {
            raw = row.departureDate;
        }
        const parsed = parseDateOrNull(raw);
        const text = parsed ? formatCheckoutDateOnly(parsed) : "—";

        $("<span>")
            .addClass("res-unit-date-text")
            .attr("dir", "ltr")
            .text(text)
            .appendTo(container);
    }

    function unitsGridScrollOptions() {
        return {
            mode: "standard",
            columnRenderingMode: "standard",
            scrollByContent: true,
            scrollByThumb: true,
            showScrollbar: "always",
            useNative: false
        };
    }

    function applyUnitsGridLayoutOptions() {
        try {
            const grid = $("#unitsGrid").dxDataGrid("instance");
            if (!grid) {
                return;
            }

            const mobile = isPaymentsMobileViewport();
            grid.option("columnAutoWidth", !mobile);
            grid.option("wordWrapEnabled", mobile);
            grid.option("columnFixing.enabled", !mobile);
            grid.option("scrolling", unitsGridScrollOptions());
        } catch {
            /* grid not ready */
        }
    }

    function formatMoneyEn(value) {
        if (value === null || value === undefined) {
            return "—";
        }

        return Number(value).toLocaleString("en-US", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function dateKeyLocal(d) {
        const x = new Date(d);
        const y = x.getFullYear();
        const m = String(x.getMonth() + 1).padStart(2, "0");
        const day = String(x.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function addCalendarMonths(date, deltaMonths) {
        const x = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        x.setMonth(x.getMonth() + deltaMonths);
        return x;
    }

    function getReservationMonthCountForPricing() {
        const d = pageCtx.detail && pageCtx.detail.dates;
        const fromDetail = d && d.numberOfMonths != null ? Number(d.numberOfMonths) : NaN;
        if (Number.isFinite(fromDetail) && fromDetail > 0) {
            return Math.floor(fromDetail);
        }

        const mInst = $("#resMonths").dxNumberBox("instance");
        const fromUi = mInst ? Number(mInst.option("value")) : NaN;
        if (Number.isFinite(fromUi) && fromUi > 0) {
            return Math.floor(fromUi);
        }

        const ci = getReservationCheckInCombined();
        const co = getReservationCheckOutCombined();
        const t = thirtyDayMonthsBetween(ci, co);
        if (t != null && t > 0) {
            return t;
        }

        return 1;
    }

    /**
     * Raw pricing rows (daily = one per hotel night; monthly = one per calendar month block from reservation check-in).
     * When pricing periods exist, lines are scoped per closed/active segment — not the whole stay with active rental only.
     * Does not read pageCtx.pricingRateByLineKey — use getNightPricingLines() for display amounts.
     */
    function resolveUnitForPeriod(period, units) {
        const pid = period && period.unitId != null ? Number(period.unitId) : NaN;
        if (Number.isFinite(pid) && pid > 0 && Array.isArray(units)) {
            const match = units.find(
                (u) =>
                    Number(u.unitId) === pid ||
                    Number(u.apartmentId) === pid ||
                    Number(u.apartmentZaaerId) === pid
            );
            if (match) {
                return match;
            }
        }

        return units && units.length ? units[0] : null;
    }

    function buildPricingSlotLinesFromPeriods(periodItems, units) {
        const lines = [];
        const sorted = (periodItems || []).slice().sort((a, b) => new Date(a.fromDate) - new Date(b.fromDate));

        sorted.forEach((period) => {
            const u = resolveUnitForPeriod(period, units);
            if (!u) {
                return;
            }

            const from = parseDateOrNull(period.fromDate);
            const to = parseDateOrNull(period.toDate);
            if (!from || !to || to <= from) {
                return;
            }

            const rt = (u.roomTypeName && `${u.roomTypeName}`.trim()) || "—";
            const gross = Number(period.grossRate) || 0;
            const pid = period.periodId != null ? period.periodId : 0;
            const monthly = normRental(period.rentalType) === "Monthly";

            if (monthly) {
                lines.push({
                    lineKey: `${u.unitId}_p${pid}_m_0`,
                    periodIndex: 0,
                    periodId: pid,
                    periodGrossRate: gross,
                    unitId: u.unitId,
                    apartmentLabel: u.apartmentLabel || "—",
                    unitNumber: unitGridNumberOnly(u),
                    roomTypeName: rt,
                    nightDate: new Date(from.getFullYear(), from.getMonth(), from.getDate())
                });
                return;
            }

            const nNights = hotelNightCount(from, to);
            for (let i = 0; i < nNights; i += 1) {
                const d = new Date(from.getFullYear(), from.getMonth(), from.getDate() + i);
                const dk = dateKeyLocal(d);
                lines.push({
                    lineKey: `${u.unitId}_p${pid}_${dk}`,
                    periodIndex: i,
                    periodId: pid,
                    periodGrossRate: gross,
                    unitId: u.unitId,
                    apartmentLabel: u.apartmentLabel || "—",
                    unitNumber: unitGridNumberOnly(u),
                    roomTypeName: rt,
                    nightDate: d
                });
            }
        });

        return lines;
    }

    function buildRawPricingSlotLines() {
        const detail = pageCtx.detail;
        const units = (detail && detail.units) || [];
        const periodsPayload = getDetailPeriodsPayload(detail);

        if (periodsPayload.items.length > 0) {
            return buildPricingSlotLinesFromPeriods(periodsPayload.items, units);
        }

        const lines = [];
        const monthly = isMonthlyRentalMode();
        const ciRes = getReservationCheckInCombined();
        const startCal = ciRes
            ? new Date(ciRes.getFullYear(), ciRes.getMonth(), ciRes.getDate())
            : null;

        units.forEach((u) => {
            const ci = new Date(u.checkInDate);
            const co = resolveUnitCheckOutDateForPricing(u);
            if (Number.isNaN(ci.getTime()) || !co || Number.isNaN(co.getTime()) || co <= ci) {
                return;
            }

            const rt = (u.roomTypeName && `${u.roomTypeName}`.trim()) || "—";

            if (monthly) {
                if (!startCal || Number.isNaN(startCal.getTime())) {
                    return;
                }

                const months = Math.max(1, getReservationMonthCountForPricing());
                for (let m = 0; m < months; m += 1) {
                    const d = addCalendarMonths(startCal, m);
                    const lk = `${u.unitId}_m_${m}`;
                    lines.push({
                        lineKey: lk,
                        periodIndex: m,
                        unitId: u.unitId,
                        apartmentLabel: u.apartmentLabel || "—",
                        unitNumber: unitGridNumberOnly(u),
                        roomTypeName: rt,
                        nightDate: d
                    });
                }

                return;
            }

            const startUnit = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate());
            const nNights = hotelNightCount(ci, co);
            if (!nNights || nNights <= 0) {
                return;
            }

            for (let i = 0; i < nNights; i += 1) {
                const d = new Date(startUnit.getFullYear(), startUnit.getMonth(), startUnit.getDate() + i);
                const dk = dateKeyLocal(d);
                const lk = `${u.unitId}_${dk}`;
                lines.push({
                    lineKey: lk,
                    periodIndex: i,
                    unitId: u.unitId,
                    apartmentLabel: u.apartmentLabel || "—",
                    unitNumber: unitGridNumberOnly(u),
                    roomTypeName: rt,
                    nightDate: d
                });
            }
        });

        return lines;
    }

    function getNightPricingLines() {
        const rates = pageCtx.pricingRateByLineKey || {};
        return buildRawPricingSlotLines().map((ln) => ({
            ...ln,
            unitPrice: Number(rates[ln.lineKey]) || 0
        }));
    }

    /** Canonical lineKey for a unit + period date (monthly uses `unitId_m_index`, daily uses `unitId_yyyy-MM-dd`). */
    function resolvePricingSlotLineKey(unitId, nightDate) {
        const uid = Number(unitId);
        if (!Number.isFinite(uid) || uid <= 0) {
            return null;
        }

        const nd = nightDate instanceof Date ? nightDate : parseDateOrNull(nightDate);
        if (!nd || Number.isNaN(nd.getTime())) {
            return null;
        }

        const sk = dateKeyLocal(nd);
        const slot = buildRawPricingSlotLines().find((ln) => {
            if (Number(ln.unitId) !== uid) {
                return false;
            }

            const slotNight =
                ln.nightDate instanceof Date ? ln.nightDate : parseDateOrNull(ln.nightDate);
            if (!slotNight || Number.isNaN(slotNight.getTime())) {
                return false;
            }

            return dateKeyLocal(slotNight) === sk;
        });

        return slot && slot.lineKey ? slot.lineKey : null;
    }

    /**
     * Migrates legacy popup/API keys (`unitId_yyyy-MM-dd`) onto monthly slot keys (`unitId_m_0`).
     * Without this, applied rates are invisible to getNightPricingLines() and save falls back to defaultGrossRate.
     */
    function normalizePricingRateByLineKey() {
        const rates = pageCtx.pricingRateByLineKey || {};
        const slots = buildRawPricingSlotLines();
        if (!slots.length || !Object.keys(rates).length) {
            return;
        }

        const validKeys = new Set(slots.map((s) => s.lineKey));
        const next = {};

        Object.keys(rates).forEach((k) => {
            const val = Number(rates[k]);
            if (!Number.isFinite(val) || val <= 0) {
                return;
            }

            if (validKeys.has(k)) {
                const existing = Number(next[k]);
                if (!Number.isFinite(existing) || existing <= 0) {
                    next[k] = val;
                }
                return;
            }

            const m = /^(-?\d+)_(.+)$/.exec(k);
            if (!m) {
                return;
            }

            const uid = Number(m[1]);
            const tail = m[2];
            let targetKey = null;

            if (/^\d{4}-\d{2}-\d{2}$/.test(tail)) {
                targetKey = resolvePricingSlotLineKey(uid, parseDateOrNull(tail));
            } else {
                const mIdx = /^m_(\d+)$/.exec(tail);
                if (mIdx) {
                    const idx = Number(mIdx[1]);
                    const slot = slots.find(
                        (s) => Number(s.unitId) === uid && Number(s.periodIndex) === idx
                    );
                    targetKey = slot && slot.lineKey ? slot.lineKey : null;
                }
            }

            if (targetKey) {
                const existing = Number(next[targetKey]);
                if (!Number.isFinite(existing) || existing <= 0) {
                    next[targetKey] = val;
                }
            }
        });

        pageCtx.pricingRateByLineKey = next;
    }

    function writePricingRatesFromPopupRows(rows) {
        const next = { ...(pageCtx.pricingRateByLineKey || {}) };
        (rows || []).forEach((row) => {
            if (!row) {
                return;
            }

            const price = Number(row.unitPrice);
            if (!Number.isFinite(price) || price < 0) {
                return;
            }

            const uid = Number(row.unitId);
            const night = row.nightDate instanceof Date ? row.nightDate : parseDateOrNull(row.nightDate);
            const key =
                resolvePricingSlotLineKey(uid, night) ||
                (row.lineKey && buildRawPricingSlotLines().some((s) => s.lineKey === row.lineKey)
                    ? row.lineKey
                    : null);

            if (key) {
                next[key] = price;
            }
        });

        pageCtx.pricingRateByLineKey = next;
        normalizePricingRateByLineKey();
    }

    /**
     * Maps previous reservation unit rows to current ones by apartment id (handles draft → real unit_id).
     */
    function buildUnitIdRemapAfterSave(prevUnits, nextUnits) {
        const map = new Map();
        const prev = Array.isArray(prevUnits) ? prevUnits : [];
        const next = Array.isArray(nextUnits) ? nextUnits : [];

        prev.forEach((pu) => {
            const ou = Number(pu.unitId);
            if (!Number.isFinite(ou)) {
                return;
            }

            const aid = pu.apartmentId != null ? Number(pu.apartmentId) : NaN;
            const az = pu.apartmentZaaerId != null ? Number(pu.apartmentZaaerId) : NaN;
            const nu = next.find((x) => {
                const xa = x.apartmentId != null ? Number(x.apartmentId) : NaN;
                const xz = x.apartmentZaaerId != null ? Number(x.apartmentZaaerId) : NaN;
                if (Number.isFinite(aid) && aid > 0 && (xa === aid || xz === aid)) {
                    return true;
                }

                if (Number.isFinite(az) && az > 0 && (xz === az || xa === az)) {
                    return true;
                }

                return false;
            });

            if (nu && nu.unitId != null) {
                const nid = Number(nu.unitId);
                if (Number.isFinite(nid) && nid > 0 && nid !== ou) {
                    map.set(ou, nid);
                }
            }
        });

        return map;
    }

    /** After PATCH, re-key `pricingRateByLineKey` so line keys use persisted `unit_id` values. */
    function rekeyPricingRateByLineKeyAfterSave(prevUnits, nextUnits) {
        const rates = pageCtx.pricingRateByLineKey;
        if (!rates || typeof rates !== "object" || !Object.keys(rates).length) {
            return;
        }

        const remap = buildUnitIdRemapAfterSave(prevUnits, nextUnits);
        if (!remap.size) {
            return;
        }

        const nextRates = {};
        Object.keys(rates).forEach((key) => {
            const m = /^(-?\d+)_(.+)$/.exec(key);
            if (!m) {
                return;
            }

            const oldUid = Number(m[1]);
            const tail = m[2];
            const newUid = remap.has(oldUid) ? remap.get(oldUid) : oldUid;
            nextRates[`${newUid}_${tail}`] = rates[key];
        });

        pageCtx.pricingRateByLineKey = nextRates;
    }

    /** Payload for PUT unit-day-rates (call only after reservation is saved / has real unit ids). */
    function buildUnitDayRatesSavePayload() {
        const lines = trimPricingLinesToHotelNights(getNightPricingLines());
        const items = [];

        for (let i = 0; i < lines.length; i += 1) {
            const ln = lines[i] || {};
            const uid = Number(ln.unitId);
            if (!Number.isFinite(uid) || uid <= 0) {
                continue;
            }

            const nd = ln.nightDate instanceof Date ? ln.nightDate : parseDateOrNull(ln.nightDate);
            if (!nd || Number.isNaN(nd.getTime())) {
                continue;
            }

            items.push({
                rateId: ln.rateId,
                unitId: uid,
                nightDate: dateKeyLocal(nd),
                grossRate: Number(ln.unitPrice) || 0
            });
        }

        return { items };
    }

    function commitOpenUnitPricingGridToRateMap() {
        const grid = $("#unitPricingGridInner").dxDataGrid("instance");
        if (!grid) {
            return;
        }

        const rows = grid.option("dataSource") || [];
        if (!Array.isArray(rows) || !rows.length) {
            return;
        }

        writePricingRatesFromPopupRows(rows);
        pageCtx.useLocalFinancialTotals = true;
    }

    function prunePricingRateByLineKeyToCurrentLines() {
        const valid = new Set(buildRawPricingSlotLines().map((ln) => ln.lineKey));
        const rates = pageCtx.pricingRateByLineKey || {};
        const next = {};
        Object.keys(rates).forEach((k) => {
            if (valid.has(k)) {
                next[k] = rates[k];
            }
        });
        pageCtx.pricingRateByLineKey = next;
    }

    /**
     * Returns the nearest positive rate already stored for `unitId` in `rates`,
     * measured by calendar distance from `targetDate`. Used to inherit a rate for
     * newly added date slots (e.g. when checkout is extended by one day).
     */
    function findNearestRateForUnit(unitId, targetDate, allLines, rates) {
        const targetMs =
            targetDate instanceof Date ? targetDate.getTime() : new Date(targetDate).getTime();
        let best = 0;
        let bestDist = Infinity;
        allLines.forEach((ln) => {
            if (Number(ln.unitId) !== unitId) {
                return;
            }
            const r = Number(rates[ln.lineKey]);
            if (!Number.isFinite(r) || r <= 0) {
                return;
            }
            const d =
                ln.nightDate instanceof Date
                    ? ln.nightDate.getTime()
                    : new Date(ln.nightDate).getTime();
            const dist = Math.abs(d - targetMs);
            if (dist < bestDist) {
                bestDist = dist;
                best = r;
            }
        });
        return best;
    }

    function ensurePricingRatesForAllLines() {
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        if (!units.length) {
            return;
        }

        const rates = { ...(pageCtx.pricingRateByLineKey || {}) };
        const allLines = buildRawPricingSlotLines();

        allLines.forEach((ln) => {
            const cur = rates[ln.lineKey];
            const curNum = Number(cur);
            if (Number.isFinite(curNum) && curNum > 0) {
                return;
            }

            const periodGross = Number(ln.periodGrossRate);
            if (Number.isFinite(periodGross) && periodGross > 0) {
                rates[ln.lineKey] = periodGross;
                return;
            }

            const uid = Number(ln.unitId);

            // Prefer user-applied / sibling slot rates before room_type_rates default (avoids 2000 on new months).
            const inherited = findNearestRateForUnit(uid, ln.nightDate, allLines, rates);
            if (inherited > 0) {
                rates[ln.lineKey] = inherited;
                return;
            }

            const snap = pageCtx._unitRateSnapshotForDateSync;
            if (snap && snap[uid] > 0) {
                rates[ln.lineKey] = snap[uid];
                return;
            }

            const u = units.find((x) => Number(x.unitId) === uid);
            const def = u && u.defaultGrossRate != null ? Number(u.defaultGrossRate) : 0;
            if (def > 0) {
                rates[ln.lineKey] = def;
                return;
            }

            const persistedTotal = Number(u && u.totalAmount) || 0;
            const unitLineCount = allLines.filter((x) => Number(x.unitId) === uid).length;
            if (persistedTotal > 0 && unitLineCount > 0) {
                rates[ln.lineKey] = roundMoney(persistedTotal / unitLineCount);
            }
        });

        pageCtx.pricingRateByLineKey = rates;
    }

    function sumPersistedUnitBillableTotals() {
        return roundMoney(
            ((pageCtx.detail && pageCtx.detail.units) || []).reduce((sum, u) => {
                const status = normalizeUnitStatusKey(u && u.unitStatus);
                if (status === "cancelled" || status === "canceled" || status === "noshow") {
                    return sum;
                }

                return sum + (Number(u.totalAmount) || 0);
            }, 0)
        );
    }

    /** Seed pricing map from persisted reservation_units totals when day-rate rows are missing. */
    function seedPricingRatesFromPersistedUnitAmounts() {
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        const lines = buildRawPricingSlotLines();
        if (!units.length || !lines.length) {
            return false;
        }

        const rates = { ...(pageCtx.pricingRateByLineKey || {}) };
        const linesByUnit = new Map();
        lines.forEach((ln) => {
            const uid = Number(ln.unitId);
            if (!linesByUnit.has(uid)) {
                linesByUnit.set(uid, []);
            }

            linesByUnit.get(uid).push(ln);
        });

        let seeded = false;
        linesByUnit.forEach((unitLines, uid) => {
            const u = units.find((x) => Number(x.unitId) === uid);
            const stayTotal = Number(u && u.totalAmount) || 0;
            if (stayTotal <= 0 || unitLines.length <= 0) {
                return;
            }

            const grossPerLine = roundMoney(stayTotal / unitLines.length);
            if (grossPerLine <= 0) {
                return;
            }

            unitLines.forEach((ln) => {
                const cur = Number(rates[ln.lineKey]);
                if (Number.isFinite(cur) && cur > 0) {
                    return;
                }

                rates[ln.lineKey] = grossPerLine;
                seeded = true;
            });
        });

        if (!seeded) {
            return false;
        }

        pageCtx.pricingRateByLineKey = rates;
        return true;
    }

    /** When booking_engine (or legacy) rows have header totals but no day-rate lines, seed pricing map from saved financials. */
    function seedPricingRatesFromSavedFinancials(options) {
        const fin = (pageCtx.detail && pageCtx.detail.financial) || {};
        const savedTotal = roundMoney(fin.totalAmount);
        const savedSubtotal = roundMoney(fin.subtotal);
        if (savedTotal <= 0 && savedSubtotal <= 0) {
            return false;
        }

        const lines = buildRawPricingSlotLines();
        if (!lines.length) {
            return false;
        }

        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        const linesByUnit = new Map();
        lines.forEach((ln) => {
            const uid = Number(ln.unitId);
            if (!linesByUnit.has(uid)) {
                linesByUnit.set(uid, []);
            }

            linesByUnit.get(uid).push(ln);
        });

        const savedExtra = roundMoney(fin.totalExtra);
        const rentGrossTotal =
            savedTotal > 0
                ? roundMoney(Math.max(0, savedTotal - savedExtra))
                : roundMoney(savedSubtotal + (Number(fin.totalTaxAmount) || 0));

        const rates = {};
        let seeded = false;

        linesByUnit.forEach((unitLines, uid) => {
            const u = units.find((x) => Number(x.unitId) === uid);
            let grossPerLine = 0;

            const def = u && u.defaultGrossRate != null ? Number(u.defaultGrossRate) : 0;
            if (Number.isFinite(def) && def > 0) {
                grossPerLine = roundMoney(def);
            } else if (units.length === 1 && unitLines.length > 0) {
                grossPerLine = roundMoney(rentGrossTotal / unitLines.length);
            }

            if (grossPerLine <= 0) {
                return;
            }

            unitLines.forEach((ln) => {
                if (ln.lineKey) {
                    rates[ln.lineKey] = grossPerLine;
                    seeded = true;
                }
            });
        });

        if (!seeded) {
            return false;
        }

        pageCtx.pricingRateByLineKey = rates;
        if (!(options && options.keepServerFinancialTotals)) {
            pageCtx.useLocalFinancialTotals = true;
            syncFinancialUi({ skipFlash: true });
            const ug = $("#unitsGrid").dxDataGrid("instance");
            if (ug) {
                ug.refresh();
            }
        }
        return true;
    }

    /** Keep pricing rows aligned with current stay length (nights or months per unit). */
    function trimPricingLinesToHotelNights(lines) {
        const detail = pageCtx.detail;
        const units = (detail && detail.units) || [];
        if (!lines.length || !units.length) {
            return lines;
        }

        const monthly = isMonthlyRentalMode();
        const unitIdsInDetail = new Set(units.map((u) => Number(u.unitId)));
        const byUnit = new Map();
        const orphan = [];

        for (const ln of lines) {
            const uid = Number(ln.unitId);
            if (!unitIdsInDetail.has(uid)) {
                orphan.push(ln);
                continue;
            }

            if (!byUnit.has(uid)) {
                byUnit.set(uid, []);
            }

            byUnit.get(uid).push(ln);
        }

        const out = [];
        for (const u of units) {
            const uid = Number(u.unitId);
            const ci = new Date(u.checkInDate);
            const co = new Date(u.checkOutDate);
            let maxN;
            if (monthly) {
                maxN = Math.max(1, getReservationMonthCountForPricing());
            } else {
                maxN = hotelNightCount(ci, co);
            }

            if (!maxN || maxN <= 0) {
                continue;
            }

            let arr = byUnit.get(uid) || [];
            arr = arr.slice().sort((a, b) => {
                const pa = a.periodIndex;
                const pb = b.periodIndex;
                if (pa != null || pb != null) {
                    return (pa || 0) - (pb || 0);
                }

                return new Date(a.nightDate) - new Date(b.nightDate);
            });
            const take = Math.min(maxN, arr.length);
            for (let i = 0; i < take; i += 1) {
                out.push(arr[i]);
            }
        }

        return out.length ? out.concat(orphan) : lines;
    }

    /**
     * Split tax-inclusive gross (VAT + EWA) using same defaults as unit pricing popup when hotel summary is absent.
     */
    function splitTaxInclusiveGrossLine(gross) {
        const vatRate = 0.15;
        const ewaRate = 0.025;
        const amount = Number(gross) || 0;
        if (amount <= 0) {
            return { net: 0, ewa: 0, vat: 0, total: 0 };
        }

        const divisor = (1 + ewaRate) * (1 + vatRate);
        const net = divisor ? amount / divisor : amount;
        const ewa = net * ewaRate;
        const vat = amount - net - ewa;
        return { net, ewa, vat, total: amount };
    }

    function defaultHotelPricingTaxConfig() {
        return { vatRate: 15, ewaRate: 2.5, taxIncluded: true };
    }

    function normalizePricingTaxConfig(raw) {
        const d = defaultHotelPricingTaxConfig();
        if (!raw || typeof raw !== "object") {
            return d;
        }

        return {
            vatRate: Number(raw.vatRate) || d.vatRate,
            ewaRate: Number(raw.ewaRate) || d.ewaRate,
            taxIncluded: raw.taxIncluded !== false
        };
    }

    function getExtrasHotelTaxConfig() {
        const p = pageCtx.detail && pageCtx.detail.pricingTax;
        if (p && typeof p === "object" && (p.vatRate != null || p.ewaRate != null)) {
            return {
                vatRate: Number(p.vatRate) || 0,
                ewaRate: Number(p.ewaRate) || 0,
                vatIncluded: p.vatTaxIncluded !== false,
                ewaIncluded: p.lodgingTaxIncluded !== false
            };
        }

        const d = defaultHotelPricingTaxConfig();
        return {
            vatRate: d.vatRate,
            ewaRate: d.ewaRate,
            vatIncluded: d.taxIncluded !== false,
            ewaIncluded: d.taxIncluded !== false
        };
    }

    /**
     * Matches C# CalculatePricingAmounts (VAT + lodging/EWA, tax_included per tax row).
     */
    function calculatePmsPricingAmounts(extendedInput, cfg) {
        const vatRate = (Number(cfg.vatRate) || 0) / 100;
        const ewaRate = (Number(cfg.ewaRate) || 0) / 100;
        const gross = Math.round((Number(extendedInput) || 0) * 100) / 100;
        if (vatRate <= 0 && ewaRate <= 0) {
            return { net: gross, ewa: 0, vat: 0, total: gross };
        }

        const vatIncluded = cfg.vatIncluded !== false;
        const ewaIncluded = cfg.ewaIncluded !== false;
        if (ewaIncluded && vatIncluded) {
            const lr = ewaRate;
            const vr = vatRate;
            const divisor = 1 + lr + (1 + lr) * vr;
            if (divisor === 0) {
                return { net: gross, ewa: 0, vat: 0, total: gross };
            }

            const net = Math.round((gross / divisor) * 100) / 100;
            const ewa = Math.round(net * lr * 100) / 100;
            let vat = Math.round((net + ewa) * vr * 100) / 100;
            let total = Math.round((net + ewa + vat) * 100) / 100;
            const drift = Math.round((gross - total) * 100) / 100;
            if (drift !== 0) {
                vat = Math.round((vat + drift) * 100) / 100;
                total = gross;
            }

            return { net, ewa, vat, total };
        }

        const ewa = Math.round(gross * ewaRate * 100) / 100;
        const vat = Math.round((gross + ewa) * vatRate * 100) / 100;
        return {
            net: gross,
            ewa,
            vat,
            total: Math.round((gross + ewa + vat) * 100) / 100
        };
    }

    /**
     * Same VAT/EWA split as ReservationDetailService.CalculatePricingAmounts (popup day-rate summary or defaults).
     */
    function calculateUnitPricingTaxBreakdown(gross, taxConfig) {
        const taxCfg = normalizePricingTaxConfig(taxConfig);
        return calculatePmsPricingAmounts(gross, {
            vatRate: taxCfg.vatRate,
            ewaRate: taxCfg.ewaRate,
            vatIncluded: taxCfg.taxIncluded !== false,
            ewaIncluded: taxCfg.taxIncluded !== false
        });
    }

    function sumUnitGrossRentFromNightPricing(unitRow) {
        const uid = unitRow && unitRow.unitId != null ? Number(unitRow.unitId) : NaN;
        if (!Number.isFinite(uid) || uid === 0) {
            return 0;
        }

        const rates = pageCtx.pricingRateByLineKey || {};
        let sum = 0;
        buildRawPricingSlotLines().forEach((ln) => {
            if (Number(ln.unitId) !== uid) {
                return;
            }

            sum += Number(rates[ln.lineKey]) || 0;
        });

        if (sum <= 0 && unitRow && unitRow.defaultGrossRate != null) {
            const def = Number(unitRow.defaultGrossRate);
            if (Number.isFinite(def) && def > 0) {
                const lines = buildRawPricingSlotLines().filter((ln) => Number(ln.unitId) === uid);
                const n = Math.max(1, lines.length);
                sum = def * n;
            }
        }

        if (sum <= 0) {
            const persisted = Number(unitRow && unitRow.totalAmount) || 0;
            if (persisted > 0) {
                sum = persisted;
            }
        }

        return sum;
    }

    /**
     * Returns the last (most recent slot date) positive nightly rate stored for the unit.
     * Used in the units grid "قيمة الإيجار" column so the user sees the current per-night
     * rate at a glance rather than the full stay total.
     */
    function getUnitLastNightRate(unitRow) {
        const uid = unitRow && unitRow.unitId != null ? Number(unitRow.unitId) : NaN;
        if (!Number.isFinite(uid) || uid === 0) {
            return 0;
        }

        const rates = pageCtx.pricingRateByLineKey || {};
        const unitLines = buildRawPricingSlotLines().filter((ln) => Number(ln.unitId) === uid);
        if (!unitLines.length) {
            const def =
                unitRow && unitRow.defaultGrossRate != null ? Number(unitRow.defaultGrossRate) : 0;
            return Number.isFinite(def) && def > 0 ? def : 0;
        }

        // Sort descending by date/index so the most recent slot is first.
        unitLines.sort((a, b) => {
            const da = a.nightDate instanceof Date ? a.nightDate : new Date(a.nightDate);
            const db = b.nightDate instanceof Date ? b.nightDate : new Date(b.nightDate);
            return db - da;
        });

        for (const ln of unitLines) {
            const r = Number(rates[ln.lineKey]);
            if (Number.isFinite(r) && r > 0) {
                return r;
            }
        }

        const def =
            unitRow && unitRow.defaultGrossRate != null ? Number(unitRow.defaultGrossRate) : 0;
        if (Number.isFinite(def) && def > 0) {
            return def;
        }

        const stayTotal = Number(unitRow && unitRow.totalAmount) || 0;
        if (stayTotal > 0 && unitLines.length > 0) {
            return roundMoney(stayTotal / unitLines.length);
        }

        return 0;
    }

    function getReservationRouteIdForRates() {
        const rid =
            pageCtx.routeId ||
            (pageCtx.detail && pageCtx.detail.zaaerId) ||
            (pageCtx.detail && pageCtx.detail.reservationId) ||
            null;
        const n = Number(rid);
        return Number.isFinite(n) && n > 0 ? n : null;
    }

    function findReservationUnitForDayRateRow(rate) {
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        const rid = Number(rate.unitId);
        return (
            units.find(
                (u) =>
                    Number(u.unitId) === rid ||
                    Number(u.apartmentId) === rid ||
                    Number(u.apartmentZaaerId) === rid
            ) || {}
        );
    }

    /**
     * Maps API day-rate rows onto pricing slots (`unitId_m_0` monthly / `unitId_yyyy-MM-dd` daily).
     * Matches by unit + night date first, then by sorted period order (monthly DB dates may differ by a few days).
     */
    function applyServerDayRatesToPricingMap(rateRows) {
        if (!pageCtx.detail || !rateRows || !rateRows.length) {
            return;
        }

        const slotLines = trimPricingLinesToHotelNights(buildRawPricingSlotLines());
        if (!slotLines.length) {
            return;
        }

        const next = { ...(pageCtx.pricingRateByLineKey || {}) };
        const slotsByUnit = new Map();
        slotLines.forEach((slot) => {
            const uid = Number(slot.unitId);
            if (!slotsByUnit.has(uid)) {
                slotsByUnit.set(uid, []);
            }

            slotsByUnit.get(uid).push(slot);
        });

        slotsByUnit.forEach((arr) => {
            arr.sort((a, b) => {
                const pa = a.periodIndex != null ? a.periodIndex : 0;
                const pb = b.periodIndex != null ? b.periodIndex : 0;
                if (pa !== pb) {
                    return pa - pb;
                }

                return new Date(a.nightDate) - new Date(b.nightDate);
            });
        });

        const ratesByUnit = new Map();
        rateRows.forEach((rate) => {
            const u = findReservationUnitForDayRateRow(rate);
            const uid = Number(u.unitId || rate.unitId);
            if (!Number.isFinite(uid) || uid <= 0) {
                return;
            }

            const night = parseDateOrNull(rate.nightDate) || new Date(rate.nightDate);
            if (!night || Number.isNaN(night.getTime())) {
                return;
            }

            if (!ratesByUnit.has(uid)) {
                ratesByUnit.set(uid, []);
            }

            ratesByUnit.get(uid).push({ rate, night });
        });

        ratesByUnit.forEach((arr) => {
            arr.sort((a, b) => a.night - b.night);
        });

        slotsByUnit.forEach((slots, uid) => {
            const rateEntries = ratesByUnit.get(uid) || [];
            const used = new Set();

            slots.forEach((slot) => {
                if (!slot.lineKey) {
                    return;
                }

                const slotNight =
                    slot.nightDate instanceof Date ? slot.nightDate : parseDateOrNull(slot.nightDate);
                if (!slotNight || Number.isNaN(slotNight.getTime())) {
                    return;
                }

                const sk = dateKeyLocal(slotNight);
                const matchIdx = rateEntries.findIndex((re, i) => {
                    if (used.has(i)) {
                        return false;
                    }

                    return dateKeyLocal(re.night) === sk;
                });

                if (matchIdx >= 0) {
                    used.add(matchIdx);
                    next[slot.lineKey] = Number(rateEntries[matchIdx].rate.grossRate) || 0;
                }
            });

            slots.forEach((slot) => {
                if (!slot.lineKey) {
                    return;
                }

                const cur = Number(next[slot.lineKey]);
                if (Number.isFinite(cur) && cur > 0) {
                    return;
                }

                const unusedIdx = rateEntries.findIndex((_, i) => !used.has(i));
                if (unusedIdx >= 0) {
                    used.add(unusedIdx);
                    next[slot.lineKey] = Number(rateEntries[unusedIdx].rate.grossRate) || 0;
                }
            });
        });

        pageCtx.pricingRateByLineKey = next;
        normalizePricingRateByLineKey();
    }

    /**
     * Maps GET/PUT unit-day-rates rows into pageCtx.pricingRateByLineKey using the same slot lineKeys
     * as buildRawPricingSlotLines (daily + monthly), so the units grid rent column matches the pricing popup.
     */
    function mergeLoadedUnitDayRatesIntoPricingMap(loadedRates) {
        if (!pageCtx.detail || !loadedRates || !Array.isArray(loadedRates.items) || !loadedRates.items.length) {
            return;
        }

        applyServerDayRatesToPricingMap(loadedRates.items.slice());
    }

    async function hydratePricingFromServerDayRates(options) {
        const rid = getReservationRouteIdForRates();
        if (rid == null) {
            return;
        }

        const hotelId = pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId);
        let loadedRates = null;
        try {
            loadedRates = await window.Zaaer.ReservationDetailService.loadUnitDayRates(rid, null, hotelId);
        } catch {
            loadedRates = null;
        }

        if (!loadedRates || !loadedRates.items || !loadedRates.items.length) {
            seedPricingRatesFromPersistedUnitAmounts() ||
                seedPricingRatesFromSavedFinancials(options);
            ensurePricingRatesForAllLines();
            if (!(options && options.keepServerFinancialTotals)) {
                const local = computeLocalFinancialTotals();
                if (local.rentAndExtrasTotal > 0 || sumPersistedUnitBillableTotals() > 0) {
                    pageCtx.useLocalFinancialTotals = local.rentAndExtrasTotal > 0;
                    syncFinancialUi({ skipFlash: true });
                }
            }

            const ug = $("#unitsGrid").dxDataGrid("instance");
            if (ug) {
                ug.refresh();
            }
            return;
        }

        syncNonCheckedInUnitsWithReservationDates();
        prunePricingRateByLineKeyToCurrentLines();
        mergeLoadedUnitDayRatesIntoPricingMap(loadedRates);
        ensurePricingRatesForAllLines();
        if (!(options && options.keepServerFinancialTotals)) {
            const local = computeLocalFinancialTotals();
            const savedTotal = roundMoney(
                (pageCtx.detail && pageCtx.detail.financial && pageCtx.detail.financial.totalAmount) || 0
            );
            const canTrustLocal =
                localPricingCoversAllBillableUnits() &&
                (local.rentAndExtrasTotal > 0 || savedTotal <= 0) &&
                !(savedTotal > 0 && local.rentAndExtrasTotal + 0.009 < savedTotal && reservationHasClosedUnitLines());

            if (canTrustLocal) {
                pageCtx.useLocalFinancialTotals = true;
                syncFinancialUi({ skipFlash: true });
            }
        }

        const ug = $("#unitsGrid").dxDataGrid("instance");
        if (ug) {
            ug.refresh();
        }
    }

    function refreshUnitPricingPopupDataIfOpen() {
        if (!pageCtx._unitPricingPopupActive) {
            return;
        }

        const grid = $("#unitPricingGridInner").dxDataGrid("instance");
        if (!grid) {
            return;
        }

        const tc = pageCtx._unitPricingTaxConfig || defaultHotelPricingTaxConfig();
        const localLines = getNightPricingLines();
        const linesRaw = localLines.map((x) => {
            const calc = calculateUnitPricingTaxBreakdown(x.unitPrice, tc);
            return {
                ...x,
                rateUnitId: x.unitId,
                ewaAmount: calc.ewa,
                netAmount: calc.net,
                vatAmount: calc.vat
            };
        });
        const nextLines = trimPricingLinesToHotelNights(linesRaw);
        grid.option("dataSource", nextLines.map((x) => ({ ...x })));
        grid.refresh();

        const data = grid.option("dataSource") || [];
        let tot = 0;
        data.forEach((row) => {
            const calc = calculateUnitPricingTaxBreakdown(row.unitPrice, tc);
            row.netAmount = calc.net;
            row.ewaAmount = calc.ewa;
            row.vatAmount = calc.vat;
            tot += calc.total;
        });

        const $tot = $("#unitPricingTot");
        if ($tot.length) {
            $tot.text(formatMoneyEn(tot));
        }
        const $count = $("#unitPricingCount");
        if ($count.length) {
            $count.text(`(${data.length})`);
        }
    }

    function computeLocalFinancialTotals() {
        const lines = getNightPricingLines();
        let subNet = 0;
        let tax = 0;
        let total = 0;
        lines.forEach((ln) => {
            const g = Number(ln.unitPrice) || 0;
            const c = splitTaxInclusiveGrossLine(g);
            subNet += c.net;
            tax += c.ewa + c.vat;
            total += c.total;
        });

        let exNet = 0;
        let exTax = 0;
        let exTot = 0;
        (pageCtx.extras || []).forEach((row) => {
            const fd = {
                guestCount: row.guestCount,
                nightCount: row.nightCount,
                unitPrice: row.unitPrice,
                postingRule: row.postingRule,
                serviceDate: row.serviceDate,
                unitId: row.unitId,
                packageId: row.packageId,
                itemName: row.itemName
            };
            const { extended } = computeExtraExtendedFromFormData(fd);
            const c = calculatePmsPricingAmounts(extended, getExtrasHotelTaxConfig());
            exNet += c.net;
            exTax += c.ewa + c.vat;
            exTot += c.total;
        });

        const round2 = (x) => Math.round(x * 100) / 100;
        const extraCount = (pageCtx.extras || []).length;

        // Fold in server-side penalties and discounts (not stored in pricingRateByLineKey).
        const fin = (pageCtx.detail && pageCtx.detail.financial) || {};
        const penalties = round2(Number(fin.totalPenalties) || 0);
        const discounts = sumActiveDiscountsFromPage();
        const rentAndExtrasTotal = round2(total + exTot);

        return {
            subtotal: round2(subNet + exNet),
            tax: round2(tax + exTax),
            rentAndExtrasTotal,
            total: round2(rentAndExtrasTotal + penalties - discounts),
            penalties,
            discounts,
            lineCount: lines.length + (extraCount > 0 ? 1 : 0)
        };
    }

    function matchPickerRowForUnit(unitRow, pickerRows) {
        if (!unitRow || !Array.isArray(pickerRows)) {
            return null;
        }

        const aid = unitRow.apartmentId != null ? Number(unitRow.apartmentId) : NaN;
        const zid = unitRow.apartmentZaaerId != null ? Number(unitRow.apartmentZaaerId) : NaN;
        const uid = unitRow.unitId != null ? Number(unitRow.unitId) : NaN;

        return (
            pickerRows.find((r) => {
                const rid = r.apartmentId != null ? Number(r.apartmentId) : NaN;
                const rz = r.zaaerId != null ? Number(r.zaaerId) : NaN;
                if (Number.isFinite(aid) && aid > 0 && rid === aid) {
                    return true;
                }

                if (Number.isFinite(zid) && zid > 0 && (rz === zid || rid === zid)) {
                    return true;
                }

                if (Number.isFinite(uid) && uid > 0 && (rid === uid || rz === uid)) {
                    return true;
                }

                return false;
            }) || null
        );
    }

    /** Daily/monthly list gross from for-picker row (backed by room_type_rates on the server). */
    function suggestedGrossFromPickerRow(match, monthly) {
        if (!match) {
            return 0;
        }

        const dRaw = match.dailySuggestedGross != null ? Number(match.dailySuggestedGross) : NaN;
        const moRaw = match.monthlySuggestedGross != null ? Number(match.monthlySuggestedGross) : NaN;
        if (monthly && Number.isFinite(moRaw) && moRaw > 0) {
            return moRaw;
        }

        if (!monthly && Number.isFinite(dRaw) && dRaw > 0) {
            return dRaw;
        }

        if (Number.isFinite(dRaw) && dRaw > 0) {
            return dRaw;
        }

        if (Number.isFinite(moRaw) && moRaw > 0) {
            return moRaw;
        }

        return 0;
    }

    function loadPickerApartmentsCached(hotelId) {
        const hid = Number(hotelId);
        if (!Number.isFinite(hid) || hid <= 0) {
            return Promise.resolve([]);
        }

        const cached = pageCtx._pickerApartmentsCache;
        if (cached && cached.hotelId === hid && Array.isArray(cached.rows)) {
            return Promise.resolve(cached.rows);
        }

        return window.Zaaer.ReservationDetailService.loadApartmentsForPicker(hid)
            .then((rows) => {
                const list = Array.isArray(rows) ? rows : [];
                pageCtx._pickerApartmentsCache = { hotelId: hid, rows: list };
                return list;
            })
            .catch(() => []);
    }

    /**
     * For each unit on the reservation, set defaultGrossRate from room_type_rates via for-picker API
     * (daily_rate_min / monthly_rate_min), then refresh pricing lines and financial cards.
     */
    function applySuggestedGrossRatesFromPickerToUnits(options) {
        const opts = options || {};
        const detail = pageCtx.detail;
        if (!detail || !detail.hotelId || !Array.isArray(detail.units) || !detail.units.length) {
            return Promise.resolve(false);
        }

        const hotelId = Number(detail.hotelId);
        const monthly =
            opts.rentalMode != null ? normRental(opts.rentalMode) === "Monthly" : isMonthlyRentalMode();

        return loadPickerApartmentsCached(hotelId).then((rows) => {
            if (!rows.length) {
                return false;
            }

            let changed = false;
            detail.units.forEach((u) => {
                if (u.pickerCustomGrossRate != null) {
                    u.defaultGrossRate = roundMoney(Number(u.pickerCustomGrossRate));
                    changed = true;
                    return;
                }

                const match = matchPickerRowForUnit(u, rows);
                const gross = suggestedGrossFromPickerRow(match, monthly);
                if (gross > 0) {
                    u.defaultGrossRate = gross;
                    changed = true;
                }
            });

            if (!changed) {
                return false;
            }

            if (opts.refreshPricing !== false) {
                pageCtx.useLocalFinancialTotals = true;
                if (opts.clearPricingMap) {
                    pageCtx.pricingRateByLineKey = {};
                }
                onReservationStayDatesChanged();
            }

            const ug = $("#unitsGrid").dxDataGrid("instance");
            if (ug) {
                ug.refresh();
            }

            return true;
        });
    }

    /**
     * Fills suggested gross + financial preview from read-only apartments for-picker (no reservation row created).
     */
    function enrichNewReservationDetailWithSuggestedRates(detail) {
        if (!detail || !detail.hotelId || !Array.isArray(detail.units) || !detail.units.length) {
            return Promise.resolve();
        }

        const hotelId = Number(detail.hotelId);
        if (!Number.isFinite(hotelId) || hotelId <= 0) {
            return Promise.resolve();
        }

        return applySuggestedGrossRatesFromPickerToUnits({ refreshPricing: false })
            .then((ok) => {
                if (!ok) {
                    return;
                }

                pageCtx.pricingRateByLineKey = {};
                ensurePricingRatesForAllLines();
                pageCtx.useLocalFinancialTotals = true;
                const local = computeLocalFinancialTotals();
                detail.financial = {
                    ...((detail.financial && typeof detail.financial === "object" && detail.financial) || {}),
                    subtotal: local.subtotal,
                    totalTaxAmount: local.tax,
                    totalAmount: local.total,
                    amountPaid: 0,
                    balanceAmount: local.total
                };
            })
            .catch((err) => {
                console.warn("reservation-detail: for-picker pricing enrich failed", err);
            });
    }

    function renderFinancialPanel(d, snapshotOverride) {
        const $finHost = $("#resFinGrid");
        if ($finHost.length && !canViewFinancialSummary()) {
            $finHost.empty().hide();
            return;
        }

        if ($finHost.length) {
            $finHost.show();
        }

        const snapshot = snapshotOverride || computeReservationFinancialSnapshot();
        const subtotal = snapshot.subtotal;
        const tax = snapshot.tax;
        const total = snapshot.total;
        const paid = snapshot.paid;
        const balance = snapshot.balance;
        const penalties = snapshot.penalties;
        const discounts = snapshot.discounts;
        const balanceNum = Number(balance) || 0;
        const totalExtra = roundMoney(snapshot.totalExtra);

        function financialCard(label, value, extraClass, iconName, cardOptions) {
            const opts = cardOptions && typeof cardOptions === "object" ? cardOptions : null;
            const clickable = !!(opts && typeof opts.onClick === "function");
            const $label = $("<div>").addClass("res-fin-label").text(label);
            const $labelBlock =
                iconName && `${iconName}`.trim() !== ""
                    ? $("<div>")
                          .addClass("res-fin-label-wrap")
                          .append(
                              $("<span>")
                                  .addClass(`dx-icon dx-icon-${iconName}`)
                                  .attr("aria-hidden", "true"),
                              $label
                          )
                    : $label;

            const $row = $("<div>")
                .addClass("res-fin-card-row")
                .append(
                    $("<div>").addClass("res-fin-label-side").append($labelBlock),
                    $("<div>").addClass("res-fin-value").text(formatMoneyEn(value))
                );

            if (clickable) {
                $row.append(
                    $("<span>")
                        .addClass("res-fin-card__tap-icon dx-icon dx-icon-find")
                        .attr("aria-hidden", "true")
                );
            }

            const $card = $("<div>")
                .addClass("res-fin-card")
                .toggleClass(extraClass || "", !!extraClass)
                .append($row);

            if (clickable) {
                $card
                    .addClass("res-fin-card--clickable")
                    .attr({
                        role: "button",
                        tabindex: 0,
                        title: opts.hint || label
                    })
                    .on("click", (e) => {
                        e.preventDefault();
                        opts.onClick(e);
                    })
                    .on("keydown", (e) => {
                        if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            opts.onClick(e);
                        }
                    });
            }

            return $card;
        }

        $("#resFinGrid").empty().append(
            $("<div>").addClass("res-fin-summary-row res-fin-summary-row--inline").append(
                (() => {
                    const parts = [];
                    void subtotal;
                    void tax;
                    if (totalExtra > 0.0000001) {
                        parts.push(
                            financialCard(
                                t("reservationDetail.extras.financialExtras"),
                                totalExtra,
                                "res-fin-card--extras",
                                "gift",
                                {
                                    hint: t("reservationDetail.extras.popupOpenHint"),
                                    onClick: () => openReservationExtrasSummaryPopup()
                                }
                            )
                        );
                    }
                    if (penalties > 0.0000001) {
                        parts.push(
                            financialCard(t("reservationDetail.financial.penalties"), penalties, "res-fin-card--penalties", "warning")
                        );
                    }
                    if (discounts > 0.0000001) {
                        parts.push(
                            financialCard(t("reservationDetail.financial.discounts"), discounts, "res-fin-card--discounts", "percent")
                        );
                    }

                    parts.push(
                        financialCard(t("reservationDetail.total"), total, "res-fin-card--total"),
                        financialCard(t("reservationDetail.paid"), paid),
                        financialCard(
                            t("reservationDetail.balance"),
                            balance,
                            balanceNum > 0
                                ? "res-fin-card--balance-due"
                                : balanceNum < 0
                                  ? "res-fin-card--balance-credit"
                                  : "res-fin-card--balance-zero",
                            "money"
                        )
                    );
                    return parts;
                })()
            )
        );
    }

    function financialTabItems() {
        return [
            {
                id: "summary",
                text: t("reservationDetail.financial.tabs.summary"),
                icon: "chart"
            },
            {
                id: "pricing",
                text: t("reservationDetail.financial.tabs.pricing"),
                icon: "money"
            },
            {
                id: "payments",
                text: t("reservationDetail.financial.tabs.payments"),
                icon: "card"
            },
            {
                id: "invoices",
                text: t("reservationDetail.financial.tabs.invoices"),
                icon: "doc"
            },
            {
                id: "adjustments",
                text: t("reservationDetail.financial.tabs.adjustments"),
                icon: "percent"
            }
        ];
    }

    function renderFinancialTabTitle(item, _index, element) {
        const $el = $(element).empty();
        $("<div>")
            .addClass("res-fin-tab-title")
            .append(
                $("<span>")
                    .addClass(`dx-icon dx-icon-${item.icon || "folder"}`)
                    .attr("aria-hidden", "true"),
                $("<span>").addClass("res-fin-tab-title-text").text(item.text || "")
            )
            .appendTo($el);
    }

    function appendFinancialTabIntro($root, titleKey, textKey) {
        $("<div>")
            .addClass("res-fin-tab-intro")
            .append(
                $("<div>").addClass("res-fin-tab-intro-title").text(t(titleKey)),
                $("<div>").addClass("res-fin-tab-intro-text").text(t(textKey))
            )
            .appendTo($root);
    }

    function initUnitPricingButton() {
        if (pageCtx.isHallProperty) {
            syncHallUnitPricingButton();
            return;
        }

        const $btn = $("#btnUnitPricing");
        if (!$btn.length) {
            return;
        }

        let existing = null;
        try {
            existing = $btn.dxButton("instance");
        } catch {
            existing = null;
        }

        const opts = {
            text: t("reservationDetail.financial.openUnitPricing"),
            icon: "money",
            type: "default",
            stylingMode: "contained",
            visible: canViewUnitPricing(),
            onClick: openUnitPricingPopup
        };

        if (existing) {
            existing.option(opts);
            return;
        }

        $btn.dxButton(opts);
    }

    function renderFinancialSummaryTab($root) {
        $root.empty().addClass("res-fin-tab-pane res-fin-summary-pane");
        appendFinancialTabIntro(
            $root,
            "reservationDetail.financial.summaryTitle",
            "reservationDetail.financial.summaryIntro"
        );

        $("<div>")
            .attr("id", "resFinGrid")
            .addClass("res-fin-inline-summary res-fin-inline-summary--tab")
            .appendTo($root);

        $("<div>")
            .attr("id", "resDiscountsWrap")
            .addClass("res-discounts-wrap res-discounts-wrap--hidden")
            .appendTo($root);

        if (pageCtx.detail) {
            renderFinancialPanel(pageCtx.detail);
            renderDiscountsList();
            updateDiscountsSectionVisibility();
        }
    }

    function renderFinancialPricingTab($root) {
        $root.empty().addClass("res-fin-tab-pane res-fin-pricing-pane");
        appendFinancialTabIntro(
            $root,
            "reservationDetail.financial.pricingTabTitle",
            "reservationDetail.financial.pricingTabIntro"
        );

        const $action = $("<div>")
            .addClass("res-fin-tab-action-panel")
            .append(
                $("<div>")
                    .addClass("res-fin-tab-action-copy")
                    .append(
                        $("<div>")
                            .addClass("res-fin-tab-action-title")
                            .text(t("reservationDetail.financial.unitPricingTitle")),
                        $("<div>")
                            .addClass("res-fin-tab-action-text")
                            .text(t("reservationDetail.financial.pricingHint"))
                    ),
                $("<div>").attr("id", "btnUnitPricing").addClass("res-fin-tab-action-button")
            )
            .appendTo($root);

        const hasUnits = !!(pageCtx.detail && Array.isArray(pageCtx.detail.units) && pageCtx.detail.units.length);
        $("<div>")
            .addClass("res-fin-tab-note")
            .text(
                hasUnits
                    ? t("reservationDetail.financial.pricingReady")
                    : t("reservationDetail.financial.noUnitsForPricing")
            )
            .appendTo($action);

        initUnitPricingButton();
    }

    function renderFinancialComingSoonTab($root, titleKey, textKey, iconName) {
        $root.empty().addClass("res-fin-tab-pane res-fin-tab-pane--empty");
        $("<div>")
            .addClass("res-fin-tab-empty")
            .append(
                $("<span>")
                    .addClass(`dx-icon dx-icon-${iconName || "info"}`)
                    .attr("aria-hidden", "true"),
                $("<div>").addClass("res-fin-tab-empty-title").text(t(titleKey)),
                $("<div>").addClass("res-fin-tab-empty-text").text(t(textKey))
            )
            .appendTo($root);
    }

    function appendFinancialActionButton($root, textKey, iconName, onClick) {
        $("<div>")
            .addClass("res-fin-action-btn")
            .appendTo($root)
            .dxButton({
                text: t(textKey),
                icon: iconName,
                type: "default",
                stylingMode: "contained",
                onClick
            });
    }

    function renderFinancialAdjustmentsTab($root) {
        $root.empty().addClass("res-fin-tab-pane res-fin-adjustments-pane");
        appendFinancialTabIntro(
            $root,
            "reservationDetail.financial.adjustmentsTitle",
            "reservationDetail.financial.adjustmentsIntro"
        );

        const $actions = $("<div>").addClass("res-fin-actions-grid").appendTo($root);
        if (!reservationGridsActionsDisabled()) {
            appendFinancialActionButton($actions, "reservationDetail.actions.addDiscount", "money", () =>
                openReservationDiscountPopup(null)
            );
            appendFinancialActionButton($actions, "reservationDetail.actions.addPenalty", "warning", () =>
                openReservationActionPopup("penalty")
            );
        }

        $("<div>")
            .addClass("res-fin-tab-note")
            .text(t("reservationDetail.financial.adjustmentsNote"))
            .appendTo($root);
    }

    function renderFinancialTabContent(item, _index, element) {
        const tabId = item && item.id ? item.id : "summary";
        const $root = $(element);
        pageCtx.financialTabsLoaded = pageCtx.financialTabsLoaded || {};

        if (pageCtx.financialTabsLoaded[tabId]) {
            return;
        }

        pageCtx.financialTabsLoaded[tabId] = true;
        $root.empty();
        if (tabId === "summary") {
            renderFinancialSummaryTab($root);
            return;
        }

        if (tabId === "pricing") {
            renderFinancialPricingTab($root);
            return;
        }

        if (tabId === "payments") {
            renderFinancialComingSoonTab(
                $root,
                "reservationDetail.financial.paymentsTitle",
                "reservationDetail.financial.paymentsIntro",
                "card"
            );
            return;
        }

        if (tabId === "invoices") {
            renderFinancialComingSoonTab(
                $root,
                "reservationDetail.financial.invoicesTitle",
                "reservationDetail.financial.invoicesIntro",
                "doc"
            );
            return;
        }

        renderFinancialAdjustmentsTab($root);
    }

    function initFinancialTabPanel() {
        const $host = $("#resFinancialTabPanel");
        if (!$host.length) {
            return;
        }

        $host.dxTabPanel({
            items: financialTabItems(),
            keyExpr: "id",
            selectedIndex: 0,
            deferRendering: true,
            repaintChangesOnly: true,
            animationEnabled: false,
            scrollingEnabled: true,
            scrollByContent: true,
            showNavButtons: true,
            stylingMode: "secondary",
            iconPosition: "start",
            tabsPosition: "top",
            swipeEnabled: false,
            rtlEnabled: isArabic(),
            elementAttr: { class: "res-financial-tabs" },
            itemTitleTemplate: renderFinancialTabTitle,
            itemTemplate: renderFinancialTabContent,
            onSelectionChanged(e) {
                const item = e.addedItems && e.addedItems[0];
                if (item && item.id === "pricing") {
                    initUnitPricingButton();
                }
            }
        });
    }

    function flashFinancialSection() {
        const $sec = $("#res-section-financial");
        if (!$sec.length) {
            return;
        }

        $sec.removeClass("res-fin-sync-pulse");
        void $sec[0].offsetWidth;
        $sec.addClass("res-fin-sync-pulse");
        window.clearTimeout(flashFinancialSection._t);
        flashFinancialSection._t = window.setTimeout(() => {
            $sec.removeClass("res-fin-sync-pulse");
        }, 780);
    }

    function openUnitPricingPopupForUnit(unitRow) {
        if (!unitRow) {
            return;
        }

        if (isNewReservationForUnitPricing()) {
            if (!canSaveReservation()) {
                notifyForbidden();
                return;
            }
        } else if (!canViewUnitPricing() && !canEditUnitPricing()) {
            notifyForbidden();
            return;
        }

        const filterUnitId = unitRow.unitId != null ? Number(unitRow.unitId) : null;
        const unitLabel = unitGridNumberOnly(unitRow) || (filterUnitId != null ? String(filterUnitId) : "");
        openUnitPricingPopup({
            filterUnitId: Number.isFinite(filterUnitId) ? filterUnitId : null,
            unitLabel
        });
    }

    async function openUnitPricingPopup(options) {
        options = options || {};
        const filterUnitId =
            options.filterUnitId != null && Number.isFinite(Number(options.filterUnitId))
                ? Number(options.filterUnitId)
                : null;
        const filterUnitLabel = (options.unitLabel || "").toString().trim();

        if (!filterUnitId && !canViewUnitPricing()) {
            notifyForbidden();
            return;
        }

        const activeReservationId =
            pageCtx.routeId ||
            (pageCtx.detail && pageCtx.detail.zaaerId) ||
            (pageCtx.detail && pageCtx.detail.reservationId) ||
            null;
        const canPersistRates =
            activeReservationId !== null &&
            activeReservationId !== undefined &&
            `${activeReservationId}`.trim() !== "" &&
            `${activeReservationId}`.trim().toLowerCase() !== "null" &&
            Number.isFinite(Number(activeReservationId)) &&
            Number(activeReservationId) > 0;

        let loadedRates = null;
        if (canPersistRates) {
            try {
                loadedRates = await window.Zaaer.ReservationDetailService.loadUnitDayRates(
                    activeReservationId,
                    null,
                    pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId)
                );
            } catch {
                loadedRates = null;
            }
        }

        syncNonCheckedInUnitsWithReservationDates();
        prunePricingRateByLineKeyToCurrentLines();

        if (loadedRates && loadedRates.items && loadedRates.items.length) {
            mergeLoadedUnitDayRatesIntoPricingMap(loadedRates);
            const ugMerge = $("#unitsGrid").dxDataGrid("instance");
            if (ugMerge) {
                ugMerge.refresh();
            }
        }

        ensurePricingRatesForAllLines();

        const taxConfig = normalizePricingTaxConfig((loadedRates && loadedRates.summary) || null);
        pageCtx._unitPricingTaxConfig = taxConfig;

        function calculatePricingAmounts(gross) {
            return calculateUnitPricingTaxBreakdown(gross, taxConfig);
        }

        const linesRaw = getNightPricingLines().map((x) => {
            const calc = calculatePricingAmounts(x.unitPrice);
            return {
                ...x,
                rateUnitId: x.unitId,
                ewaAmount: calc.ewa,
                netAmount: calc.net,
                vatAmount: calc.vat
            };
        });

        let lines = trimPricingLinesToHotelNights(linesRaw);

        if (filterUnitId != null) {
            lines = lines.filter((ln) => Number(ln.unitId) === filterUnitId);
        }

        if (!lines.length) {
            DevExpress.ui.notify(
                filterUnitId != null
                    ? t("reservationDetail.financial.noUnitNightsForPricing")
                    : t("reservationDetail.financial.noUnitsForPricing"),
                "warning",
                2800
            );
            return;
        }

        const $host = $("<div>").appendTo("body");
        const roomTypes = [...new Set(lines.map((l) => l.roomTypeName).filter((x) => x && x !== "—"))];
        const typeItems = [{ id: "__ALL__", text: t("common.all") }].concat(
            roomTypes.map((name) => ({ id: name, text: name }))
        );

        const singleUnitMode = filterUnitId != null;
        const showBulkPricingToolbar = isNewReservationForUnitPricing() && filterUnitId == null;
        const showPricingSearch = !singleUnitMode && lines.length > 6;
        const rowH = 32;
        const gridHeaderH = 36;
        const gridChromeH = 8;
        const searchPanelH = showPricingSearch ? 36 : 0;
        const naturalGridH =
            gridHeaderH + searchPanelH + gridChromeH + Math.max(1, lines.length) * rowH;
        const footerReserveH = 92;
        const bulkReserveH = showBulkPricingToolbar ? 78 : 0;
        const popupContentPadH = 24;
        const popupTitleReserveH = 52;
        const popupMaxH = Math.floor(window.innerHeight * 0.88);
        const naturalPopupH =
            naturalGridH + footerReserveH + bulkReserveH + popupContentPadH + popupTitleReserveH;
        const unitPricingCompactPopup = naturalPopupH <= popupMaxH;

        let gridInstance = null;

        /** Keeps footer/actions visible; scrolling stays inside the DataGrid (DevExtreme popup pattern). */
        function fitUnitPricingLayout(popupInst) {
            if (!popupInst || !gridInstance) {
                return;
            }

            const $content = $(popupInst.content());
            const contentH = $content.innerHeight();
            if (contentH <= 0) {
                return;
            }

            let fixedH = 0;
            $content.find(".unit-pricing-bulk, .unit-pricing-footer").each(function () {
                fixedH += $(this).outerHeight(true) || 0;
            });

            const available = Math.max(80, contentH - fixedH - 8);
            const nextGridH = Math.min(naturalGridH, available);
            const currentGridH = gridInstance.option("height");

            if (unitPricingCompactPopup) {
                if (currentGridH !== "auto") {
                    gridInstance.option("height", "auto");
                }
                return;
            }

            if (typeof currentGridH !== "number" || Math.abs(currentGridH - nextGridH) > 2) {
                gridInstance.option("height", nextGridH);
            }
        }

        /**
         * Builds a preview financial object from the current popup grid rows.
         * Includes night-rate totals + unsaved extras + penalties/discounts from the saved financial.
         * Does NOT mutate any pageCtx state.
         */
        function computePreviewFinancialFromData(data) {
            const round2 = (x) => Math.round((Number(x) || 0) * 100) / 100;
            let subNet = 0;
            let ewa = 0;
            let vat = 0;
            let nightTotal = 0;
            data.forEach((row) => {
                const calc = calculatePricingAmounts(row.unitPrice);
                subNet += calc.net;
                ewa += calc.ewa;
                vat += calc.vat;
                nightTotal += calc.total;
            });

            const extraTotal = (pageCtx.extras || []).reduce((s, r) => s + (Number(r.totalAmount) || 0), 0);
            const fin = (pageCtx.detail && pageCtx.detail.financial) || {};
            const penalties = Number(fin.totalPenalties) || 0;
            const discounts = sumActiveDiscountsFromPage();
            const paid = Number(fin.amountPaid) || 0;

            const grandTotal = round2(nightTotal + extraTotal + penalties);
            return {
                ...fin,
                subtotal: round2(subNet),
                totalTaxAmount: round2(ewa + vat),
                totalExtra: round2(extraTotal),
                totalAmount: grandTotal,
                balanceAmount: round2(grandTotal - discounts - paid),
                _isPreview: true
            };
        }

        function syncSummaryFooter() {
            if (!gridInstance) {
                return;
            }

            const data = gridInstance.option("dataSource") || [];
            let tot = 0;

            data.forEach((row) => {
                const calc = calculatePricingAmounts(row.unitPrice);
                row.netAmount = calc.net;
                row.ewaAmount = calc.ewa;
                row.vatAmount = calc.vat;
                tot += calc.total;
            });

            $("#unitPricingTot").text(formatMoneyEn(tot));
            $("#unitPricingCount").text(`(${data.length})`);

            // Live-preview main-page summary cards while the popup is open.
            if (pageCtx.detail) {
                const previewFin = computePreviewFinancialFromData(data);
                renderFinancialPanel(pageCtx.detail, {
                    subtotal: roundMoney(previewFin.subtotal),
                    tax: roundMoney(previewFin.totalTaxAmount),
                    totalExtra: roundMoney(previewFin.totalExtra),
                    penalties: roundMoney(previewFin.totalPenalties),
                    discounts: sumActiveDiscountsFromPage(),
                    total: roundMoney(previewFin.totalAmount),
                    paid: roundMoney(previewFin.amountPaid),
                    balance: roundMoney(previewFin.balanceAmount)
                });
                $("#resFinGrid").addClass("res-fin-grid--preview");
            }
        }

        function applyFromPopup() {
            if (!canApplyUnitPricingInContext()) {
                notifyForbidden();
                return;
            }

            const data = gridInstance.option("dataSource") || [];
            const belowMin = validateUnitPricingApplyRows(data);
            if (!belowMin.ok) {
                notifyPricingBelowMinimumDenied(belowMin.minGross);
                return;
            }

            // Always apply pricing in memory only; DB is updated only on «حفظ الحجز» (saveReservation).
            writePricingRatesFromPopupRows(data);

            // Commit preview financial (includes extras + penalties/discounts) to page state.
            pageCtx.detail.financial = computePreviewFinancialFromData(data);
            delete pageCtx.detail.financial._isPreview;
            pageCtx._prePricingFinancialSnapshot = null;

            pageCtx.useLocalFinancialTotals = true;
            $("#resFinGrid").removeClass("res-fin-grid--preview");
            syncFinancialUi();
            const ug = $("#unitsGrid").dxDataGrid("instance");
            if (ug) {
                ug.refresh();
            }
            $host.dxPopup("instance").hide();
            DevExpress.ui.notify(t("reservationDetail.financial.pricingAppliedLocal"), "success", 2400);
        }

        const popupTitle =
            filterUnitId != null
                ? t("reservationDetail.financial.unitPricingTitleForUnit").replace(
                      "{unit}",
                      filterUnitLabel || lines[0].unitNumber || String(filterUnitId)
                  )
                : t("reservationDetail.financial.unitPricingTitle");

        $host.dxPopup({
            title: popupTitle,
            width: singleUnitMode
                ? Math.min(700, Math.max(360, window.innerWidth - 24))
                : Math.min(920, Math.max(360, window.innerWidth - 24)),
            height: unitPricingCompactPopup ? "auto" : popupMaxH,
            maxHeight: popupMaxH,
            visible: true,
            showCloseButton: true,
            showTitle: true,
            hideOnOutsideClick: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: {
                class: [
                    "unit-pricing-popup",
                    "unit-pricing-popup--compact",
                    singleUnitMode ? "unit-pricing-popup--single-unit" : "",
                    unitPricingCompactPopup ? "unit-pricing-popup--fits" : ""
                ]
                    .filter(Boolean)
                    .join(" ")
            },
            onShowing(e) {
                pageCtx._unitPricingPopupActive = true;
                // Save a snapshot so we can restore the main-page totals if the user closes without applying.
                pageCtx._prePricingFinancialSnapshot =
                    pageCtx.detail && pageCtx.detail.financial
                        ? { ...pageCtx.detail.financial }
                        : null;
                const $root = $(e.component.content()).empty().addClass("unit-pricing-layout");

                if (showBulkPricingToolbar) {
                    const $bulk = $("<div>").addClass("unit-pricing-bulk").appendTo($root);
                    $("<div>").attr("id", "upBulkRoomType").appendTo($bulk);
                    $("<div>").attr("id", "upBulkAmount").appendTo($bulk);
                }

                const $gridScroll = $("<div>")
                    .addClass("unit-pricing-grid-scroll")
                    .appendTo($root);
                $("<div>").attr("id", "unitPricingGridInner").appendTo($gridScroll);

                const $foot = $("<div>").addClass("unit-pricing-footer").appendTo($root);

                const $sumBar = $("<div>").addClass("unit-pricing-summary-bar").appendTo($foot);
                const $sumCard = $("<div>")
                    .addClass("res-payments-summary-card unit-pricing-total-card")
                    .appendTo($sumBar);
                const $sumTitle = $("<div>").addClass("res-payments-summary-card-k").appendTo($sumCard);
                $("<span>")
                    .addClass("res-payments-summary-card-label")
                    .text(t("reservationDetail.total"))
                    .appendTo($sumTitle);
                $("<span>")
                    .attr("id", "unitPricingCount")
                    .addClass("res-payments-summary-card-count")
                    .text(`(${lines.length})`)
                    .appendTo($sumTitle);
                const $sumValue = $("<div>").addClass("res-payments-summary-card-v").appendTo($sumCard);
                $("<span>")
                    .attr("id", "unitPricingTot")
                    .addClass("res-payments-summary-amt")
                    .text(formatMoneyEn(0))
                    .appendTo($sumValue);
                appendPaymentsSummaryCurrency($sumValue);

                const $actions = $("<div>").addClass("unit-pricing-footer-actions").appendTo($foot);
                $("<div>").appendTo($actions).dxButton({
                    text: t("common.close"),
                    stylingMode: "text",
                    onClick() {
                        e.component.hide();
                    }
                });
                if (canApplyUnitPricingFromPopup()) {
                    $("<div>").appendTo($actions).dxButton({
                        text: t("reservationDetail.financial.applyPricing"),
                        icon: "check",
                        type: "default",
                        stylingMode: "contained",
                        onClick: applyFromPopup
                    });
                }

                /** Applies the bulk amount to all grid rows that match the current room-type filter. */
                function applyBulkFromToolbar() {
                    if (!canBulkEditUnitPricing()) {
                        return;
                    }

                    if (!gridInstance) {
                        return;
                    }

                    const sb = $("#upBulkRoomType").dxSelectBox("instance");
                    const nb = $("#upBulkAmount").dxNumberBox("instance");
                    if (!sb || !nb) {
                        return;
                    }

                    const filter = sb.option("value");
                    const amt = nb.option("value");
                    // Require a positive value — never let a zero/null silently wipe prices.
                    if (amt === undefined || amt === null) {
                        return;
                    }
                    const price = Number(amt);
                    if (Number.isNaN(price) || price <= 0) {
                        return;
                    }

                    const data = (gridInstance.option("dataSource") || []).map((row) => ({ ...row }));
                    data.forEach((row) => {
                        if (filter === "__ALL__" || row.roomTypeName === filter) {
                            row.unitPrice = price;
                        }
                    });
                    gridInstance.option("dataSource", data);
                    // Re-apply the visual filter so the grid still shows only the selected type.
                    applyRoomTypeGridFilter(filter);
                    gridInstance.refresh();
                    syncSummaryFooter();
                }

                function selectNumberBoxInput(componentOrElement) {
                    const $el =
                        componentOrElement && componentOrElement.element
                            ? componentOrElement.element()
                            : $(componentOrElement);
                    const input = $el && $el.find ? $el.find("input").get(0) : null;
                    if (input) {
                        window.setTimeout(() => {
                            try {
                                input.focus();
                                input.select();
                            } catch {
                                /* best effort */
                            }
                        }, 0);
                    }
                }

                function attachNumberBoxSelectAll(componentOrElement) {
                    const $el =
                        componentOrElement && componentOrElement.element
                            ? componentOrElement.element()
                            : $(componentOrElement);
                    if (!$el || !$el.length) {
                        return;
                    }

                    $el.off(".unitPriceSelectAll");
                    $el.on(
                        "focusin.unitPriceSelectAll mousedown.unitPriceSelectAll click.unitPriceSelectAll",
                        "input",
                        function () {
                            const input = this;
                            window.setTimeout(() => {
                                try {
                                    input.select();
                                } catch {
                                    /* best effort */
                                }
                            }, 0);
                        }
                    );
                }

                function updatePricingComputedCell(rowKey, fieldName, value) {
                    if (!gridInstance || !rowKey) {
                        return;
                    }

                    const rowIndex = gridInstance.getRowIndexByKey(rowKey);
                    const visibleIndex = gridInstance.columnOption(fieldName, "visibleIndex");
                    if (rowIndex < 0 || visibleIndex == null || visibleIndex < 0) {
                        return;
                    }

                    const $row = $(gridInstance.getRowElement(rowIndex));
                    const $cell = $row.children("td").eq(visibleIndex);
                    if (!$cell.length) {
                        return;
                    }

                    $cell.text(formatMoneyEn(value));
                }

                function updatePricingRowLive(rowKey, value) {
                    if (!gridInstance || !rowKey) {
                        return;
                    }

                    const price = Number(value);
                    const nextPrice = Number.isFinite(price) && price >= 0 ? price : 0;
                    const data = gridInstance.option("dataSource") || [];
                    const row = data.find((x) => x && x.lineKey === rowKey);
                    if (!row) {
                        return;
                    }

                    row.unitPrice = nextPrice;
                    const calc = calculatePricingAmounts(nextPrice);
                    row.netAmount = calc.net;
                    row.ewaAmount = calc.ewa;
                    row.vatAmount = calc.vat;

                    updatePricingComputedCell(rowKey, "netAmount", calc.net);
                    updatePricingComputedCell(rowKey, "ewaAmount", calc.ewa);
                    updatePricingComputedCell(rowKey, "vatAmount", calc.vat);
                    syncSummaryFooter();
                }

                /** Filters the pricing grid display by room type without touching the data. */
                function applyRoomTypeGridFilter(roomType) {
                    if (!gridInstance) {
                        return;
                    }
                    if (!roomType || roomType === "__ALL__") {
                        gridInstance.clearFilter("dataSource");
                    } else {
                        gridInstance.filter(["roomTypeName", "=", roomType]);
                    }
                }

                if (showBulkPricingToolbar) {
                    $("#upBulkRoomType").dxSelectBox({
                        dataSource: typeItems,
                        valueExpr: "id",
                        displayExpr: "text",
                        value: "__ALL__",
                        onValueChanged(ev) {
                            // Filter the grid visually; do NOT apply prices — that only happens
                            // when the user explicitly enters an amount in the number box.
                            applyRoomTypeGridFilter(ev.value);
                        }
                    });

                    const bulkRateEditable = canBulkEditUnitPricing();
                    const $bulk = $(".unit-pricing-bulk", $root);
                    if (!bulkRateEditable && $bulk.length) {
                        $bulk.addClass("unit-pricing-bulk--readonly");
                    }

                    $("#upBulkAmount").dxNumberBox({
                        min: 0,
                        format: "#,##0.00",
                        showSpinButtons: bulkRateEditable,
                        readOnly: !bulkRateEditable,
                        disabled: !bulkRateEditable,
                        valueChangeEvent: "keyup input change",
                        placeholder: t("reservationDetail.financial.bulkRateLabel"),
                        onInitialized(e) {
                            attachNumberBoxSelectAll(e.component);
                        },
                        onFocusIn(e) {
                            selectNumberBoxInput(e.component);
                        },
                        onInput() {
                            applyBulkFromToolbar();
                        },
                        onValueChanged() {
                            applyBulkFromToolbar();
                        }
                    });
                }

                const ds = lines.map((x) => ({ ...x }));
                const pricingGridEditable = canApplyUnitPricingInContext();
                const po = window.Zaaer.PmsGridOptions;
                $("#unitPricingGridInner").dxDataGrid(
                    po.merge(po.baseline(), {
                    dataSource: ds,
                    keyExpr: "lineKey",
                    height: unitPricingCompactPopup ? "auto" : naturalGridH,
                    columnAutoWidth: false,
                    wordWrapEnabled: false,
                    rowAlternationEnabled: false,
                    hoverStateEnabled: true,
                    headerFilter: { visible: false },
                    searchPanel: { visible: showPricingSearch, width: 220 },
                    paging: { enabled: false },
                    pager: { visible: false },
                    elementAttr: { class: "pms-grid-compact unit-pricing-grid" },
                    scrolling: po.scrollingOptions({
                        mode: "standard",
                        useNative: false,
                        showScrollbar: unitPricingCompactPopup ? "onHover" : "always"
                    }),
                    editing: {
                        mode: "cell",
                        allowUpdating: pricingGridEditable
                    },
                    onEditingStart(e) {
                        if (!canEditPriceForPricingRow(e.data)) {
                            e.cancel = true;
                        }
                    },
                    columns: [
                        {
                            dataField: "unitNumber",
                            caption: t("reservationDetail.units.unit"),
                            allowEditing: false,
                            width: 54,
                            minWidth: 50,
                            visible: !singleUnitMode,
                            showInColumnChooser: false
                        },
                        {
                            dataField: "roomTypeName",
                            caption: t("reservationDetail.units.roomType"),
                            allowEditing: false,
                            width: singleUnitMode ? 200 : 176,
                            minWidth: 120,
                            cssClass: "res-pricing-col-room-type",
                            cellTemplate(container, cell) {
                                const text =
                                    cell.value && `${cell.value}`.trim() ? `${cell.value}`.trim() : "—";
                                $("<span>")
                                    .addClass("res-pricing-room-type-text")
                                    .attr("title", text)
                                    .text(text)
                                    .appendTo(container);
                            }
                        },
                        {
                            dataField: "nightDate",
                            caption: t("reservationDetail.financial.nightDate"),
                            dataType: "date",
                            format: "dd/MM/yyyy",
                            allowEditing: false,
                            width: 96,
                            minWidth: 88,
                            alignment: "center",
                            cssClass: "res-pricing-col-date"
                        },
                        {
                            dataField: "unitPrice",
                            caption: t("reservationDetail.financial.nightRate"),
                            dataType: "number",
                            format: "#,##0.00",
                            width: 102,
                            minWidth: 96,
                            alignment: "center",
                            cssClass: "res-pricing-col-night-rate",
                            editorOptions: {
                                min: 0,
                                format: "#,##0.00",
                                onInitialized(e) {
                                    attachNumberBoxSelectAll(e.component);
                                },
                                onFocusIn(e) {
                                    selectNumberBoxInput(e.component);
                                }
                            },
                            cellTemplate(container, cell) {
                                const price = Number(cell.value);
                                const text = Number.isFinite(price) ? formatMoneyEn(price) : "—";
                                $("<span>")
                                    .addClass("res-pricing-night-rate-val")
                                    .text(text)
                                    .appendTo(container);
                            }
                        },
                        {
                            dataField: "netAmount",
                            caption: t("reservationDetail.subtotal"),
                            dataType: "number",
                            format: "#,##0.00",
                            allowEditing: false,
                            visible: false,
                            showInColumnChooser: false,
                            calculateCellValue: (r) => calculatePricingAmounts(r.unitPrice).net
                        },
                        {
                            dataField: "ewaAmount",
                            caption: t("reservationDetail.financial.ewa"),
                            dataType: "number",
                            format: "#,##0.00",
                            allowEditing: false,
                            visible: false,
                            showInColumnChooser: false,
                            calculateCellValue: (r) => calculatePricingAmounts(r.unitPrice).ewa
                        },
                        {
                            dataField: "vatAmount",
                            caption: t("reservationDetail.tax"),
                            dataType: "number",
                            format: "#,##0.00",
                            allowEditing: false,
                            visible: false,
                            showInColumnChooser: false,
                            calculateCellValue: (r) => calculatePricingAmounts(r.unitPrice).vat
                        },
                        {
                            caption: t("reservationDetail.financial.lineAmount"),
                            calculateCellValue: (r) => calculatePricingAmounts(r.unitPrice).total,
                            format: "#,##0.00",
                            allowEditing: false,
                            visible: false
                        },
                        {
                            type: "buttons",
                            width: 44,
                            minWidth: 40,
                            caption: "",
                            alignment: "center",
                            cssClass: "res-pricing-col-actions",
                            buttons: [
                                {
                                    hint: t("reservationDetail.financial.deleteNightRow"),
                                    icon: "trash",
                                    cssClass: "res-pricing-row-delete",
                                    visible(e) {
                                        return canEditPriceForPricingRow(e.row && e.row.data);
                                    },
                                    onClick(e) {
                                        if (!canEditPriceForPricingRow(e.row && e.row.data)) {
                                            notifyForbidden();
                                            return;
                                        }

                                        const g = $("#unitPricingGridInner").dxDataGrid("instance");
                                        if (!g) {
                                            return;
                                        }

                                        const data = g.option("dataSource") || [];
                                        if (data.length <= 1) {
                                            DevExpress.ui.notify(
                                                t("reservationDetail.financial.cannotDeleteLastNight"),
                                                "warning",
                                                3200
                                            );
                                            return;
                                        }

                                        const rowKey = e.row && e.row.data && e.row.data.lineKey;
                                        DevExpress.ui.dialog
                                            .confirm(
                                                t("reservationDetail.financial.confirmDeleteNightRow"),
                                                t("reservationDetail.actions.delete")
                                            )
                                            .done((yes) => {
                                                if (!yes) {
                                                    return;
                                                }

                                                const next = data.filter((r) => r.lineKey !== rowKey);
                                                g.option("dataSource", next);
                                                g.refresh();
                                                syncSummaryFooter();
                                            });
                                    }
                                }
                            ]
                        }
                    ],
                    onEditorPreparing(e) {
                        if (e.parentType !== "dataRow" || e.dataField !== "unitPrice") {
                            return;
                        }

                        e.editorOptions.valueChangeEvent = "keyup input change";
                        const prevInitialized = e.editorOptions.onInitialized;
                        e.editorOptions.onInitialized = function (args) {
                            if (typeof prevInitialized === "function") {
                                prevInitialized.apply(this, arguments);
                            }
                            attachNumberBoxSelectAll(args.component);
                            selectNumberBoxInput(args.component);
                        };
                        e.editorOptions.onValueChanged = function (args) {
                            updatePricingRowLive(
                                e.row && e.row.data && e.row.data.lineKey,
                                args.value
                            );
                        };
                    },
                    onEditorPrepared(e) {
                        if (e.parentType !== "dataRow" || e.dataField !== "unitPrice") {
                            return;
                        }

                        attachNumberBoxSelectAll(e.editorElement);
                        selectNumberBoxInput(e.editorElement);
                    },
                    onCellValueChanged() {
                        syncSummaryFooter();
                    },
                    onSaved() {
                        syncSummaryFooter();
                    }
                    })
                );

                gridInstance = $("#unitPricingGridInner").dxDataGrid("instance");
                syncSummaryFooter();
                fitUnitPricingLayout(e.component);
                requestAnimationFrame(() => fitUnitPricingLayout(e.component));
            },
            onResize(e) {
                fitUnitPricingLayout(e.component);
            },
            onHidden() {
                pageCtx._unitPricingPopupActive = false;
                pageCtx._unitPricingTaxConfig = null;
                // If user closed without applying, restore the pre-popup financials.
                if (pageCtx._prePricingFinancialSnapshot !== null && pageCtx._prePricingFinancialSnapshot !== undefined) {
                    if (pageCtx.detail) {
                        pageCtx.detail.financial = pageCtx._prePricingFinancialSnapshot;
                    }
                    pageCtx._prePricingFinancialSnapshot = null;
                    $("#resFinGrid").removeClass("res-fin-grid--preview");
                    syncFinancialUi();
                }
                $host.remove();
            }
        });
    }

    function normPickerToken(s) {
        return `${s || ""}`.trim().toLowerCase();
    }

    function isDirtyHousekeeping(hk) {
        const x = normPickerToken(hk);
        if (!x) {
            return false;
        }
        if (x.includes("dirty")) {
            return true;
        }
        return x.includes("غير") && x.includes("نظيف");
    }

    /** Active maintenance row in maintenances (unit_id = zaaer_id / apartment_id), from for-picker API. */
    function isApartmentMaintenanceActive(apt) {
        if (!apt || typeof apt !== "object") {
            return false;
        }

        return apt.maintenanceActive === true || apt.MaintenanceActive === true;
    }

    /** Occupancy status eligible for «add unit» picker (vacant / available only). */
    function isVacantPickerStatus(status) {
        const x = normPickerToken(status);
        if (!x) {
            return false;
        }

        return (
            x === "vacant" ||
            x.includes("vacant") ||
            x.includes("available") ||
            x.includes("avail") ||
            x.includes("شاغر") ||
            x.includes("خالي") ||
            x.includes("free")
        );
    }

    /**
     * Dirty housekeeping → yellow border; else rented-like → red; else vacant-like → green.
     */
    function pickerCardBorderClass(status, hk) {
        if (isDirtyHousekeeping(hk)) {
            return "unit-picker-card--dirty";
        }

        const x = normPickerToken(status);
        if (x.includes("reserved") || x.includes("حجز")) {
            return "unit-picker-card--reserved";
        }

        if (x.includes("rent") || x.includes("occup") || x.includes("مشغول")) {
            return "unit-picker-card--rented";
        }

        if (
            x.includes("vacant") ||
            x.includes("available") ||
            x.includes("avail") ||
            x.includes("شاغر") ||
            x.includes("خالي") ||
            x.includes("free")
        ) {
            return "unit-picker-card--vacant";
        }

        return "unit-picker-card--vacant";
    }

    function pickerOccupancyBadgeClass(status) {
        const x = normPickerToken(status);
        if (x.includes("reserved") || x.includes("حجز")) {
            return "unit-picker-badge--reserved";
        }

        if (x.includes("rent") || x.includes("occup") || x.includes("مشغول")) {
            return "unit-picker-badge--rented";
        }

        return "unit-picker-badge--vacant";
    }

    function translatePickerOccupancy(status) {
        const raw = `${status || ""}`.trim();
        if (!raw) {
            return "—";
        }

        const x = normPickerToken(raw);
        if (
            x.includes("vacant") ||
            x.includes("available") ||
            x.includes("avail") ||
            x.includes("شاغر") ||
            x.includes("خالي")
        ) {
            return t("reservationDetail.units.statusVacant");
        }

        if (
            x.includes("rent") ||
            x.includes("occup") ||
            x.includes("مشغول")
        ) {
            return t("reservationDetail.units.statusRented");
        }

        if (x.includes("reserved") || x.includes("حجز")) {
            return t("reservationDetail.units.statusReserved");
        }

        if (x.includes("maintenance") || x.includes("maint") || x.includes("صيان")) {
            return t("reservationDetail.units.statusMaintenance");
        }

        return raw;
    }

    function translatePickerHousekeeping(hk) {
        const raw = `${hk || ""}`.trim();
        if (!raw) {
            return "";
        }

        return translatePickerFilterLabel(raw, "hk");
    }

    /** Occupancy + housekeeping labels for unit-transfer dropdown rows. */
    function buildUnitTransferPickerStatusParts(apt) {
        if (!apt || typeof apt !== "object") {
            return { occText: "—", occBadgeClass: "unit-transfer-status-badge--neutral", hkText: null, hkBadgeClass: null };
        }

        if (isApartmentMaintenanceActive(apt)) {
            return {
                occText: t("reservationDetail.units.statusMaintenance"),
                occBadgeClass: "unit-transfer-status-badge--maintenance",
                hkText: null,
                hkBadgeClass: null
            };
        }

        const occ = translatePickerOccupancy(apt.status);
        const hk = translatePickerHousekeeping(apt.housekeepingStatus);
        return {
            occText: occ && occ !== "—" ? occ : null,
            occBadgeClass: pickerTransferOccBadgeClass(apt.status || ""),
            hkText: hk || null,
            hkBadgeClass: hk ? pickerTransferHkBadgeClass(apt.housekeepingStatus) : null
        };
    }

    function pickerTransferOccBadgeClass(status) {
        const x = normPickerToken(status);
        if (x.includes("reserved") || x.includes("حجز")) {
            return "unit-transfer-status-badge--reserved";
        }

        if (x.includes("rent") || x.includes("occup") || x.includes("مشغول")) {
            return "unit-transfer-status-badge--rented";
        }

        return "unit-transfer-status-badge--vacant";
    }

    function pickerTransferHkBadgeClass(hkStatus) {
        const x = normPickerToken(hkStatus);
        if (x.includes("dirty")) {
            return "unit-transfer-status-badge--dirty";
        }

        if (x.includes("clean") && !x.includes("dirty")) {
            return "unit-transfer-status-badge--clean";
        }

        if (x.includes("inspect")) {
            return "unit-transfer-status-badge--inspected";
        }

        if (x.includes("cleaning")) {
            return "unit-transfer-status-badge--cleaning";
        }

        return "unit-transfer-status-badge--hk-neutral";
    }

    /** @deprecated use buildUnitTransferPickerStatusParts */
    function buildUnitTransferPickerStatusText(apt) {
        if (!apt || typeof apt !== "object") {
            return "—";
        }

        if (isApartmentMaintenanceActive(apt)) {
            return t("reservationDetail.units.statusMaintenance");
        }

        const parts = [];
        const occ = translatePickerOccupancy(apt.status);
        if (occ && occ !== "—") {
            parts.push(occ);
        }

        const hkLabel = translatePickerHousekeeping(apt.housekeepingStatus);
        if (hkLabel) {
            parts.push(hkLabel);
        }

        return parts.length ? parts.join(" · ") : "—";
    }

    function buildUnitTransferPickerCandidate(apt) {
        const apartmentId = Number(apt.apartmentId);
        const code = apt.apartmentCode ? `${apt.apartmentCode}`.trim() : "";
        const rType = apt.roomTypeName ? `${apt.roomTypeName}`.trim() : "";
        const label = rType ? `${rType} — ${code || apartmentId}` : code || String(apartmentId);
        const inMaintenance = isApartmentMaintenanceActive(apt);
        const vacant = isVacantPickerStatus(apt.status) && !inMaintenance;
        const statusParts = buildUnitTransferPickerStatusParts(apt);
        return {
            apartmentId,
            label,
            suggestedGross: pickSuggestedGrossFromApt(apt),
            statusText: buildUnitTransferPickerStatusText(apt),
            statusParts,
            statusBadgeClass: inMaintenance
                ? "unit-picker-badge--maintenance"
                : pickerOccupancyBadgeClass(apt.status || ""),
            vacant,
            disabled: !vacant
        };
    }

    /**
     * Filter dropdown labels: in Arabic, map common English API values to i18n (value id stays raw for filtering).
     * @param {"rt"|"st"|"hk"} category
     */
    function translatePickerFilterLabel(raw, category) {
        if (raw === undefined || raw === null) {
            return raw;
        }

        const s = `${raw}`.trim();
        if (!isArabic()) {
            return s;
        }

        if (category === "rt") {
            return s;
        }

        if (category === "st") {
            const tr = translatePickerOccupancy(s);
            return tr === "—" ? s : tr;
        }

        if (category === "hk") {
            const x = normPickerToken(s);
            if (x.includes("dirty")) {
                return t("reservationDetail.units.statusDirty");
            }
            if (x.includes("clean") && !x.includes("dirty")) {
                return t("housekeeping.clean");
            }
            if (x.includes("inspect")) {
                return t("housekeeping.inspected");
            }
            if (x.includes("cleaning")) {
                return t("housekeeping.cleaning");
            }
            return s;
        }

        return s;
    }

    /** Column «الوحدة»: room code only. */
    function unitGridNumberOnly(row) {
        if (!row) {
            return "—";
        }

        const code = row.apartmentCode ?? row.ApartmentCode;
        if (code !== undefined && code !== null && `${code}`.trim() !== "") {
            return `${code}`.trim();
        }

        const unitNumber = row.unitNumber ?? row.UnitNumber;
        if (unitNumber !== undefined && unitNumber !== null && `${unitNumber}`.trim() !== "") {
            return `${unitNumber}`.trim();
        }

        const lbl = (row.apartmentLabel ?? row.ApartmentLabel ?? "").trim();
        if (!lbl) {
            return "—";
        }

        const idx = lbl.search(/\s*[—\-]\s*/);
        if (idx > 0) {
            return lbl.slice(0, idx).trim();
        }

        const parenIdx = lbl.indexOf("(");
        if (parenIdx > 0) {
            return lbl.slice(0, parenIdx).trim();
        }

        return lbl;
    }

    function stripPricingRatesForUnit(unitId) {
        const uid = unitId != null ? String(unitId) : "";
        if (!uid) {
            return;
        }

        const prefix = `${uid}_`;
        const rates = pageCtx.pricingRateByLineKey || {};
        Object.keys(rates).forEach((k) => {
            if (k.startsWith(prefix)) {
                delete rates[k];
            }
        });
    }

    function applyLocalUnitRemoval(row) {
        const uid = Number(row.unitId);
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        pageCtx.detail.units = units.filter((u) => Number(u.unitId) !== uid);
        stripPricingRatesForUnit(uid);
        pruneDiscountsForRemovedUnit(uid);

        const remaining = (pageCtx.detail.units || []).length;
        pageCtx.useLocalFinancialTotals = remaining > 0;

        const ug = $("#unitsGrid").dxDataGrid("instance");
        if (ug) {
            ug.option("dataSource", pageCtx.detail.units.slice());
        }

        refreshCompanionsGrid();
        syncFinancialUi();
    }

    function patchReservationUnitsOnly() {
        if (!pageCtx.routeId) {
            return Promise.reject(new Error("missing route"));
        }

        if (!canAddUnit() && !canRemoveUnit()) {
            requirePmsPermission("reservations.unit_add");
            return Promise.reject(new Error("forbidden"));
        }

        const unitsPatch = buildUnitsPatchPayload();
        if (hasUnitMembershipChanges(unitsPatch) && !canRemoveUnit()) {
            requirePmsPermission("reservations.unit_remove");
            return Promise.reject(new Error("forbidden"));
        }

        const baselineCount = (reservationBaseline().units || []).length;
        if (unitsPatch.length > baselineCount && !canAddUnit()) {
            requirePmsPermission("reservations.unit_add");
            return Promise.reject(new Error("forbidden"));
        }

        if (!unitsPatch.length) {
            return Promise.reject(new Error("empty units"));
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.show();
        }

        return window.Zaaer.ReservationDetailService.patchReservation(
            pageCtx.routeId,
            { units: unitsPatch },
            pageCtx.hotelIdParam
        )
            .then((detail) => {
                if (detailPatchUsable(detail)) {
                    applyPostMutationReservationDetail(detail);
                }

                DevExpress.ui.notify(t("reservationDetail.units.removedPersisted"), "success", 2600);
                return detail;
            })
            .catch((err) => {
                const status = err && (err.status || err.statusCode);
                if (status === 403) {
                    const permCode =
                        err &&
                        err.responseJSON &&
                        (err.responseJSON.permissionCode || err.responseJSON.PermissionCode);
                    const msg = permCode
                        ? `${t("common.forbidden")} (${permCode})`
                        : t("common.forbidden");
                    DevExpress.ui.notify(msg, "warning", 4200);
                } else {
                    DevExpress.ui.notify(
                        err && err.message ? String(err.message) : t("reservationDetail.units.removeFailed"),
                        "error",
                        4200
                    );
                }

                return loadPage(false);
            })
            .finally(() => {
                if (lp) {
                    lp.hide();
                }
            });
    }

    function removeReservationUnit(row) {
        if (!row || row.unitId == null) {
            return;
        }

        if (!canRemoveUnit()) {
            requirePmsPermission("reservations.unit_remove");
            return;
        }

        if (isCheckedInUnit(row) || isCheckedOutUnit(row)) {
            return;
        }

        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        if (units.length <= 1) {
            DevExpress.ui.notify(t("reservationDetail.units.cannotRemoveLast"), "warning", 3200);
            return;
        }

        DevExpress.ui.dialog
            .confirm(t("reservationDetail.confirm.deleteUnit"), t("reservationDetail.actions.delete"))
            .done((yes) => {
                if (!yes) {
                    return;
                }

                applyLocalUnitRemoval(row);

                const uid = Number(row.unitId);
                const isPersistedLine =
                    isPersistedReservation() &&
                    pageCtx.routeId &&
                    Number.isFinite(uid) &&
                    uid > 0 &&
                    !row.isPendingUnit;

                if (isPersistedLine) {
                    patchReservationUnitsOnly();
                    return;
                }

                DevExpress.ui.notify(t("reservationDetail.units.removedLocal"), "success", 2400);
            });
    }

    function isCheckedInUnit(row) {
        const normalized = `${(row && row.unitStatus) || ""}`
            .trim()
            .toLowerCase()
            .replace(/[\s-]+/g, "_");
        return normalized === "checked_in" || normalized === "checkedin";
    }

    function isCheckedOutUnit(row) {
        const normalized = `${(row && row.unitStatus) || ""}`
            .trim()
            .toLowerCase()
            .replace(/[\s-]+/g, "_");
        return (
            normalized === "checked_out" ||
            normalized === "checkedout" ||
            normalized === "checkout" ||
            normalized === "check_out"
        );
    }

    function normalizeUnitStatusKey(raw) {
        return `${raw || ""}`.trim().toLowerCase().replace(/[\s_-]+/g, "");
    }

    function isUnitLineCheckedOut(unitRow) {
        const x = normalizeUnitStatusKey(unitRow && unitRow.unitStatus);
        return x === "checkedout" || x === "checkout";
    }

    function isUnitLineTerminalForBilling(unitRow) {
        const x = normalizeUnitStatusKey(unitRow && unitRow.unitStatus);
        return x === "checkedout" || x === "checkout" || x === "cancelled" || x === "canceled" || x === "noshow";
    }

    /** Checked-out units may have operational checkout on the same calendar day; keep at least one billed hotel night. */
    function resolveUnitCheckOutDateForPricing(unitRow) {
        const ci = parseDateOrNull(unitRow && unitRow.checkInDate);
        let co = parseDateOrNull(unitRow && unitRow.checkOutDate);
        if (!ci || Number.isNaN(ci.getTime())) {
            return co;
        }

        if (!co || Number.isNaN(co.getTime())) {
            co = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate());
        }

        if (!isUnitLineCheckedOut(unitRow)) {
            return co;
        }

        const ciCal = new Date(ci.getFullYear(), ci.getMonth(), ci.getDate());
        let coCal = new Date(co.getFullYear(), co.getMonth(), co.getDate());
        if (coCal > ciCal) {
            return co;
        }

        const dep = parseDateOrNull(unitRow && unitRow.departureDate);
        if (dep && !Number.isNaN(dep.getTime())) {
            const depCal = new Date(dep.getFullYear(), dep.getMonth(), dep.getDate());
            if (depCal > ciCal) {
                return dep;
            }
        }

        return new Date(ciCal.getFullYear(), ciCal.getMonth(), ciCal.getDate() + 1);
    }

    function reservationHasClosedUnitLines() {
        return ((pageCtx.detail && pageCtx.detail.units) || []).some((u) => isUnitLineTerminalForBilling(u));
    }

    function localPricingCoversAllBillableUnits() {
        const units = ((pageCtx.detail && pageCtx.detail.units) || []).filter((u) => {
            const x = normalizeUnitStatusKey(u && u.unitStatus);
            return x !== "cancelled" && x !== "canceled" && x !== "noshow";
        });
        if (!units.length) {
            return true;
        }

        const lines = buildRawPricingSlotLines();
        const idsWithLines = new Set(lines.map((ln) => Number(ln.unitId)));
        return units.every((u) => idsWithLines.has(Number(u.unitId)));
    }

    function formatUnitStatusLabel(row) {
        const raw = (row && row.unitStatus) || "";
        const x = normalizeUnitStatusKey(raw);
        if (pageCtx.isLocalNewReservation) {
            if (
                !x ||
                x === "confirmed" ||
                x === "unconfirmed" ||
                x === "reserved" ||
                x === "pending"
            ) {
                return t("reservationDetail.units.pendingLocal");
            }
        }

        const enLabels = {
            confirmed: "Confirmed",
            unconfirmed: "Unconfirmed",
            cancelled: "Cancelled",
            canceled: "Cancelled",
            noshow: "No show",
            reserved: "Reserved",
            checkedin: "Checked in",
            checkedout: "Checked out",
            checkout: "Checked out"
        };

        const arLabels = {
            confirmed: t("reservationDetail.status.confirmed"),
            unconfirmed: t("reservationDetail.status.unconfirmed"),
            cancelled: t("reservationDetail.status.cancelled"),
            canceled: t("reservationDetail.status.cancelled"),
            noshow: t("reservationDetail.units.lineStatusNoShow"),
            reserved: t("reservationDetail.units.lineStatusReserved"),
            checkedin: t("reservationDetail.units.lineStatusCheckedIn"),
            checkedout: t("reservationDetail.units.lineStatusCheckedOut"),
            checkout: t("reservationDetail.units.lineStatusCheckedOut")
        };

        if (!x) {
            return "—";
        }

        if (!isArabic()) {
            return enLabels[x] || raw || "—";
        }

        return arLabels[x] || raw || "—";
    }

    function unitStatusCssClass(row) {
        const x = normalizeUnitStatusKey(row && row.unitStatus);
        const label = formatUnitStatusLabel(row);
        const checkedInLabel = t("reservationDetail.units.lineStatusCheckedIn");
        if (
            x === "checkedin" ||
            x === "checked_in" ||
            label === checkedInLabel ||
            label === t("reservationDetail.status.checkedIn")
        ) {
            return "res-unit-status--checked-in";
        }
        if (x === "checkedout" || x === "checked_out" || x === "checkout") {
            return "res-unit-status--checked-out";
        }
        if (x === "cancelled" || x === "canceled" || x === "noshow") {
            return "res-unit-status--inactive";
        }
        if (x === "reserved" || x === "confirmed" || x === "unconfirmed") {
            return "res-unit-status--awaiting";
        }
        return "";
    }

    function resolveReservationStatusForSave() {
        if (isGuestArrivedSwitchOn()) {
            return "checked_in";
        }

        const statusInst = $("#resGeneralStatus").dxSelectBox("instance");
        const statusUi = statusInst ? statusInst.option("value") : "confirmed";
        if (statusUi === "unconfirmed") {
            return "unconfirmed";
        }

        return "confirmed";
    }

    /** Show unit transfer when the line is still active (not checked out / cancelled / no-show). */
    function canTransferSourceUnit(row) {
        const x = `${(row && row.unitStatus) || ""}`
            .trim()
            .toLowerCase()
            .replace(/[\s_-]+/g, "");
        if (!x) {
            return false;
        }
        if (x === "checkedout" || x === "cancelled" || x === "noshow") {
            return false;
        }
        return true;
    }

    function isCheckedInReservation() {
        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        const normalized = `${header.status || header.reservationStatus || ""}`
            .trim()
            .toLowerCase()
            .replace(/[\s-]+/g, "_");

        return (
            normalized === "checked_in" ||
            normalized === "checkedin" ||
            normalized === "check_in"
        );
    }

    function isCheckedOutReservation() {
        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        return (
            normalizeHeaderReservationStatus(header.status || header.reservationStatus) ===
            "checked_out"
        );
    }

    /** Block discounts, packages, penalties, notes, and similar edits after check-out. */
    function guardReservationModificationLocked() {
        if (!reservationGridsActionsDisabled()) {
            return true;
        }

        DevExpress.ui.notify(t("reservationDetail.permissions.checkedOut"), "warning", 3600);
        return false;
    }

    function getReservationBalanceAmount() {
        const f = pageCtx.detail && pageCtx.detail.financial;
        if (!f) {
            return 0;
        }
        const n = Number(f.balanceAmount);
        return Number.isFinite(n) ? n : 0;
    }

    function getNewUnitStayDates() {
        const reservationCheckIn = getReservationCheckInCombined();
        const reservationCheckOut = getReservationCheckOutCombined();
        if (!reservationCheckIn || !reservationCheckOut) {
            return null;
        }

        if (isCheckedInReservation()) {
            const from = new Date();
            if (reservationCheckIn instanceof Date && !Number.isNaN(reservationCheckIn.getTime())) {
                from.setHours(
                    reservationCheckIn.getHours(),
                    reservationCheckIn.getMinutes(),
                    reservationCheckIn.getSeconds(),
                    reservationCheckIn.getMilliseconds()
                );
            }

            return { checkIn: from, checkOut: reservationCheckOut };
        }

        return { checkIn: reservationCheckIn, checkOut: reservationCheckOut };
    }

    function syncNonCheckedInUnitsWithReservationDates() {
        if (!pageCtx.detail) {
            return;
        }

        // Mirror reservation date pickers on every unit (same as new reservations).
        const checkIn = getReservationCheckInCombined();
        const checkOut = getReservationCheckOutCombined();
        if (!checkIn || !checkOut) {
            return;
        }

        const ci = new Date(checkIn.getTime());
        const co = new Date(checkOut.getTime());
        const dep = new Date(checkOut.getTime());

        pageCtx.detail.units = (pageCtx.detail.units || []).map((u) => {
            if (normalizeUnitStatusKey(u && u.unitStatus) === "checkedout") {
                return u;
            }

            return {
                ...u,
                checkInDate: new Date(ci.getTime()),
                checkOutDate: new Date(co.getTime()),
                departureDate: dep
            };
        });

        const ug = $("#unitsGrid").dxDataGrid("instance");
        if (ug) {
            ug.option("dataSource", pageCtx.detail.units.slice());
            if (typeof ug.repaintRows === "function") {
                ug.repaintRows();
            } else {
                ug.refresh();
            }
        }
    }

    /** Preserve per-unit gross rates before line keys change (e.g. arrival date shift). */
    function snapshotUnitGrossRatesFromPricingMap() {
        const rates = pageCtx.pricingRateByLineKey || {};
        const byUnit = {};
        Object.keys(rates).forEach((k) => {
            const uid = Number(String(k).split("_")[0]);
            const r = Number(rates[k]);
            if (!Number.isFinite(uid) || !Number.isFinite(r) || r <= 0) {
                return;
            }
            if (byUnit[uid] == null || byUnit[uid] < r) {
                byUnit[uid] = r;
            }
        });
        return byUnit;
    }

    function onReservationStayDatesChanged() {
        if (!pageCtx.detail) {
            return;
        }

        mergeOpenEditorsIntoDetail(pageCtx.detail);

        normalizePricingRateByLineKey();
        const rateSnap = snapshotUnitGrossRatesFromPricingMap();
        syncNonCheckedInUnitsWithReservationDates();
        pageCtx._unitRateSnapshotForDateSync = rateSnap;
        try {
            prunePricingRateByLineKeyToCurrentLines();
            normalizePricingRateByLineKey();
            ensurePricingRatesForAllLines();
        } finally {
            delete pageCtx._unitRateSnapshotForDateSync;
        }
        if ((pageCtx.detail.units && pageCtx.detail.units.length) || (pageCtx.extras && pageCtx.extras.length)) {
            pageCtx.useLocalFinancialTotals = true;
        }

        syncFinancialUi({ skipFlash: true });
        refreshUnitPricingPopupDataIfOpen();

        const ugAfter = $("#unitsGrid").dxDataGrid("instance");
        if (ugAfter) {
            if (typeof ugAfter.repaintRows === "function") {
                ugAfter.repaintRows();
            } else {
                ugAfter.refresh();
            }
        }

        flashFinancialSection();
    }

    function runUnitCheckoutForRow(row) {
        if (!row) {
            return;
        }

        const uid = Number(row.unitId);
        if (!Number.isFinite(uid) || uid <= 0) {
            return;
        }

        const units = pageCtx.detail && Array.isArray(pageCtx.detail.units) ? pageCtx.detail.units : [];
        if (units.length <= 1) {
            if (!requirePmsPermission("reservations.check_out")) {
                return;
            }

            if (!ensureCheckoutDepartureAllowed()) {
                return;
            }

            showCheckoutTaxInvoiceReminder();
            openCheckoutStepperAndRun();
            return;
        }

        if (!ensureCheckoutDepartureAllowed()) {
            return;
        }

        DevExpress.ui.dialog
            .confirm(t("reservationDetail.units.unitCheckoutConfirm"), t("reservationDetail.units.unitCheckout"))
            .done((yes) => {
                if (!yes) {
                    return;
                }

                const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
                lp.show();
                window.Zaaer.ReservationDetailService.checkoutReservationUnit(pageCtx.routeId, uid, pageCtx.hotelIdParam)
                    .then((detail) => {
                        if (detailPatchUsable(detail)) {
                            applyPostMutationReservationDetail(detail);
                        } else {
                            initFooter();
                        }

                        DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2200);
                        return loadPage(false);
                    })
                    .catch((err) => {
                        DevExpress.ui.notify(
                            err && err.message ? String(err.message) : t("error.loadReservationDetail"),
                            "error",
                            4200
                        );
                    })
                    .finally(() => lp.hide());
            });
    }

    function renderUnitStayActionsCell(container, cell) {
        const row = cell.data;
        const $wrap = $("<div>").addClass("res-unit-stay-actions-cell").appendTo(container);

        if (reservationGridsActionsDisabled()) {
            return;
        }

        if (row && row.isPendingUnit) {
            return;
        }

        if (canTransferSourceUnit(row) && canTransferUnit()) {
            $("<div>")
                .appendTo($wrap)
                .dxButton({
                    icon: "sorted",
                    hint: t("reservationDetail.units.transfer"),
                    stylingMode: "text",
                    type: "default",
                    elementAttr: { class: "res-unit-stay-action-btn res-unit-stay-action-btn--transfer" },
                    onClick() {
                        if (!requirePmsPermission("reservations.unit_change")) {
                            return;
                        }

                        openUnitTransferPopup(row);
                    }
                });
        }

        if (isCheckedInUnit(row) && canUnitCheckout()) {
            $("<div>")
                .appendTo($wrap)
                .dxButton({
                    icon: "runner",
                    hint: `${t("reservationDetail.units.unitCheckout")}\n${checkoutTaxInvoiceHintText()}`,
                    stylingMode: "text",
                    type: "default",
                    elementAttr: { class: "res-unit-stay-action-btn res-unit-stay-action-btn--checkout" },
                    onClick() {
                        if (!requirePmsPermission("reservations.unit_check_out")) {
                            return;
                        }

                        runUnitCheckoutForRow(row);
                    }
                });
        }
    }

    function canShowRateSource() {
        const api = window.Zaaer.ApiService;
        return api && api.hasPermission && api.hasPermission("property.rates.show_source");
    }

    function rateSourceLabel(code) {
        const c = (code || "none").trim().toLowerCase();
        const key = "property.rates.source." + c;
        const label = t(key);
        return label !== key ? label : c;
    }

    function buildUnitsGridColumns() {
        const actionsOff = reservationGridsActionsDisabled();

        const removeCol = {
            type: "buttons",
            name: "unitRemoveActions",
            width: 36,
            caption: "",
            visible: !actionsOff,
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            cssClass: "res-units-col-delete",
            buttons: [
                {
                    hint: t("reservationDetail.actions.delete"),
                    icon: "trash",
                    stylingMode: "text",
                    cssClass: "res-unit-delete-btn",
                    visible(e) {
                        if (actionsOff || !canRemoveUnit()) {
                            return false;
                        }

                        const row = e.row && e.row.data;
                        return !isCheckedInUnit(row) && !isCheckedOutUnit(row);
                    },
                    onClick(e) {
                        if (!requirePmsPermission("reservations.unit_remove")) {
                            return;
                        }

                        removeReservationUnit(e.row && e.row.data);
                    }
                }
            ]
        };

        const mobile = isPaymentsMobileViewport();

        const bodyCols = [
            {
                name: "unitNo",
                dataField: "apartmentCode",
                caption: t("reservationDetail.units.unit"),
                width: mobile ? 88 : undefined,
                minWidth: mobile ? 80 : 96,
                cssClass: "res-units-col-unit-no",
                calculateCellValue: (r) => unitGridNumberOnly(r),
                cellTemplate(container, cell) {
                    const $wrap = $("<div>").addClass("res-unit-no-cell").appendTo(container);
                    $("<span>")
                        .addClass("res-unit-no-bold")
                        .text(unitGridNumberOnly(cell.data))
                        .appendTo($wrap);
                    $("<div>")
                        .appendTo($wrap)
                        .dxButton({
                            icon: "edit",
                            hint: t("reservationDetail.units.editPrice"),
                            stylingMode: "text",
                            elementAttr: { class: "res-unit-price-edit-btn" },
                            onClick() {
                                openUnitPricingPopupForUnit(cell.data);
                            }
                        });
                }
            },
            {
                dataField: "roomTypeName",
                caption: t("reservationDetail.units.roomType"),
                width: mobile ? 140 : undefined,
                minWidth: mobile ? 128 : 140,
                cssClass: "res-units-col-room-type",
                cellTemplate(container, cell) {
                    const text =
                        cell.data && cell.data.roomTypeName && `${cell.data.roomTypeName}`.trim()
                            ? `${cell.data.roomTypeName}`.trim()
                            : "—";
                    $("<span>")
                        .addClass("res-unit-room-type-text")
                        .attr("title", text)
                        .text(text)
                        .appendTo(container);
                }
            },
            {
                name: "rentAmount",
                caption: t("reservationDetail.units.rentAmount"),
                width: mobile ? 132 : undefined,
                minWidth: mobile ? 124 : 108,
                alignment: "center",
                cssClass: "res-units-col-rent",
                allowEditing: false,
                allowSorting: false,
                allowFiltering: false,
                allowHeaderFiltering: false,
                cellTemplate(container, cell) {
                    const lastRate = getUnitLastNightRate(cell.data);
                    const total = sumUnitGrossRentFromNightPricing(cell.data);
                    const $wrap = $("<div>").addClass("res-unit-rate-cell");
                    $("<span>").addClass("res-unit-rate-main").text(formatMoneyEn(lastRate)).appendTo($wrap);
                    if (total > 0 && Math.abs(total - lastRate) > 0.005) {
                        // Show stay total as secondary label only when it differs from the nightly rate.
                        $("<span>")
                            .addClass("res-unit-rate-total")
                            .text(`${t("reservationDetail.units.total")}: ${formatMoneyEn(total)}`)
                            .appendTo($wrap);
                    }
                    if (canShowRateSource() && cell.data && cell.data.defaultGrossRateSource) {
                        $("<span>")
                            .addClass("res-unit-rate-source")
                            .text(rateSourceLabel(cell.data.defaultGrossRateSource))
                            .appendTo($wrap);
                    }
                    $wrap.appendTo(container);
                }
            },
            {
                caption: t("reservationDetail.units.from"),
                dataField: "checkInDate",
                width: mobile ? 116 : undefined,
                minWidth: 108,
                cssClass: "res-units-col-date",
                allowSorting: false,
                allowFiltering: false,
                allowHeaderFiltering: false,
                cellTemplate(container, e) {
                    renderUnitDateTextCell(container, e, "checkInDate");
                }
            },
            {
                caption: t("reservationDetail.units.to"),
                dataField: "checkOutDate",
                width: mobile ? 116 : undefined,
                minWidth: 108,
                cssClass: "res-units-col-date",
                allowSorting: false,
                allowFiltering: false,
                allowHeaderFiltering: false,
                cellTemplate(container, e) {
                    renderUnitDateTextCell(container, e, "checkOutDate");
                }
            },
            {
                caption: t("reservationDetail.units.departure"),
                dataField: "departureDate",
                width: 116,
                minWidth: 112,
                cssClass: "res-units-col-date",
                visible: false,
                showInColumnChooser: false,
                allowHiding: false,
                cellTemplate(container, e) {
                    renderUnitDateTextCell(container, e, "departureDate");
                }
            },
            {
                caption: t("reservationDetail.units.status"),
                dataField: "unitStatus",
                width: mobile ? 118 : undefined,
                minWidth: mobile ? 112 : 96,
                cssClass: "res-units-col-status",
                calculateCellValue(row) {
                    return formatUnitStatusLabel(row);
                },
                cellTemplate(container, cell) {
                    const row = cell.data;
                    const statusClass = unitStatusCssClass(row);
                    const $label = $("<span>")
                        .addClass("res-unit-status-label")
                        .text(formatUnitStatusLabel(row));
                    if (statusClass) {
                        $label.addClass(statusClass);
                    }
                    $label.appendTo(container);
                }
            }
        ];

        if (pageCtx.isLocalNewReservation) {
            return pickResDetailMobileColumns(
                [...bodyCols, removeCol],
                ["unitNo", "roomTypeName", "rentAmount", "unitStatus"],
                4
            );
        }

        const stayCol = {
            name: "unitStayActions",
            width: mobile ? 84 : 100,
            minWidth: mobile ? 76 : 92,
            caption: t("reservationDetail.units.actions"),
            visible: !actionsOff,
            fixed: !mobile,
            fixedPosition: reservationGridActionFixedPosition(),
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            cssClass: "res-units-col-actions",
            alignment: "center",
            cellTemplate: renderUnitStayActionsCell
        };

        return pickResDetailMobileColumns(
            [...bodyCols, stayCol, removeCol],
            ["unitNo", "roomTypeName", "rentAmount", "unitStatus", "unitStayActions"],
            4
        );
    }

    function isApartmentOnReservation(apt, units) {
        const aid = apt.apartmentId != null ? Number(apt.apartmentId) : null;
        const zid = apt.zaaerId != null ? Number(apt.zaaerId) : null;
        return units.some((u) => {
            const ua = u.apartmentId != null ? Number(u.apartmentId) : null;
            const uaz = u.apartmentZaaerId != null ? Number(u.apartmentZaaerId) : null;
            const uuz = u.unitZaaerId != null ? Number(u.unitZaaerId) : null;
            if (aid != null && ua === aid) {
                return true;
            }
            if (zid != null && (uaz === zid || uuz === zid)) {
                return true;
            }
            return false;
        });
    }

    function apartmentRowMatchesPickerApt(unitRow, apt) {
        const aid = apt.apartmentId != null ? Number(apt.apartmentId) : NaN;
        const az = apt.zaaerId != null ? Number(apt.zaaerId) : NaN;
        const rowApt = unitRow.apartmentId != null ? Number(unitRow.apartmentId) : NaN;
        const rowAptZ = unitRow.apartmentZaaerId != null ? Number(unitRow.apartmentZaaerId) : NaN;
        if (Number.isFinite(aid) && Number.isFinite(rowApt) && aid === rowApt) {
            return true;
        }
        if (Number.isFinite(az) && Number.isFinite(rowAptZ) && az === rowAptZ) {
            return true;
        }
        if (Number.isFinite(az) && Number.isFinite(rowApt) && az === rowApt) {
            return true;
        }
        if (Number.isFinite(aid) && Number.isFinite(rowAptZ) && aid === rowAptZ) {
            return true;
        }
        return false;
    }

    /** Room code/label for transfer popup title (single token, e.g. "105" not "105__105"). */
    function transferRoomDisplayForTitle(row) {
        if (!row || typeof row !== "object") {
            return "—";
        }
        const code = row.apartmentCode != null ? `${row.apartmentCode}`.trim() : "";
        if (code) {
            return code;
        }
        const label = row.apartmentLabel != null ? `${row.apartmentLabel}`.trim() : "";
        if (!label) {
            return "—";
        }
        const segments = label.split(/\s*(?:_+|——|—|--)\s*/).map((s) => s.trim()).filter(Boolean);
        if (segments.length >= 2 && segments.every((s) => s === segments[0])) {
            return segments[0];
        }
        return segments[0] || label;
    }

    function openUnitTransferPopup(fromUnitRow) {
        if (!pageCtx.detail) {
            DevExpress.ui.notify(t("error.loadReservationDetail"), "warning", 2600);
            return;
        }

        if (pageCtx.isLocalNewReservation) {
            return;
        }

        const hotelId = pageCtx.detail.hotelId;
        if (!hotelId) {
            DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 2600);
            return;
        }

        if (!fromUnitRow || !Number.isFinite(Number(fromUnitRow.unitId))) {
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.show();
        }

        window.Zaaer.ReservationDetailService.loadApartmentsForPicker(hotelId)
            .then((rows) => {
                if (lp) {
                    lp.hide();
                }
                openUnitTransferPopupCore(fromUnitRow, Array.isArray(rows) ? rows : []);
            })
            .catch(() => {
                if (lp) {
                    lp.hide();
                }
                DevExpress.ui.notify(t("reservationDetail.units.loadPickerFailed"), "error", 3200);
            });
    }

    function appendUnitTransferSelectItem(element, data) {
        if (!data || !element) {
            return;
        }

        const dis = !!data.disabled;
        const $row = $("<div/>").addClass("unit-transfer-select-item");
        if (dis) {
            $row.addClass("unit-transfer-select-item--disabled");
        } else {
            $row.addClass("unit-transfer-select-item--selectable");
        }

        const $main = $("<div/>").addClass("unit-transfer-select-item__main").appendTo($row);

        $("<span/>")
            .addClass("unit-transfer-select-item__label")
            .text(data.label || "")
            .appendTo($main);

        const price = data.suggestedGross != null ? Number(data.suggestedGross) : NaN;
        if (Number.isFinite(price) && price > 0) {
            const $priceWrap = $("<span/>").addClass("unit-transfer-select-item__price-wrap").appendTo($main);
            $("<span/>")
                .addClass("unit-transfer-select-item__price-arrow")
                .attr("aria-hidden", "true")
                .appendTo($priceWrap);
            $("<span/>")
                .addClass("unit-transfer-select-item__price")
                .text(formatMoneyEn(price))
                .appendTo($priceWrap);
        }

        const parts = data.statusParts || {};
        const $statuses = $("<div/>").addClass("unit-transfer-select-item__statuses").appendTo($row);

        if (parts.occText) {
            $("<span/>")
                .addClass(`unit-transfer-status-badge ${parts.occBadgeClass || "unit-transfer-status-badge--neutral"}`)
                .text(parts.occText)
                .appendTo($statuses);
        }

        if (parts.hkText) {
            $("<span/>")
                .addClass(`unit-transfer-status-badge ${parts.hkBadgeClass || "unit-transfer-status-badge--hk-neutral"}`)
                .text(parts.hkText)
                .appendTo($statuses);
        }

        if (!$statuses.children().length) {
            $("<span/>")
                .addClass("unit-transfer-status-badge unit-transfer-status-badge--neutral")
                .text(data.statusText || "—")
                .appendTo($statuses);
        }

        $(element).empty().append($row);
    }

    function openUnitTransferPopupCore(fromUnitRow, allApartments) {
        const unitsOnRes = (pageCtx.detail && pageCtx.detail.units) || [];
        const candidates = allApartments
            .filter((a) => !apartmentRowMatchesPickerApt(fromUnitRow, a))
            .filter((a) => !isApartmentOnReservation(a, unitsOnRes))
            .map((a) => buildUnitTransferPickerCandidate(a))
            .filter((x) => Number.isFinite(x.apartmentId) && x.apartmentId > 0)
            .sort((a, b) => {
                if (a.vacant !== b.vacant) {
                    return a.vacant ? -1 : 1;
                }

                return `${a.label || ""}`.localeCompare(`${b.label || ""}`, undefined, {
                    sensitivity: "base",
                    numeric: true
                });
            });

        if (!candidates.length) {
            DevExpress.ui.notify(t("reservationDetail.units.transferNoTargets"), "warning", 3200);
            return;
        }

        const hasVacantTarget = candidates.some((c) => c.vacant);
        if (!hasVacantTarget) {
            DevExpress.ui.notify(t("reservationDetail.units.transferNoTargets"), "warning", 3200);
            return;
        }

        const curLabel = transferRoomDisplayForTitle(fromUnitRow);
        const $host = $("<div>").appendTo("body");
        let popupInst = null;
        let sbInst = null;
        let rgInst = null;
        let taInst = null;

        $host.dxPopup({
            title: `${t("reservationDetail.units.transferTitle")}: ${curLabel}`,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            visible: true,
            showCloseButton: true,
            showTitle: true,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup unit-transfer-popup-wrap" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty();
                const $shell = $("<div/>")
                    .addClass("unit-transfer-root")
                    .appendTo($content);

                $("<div/>")
                    .addClass("unit-transfer-field-label")
                    .text(t("reservationDetail.units.transferPickUnit"))
                    .appendTo($shell);

                const $sbHost = $("<div/>").addClass("unit-transfer-field-host").appendTo($shell);
                const $rgHost = $("<div/>").addClass("unit-transfer-field-host").appendTo($shell);
                const $taHost = $("<div/>").addClass("unit-transfer-field-host").appendTo($shell);
                const $actions = $("<div/>").addClass("unit-transfer-actions").appendTo($shell);

                $sbHost.dxSelectBox({
                    dataSource: candidates,
                    valueExpr: "apartmentId",
                    displayExpr: "label",
                    searchEnabled: true,
                    showClearButton: true,
                    openOnFieldClick: true,
                    placeholder: t("reservationDetail.units.transferPickUnit"),
                    dropDownOptions: {
                        hideOnParentScroll: false,
                        container: document.body
                    },
                    itemTemplate(data, _index, element) {
                        appendUnitTransferSelectItem(element, data);
                    },
                    onInitialized(e) {
                        sbInst = e.component;
                    },
                    onValueChanged(ev) {
                        const v = ev.value;
                        const row = candidates.find((c) => c.apartmentId === v);
                        if (row && row.disabled) {
                            ev.component.option("value", ev.previousValue ?? null);
                            DevExpress.ui.notify(t("reservationDetail.units.transferNotVacantHint"), "info", 2600);
                        }
                    },
                    onItemClick(ev) {
                        if (ev.itemData && ev.itemData.disabled) {
                            ev.event.preventDefault();
                            DevExpress.ui.notify(t("reservationDetail.units.transferNotVacantHint"), "info", 2600);
                        }
                    }
                });

                $rgHost.dxRadioGroup({
                    items: [
                        { value: "SamePrice", text: t("reservationDetail.units.transferModeSame") },
                        { value: "NewFromToday", text: t("reservationDetail.units.transferModeToday") },
                        { value: "NewForAllDays", text: t("reservationDetail.units.transferModeAll") }
                    ],
                    value: "SamePrice",
                    valueExpr: "value",
                    displayExpr: "text",
                    layout: "vertical",
                    onInitialized(e) {
                        rgInst = e.component;
                    }
                });

                $taHost.dxTextArea({
                    maxLength: 500,
                    height: 88,
                    placeholder: t("reservationDetail.units.transferComment"),
                    onInitialized(e) {
                        taInst = e.component;
                    }
                });

                $("<div/>")
                    .appendTo($actions)
                    .dxButton({
                        text: t("reservationDetail.units.transferDiscard"),
                        stylingMode: "outlined",
                        type: "normal",
                        onClick() {
                            if (popupInst) {
                                popupInst.hide();
                            }
                        }
                    });

                $("<div/>")
                    .appendTo($actions)
                    .dxButton({
                        text: t("reservationDetail.units.transferSave"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            const toId = sbInst ? sbInst.option("value") : null;
                            const picked = candidates.find((c) => c.apartmentId === toId);
                            if (!toId || !picked || picked.disabled) {
                                DevExpress.ui.notify(t("reservationDetail.units.transferPickUnit"), "warning", 2600);
                                return;
                            }

                            const applyMode = rgInst ? rgInst.option("value") : "SamePrice";
                            const comment = taInst ? `${taInst.option("value") || ""}`.trim() : "";
                            if (!comment) {
                                DevExpress.ui.notify(
                                    t("reservationDetail.units.transferCommentRequired"),
                                    "warning",
                                    3200
                                );
                                if (taInst) {
                                    taInst.focus();
                                }
                                return;
                            }

                            const lp2 = $("#reservationLoadPanel").dxLoadPanel("instance");
                            if (lp2) {
                                lp2.show();
                            }

                            window.Zaaer.ReservationDetailService.swapReservationUnit(
                                pageCtx.routeId,
                                {
                                    unitId: Number(fromUnitRow.unitId),
                                    toApartmentId: Number(toId),
                                    applyMode,
                                    comment: comment || null
                                },
                                pageCtx.hotelIdParam
                            )
                                .then(() => loadPage(false))
                                .then(() => {
                                    DevExpress.ui.notify(t("reservationDetail.units.transferSuccess"), "success", 2600);
                                    if (popupInst) {
                                        popupInst.hide();
                                    }
                                })
                                .catch((err) => {
                                    const msg =
                                        err && err.message ? String(err.message) : t("reservationDetail.units.transferFailed");
                                    DevExpress.ui.notify(msg, "error", 4200);
                                })
                                .finally(() => {
                                    if (lp2) {
                                        lp2.hide();
                                    }
                                });
                        }
                    });
            },
            onShown() {
                popupInst = $host.dxPopup("instance");
            },
            onHidden() {
                sbInst = null;
                rgInst = null;
                taInst = null;
                popupInst = null;
                $host.remove();
            }
        });
    }

    function pickSuggestedGrossFromApt(apt) {
        if (!apt || typeof apt !== "object") {
            return 0;
        }

        return suggestedGrossFromPickerRow(apt, isMonthlyRentalMode());
    }

    function buildPendingUnitRow(apt, checkIn, checkOut, grossOverride) {
        const type = apt.roomTypeName ? `${apt.roomTypeName}`.trim() : "";
        const code = apt.apartmentCode ? `${apt.apartmentCode}`.trim() : "";
        const label = type ? `${type} — ${code}` : code || "—";
        const aptId = Number(apt.apartmentId);
        const uid =
            Number.isFinite(aptId) && aptId !== 0 ? -Math.abs(aptId) : -Math.abs(Date.now() % 2147483647);
        const overrideNum =
            grossOverride != null && Number.isFinite(Number(grossOverride))
                ? roundMoney(Number(grossOverride))
                : NaN;
        const defaultGrossRate =
            Number.isFinite(overrideNum) && overrideNum >= 0
                ? overrideNum
                : pickSuggestedGrossFromApt(apt);

        return {
            unitId: uid,
            unitZaaerId: null,
            apartmentId: apt.apartmentId,
            apartmentZaaerId: apt.zaaerId != null ? apt.zaaerId : null,
            apartmentCode: code || null,
            apartmentLabel: label,
            roomTypeName: type || null,
            buildingName: apt.buildingName || null,
            floorName: apt.floorName || null,
            checkInDate: checkIn,
            checkOutDate: checkOut,
            departureDate: null,
            unitStatus: t("reservationDetail.units.pendingLocal"),
            isPendingUnit: true,
            defaultGrossRate,
            pickerCustomGrossRate:
                grossOverride != null && Number.isFinite(Number(grossOverride))
                    ? roundMoney(Number(grossOverride))
                    : undefined
        };
    }

    function openUnitPicker() {
        if (!requirePmsPermission("reservations.unit_add")) {
            return;
        }

        if (!pageCtx.detail) {
            DevExpress.ui.notify(t("error.loadReservationDetail"), "warning", 2600);
            return;
        }

        const hotelId = pageCtx.detail.hotelId;
        if (!hotelId) {
            DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 2600);
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.show();
        }

        window.Zaaer.ReservationDetailService.loadApartmentsForPicker(hotelId)
            .then((rows) => {
                if (lp) {
                    lp.hide();
                }
                openUnitPickerPopup(Array.isArray(rows) ? rows : []);
            })
            .catch(() => {
                if (lp) {
                    lp.hide();
                }
                DevExpress.ui.notify(t("reservationDetail.units.loadPickerFailed"), "error", 3200);
            });
    }

    function openUnitPickerPopup(allApartments) {
        const EMPTY_MARK = "__EMPTY__";
        const bulkPick = canBulkAddReservationUnits();
        const unitsOnRes = (pageCtx.detail && pageCtx.detail.units) || [];
        const available = allApartments
            .filter((a) => !isApartmentOnReservation(a, unitsOnRes))
            .filter((a) => isVacantPickerStatus(a.status) && !isApartmentMaintenanceActive(a));

        if (!available.length) {
            DevExpress.ui.notify(t("reservationDetail.units.noVacantUnitsAvailable"), "warning", 3200);
            return;
        }

        const aptById = new Map(available.map((a) => [Number(a.apartmentId), a]));
        const isMobile = window.matchMedia("(max-width: 767px)").matches;
        const selectedIds = new Set();
        const customRatesByApartmentId = new Map();
        let filterRoomType = "__ALL__";
        let filterUnitSearch = "";
        let focusRateApartmentId = null;

        function pickerRoomTypeName(row) {
            if (!row || typeof row !== "object") {
                return "";
            }

            const raw =
                row.roomTypeName ??
                row.RoomTypeName ??
                row.roomType ??
                row.RoomType ??
                "";
            return `${raw}`.trim();
        }

        function apartmentCodeMatchesSearch(row) {
            const q = `${filterUnitSearch || ""}`.trim().toLowerCase();
            if (!q) {
                return true;
            }

            const code = `${row.apartmentCode ?? row.ApartmentCode ?? ""}`.trim().toLowerCase();
            return code.includes(q);
        }

        function closeUnitPickerDrawer() {
            $("#unitPickerDrawerOverlay").remove();
            $("body").removeClass("unit-picker-drawer-open");
        }

        function pickerRateForApartment(apt) {
            const id = Number(apt.apartmentId);
            if (customRatesByApartmentId.has(id)) {
                return customRatesByApartmentId.get(id);
            }
            return pickSuggestedGrossFromApt(apt);
        }

        function setPickerRateForApartment(apartmentId, value) {
            if (value === undefined || value === null || value === "") {
                return;
            }

            const n = Number(value);
            if (!Number.isFinite(n) || n < 0) {
                return;
            }

            customRatesByApartmentId.set(Number(apartmentId), roundMoney(n));
        }

        function distinctOptional(rows, getVal) {
            let hasEmpty = false;
            const vals = new Set();
            rows.forEach((r) => {
                const raw = getVal(r);
                const s = raw === undefined || raw === null ? "" : `${raw}`.trim();
                if (!s) {
                    hasEmpty = true;
                } else {
                    vals.add(s);
                }
            });
            return { hasEmpty, values: [...vals].sort((a, b) => a.localeCompare(b)) };
        }

        function updatePickerSelectionFooter($countEl, $chipsEl) {
            const n = selectedIds.size;
            $countEl.text(t("reservationDetail.units.selectedCount").replace("{0}", String(n)));
            $chipsEl.empty().toggleClass("is-empty", n === 0);
            if (!n) {
                return;
            }

            const rows = [];
            selectedIds.forEach((apid) => {
                const apt = aptById.get(Number(apid));
                const code = apt && apt.apartmentCode != null ? `${apt.apartmentCode}`.trim() : "";
                rows.push({
                    id: Number(apid),
                    code: code || String(apid)
                });
            });
            rows.sort((a, b) => a.code.localeCompare(b.code, undefined, { numeric: true }));

            rows.forEach((row) => {
                const $chip = $("<span>").addClass("unit-picker-selected-chip").attr("title", row.code);
                $chip.append($("<span>").addClass("unit-picker-selected-chip-code").text(row.code));
                if (bulkPick) {
                    $chip.append(
                        $("<button>")
                            .attr("type", "button")
                            .addClass("unit-picker-selected-chip-remove")
                            .attr("aria-label", t("reservationDetail.actions.delete"))
                            .html("&times;")
                            .on("click", (ev) => {
                                ev.preventDefault();
                                ev.stopPropagation();
                                selectedIds.delete(row.id);
                                customRatesByApartmentId.delete(row.id);
                                renderCards($grid, $countEl, $chipsEl);
                            })
                    );
                }
                $chipsEl.append($chip);
            });
        }

        function toWesternDigits(raw) {
            let s = raw == null ? "" : `${raw}`;
            const ar = "٠١٢٣٤٥٦٧٨٩";
            const fa = "۰۱۲۳۴۵۶۷۸۹";
            for (let i = 0; i < 10; i++) {
                s = s.split(ar[i]).join(String(i));
                s = s.split(fa[i]).join(String(i));
            }
            return s.replace(/,/g, "").trim();
        }

        function formatPickerRateCellValue(n) {
            const v = roundMoney(Number(n) || 0);
            return v.toLocaleString("en-US", {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            });
        }

        function mountPickerRateEditor($priceRow, apartmentId, gross) {
            const startVal = roundMoney(Number(gross) || 0);

            const $control = $("<div>").addClass("unit-picker-rate-control");
            const $minus = $("<button>")
                .attr("type", "button")
                .addClass("unit-picker-rate-step")
                .attr("aria-label", t("reservationDetail.units.pickerRateDecrease"))
                .text("−");
            const $input = $("<input>")
                .attr("type", "text")
                .attr("inputmode", "decimal")
                .attr("dir", "ltr")
                .attr("lang", "en")
                .attr("autocomplete", "off")
                .addClass("unit-picker-rate-input")
                .attr("aria-label", t("reservationDetail.units.pickerRate"))
                .val(formatPickerRateCellValue(startVal));
            const $plus = $("<button>")
                .attr("type", "button")
                .addClass("unit-picker-rate-step")
                .attr("aria-label", t("reservationDetail.units.pickerRateIncrease"))
                .text("+");

            $control.append($minus, $input, $plus);
            $priceRow.append($control);
            $control.on("click mousedown pointerdown", (ev) => ev.stopPropagation());

            function readVal() {
                const n = Number(toWesternDigits($input.val()));
                return Number.isFinite(n) ? roundMoney(Math.max(0, n)) : 0;
            }

            function commit(next) {
                const v = roundMoney(Math.max(0, next));
                $input.val(formatPickerRateCellValue(v));
                setPickerRateForApartment(apartmentId, v);
            }

            $input.on("focus", function onRateFocus() {
                this.select();
            });

            $input.on("input", function onRateInput() {
                const norm = toWesternDigits(this.value);
                if (norm !== this.value) {
                    this.value = norm;
                }
            });

            $input.on("change blur", () => {
                commit(readVal());
            });

            $minus.on("click", (ev) => {
                ev.preventDefault();
                ev.stopPropagation();
                commit(readVal() - 1);
            });
            $plus.on("click", (ev) => {
                ev.preventDefault();
                ev.stopPropagation();
                commit(readVal() + 1);
            });

            if (startVal > 0) {
                setPickerRateForApartment(apartmentId, startVal);
            }

            return $input;
        }

        function filterRows() {
            return available.filter((a) => {
                if (!apartmentCodeMatchesSearch(a)) {
                    return false;
                }

                const rt = pickerRoomTypeName(a);
                if (filterRoomType !== "__ALL__") {
                    if (filterRoomType === EMPTY_MARK) {
                        if (rt !== "") {
                            return false;
                        }
                    } else if (rt !== filterRoomType) {
                        return false;
                    }
                }

                return true;
            });
        }

        function renderCards($grid, $countEl, $chipsEl) {
            const rows = filterRows();
            $grid.empty();

            rows.forEach((a) => {
                const id = Number(a.apartmentId);
                const sel = selectedIds.has(id);
                const code = a.apartmentCode || "—";
                const rType = pickerRoomTypeName(a);
                const st = a.status || "";
                const hk = a.housekeepingStatus || "";

                const borderCls = pickerCardBorderClass(st, hk);
                const occLabel = translatePickerOccupancy(st);
                const badgeCls = pickerOccupancyBadgeClass(st);

                const $card = $("<div>")
                    .addClass("unit-picker-card")
                    .addClass(borderCls)
                    .toggleClass("is-selected", sel)
                    .attr("data-aptid", String(id))
                    .attr("tabindex", 0)
                    .on("click", (ev) => {
                        if (
                            $(ev.target).closest(
                                ".unit-picker-card-price-row, .unit-picker-rate-editor, .unit-picker-rate-input, .unit-picker-rate-control"
                            ).length
                        ) {
                            return;
                        }

                        if (bulkPick) {
                            if (selectedIds.has(id)) {
                                selectedIds.delete(id);
                                customRatesByApartmentId.delete(id);
                            } else {
                                selectedIds.add(id);
                                if (!customRatesByApartmentId.has(id)) {
                                    customRatesByApartmentId.set(id, pickSuggestedGrossFromApt(a));
                                }
                                focusRateApartmentId = id;
                            }
                        } else {
                            selectedIds.clear();
                            customRatesByApartmentId.clear();
                            selectedIds.add(id);
                            customRatesByApartmentId.set(id, pickSuggestedGrossFromApt(a));
                            focusRateApartmentId = id;
                        }
                        renderCards($grid, $countEl, $chipsEl);
                    });

                const gross = pickerRateForApartment(a);
                const $head = $("<div>").addClass("unit-picker-card-head");
                $head.append($("<span>").addClass("unit-picker-card-code").text(code));
                if (!sel && Number.isFinite(gross) && gross > 0) {
                    $head.append(
                        $("<span>")
                            .addClass("unit-picker-card-price")
                            .text(`(${formatMoneyEn(gross)})`)
                    );
                }
                $card.append($head);
                if (rType) {
                    $card.append($("<div>").addClass("unit-picker-card-type").text(rType));
                }
                $card.append(
                    $("<div>")
                        .addClass("unit-picker-card-row")
                        .append(
                            $("<span>")
                                .addClass("unit-picker-badge")
                                .addClass(badgeCls)
                                .text(occLabel)
                        )
                );
                if (isDirtyHousekeeping(hk)) {
                    $card.append(
                        $("<div>")
                            .addClass("unit-picker-card-row")
                            .append(
                                $("<span>")
                                    .addClass("unit-picker-badge--dirty-hk")
                                    .text(t("reservationDetail.units.statusDirty"))
                            )
                    );
                }

                if (sel) {
                    const $priceRow = $("<div>").addClass("unit-picker-card-price-row");
                    $priceRow.append(
                        $("<span>")
                            .addClass("unit-picker-card-price-label")
                            .text(t("reservationDetail.units.pickerRate"))
                    );
                    const $nbHost = mountPickerRateEditor($priceRow, id, gross);
                    $nbHost.addClass("unit-picker-rate-editor").attr("data-aptid", String(id));
                    $card.append($priceRow);
                }

                $grid.append($card);
            });

            updatePickerSelectionFooter($countEl, $chipsEl);

            if (focusRateApartmentId != null) {
                const cur = focusRateApartmentId;
                focusRateApartmentId = null;
                setTimeout(() => {
                    const input = $grid.find(`.unit-picker-rate-editor[data-aptid='${cur}']`).get(0);
                    if (input) {
                        input.focus();
                        if (typeof input.select === "function") {
                            input.select();
                        }
                    }
                }, 0);
            }
        }

        function resolvePickerGrossOverride(apartmentId) {
            const id = Number(apartmentId);
            if (customRatesByApartmentId.has(id)) {
                return customRatesByApartmentId.get(id);
            }
            return undefined;
        }

        $("body").addClass("unit-picker-drawer-open");
        const rtl = isArabic();
        const $overlay = $("<div>")
            .attr("id", "unitPickerDrawerOverlay")
            .addClass("unit-picker-overlay")
            .addClass(isMobile ? "unit-picker-overlay--modal" : "unit-picker-overlay--drawer")
            .toggleClass("unit-picker-overlay--rtl", rtl)
            .toggleClass("unit-picker-overlay--ltr", !rtl)
            .appendTo("body");

        const $panel = $("<div>")
            .addClass("unit-picker-panel")
            .addClass(isMobile ? "unit-picker-panel--modal" : "unit-picker-panel--drawer")
            .appendTo($overlay);

        $panel.on("click", (ev) => ev.stopPropagation());

        if (!isMobile) {
            const key = "pms.unitPickerDrawerWidthPx";
            const saved = Number(window.localStorage ? window.localStorage.getItem(key) : NaN);
            const initial = Number.isFinite(saved) && saved >= 360 && saved <= 860 ? saved : null;
            if (initial != null) {
                $panel.css("width", `${initial}px`);
            }

            const $handle = $("<div>")
                .addClass("unit-picker-resize-handle")
                .toggleClass("unit-picker-resize-handle--rtl", rtl)
                .appendTo($panel);

            let resizing = false;
            let startX = 0;
            let startW = 0;
            const minW = 360;
            const maxW = Math.min(860, Math.max(520, window.innerWidth - 120));

            function onMove(ev) {
                if (!resizing) {
                    return;
                }
                const x = ev && typeof ev.clientX === "number" ? ev.clientX : 0;
                const dx = x - startX;
                const next = rtl ? startW - dx : startW + dx;
                const w = Math.max(minW, Math.min(maxW, next));
                $panel.css("width", `${w}px`);
            }

            function onUp() {
                if (!resizing) {
                    return;
                }
                resizing = false;
                $("body").removeClass("unit-picker-resizing");
                const w = Math.round($panel.outerWidth() || 0);
                try {
                    if (window.localStorage && w > 0) {
                        window.localStorage.setItem(key, String(w));
                    }
                } catch {
                    // ignore
                }
                $(document).off("mousemove", onMove);
                $(document).off("mouseup", onUp);
            }

            $handle.on("mousedown", (ev) => {
                resizing = true;
                startX = ev.clientX;
                startW = $panel.outerWidth() || 0;
                $("body").addClass("unit-picker-resizing");
                $(document).on("mousemove", onMove);
                $(document).on("mouseup", onUp);
                ev.preventDefault();
                ev.stopPropagation();
            });
        }

        const $shell = $("<div>").addClass("unit-picker-root").appendTo($panel);

        $("<div>")
            .addClass("unit-picker-head")
            .append(
                $("<div>")
                    .addClass("unit-picker-sub")
                    .text(
                        bulkPick
                            ? t("reservationDetail.units.pickerSubtitle")
                            : t("reservationDetail.units.pickerSubtitleSingle")
                    )
            )
            .appendTo($shell);

        const $filters = $("<div>").addClass("unit-picker-filters").appendTo($shell);

        const rtDist = distinctOptional(available, pickerRoomTypeName);

        const rtItems = [{ id: "__ALL__", text: t("common.all") }]
            .concat(rtDist.hasEmpty ? [{ id: EMPTY_MARK, text: t("reservationDetail.units.notSpecified") }] : [])
            .concat(rtDist.values.map((x) => ({ id: x, text: translatePickerFilterLabel(x, "rt") })));

        const $search = $("<div>").addClass("unit-picker-filters-search").attr("id", "upFilterSearch").appendTo($filters);
        const $gr = $("<div>").addClass("unit-picker-filters-type").attr("id", "upFilterRt").appendTo($filters);
        const $reset = $("<div>").attr("id", "upFilterReset").appendTo($filters);
        const $close = $("<div>").addClass("unit-picker-filters-close").attr("id", "upPickerClose").appendTo($filters);

        const $gridHost = $("<div>").addClass("unit-picker-grid-host").appendTo($shell);
        const $grid = $("<div>").addClass("unit-picker-grid").appendTo($gridHost);
        const $footer = $("<div>").addClass("unit-picker-footer").appendTo($shell);
        const $footerMeta = $("<div>").addClass("unit-picker-footer-meta").appendTo($footer);
        const $countEl = $("<div>").addClass("unit-picker-selected-count").appendTo($footerMeta);
        const $chipsEl = $("<div>").addClass("unit-picker-selected-chips is-empty").appendTo($footerMeta);
        const $actions = $("<div>").addClass("unit-picker-footer-actions").appendTo($footer);
        const $btnOk = $("<div>").attr("id", "upPickConfirm").appendTo($actions);
        const $btnCancel = $("<div>").attr("id", "upPickCancel").appendTo($actions);

        $search.dxTextBox({
            mode: "search",
            placeholder: t("reservationDetail.units.pickerSearchPlaceholder"),
            valueChangeEvent: "input",
            showClearButton: true,
            rtlEnabled: isArabic(),
            onValueChanged(ev) {
                filterUnitSearch = ev.value == null ? "" : `${ev.value}`;
                renderCards($grid, $countEl, $chipsEl);
            }
        });

        $gr.dxSelectBox({
            items: rtItems,
            valueExpr: "id",
            displayExpr: "text",
            value: "__ALL__",
            searchEnabled: true,
            showClearButton: false,
            rtlEnabled: isArabic(),
            placeholder: t("reservationDetail.units.filterRoomType"),
            dropDownOptions: {
                container: $("body"),
                wrapperAttr: { class: "unit-picker-filter-dropdown" }
            },
            onValueChanged(ev) {
                filterRoomType = ev.value === undefined || ev.value === null ? "__ALL__" : ev.value;
                renderCards($grid, $countEl, $chipsEl);
            }
        });

        $reset.dxButton({
            icon: "refresh",
            hint: t("reservationDetail.units.resetFilters"),
            stylingMode: "text",
            type: "normal",
            onClick() {
                filterRoomType = "__ALL__";
                filterUnitSearch = "";
                selectedIds.clear();
                customRatesByApartmentId.clear();
                $("#upFilterRt").dxSelectBox("instance").option("value", "__ALL__");
                $("#upFilterSearch").dxTextBox("instance").option("value", "");
                renderCards($grid, $countEl, $chipsEl);
            }
        });

        $close.dxButton({
            icon: "close",
            stylingMode: "text",
            type: "normal",
            hint: t("common.cancel"),
            onClick: closeUnitPickerDrawer
        });

        $btnOk.dxButton({
            text: t("reservationDetail.units.confirmPicker"),
            icon: "check",
            type: "default",
            stylingMode: "contained",
            onClick() {
                if (!selectedIds.size) {
                    DevExpress.ui.notify(t("reservationDetail.units.noneSelected"), "warning", 2400);
                    return;
                }

                const stayDates = getNewUnitStayDates();
                if (!stayDates) {
                    DevExpress.ui.notify(t("reservationDetail.units.needDatesForUnits"), "warning", 3200);
                    return;
                }

                const mapById = new Map(available.map((x) => [Number(x.apartmentId), x]));
                let nextUnits;
                const replacingSingle =
                    !bulkPick &&
                    isNewReservationForDateRules() &&
                    unitsOnRes.length === 1;

                if (bulkPick) {
                    nextUnits = [...unitsOnRes];
                    selectedIds.forEach((apid) => {
                        const apt = mapById.get(Number(apid));
                        if (!apt) {
                            return;
                        }
                        nextUnits.push(
                            buildPendingUnitRow(
                                apt,
                                stayDates.checkIn,
                                stayDates.checkOut,
                                resolvePickerGrossOverride(apid)
                            )
                        );
                    });
                } else {
                    const apid = [...selectedIds][0];
                    const apt = mapById.get(Number(apid));
                    if (!apt) {
                        return;
                    }
                    const row = buildPendingUnitRow(
                        apt,
                        stayDates.checkIn,
                        stayDates.checkOut,
                        resolvePickerGrossOverride(apid)
                    );
                    if (replacingSingle && unitsOnRes.length === 1) {
                        const prev = unitsOnRes[0] || {};
                        const prevUnitId = Number(prev.unitId);
                        if (Number.isFinite(prevUnitId) && prevUnitId > 0 && prev.isPendingUnit !== true) {
                            row.unitId = prevUnitId;
                            row.isPendingUnit = false;
                        }
                    }

                    nextUnits = replacingSingle ? [row] : [...unitsOnRes, row];
                }

                pageCtx.detail.units = nextUnits;
                pageCtx.useLocalFinancialTotals = nextUnits.length > 0;

                function refreshUnitsUiAfterPicker() {
                    ensurePricingRatesForAllLines();
                    const ug = $("#unitsGrid").dxDataGrid("instance");
                    if (ug) {
                        ug.option("dataSource", pageCtx.detail.units.slice());
                        ug.refresh();
                    }
                    refreshCompanionsGrid();
                    syncFinancialUi();
                }

                function notifyUnitsPickerDone(persisted) {
                    let msg;
                    if (replacingSingle) {
                        msg = t("reservationDetail.units.replacedLocal");
                    } else if (persisted) {
                        msg = t("reservationDetail.units.addedPersisted");
                    } else {
                        msg = t("reservationDetail.units.addedLocal");
                    }

                    DevExpress.ui.notify(msg, "success", 2600);
                    closeUnitPickerDrawer();
                }

                applySuggestedGrossRatesFromPickerToUnits({ refreshPricing: true, clearPricingMap: false })
                    .then(() => {
                        refreshUnitsUiAfterPicker();
                        notifyUnitsPickerDone(false);
                    })
                    .catch(() => {
                        refreshUnitsUiAfterPicker();
                        notifyUnitsPickerDone(false);
                    });
            }
        });

        $btnCancel.dxButton({
            text: t("reservationDetail.units.cancelPicker"),
            icon: "close",
            stylingMode: "outlined",
            type: "normal",
            onClick() {
                closeUnitPickerDrawer();
            }
        });

        renderCards($grid, $countEl, $chipsEl);
    }

    function getRouteId() {
        const params = new URLSearchParams(window.location.search);
        const raw = params.get("id") || params.get("reservationId");
        if (!raw) {
            return null;
        }

        const n = parseInt(raw, 10);
        return Number.isFinite(n) ? n : null;
    }

    /**
     * URL query `id` and API route should use Zaaer integration id when set; otherwise internal reservation PK.
     */
    function preferZaaerRouteKey(detail) {
        if (!detail || typeof detail !== "object") {
            return null;
        }

        const z = detail.zaaerId != null ? Number(detail.zaaerId) : NaN;
        if (Number.isFinite(z) && z > 0) {
            return z;
        }

        const r = detail.reservationId != null ? Number(detail.reservationId) : NaN;
        return Number.isFinite(r) && r > 0 ? r : null;
    }

    /** Route key for PMS payment/invoice APIs: URL <c>id</c> is Zaaer integration id when present. */
    function resolvePaymentReservationRouteId(detail) {
        const rk = preferZaaerRouteKey(detail);
        if (rk != null) {
            return rk;
        }

        if (pageCtx.routeId != null && pageCtx.routeId !== "") {
            return pageCtx.routeId;
        }

        return null;
    }

    function syncUrlReservationIdParam(desiredId) {
        if (desiredId == null || desiredId === "") {
            return;
        }

        const u = new URL(window.location.href);
        const cur = u.searchParams.get("id");
        if (cur !== null && String(cur) === String(desiredId)) {
            return;
        }

        u.searchParams.set("id", String(desiredId));
        window.history.replaceState({}, "", u.toString());
    }

    function applyReservationRouteFromDetail(detail, options) {
        const opts = options || {};
        const rk = preferZaaerRouteKey(detail);
        if (rk != null) {
            pageCtx.routeId = rk;
        }

        if (!opts.skipUrlSync && rk != null) {
            syncUrlReservationIdParam(rk);
        }
    }

    function getNewReservationFromQuery() {
        const params = new URLSearchParams(window.location.search);
        const v = params.get("newReservation");
        return v === "1" || `${v}`.toLowerCase() === "true";
    }

    const NEW_RESERVATION_CTX_KEY = "zaaer.pms.newReservationContext";

    function consumeNewReservationContext() {
        const raw = window.sessionStorage.getItem(NEW_RESERVATION_CTX_KEY);
        if (!raw) {
            return null;
        }

        try {
            const o = JSON.parse(raw);
            window.sessionStorage.removeItem(NEW_RESERVATION_CTX_KEY);
            return o && typeof o === "object" ? o : null;
        } catch {
            window.sessionStorage.removeItem(NEW_RESERVATION_CTX_KEY);
            return null;
        }
    }

    function getQueryText(params, key) {
        const v = params.get(key);
        return v === undefined || v === null || v === "" ? null : v;
    }

    function ctxOrQueryString(params, ctx, key) {
        const q = getQueryText(params, key);
        if (q !== null) {
            return q;
        }

        if (!ctx || ctx[key] === undefined || ctx[key] === null) {
            return null;
        }

        const s = `${ctx[key]}`.trim();
        return s === "" ? null : s;
    }

    function ctxOrQueryNumber(params, ctx, key) {
        const raw = ctxOrQueryString(params, ctx, key);
        if (raw === null) {
            return null;
        }

        const n = Number(raw);
        return Number.isFinite(n) ? n : null;
    }

    function buildClientNewReservationDetail(params, ctx, ksaNow) {
        const ksaClock =
            ksaNow instanceof Date && !Number.isNaN(ksaNow.getTime())
                ? ksaNow
                : window.Zaaer.KsaTime && typeof window.Zaaer.KsaTime.nowFromIntl === "function"
                  ? window.Zaaer.KsaTime.nowFromIntl()
                  : new Date();
        const checkInDate = ksaClock;
        const checkOutDate = defaultCheckOutFromCheckInAndNights(checkInDate, 1);

        /** Same idea as Zaaer PMS `/reservations/create?unit=…` — optional `unit` preselects apartment by Zaaer id when no explicit apartment* query/context. */
        let ctxEffective = ctx && typeof ctx === "object" ? { ...ctx } : {};
        const unitQ = getQueryText(params, "unit");
        const hasApartmentQuery =
            getQueryText(params, "apartmentId") !== null || getQueryText(params, "apartmentZaaerId") !== null;
        if (
            unitQ !== null &&
            !hasApartmentQuery &&
            ctxEffective.apartmentId == null &&
            ctxEffective.apartmentZaaerId == null
        ) {
            const n = Number(unitQ);
            if (Number.isFinite(n)) {
                ctxEffective.apartmentZaaerId = n;
            }
        }

        const apartmentId = ctxOrQueryNumber(params, ctxEffective, "apartmentId");
        const apartmentZaaerId = ctxOrQueryNumber(params, ctxEffective, "apartmentZaaerId");
        const apartmentCode = ctxOrQueryString(params, ctx, "apartmentCode");
        const hotelIdCandidate = ctxOrQueryNumber(params, ctxEffective, "hotelId");
        const hotelId = hotelIdCandidate != null && hotelIdCandidate > 0 ? hotelIdCandidate : null;
        const hotelCodeFromCtx = ctxOrQueryString(params, ctxEffective, "hotelCode");

        return {
            reservationId: null,
            zaaerId: null,
            hotelId,
            hotelCode: hotelCodeFromCtx != null && `${hotelCodeFromCtx}`.trim() !== "" ? `${hotelCodeFromCtx}`.trim() : undefined,
            customerId: null,
            corporateId: null,
            header: {
                reservationNo: t("reservationDetail.newLocalReservation"),
                source: null,
                mainGuestName: null,
                actualArrival: null,
                status: "checked_in"
            },
            general: {
                reservationType: "individual",
                visitPurposeId: null,
                source: null,
                cmBookingNo: null
            },
            dates: {
                rentalType: "Daily",
                checkInDate: checkInDate,
                checkOutDate: checkOutDate,
                departureDate: null,
                numberOfMonths: null,
                totalNights: 1,
                monthlyCalendarMode: "ThirtyDay",
                isAutoExtend: true,
                reservationDate: ksaClock
            },
            units:
                apartmentId != null || apartmentZaaerId != null
                    ? [
                          buildPendingUnitRow(
                              {
                                  apartmentId,
                                  zaaerId: apartmentZaaerId,
                                  apartmentCode,
                                  roomTypeName: ctxOrQueryString(params, ctxEffective, "roomTypeName"),
                                  buildingName: ctxOrQueryString(params, ctxEffective, "buildingName"),
                                  floorName: ctxOrQueryString(params, ctxEffective, "floorName")
                              },
                              checkInDate,
                              checkOutDate,
                              undefined
                          )
                      ]
                    : [],
            company: null,
            guests: [],
            financial: {
                balanceAmount: 0,
                totalAmount: 0,
                amountPaid: 0,
                subtotal: 0,
                totalTaxAmount: 0
            },
            companions: [],
            extras: []
        };
    }

    function setReservationHeaderVisible(visible) {
        const show = !!visible;
        const showLodgingPartyOnly = pageCtx.isLodgingProperty && pageCtx.isLocalNewReservation && !show;
        $("#res-section-header").toggle(show || showLodgingPartyOnly);
        $("#res-section-header .res-kv-grid").toggle(show);
        $("#res-section-header .res-section-head").toggle(show);
        if (showLodgingPartyOnly) {
            syncLodgingPartyCards();
        }
    }

    function kv(label, value) {
        return $("<div>")
            .addClass("res-kv")
            .append($("<div>").addClass("res-k").text(label))
            .append($("<div>").addClass("res-v").text(value));
    }

    function kvSpan(label, spanId) {
        return $("<div>")
            .addClass("res-kv")
            .append($("<div>").addClass("res-k").text(label))
            .append($("<div>").addClass("res-v").append($("<span>").attr("id", spanId)));
    }

    function syncReservationHeaderKvCards(detail) {
        const header = (detail && detail.header) || {};
        const dates = (detail && detail.dates) || {};
        const statusNorm = normalizeHeaderReservationStatus(
            header.status || header.reservationStatus
        );

        $("#resHeaderArrival").text(formatDateTimeEn(dates.reservationDate));

        const showDeparture = statusNorm === "checked_out";
        const $depWrap = $("#resKvDepartureWrap");
        if ($depWrap.length) {
            $depWrap.toggle(showDeparture);
            if (showDeparture) {
                $("#resHeaderDeparture").text(formatDateTimeEn(dates.departureDate));
            } else {
                $("#resHeaderDeparture").text("—");
            }
        }
    }

    function normRental(v) {
        const x = `${v || ""}`.toLowerCase();
        if (x.includes("month")) {
            return "Monthly";
        }

        return "Daily";
    }

    function normalizeReservationGeneralStatus(value) {
        const x = `${value || ""}`.trim().toLowerCase().replace(/[\s_-]+/g, "");
        if (x === "unconfirmed") {
            return "unconfirmed";
        }

        return "confirmed";
    }

    /** Maps DB/header status to guest arrival (controls saved reservation status on save). */
    function guestArrivalValueFromHeaderStatus(status) {
        const x = `${status || ""}`.trim().toLowerCase().replace(/[\s_-]+/g, "");
        if (x === "checkin" || x === "checkedin") {
            return "arrived";
        }

        return "not_arrived";
    }

    function isGuestArrivedSwitchOn() {
        const inst = $("#resGeneralArrival").dxSwitch("instance");
        return inst ? !!inst.option("value") : true;
    }

    function syncGuestArrivalSwitchLabel() {
        const $label = $("#resGuestArrivalLabel");
        if (!$label.length) {
            return;
        }

        $label.text(
            isGuestArrivedSwitchOn()
                ? t("reservationDetail.guestArrival.arrivedAtHotel")
                : t("reservationDetail.guestArrival.notArrivedYet")
        );
    }

    function applyGuestArrivalSwitchSideEffects(isArrived) {
        const statusInst = $("#resGeneralStatus").dxSelectBox("instance");
        if (!statusInst) {
            return;
        }

        statusInst.option("disabled", isArrived);
        if (isArrived) {
            statusInst.option("value", "confirmed");
        }
    }

    function normalizeHeaderReservationStatus(status) {
        const x = `${status || ""}`.trim().toLowerCase().replace(/[\s_-]+/g, "");
        if (x === "checkin" || x === "checkedin") {
            return "checked_in";
        }

        if (x === "checkedout" || x === "checkout") {
            return "checked_out";
        }

        if (x === "cancelled" || x === "canceled") {
            return "cancelled";
        }

        if (x === "unconfirmed") {
            return "unconfirmed";
        }

        return "confirmed";
    }

    function isCancelledReservation() {
        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        return (
            normalizeHeaderReservationStatus(header.status || header.reservationStatus) === "cancelled"
        );
    }

    function isArrivalSwitchLockedByStayState() {
        if (pageCtx.arrivalSwitchLockedFromUndo) {
            return true;
        }

        return (
            isCheckedInReservation() ||
            isCheckedOutReservation() ||
            isCancelledReservation()
        );
    }

    function syncArrivalSwitchFromReservationDetail() {
        const header = (pageCtx.detail && pageCtx.detail.header) || {};
        const statusNorm = normalizeHeaderReservationStatus(header.status || header.reservationStatus);
        let isArrived = guestArrivalValueFromHeaderStatus(statusNorm) === "arrived";
        if (pageCtx.isLocalNewReservation) {
            isArrived = true;
        }

        const arrivalSwitch = $("#resGeneralArrival").dxSwitch("instance");
        if (!arrivalSwitch) {
            return;
        }

        pageCtx._suppressArrivalSwitchEvent = true;
        try {
            arrivalSwitch.option("value", isArrived);
            arrivalSwitch.option("disabled", isArrivalSwitchLockedByStayState());
        } finally {
            pageCtx._suppressArrivalSwitchEvent = false;
        }

        syncGuestArrivalSwitchLabel();
        applyGuestArrivalSwitchSideEffects(isArrived);
    }

    function setReservationHeaderStatusLocal(status) {
        if (!pageCtx.detail) {
            pageCtx.detail = {};
        }

        if (!pageCtx.detail.header || typeof pageCtx.detail.header !== "object") {
            pageCtx.detail.header = {};
        }

        pageCtx.detail.header.status = status;
        pageCtx.detail.header.reservationStatus = status;
    }

    function canCheckInReservation() {
        return (
            hasPmsPermission("reservations.check_in") ||
            hasPmsPermission("reservations.update")
        );
    }

    function canCancelReservationFromOtherOptions() {
        return hasPmsPermission("reservations.cancel");
    }

    function canUndoCheckInFromOtherOptions() {
        return hasPmsPermission("reservations.undo_check_in");
    }

    function canLateCheckOutReservation() {
        return hasPmsPermission("reservations.late_check_out");
    }

    function ksaTodayAtMidnight() {
        const now = new Date();
        return new Date(now.getFullYear(), now.getMonth(), now.getDate());
    }

    function plannedCheckOutAtMidnight() {
        const dates = pageCtx.detail && pageCtx.detail.dates;
        const raw = dates && dates.checkOutDate;
        if (!raw) {
            return null;
        }

        const d = raw instanceof Date ? raw : new Date(raw);
        if (Number.isNaN(d.getTime())) {
            return null;
        }

        return new Date(d.getFullYear(), d.getMonth(), d.getDate());
    }

    /** Planned departure calendar day is strictly before today (browser local ≈ KSA for on-site PMS). */
    function isPlannedDepartureBeforeKsaToday() {
        const planned = plannedCheckOutAtMidnight();
        if (!planned) {
            return false;
        }

        return planned.getTime() < ksaTodayAtMidnight().getTime();
    }

    /**
     * Block checkout wizard when departure is overdue unless reservations.late_check_out is granted.
     */
    function ensureCheckoutDepartureAllowed() {
        if (!isPlannedDepartureBeforeKsaToday()) {
            return true;
        }

        if (canLateCheckOutReservation()) {
            return true;
        }

        DevExpress.ui.notify(t("reservationDetail.checkoutErrLateDeparture"), "warning", 4200);
        return false;
    }

    function reservationOtherOptionsItems() {
        if (!pageCtx.routeId || pageCtx.isLocalNewReservation) {
            return [];
        }

        const items = [];

        if (isCheckedInReservation() && !isCheckedOutReservation()) {
            if (canCheckoutReservation()) {
                items.push({
                    id: "checkout",
                    text: hallCheckoutOtherOptionText(),
                    icon: "runner"
                });
            }

            if (canUndoCheckInFromOtherOptions()) {
                items.push({
                    id: "undo_checkin",
                    text: t("reservationDetail.otherOptions.undoCheckIn"),
                    icon: "undo"
                });
            }
        } else if (!isCheckedOutReservation() && !isCancelledReservation()) {
            if (canCancelReservationFromOtherOptions()) {
                items.push({
                    id: "cancel_bookings",
                    text: t("reservationDetail.otherOptions.cancelBookings"),
                    icon: "clear"
                });
            }
        }

        if (hasPmsPermission("resort_tickets.view")) {
            items.push({
                id: "resort_tickets",
                text: t("reservationDetail.tickets.title"),
                icon: "print"
            });
        }

        items.push(
            {
                id: "booking_summary",
                text: t("reservationDetail.otherOptions.bookingSummary"),
                icon: "chart"
            },
            {
                id: "booking_details",
                text: t("reservationDetail.otherOptions.bookingDetails"),
                icon: "detailslayout"
            },
            {
                id: "housing_contract",
                text: t("reservationDetail.otherOptions.housingContract"),
                icon: "doc"
            },
            {
                id: "activity_log",
                text: t("reservationDetail.otherOptions.activityLog"),
                icon: "event"
            }
        );

        return items;
    }

    function refreshReservationOtherOptionsMenu() {
        const $host = $("#reservationOtherOptions");
        if (!$host.length) {
            return;
        }

        try {
            const btn = $host.dxDropDownButton("instance");
            if (!btn) {
                return;
            }

            const items = reservationOtherOptionsItems();
            btn.option("items", items);
            btn.option("visible", items.length > 0);
        } catch {
            /* not initialized */
        }
    }

    function patchReservationStatusOnly(reservationStatus) {
        if (!pageCtx.routeId) {
            return Promise.reject(new Error("missing route"));
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.show();
        }

        return window.Zaaer.ReservationDetailService.patchReservation(
            pageCtx.routeId,
            { reservationStatus: reservationStatus },
            pageCtx.hotelIdParam
        )
            .then((detail) => {
                const statusNorm = normalizeHeaderReservationStatus(
                    (detail && detail.header && (detail.header.status || detail.header.reservationStatus)) ||
                        reservationStatus
                );
                pageCtx.arrivalSwitchLockedFromUndo = statusNorm === "confirmed";

                if (detailPatchUsable(detail)) {
                    applyPostMutationReservationDetail(detail);
                } else {
                    setReservationHeaderStatusLocal(reservationStatus);
                    syncArrivalSwitchFromReservationDetail();
                    initFooter();
                    refreshReservationOtherOptionsMenu();
                }

                DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2200);
                return detail;
            })
            .catch((err) => {
                const status = err && (err.status || err.statusCode);
                if (status === 403) {
                    const permCode =
                        err &&
                        err.responseJSON &&
                        (err.responseJSON.permissionCode || err.responseJSON.PermissionCode);
                    const msg = permCode
                        ? `${t("common.forbidden")} (${permCode})`
                        : t("common.forbidden");
                    DevExpress.ui.notify(msg, "warning", 4200);
                } else {
                    DevExpress.ui.notify(
                        err && err.message ? String(err.message) : t("error.saveReservationDetail"),
                        "error",
                        4200
                    );
                }

                throw err;
            })
            .finally(() => {
                if (lp) {
                    lp.hide();
                }
            });
    }

    function runUndoCheckInFromOtherOptions() {
        if (!canUndoCheckInFromOtherOptions()) {
            notifyForbidden();
            return;
        }

        DevExpress.ui.dialog
            .confirm(
                t("reservationDetail.otherOptions.confirmUndoCheckIn"),
                t("reservationDetail.otherOptions.undoCheckIn")
            )
            .done((yes) => {
                if (!yes) {
                    return;
                }

                pageCtx.arrivalSwitchLockedFromUndo = true;
                patchReservationStatusOnly("confirmed");
            });
    }

    function runCheckInFromFooter() {
        if (!canCheckInReservation()) {
            notifyForbidden();
            return;
        }

        if (!ensureReservationCompleteForOperations()) {
            return;
        }

        DevExpress.ui.dialog
            .confirm(
                t("reservationDetail.otherOptions.confirmCheckIn"),
                t("reservationDetail.actions.checkIn")
            )
            .done((yes) => {
                if (!yes) {
                    return;
                }

                pageCtx.arrivalSwitchLockedFromUndo = false;
                patchReservationStatusOnly("checked_in");
            });
    }

    function fetchPropertyMode() {
        const ps = window.Zaaer && window.Zaaer.PropertySettingsService;
        if (!ps || typeof ps.getMode !== "function") {
            return Promise.resolve(null);
        }
        return ps.getMode().catch(() => null);
    }

    function applyPropertyMode(mode) {
        pageCtx.propertyMode = mode;
        pageCtx.isHallProperty = !!(mode && (mode.isHall || mode.propertyType === "hall"));
        pageCtx.isLodgingProperty = !pageCtx.isHallProperty;
        const propertyType = mode && String(mode.propertyType || mode.PropertyType || "").toLowerCase();
        pageCtx.isHotelProperty =
            propertyType === "hotel" ||
            !!(mode && mode.isHotel && !pageCtx.isHallProperty && !mode.isResort && propertyType !== "resort");
        updateExtrasSectionVisibility();
    }

    function resolveRoomBoardUrl() {
        const fromQuery = new URLSearchParams(window.location.search).get("hotelCode");
        const fromDetail =
            pageCtx.detail && pageCtx.detail.hotelCode != null
                ? String(pageCtx.detail.hotelCode).trim()
                : "";
        const fromApi =
            window.Zaaer &&
            window.Zaaer.ApiService &&
            typeof window.Zaaer.ApiService.getHotelCode === "function"
                ? String(window.Zaaer.ApiService.getHotelCode() || "").trim()
                : "";
        const hotelCode = fromQuery || fromDetail || fromApi;
        if (pageCtx.isHallProperty) {
            return hotelCode
                ? `/hall-operations.html?hotelCode=${encodeURIComponent(hotelCode)}`
                : "/hall-operations.html";
        }
        if (hotelCode) {
            return `/room-board.html?hotelCode=${encodeURIComponent(hotelCode)}`;
        }
        return "/room-board.html";
    }

    function loadHallEventContextIfNeeded() {
        if (!pageCtx.isHallProperty || !pageCtx.routeId) {
            return Promise.resolve();
        }
        const hallSvc = window.Zaaer && window.Zaaer.HallEventsService;
        if (!hallSvc || typeof hallSvc.getEvent !== "function") {
            return Promise.resolve();
        }
        return hallSvc
            .getEvent(pageCtx.routeId)
            .then((ev) => {
                pageCtx.hallEvent = ev || null;
            })
            .catch(() => {
                pageCtx.hallEvent = null;
            });
    }

    function formatHallSummaryDateEn(value) {
        const d = parseDateOrNull(value);
        if (!d) {
            return "";
        }

        const day = String(d.getDate()).padStart(2, "0");
        const month = String(d.getMonth() + 1).padStart(2, "0");
        return `${day}/${month}/${d.getFullYear()}`;
    }

    function formatHallSummaryTimeEn(value) {
        if (value == null || value === "") {
            return "";
        }

        const s = String(value).trim();
        const m = /^(\d{1,2}):(\d{2})/.exec(s);
        if (m) {
            return `${String(m[1]).padStart(2, "0")}:${m[2]}`;
        }

        return s;
    }

    function renderHallEventSummary() {
        const ev = pageCtx.hallEvent;
        if (!ev || !pageCtx.isHallProperty) {
            $("#res-section-header .hall-event-summary").remove();
            return;
        }
        const $host = $("#res-section-header .res-section-body");
        if (!$host.length) {
            return;
        }
        $host.find(".hall-event-summary").remove();
        const gregDate = formatHallSummaryDateEn(ev.eventDate);
        const start = formatHallSummaryTimeEn(ev.eventStartTime);
        const end = formatHallSummaryTimeEn(ev.eventEndTime);
        const timeRange = start && end ? `${start} — ${end}` : start || end || "";
        const meta = [gregDate, timeRange, ev.hallName].filter(Boolean).join(" · ");
        $("<div class='hall-event-summary'/>")
            .append($("<strong/>").text(ev.occasionName || ev.reservationNo || ""))
            .append(
                $("<span/>")
                    .addClass("hall-event-summary__meta")
                    .attr("dir", "ltr")
                    .text(meta)
            )
            .prependTo($host);
    }

    function canEditHallRent() {
        if (!pageCtx.isHallProperty || !pageCtx.routeId) {
            return false;
        }

        if (pageCtx.isClientNewReservation || pageCtx.checkoutUiPendingFirstSave) {
            return false;
        }

        if (reservationGridsActionsDisabled()) {
            return false;
        }

        return (
            hasPmsPermission("hall.events.manage")
            || canApplyUnitPricingInContext()
            || canSaveReservation()
        );
    }

    function hallCheckoutActionText() {
        return pageCtx.isHallProperty
            ? t("reservationDetail.hall.endEvent")
            : t("reservationDetail.actions.checkout");
    }

    function hallCheckoutOtherOptionText() {
        return pageCtx.isHallProperty
            ? t("reservationDetail.hall.endEvent")
            : t("reservationDetail.otherOptions.checkout");
    }

    function resolveHallRentGrossAmount() {
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        if (units.length) {
            const u = units[0];
            const gross = Number(u.totalAmount != null ? u.totalAmount : u.rentAmount) || 0;
            if (gross > 0) {
                return roundMoney(gross);
            }
        }

        const fin = (pageCtx.detail && pageCtx.detail.financial) || {};
        const extras = sumLocalExtrasTotalFromPage();
        return roundMoney(Math.max(0, (Number(fin.totalAmount) || 0) - extras));
    }

    function openHallRentEditPopup() {
        if (!canEditHallRent()) {
            notifyForbidden();
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || typeof svc.updateHallRent !== "function") {
            DevExpress.ui.notify(t("common.error"), "error", 3200);
            return;
        }

        const reservationId = preferZaaerRouteKey(pageCtx.detail) ?? pageCtx.routeId;
        const hotelId = pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId);
        const currentRent = resolveHallRentGrossAmount();
        const $host = $("<div class='res-hall-rent-popup'/>").appendTo("body");
        let formInst = null;

        $host.dxPopup({
            width: Math.min(480, Math.max(340, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            title: t("reservationDetail.hall.editHallRentTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            dragEnabled: false,
            rtlEnabled: isArabic(),
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-hall-rent-popup-wrap" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty();
                $("<p class='res-hall-rent-popup__hint'/>")
                    .text(t("reservationDetail.hall.editHallRentHint"))
                    .appendTo($content);
                const $form = $("<div/>").appendTo($content);
                $form.dxForm({
                    formData: { hallRentAmount: currentRent },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        {
                            dataField: "hallRentAmount",
                            label: { text: t("reservationDetail.hall.editHallRentAmount") },
                            editorType: "dxNumberBox",
                            editorOptions: {
                                min: 0,
                                format: { type: "fixedPoint", precision: 2 },
                                showSpinButtons: true
                            },
                            validationRules: [{ type: "required" }, { type: "range", min: 0 }]
                        }
                    ]
                });
                formInst = $form.dxForm("instance");
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.actions.save"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!formInst) {
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation || !validation.isValid) {
                                return;
                            }

                            const amount = roundMoney(formInst.option("formData").hallRentAmount);
                            const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
                            if (lp) {
                                lp.show();
                            }

                            svc.updateHallRent(reservationId, amount, hotelId)
                                .then((detail) => {
                                    if (detailPatchUsable(detail)) {
                                        applyPostMutationReservationDetail(detail);
                                    }
                                    return loadHallEventContextIfNeeded().then(() => {
                                        renderHallEventSummary();
                                        DevExpress.ui.notify(
                                            t("reservationDetail.hall.editHallRentSaved"),
                                            "success",
                                            2400
                                        );
                                        $host.dxPopup("instance").hide();
                                    });
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        (err && err.message) || t("common.error"),
                                        "error",
                                        3600
                                    );
                                })
                                .finally(() => {
                                    if (lp) {
                                        lp.hide();
                                    }
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function syncHallUnitPricingButton() {
        const $btn = $("#btnUnitPricing");
        if (!$btn.length) {
            return;
        }

        if (!pageCtx.isHallProperty) {
            initUnitPricingButton();
            return;
        }

        let existing = null;
        try {
            existing = $btn.dxButton("instance");
        } catch {
            existing = null;
        }

        const canEdit = canEditHallRent();
        const canOpen = canEdit || canViewUnitPricing() || canSaveReservation();
        const opts = {
            text: t("reservationDetail.hall.editHallRent"),
            icon: "money",
            type: "default",
            stylingMode: "contained",
            visible: canOpen,
            disabled: !canOpen,
            onClick: openHallRentEditPopup
        };

        if (existing) {
            existing.option(opts);
            return;
        }

        $btn.dxButton(opts);
    }

    function getPrimaryGuestRow() {
        const guests = (pageCtx.detail && pageCtx.detail.guests) || [];
        return Array.isArray(guests) && guests.length ? guests[0] : null;
    }

    function relocateHallExtrasBlockForHall() {
        const $extras = $("#resGuestsExtrasRoot");
        if (!$extras.length) {
            return;
        }

        if (pageCtx.isHallProperty) {
            const $finBody = $("#res-section-financial .res-section-body");
            if ($finBody.length && !$extras.parent().is($finBody)) {
                $extras.detach().appendTo($finBody);
            }
            return;
        }

        const $companionsHost = $(".res-guests-companions");
        if ($companionsHost.length && !$companionsHost.find("#resGuestsExtrasRoot").length) {
            $extras.detach().appendTo($companionsHost);
        }
    }

    function syncHallMainGuestEditButton() {
        const $host = $("#resHeaderGuestEdit");
        if (!$host.length) {
            return;
        }

        if (!pageCtx.isHallProperty) {
            if ($host.data("dxButton")) {
                $host.dxButton("instance").option("visible", false);
            }
            return;
        }

        const row = getPrimaryGuestRow();
        const ro = reservationGridsActionsDisabled();
        const canAdd = pageCtx.isLocalNewReservation && canAddGuest() && !ro;
        const canEdit = row && row.customerId && canEditGuest() && !ro;
        const visible = canAdd || canEdit;
        const icon = canAdd && !canEdit ? "plus" : "edit";
        const hint = canAdd && !canEdit
            ? t("reservationDetail.guest.gridAddHint")
            : t("reservationDetail.guest.editTitle");

        if (!$host.data("dxButton")) {
            $host.dxButton({
                icon: icon,
                stylingMode: "text",
                type: "default",
                visible: visible,
                disabled: !visible,
                hint: hint,
                elementAttr: { class: "res-kv-main-guest-edit-btn" },
                onClick() {
                    const guestRow = getPrimaryGuestRow();
                    if (guestRow && guestRow.customerId) {
                        openGuestEdit(guestRow);
                        return;
                    }
                    openGuestPicker();
                }
            });
            return;
        }

        const btn = $host.dxButton("instance");
        btn.option({
            icon: icon,
            visible: visible,
            disabled: !visible,
            hint: hint
        });
    }

    function buildLodgingPartyCardShell(kind) {
        const labelKey =
            kind === "guest"
                ? "reservationDetail.lodging.partyGuest"
                : kind === "company"
                  ? "reservationDetail.lodging.partyCompany"
                  : "reservationDetail.lodging.partyCompanions";
        const idPrefix =
            kind === "guest" ? "Guest" : kind === "company" ? "Company" : "Companions";

        const $card = $("<article/>")
            .addClass(`res-lodging-party-card res-lodging-party-card--${kind}`)
            .attr("data-party-kind", kind);

        if (kind === "companions") {
            $card.attr("tabindex", "0").attr("role", "button");
        }

        if (kind === "companions") {
            const $inner = $("<div/>").addClass("res-lodging-party-card__companions-inner").appendTo($card);
            $("<span/>")
                .addClass("res-lodging-party-card__companions-icon dx-icon dx-icon-group")
                .attr("aria-hidden", "true")
                .appendTo($inner);

            const $text = $("<div/>").addClass("res-lodging-party-card__companions-text").appendTo($inner);
            $("<div/>").addClass("res-lodging-party-card__label").text(t(labelKey)).appendTo($text);
            $("<span/>")
                .addClass("res-lodging-party-card__value")
                .attr("id", `resLodgingParty${idPrefix}Value`)
                .appendTo($text);

            $("<span/>")
                .addClass("res-lodging-party-card__badge res-lodging-party-card__badge--empty")
                .attr("id", "resLodgingCompanionsBadge")
                .text("0")
                .appendTo($inner);
            return $card;
        }

        const $labelRow = $("<div/>").addClass("res-lodging-party-card__label-row").appendTo($card);
        $("<span/>")
            .addClass(
                `res-lodging-party-card__label-icon dx-icon dx-icon-${kind === "guest" ? "user" : "briefcase"}`
            )
            .attr("aria-hidden", "true")
            .appendTo($labelRow);
        $("<span/>").addClass("res-lodging-party-card__label").text(t(labelKey)).appendTo($labelRow);

        const $body = $("<div/>").addClass("res-lodging-party-card__body").appendTo($card);
        $("<span/>")
            .addClass("res-lodging-party-card__value")
            .attr("id", `resLodgingParty${idPrefix}Value`)
            .appendTo($body);

        $("<span/>")
            .addClass("res-lodging-party-card__edit")
            .attr("id", `resLodgingParty${idPrefix}Edit`)
            .appendTo($body);

        return $card;
    }

    function syncLodgingPartyCardEditButton($host, options) {
        if (!$host || !$host.length) {
            return;
        }

        const row = options.getRow ? options.getRow() : null;
        const ro = reservationGridsActionsDisabled();
        const canAdd = options.canAdd && options.canAdd() && !ro;
        const canEdit = options.canEdit && options.canEdit(row) && !ro;
        const visible = canAdd || canEdit;
        const icon = canAdd && !canEdit ? "plus" : "edit";
        const hint = options.hint || t("reservationDetail.actions.edit");

        if (!$host.data("dxButton")) {
            $host.dxButton({
                icon: icon,
                stylingMode: "text",
                type: "default",
                visible: visible,
                disabled: !visible,
                hint: hint,
                elementAttr: { class: "res-lodging-party-card__edit-btn" },
                onClick() {
                    if (typeof options.onClick === "function") {
                        options.onClick(row);
                    }
                }
            });
            return;
        }

        const btn = $host.dxButton("instance");
        btn.option({
            icon: icon,
            visible: visible,
            disabled: !visible,
            hint: hint,
            onClick() {
                if (typeof options.onClick === "function") {
                    options.onClick(options.getRow ? options.getRow() : null);
                }
            }
        });
    }

    function openLodgingCompanionsPopup() {
        if (reservationGridsActionsDisabled()) {
            return;
        }

        const $host = $("<div/>").appendTo("body");
        let gridInst = null;
        let popupInst = null;
        let $saveBtnHost = null;

        function syncCompanionPopupChrome() {
            const rows = pageCtx.companions || [];
            const hasRows = rows.length > 0;
            const readyToSave = companionsReadyForPersist(rows);

            if ($saveBtnHost && $saveBtnHost.length) {
                $saveBtnHost.toggle(hasRows);
                try {
                    const saveBtn = $saveBtnHost.dxButton("instance");
                    if (saveBtn) {
                        saveBtn.option(
                            "disabled",
                            !readyToSave || reservationGridsActionsDisabled() || !canPersistCompanions()
                        );
                    }
                } catch (_) {
                    /* button may not be initialized yet */
                }
            }
        }

        function refreshCompanionPopupGrid() {
            if (!gridInst) {
                return;
            }

            gridInst.option("editing.allowUpdating", canEditCompanionAssignment());
            gridInst.option("dataSource", (pageCtx.companions || []).slice());
            syncCompanionPopupChrome();
        }

        function addCompanionFromPicker() {
            openGuestPicker({
                onPick(customerId, rowData) {
                    pageCtx.companions = pageCtx.companions || [];
                    pageCtx.companions.push(buildCompanionSlotFromCustomer(customerId, rowData));
                    refreshCompanionsGrid();
                    refreshCompanionPopupGrid();
                    DevExpress.ui.notify(t("reservationDetail.companion.addedOk"), "success", 2200);
                }
            });
        }

        $host.dxPopup({
            width: Math.min(960, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "72vh",
            title: t("reservationDetail.lodging.companionsPopupTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: dropdownAwarePopupHideOnOutsideClick,
            dragEnabled: false,
            rtlEnabled: isArabic(),
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup res-lodging-companions-popup-wrap" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty().addClass("res-lodging-companions-popup");

                const $head = $("<div/>").addClass("res-lodging-companions-popup__head").appendTo($content);
                $("<div/>")
                    .appendTo($head)
                    .dxButton({
                        text: t("reservationDetail.guest.addCompanion"),
                        icon: "plus",
                        type: "default",
                        stylingMode: "contained",
                        disabled: reservationGridsActionsDisabled() || !canAddGuest(),
                        onClick: addCompanionFromPicker
                    });

                const $gridHost = $("<div/>")
                    .addClass("res-lodging-companions-popup__grid pms-grid-compact")
                    .appendTo($content);

                const $foot = $("<div/>").addClass("res-lodging-companions-popup__foot").appendTo($content);
                $saveBtnHost = $("<div/>").addClass("res-lodging-companions-popup__save-host").appendTo($foot);
                $saveBtnHost.hide();
                $saveBtnHost.dxButton({
                    text: t("reservationDetail.companion.addAndSave"),
                    icon: "save",
                    type: "default",
                    stylingMode: "contained",
                    disabled: reservationGridsActionsDisabled() || !canPersistCompanions(),
                    onClick() {
                        persistReservationCompanions({ silent: false }).catch(function () {
                            /* notify handled in persistReservationCompanions */
                        });
                    }
                });

                $("<div/>")
                    .appendTo($foot)
                    .dxButton({
                        text: t("common.close"),
                        type: "normal",
                        stylingMode: "text",
                        onClick() {
                            if (popupInst) {
                                popupInst.hide();
                            }
                        }
                    });

                $gridHost.dxDataGrid(
                    buildCompanionsDataGridOptions({
                        height: Math.min(300, Math.max(160, window.innerHeight * 0.36)),
                        searchPanel: { visible: true, width: 260 },
                        onCellValueChanged(e) {
                            handleCompanionGridCellValueChanged(e);
                            syncCompanionPopupChrome();
                        }
                    })
                );
                gridInst = $gridHost.dxDataGrid("instance");
                refreshCompanionPopupGrid();
            },
            onShown() {
                popupInst = $host.dxPopup("instance");
                syncCompanionPopupChrome();
            },
            onHidden() {
                pageCtx._syncCompanionsPopupFooter = null;
                refreshCompanionsGrid();
                syncLodgingPartyCards();
                $host.remove();
            }
        });

        pageCtx._syncCompanionsPopupFooter = syncCompanionPopupChrome;
    }

    function syncLodgingPartyCards() {
        const $cards = $("#resLodgingPartyCards");
        if (!$cards.length) {
            return;
        }

        if (!pageCtx.isLodgingProperty) {
            $cards.hide();
            return;
        }

        $cards.show();

        const guestRow = getPrimaryGuestRow();
        const guestName =
            (guestRow && guestRow.customerName) ||
            (pageCtx.detail && pageCtx.detail.header && pageCtx.detail.header.mainGuestName) ||
            "";
        const $guestValue = $("#resLodgingPartyGuestValue");
        $guestValue
            .text(guestName || t("reservationDetail.lodging.noGuest"))
            .toggleClass("res-lodging-party-card__value--empty", !guestName);

        syncLodgingPartyCardEditButton($("#resLodgingPartyGuestEdit"), {
            getRow: getPrimaryGuestRow,
            canAdd: () => pageCtx.isLocalNewReservation && canAddGuest(),
            canEdit: (row) => !!(row && row.customerId && canEditGuest()),
            hint: t("reservationDetail.guest.editTitle"),
            onClick(row) {
                if (row && row.customerId) {
                    openGuestEdit(row);
                    return;
                }
                openGuestPicker();
            }
        });

        const showCompany = isCompanyReservationKind(pageCtx.detail);
        $(".res-lodging-party-card--company").toggleClass("res-lodging-party-card--hidden", !showCompany);

        const company = pageCtx.detail && pageCtx.detail.company;
        const companyName = company && company.corporateName ? `${company.corporateName}`.trim() : "";
        const $companyValue = $("#resLodgingPartyCompanyValue");
        $companyValue
            .text(companyName || t("reservationDetail.lodging.noCompany"))
            .toggleClass("res-lodging-party-card__value--empty", !companyName);

        syncLodgingPartyCardEditButton($("#resLodgingPartyCompanyEdit"), {
            getRow: () => company,
            canAdd: () => canAddCompany(),
            canEdit: (row) => !!(row && (row.corporateId || row.corporateName) && canAddCompany()),
            hint: companyName
                ? t("reservationDetail.company.editTitle")
                : t("reservationDetail.company.selectTitle"),
            onClick(row) {
                if (row && (row.corporateId || row.corporateName)) {
                    openCorporateEdit();
                    return;
                }
                openCorporatePicker();
            }
        });

        const companionCount = (pageCtx.companions || []).length;
        const $companionsCard = $(".res-lodging-party-card--companions");
        const $badge = $("#resLodgingCompanionsBadge");
        const $companionsValue = $("#resLodgingPartyCompanionsValue");
        $badge
            .text(companionCount > 99 ? "99+" : String(companionCount))
            .toggleClass("res-lodging-party-card__badge--empty", companionCount === 0);
        $companionsCard.toggleClass("has-companions", companionCount > 0);
        $companionsValue.text(
            companionCount > 0
                ? t("reservationDetail.lodging.companionsCount").replace("{0}", String(companionCount))
                : t("reservationDetail.lodging.noCompanions")
        ).toggleClass("res-lodging-party-card__value--empty", companionCount === 0);

        if (!$companionsCard.data("lodgingPartyBound")) {
            $companionsCard.data("lodgingPartyBound", true);
            $companionsCard.on("click keydown", function (evt) {
                if (evt.type === "keydown" && evt.key !== "Enter" && evt.key !== " ") {
                    return;
                }

                if ($(evt.target).closest(".dx-button").length) {
                    return;
                }

                evt.preventDefault();
                openLodgingCompanionsPopup();
            });
        }
    }

    function applyLodgingReservationUi() {
        if (!pageCtx.isLodgingProperty) {
            document.body.classList.remove("lodging-reservation-detail");
            syncLodgingPartyCards();
            return;
        }

        document.body.classList.add("lodging-reservation-detail");
        syncLodgingPartyCards();
    }

    function applyHallReservationUi() {
        const backBtn = $("#backToBoard").dxButton("instance");
        if (!pageCtx.isHallProperty) {
            document.body.classList.remove("hall-reservation-detail");
            relocateHallExtrasBlockForHall();
            syncHallMainGuestEditButton();
            syncHallUnitPricingButton();
            applyLodgingReservationUi();
            if (backBtn) {
                backBtn.option({
                    text: t("reservationDetail.backToBoard"),
                    onClick() {
                        window.location.href = resolveRoomBoardUrl();
                    }
                });
            }
            return;
        }
        document.body.classList.add("hall-reservation-detail");
        document.body.classList.remove("lodging-reservation-detail");
        if (backBtn) {
            backBtn.option({
                text: t("reservationDetail.backToHallOps"),
                onClick() {
                    window.location.href = resolveRoomBoardUrl();
                }
            });
        }
        renderHallEventSummary();
        syncHallHijriFromGregorianCheckIn();
        relocateHallExtrasBlockForHall();
        updateExtrasSectionVisibility();
        syncHallMainGuestEditButton();
        syncHallUnitPricingButton();
    }

    function initHallEventHijriEditorIfNeeded() {
        const $host = $("#resEventDateHijri");
        if (!$host.length || !pageCtx.isHallProperty) {
            pageCtx.hallEventHijriEditor = null;
            return;
        }

        const hijri = window.Zaaer && window.Zaaer.PmsHijriDate;
        if (!hijri || typeof hijri.attachEventDateTextBox !== "function") {
            return;
        }

        if ($host.data("dxTextBox")) {
            return;
        }

        pageCtx.hallEventHijriEditor = hijri.attachEventDateTextBox($host, {
            t,
            label: t("reservationDetail.eventDateHijri"),
            onGregorianChange(greg) {
                if (suppressHallHijriSync || !pageCtx.isHallProperty || !greg) {
                    return;
                }

                suppressHallHijriSync = true;
                suppressDateDurationSync = true;
                try {
                    const ci = getReservationCheckInCombined();
                    const co = getReservationCheckOutCombined();
                    const startH = ci ? ci.getHours() : 18;
                    const startM = ci ? ci.getMinutes() : 0;
                    const endH = co ? co.getHours() : 23;
                    const endM = co ? co.getMinutes() : 0;
                    const newCi = new Date(greg.getTime());
                    newCi.setHours(startH, startM, 0, 0);
                    const newCo = new Date(greg.getTime());
                    newCo.setHours(endH, endM, 0, 0);
                    setReservationCheckInFromDateTime(newCi);
                    setReservationCheckOutFromDateTime(newCo);
                    syncDurationFieldsFromDates({ flash: true });
                } finally {
                    suppressDateDurationSync = false;
                    suppressHallHijriSync = false;
                }
            }
        });
    }

    function syncHallHijriFromGregorianCheckIn() {
        if (!pageCtx.isHallProperty || suppressHallHijriSync || !pageCtx.hallEventHijriEditor) {
            return;
        }

        const ev = pageCtx.hallEvent;
        if (ev && (ev.eventDateHijri || ev.eventDateHijriDisplay)) {
            const stored = ev.eventDateHijri || ev.eventDateHijriDisplay;
            const hijri = window.Zaaer && window.Zaaer.PmsHijriDate;
            if (hijri && typeof hijri.parseFlexibleHijriToGregorian === "function") {
                const greg = hijri.parseFlexibleHijriToGregorian(stored);
                if (greg) {
                    suppressHallHijriSync = true;
                    pageCtx.hallEventHijriEditor.setFromGregorian(greg);
                    suppressHallHijriSync = false;
                    return;
                }
            }
        }

        const ci = getReservationCheckInCombined();
        if (ci) {
            suppressHallHijriSync = true;
            pageCtx.hallEventHijriEditor.setFromGregorian(ci);
            suppressHallHijriSync = false;
        }
    }

    function wireHallHijriDateSync() {
        if (pageCtx._hallHijriWireDone) {
            return;
        }

        const inst = $("#resCheckInDate").dxDateBox("instance");
        if (!inst) {
            return;
        }

        const prevHandler = inst.option("onValueChanged");
        inst.option("onValueChanged", function onCheckInDateChanged(e) {
            if (typeof prevHandler === "function") {
                prevHandler.call(this, e);
            }
            if (!suppressDateDurationSync && pageCtx.isHallProperty) {
                syncHallHijriFromGregorianCheckIn();
            }
        });
        pageCtx._hallHijriWireDone = true;
    }

    function formatLocalTimeParam(date) {
        if (!date) {
            return null;
        }
        const d = date instanceof Date ? date : new Date(date);
        return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
    }

    function formatLocalDateParamForHall(value) {
        const dp = window.Zaaer && window.Zaaer.PmsDateParam;
        if (dp && typeof dp.formatLocalDateParam === "function") {
            return dp.formatLocalDateParam(value);
        }
        const d = value instanceof Date ? value : new Date(value);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function syncHallEventScheduleAfterSave() {
        if (!pageCtx.isHallProperty || !pageCtx.routeId) {
            return Promise.resolve();
        }

        const hallSvc = window.Zaaer && window.Zaaer.HallEventsService;
        if (!hallSvc || typeof hallSvc.updateSchedule !== "function") {
            return Promise.resolve();
        }

        const ci = getReservationCheckInCombined();
        const co = getReservationCheckOutCombined();
        if (!ci || !co) {
            return Promise.resolve();
        }

        const hijriStorage = pageCtx.hallEventHijriEditor
            ? pageCtx.hallEventHijriEditor.getStorageValue()
            : "";

        return hallSvc
            .updateSchedule(pageCtx.routeId, {
                eventDate: formatLocalDateParamForHall(ci),
                eventDateHijri: hijriStorage || null,
                eventStartTime: formatLocalTimeParam(ci),
                eventEndTime: formatLocalTimeParam(co)
            })
            .then((ev) => {
                pageCtx.hallEvent = ev || pageCtx.hallEvent;
                renderHallEventSummary();
                syncHallHijriFromGregorianCheckIn();
            })
            .catch((err) => {
                console.warn("reservation-detail: hall schedule sync failed", err);
            });
    }

    function lockReservationPageForExit(messageKey) {
        if (pageCtx.pageNavLocked) {
            return;
        }
        pageCtx.pageNavLocked = true;
        document.body.classList.add("res-page-exit-lock");

        try {
            if (DevExpress.ui.dialog && typeof DevExpress.ui.dialog.closeDialog === "function") {
                DevExpress.ui.dialog.closeDialog();
            }
        } catch {
            /* ignore */
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.option({
                visible: true,
                shading: true,
                showIndicator: true,
                showPane: true,
                closeOnOutsideClick: false,
                message: messageKey ? t(messageKey) : t("reservationDetail.otherOptions.cancelling")
            });
            lp.show();
        }
    }

    function unlockReservationPageExit() {
        pageCtx.pageNavLocked = false;
        document.body.classList.remove("res-page-exit-lock");
        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.hide();
        }
    }

    function navigateToRoomBoardNow() {
        window.location.replace(resolveRoomBoardUrl());
    }

    function notifyCancelBlockedByFinancials() {
        DevExpress.ui.notify(t("reservationDetail.cancelErrActiveFinancials"), "warning", 4500);
    }

    function runCancelReservationFromOtherOptions() {
        if (!canCancelReservationFromOtherOptions()) {
            notifyForbidden();
            return;
        }

        if (!pageCtx.routeId) {
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (lp) {
            lp.show();
        }

        loadReservationCancelFinancialBundle()
            .then((bundle) => {
                if (reservationHasBlockingFinancialOperations(bundle)) {
                    notifyCancelBlockedByFinancials();
                    return;
                }

                DevExpress.ui.dialog
                    .confirm(
                        t("reservationDetail.otherOptions.confirmCancel"),
                        t("reservationDetail.otherOptions.cancelBookings")
                    )
                    .done((yes) => {
                        if (!yes) {
                            return;
                        }

                        loadReservationCancelFinancialBundle().then((bundleAgain) => {
                            if (reservationHasBlockingFinancialOperations(bundleAgain)) {
                                notifyCancelBlockedByFinancials();
                                return;
                            }

                            lockReservationPageForExit("reservationDetail.otherOptions.cancelling");

                            window.Zaaer.ReservationDetailService.cancelReservation(
                                pageCtx.routeId,
                                pageCtx.hotelIdParam
                            )
                                .then(() => {
                                    navigateToRoomBoardNow();
                                })
                                .catch((err) => {
                                    unlockReservationPageExit();
                                    const status = err && (err.status || err.statusCode);
                                    if (status === 403) {
                                        const permCode =
                                            err &&
                                            err.responseJSON &&
                                            (err.responseJSON.permissionCode ||
                                                err.responseJSON.PermissionCode);
                                        const msg = permCode
                                            ? `${t("common.forbidden")} (${permCode})`
                                            : t("common.forbidden");
                                        DevExpress.ui.notify(msg, "warning", 4200);
                                    } else {
                                        DevExpress.ui.notify(
                                            err && err.message
                                                ? String(err.message)
                                                : t("error.saveReservationDetail"),
                                            "error",
                                            4200
                                        );
                                    }
                                });
                        });
                    });
            })
            .finally(() => {
                if (lp) {
                    lp.hide();
                }
            });
    }

    function formatActivityLogRentalType(value) {
        const key = normRental(value);
        if (key === "Monthly") {
            return t("roomBoard.rentalType.monthly");
        }

        if (key === "Daily") {
            return t("roomBoard.rentalType.daily");
        }

        const raw = String(value || "")
            .trim()
            .toLowerCase();
        if (raw.includes("year")) {
            return t("roomBoard.rentalType.yearly");
        }

        if (raw.includes("hour")) {
            return t("roomBoard.rentalType.inhour");
        }

        return value ? String(value) : "";
    }

    function activityLogI18nKey(eventKey) {
        const k = String(eventKey || "unknown")
            .trim()
            .toLowerCase()
            .replace(/\./g, "_");
        return `activityLog.events.${k}`;
    }

    function formatActivityLogDateTime(value) {
        if (!value) {
            return "";
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }

        try {
            const locale = isArabic() ? "ar-SA-u-ca-gregory" : "en-GB";
            const datePart = d.toLocaleDateString(locale, {
                weekday: "long",
                year: "numeric",
                month: "2-digit",
                day: "2-digit"
            });
            const timePart = d.toLocaleTimeString(locale, {
                hour: "2-digit",
                minute: "2-digit",
                hour12: true
            });
            return `${datePart} ${timePart}`;
        } catch {
            return formatReceiptDisplayDate(d);
        }
    }

    function formatActivityLogMoney(amount) {
        const n = Number(amount);
        if (!Number.isFinite(n)) {
            return "";
        }

        return `${DevExpress.localization.formatNumber(n, "#,##0.00")} SAR`;
    }

    function normalizeActivityPayload(row) {
        const p = row && row.payload;
        if (!p || typeof p !== "object" || Array.isArray(p)) {
            return {};
        }

        return p;
    }

    function buildActivityLogMessageHtml(row) {
        const payload = normalizeActivityPayload(row);
        const key = activityLogI18nKey(row.eventKey);
        let template = t(key);
        if (!template || template === key) {
            template = row.eventKey || "";
        }

        const actorName =
            payload.actorName ||
            row.actorDisplayName ||
            row.createdBy ||
            "";
        const reservationNo = payload.reservationNo || row.reservationNo || "";
        const receiptNo = payload.receiptNo || row.refNo || "";
        const promissoryNo = payload.promissoryNo || row.refNo || "";
        const invoiceNo = payload.invoiceNo || "";
        const creditNoteNo = payload.creditNoteNo || row.refNo || "";
        const debitNoteNo = payload.debitNoteNo || row.refNo || "";
        const amountRaw = payload.amount != null ? payload.amount : row.amountTo;
        const amount = amountRaw != null ? formatActivityLogMoney(amountRaw) : "";
        const amountFromRaw =
            row.amountFrom != null ? row.amountFrom : payload.amountFrom != null ? payload.amountFrom : null;
        const amountToRaw =
            row.amountTo != null ? row.amountTo : payload.amountTo != null ? payload.amountTo : amountRaw;
        const amountFrom = amountFromRaw != null ? formatActivityLogMoney(amountFromRaw) : "";
        const amountTo = amountToRaw != null ? formatActivityLogMoney(amountToRaw) : "";
        const date = formatActivityLogDateTime(row.createdAt);
        const fromDate =
            payload.fromDate != null ? formatReceiptDisplayDate(payload.fromDate) : "";
        const toDate = payload.toDate != null ? formatReceiptDisplayDate(payload.toDate) : "";
        const rentalType = formatActivityLogRentalType(payload.rentalType);

        const replacements = [
            { token: "{actorName}", value: actorName, cls: "activity-log-hl activity-log-hl--actor" },
            { token: "{reservationNo}", value: reservationNo, cls: "activity-log-hl activity-log-hl--res" },
            { token: "{receiptNo}", value: receiptNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{promissoryNo}", value: promissoryNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{invoiceNo}", value: invoiceNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{creditNoteNo}", value: creditNoteNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{debitNoteNo}", value: debitNoteNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{amount}", value: amount, cls: "activity-log-hl activity-log-hl--money" },
            { token: "{amountFrom}", value: amountFrom, cls: "activity-log-hl activity-log-hl--money" },
            { token: "{amountTo}", value: amountTo, cls: "activity-log-hl activity-log-hl--money" },
            { token: "{fromDate}", value: fromDate, cls: "activity-log-hl activity-log-hl--date" },
            { token: "{toDate}", value: toDate, cls: "activity-log-hl activity-log-hl--date" },
            { token: "{rentalType}", value: rentalType, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{date}", value: date, cls: "activity-log-hl activity-log-hl--date" }
        ];

        let html = $("<span>").text(template).html();
        replacements.forEach((r) => {
            if (!r.value) {
                return;
            }

            const safe = $("<span>").text(String(r.value)).html();
            const mark = `<span class="${r.cls}">${safe}</span>`;
            html = html.split(r.token).join(mark);
        });

        return html;
    }

    function renderActivityLogTimeline($host, rows) {
        if (window.PmsActivityLogRender && typeof window.PmsActivityLogRender.renderActivityLogTimeline === "function") {
            window.PmsActivityLogRender.renderActivityLogTimeline($host, rows);
            return;
        }
    }

    function switchReservationPageTab(tabId) {
        try {
            const inst = $("#resPageTabPanel").dxTabPanel("instance");
            if (!inst) {
                return;
            }

            const items = inst.option("items") || [];
            const idx = items.findIndex((item) => item && item.id === tabId);
            if (idx >= 0) {
                inst.option("selectedIndex", idx);
            }
        } catch {
            /* tab panel not ready */
        }
    }

    function switchPaymentRecordTab(kind) {
        try {
            const $panel = $(".res-payment-tabpanel-host").first();
            const inst = $panel.dxTabPanel("instance");
            if (!inst) {
                return;
            }

            const items = inst.option("items") || [];
            const idx = items.findIndex((item) => item && item.id === kind);
            if (idx >= 0) {
                inst.option("selectedIndex", idx);
                const item = items[idx];
                ensurePaymentGridForTab(inst, item, idx);
            }
        } catch {
            /* payments tab not rendered yet */
        }
    }

    function hideActivityLogPopup($popupHost) {
        if (!$popupHost || !$popupHost.length) {
            return;
        }

        try {
            $popupHost.dxPopup("instance").hide();
        } catch {
            /* ignore */
        }
    }

    function openActivityLogPromissoryDetail(row, $activityPopupHost) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const ctx = paymentReceiptContext();
        if (
            !svc ||
            typeof svc.loadPromissoryRows !== "function" ||
            (!ctx.reservationRouteId && !ctx.reservationId)
        ) {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
            return;
        }

        if (!canEditPromissoryNoteVoucher()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        hideActivityLogPopup($activityPopupHost);
        switchReservationPageTab("payments");

        window.setTimeout(() => {
            switchPaymentRecordTab("promissory");
            svc.loadPromissoryRows({
                reservationId: ctx.reservationRouteId || ctx.reservationId
            })
                .then((rows) => {
                    const payload = (row && row.payload) || {};
                    const refId = row && row.refId;
                    const refNo = (row && row.refNo) || payload.promissoryNo || "";
                    let match = null;

                    if (refId != null && refId !== "") {
                        match = (rows || []).find(
                            (r) => Number(r.promissoryNoteId || r.id) === Number(refId)
                        );
                    }

                    if (!match && refNo) {
                        match = (rows || []).find(
                            (r) =>
                                String(r.promissoryNo || r.number || "") === String(refNo)
                        );
                    }

                    if (!match || !match.zaaerId) {
                        DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                        return;
                    }

                    openPromissoryNotePopup({ editRow: match });
                })
                .catch((err) => {
                    DevExpress.ui.notify(
                        (err && err.message) || t("activityLog.detailsNotFound"),
                        "error",
                        3200
                    );
                });
        }, 280);
    }

    function openActivityLogPaymentDetail(row, $activityPopupHost) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const ctx = paymentReceiptContext();
        if (
            !svc ||
            typeof svc.loadPaymentRows !== "function" ||
            (!ctx.reservationRouteId && !ctx.reservationId)
        ) {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
            return;
        }

        const eventKey = String((row && row.eventKey) || "").toLowerCase();
        const refType = String((row && row.refType) || "").toLowerCase();
        const isRefund =
            eventKey.indexOf("refund") >= 0 || refType.indexOf("refund") >= 0;
        const kind = isRefund ? "disbursements" : "receipts";

        if (!canEditPaymentVoucherByKind(kind)) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        hideActivityLogPopup($activityPopupHost);
        switchReservationPageTab("payments");

        window.setTimeout(() => {
            switchPaymentRecordTab(kind);
            svc.loadPaymentRows({
                kind,
                reservationId: ctx.reservationRouteId || ctx.reservationId
            })
                .then((rows) => {
                    const payload = (row && row.payload) || {};
                    const refId = row && row.refId;
                    const refNo = (row && row.refNo) || payload.receiptNo || "";
                    let match = null;

                    if (refId != null && refId !== "") {
                        match = (rows || []).find(
                            (r) => Number(r.receiptId) === Number(refId)
                        );
                    }

                    if (!match && refNo) {
                        match = (rows || []).find(
                            (r) => String(r.receiptNo || r.number || "") === String(refNo)
                        );
                    }

                    if (!match || !match.zaaerId) {
                        DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                        return;
                    }

                    if (isRefund) {
                        openPaymentDisbursementPopup({ editRow: match });
                    } else {
                        openPaymentReceiptPopup({ editRow: match });
                    }
                })
                .catch((err) => {
                    DevExpress.ui.notify(
                        (err && err.message) || t("activityLog.detailsNotFound"),
                        "error",
                        3200
                    );
                });
        }, 280);
    }

    function handleActivityLogViewDetails(row, $activityPopupHost) {
        if (!row) {
            return;
        }

        const refType = String(row.refType || "").toLowerCase();
        const eventKey = String(row.eventKey || "").toLowerCase();

        if (
            refType === "paymentreceipt" ||
            refType === "paymentrefund" ||
            eventKey.startsWith("payment.")
        ) {
            openActivityLogPaymentDetail(row, $activityPopupHost);
            return;
        }

        if (refType === "promissorynote" || eventKey.startsWith("promissory.")) {
            openActivityLogPromissoryDetail(row, $activityPopupHost);
            return;
        }

        if (refType === "note" || eventKey === "reservation.note_added") {
            hideActivityLogPopup($activityPopupHost);
            if (canUseReservationNotes()) {
                openReservationNotesPopup();
            } else {
                DevExpress.ui.notify(t("reservationDetail.notes.requiresSave"), "warning", 3200);
            }
            return;
        }

        if (refType === "discount" || eventKey === "reservation.discount_applied") {
            hideActivityLogPopup($activityPopupHost);
            switchReservationPageTab("details");
            window.setTimeout(() => {
                const $fin = $("#res-section-financial");
                if ($fin.length) {
                    $("html, body").animate({ scrollTop: $fin.offset().top - 72 }, 220);
                }
            }, 280);
            return;
        }

        DevExpress.ui.notify(t("activityLog.detailsPending"), "info", 2600);
    }

    function openReservationActivityLogPopup() {
        const routeId = pageCtx.routeId;
        const hotelId = pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId);
        if (!routeId) {
            DevExpress.ui.notify(t("reservationDetail.notes.requiresSave"), "warning", 3200);
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || typeof svc.loadReservationActivityLogs !== "function") {
            DevExpress.ui.notify(t("activityLog.loadFailed"), "error", 3200);
            return;
        }

        if (window.PmsActivityLogRender && typeof window.PmsActivityLogRender.setDetailHandler === "function") {
            window.PmsActivityLogRender.setDetailHandler(null);
        }

        const $host = $("<div>").appendTo("body");
        $host.dxPopup({
            title: t("activityLog.title"),
            width: Math.min(640, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "78vh",
            visible: true,
            showCloseButton: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup activity-log-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty().addClass("activity-log-popup-body");
                const $scroll = $("<div>").addClass("activity-log-popup-scroll").appendTo($content);
                const $timelineHost = $("<div>").appendTo($scroll);
                $("<div>").addClass("activity-log-loading").text(t("common.loading")).appendTo($timelineHost);

                if (window.PmsActivityLogRender && typeof window.PmsActivityLogRender.setDetailHandler === "function") {
                    window.PmsActivityLogRender.setDetailHandler((row) =>
                        handleActivityLogViewDetails(row, $host)
                    );
                }

                svc.loadReservationActivityLogs(routeId, hotelId, { skip: 0, take: 100 })
                    .then((rows) => {
                        renderActivityLogTimeline($timelineHost, rows);
                    })
                    .catch((err) => {
                        $timelineHost.empty();
                        $("<p>")
                            .addClass("activity-log-empty")
                            .text((err && err.message) || t("activityLog.loadFailed"))
                            .appendTo($timelineHost);
                    });
            },
            onHidden() {
                if (window.PmsActivityLogRender && typeof window.PmsActivityLogRender.setDetailHandler === "function") {
                    window.PmsActivityLogRender.setDetailHandler(null);
                }
                $host.remove();
            }
        });
    }

    function handleReservationOtherOption(optionId) {
        if (optionId === "undo_checkin") {
            if (!requirePmsPermission("reservations.undo_check_in")) {
                return;
            }

            runUndoCheckInFromOtherOptions();
            return;
        }

        if (optionId === "cancel_bookings") {
            if (!requirePmsPermission("reservations.cancel")) {
                return;
            }

            runCancelReservationFromOtherOptions();
            return;
        }

        if (optionId === "checkout") {
            if (!requirePmsPermission("reservations.check_out")) {
                return;
            }

            if (!ensureCheckoutDepartureAllowed()) {
                return;
            }

            showCheckoutTaxInvoiceReminder();
            openCheckoutStepperAndRun();
            return;
        }

        if (optionId === "activity_log") {
            if (!hasPmsPermission("reservations.activity_log_view")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }

            openReservationActivityLogPopup();
            return;
        }

        if (optionId === "resort_tickets") {
            if (!hasPmsPermission("resort_tickets.view")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }

            if (!window.Zaaer.RoomResortTicketPopup || typeof window.Zaaer.RoomResortTicketPopup.open !== "function") {
                DevExpress.ui.notify(t("roomBoard.resortTickets.missingModule"), "error", 3000);
                return;
            }

            window.Zaaer.RoomResortTicketPopup.open(
                { reservationId: pageCtx.routeId },
                t,
                refreshReservationOtherOptionsMenu
            );
            return;
        }

        const labels = {
            resort_tickets: t("reservationDetail.tickets.title"),
            booking_summary: t("reservationDetail.otherOptions.bookingSummary"),
            booking_details: t("reservationDetail.otherOptions.bookingDetails"),
            housing_contract: t("reservationDetail.otherOptions.housingContract"),
            activity_log: t("reservationDetail.otherOptions.activityLog")
        };

        DevExpress.ui.notify(
            t("reservationDetail.otherOptions.pending").replace(
                "{action}",
                labels[optionId] || optionId || ""
            ),
            "info",
            2800
        );
    }

    function initReservationOtherOptions() {
        const items = reservationOtherOptionsItems();
        $("#reservationOtherOptions")
            .addClass("res-other-options-btn--icon-only")
            .dxDropDownButton({
            text: "",
            icon: "preferences",
            hint: t("reservationDetail.otherOptions.title"),
            type: "default",
            stylingMode: "contained",
            visible: items.length > 0,
            items: items,
            keyExpr: "id",
            displayExpr: "text",
            showArrowIcon: false,
            rtlEnabled: isArabic(),
            elementAttr: { class: "res-other-options-btn" },
            dropDownOptions: {
                width: 280,
                wrapperAttr: { class: "res-other-options-dropdown-popup" }
            },
            onItemClick(e) {
                handleReservationOtherOption(e.itemData && e.itemData.id);
            }
        });
    }

    function normalizeReservationKindKey(raw, detail) {
        const s = raw != null ? `${raw}`.trim().toLowerCase() : "";
        if (s === "company" || s === "corporate") {
            return "company";
        }
        if (s === "individual" || s === "فردي") {
            return "individual";
        }
        if (detail && (detail.corporateId || detail.company)) {
            return "company";
        }
        return "individual";
    }

    function isCompanyReservationKind(detailOptional) {
        const kindInst = $("#resGeneralKind").dxSelectBox("instance");
        if (kindInst) {
            return kindInst.option("value") === "company";
        }
        const d = detailOptional || pageCtx.detail;
        const general = d && d.general && typeof d.general === "object" ? d.general : {};
        return normalizeReservationKindKey(general.reservationType, d) === "company";
    }

    /** Show/hide entire «معلومات المؤسسة» section from reservation kind (not from empty company grid). */
    function setCompanySectionVisible(showCompanySection) {
        const show =
            showCompanySection !== undefined ? !!showCompanySection : isCompanyReservationKind();
        if (!pageCtx.isLodgingProperty) {
            $("#res-section-company").toggle(show);
        }
        toggleCompanyEditors(!show);
        if (pageCtx.isLodgingProperty) {
            syncLodgingPartyCards();
        }
    }

    function reservationSectionDataGridOptions(extra) {
        const po = window.Zaaer && window.Zaaer.PmsGridOptions;
        if (po && typeof po.merge === "function" && typeof po.baseline === "function") {
            return po.merge(po.baseline(), extra || {});
        }

        return Object.assign(
            {
                showBorders: true,
                columnAutoWidth: true,
                wordWrapEnabled: false,
                rowAlternationEnabled: true,
                hoverStateEnabled: true,
                showColumnLines: true,
                showRowLines: true,
                rtlEnabled: isArabic(),
                width: "100%",
                columnMinWidth: 64,
                elementAttr: { class: "pms-grid-compact" },
                headerFilter: { visible: true, search: { enabled: true } },
                scrolling: {
                    mode: "standard",
                    columnRenderingMode: "standard",
                    scrollByContent: true,
                    scrollByThumb: true,
                    showScrollbar: "always",
                    useNative: isArabic()
                }
            },
            extra || {}
        );
    }

    function buildPmsPickerSelectColumn(onPick, hint) {
        return {
            type: "buttons",
            width: 50,
            minWidth: 50,
            fixed: true,
            fixedPosition: isArabic() ? "right" : "left",
            cssClass: "guest-picker-col-select",
            caption: "",
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: hint || t("reservationDetail.guest.pickVisitorHint"),
                    icon: isArabic() ? "chevronleft" : "chevronright",
                    cssClass: "guest-picker-select-btn guest-picker-select-btn--arrow",
                    onClick(e) {
                        if (e.event && typeof e.event.stopPropagation === "function") {
                            e.event.stopPropagation();
                        }
                        if (e.row && e.row.data && typeof onPick === "function") {
                            onPick(e.row.data);
                        }
                    }
                }
            ]
        };
    }

    function pmsPickerGridPagingOptions() {
        return { pageSize: 50 };
    }

    function pmsPickerGridPagerOptions() {
        return {
            visible: true,
            showPageSizeSelector: true,
            allowedPageSizes: [10, 20, 50],
            showInfo: true,
            showNavigationButtons: true,
            displayMode: "full"
        };
    }

    function checkoutTaxInvoiceHintText() {
        return t("reservationDetail.checkout.taxInvoiceHint");
    }

    function showCheckoutTaxInvoiceReminder() {
        DevExpress.ui.notify(checkoutTaxInvoiceHintText(), "info", 5200);
    }

    function formatCheckoutMoney(value) {
        return DevExpress.localization.formatNumber(roundMoney(value), "#,##0.00");
    }

    function formatCheckoutDateOnly(value) {
        if (!value) {
            return "—";
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "—";
        }

        return formatReceiptDisplayDate(d);
    }

    function checkoutWizardFooterIcons() {
        if (isArabic()) {
            return { back: "chevronright", next: "chevronleft" };
        }

        return { back: "chevronleft", next: "chevronright" };
    }

    function paymentRowAmountAbs(row) {
        return Math.abs(Number((row && (row.amountPaid ?? row.amount)) || 0) || 0);
    }

    function computeUnrefundedSecurityDepositAmount(rows) {
        let received = 0;
        let refunded = 0;
        (rows || []).forEach((r) => {
            if (!r || isPaymentRowCancelled(r)) {
                return;
            }

            const kind = paymentRowUiReceiptKind(r);
            const amt = paymentRowAmountAbs(r);
            if (kind === "security_deposit") {
                received += amt;
            } else if (kind === "security_deposit_refund") {
                refunded += amt;
            }
        });
        return roundMoney(Math.max(0, received - refunded));
    }

    function getCachedPaymentRows(kind) {
        const cache = pageCtx.paymentRowsCache;
        if (!cache || !Object.prototype.hasOwnProperty.call(cache, kind)) {
            return null;
        }

        return Array.isArray(cache[kind]) ? cache[kind] : [];
    }

    /**
     * Refundable deposit balance for this reservation (receipts − disbursements, excl. cancelled).
     * Uses payment tab cache when warm — O(n) over cached rows, no extra API in the common case.
     */
    function resolveAvailableSecurityDepositRefund(options) {
        options = options || {};
        const excludeRow = options.excludeRow || null;

        function compute(receipts, disbursements) {
            const rows = (receipts || []).concat(disbursements || []);
            let available = computeUnrefundedSecurityDepositAmount(rows);
            if (
                excludeRow &&
                paymentRowUiReceiptKind(excludeRow) === "security_deposit_refund" &&
                !isPaymentRowCancelled(excludeRow)
            ) {
                available = roundMoney(available + paymentRowAmountAbs(excludeRow));
            }

            return available;
        }

        const cachedReceipts = getCachedPaymentRows("receipts");
        const cachedDisbursements = getCachedPaymentRows("disbursements");
        if (cachedReceipts !== null && cachedDisbursements !== null) {
            return Promise.resolve(compute(cachedReceipts, cachedDisbursements));
        }

        return Promise.all([
            cachedReceipts !== null
                ? Promise.resolve(cachedReceipts)
                : loadReservationPaymentRows("receipts"),
            cachedDisbursements !== null
                ? Promise.resolve(cachedDisbursements)
                : loadReservationPaymentRows("disbursements")
        ]).then(([receipts, disbursements]) => {
            syncPaymentRowsCache("receipts", receipts);
            syncPaymentRowsCache("disbursements", disbursements);
            return compute(receipts, disbursements);
        });
    }

    function computeCheckoutRefundTotals(rows) {
        let rentRefund = 0;
        let depositRefund = 0;
        (rows || []).forEach((r) => {
            if (!r || isPaymentRowCancelled(r)) {
                return;
            }

            const kind = paymentRowUiReceiptKind(r);
            const amt = paymentRowAmountAbs(r);
            if (kind === "refund") {
                rentRefund += amt;
            } else if (kind === "security_deposit_refund") {
                depositRefund += amt;
            }
        });
        return {
            rentRefund: roundMoney(rentRefund),
            depositRefund: roundMoney(depositRefund),
            totalRefund: roundMoney(rentRefund + depositRefund)
        };
    }

    function loadCheckoutPaymentRowsBundle() {
        return Promise.all([
            loadReservationPaymentRows("receipts"),
            loadReservationPaymentRows("disbursements"),
            loadReservationPaymentRows("promissory")
        ]).then(([receipts, disbursements, promissory]) => {
            const r = Array.isArray(receipts) ? receipts : [];
            const d = Array.isArray(disbursements) ? disbursements : [];
            const p = Array.isArray(promissory) ? promissory : [];
            return {
                receipts: r,
                disbursements: d,
                promissory: p,
                all: r.concat(d)
            };
        });
    }

    function loadReservationCancelFinancialBundle() {
        return Promise.all([
            loadReservationPaymentRows("receipts"),
            loadReservationPaymentRows("disbursements"),
            loadReservationPaymentRows("promissory"),
            loadReservationPaymentRows("invoices")
        ]).then(([receipts, disbursements, promissory, invoices]) => ({
            receipts: Array.isArray(receipts) ? receipts : [],
            disbursements: Array.isArray(disbursements) ? disbursements : [],
            promissory: Array.isArray(promissory) ? promissory : [],
            invoices: Array.isArray(invoices) ? invoices : []
        }));
    }

    function isInvoiceRowActiveForCancel(row) {
        const status = String((row && row.paymentStatus) || "").toLowerCase();
        return !["reversed", "void", "voided", "cancelled", "canceled"].includes(status);
    }

    function reservationHasBlockingFinancialOperations(bundle) {
        if (!bundle) {
            return false;
        }

        const hasReceipt = (bundle.receipts || []).some((row) => row && !isPaymentRowCancelled(row));
        const hasDisbursement = (bundle.disbursements || []).some((row) => row && !isPaymentRowCancelled(row));
        const hasPromissory = (bundle.promissory || []).some((row) => row && !isPromissoryRowCancelled(row));
        const hasInvoice = (bundle.invoices || []).some((row) => row && isInvoiceRowActiveForCancel(row));
        return hasReceipt || hasDisbursement || hasPromissory || hasInvoice;
    }

    function sumActivePromissoryNoteAmounts(rows) {
        return (rows || []).reduce((sum, row) => {
            if (!row) {
                return sum;
            }

            const status = String(row.status || "").toLowerCase();
            if (status === "cancelled") {
                return sum;
            }

            const amt = Number(row.amount) || 0;
            return sum + (Number.isFinite(amt) ? amt : 0);
        }, 0);
    }

    function isCheckoutBalanceCleared(balance, promissoryTotal) {
        const bal = Number(balance) || 0;
        const prom = Number(promissoryTotal) || 0;
        return bal <= 0.01 || prom >= bal - 0.01;
    }

    function isCheckoutInvoiceCleared(invoiceRemaining) {
        return (Number(invoiceRemaining) || 0) <= 0.01;
    }

    function sumLocalExtrasTotalFromPage() {
        return roundMoney(
            (pageCtx.extras || []).reduce((sum, row) => sum + (Number(row.totalAmount) || 0), 0)
        );
    }

    function hasUnsavedExtrasVsServer(serverExtrasTotal) {
        if (!isPersistedReservation()) {
            return false;
        }

        if (shouldSendExtrasInPatch(buildExtrasPatchPayload())) {
            return true;
        }

        return Math.abs(sumLocalExtrasTotalFromPage() - roundMoney(serverExtrasTotal)) > 0.01;
    }

    function applyCheckoutSnapshotToPage(snap) {
        if (!snap || !pageCtx.detail || !pageCtx.detail.financial) {
            return;
        }

        const fin = pageCtx.detail.financial;
        fin.balanceAmount = snap.balanceAmount;
        fin.amountPaid = snap.amountPaid;
        fin.totalAmount = snap.totalAmount;
        fin.totalExtra = snap.extrasTotal;
        fin.totalPenalties = snap.penaltiesTotal;
        if (snap.discountsTotal != null) {
            fin.totalDiscounts = snap.discountsTotal;
        }

        renderFinancialPanel(pageCtx.detail, {
            subtotal: fin.subtotal,
            tax: fin.totalTaxAmount,
            totalExtra: snap.extrasTotal,
            penalties: snap.penaltiesTotal,
            discounts: snap.discountsTotal,
            total: snap.totalAmount,
            paid: snap.amountPaid,
            balance: snap.balanceAmount
        });
    }

    function applyCheckoutSnapshotToWizard(wizardCtx, snap) {
        if (!snap || !wizardCtx) {
            return;
        }

        wizardCtx.balance = roundMoney(snap.balanceAmount);
        wizardCtx.invoicedTotal = roundMoney(snap.invoicedTotal);
        wizardCtx.invoiceRemaining = roundMoney(snap.invoiceRemaining);
        wizardCtx.invoiceRequiredAmount = roundMoney(snap.invoiceRequiredAmount);
        wizardCtx.creditNotesTotal = roundMoney(snap.creditNotesTotal);
        wizardCtx.localExtrasMismatch = hasUnsavedExtrasVsServer(snap.extrasTotal);
        applyCheckoutSnapshotToPage(snap);
    }

    function getCheckoutStayFacts() {
        const d = pageCtx.detail || {};
        const header = d.header && typeof d.header === "object" ? d.header : {};
        const dates = d.dates && typeof d.dates === "object" ? d.dates : {};
        const rental = normRental(dates.rentalType);
        const monthly = rental === "Monthly";
        const ci = dates.checkInDate ? new Date(dates.checkInDate) : getReservationCheckInCombined();
        const co = dates.checkOutDate ? new Date(dates.checkOutDate) : getReservationCheckOutCombined();
        let durationText = "—";
        if (monthly) {
            const months =
                dates.numberOfMonths != null
                    ? Math.max(1, Math.floor(Number(dates.numberOfMonths)) || 1)
                    : getReservationMonthCountForPricing();
            durationText = t("reservationDetail.checkoutWizard.summaryMonths").replace(
                "{count}",
                String(months)
            );
        } else {
            const nights =
                dates.totalNights != null
                    ? Math.max(1, Math.floor(Number(dates.totalNights)) || 1)
                    : hotelNightCount(ci, co);
            durationText = t("reservationDetail.checkoutWizard.summaryNights").replace(
                "{count}",
                nights != null ? String(nights) : "—"
            );
        }

        const rentalLabel = monthly
            ? t("reservationDetail.rental.monthly")
            : t("reservationDetail.rental.daily");
        const unitCount = Array.isArray(d.units) ? d.units.length : 0;

        return {
            guestName: header.mainGuestName || "—",
            reservationNo: header.reservationNo || "—",
            rentalLabel,
            checkIn: ci,
            checkOut: co,
            durationText,
            unitCount
        };
    }

    function appendCheckoutReviewRow($root, label, value, extraClass) {
        const $row = $("<div>").addClass("checkout-review-row").appendTo($root);
        if (extraClass) {
            $row.addClass(extraClass);
        }

        $("<span>").addClass("checkout-review-row__label").text(label).appendTo($row);
        $("<span>").addClass("checkout-review-row__value").text(value).appendTo($row);
    }

    function buildCheckoutReviewSummaryPanel(refunds, finOverride) {
        const facts = getCheckoutStayFacts();
        const fin = finOverride || computeReservationFinancialSnapshot();
        const paid = roundMoney(fin.paid != null ? fin.paid : fin.amountPaid);
        const discounts = roundMoney(fin.discounts != null ? fin.discounts : fin.totalDiscounts);
        const total = roundMoney(
            (fin.total != null ? fin.total : fin.totalAmount) - discounts
        );
        const balance = roundMoney(fin.balance != null ? fin.balance : fin.balanceAmount);
        const refundTotal = refunds ? refunds.totalRefund : 0;

        const $card = $("<div>").addClass("checkout-review-summary");
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryGuest"),
            facts.guestName
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryReservation"),
            facts.reservationNo
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryRental"),
            facts.rentalLabel
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryUnits"),
            String(facts.unitCount)
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryCheckIn"),
            formatCheckoutDateOnly(facts.checkIn)
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryCheckOut"),
            formatCheckoutDateOnly(facts.checkOut)
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryDuration"),
            facts.durationText
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryTotal"),
            formatCheckoutMoney(total),
            "checkout-review-row--emphasis"
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryPaid"),
            formatCheckoutMoney(paid)
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryRefunded"),
            formatCheckoutMoney(refundTotal)
        );
        appendCheckoutReviewRow(
            $card,
            t("reservationDetail.checkoutWizard.summaryBalance"),
            formatCheckoutMoney(balance),
            balance > 0.01 ? "checkout-review-row--due" : "checkout-review-row--ok"
        );
        return $card;
    }

    function openCheckoutBalanceReceiptPopup(amountDue, onSettled) {
        openPaymentReceiptPopup({
            checkoutFlow: {
                receiptType: "receipt",
                amount: amountDue,
                titleKey: "reservationDetail.checkoutWizard.payBalanceTitle",
                onSettled: typeof onSettled === "function" ? onSettled : null
            }
        });
    }

    function openCheckoutDepositRefundPopup(amountDue, onSettled) {
        openPaymentDisbursementPopup({
            checkoutFlow: {
                receiptType: "security_deposit_refund",
                amount: amountDue,
                titleKey: "reservationDetail.checkoutWizard.refundDepositTitle",
                onSettled: typeof onSettled === "function" ? onSettled : null
            }
        });
    }

    function openCheckoutStepperAndRun() {
        if (!pageCtx.routeId || pageCtx.checkoutUiPendingFirstSave) {
            return;
        }

        if (!ensureCheckoutDepartureAllowed()) {
            return;
        }

        if (typeof $.fn.dxMultiView !== "function") {
            DevExpress.ui.notify(t("reservationDetail.checkoutWizard.multiViewMissing"), "error", 4200);
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || typeof svc.loadCheckoutSnapshot !== "function") {
            DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 4200);
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        lp.show();
        svc.loadCheckoutSnapshot(pageCtx.routeId, pageCtx.hotelIdParam)
            .then((snap) => {
                lp.hide();
                if (!snap) {
                    DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 4200);
                    return;
                }
                openCheckoutStepperWithSnapshot(snap);
            })
            .catch((err) => {
                lp.hide();
                DevExpress.ui.notify(
                    err && err.message ? String(err.message) : t("error.loadReservationDetail"),
                    "error",
                    4200
                );
            });
    }

    function openCheckoutStepperWithSnapshot(initialSnapshot) {
        const $host = $("<div>").appendTo("body");
        const wizardCtx = {
            balance: roundMoney(initialSnapshot.balanceAmount),
            promissoryTotal: 0,
            depositPending: 0,
            refundTotals: computeCheckoutRefundTotals([]),
            paymentBundle: { receipts: [], disbursements: [], all: [] },
            depositAutoOpened: false,
            invoiceAutoOpened: false,
            invoicedTotal: roundMoney(initialSnapshot.invoicedTotal),
            invoiceRemaining: roundMoney(initialSnapshot.invoiceRemaining),
            invoiceRequiredAmount: roundMoney(initialSnapshot.invoiceRequiredAmount),
            creditNotesTotal: roundMoney(initialSnapshot.creditNotesTotal),
            localExtrasMismatch: hasUnsavedExtrasVsServer(initialSnapshot.extrasTotal)
        };

        applyCheckoutSnapshotToPage(initialSnapshot);

        if (wizardCtx.localExtrasMismatch) {
            DevExpress.ui.notify(t("reservationDetail.checkoutWizard.unsavedExtrasWarn"), "warning", 5200);
        }

        $host.dxPopup({
            title: t("reservationDetail.checkoutWizard.title"),
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "72vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            visible: true,
            showCloseButton: true,
            showTitle: true,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup checkout-wizard-popup" },
            onShowing(e) {
                const popupInst = e.component;
                const $root = $(popupInst.content()).empty();
                const $shell = $("<div>").addClass("checkout-wizard-root").appendTo($root);

                $("<div>")
                    .addClass("checkout-tax-invoice-hint")
                    .attr("role", "note")
                    .append($("<p>").text(t("reservationDetail.checkout.taxInvoiceHint")))
                    .appendTo($shell);

                const $stepperHost = $("<nav>")
                    .addClass("checkout-pms-stepper")
                    .attr("aria-label", t("reservationDetail.checkoutWizard.title"))
                    .appendTo($shell);
                const $track = $("<div>").addClass("checkout-pms-stepper__track").appendTo($stepperHost);

                const $balancePanelHost = $("<div>").addClass("checkout-wizard-panel");
                const $depositsPanelHost = $("<div>").addClass("checkout-wizard-panel");
                const $invoicesPanelHost = $("<div>").addClass("checkout-wizard-panel");
                const $reviewPanelHost = $("<div>").addClass("checkout-wizard-panel");

                function isBalanceCleared() {
                    return isCheckoutBalanceCleared(wizardCtx.balance, wizardCtx.promissoryTotal);
                }

                function isDepositCleared() {
                    return wizardCtx.depositPending <= 0.01;
                }

                function isInvoiceCleared() {
                    return isCheckoutInvoiceCleared(wizardCtx.invoiceRemaining);
                }

                function syncWizardFromPayments(bundle) {
                    wizardCtx.paymentBundle = bundle || wizardCtx.paymentBundle;
                    wizardCtx.promissoryTotal = sumActivePromissoryNoteAmounts(
                        wizardCtx.paymentBundle.promissory
                    );
                    wizardCtx.depositPending = computeUnrefundedSecurityDepositAmount(
                        wizardCtx.paymentBundle.all
                    );
                    wizardCtx.refundTotals = computeCheckoutRefundTotals(wizardCtx.paymentBundle.all);
                }

                function refreshWizardFinancials() {
                    const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
                    if (!svc || typeof svc.loadCheckoutSnapshot !== "function") {
                        return Promise.resolve();
                    }

                    return svc
                        .loadCheckoutSnapshot(pageCtx.routeId, pageCtx.hotelIdParam)
                        .then((snap) => {
                            if (snap) {
                                applyCheckoutSnapshotToWizard(wizardCtx, snap);
                            }
                            return loadReservationPaymentRows("promissory").then((rows) => {
                                wizardCtx.promissoryTotal = sumActivePromissoryNoteAmounts(rows);
                            });
                        })
                        .then(() => {
                            paintBalancePanel();
                            paintInvoicesPanel();
                            paintReviewPanel();
                            updateNavFromIdx(readStepIndex());
                        });
                }

                function paintBalancePanel() {
                    $balancePanelHost.empty();
                    const cleared = isBalanceCleared();
                    const amountStr = formatCheckoutMoney(wizardCtx.balance);
                    const $msg = $("<p>").addClass("checkout-wizard-msg");
                    if (cleared) {
                        $msg.addClass("checkout-wizard-msg--ok");
                        if (
                            wizardCtx.balance > 0.01 &&
                            wizardCtx.promissoryTotal >= wizardCtx.balance - 0.01
                        ) {
                            $msg.text(
                                t("reservationDetail.checkoutWizard.balanceOkPromissory").replace(
                                    "{amount}",
                                    formatCheckoutMoney(wizardCtx.promissoryTotal)
                                )
                            );
                        } else {
                            $msg.text(t("reservationDetail.checkoutWizard.balanceOk"));
                        }
                    } else {
                        $msg.addClass("checkout-wizard-msg--due").text(
                            t("reservationDetail.checkoutWizard.balanceBlock").replace("{amount}", amountStr)
                        );
                    }

                    $balancePanelHost.append($msg);

                    if (wizardCtx.localExtrasMismatch) {
                        $("<p>")
                            .addClass("checkout-wizard-hint checkout-wizard-hint--warn")
                            .text(t("reservationDetail.checkoutWizard.unsavedExtrasHint"))
                            .appendTo($balancePanelHost);
                    }
                }

                function paintInvoicesPanel() {
                    $invoicesPanelHost.empty();
                    const cleared = isInvoiceCleared();
                    const requiredStr = formatCheckoutMoney(wizardCtx.invoiceRequiredAmount);
                    const invoicedStr = formatCheckoutMoney(wizardCtx.invoicedTotal);
                    const remainingStr = formatCheckoutMoney(wizardCtx.invoiceRemaining);
                    const $msg = $("<p>").addClass("checkout-wizard-msg");

                    if (cleared) {
                        $msg
                            .addClass("checkout-wizard-msg--ok")
                            .text(t("reservationDetail.checkoutWizard.invoicesOk"));
                    } else {
                        $msg
                            .addClass("checkout-wizard-msg--due")
                            .text(
                                t("reservationDetail.checkoutWizard.invoicesDue").replace(
                                    "{amount}",
                                    remainingStr
                                )
                            );
                    }

                    $invoicesPanelHost.append($msg);
                    $("<p>")
                        .addClass("checkout-wizard-hint")
                        .text(
                            t("reservationDetail.checkoutWizard.invoicesSummary")
                                .replace("{required}", requiredStr)
                                .replace("{invoiced}", invoicedStr)
                        )
                        .appendTo($invoicesPanelHost);

                    if (wizardCtx.creditNotesTotal > 0.01) {
                        $("<p>")
                            .addClass("checkout-wizard-hint")
                            .text(
                                t("reservationDetail.payments.invoice.creditNotesHint").replace(
                                    "{amount}",
                                    formatCheckoutMoney(wizardCtx.creditNotesTotal)
                                )
                            )
                            .appendTo($invoicesPanelHost);
                    }

                    if (!cleared) {
                        $("<p>")
                            .addClass("checkout-wizard-hint")
                            .text(t("reservationDetail.checkoutWizard.invoicesHint"))
                            .appendTo($invoicesPanelHost);
                    }
                }

                function paintDepositsPanel() {
                    $depositsPanelHost.empty();
                    const cleared = isDepositCleared();
                    const amountStr = formatCheckoutMoney(wizardCtx.depositPending);
                    const $msg = $("<p>").addClass("checkout-wizard-msg");
                    if (cleared) {
                        $msg.addClass("checkout-wizard-msg--ok").text(
                            t("reservationDetail.checkoutWizard.depositsOk")
                        );
                    } else {
                        $msg.addClass("checkout-wizard-msg--due").text(
                            t("reservationDetail.checkoutWizard.depositsDue").replace("{amount}", amountStr)
                        );
                    }

                    $depositsPanelHost.append($msg);
                    if (!cleared) {
                        $("<p>")
                            .addClass("checkout-wizard-hint")
                            .text(t("reservationDetail.checkoutWizard.depositsRefundHint"))
                            .appendTo($depositsPanelHost);
                    }
                }

                function paintReviewPanel() {
                    $reviewPanelHost.empty();
                    const f = pageCtx.detail && pageCtx.detail.financial;
                    const finForReview = f
                        ? {
                              paid: f.amountPaid,
                              total: f.totalAmount,
                              discounts: f.totalDiscounts || 0,
                              balance: f.balanceAmount
                          }
                        : null;
                    $reviewPanelHost.append(
                        buildCheckoutReviewSummaryPanel(wizardCtx.refundTotals, finForReview)
                    );
                    $("<p>")
                        .addClass("checkout-wizard-review-note")
                        .text(t("reservationDetail.checkoutWizard.reviewConfirmHint"))
                        .appendTo($reviewPanelHost);
                }

                const $multiViewHost = $("<div>").addClass("checkout-wizard-panels").appendTo($shell);

                const $footer = $("<div>").addClass("checkout-wizard-footer").appendTo($shell);
                const $stepLabel = $("<div>").addClass("checkout-wizard-step-label").appendTo($footer);
                const $actions = $("<div>").addClass("checkout-wizard-actions").appendTo($footer);

                let multiViewInst = null;
                let currentStepIdx = 0;
                const $stepEls = [];
                const $stepConnectors = [];

                function readStepIndex() {
                    return currentStepIdx;
                }

                function setStepLabel(idx) {
                    $stepLabel.text(
                        t("reservationDetail.checkoutWizard.stepOf")
                            .replace("{current}", String(idx + 1))
                            .replace("{total}", "4")
                    );
                }

                function applyStepVisuals(idx) {
                    for (let j = 0; j < $stepEls.length; j += 1) {
                        const $b = $stepEls[j];
                        $b.toggleClass("checkout-pms-step--current", j === idx);
                        $b.toggleClass("checkout-pms-step--done", j < idx);
                        $b.toggleClass("checkout-pms-step--upcoming", j > idx);
                        if (j === idx) {
                            $b.attr("aria-current", "step");
                        } else {
                            $b.removeAttr("aria-current");
                        }
                    }

                    for (let c = 0; c < $stepConnectors.length; c += 1) {
                        const $conn = $stepConnectors[c];
                        $conn.removeClass(
                            "checkout-pms-step-connector--done checkout-pms-step-connector--active"
                        );
                        if (c < idx) {
                            $conn.addClass("checkout-pms-step-connector--done");
                        } else if (c === idx) {
                            $conn.addClass("checkout-pms-step-connector--active");
                        }
                    }
                }

                function canEnterStep(nextIdx) {
                    if (nextIdx >= 1 && !isBalanceCleared()) {
                        return false;
                    }

                    if (nextIdx >= 2 && !isDepositCleared()) {
                        return false;
                    }

                    if (nextIdx >= 3 && !isInvoiceCleared()) {
                        return false;
                    }

                    return true;
                }

                function goToStep(nextIdx, options) {
                    options = options || {};
                    if (nextIdx === currentStepIdx) {
                        return Promise.resolve();
                    }

                    if (!options.force && nextIdx > currentStepIdx && !canEnterStep(nextIdx)) {
                        if (nextIdx >= 1 && !isBalanceCleared()) {
                            DevExpress.ui.notify(
                                t("reservationDetail.checkoutErrBalance"),
                                "warning",
                                3600
                            );
                        } else if (nextIdx >= 2 && !isDepositCleared()) {
                            DevExpress.ui.notify(
                                t("reservationDetail.checkoutWizard.depositsRequired"),
                                "warning",
                                3600
                            );
                        } else if (nextIdx >= 3 && !isInvoiceCleared()) {
                            DevExpress.ui.notify(
                                t("reservationDetail.checkoutWizard.invoicesRequired"),
                                "warning",
                                3600
                            );
                        }
                        return Promise.resolve();
                    }

                    currentStepIdx = nextIdx;
                    if (multiViewInst) {
                        multiViewInst.option("selectedIndex", nextIdx);
                    }
                    applyStepVisuals(nextIdx);
                    updateNavFromIdx(nextIdx);

                    if (nextIdx === 1) {
                        paintDepositsPanel();
                        return maybeAutoOpenDepositRefund();
                    }

                    if (nextIdx === 2) {
                        paintInvoicesPanel();
                        return maybeAutoOpenCheckoutInvoice();
                    }

                    if (nextIdx === 3) {
                        paintReviewPanel();
                    }

                    return Promise.resolve();
                }

                function tryGoToStep(nextIdx) {
                    return goToStep(nextIdx);
                }

                function nextButtonText(idx) {
                    if (idx === 0 && !isBalanceCleared()) {
                        return t("reservationDetail.checkoutWizard.payBalance");
                    }

                    if (idx === 1 && !isDepositCleared()) {
                        return t("reservationDetail.checkoutWizard.refundDeposit");
                    }

                    if (idx === 2 && !isInvoiceCleared()) {
                        return t("reservationDetail.checkoutWizard.issueInvoice");
                    }

                    return t("reservationDetail.checkoutWizard.next");
                }

                function updateNavFromIdx(idx) {
                    const navIcons = checkoutWizardFooterIcons();
                    setStepLabel(idx);
                    $btnBack.dxButton("instance").option("visible", idx > 0);
                    const atLast = idx >= 3;
                    $btnNext.dxButton("instance").option({
                        visible: !atLast,
                        disabled: false,
                        text: nextButtonText(idx),
                        icon: navIcons.next
                    });
                    $btnConfirm.dxButton("instance").option({
                        visible: atLast,
                        disabled: !isBalanceCleared() || !isDepositCleared() || !isInvoiceCleared()
                    });
                }

                function afterBalanceReceiptSaved() {
                    return refreshWizardFinancials().then(() => {
                        if (isBalanceCleared()) {
                            return goToStep(1);
                        }
                    });
                }

                function afterDepositRefundSaved() {
                    return loadCheckoutPaymentRowsBundle()
                        .then((bundle) => {
                            syncWizardFromPayments(bundle);
                            paintDepositsPanel();
                            updateNavFromIdx(readStepIndex());
                            if (isDepositCleared() && readStepIndex() === 1) {
                                return goToStep(2);
                            }
                        });
                }

                function afterCheckoutInvoiceSaved() {
                    return refreshWizardFinancials().then(() => {
                        paintInvoicesPanel();
                        updateNavFromIdx(readStepIndex());
                        if (isInvoiceCleared() && readStepIndex() === 2) {
                            return goToStep(3);
                        }
                    });
                }

                function maybeAutoOpenDepositRefund() {
                    if (isDepositCleared() || wizardCtx.depositAutoOpened) {
                        return Promise.resolve();
                    }

                    wizardCtx.depositAutoOpened = true;
                    return openCheckoutDepositRefundFlow();
                }

                function openCheckoutBalanceReceiptFlow() {
                    const due = roundMoney(wizardCtx.balance);
                    if (due <= 0.01) {
                        return goToStep(1);
                    }

                    openCheckoutBalanceReceiptPopup(due, afterBalanceReceiptSaved);
                    return Promise.resolve();
                }

                function openCheckoutDepositRefundFlow() {
                    const due = roundMoney(wizardCtx.depositPending);
                    if (due <= 0.01) {
                        return goToStep(2, { force: true });
                    }

                    openCheckoutDepositRefundPopup(due, afterDepositRefundSaved);
                    return Promise.resolve();
                }

                function openCheckoutInvoiceFlow() {
                    const due = roundMoney(wizardCtx.invoiceRemaining);
                    if (due <= 0.01) {
                        return goToStep(3, { force: true });
                    }

                    openInvoicePopup({
                        prefillAmount: due,
                        onSaved: afterCheckoutInvoiceSaved
                    });
                    return Promise.resolve();
                }

                function maybeAutoOpenCheckoutInvoice() {
                    if (isInvoiceCleared() || wizardCtx.invoiceAutoOpened) {
                        return Promise.resolve();
                    }

                    wizardCtx.invoiceAutoOpened = true;
                    return openCheckoutInvoiceFlow();
                }

                function handleNextClick() {
                    const idx = readStepIndex();
                    if (idx === 0) {
                        if (!isBalanceCleared()) {
                            return openCheckoutBalanceReceiptFlow();
                        }
                        return goToStep(1);
                    }

                    if (idx === 1) {
                        if (!isDepositCleared()) {
                            return openCheckoutDepositRefundFlow();
                        }
                        return goToStep(2);
                    }

                    if (idx === 2) {
                        if (!isInvoiceCleared()) {
                            return openCheckoutInvoiceFlow();
                        }
                        return goToStep(3);
                    }

                    return Promise.resolve();
                }

                const $btnCancel = $("<div>").appendTo($actions);
                const $btnBack = $("<div>").appendTo($actions);
                const $btnNext = $("<div>").appendTo($actions);
                const $btnConfirm = $("<div>").appendTo($actions);

                multiViewInst = $multiViewHost
                    .dxMultiView({
                        items: [
                            { k: "balance" },
                            { k: "deposits" },
                            { k: "invoices" },
                            { k: "review" }
                        ],
                        itemTemplate(_itemData, itemIndex, itemElement) {
                            const $el = $(itemElement).empty();
                            if (itemIndex === 0) {
                                $el.append($balancePanelHost);
                            } else if (itemIndex === 1) {
                                $el.append($depositsPanelHost);
                            } else if (itemIndex === 2) {
                                $el.append($invoicesPanelHost);
                            } else {
                                $el.append($reviewPanelHost);
                            }
                        },
                        selectedIndex: 0,
                        swipeEnabled: false,
                        animationEnabled: true,
                        deferRendering: false
                    })
                    .dxMultiView("instance");

                const stepDefs = [
                    { label: t("reservationDetail.checkoutWizard.stepBalance"), icon: "money" },
                    { label: t("reservationDetail.checkoutWizard.stepDeposits"), icon: "box" },
                    { label: t("reservationDetail.checkoutWizard.stepInvoices"), icon: "doc" },
                    { label: t("reservationDetail.checkoutWizard.stepReview"), icon: "check" }
                ];

                for (let i = 0; i < stepDefs.length; i += 1) {
                    const def = stepDefs[i];
                    const $btn = $("<button>", { type: "button" })
                        .addClass("checkout-pms-step")
                        .toggleClass("checkout-pms-step--current", i === 0)
                        .toggleClass("checkout-pms-step--upcoming", i > 0);
                    $btn.append(
                        $("<span>")
                            .addClass("checkout-pms-step__orbit")
                            .append($("<span>").addClass(`dx-icon dx-icon-${def.icon}`))
                    );
                    $btn.append($("<span>").addClass("checkout-pms-step__label").text(def.label));
                    const stepIndex = i;
                    $btn.on("click", () => {
                        if (stepIndex > readStepIndex() && stepIndex >= 1 && !isBalanceCleared()) {
                            openCheckoutBalanceReceiptFlow();
                            return;
                        }
                        if (stepIndex >= 2 && !isDepositCleared()) {
                            if (!isBalanceCleared()) {
                                DevExpress.ui.notify(
                                    t("reservationDetail.checkoutErrBalance"),
                                    "warning",
                                    3600
                                );
                                return;
                            }
                            goToStep(1).then(() => openCheckoutDepositRefundFlow());
                            return;
                        }
                        if (stepIndex >= 3 && !isInvoiceCleared()) {
                            if (!isDepositCleared()) {
                                DevExpress.ui.notify(
                                    t("reservationDetail.checkoutWizard.depositsRequired"),
                                    "warning",
                                    3600
                                );
                                return;
                            }
                            goToStep(2).then(() => openCheckoutInvoiceFlow());
                            return;
                        }
                        tryGoToStep(stepIndex);
                    });
                    $track.append($btn);
                    $stepEls.push($btn);

                    if (i < stepDefs.length - 1) {
                        const $conn = $("<div>")
                            .addClass("checkout-pms-step-connector")
                            .attr("aria-hidden", "true");
                        $conn.append($("<span>").addClass("checkout-pms-step-connector__line"));
                        $track.append($conn);
                        $stepConnectors.push($conn);
                    }
                }

                $btnCancel.dxButton({
                    text: t("reservationDetail.checkoutWizard.cancel"),
                    stylingMode: "text",
                    onClick() {
                        popupInst.hide();
                    }
                });

                const navIcons = checkoutWizardFooterIcons();
                $btnBack.dxButton({
                    text: t("reservationDetail.checkoutWizard.back"),
                    icon: navIcons.back,
                    stylingMode: "outlined",
                    type: "normal",
                    visible: false,
                    onClick() {
                        const idx = readStepIndex();
                        if (idx > 0) {
                            tryGoToStep(idx - 1);
                        }
                    }
                });

                $btnNext.dxButton({
                    text: nextButtonText(0),
                    icon: navIcons.next,
                    type: "default",
                    stylingMode: "contained",
                    onClick() {
                        handleNextClick();
                    }
                });

                $btnConfirm.dxButton({
                    text: t("reservationDetail.checkoutWizard.confirm"),
                    type: "danger",
                    stylingMode: "contained",
                    visible: false,
                    onClick() {
                        if (!isBalanceCleared() || !isDepositCleared() || !isInvoiceCleared()) {
                            DevExpress.ui.notify(
                                t("reservationDetail.checkoutWizard.confirmBlocked"),
                                "warning",
                                3600
                            );
                            return;
                        }

                        popupInst.hide();
                        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
                        lp.show();
                        window.Zaaer.ReservationDetailService.checkoutReservation(pageCtx.routeId, pageCtx.hotelIdParam)
                            .then((detail) => {
                                if (detailPatchUsable(detail)) {
                                    applyPostMutationReservationDetail(detail);
                                } else {
                                    applyOptimisticReservationMarkedCheckedOut();
                                }
                                DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2200);
                                return loadPage(false);
                            })
                            .catch((err) => {
                                DevExpress.ui.notify(
                                    err && err.message ? String(err.message) : t("error.loadReservationDetail"),
                                    "error",
                                    4200
                                );
                            })
                            .finally(() => lp.hide());
                    }
                });

                paintBalancePanel();
                paintDepositsPanel();
                paintInvoicesPanel();
                paintReviewPanel();
                applyStepVisuals(0);
                updateNavFromIdx(0);

                loadCheckoutPaymentRowsBundle().then((bundle) => {
                    syncWizardFromPayments(bundle);
                    paintBalancePanel();
                    paintDepositsPanel();
                    paintInvoicesPanel();
                    paintReviewPanel();
                    updateNavFromIdx(readStepIndex());
                });
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function detailPatchUsable(detail) {
        return (
            detail &&
            typeof detail === "object" &&
            (detail.header != null ||
                detail.reservationId != null ||
                (Array.isArray(detail.units) && detail.units.length > 0))
        );
    }

    /**
     * After checkout / unit-checkout / reopen: apply server-returned detail and repaint chrome
     * before loadPage finishes — keeps footer (e.g. hide Save) in sync immediately after API success.
     */
    function applyPostMutationReservationDetail(detail) {
        if (!detailPatchUsable(detail)) {
            return;
        }

        pageCtx.detail = detail;
        markReservationBaseline(detail);
        pageCtx.useLocalFinancialTotals = false;
        pageCtx.pricingRateByLineKey = {};
        ingestCompanionsFromDetail(detail);
        ingestExtrasFromDetail(detail);
        ingestDiscountsFromDetail(detail);
        try {
            renderDetails(pageCtx.detail);
        } catch (err) {
            console.error("reservation-detail: renderDetails after server mutation failed", err);
        }

        hydratePricingFromServerDayRates({ keepServerFinancialTotals: true })
            .catch(() => {
                /* keep server financial snapshot from detail */
            })
            .finally(() => {
                pageCtx.useLocalFinancialTotals = false;
                renderReservationPeriodsUi(pageCtx.detail);
                syncFinancialUi({ skipFlash: true });
                const ug = $("#unitsGrid").dxDataGrid("instance");
                if (ug) {
                    ug.refresh();
                }
            });

        initFooter();
        refreshReservationOtherOptionsMenu();
    }

    /** If checkout succeeded but detail payload is missing, still flip UI so Save hides until reload. */
    function applyOptimisticReservationMarkedCheckedOut() {
        if (!pageCtx.detail) {
            return;
        }

        if (!pageCtx.detail.header || typeof pageCtx.detail.header !== "object") {
            pageCtx.detail.header = {};
        }

        pageCtx.detail.header.status = "CheckedOut";
        if (pageCtx.detail.financial && typeof pageCtx.detail.financial === "object") {
            pageCtx.detail.financial.balanceAmount = 0;
        }

        if (Array.isArray(pageCtx.detail.units)) {
            pageCtx.detail.units.forEach((u) => {
                if (u && typeof u === "object") {
                    u.unitStatus = "CheckedOut";
                }
            });
        }

        try {
            renderDetails(pageCtx.detail);
        } catch (err) {
            console.error("reservation-detail: renderDetails optimistic checkout failed", err);
        }

        initFooter();
    }

    function initFooter() {
        const $f = $("#resFooterBar").empty();

        $("<div>")
            .appendTo($f)
            .dxButton({
                text: t("reservationDetail.actions.cancel"),
                stylingMode: "text",
                type: "normal",
                onClick() {
                    window.location.href = "/room-board.html";
                }
            });

        $("<div>")
            .appendTo($f)
            .dxButton({
                text: t("reservationDetail.actions.save"),
                type: "default",
                stylingMode: "contained",
                visible:
                    canPersistReservationChanges() &&
                    (!pageCtx.routeId ||
                        !!pageCtx.checkoutUiPendingFirstSave ||
                        !isCheckedOutReservation()),
                onInitialized(e) {
                    pageCtx.saveBtnInst = e.component;
                },
                onClick() {
                    const creating = !pageCtx.routeId || pageCtx.isLocalNewReservation || pageCtx.isClientNewReservation;
                    if (creating && !requirePmsPermission(RESERVATION_DETAIL_PERMISSION_MATRIX.create)) {
                        return;
                    }

                    if (!creating && !canPersistReservationChanges()) {
                        notifyForbidden();
                        return;
                    }

                    saveReservation();
                }
            });

        const footOk = !!pageCtx.routeId && !pageCtx.checkoutUiPendingFirstSave;
        const showCheckIn =
            footOk &&
            !isCheckedOutReservation() &&
            !isCancelledReservation() &&
            !isCheckedInReservation() &&
            canCheckInReservation();
        const showCheckout =
            footOk &&
            !isCheckedOutReservation() &&
            !isCancelledReservation() &&
            isCheckedInReservation() &&
            canCheckoutReservation();

        $("<div>")
            .appendTo($f)
            .dxButton({
                text: t("reservationDetail.actions.checkIn"),
                type: "default",
                stylingMode: "contained",
                visible: showCheckIn,
                elementAttr: {
                    class: "res-footer-checkin res-footer-checkin--awaiting"
                },
                onClick() {
                    runCheckInFromFooter();
                }
            });

        $("<div>")
            .appendTo($f)
            .dxButton({
                text: hallCheckoutActionText(),
                hint: checkoutTaxInvoiceHintText(),
                type: "danger",
                stylingMode: "contained",
                visible: showCheckout,
                onClick() {
                    if (!requirePmsPermission("reservations.check_out")) {
                        return;
                    }

                    if (!ensureCheckoutDepartureAllowed()) {
                        return;
                    }

                    showCheckoutTaxInvoiceReminder();
                    openCheckoutStepperAndRun();
                }
            });

        $("<div>")
            .appendTo($f)
            .dxButton({
                text: t("reservationDetail.actions.recheckin"),
                type: "default",
                stylingMode: "contained",
                visible: footOk && isCheckedOutReservation() && canRecheckinReservation(),
                onClick() {
                    if (!requirePmsPermission("reservations.reopen")) {
                        return;
                    }

                    DevExpress.ui.dialog
                        .confirm(t("reservationDetail.confirm.recheckin"), t("reservationDetail.actions.recheckin"))
                        .done((yes) => {
                            if (!yes) {
                                return;
                            }

                            const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
                            lp.show();
                            window.Zaaer.ReservationDetailService.reopenReservationAfterCheckout(
                                pageCtx.routeId,
                                pageCtx.hotelIdParam
                            )
                                .then((detail) => {
                                    if (detailPatchUsable(detail)) {
                                        applyPostMutationReservationDetail(detail);
                                    } else {
                                        initFooter();
                                    }
                                    DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2200);
                                    return loadPage(false);
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        err && err.message ? String(err.message) : t("error.loadReservationDetail"),
                                        "error",
                                        4200
                                    );
                                })
                                .finally(() => lp.hide());
                        });
                }
            });
    }

    function buildUnitsPatchPayload() {
        const units = pageCtx.detail && Array.isArray(pageCtx.detail.units) ? pageCtx.detail.units : [];
        const list = [];

        for (let i = 0; i < units.length; i += 1) {
            const u = units[i] || {};
            const unitIdNum = Number(u.unitId);
            const apt = Number(u.apartmentId);
            const aptZ = u.apartmentZaaerId != null ? Number(u.apartmentZaaerId) : NaN;
            const item = {};

            if (Number.isFinite(unitIdNum) && unitIdNum > 0 && u.isPendingUnit !== true) {
                item.unitId = unitIdNum;
            }

            if (Number.isFinite(apt) && apt > 0) {
                item.apartmentId = apt;
            }

            if (Number.isFinite(aptZ) && aptZ > 0) {
                item.apartmentZaaerId = aptZ;
            }

            if (u.checkInDate != null) {
                item.checkInDate = u.checkInDate;
            }

            if (u.checkOutDate != null) {
                item.checkOutDate = u.checkOutDate;
            }

            if (u.departureDate != null) {
                item.departureDate = u.departureDate;
            }

            list.push(item);
        }

        return list;
    }

    /** Listed reservation without stay-date grant: do not send dates in PATCH (avoids false permission errors on price-only save). */
    function appendStayDateFieldsToPayload(payload, fields) {
        const { rental, checkIn, checkOut, months, nights, auto, monthlyCalendarMode, baseDates } = fields;
        const includeStayDates = isNewReservationForDateRules() || canEditStayDatesSection();

        if (includeStayDates) {
            if (!hasReservationPricingPeriods(pageCtx.detail)) {
                payload.rentalType = rental;
            }
            payload.checkInDate = checkIn || undefined;
            payload.checkOutDate = checkOut || undefined;
            payload.numberOfMonths = months !== undefined && months !== null ? Number(months) : undefined;
            payload.totalNights = nights !== undefined && nights !== null ? Number(nights) : undefined;
            if (`${rental || ""}`.trim().toLowerCase() === "monthly") {
                payload.monthlyCalendarMode = resolveEffectiveMonthlyCalendarMode(monthlyCalendarMode);
            }
        }

        if (shouldHideAutoExtendControl()) {
            payload.isAutoExtend = true;
        } else if (includeStayDates) {
            payload.isAutoExtend = auto;
        } else if (
            canPatchReservationField("autoExtend") &&
            Boolean(auto) !== Boolean(baseDates && baseDates.isAutoExtend)
        ) {
            payload.isAutoExtend = Boolean(auto);
        }
    }

    function buildPayload() {
        const kind = $("#resGeneralKind").dxSelectBox("instance").option("value");
        const arrival = isGuestArrivedSwitchOn() ? "arrived" : "not_arrived";
        const purpose = $("#resGeneralPurpose").dxSelectBox("instance").option("value");
        const source = $("#resGeneralSource").dxSelectBox("instance").option("value");
        const cmRaw = $("#resCmBookingNo").dxTextBox("instance").option("value");
        const rental = $("#resRentalGroup").dxButtonGroup("instance").option("selectedItemKeys")[0];
        const checkIn = getReservationCheckInCombined();
        const checkOut = getReservationCheckOutCombined();
        const months = $("#resMonths").dxNumberBox("instance").option("value");
        const nights = $("#resNights").dxNumberBox("instance").option("value");
        const auto = resolveIsAutoExtendForSave();

        const cmTrimmed = isReceptionReservationSource(source) ? "" : cmRaw != null ? `${cmRaw}`.trim() : "";
        void arrival;
        const reservationStatus = resolveReservationStatusForSave();
        const base = reservationBaseline();
        const baseDates = base.dates && typeof base.dates === "object" ? base.dates : {};
        const isFullSave = canSaveReservation();
        const isNewReservation = isNewReservationForDateRules();

        if (isFullSave || isNewReservation) {
            const payload = {
                reservationKind: kind,
                reservationStatus,
                visitPurposeId: purpose !== undefined && purpose !== null ? Number(purpose) : undefined,
                source,
                cmBookingNo: cmTrimmed,
                corporateId: currentCorporatePatchValue(kind)
            };

            appendStayDateFieldsToPayload(payload, {
                rental,
                checkIn,
                checkOut,
                months,
                nights,
                auto,
                monthlyCalendarMode: getSelectedMonthlyCalendarKey(),
                baseDates
            });

            if (kind === "individual") {
                payload.corporateId = undefined;
            }

            const cust = pageCtx.detail && pageCtx.detail.customerId;
            if (cust !== undefined && cust !== null && Number(cust) > 0) {
                payload.customerId = Number(cust);
            }

            payload.companions = buildCompanionsPatchPayload();

            const unitsPatch = buildUnitsPatchPayload();
            if (unitsPatch.length > 0) {
                payload.units = unitsPatch;
            }

            payload.extras = buildExtrasPatchPayload();
            return payload;
        }

        const payload = {};

        if (shouldHideAutoExtendControl()) {
            if (!Boolean(baseDates.isAutoExtend)) {
                payload.isAutoExtend = true;
            }
        } else if (
            canPatchReservationField("autoExtend") &&
            Boolean(auto) !== Boolean(baseDates.isAutoExtend)
        ) {
            payload.isAutoExtend = Boolean(auto);
        }

        if (
            canPatchReservationField("rentalType") &&
            !hasReservationPricingPeriods(pageCtx.detail) &&
            !sameStringValue(rental, baseDates.rentalType)
        ) {
            payload.rentalType = rental;
        }

        if (isPersistedReservation() && canPatchReservationField("stayDatesAfterCheckin")) {
            if (!sameDateTimeValue(checkIn, baseDates.checkInDate)) {
                payload.checkInDate = checkIn || undefined;
            }

            if (!sameDateTimeValue(checkOut, baseDates.checkOutDate)) {
                payload.checkOutDate = checkOut || undefined;
            }
        }

        if (canPatchReservationField("company")) {
            const corpValue = currentCorporatePatchValue(kind);
            if (kind === "individual" && base.corporateId != null) {
                payload.reservationKind = kind;
                payload.corporateId = undefined;
            } else if (kind === "company" && !sameNumberValue(corpValue, base.corporateId)) {
                payload.reservationKind = kind;
                payload.corporateId = corpValue;
            }
        }

        const unitsPatch = buildUnitsPatchPayload();
        if (
            unitsPatch.length > 0 &&
            shouldSendUnitsInPatch(unitsPatch) &&
            (canPatchReservationField("unitAdd") ||
                canPatchReservationField("unitRemove") ||
                canSaveReservation())
        ) {
            payload.units = unitsPatch;
        }

        const extrasPatch = buildExtrasPatchPayload();
        if (canPatchReservationField("package") && shouldSendExtrasInPatch(extrasPatch)) {
            payload.extras = extrasPatch;
        }

        const companionsPatch = buildCompanionsPatchPayload();
        if (
            canPersistCompanions() &&
            companionsReadyForPersist() &&
            shouldSendCompanionsInPatch(companionsPatch)
        ) {
            payload.companions = companionsPatch;
        }

        if (canSaveReservation()) {
            const baseStatus = normalizeHeaderReservationStatus(
                base.header && (base.header.status || base.header.reservationStatus)
            );
            if (!sameStringValue(reservationStatus, baseStatus)) {
                payload.reservationStatus = reservationStatus;
            }
        }

        // Keep read-only/general fields out of partial PATCH unless reservations.update.
        void purpose;
        void source;
        void cmTrimmed;
        void months;
        void nights;

        return payload;
    }

    function shouldSendExtrasInPatch(extrasPatch) {
        if (!isPersistedReservation()) {
            return (extrasPatch || []).length > 0;
        }

        const current = extrasPatch || [];
        const baseline = (pageCtx.persistedDetail && pageCtx.persistedDetail.extras) || [];
        if (current.length !== baseline.length) {
            return true;
        }

        const currentNames = current
            .map((r) => (r.itemName != null ? String(r.itemName).trim() : ""))
            .sort()
            .join("\u0001");
        const baselineNames = baseline
            .map((r) => (r.itemName != null ? String(r.itemName).trim() : ""))
            .sort()
            .join("\u0001");
        return currentNames !== baselineNames;
    }

    function companionPatchFingerprint(list) {
        return (list || [])
            .map(function (r) {
                const cid = r.customerId != null ? Number(r.customerId) : 0;
                const uid = r.unitId != null && r.unitId !== "" ? Number(r.unitId) : "";
                const rid = r.relationId != null && r.relationId !== "" ? Number(r.relationId) : "";
                return `${cid}|${uid}|${rid}`;
            })
            .sort()
            .join("\u0001");
    }

    function shouldSendCompanionsInPatch(companionsPatch) {
        const current = companionsPatch || [];
        if (!isPersistedReservation()) {
            return current.length > 0;
        }

        const baseline = (pageCtx.persistedDetail && pageCtx.persistedDetail.companions) || [];
        return companionPatchFingerprint(current) !== companionPatchFingerprint(baseline);
    }

    function buildExtrasPatchPayload() {
        const rows = pageCtx.extras || [];
        const list = [];
        for (let i = 0; i < rows.length; i += 1) {
            const r = rows[i] || {};
            const ru = r.unitId != null && r.unitId !== "" ? Number(r.unitId) : null;
            list.push({
                reservationUnitId: ru != null && Number.isFinite(ru) && ru > 0 ? ru : null,
                packageId: r.packageId != null && r.packageId !== "" && r.packageId !== "__ADD__" ? Number(r.packageId) : null,
                itemName: r.itemName != null ? String(r.itemName) : null,
                postingRule: r.postingRule || "OnCheckIn",
                serviceDate: r.serviceDate || null,
                guestCount: r.guestCount != null ? Number(r.guestCount) : 1,
                nightCount: r.nightCount != null ? Number(r.nightCount) : null,
                unitPrice: r.unitPrice != null ? Number(r.unitPrice) : null
            });
        }
        return list;
    }

    function resolvePrimaryApartmentRefForDraft() {
        const units =
            pageCtx.detail && Array.isArray(pageCtx.detail.units) ? pageCtx.detail.units : [];
        if (!units.length) {
            return null;
        }

        const u0 = units[0];
        const n = (x) => {
            if (x === undefined || x === null || x === "") {
                return null;
            }
            const v = Number(x);
            return Number.isFinite(v) && v > 0 ? v : null;
        };

        return n(u0.apartmentId) ?? n(u0.apartmentZaaerId) ?? n(u0.unitId);
    }

    function buildCompanionsPatchPayload() {
        const rows = pageCtx.companions || [];
        const list = [];

        for (let i = 0; i < rows.length; i += 1) {
            const r = rows[i] || {};
            const customerId = Number(r.customerId);
            if (!Number.isFinite(customerId) || customerId <= 0) {
                continue;
            }

            const unitParsed = coerceGridLookupScalar(r.unitId);

            const relRaw = r.relationId;
            const relationParsed = coerceGridLookupScalar(relRaw);

            const item = {
                customerId
            };
            const rkRaw = r.rowKey;
            if (rkRaw !== undefined && rkRaw !== null && rkRaw !== "") {
                const rk = Number(rkRaw);
                if (Number.isFinite(rk)) {
                    item.rowKey = rk;
                }
            }

            if (unitParsed != null && unitParsed > 0) {
                item.unitId = unitParsed;
                const uRow = ((pageCtx.detail && pageCtx.detail.units) || []).find((x) => Number(x.unitId) === unitParsed);
                const az = uRow && uRow.apartmentZaaerId != null ? Number(uRow.apartmentZaaerId) : NaN;
                if (Number.isFinite(az) && az > 0) {
                    item.apartmentZaaerId = az;
                }
            } else {
                continue;
            }

            if (Number.isFinite(relationParsed) && relationParsed > 0) {
                item.relationId = relationParsed;
            } else {
                continue;
            }

            list.push(item);
        }

        return list;
    }

    function hasMeaningfulFinancial(fin) {
        if (!fin || typeof fin !== "object") {
            return false;
        }

        const total = Number(fin.totalAmount);
        const sub = Number(fin.subtotal);
        return (Number.isFinite(total) && total > 0) || (Number.isFinite(sub) && sub > 0);
    }

    function mergeReservationDetailAfterDraft(created) {
        const prev = pageCtx.detail || {};
        const extraPrev = Array.isArray(prev.extras) ? prev.extras : [];
        const prevUnits = Array.isArray(prev.units) ? prev.units : [];
        const createdUnits = Array.isArray(created.units) ? created.units : [];
        const prevHasPending = prevUnits.some((u) => Number(u.unitId) < 0);
        const needsMergedUnits = prevUnits.length > createdUnits.length || prevHasPending;

        const mergedUnits = needsMergedUnits
            ? prevUnits.map((u, idx) => {
                  if (idx === 0 && createdUnits[0]) {
                      const c0 = createdUnits[0];
                      return {
                          ...u,
                          unitId: c0.unitId,
                          unitZaaerId: c0.unitZaaerId ?? u.unitZaaerId,
                          unitStatus: c0.unitStatus ?? u.unitStatus
                      };
                  }
                  return u;
              })
            : createdUnits;

        return {
            ...created,
            units: mergedUnits,
            guests:
                prev.guests && Array.isArray(prev.guests) && prev.guests.length
                    ? prev.guests.slice()
                    : created.guests,
            customerId:
                prev.customerId != null && Number(prev.customerId) > 0
                    ? Number(prev.customerId)
                    : created.customerId,
            extras: extraPrev.length ? extraPrev : created.extras || [],
            corporateId: (function () {
                const pms = window.Zaaer.PmsCorporateCustomerService;
                if (created && created.company && pms && typeof pms.reservationCorporateId === "function") {
                    const r = pms.reservationCorporateId(created.company);
                    if (r != null && Number(r) > 0) {
                        return Number(r);
                    }
                }
                if (prev.corporateId != null && Number(prev.corporateId) > 0) {
                    return Number(prev.corporateId);
                }
                if (created.corporateId != null && Number(created.corporateId) > 0) {
                    return Number(created.corporateId);
                }
                return null;
            })(),
            companions:
                pageCtx.companions && pageCtx.companions.length
                    ? pageCtx.companions.slice()
                    : Array.isArray(created.companions) && created.companions.length
                      ? created.companions.slice()
                      : [],
            financial: prev.financial && hasMeaningfulFinancial(prev.financial) ? prev.financial : created.financial
        };
    }

    /** Hall / venue bookings — lighter validation (no hotel source, nights, or unit rent rules). */
    function validateHallReservationBeforeSave() {
        const errors = [];

        const ci = getReservationCheckInCombined();
        const co = getReservationCheckOutCombined();
        if (!ci || !co) {
            errors.push(t("reservationDetail.validation.datesRequired"));
        } else if (ci.getTime() >= co.getTime()) {
            errors.push(t("reservationDetail.validation.datesMustDiffer"));
        }

        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        if (!units.length) {
            errors.push(t("reservationDetail.validation.unitRequired"));
        }

        const guestGrid = $("#guestsGrid").dxDataGrid("instance");
        const guestData = guestGrid ? guestGrid.option("dataSource") || [] : [];
        validatePrimaryGuestProfile(guestData).forEach(function (msg) {
            errors.push(msg);
        });

        return errors;
    }

    /** Validates all required fields before saving/updating a reservation.
     *  Returns an array of error message strings; empty array means valid. */
    function validateReservationBeforeSave() {
        if (pageCtx.isHallProperty) {
            return validateHallReservationBeforeSave();
        }

        const errors = [];

        // 1. Booking source
        const sourceInst = $("#resGeneralSource").dxSelectBox("instance");
        if (!sourceInst || !sourceInst.option("value")) {
            errors.push(t("reservationDetail.validation.sourceRequired"));
        }

        // 2. Visit purpose
        const purposeInst = $("#resGeneralPurpose").dxSelectBox("instance");
        const purposeVal = purposeInst ? purposeInst.option("value") : undefined;
        if (purposeVal === null || purposeVal === undefined) {
            errors.push(t("reservationDetail.validation.purposeRequired"));
        }

        // 3. Dates and duration
        const ci = getReservationCheckInCombined();
        const co = getReservationCheckOutCombined();
        if (!ci || !co) {
            errors.push(t("reservationDetail.validation.datesRequired"));
        } else if (ci.getTime() >= co.getTime()) {
            errors.push(t("reservationDetail.validation.datesMustDiffer"));
        } else {
            const isMonthly = isMonthlyRentalMode();
            if (isMonthly) {
                const monthsInst = $("#resMonths").dxNumberBox("instance");
                const months = monthsInst ? Number(monthsInst.option("value") || 0) : 0;
                if (months <= 0) {
                    errors.push(t("reservationDetail.validation.monthsRequired"));
                }
            } else {
                const nightsInst = $("#resNights").dxNumberBox("instance");
                const nights = nightsInst ? Number(nightsInst.option("value") || 0) : 0;
                if (nights <= 0) {
                    errors.push(t("reservationDetail.validation.nightsRequired"));
                }
            }
        }

        // 4. Units — must exist, and no unit may have zero rent
        const units = (pageCtx.detail && pageCtx.detail.units) || [];
        if (!units.length) {
            errors.push(t("reservationDetail.validation.unitRequired"));
        } else {
            const hasZeroRent = units.some(function (u) {
                return getUnitLastNightRate(u) <= 0;
            });
            if (hasZeroRent) {
                errors.push(t("reservationDetail.validation.unitRentRequired"));
            }
        }

        const kindInst = $("#resGeneralKind").dxSelectBox("instance");
        const reservationKind = kindInst ? kindInst.option("value") : "individual";
        if (
            reservationKind === "company" &&
            !(pageCtx.detail && (pageCtx.detail.corporateId || pageCtx.detail.company))
        ) {
            errors.push(t("reservationDetail.validation.companyRequired"));
        }

        // 5. Guest — primary visitor profile must be complete (same rules as guest form)
        const guestGrid = $("#guestsGrid").dxDataGrid("instance");
        const guestData = guestGrid ? guestGrid.option("dataSource") || [] : [];
        validatePrimaryGuestProfile(guestData).forEach(function (msg) {
            errors.push(msg);
        });

        // 6. Companions — if any exist, each must have unit and relation
        const companions = pageCtx.companions || [];
        if (companions.length > 0 && !companionsReadyForPersist(companions)) {
            errors.push(t("reservationDetail.validation.companionUnitRelationRequired"));
        }

        return errors;
    }

    function validatePrimaryGuestProfile(guestData) {
        const errors = [];
        const list = Array.isArray(guestData) ? guestData : [];
        if (!list.length) {
            errors.push(t("reservationDetail.validation.guestRequired"));
            return errors;
        }

        const primary = list.find((g) => g && g.isPrimary) || list[0];
        if (!primary) {
            errors.push(t("reservationDetail.validation.guestRequired"));
            return errors;
        }

        const name = String(primary.customerName || "").trim();
        if (!name) {
            errors.push(t("reservationDetail.guest.validationName"));
        }

        const gender = String(primary.gender || "").trim();
        if (!gender) {
            errors.push(t("reservationDetail.guest.validationGender"));
        }

        const gtypeId = primary.gtypeId != null ? Number(primary.gtypeId) : 0;
        if (!Number.isFinite(gtypeId) || gtypeId <= 0) {
            errors.push(t("reservationDetail.guest.validationLookups"));
        }

        const nationalityId =
            primary.nationalityId != null
                ? Number(primary.nationalityId)
                : primary.nId != null
                  ? Number(primary.nId)
                  : 0;
        const hasNationality =
            (Number.isFinite(nationalityId) && nationalityId > 0) ||
            String(primary.nationalityName || primary.nationalityNameAr || "").trim();
        if (!hasNationality) {
            errors.push(t("reservationDetail.guest.validationLookups"));
        }

        const birth = primary.birthDate;
        if (!birth || (birth instanceof Date && Number.isNaN(birth.getTime()))) {
            errors.push(t("reservationDetail.guest.validationBirth"));
        }

        const idNumber = String(primary.idNumber || "").trim();
        const idTypeLabel = String(primary.idTypeName || primary.idTypeNameAr || "").trim();
        if (!idNumber || !idTypeLabel) {
            errors.push(t("reservationDetail.guest.validationIds"));
        }

        const mobileDigits = String(primary.mobileNo || "").replace(/\D/g, "");
        if (!mobileDigits || mobileDigits.length < 11) {
            errors.push(t("reservationDetail.guest.validationMobile"));
        } else if (!mobileDigits.startsWith("966")) {
            errors.push(t("reservationDetail.guest.validationMobile"));
        }

        return errors;
    }

    function showReservationValidationDialog(validationErrors) {
        const listHtml = validationErrors
            .map(function (e) {
                return "<li style=\"margin-bottom:6px\">" + e + "</li>";
            })
            .join("");
        DevExpress.ui.dialog.alert(
            "<ul style=\"margin:8px 0 4px 0;padding-right:20px;text-align:right\">" + listHtml + "</ul>",
            t("reservationDetail.validation.title")
        );
    }

    /** Same rules as save — blocks check-in and financial vouchers until the reservation is complete. */
    function ensureReservationCompleteForOperations() {
        mergeOpenEditorsIntoDetail(pageCtx.detail);
        commitOpenUnitPricingGridToRateMap();
        const validationErrors = validateReservationBeforeSave();
        if (validationErrors.length > 0) {
            showReservationValidationDialog(validationErrors);
            return false;
        }
        return true;
    }

    function saveReservation() {
        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");

        // Run validation before showing the load panel
        mergeOpenEditorsIntoDetail(pageCtx.detail);
        commitOpenUnitPricingGridToRateMap();
        normalizePricingRateByLineKey();
        const validationErrors = validateReservationBeforeSave();
        if (validationErrors.length > 0) {
            showReservationValidationDialog(validationErrors);
            return;
        }

        if (!reservationSaveGuard.begin()) {
            return;
        }

        const sg = window.Zaaer && window.Zaaer.SaveGuard;
        if (sg && pageCtx.saveBtnInst) {
            sg.setButtonDisabled(pageCtx.saveBtnInst, true);
        }

        const needDraft = pageCtx.isClientNewReservation || !pageCtx.routeId;
        pageCtx._forceFullSavePayload = needDraft;

        let createPromise = Promise.resolve(null);
        let skipPatchAfterCreate = false;

        if (needDraft) {
            const apt = resolvePrimaryApartmentRefForDraft();
            if (!apt) {
                reservationSaveGuard.end();
                if (sg && pageCtx.saveBtnInst) {
                    sg.setButtonDisabled(pageCtx.saveBtnInst, false);
                }
                lp.hide();
                DevExpress.ui.notify(t("reservationDetail.draftNeedsUnit"), "warning", 3800);
                return;
            }

            const prevDraftUnits =
                pageCtx.detail && Array.isArray(pageCtx.detail.units) ? pageCtx.detail.units.slice() : [];
            const createPayload = buildPayload();
            createPromise = window.Zaaer.ReservationDetailService.createReservation(apt, createPayload).then((created) => {
                skipPatchAfterCreate = true;
                applyReservationRouteFromDetail(created, { skipUrlSync: true });
                pageCtx.isClientNewReservation = false;
                pageCtx.detail = created;
                markReservationBaseline(created);
                ingestCompanionsFromDetail(created);
                rekeyPricingRateByLineKeyAfterSave(prevDraftUnits, pageCtx.detail.units || []);
                applyReservationEditorsPermissionState();
                return created;
            });
        }

        Promise.resolve()
            .then(function () {
                return ensureFreshPermissions();
            })
            .then(function () {
                lp.show();
                return createPromise;
            })
            .then(function () {
                if (skipPatchAfterCreate) {
                    return pageCtx.detail;
                }

                if (pageCtx.detail) {
                    mergeOpenEditorsIntoDetail(pageCtx.detail);
                    commitOpenUnitPricingGridToRateMap();
                    normalizePricingRateByLineKey();
                    onReservationStayDatesChanged();
                }

                const patchPayload = buildPayload();
                if (!hasPayloadValues(patchPayload)) {
                    return pageCtx.detail;
                }

                return window.Zaaer.ReservationDetailService.patchReservation(
                    pageCtx.routeId,
                    patchPayload,
                    pageCtx.hotelIdParam
                );
            })
            .then(function (data) {
                const prevUnits =
                    pageCtx.detail && Array.isArray(pageCtx.detail.units) ? pageCtx.detail.units.slice() : [];

                pageCtx.detail = data;
                markReservationBaseline(data);
                ingestCompanionsFromDetail(data);
                const rk = preferZaaerRouteKey(data);
                if (rk != null) {
                    pageCtx.routeId = rk;
                }

                rekeyPricingRateByLineKeyAfterSave(prevUnits, data.units || []);
                normalizePricingRateByLineKey();

                if (pageCtx.isLocalNewReservation) {
                    pageCtx.isLocalNewReservation = false;
                    const u = new URL(window.location.href);
                    u.searchParams.delete("newReservation");
                    if (rk != null) {
                        u.searchParams.set("id", String(rk));
                    }
                    window.history.replaceState({}, "", u.toString());
                    setReservationHeaderVisible(true);
                    syncCmBookingVisibility(
                        $("#resGeneralSource").dxSelectBox("instance")
                            ? $("#resGeneralSource").dxSelectBox("instance").option("value")
                            : null
                    );
                    const statusInst = $("#resGeneralStatus").dxSelectBox("instance");
                    if (statusInst && !isGuestArrivedSwitchOn()) {
                        statusInst.option("disabled", false);
                    }
                } else if (rk != null) {
                    syncUrlReservationIdParam(rk);
                }

                const hotelForRates = pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId);
                normalizePricingRateByLineKey();
                const drPayload =
                    canApplyUnitPricingFromPopup() ? buildUnitDayRatesSavePayload() : { items: [] };

                const finishSaveUi = () => {
                    pageCtx.useLocalFinancialTotals = false;
                    pageCtx.pricingRateByLineKey = {};
                    ingestExtrasFromDetail(pageCtx.detail);
                    ingestDiscountsFromDetail(pageCtx.detail);
                    DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2200);
                    renderDetails(pageCtx.detail);
                    pageCtx.checkoutUiPendingFirstSave = false;
                    initFooter();
                    refreshNotesBadge();
                    return hydratePricingFromServerDayRates({ keepServerFinancialTotals: true })
                        .catch(() => {
                            /* keep server financial snapshot from detail */
                        })
                        .finally(() => {
                            pageCtx.useLocalFinancialTotals = false;
                            syncFinancialUi({ skipFlash: true });
                            const ug = $("#unitsGrid").dxDataGrid("instance");
                            if (ug) {
                                ug.refresh();
                            }
                        });
                };

                const afterHallScheduleSync = () => {
                    if (!drPayload.items.length) {
                        return finishSaveUi();
                    }

                    return window.Zaaer.ReservationDetailService.saveUnitDayRates(
                    pageCtx.routeId,
                    drPayload,
                    hotelForRates
                ).then((saved) => {
                    const fin = (pageCtx.detail && pageCtx.detail.financial) || {};
                    const paid = Number(fin.amountPaid) || 0;
                    const totalExtra = roundMoney(
                        (pageCtx.extras || []).reduce((sum, row) => sum + (Number(row.totalAmount) || 0), 0)
                    );
                    const unitTotal = Number(saved.summary && saved.summary.total) || 0;
                    const grand = roundMoney(unitTotal + totalExtra);
                    pageCtx.detail.financial = {
                        ...fin,
                        subtotal: saved.summary.subtotal,
                        totalTaxAmount:
                            (Number(saved.summary.ewaAmount) || 0) + (Number(saved.summary.vatAmount) || 0),
                        totalAmount: grand,
                        totalExtra: totalExtra,
                        balanceAmount: roundMoney(grand - paid)
                    };
                    pageCtx.pricingRateByLineKey = {};
                    mergeLoadedUnitDayRatesIntoPricingMap(saved);
                    pageCtx.useLocalFinancialTotals = false;
                    markReservationBaseline(pageCtx.detail);
                    return finishSaveUi();
                });
                };

                if (pageCtx.isHallProperty) {
                    return syncHallEventScheduleAfterSave().then(() => afterHallScheduleSync());
                }

                return afterHallScheduleSync();
            })
            .catch(function (err) {
                console.error("reservation-detail: saveReservation failed", err);
                const status = err && (err.status || err.statusCode);
                if (status === 403) {
                    const permCode =
                        err &&
                        err.responseJSON &&
                        (err.responseJSON.permissionCode || err.responseJSON.PermissionCode);
                    const msg = permCode
                        ? t("reservationDetail.permissions.missingCode").replace("{0}", permCode)
                        : t("common.forbidden");
                    DevExpress.ui.notify(msg, "warning", 4200);
                } else {
                    DevExpress.ui.notify(
                        (err && err.message) || t("error.saveReservationDetail"),
                        "error",
                        3400
                    );
                }
            })
            .finally(function () {
                delete pageCtx._forceFullSavePayload;
                reservationSaveGuard.end();
                if (sg && pageCtx.saveBtnInst) {
                    sg.setButtonDisabled(pageCtx.saveBtnInst, false);
                }
                lp.hide();
            });
    }

    function formatReservationHeaderStatusLabel(status) {
        const x = `${status || ""}`.trim().toLowerCase().replace(/[\s_-]+/g, "");
        if (x === "unconfirmed") {
            return t("reservationDetail.status.pendingAddition");
        }

        if (x === "confirmed") {
            return t("reservationDetail.status.confirmed");
        }

        if (x === "checkedin" || x === "checkin") {
            return t("reservationDetail.status.checkedIn");
        }

        if (x === "checkedout" || x === "checkout") {
            return t("reservationDetail.status.checkedOut");
        }

        if (x === "cancelled") {
            return t("reservationDetail.status.cancelled");
        }

        const raw = `${status || ""}`.trim();
        return raw || "—";
    }

    function sectionShell(id, tagKey, $body, $headEnd) {
        const compactSectionIds = ["res-section-header", "res-section-general", "res-section-dates"];
        const compactClass = compactSectionIds.includes(id) ? " res-section--compact" : "";
        const $sec = $(`<section class="res-section res-anchor-section${compactClass}" id="${id}"></section>`);
        const $head = $("<div>").addClass("res-section-head");
        const $start = $("<div>").addClass("res-section-head-start");
        $("<span>").addClass("res-section-tag").text(t(tagKey)).appendTo($start);
        $head.append($start);
        if ($headEnd && $headEnd.length) {
            $("<div>").addClass("res-section-head-end").append($headEnd).appendTo($head);
        }

        $sec.append($head);
        const $wrap = $("<div>").addClass("res-section-body").appendTo($sec);
        $wrap.append($body);
        return $sec;
    }

    function isDatesSectionCompactLayout() {
        const section = document.getElementById("res-section-dates");
        if (section) {
            const width = section.getBoundingClientRect().width;
            if (width > 0 && width < 720) {
                return true;
            }
        }

        return window.matchMedia("(max-width: 960px)").matches;
    }

    /** Mobile / narrow panel: rental + calendar toggles sit on the dates section title row. */
    function syncDatesTogglesPlacement() {
        const $section = $("#res-section-dates");
        const $rental = $("#res-section-dates .res-date-cell--rental");
        const $calendar = $("#res-section-dates .res-date-cell--calendar");
        const $bodySlot = $("#res-date-core-toggles-slot");
        const $headSlot = $("#res-dates-head-toggles-slot");
        if (!$section.length || !$rental.length || !$calendar.length || !$bodySlot.length || !$headSlot.length) {
            return;
        }

        const compact = isDatesSectionCompactLayout();
        if (compact) {
            $headSlot.append($rental.detach(), $calendar.detach());
            $section.addClass("res-dates--mobile-head-toggles res-dates--h-scroll");
        } else {
            $bodySlot.append($rental.detach(), $calendar.detach());
            $section.removeClass("res-dates--mobile-head-toggles res-dates--h-scroll");
        }
    }

    function bindDatesSectionLayoutObserver() {
        const section = document.getElementById("res-section-dates");
        if (!section || section._datesLayoutObserverBound) {
            return;
        }

        section._datesLayoutObserverBound = true;
        let rafId = 0;

        const schedule = () => {
            if (rafId) {
                return;
            }

            rafId = window.requestAnimationFrame(() => {
                rafId = 0;
                syncDatesTogglesPlacement();
            });
        };

        if (typeof ResizeObserver !== "undefined") {
            const observer = new ResizeObserver(schedule);
            observer.observe(section);
            section._datesLayoutObserver = observer;
        }

        window.addEventListener("resize", schedule, { passive: true });
    }

    function renderDetails(d) {
        if (!d || typeof d !== "object") {
            return;
        }

        const hc = d.hotelCode != null && `${d.hotelCode}`.trim() !== "" ? String(d.hotelCode).trim() : "";
        if (hc) {
            window.Zaaer.ApiService.setHotelCode(hc);
        }

        const header = d.header && typeof d.header === "object" ? d.header : {};
        const general = d.general && typeof d.general === "object" ? d.general : {};
        const dates = d.dates && typeof d.dates === "object" ? d.dates : {};

        $("#resHeaderNo").text(header.reservationNo || "—");
        $("#resHeaderSource").text(header.source || "—");
        $("#resHeaderGuest").text(header.mainGuestName || "—");
        syncReservationHeaderKvCards(d);

        suppressDateDurationSync = true;
        try {
            const reservationKind = normalizeReservationKindKey(general.reservationType, d);
            $("#resGeneralKind").dxSelectBox("instance").option("value", reservationKind);
            $("#resGeneralStatus")
                .dxSelectBox("instance")
                .option("value", normalizeReservationGeneralStatus(header.status));
            if (normalizeHeaderReservationStatus(header.status || header.reservationStatus) === "checked_in") {
                pageCtx.arrivalSwitchLockedFromUndo = false;
            }

            syncArrivalSwitchFromReservationDetail();

            $("#resGeneralPurpose").dxSelectBox("instance").option("value", general.visitPurposeId ?? null);

            const srcItems = pageCtx.sources || [];
            const srcRaw = general.source || header.source || "";
            const matchSrc =
                srcItems.find((x) => x.code === srcRaw || x.name === srcRaw || x.nameAr === srcRaw) || null;
            $("#resGeneralSource").dxSelectBox("instance").option("value", matchSrc ? matchSrc.code : srcRaw || null);
            $("#resCmBookingNo").dxTextBox("instance").option("value", general.cmBookingNo ?? "");
            syncCmBookingVisibility(matchSrc ? matchSrc.code : srcRaw || null);

            $("#resRentalGroup").dxButtonGroup("instance").option("selectedItemKeys", [normRental(dates.rentalType)]);
            syncMonthlyCalendarControlFromEffectiveMode(normMonthlyCalendarMode(dates.monthlyCalendarMode));
            setReservationCheckInFromDateTime(dates.checkInDate ? new Date(dates.checkInDate) : null);
            setReservationCheckOutFromDateTime(dates.checkOutDate ? new Date(dates.checkOutDate) : null);
            $("#resMonths").dxNumberBox("instance").option("value", dates.numberOfMonths ?? null);
            $("#resNights").dxNumberBox("instance").option("value", dates.totalNights ?? null);
            $("#resAutoExtend")
                .dxSwitch("instance")
                .option("value", shouldHideAutoExtendControl() ? true : !!dates.isAutoExtend);

            applyRentalDurationVisibility();
            syncDurationFieldsFromDates({ flash: false, skipFinancialRecompute: true });
            pageCtx._pricingRentalMode = getSelectedRentalKey();
            pageCtx._monthlyCalendarMode = resolveEffectiveMonthlyCalendarMode(dates.monthlyCalendarMode);
            if (pageCtx.isHallProperty) {
                syncHallHijriFromGregorianCheckIn();
            }
        } finally {
            suppressDateDurationSync = false;
        }

        renderReservationPeriodsUi(d);

        syncFinancialUi({ skipFlash: true });

        const ug = $("#unitsGrid").dxDataGrid("instance");
        if (ug) {
            ug.option("columns", buildUnitsGridColumns());
            ug.option("rtlEnabled", isArabic());
            applyUnitsGridLayoutOptions();
            const unitRows = d.units || [];
            if (
                unitRows.length &&
                getReservationCheckInCombined() &&
                getReservationCheckOutCombined()
            ) {
                syncNonCheckedInUnitsWithReservationDates();
            }
            setGridDataSourceIfChanged(ug, (pageCtx.detail && pageCtx.detail.units) || unitRows);
        }
        refreshCompanyGrid();
        refreshGuestGridColumns();
        setGridDataSourceIfChanged($("#guestsGrid").dxDataGrid("instance"), d.guests || []);
        updateGuestsGridShellVisibility();
        syncLodgingPartyCards();

        refreshCompanionsGrid();
        refreshExtrasGrid();

        setCompanySectionVisible(isCompanyReservationKind(d));

        syncReservationDependentGridsChrome();
        syncHallMainGuestEditButton();
        applyReservationPermissionBanner();
        applyReservationEditorsPermissionState();
        refreshReservationOtherOptionsMenu();
    }

    function toggleCompanyEditors(disableEditors) {
        const $pick = $("#btnPickCompany");
        if ($pick.length) {
            try {
                const pick = $pick.dxButton("instance");
                if (pick) {
                    pick.option("disabled", disableEditors);
                }
            } catch (err) {
                if (!err || !`${err.message}`.includes("E0009")) {
                    throw err;
                }
            }
        }

        const $grid = $("#companyGrid");
        if ($grid.length) {
            try {
                const grid = $grid.dxDataGrid("instance");
                if (grid) {
                    grid.option("disabled", disableEditors);
                }
            } catch (err) {
                if (!err || !`${err.message}`.includes("E0009")) {
                    throw err;
                }
            }
        }
    }

    function buildCompanyGridColumns() {
        const actionsOff = reservationGridsActionsDisabled();

        const dataCols = [
            { dataField: "corporateName", caption: t("reservationDetail.company.name"), minWidth: 150 },
            { dataField: "corNo", caption: t("reservationDetail.company.corNo"), width: 120 },
            {
                dataField: "vatRegistrationNo",
                caption: t("reservationDetail.company.vatReg"),
                width: 170,
                allowSorting: false
            },
            {
                dataField: "commercialRegistrationNo",
                caption: t("reservationDetail.company.commercialReg"),
                width: 130,
                allowSorting: false
            },
            { dataField: "postalCode", caption: t("reservationDetail.company.postalCode"), width: 100, visible: false }
        ];

        const actionCol = {
            type: "buttons",
            name: "companyActions",
            width: 110,
            caption: t("reservationDetail.units.actions"),
            fixed: true,
            fixedPosition: reservationGridActionFixedPosition(),
            visible: !actionsOff,
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: t("reservationDetail.actions.edit"),
                    icon: "edit",
                    onClick() {
                        openCorporateEdit();
                    }
                },
                {
                    hint: t("reservationDetail.actions.delete"),
                    icon: "trash",
                    onClick() {
                        DevExpress.ui.dialog
                            .confirm(t("reservationDetail.confirm.deleteCompany"), t("reservationDetail.actions.delete"))
                            .done((ok) => {
                                if (ok) {
                                    removeCorporate();
                                }
                            });
                    }
                }
            ]
        };

        const full = isArabic() ? [actionCol, ...dataCols] : [...dataCols, actionCol];
        return pickResDetailMobileColumns(full, ["corporateName", "corNo"], 2);
    }

    function refreshCompanyGrid() {
        const d = pageCtx.detail;
        const rows = d && d.company ? [d.company] : [];
        const grid = $("#companyGrid").dxDataGrid("instance");
        if (grid) {
            grid.option("columns", buildCompanyGridColumns());
            grid.option("rtlEnabled", isArabic());
            setGridDataSourceIfChanged(grid, rows);
        }

        toggleCompanyEditors(!isCompanyReservationKind(d));
        if (pageCtx.isLodgingProperty) {
            syncLodgingPartyCards();
        }
    }

    function normalizeCompanyGridRow(corpOrRow) {
        const c = corpOrRow && typeof corpOrRow === "object" ? corpOrRow : {};
        const internalId = Number(c.corporateId ?? c.CorporateId);
        const zaaerIdRaw = c.zaaerId ?? c.ZaaerId;

        return {
            corporateId: Number.isFinite(internalId) && internalId > 0 ? internalId : null,
            zaaerId:
                zaaerIdRaw != null && `${zaaerIdRaw}`.trim() !== "" && Number.isFinite(Number(zaaerIdRaw))
                    ? Number(zaaerIdRaw)
                    : null,
            corporateName: `${c.corporateName ?? c.CorporateName ?? ""}`.trim() || "—",
            corNo: `${c.corNo ?? c.CorNo ?? ""}`.trim(),
            vatRegistrationNo: `${c.vatRegistrationNo ?? c.VatRegistrationNo ?? ""}`.trim(),
            commercialRegistrationNo: `${c.commercialRegistrationNo ?? c.CommercialRegistrationNo ?? ""}`.trim(),
            postalCode: c.postalCode ?? c.PostalCode ?? "",
            country: c.country ?? c.Country ?? "",
            city: c.city ?? c.City ?? "",
            address: c.address ?? c.Address ?? "",
            corporatePhone: c.corporatePhone ?? c.CorporatePhone ?? "",
            email: c.email ?? c.Email ?? ""
        };
    }

    function applyCorporateLocal(corporateId, corpOrRow) {
        if (!pageCtx.detail) {
            return;
        }

        mergeOpenEditorsIntoDetail(pageCtx.detail);

        const pmsCorp = window.Zaaer.PmsCorporateCustomerService;
        let routeId = corporateId != null ? Number(corporateId) : NaN;
        if ((!Number.isFinite(routeId) || routeId <= 0) && corpOrRow && pmsCorp && typeof pmsCorp.reservationCorporateId === "function") {
            const resolved = pmsCorp.reservationCorporateId(corpOrRow);
            if (resolved != null && Number(resolved) > 0) {
                routeId = Number(resolved);
            }
        }

        if (!Number.isFinite(routeId) || routeId <= 0) {
            DevExpress.ui.notify(t("reservationDetail.company.pickInvalid"), "warning", 2800);
            return;
        }

        const base = corpOrRow && typeof corpOrRow === "object" ? corpOrRow : {};
        pageCtx.detail.corporateId = routeId;
        pageCtx.detail.company = normalizeCompanyGridRow(
            Object.assign({}, base, {
                corporateId: base.corporateId ?? base.CorporateId ?? routeId
            })
        );

        const kindInst = $("#resGeneralKind").dxSelectBox("instance");
        if (kindInst) {
            kindInst.option("value", "company");
        }

        setCompanySectionVisible(true);
        refreshCompanyGrid();
        applyReservationEditorsPermissionState();

        DevExpress.ui.notify(t("reservationDetail.company.assignedLocal"), "success", 2200);
    }

    /** After PMS corporate update API — keep company on draft reservation without full loadPage. */
    function applyCorporateEditLocally(formData, apiRow) {
        if (!pageCtx.detail) {
            return;
        }

        mergeOpenEditorsIntoDetail(pageCtx.detail);

        const pmsCorp = window.Zaaer.PmsCorporateCustomerService;
        const prevRouteId = pageCtx.detail.corporateId;
        const merged = Object.assign({}, pageCtx.detail.company || {}, formData || {}, apiRow || {});
        pageCtx.detail.company = normalizeCompanyGridRow(merged);

        let routeId = prevRouteId != null ? Number(prevRouteId) : NaN;
        if (pmsCorp && typeof pmsCorp.reservationCorporateId === "function") {
            const resolved = pmsCorp.reservationCorporateId(pageCtx.detail.company);
            if (resolved != null && Number(resolved) > 0) {
                routeId = Number(resolved);
            }
        }

        if (!Number.isFinite(routeId) || routeId <= 0) {
            const cid = Number(merged.corporateId ?? merged.CorporateId);
            if (Number.isFinite(cid) && cid > 0) {
                routeId = cid;
            }
        }

        if (Number.isFinite(routeId) && routeId > 0) {
            pageCtx.detail.corporateId = routeId;
        }

        const kindInst = $("#resGeneralKind").dxSelectBox("instance");
        if (kindInst) {
            kindInst.option("value", "company");
        }

        setCompanySectionVisible(true);
        refreshCompanyGrid();
        applyReservationEditorsPermissionState();
    }

    function clearCorporateLocal() {
        if (!pageCtx.detail) {
            return;
        }

        mergeOpenEditorsIntoDetail(pageCtx.detail);
        pageCtx.detail.corporateId = null;
        pageCtx.detail.company = null;

        const kindInst = $("#resGeneralKind").dxSelectBox("instance");
        const stayCompanyKind = kindInst && kindInst.option("value") === "company";
        if (!stayCompanyKind && kindInst) {
            kindInst.option("value", "individual");
        }

        setCompanySectionVisible(stayCompanyKind);
        refreshCompanyGrid();
        applyReservationEditorsPermissionState();
    }

    function removeCorporate() {
        if (pageCtx.isClientNewReservation || !pageCtx.routeId) {
            clearCorporateLocal();
            DevExpress.ui.notify(t("reservationDetail.company.removedLocal"), "success", 2000);
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        lp.show();
        window.Zaaer.ReservationDetailService.patchReservation(
            pageCtx.routeId,
            { reservationKind: "individual" },
            pageCtx.hotelIdParam
        )
            .then((data) => {
                pageCtx.detail = data;
                markReservationBaseline(data);
                applyReservationRouteFromDetail(data);
                DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                renderDetails(pageCtx.detail);
            })
            .catch(() => DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200))
            .finally(() => lp.hide());
    }

    function assignCorporate(corporateId, corpOrRow) {
        const pmsCorp = window.Zaaer.PmsCorporateCustomerService;
        let routeId = corporateId;
        if (corpOrRow && pmsCorp && typeof pmsCorp.reservationCorporateId === "function") {
            const resolved = pmsCorp.reservationCorporateId(corpOrRow);
            if (resolved) {
                routeId = resolved;
            }
        }

        if (pageCtx.isClientNewReservation || !pageCtx.routeId) {
            applyCorporateLocal(routeId, corpOrRow);
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        lp.show();
        window.Zaaer.ReservationDetailService.patchReservation(
            pageCtx.routeId,
            { reservationKind: "company", corporateId: routeId },
            pageCtx.hotelIdParam
        )
            .then((data) => {
                pageCtx.detail = data;
                markReservationBaseline(data);
                applyReservationRouteFromDetail(data);
                $("#resGeneralKind").dxSelectBox("instance").option("value", "company");
                DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                renderDetails(pageCtx.detail);
            })
            .catch(() => DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200))
            .finally(() => lp.hide());
    }

    function unwrapCustomerApiPayload(res) {
        if (!res || typeof res !== "object") {
            return null;
        }

        if (res.data !== undefined && res.data !== null) {
            return res.data;
        }

        if (res.Data !== undefined && res.Data !== null) {
            return res.Data;
        }

        return res;
    }

    function pickPrimaryIdentification(cust) {
        const idents = cust.identifications || cust.Identifications;
        const list = Array.isArray(idents) ? idents : [];
        const prim = list.find((i) => i && (i.isPrimary || i.IsPrimary)) || list[0];
        return prim || null;
    }

    function buildGuestSlotFromCustomer(customerId, cust) {
        const c = cust || {};
        const prim = pickPrimaryIdentification(c);
        return {
            customerId,
            customerZaaerId: c.zaaerId ?? c.ZaaerId ?? null,
            isPrimary: true,
            customerName: c.customerName || c.CustomerName || "",
            idTypeName: prim?.idTypeName || prim?.IdTypeName || c.idTypeName || c.IdTypeName,
            idTypeNameAr: prim?.idTypeNameAr || prim?.IdTypeNameAr,
            idNumber: prim?.idNumber || prim?.IdNumber || c.idNumber || c.IdNumber,
            birthDate:
                c.birthdateGregorian || c.BirthdateGregorian || c.birthday || c.Birthday || prim?.birthDate || prim?.BirthDate,
            nationalityName: c.nationalityName || c.NationalityName,
            nationalityNameAr: c.nationalityNameAr || c.NationalityNameAr,
            mobileNo: c.mobileNo || c.MobileNo,
            email: c.email || c.Email,
            gender: c.gender || c.Gender || null,
            gtypeId: c.gtypeId != null ? c.gtypeId : c.GtypeId != null ? c.GtypeId : null,
            nationalityId: c.nId != null ? c.nId : c.NId != null ? c.NId : c.nationalityId != null ? c.nationalityId : c.NationalityId
        };
    }

    function buildCompanionSlotFromCustomer(customerId, cust) {
        const c = cust || {};
        const prim = pickPrimaryIdentification(c);
        return {
            rowKey: pageCtx.companionKeySeq++,
            customerId,
            customerZaaerId: c.zaaerId ?? c.ZaaerId ?? null,
            customerName: c.customerName || c.CustomerName || "",
            idTypeName: prim?.idTypeName || prim?.IdTypeName || c.idTypeName || c.IdTypeName,
            idTypeNameAr: prim?.idTypeNameAr || prim?.IdTypeNameAr,
            idNumber: prim?.idNumber || prim?.IdNumber || c.idNumber || c.IdNumber,
            birthDate:
                c.birthdateGregorian || c.BirthdateGregorian || c.birthday || c.Birthday || prim?.birthDate || prim?.BirthDate,
            nationalityName: c.nationalityName || c.NationalityName,
            nationalityNameAr: c.nationalityNameAr || c.NationalityNameAr,
            mobileNo: c.mobileNo || c.MobileNo,
            email: c.email || c.Email,
            gender: c.gender || c.Gender || null,
            gtypeId: c.gtypeId != null ? c.gtypeId : c.GtypeId != null ? c.GtypeId : null,
            nationalityId: c.nId != null ? c.nId : c.NId != null ? c.NId : c.nationalityId != null ? c.nationalityId : c.NationalityId,
            unitId: null,
            relationId: null
        };
    }

    function applyGuestFromCustomer(customerId, cust) {
        if (!pageCtx.detail) {
            return;
        }

        mergeOpenEditorsIntoDetail(pageCtx.detail);

        pageCtx.detail.customerId = customerId;
        const guestRow = buildGuestSlotFromCustomer(customerId, cust);
        pageCtx.detail.guests = [guestRow];
        pageCtx.detail.header =
            pageCtx.detail.header && typeof pageCtx.detail.header === "object"
                ? { ...pageCtx.detail.header, mainGuestName: guestRow.customerName || "" }
                : { mainGuestName: guestRow.customerName || "" };
        renderDetails(pageCtx.detail);
        DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 1800);
    }

    /**
     * Copies current General / Dates widget values into pageCtx.detail so renderDetails
     * does not wipe user edits (e.g. after picking a guest on a not-yet-saved reservation).
     */
    function mergeOpenEditorsIntoDetail(detail) {
        if (!detail || typeof detail !== "object") {
            return;
        }

        function selectBox(sel) {
            const $n = $(sel);
            return $n.length ? $n.dxSelectBox("instance") : null;
        }

        function textBox(sel) {
            const $n = $(sel);
            return $n.length ? $n.dxTextBox("instance") : null;
        }

        function numBox(sel) {
            const $n = $(sel);
            return $n.length ? $n.dxNumberBox("instance") : null;
        }

        function sw(sel) {
            const $n = $(sel);
            return $n.length ? $n.dxSwitch("instance") : null;
        }

        function btnGroup(sel) {
            const $n = $(sel);
            return $n.length ? $n.dxButtonGroup("instance") : null;
        }

        const gen = detail.general && typeof detail.general === "object" ? { ...detail.general } : {};
        const purposeInst = selectBox("#resGeneralPurpose");
        const sourceInst = selectBox("#resGeneralSource");
        const cmInst = textBox("#resCmBookingNo");
        const kindInst = selectBox("#resGeneralKind");
        const statusInst = selectBox("#resGeneralStatus");
        const arrivalInst = sw("#resGeneralArrival");

        if (purposeInst) {
            gen.visitPurposeId = purposeInst.option("value");
        }

        if (sourceInst) {
            gen.source = sourceInst.option("value");
        }

        if (cmInst) {
            gen.cmBookingNo = cmInst.option("value");
        }

        if (kindInst) {
            const kindVal = kindInst.option("value");
            gen.reservationType = kindVal === "company" ? "corporate" : "individual";
            detail.general = gen;
            if (kindVal !== "company") {
                detail.corporateId = null;
                detail.company = null;
            }
        } else {
            detail.general = gen;
        }

        const head = detail.header && typeof detail.header === "object" ? { ...detail.header } : {};
        if (statusInst && arrivalInst) {
            const st = statusInst.option("value");
            head.status = arrivalInst.option("value") ? "checked_in" : st;
        }

        detail.header = head;

        const dates = detail.dates && typeof detail.dates === "object" ? { ...detail.dates } : {};
        const rentalInst = btnGroup("#resRentalGroup");
        const monthsInst = numBox("#resMonths");
        const nightsInst = numBox("#resNights");
        const autoInst = sw("#resAutoExtend");

        if (rentalInst) {
            const keys = rentalInst.option("selectedItemKeys") || [];
            dates.rentalType = keys[0] === "Monthly" ? "Monthly" : "Daily";
        }

        const checkIn = getReservationCheckInCombined();
        const checkOut = getReservationCheckOutCombined();
        if (checkIn) {
            dates.checkInDate = checkIn;
        }

        if (checkOut) {
            dates.checkOutDate = checkOut;
        }

        if (monthsInst) {
            dates.numberOfMonths = monthsInst.option("value");
        }

        const calendarInst = btnGroup("#resCalendarGroup");
        if (calendarInst) {
            const keys = calendarInst.option("selectedItemKeys") || [];
            dates.monthlyCalendarMode = resolveEffectiveMonthlyCalendarMode(keys[0]);
        } else if (isMonthlyRentalMode()) {
            dates.monthlyCalendarMode = resolveEffectiveMonthlyCalendarMode(pageCtx._monthlyCalendarMode);
        }

        if (nightsInst) {
            dates.totalNights = nightsInst.option("value");
        }

        if (autoInst) {
            dates.isAutoExtend = autoInst.option("value");
        }

        detail.dates = dates;
    }

    function applyPatchResponsePreservingUi(detailFromServer) {
        if (!detailFromServer || !pageCtx.detail) {
            return detailFromServer;
        }

        mergeOpenEditorsIntoDetail(pageCtx.detail);

        const kindInst = $("#resGeneralKind").dxSelectBox("instance");
        const uiKind = kindInst ? kindInst.option("value") : null;
        const local = pageCtx.detail;

        if (
            uiKind === "company" &&
            (local.corporateId || local.company) &&
            !detailFromServer.corporateId &&
            !detailFromServer.company
        ) {
            detailFromServer.corporateId = local.corporateId;
            detailFromServer.company = local.company;
            detailFromServer.general = detailFromServer.general || {};
            detailFromServer.general.reservationType = "corporate";
        }

        return detailFromServer;
    }

    function refreshGuestProfileOnReservation(customerId, cust, sourceRow) {
        if (!pageCtx.detail || customerId == null) {
            return;
        }

        const guestRow = buildGuestSlotFromCustomer(customerId, cust);
        const srcId =
            sourceRow && sourceRow.customerId != null ? Number(sourceRow.customerId) : Number(customerId);

        if (sourceRow && sourceRow.rowKey != null) {
            const compIdx = (pageCtx.companions || []).findIndex((c) => c.rowKey === sourceRow.rowKey);
            if (compIdx >= 0) {
                Object.assign(pageCtx.companions[compIdx], guestRow, {
                    rowKey: pageCtx.companions[compIdx].rowKey,
                    unitId: pageCtx.companions[compIdx].unitId,
                    relationId: pageCtx.companions[compIdx].relationId
                });
                refreshCompanionsGrid();
                return;
            }
        }

        const guests = pageCtx.detail.guests || [];
        const guestIdx = guests.findIndex((g) => Number(g.customerId) === srcId);
        if (guestIdx >= 0) {
            pageCtx.detail.guests[guestIdx] = Object.assign({}, guests[guestIdx], guestRow);
        } else if (guests.length === 1) {
            pageCtx.detail.guests[0] = Object.assign({}, guests[0], guestRow);
        }

        pageCtx.detail.header =
            pageCtx.detail.header && typeof pageCtx.detail.header === "object"
                ? { ...pageCtx.detail.header, mainGuestName: guestRow.customerName || pageCtx.detail.header.mainGuestName }
                : { mainGuestName: guestRow.customerName || "" };

        $("#resHeaderGuest").text(guestRow.customerName || pageCtx.detail.header.mainGuestName || "—");
        syncReservationHeaderKvCards(pageCtx.detail);
        syncLodgingPartyCards();

        const grid = $("#guestsGrid").dxDataGrid("instance");
        if (grid) {
            grid.option("dataSource", (pageCtx.detail.guests || []).slice());
        }
        updateGuestsGridShellVisibility();
        syncHallMainGuestEditButton();
    }

    function assignGuest(customerId, custOrRow) {
        const pmsCust = window.Zaaer.PmsCustomerService;
        let routeId = customerId;
        if (custOrRow && pmsCust && typeof pmsCust.reservationCustomerId === "function") {
            const resolved = pmsCust.reservationCustomerId(custOrRow);
            if (resolved) {
                routeId = resolved;
            }
        }

        if (pageCtx.isClientNewReservation || !pageCtx.routeId) {
            const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
            if (lp) {
                lp.show();
            }

            const rowHasId =
                custOrRow &&
                typeof custOrRow === "object" &&
                (custOrRow.customerId != null ||
                    custOrRow.CustomerId != null ||
                    custOrRow.zaaerId != null ||
                    custOrRow.ZaaerId != null);
            if (rowHasId) {
                applyGuestFromCustomer(routeId, custOrRow);
                if (lp) {
                    lp.hide();
                }
                return;
            }

            const hotelId = pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId);
            const loadPromise = pmsCust
                ? pmsCust.getCustomer(customerId, hotelId)
                : Promise.reject(new Error("PmsCustomerService missing"));

            loadPromise
                .then((cust) => {
                    const id = (pmsCust && pmsCust.reservationCustomerId(cust)) || customerId;
                    applyGuestFromCustomer(id, cust || {});
                })
                .catch(() => DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 3200))
                .finally(() => lp && lp.hide());
            return;
        }

        const detail = pageCtx.detail || {};
        const guests = detail.guests || [];
        const primary = guests.find((g) => g.isPrimary) || guests[0];
        const currentRoute = detail.customerId != null ? Number(detail.customerId) : null;
        const targetRoute = Number(routeId);
        const samePrimaryGuest =
            primary &&
            Number.isFinite(targetRoute) &&
            targetRoute > 0 &&
            Number(primary.customerId) === targetRoute &&
            (currentRoute == null || currentRoute === targetRoute);

        if (samePrimaryGuest && custOrRow && typeof custOrRow === "object") {
            refreshGuestProfileOnReservation(targetRoute, custOrRow, primary);
            return;
        }

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        lp.show();
        window.Zaaer.ReservationDetailService.patchReservation(
            pageCtx.routeId,
            { customerId: routeId },
            pageCtx.hotelIdParam
        )
            .then((data) => {
                pageCtx.detail = applyPatchResponsePreservingUi(data);
                markReservationBaseline(pageCtx.detail);
                applyReservationRouteFromDetail(pageCtx.detail);
                DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                renderDetails(pageCtx.detail);
            })
            .catch(() => DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200))
            .finally(() => lp.hide());
    }

    function reservationHotelCodeForCorporateApi() {
        const d = pageCtx.detail;
        if (d && d.hotelCode != null && `${d.hotelCode}`.trim() !== "") {
            return String(d.hotelCode).trim();
        }
        return window.Zaaer.ApiService.getHotelCode() || "";
    }

    function resolveNumericHotelIdFromPageContext() {
        const d = pageCtx.detail;
        const fromDetail = d && d.hotelId != null ? Number(d.hotelId) : NaN;
        if (Number.isFinite(fromDetail) && fromDetail > 0) {
            return fromDetail;
        }
        const fromParam = pageCtx.hotelIdParam != null ? Number(pageCtx.hotelIdParam) : NaN;
        if (Number.isFinite(fromParam) && fromParam > 0) {
            return fromParam;
        }
        return null;
    }

    function normalizeCorporateForPickerResponse(raw) {
        if (raw == null) {
            return { items: [], resolvedHotelId: null };
        }
        if (Array.isArray(raw)) {
            return { items: raw, resolvedHotelId: null };
        }
        if (typeof raw === "object") {
            const nested =
                raw.value ??
                raw.Value ??
                raw.result ??
                raw.Result ??
                raw.payload ??
                raw.Payload ??
                raw.body ??
                raw.Body;
            if (nested != null && nested !== raw) {
                return normalizeCorporateForPickerResponse(nested);
            }
        }
        let items = raw.items !== undefined ? raw.items : raw.Items;
        if (!Array.isArray(items)) {
            items =
                raw.corporateCustomers !== undefined
                    ? raw.corporateCustomers
                    : raw.CorporateCustomers;
        }
        const rh = raw.resolvedHotelId !== undefined ? raw.resolvedHotelId : raw.ResolvedHotelId;
        const n = rh != null ? Number(rh) : NaN;
        return {
            items: Array.isArray(items) ? items : [],
            resolvedHotelId: Number.isFinite(n) && n > 0 ? n : null
        };
    }

    function normalizePagedCustomersResponse(raw) {
        let body = raw;
        if (raw && typeof raw === "object") {
            if (raw.data !== undefined) {
                body = raw.data;
            } else if (raw.Data !== undefined) {
                body = raw.Data;
            }
        }
        if (!body || typeof body !== "object") {
            return { rows: [], totalCount: 0, totalPages: 1 };
        }
        const rows = body.customers || body.Customers || [];
        const totalCount = body.totalCount ?? body.TotalCount ?? rows.length;
        const totalPages = body.totalPages ?? body.TotalPages;
        return {
            rows: Array.isArray(rows) ? rows : [],
            totalCount: Number(totalCount) || 0,
            totalPages: Number(totalPages) || 0
        };
    }

    function loadCorporatePickerDataset(hotelIdNum, hotelCodeStr) {
        const params = {};
        if (hotelIdNum != null && Number.isFinite(Number(hotelIdNum)) && Number(hotelIdNum) > 0) {
            params.hotelId = hotelIdNum;
        }
        if (hotelCodeStr) {
            params.hotelCode = hotelCodeStr;
        }
        return window.Zaaer.ApiService.get("/api/v1/pms/corporate-customers/for-picker", params).then((raw) => {
            let body = raw;
            if (raw && typeof raw === "object") {
                if (raw.data !== undefined) {
                    body = raw.data;
                } else if (raw.Data !== undefined) {
                    body = raw.Data;
                }
            }
            return normalizeCorporateForPickerResponse(body);
        });
    }

    function validateCorporateInstitutionForm(fd) {
        if (!(`${fd.corporateName || ""}`.trim())) {
            return "reservationDetail.company.validationName";
        }
        if (!(`${fd.country || ""}`.trim())) {
            return "reservationDetail.company.validationCountry";
        }
        if (!(`${fd.city || ""}`.trim())) {
            return "reservationDetail.company.validationCity";
        }
        if (!(`${fd.address || ""}`.trim())) {
            return "reservationDetail.company.validationAddress";
        }
        if (!(`${fd.postalCode || ""}`.trim())) {
            return "reservationDetail.company.validationPostal";
        }
        const cr = `${fd.commercialRegistrationNo || ""}`.trim();
        if (!/^\d{10}$/.test(cr)) {
            return "reservationDetail.company.validationCommercialReg";
        }
        const vat = `${fd.vatRegistrationNo || ""}`.trim();
        if (!/^3\d{13}3$/.test(vat)) {
            return "reservationDetail.company.validationVat";
        }
        return null;
    }

    function buildCorporateInstitutionFormItems() {
        return [
            {
                itemType: "simple",
                colSpan: 3,
                dataField: "corporateName",
                label: { text: t("reservationDetail.company.name") },
                isRequired: true
            },
            {
                dataField: "country",
                label: { text: t("reservationDetail.company.country") },
                isRequired: true
            },
            {
                dataField: "city",
                label: { text: t("reservationDetail.company.city") },
                isRequired: true
            },
            {
                itemType: "simple",
                colSpan: 3,
                dataField: "address",
                label: { text: t("reservationDetail.company.address") },
                editorType: "dxTextArea",
                editorOptions: { height: 64 },
                isRequired: true
            },
            {
                dataField: "postalCode",
                label: { text: t("reservationDetail.company.postalCode") },
                isRequired: true
            },
            {
                dataField: "commercialRegistrationNo",
                label: { text: t("reservationDetail.company.commercialReg") },
                isRequired: true
            },
            {
                dataField: "vatRegistrationNo",
                label: { text: t("reservationDetail.company.vatReg") },
                isRequired: true
            },
            {
                dataField: "discountMethod",
                label: { text: t("reservationDetail.company.discountMethod") },
                editorType: "dxSelectBox",
                editorOptions: {
                    dataSource: [
                        { id: "Amount", text: t("reservationDetail.company.discountAmount") },
                        { id: "Percentage", text: t("reservationDetail.company.discountPercent") }
                    ],
                    valueExpr: "id",
                    displayExpr: "text"
                }
            },
            {
                dataField: "discountValue",
                label: { text: t("reservationDetail.company.discountValue") },
                editorType: "dxNumberBox",
                editorOptions: { min: 0, format: "#0.##", showSpinButtons: true }
            },
            {
                dataField: "corporatePhone",
                label: { text: t("reservationDetail.company.phone") }
            },
            {
                dataField: "email",
                label: { text: t("reservationDetail.company.email") }
            },
            {
                dataField: "contactPersonName",
                label: { text: t("reservationDetail.company.contact") }
            },
            {
                dataField: "contactPersonPhone",
                label: { text: t("reservationDetail.company.contactPhone") }
            },
            {
                itemType: "simple",
                colSpan: 3,
                dataField: "notes",
                label: { text: t("reservationDetail.company.notes") },
                editorType: "dxTextArea",
                editorOptions: { height: 56 }
            }
        ];
    }

    function openCorporatePicker() {
        if (!requirePmsPermission("reservations.company_add")) {
            return;
        }

        const hotelCodeStr = reservationHotelCodeForCorporateApi();
        const hotelIdGuess = resolveNumericHotelIdFromPageContext();

        const $host = $("<div>").appendTo("body");
        let gridInst = null;
        let pickerContextHotelId = hotelIdGuess;
        let allCorporatePickerRows = [];
        const pickerReloadHolder = { reload: () => {} };

        function ensureTenantHotelCodeForCorporate() {
            let hc = window.Zaaer.ApiService.getHotelCode();
            if (hc != null && `${hc}`.trim() !== "") {
                return true;
            }
            const fromDetail =
                pageCtx.detail &&
                pageCtx.detail.hotelCode != null &&
                `${pageCtx.detail.hotelCode}`.trim() !== ""
                    ? String(pageCtx.detail.hotelCode).trim()
                    : "";
            if (fromDetail) {
                window.Zaaer.ApiService.setHotelCode(fromDetail);
                return true;
            }
            const q = new URLSearchParams(window.location.search).get("hotelCode");
            if (q != null && `${q}`.trim() !== "") {
                window.Zaaer.ApiService.setHotelCode(`${q}`.trim());
                return true;
            }
            if (hotelCodeStr) {
                window.Zaaer.ApiService.setHotelCode(hotelCodeStr);
                return true;
            }
            DevExpress.ui.notify(t("reservationDetail.missingHotelCode"), "warning", 4200);
            return false;
        }

        function refreshGridFromServer() {
            pickerReloadHolder.reload();
        }

        function openCorporateCreate(onCreated) {
            const effectiveHotelId = pickerContextHotelId;
            if (!effectiveHotelId || !Number.isFinite(Number(effectiveHotelId)) || Number(effectiveHotelId) <= 0) {
                DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 2600);
                return;
            }

            const $p = $("<div>").appendTo("body");

            $p.dxPopup({
                width: Math.min(920, Math.max(360, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "85vh",
                title: t("reservationDetail.company.createTitle"),
                visible: true,
                showCloseButton: true,
                hideOnOutsideClick: true,
                dragEnabled: false,
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                animation: {
                    show: { type: "fade", duration: 150 },
                    hide: { type: "fade", duration: 120 }
                },
                wrapperAttr: { class: "guest-visitor-popup res-guest-visitor-popup" },
                contentTemplate(contentElem) {
                    const $content = $(contentElem).empty();
                    const $scroll = $("<div>").addClass("guest-visitor-scroll").appendTo($content);
                    const $wrap = $("<div>").addClass("guest-visitor-body").appendTo($scroll);

                    const $form = $("<div>").appendTo($wrap);
                    $form.dxForm({
                        formData: {
                            hotelId: effectiveHotelId,
                            corporateName: "",
                            country: "Saudi Arabia",
                            city: "",
                            address: "",
                            postalCode: "",
                            vatRegistrationNo: "",
                            commercialRegistrationNo: "",
                            discountMethod: "Amount",
                            discountValue: 0,
                            corporatePhone: "",
                            email: "",
                            contactPersonName: "",
                            contactPersonPhone: "",
                            notes: ""
                        },
                        labelLocation: "top",
                        colCount: 3,
                        showColonAfterLabel: false,
                        showRequiredMark: true,
                        requiredMark: "*",
                        items: buildCorporateInstitutionFormItems()
                    });

                    const $footer = $("<div>").addClass("guest-visitor-footer").appendTo($content);
                    $("<div>")
                        .appendTo($footer)
                        .dxButton({
                            text: t("reservationDetail.actions.cancel"),
                            icon: "close",
                            stylingMode: "outlined",
                            type: "normal",
                            onClick() {
                                $p.dxPopup("instance").hide();
                            }
                        });
                    $("<div>")
                        .appendTo($footer)
                        .dxButton({
                            text: t("reservationDetail.company.addBtn"),
                            icon: "plus",
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                const inst = $form.dxForm("instance");
                                const fd = inst.option("formData");
                                const errKey = validateCorporateInstitutionForm(fd);
                                if (errKey) {
                                    DevExpress.ui.notify(t(errKey), "warning", 3200);
                                    return;
                                }

                                const payload = {
                                    hotelId: effectiveHotelId,
                                    corporateName: fd.corporateName.trim(),
                                    country: fd.country.trim(),
                                    city: fd.city.trim(),
                                    postalCode: fd.postalCode.trim(),
                                    address: fd.address.trim(),
                                    vatRegistrationNo: `${fd.vatRegistrationNo || ""}`.trim(),
                                    commercialRegistrationNo: `${fd.commercialRegistrationNo || ""}`.trim(),
                                    discountMethod: fd.discountMethod || null,
                                    discountValue: fd.discountValue ?? null,
                                    corporatePhone: fd.corporatePhone || null,
                                    email: fd.email || null,
                                    contactPersonName: fd.contactPersonName || null,
                                    contactPersonPhone: fd.contactPersonPhone || null,
                                    notes: fd.notes || null,
                                    isActive: true
                                };

                                const pmsCorp = window.Zaaer.PmsCorporateCustomerService;
                                if (!pmsCorp || typeof pmsCorp.createCorporate !== "function") {
                                    DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200);
                                    return;
                                }

                                pmsCorp
                                    .createCorporate(payload)
                                    .then((created) => {
                                        DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                                        $p.dxPopup("instance").hide();
                                        if (typeof onCreated === "function") {
                                            onCreated(created);
                                        }
                                    })
                                    .catch(() =>
                                        DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200)
                                    );
                            }
                        });
                },
                onHidden() {
                    $p.remove();
                }
            });
        }

        $host.dxPopup({
            width: Math.min(1040, Math.max(360, window.innerWidth - 24)),
            height: "90vh",
            maxHeight: "90vh",
            showTitle: true,
            title: t("reservationDetail.company.selectTitle"),
            visible: true,
            showCloseButton: true,
            dragEnabled: false,
            hideOnOutsideClick: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "guest-picker-popup guest-picker-popup--wide res-extra-popup" },
            onShowing(e) {
                const popupInstance = e.component;
                const $content = $(popupInstance.content()).empty().addClass("guest-picker-body guest-picker-body--picker");

                if (!ensureTenantHotelCodeForCorporate()) {
                    return;
                }

                const $toolbar = $("<div>").addClass("guest-picker-toolbar").appendTo($content);

                const $row1 = $("<div>").addClass("guest-picker-toolbar-row").appendTo($toolbar);

                const $txtHost = $("<div>").appendTo($row1);
                $txtHost.dxTextBox({
                    width: "100%",
                    showClearButton: true,
                    placeholder: t("reservationDetail.company.pickFreeText"),
                    inputAttr: { "aria-label": t("reservationDetail.company.pickFreeText") },
                    onEnterKey() {
                        reloadCorporatePickerRows();
                    },
                    onChange() {
                        reapplyFiltersOnly();
                    }
                });
                const txtInst = $txtHost.dxTextBox("instance");

                const $row2 = $("<div>").addClass("guest-picker-toolbar-row").appendTo($toolbar);

                const $countryHost = $("<div>").appendTo($row2);
                $countryHost.dxTextBox({
                    width: 220,
                    showClearButton: true,
                    placeholder: t("reservationDetail.company.pickFilterCountry"),
                    inputAttr: { "aria-label": t("reservationDetail.company.pickFilterCountry") },
                    onValueChanged() {
                        reapplyFiltersOnly();
                    }
                });
                const countryInst = $countryHost.dxTextBox("instance");

                const $cityHost = $("<div>").appendTo($row2);
                $cityHost.dxTextBox({
                    width: 220,
                    showClearButton: true,
                    placeholder: t("reservationDetail.company.pickFilterCity"),
                    inputAttr: { "aria-label": t("reservationDetail.company.pickFilterCity") },
                    onValueChanged() {
                        reapplyFiltersOnly();
                    }
                });
                const cityInst = $cityHost.dxTextBox("instance");

                const $rowActions = $("<div>").addClass("guest-picker-toolbar-row").appendTo($toolbar);

                function applyCorporatePickerFilters(sourceRows) {
                    const qText = (`${txtInst.option("value") || ""}`).trim().toLowerCase();
                    const qCountry = (`${countryInst.option("value") || ""}`).trim().toLowerCase();
                    const qCity = (`${cityInst.option("value") || ""}`).trim().toLowerCase();
                    const rows = Array.isArray(sourceRows) ? sourceRows : [];
                    return rows.filter((row) => {
                        if (!row || typeof row !== "object") {
                            return false;
                        }
                        const countryHay = `${row.country || row.Country || ""} ${row.countryAr || row.CountryAr || ""}`.toLowerCase();
                        const cityHay = `${row.city || row.City || ""} ${row.cityAr || row.CityAr || ""}`.toLowerCase();
                        if (qCountry && !countryHay.includes(qCountry)) {
                            return false;
                        }
                        if (qCity && !cityHay.includes(qCity)) {
                            return false;
                        }
                        if (!qText) {
                            return true;
                        }
                        const phone = row.corporatePhone ?? row.CorporatePhone ?? "";
                        const parts = [
                            row.corporateName,
                            row.CorporateName,
                            row.corNo,
                            row.CorNo,
                            phone,
                            row.email,
                            row.Email,
                            row.vatRegistrationNo,
                            row.VatRegistrationNo,
                            row.commercialRegistrationNo,
                            row.CommercialRegistrationNo,
                            row.postalCode,
                            row.PostalCode,
                            row.address,
                            row.Address,
                            row.city,
                            row.City,
                            row.country,
                            row.Country
                        ];
                        const blob = parts
                            .filter((x) => x != null && `${x}`.trim() !== "")
                            .map((x) => `${x}`.toLowerCase())
                            .join(" ");
                        return blob.includes(qText);
                    });
                }

                function pickCorporateRow(data) {
                    const row = data || {};
                    let cid = row.corporateId ?? row.CorporateId;
                    if (cid == null || cid === "" || Number(cid) <= 0) {
                        const z = row.zaaerId ?? row.ZaaerId;
                        if (z != null && `${z}`.trim() !== "" && Number(z) > 0) {
                            cid = z;
                        }
                    }

                    if (cid == null || cid === "" || Number(cid) <= 0) {
                        DevExpress.ui.notify(t("reservationDetail.company.pickInvalid"), "warning", 2800);
                        return;
                    }

                    popupInstance.hide();
                    assignCorporate(cid, row);
                }

                const $gridHost = $("<div>")
                    .addClass("guest-picker-grid guest-picker-grid--pl pms-grid-compact")
                    .appendTo($content);

                const po = window.Zaaer.PmsGridOptions;
                $gridHost.dxDataGrid(
                    po.merge(po.baseline(), {
                    keyExpr: "corporateId",
                    height: "calc(90vh - 300px)",
                    dataSource: [],
                    remoteOperations: false,
                    rowAlternationEnabled: false,
                    paging: pmsPickerGridPagingOptions(),
                    pager: pmsPickerGridPagerOptions(),
                    searchPanel: { visible: true, width: 280 },
                    elementAttr: { class: "guest-picker-grid guest-picker-grid--pl pms-grid-compact" },
                    loadPanel: { enabled: true },
                    columns: [
                        buildPmsPickerSelectColumn(pickCorporateRow, t("reservationDetail.company.selectBtn")),
                        {
                            dataField: "corporateName",
                            caption: t("reservationDetail.company.name"),
                            minWidth: 200
                        },
                        {
                            dataField: "corNo",
                            caption: t("reservationDetail.company.corNo"),
                            width: 120
                        },
                        {
                            dataField: "country",
                            caption: t("reservationDetail.company.country"),
                            minWidth: 100
                        },
                        {
                            dataField: "city",
                            caption: t("reservationDetail.company.city"),
                            minWidth: 100
                        },
                        {
                            caption: t("reservationDetail.company.commercialReg"),
                            width: 130,
                            allowSorting: false,
                            calculateCellValue(row) {
                                const v = row.commercialRegistrationNo ?? row.CommercialRegistrationNo;
                                return v != null && `${v}`.trim() !== "" ? v : "—";
                            }
                        },
                        {
                            caption: t("reservationDetail.company.vatReg"),
                            width: 160,
                            allowSorting: false,
                            calculateCellValue(row) {
                                const v = row.vatRegistrationNo ?? row.VatRegistrationNo;
                                return v != null && `${v}`.trim() !== "" ? v : "—";
                            }
                        },
                        {
                            dataField: "postalCode",
                            caption: t("reservationDetail.company.postalCode"),
                            width: 100,
                            calculateCellValue(row) {
                                const v = row.postalCode ?? row.PostalCode;
                                return v != null && `${v}`.trim() !== "" ? v : "—";
                            }
                        },
                        {
                            dataField: "zaaerId",
                            caption: t("reservationDetail.zaaerId"),
                            width: 96,
                            calculateCellValue(row) {
                                const v = row.zaaerId ?? row.ZaaerId;
                                return v == null || v === "" ? "—" : v;
                            }
                        }
                    ]
                    })
                );

                gridInst = $gridHost.dxDataGrid("instance");

                function reloadCorporatePickerRows() {
                    if (!gridInst) {
                        return;
                    }
                    if (!ensureTenantHotelCodeForCorporate()) {
                        return;
                    }
                    gridInst.beginCustomLoading(t("reservationDetail.company.pickLoading"));
                    loadCorporatePickerDataset(pickerContextHotelId, hotelCodeStr)
                        .done((pack) => {
                            if (pack.resolvedHotelId != null) {
                                pickerContextHotelId = pack.resolvedHotelId;
                            }
                            allCorporatePickerRows = pack.items || [];
                            if (allCorporatePickerRows.length === 0 && pack.resolvedHotelId == null) {
                                DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 3600);
                            }
                            gridInst.option("dataSource", applyCorporatePickerFilters(allCorporatePickerRows));
                            try {
                                gridInst.clearFilter();
                            } catch (e1) {
                                /* ignore */
                            }
                            try {
                                gridInst.option("filterValue", null);
                                gridInst.option("searchPanel.text", "");
                            } catch (e2) {
                                /* ignore */
                            }
                            gridInst.refresh();
                        })
                        .fail(() => DevExpress.ui.notify(t("reservationDetail.company.pickLoadFailed"), "error", 3600))
                        .always(() => gridInst.endCustomLoading());
                }

                pickerReloadHolder.reload = reloadCorporatePickerRows;

                function reapplyFiltersOnly() {
                    if (!gridInst) {
                        return;
                    }
                    gridInst.option("dataSource", applyCorporatePickerFilters(allCorporatePickerRows));
                }

                $("<div>")
                    .appendTo($rowActions)
                    .dxButton({
                        text: t("reservationDetail.guest.pickVisitorSearch"),
                        type: "default",
                        stylingMode: "contained",
                        icon: "find",
                        onClick: reloadCorporatePickerRows
                    });

                $("<div>")
                    .appendTo($rowActions)
                    .dxButton({
                        text: t("reservationDetail.guest.pickVisitorReset"),
                        stylingMode: "outlined",
                        icon: "refresh",
                        onClick() {
                            txtInst.option("value", "");
                            countryInst.option("value", "");
                            cityInst.option("value", "");
                            reloadCorporatePickerRows();
                        }
                    });

                reloadCorporatePickerRows();
            },
            onHidden() {
                $host.remove();
            },
            toolbarItems: [
                {
                    toolbar: "top",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("reservationDetail.company.createBtn"),
                        icon: "plus",
                        type: "default",
                        onClick() {
                            openCorporateCreate((created) => {
                                refreshGridFromServer();
                                if (created && typeof created === "object") {
                                    const cid = created.corporateId ?? created.CorporateId;
                                    if (cid != null && `${cid}`.trim() !== "" && Number(cid) > 0) {
                                        pickCorporateRow(created);
                                    }
                                }
                            });
                        }
                    }
                },
                {
                    toolbar: "bottom",
                    widget: "dxButton",
                    location: "after",
                    options: {
                        text: t("common.close"),
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                }
            ]
        });
    }

    function openCorporateEdit() {
        const co = pageCtx.detail && pageCtx.detail.company;
        if (!co) {
            return;
        }

        const hotelIdRaw = resolveNumericHotelIdFromPageContext();
        const hotelId = hotelIdRaw;
        const pmsCorp = window.Zaaer.PmsCorporateCustomerService;
        const routeCorpId =
            pmsCorp && typeof pmsCorp.reservationCorporateId === "function"
                ? pmsCorp.reservationCorporateId(co)
                : co.corporateId;

        const $host = $("<div>").appendTo("body");

        $host.dxPopup({
            width: Math.min(920, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "85vh",
            title: t("reservationDetail.company.editTitle"),
            visible: true,
            showCloseButton: true,
            dragEnabled: false,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "guest-visitor-popup res-company-edit-popup res-guest-visitor-popup" },
                contentTemplate(contentElem) {
                    const $content = $(contentElem).empty();
                    const $scroll = $("<div>").addClass("guest-visitor-scroll").appendTo($content);
                    const $wrap = $("<div>").addClass("guest-visitor-body").appendTo($scroll);

                    const $form = $("<div>").appendTo($wrap);
                    $form.dxForm({
                        formData: {
                            corporateId: co.corporateId,
                        hotelId,
                        corporateName: co.corporateName || "",
                        country: co.country || "Saudi Arabia",
                        city: co.city || "",
                        address: co.address || "",
                        postalCode: co.postalCode || "",
                        commercialRegistrationNo: co.commercialRegistrationNo || "",
                        vatRegistrationNo: co.vatRegistrationNo || "",
                        discountMethod: co.discountMethod || "Amount",
                        discountValue: co.discountValue != null ? Number(co.discountValue) : 0,
                        corporatePhone: co.corporatePhone || "",
                        email: co.email || "",
                        contactPersonName: co.contactPersonName || "",
                        contactPersonPhone: co.contactPersonPhone || "",
                        notes: co.notes || ""
                    },
                    labelLocation: "top",
                    colCount: 3,
                    showColonAfterLabel: false,
                    showRequiredMark: true,
                    requiredMark: "*",
                    items: buildCorporateInstitutionFormItems()
                });

                const $footer = $("<div>").addClass("guest-visitor-footer").appendTo($content);
                $("<div>")
                    .appendTo($footer)
                    .dxButton({
                        text: t("reservationDetail.actions.cancel"),
                        icon: "close",
                        stylingMode: "outlined",
                        type: "normal",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    });
                $("<div>")
                    .appendTo($footer)
                    .dxButton({
                        text: t("reservationDetail.company.save"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            if (!pmsCorp || typeof pmsCorp.updateCorporate !== "function") {
                                DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200);
                                return;
                            }

                            if (!hotelId || !Number.isFinite(Number(hotelId)) || Number(hotelId) <= 0) {
                                DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 2600);
                                return;
                            }

                            const inst = $form.dxForm("instance");
                            const fd = inst.option("formData");
                            const errKey = validateCorporateInstitutionForm(fd);
                            if (errKey) {
                                DevExpress.ui.notify(t(errKey), "warning", 3200);
                                return;
                            }

                            const payload = {
                                corporateId: fd.corporateId,
                                hotelId: fd.hotelId,
                                corporateName: fd.corporateName.trim(),
                                country: fd.country.trim(),
                                city: fd.city.trim(),
                                postalCode: fd.postalCode.trim(),
                                address: fd.address.trim(),
                                vatRegistrationNo: `${fd.vatRegistrationNo || ""}`.trim(),
                                commercialRegistrationNo: `${fd.commercialRegistrationNo || ""}`.trim(),
                                discountMethod: fd.discountMethod || null,
                                discountValue: fd.discountValue ?? null,
                                corporatePhone: fd.corporatePhone || null,
                                email: fd.email || null,
                                contactPersonName: fd.contactPersonName || null,
                                contactPersonPhone: fd.contactPersonPhone || null,
                                notes: fd.notes || null,
                                isActive: true
                            };

                            pmsCorp
                                .updateCorporate(routeCorpId, payload, hotelId)
                                .then((updated) => {
                                    DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                                    $host.dxPopup("instance").hide();
                                    if (pageCtx.isClientNewReservation || !pageCtx.routeId) {
                                        applyCorporateEditLocally(fd, updated);
                                        return;
                                    }

                                    loadPage(false);
                                })
                                .catch(() =>
                                    DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3200)
                                );
                        }
                    });
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function openGuestPicker(pickerOptions) {
        if (!requirePmsPermission("guests.create")) {
            return;
        }

        const opts =
            pickerOptions && typeof pickerOptions === "object" && typeof pickerOptions.onPick === "function"
                ? pickerOptions
                : {};
        const customOnPick = typeof opts.onPick === "function" ? opts.onPick : null;
        const $host = $("<div>").appendTo("body");

        $host.dxPopup({
            width: Math.min(1040, Math.max(360, window.innerWidth - 24)),
            height: "90vh",
            maxHeight: "90vh",
            title: t("reservationDetail.guest.selectTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: hijriAwarePopupHideOnOutsideClick,
            wrapperAttr: { class: "guest-picker-popup guest-picker-popup--wide" },
            onShowing(e) {
                const popupInstance = e.component;
                const $content = $(popupInstance.content()).empty().addClass("guest-picker-body guest-picker-body--picker");

                function pickGuestRow(data) {
                    const cid = data && (data.customerId ?? data.CustomerId);
                    if (cid == null) {
                        return;
                    }
                    if (customOnPick) {
                        customOnPick(Number(cid), data);
                        popupInstance.hide();
                        return;
                    }

                    assignGuest(Number(cid), data);
                    popupInstance.hide();
                }

                function primaryIdent(row) {
                    if (!row) {
                        return null;
                    }
                    const list = row.identifications || row.Identifications || [];
                    const arr = Array.isArray(list) ? list : [];
                    return arr.find((i) => i && (i.isPrimary || i.IsPrimary)) || arr[0] || null;
                }

                function formatIdTypeName(prim) {
                    if (!prim) {
                        return "—";
                    }
                    const ar = prim.idTypeNameAr ?? prim.IdTypeNameAr;
                    const en = prim.idTypeName ?? prim.IdTypeName;
                    return (isArabic() && ar ? ar : en || ar) || "—";
                }

                window.Zaaer.ReservationDetailService.loadGuestFormLookups()
                    .then((lk) => {
                        let searchModeState = "name";
                        const modeOrder = ["name", "id", "mobile"];

                        const $toolbar = $("<div>").addClass("guest-picker-toolbar").appendTo($content);

                        const $rowModes = $("<div>").addClass("guest-picker-toolbar-row guest-picker-toolbar-row--modes")
                            .appendTo($toolbar);
                        $("<span>")
                            .addClass("guest-picker-label")
                            .text(t("reservationDetail.guest.pickVisitorSearchBy"))
                            .appendTo($rowModes);
                        const $modes = $("<div>").addClass("guest-picker-mode-buttons").appendTo($rowModes);
                        const modeBtnRefs = {};

                        function syncModeButtons() {
                            modeOrder.forEach((m) => {
                                modeBtnRefs[m].option({
                                    stylingMode: searchModeState === m ? "contained" : "outlined",
                                    type: searchModeState === m ? "default" : "normal"
                                });
                            });
                        }

                        modeOrder.forEach((mode) => {
                            const $b = $("<div>").appendTo($modes);
                            $b.dxButton({
                                stylingMode: searchModeState === mode ? "contained" : "outlined",
                                type: searchModeState === mode ? "default" : "normal",
                                text:
                                    mode === "id"
                                        ? t("reservationDetail.guest.pickVisitorModeId")
                                        : mode === "mobile"
                                          ? t("reservationDetail.guest.pickVisitorModeMobile")
                                          : t("reservationDetail.guest.pickVisitorModeName"),
                                onClick() {
                                    searchModeState = mode;
                                    syncModeButtons();
                                }
                            });
                            modeBtnRefs[mode] = $b.dxButton("instance");
                        });

                        const $addCustHost = $("<div>").addClass("guest-picker-toolbar-end").appendTo($rowModes);
                        $addCustHost.dxButton({
                            text: t("reservationDetail.guest.createShort"),
                            type: "default",
                            icon: "plus",
                            stylingMode: "contained",
                            onClick() {
                                openCreateCustomerNested(() => {
                                    popupInstance.hide();
                                });
                            }
                        });

                        const $rowFilters = $("<div>")
                            .addClass("guest-picker-toolbar-row guest-picker-toolbar-row--filters")
                            .appendTo($toolbar);

                        const $txtHost = $("<div>").addClass("guest-picker-field").appendTo($rowFilters);
                        $txtHost.dxTextBox({
                            width: "100%",
                            showClearButton: true,
                            inputAttr: { "aria-label": t("reservationDetail.guest.pickVisitorSearch") }
                        });
                        const txtInst = $txtHost.dxTextBox("instance");
                        txtInst.option("placeholder", t("reservationDetail.guest.pickVisitorSearch"));

                        const $actionsHost = $("<div>").addClass("guest-picker-toolbar-actions").appendTo($rowFilters);

                        const $gridHost = $("<div>")
                            .addClass("guest-picker-grid guest-picker-grid--pl pms-grid-compact")
                            .appendTo($content);

                        function nationalityDisplay(row) {
                            if (!row || typeof row !== "object") {
                                return "—";
                            }

                            const ar = row.nationalityNameAr ?? row.NationalityNameAr;
                            const en = row.nationalityName ?? row.NationalityName;
                            return isArabic() ? ar || en || "—" : en || ar || "—";
                        }

                        const po = window.Zaaer.PmsGridOptions;
                        $gridHost.dxDataGrid(
                            po.merge(po.baseline(), {
                            keyExpr: "customerId",
                            height: "calc(90vh - 280px)",
                            dataSource: [],
                            remoteOperations: false,
                            rowAlternationEnabled: false,
                            paging: pmsPickerGridPagingOptions(),
                            pager: pmsPickerGridPagerOptions(),
                            searchPanel: { visible: false },
                            headerFilter: { visible: true },
                            filterRow: { visible: false },
                            elementAttr: { class: "guest-picker-grid guest-picker-grid--pl pms-grid-compact" },
                            columns: [
                                buildPmsPickerSelectColumn(pickGuestRow, t("reservationDetail.guest.pickVisitorHint")),
                                {
                                    dataField: "customerName",
                                    caption: t("reservationDetail.guest.name"),
                                    width: 220,
                                    minWidth: 180
                                },
                                {
                                    caption: t("reservationDetail.guest.idTypeCol"),
                                    width: 210,
                                    minWidth: 190,
                                    allowSorting: false,
                                    calculateCellValue(row) {
                                        return formatIdTypeName(primaryIdent(row));
                                    }
                                },
                                {
                                    caption: t("reservationDetail.guest.idNo"),
                                    width: 140,
                                    allowSorting: false,
                                    calculateCellValue(row) {
                                        const p = primaryIdent(row);
                                        const n = p && (p.idNumber ?? p.IdNumber);
                                        return n || "—";
                                    }
                                },
                                {
                                    dataField: "mobileNo",
                                    caption: t("reservationDetail.guest.phone"),
                                    minWidth: 130
                                },
                                {
                                    caption: t("reservationDetail.guest.pickVisitorNationality"),
                                    minWidth: 140,
                                    allowSorting: false,
                                    calculateCellValue(row) {
                                        return nationalityDisplay(row);
                                    }
                                }
                            ]
                            })
                        );

                        const gridInst = $gridHost.dxDataGrid("instance");

                        function ensureTenantHotelCode() {
                            let hc = window.Zaaer.ApiService.getHotelCode();
                            if (hc != null && `${hc}`.trim() !== "") {
                                return true;
                            }

                            const fromDetail =
                                pageCtx.detail &&
                                pageCtx.detail.hotelCode != null &&
                                `${pageCtx.detail.hotelCode}`.trim() !== ""
                                    ? String(pageCtx.detail.hotelCode).trim()
                                    : "";
                            if (fromDetail) {
                                window.Zaaer.ApiService.setHotelCode(fromDetail);
                                return true;
                            }

                            const q = new URLSearchParams(window.location.search).get("hotelCode");
                            if (q != null && `${q}`.trim() !== "") {
                                window.Zaaer.ApiService.setHotelCode(`${q}`.trim());
                                return true;
                            }

                            DevExpress.ui.notify(t("reservationDetail.missingHotelCode"), "warning", 4200);
                            return false;
                        }

                        function reloadGuestPickerGrid() {
                            if (!ensureTenantHotelCode()) {
                                return;
                            }

                            const term = (`${txtInst.option("value") || ""}`).trim();
                            const pageSize = 500;

                            const baseParams = {
                                pageSize,
                                searchTerm: term || undefined,
                                searchMode: term ? searchModeState : undefined
                            };

                            function customerKey(row) {
                                if (!row || typeof row !== "object") {
                                    return null;
                                }
                                const k = row.customerId ?? row.CustomerId;
                                return k == null || k === "" ? null : String(k);
                            }

                            function mergeCustomerRows(map, rows) {
                                if (!Array.isArray(rows)) {
                                    return;
                                }
                                for (let i = 0; i < rows.length; i += 1) {
                                    const row = rows[i];
                                    const key = customerKey(row);
                                    if (key) {
                                        map.set(key, row);
                                    }
                                }
                            }

                            gridInst.beginCustomLoading(t("reservationDetail.guest.pickVisitorSearch"));

                            window.Zaaer.ApiService.get("/api/v1/pms/customers", { ...baseParams, pageNumber: 1 })
                                .then((firstRes) => {
                                    const byId = new Map();
                                    const firstPage = normalizePagedCustomersResponse(firstRes);
                                    mergeCustomerRows(byId, firstPage.rows);

                                    const totalPagesRaw = firstPage.totalPages;
                                    const totalCount = firstPage.totalCount;
                                    let totalPages =
                                        typeof totalPagesRaw === "number" && totalPagesRaw > 0
                                            ? totalPagesRaw
                                            : typeof totalCount === "number" && totalCount >= 0
                                              ? Math.max(1, Math.ceil(totalCount / pageSize))
                                              : 1;

                                    let chain = $.Deferred().resolve().promise();
                                    for (let p = 2; p <= totalPages; p += 1) {
                                        const pageNumber = p;
                                        chain = chain.then(() =>
                                            window.Zaaer.ApiService.get("/api/v1/pms/customers", {
                                                ...baseParams,
                                                pageNumber
                                            }).then((res) => {
                                                const page = normalizePagedCustomersResponse(res);
                                                mergeCustomerRows(byId, page.rows);
                                            })
                                        );
                                    }

                                    return chain.then(() => Array.from(byId.values()));
                                })
                                .done((allRows) => {
                                    gridInst.option("dataSource", allRows);
                                    gridInst.refresh();
                                })
                                .fail(() =>
                                    DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 3200)
                                )
                                .always(() => gridInst.endCustomLoading());
                        }

                        txtInst.option("onEnterKey", reloadGuestPickerGrid);

                        $("<div>")
                            .appendTo($actionsHost)
                            .dxButton({
                                text: t("reservationDetail.guest.pickVisitorSearch"),
                                type: "default",
                                stylingMode: "contained",
                                icon: "find",
                                onClick: reloadGuestPickerGrid
                            });

                        $("<div>")
                            .appendTo($actionsHost)
                            .dxButton({
                                text: t("reservationDetail.guest.pickVisitorReset"),
                                stylingMode: "outlined",
                                icon: "refresh",
                                onClick() {
                                    txtInst.option("value", "");
                                    searchModeState = "name";
                                    syncModeButtons();
                                    reloadGuestPickerGrid();
                                }
                            });

                        reloadGuestPickerGrid();
                    })
                    .catch(() => {
                        DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 3200);
                    });
            },
            onHidden() {
                closeOpenHijriPickers();
                $host.remove();
            }
        });
    }

    function openCreateCustomerNested(onDone) {
        window.Zaaer.GuestVisitorForm.open({
            mode: "create",
            onDone,
            t,
            isArabic,
            pageCtx,
            hotelCode: window.Zaaer.ApiService.getHotelCode(),
            assignGuest,
            loadPage
        });
    }

    function openGuestEdit(row) {
        if (!row || !row.customerId) {
            return;
        }

        if (!requirePmsPermission("guests.update")) {
            return;
        }

        window.Zaaer.GuestVisitorForm.open({
            mode: "edit",
            customerId: row.customerId,
            onGuestUpdated(customerId, cust) {
                refreshGuestProfileOnReservation(customerId, cust, row);
            },
            t,
            isArabic,
            pageCtx,
            hotelCode: window.Zaaer.ApiService.getHotelCode()
        });
    }

    function getReservationRouteIdForNotes() {
        const fromDetail = pageCtx.detail ? preferZaaerRouteKey(pageCtx.detail) : null;
        const routeId = fromDetail != null ? fromDetail : pageCtx.routeId;
        return routeId != null && routeId !== "" ? Number(routeId) : null;
    }

    function getNotesHotelId() {
        return resolveNumericHotelIdFromPageContext();
    }

    function sortReservationNotesChronologically(notes) {
        return (notes || []).slice().sort((a, b) => {
            const ta = new Date(a.createdAt).getTime() || 0;
            const tb = new Date(b.createdAt).getTime() || 0;
            if (ta !== tb) {
                return ta - tb;
            }
            return (Number(a.noteId) || 0) - (Number(b.noteId) || 0);
        });
    }

    function setReservationNotesPanelState($panel, isEmpty) {
        if (!$panel || !$panel.length) {
            return;
        }
        $panel.toggleClass("res-notes-panel--empty", !!isEmpty);
    }

    function canUseReservationNotes() {
        return (
            getReservationRouteIdForNotes() != null &&
            !pageCtx.isClientNewReservation &&
            !pageCtx.checkoutUiPendingFirstSave &&
            !reservationGridsActionsDisabled()
        );
    }

    function resolveNotesListCount(listDto) {
        const payload = listDto || { count: 0, notes: [] };
        const notes = Array.isArray(payload.notes) ? payload.notes : [];
        const countRaw = payload.count;
        if (countRaw != null && countRaw !== "" && Number.isFinite(Number(countRaw))) {
            return Math.max(0, Number(countRaw));
        }
        return notes.length;
    }

    function refreshNotesBadge(count) {
        if (count != null && count !== "") {
            pageCtx.notesCount = Math.max(0, Number(count) || 0);
            if (pageCtx.detail) {
                pageCtx.detail.notesCount = pageCtx.notesCount;
            }
        }

        const $badge = $("#reservationNotesHost .res-notes-badge");
        const $btn = $("#reservationNotesHost .res-notes-btn");
        if (!$badge.length) {
            return;
        }

        if (pageCtx.notesCount > 0) {
            $badge.text(pageCtx.notesCount > 99 ? "99+" : String(pageCtx.notesCount)).show();
        } else {
            $badge.hide();
        }

        if ($btn.length) {
            const enabled = canUseReservationNotes();
            const checkedOut = reservationGridsActionsDisabled();
            $btn.prop("disabled", !enabled);
            let hint = t("reservationDetail.notes.open");
            if (checkedOut) {
                hint = t("reservationDetail.permissions.checkedOut");
            } else if (!enabled) {
                hint = t("reservationDetail.notes.requiresSave");
            }
            $btn.attr("title", hint);
        }
    }

    function initReservationNotesButton() {
        const $host = $("#reservationNotesHost");
        if (!$host.length) {
            return;
        }

        $host.empty();
        const $btn = $("<button>", {
            type: "button",
            class: "res-notes-btn",
            "aria-label": t("reservationDetail.notes.open")
        });
        $btn.append($("<i>").addClass("dx-icon dx-icon-comment"));
        $btn.append($("<span>").addClass("res-notes-badge").hide().text("0"));
        $btn.on("click", () => {
            if (!canUseReservationNotes()) {
                const msg = reservationGridsActionsDisabled()
                    ? t("reservationDetail.permissions.checkedOut")
                    : t("reservationDetail.notes.requiresSave");
                DevExpress.ui.notify(msg, "warning", 3200);
                return;
            }
            openReservationNotesPopup();
        });
        $host.append($btn);
        pageCtx.notesCount = (pageCtx.detail && pageCtx.detail.notesCount) || 0;
        refreshNotesBadge();
    }

    const NOTE_ATTACHMENT_MAX_BYTES = 10 * 1024 * 1024;
    const NOTE_ATTACHMENT_ACCEPT = "image/jpeg,image/png,image/gif,image/webp,image/bmp,application/pdf,.jpg,.jpeg,.png,.gif,.webp,.bmp,.pdf";

    function isNoteAttachmentImage(note) {
        const ct = `${(note && note.attachmentContentType) || ""}`.toLowerCase();
        if (ct.startsWith("image/")) {
            return true;
        }

        const path = `${(note && note.attachmentPath) || ""}`.toLowerCase();
        return /\.(jpg|jpeg|png|gif|webp|bmp)$/i.test(path);
    }

    function isAllowedNoteAttachmentFile(file) {
        if (!file) {
            return false;
        }

        if (file.size > NOTE_ATTACHMENT_MAX_BYTES) {
            return false;
        }

        const name = `${file.name || ""}`.toLowerCase();
        const type = `${file.type || ""}`.toLowerCase();
        if (type.startsWith("image/") || type === "application/pdf") {
            return true;
        }

        return /\.(jpg|jpeg|png|gif|webp|bmp|pdf)$/i.test(name);
    }

    function appendNoteAttachmentToBubble($bubble, note) {
        if (!note || !note.hasAttachment || !note.attachmentPath) {
            return;
        }

        const href = note.attachmentPath;
        const label = note.attachmentOriginalName || t("reservationDetail.notes.viewAttachment");
        const $wrap = $("<div>").addClass("res-notes-attachment").appendTo($bubble);

        if (isNoteAttachmentImage(note)) {
            $("<a>", { href, target: "_blank", rel: "noopener noreferrer" })
                .append($("<img>", { src: href, alt: label }))
                .appendTo($wrap);
        } else {
            $("<a>", {
                href,
                target: "_blank",
                rel: "noopener noreferrer",
                class: "res-notes-attachment-link",
                text: label
            }).appendTo($wrap);
        }
    }

    function formatNoteDaySep(value) {
        const d = value ? new Date(value) : null;
        if (!d || Number.isNaN(d.getTime())) {
            return "";
        }

        return new Intl.DateTimeFormat(isArabic() ? "ar-SA" : "en-GB", {
            weekday: "short",
            year: "numeric",
            month: "short",
            day: "numeric"
        }).format(d);
    }

    function renderReservationNotesThread($thread, notes, handlers, $panel) {
        if (!$thread || !$thread.length) {
            return;
        }

        const sorted = sortReservationNotesChronologically(notes);
        const isEmpty = !sorted.length;
        if ($panel && $panel.length) {
            setReservationNotesPanelState($panel, isEmpty);
        }

        $thread.empty();
        if (isEmpty) {
            return;
        }

        let lastDayKey = null;
        sorted.forEach((note) => {
            const created = note.createdAt ? new Date(note.createdAt) : null;
            const dayKey =
                created && !Number.isNaN(created.getTime())
                    ? `${created.getFullYear()}-${created.getMonth()}-${created.getDate()}`
                    : null;
            if (dayKey && dayKey !== lastDayKey) {
                lastDayKey = dayKey;
                $("<div>").addClass("res-notes-day-sep").text(formatNoteDaySep(note.createdAt)).appendTo($thread);
            }

            const isGuest = note.noteType === "guest";
            const typeLabel =
                note.noteType === "guest"
                    ? t("reservationDetail.actions.noteGuest")
                    : t("reservationDetail.actions.noteInternal");
            const edited =
                note.updatedAt &&
                note.createdAt &&
                new Date(note.updatedAt).getTime() > new Date(note.createdAt).getTime();

            const $row = $("<div>").addClass("res-notes-msg-row").appendTo($thread);
            $("<div>")
                .addClass("res-notes-avatar")
                .text((note.createdByDisplayName || "?").trim().charAt(0).toUpperCase())
                .appendTo($row);
            const $bubble = $("<article>").addClass("res-notes-bubble").appendTo($row);
            if (isGuest) {
                $bubble.addClass("res-notes-bubble--guest");
            }

            const $head = $("<div>").addClass("res-notes-bubble-head").appendTo($bubble);
            $("<span>").addClass("res-notes-bubble-author").text(note.createdByDisplayName || "—").appendTo($head);
            const $type = $("<span>").addClass("res-notes-bubble-type").text(typeLabel).appendTo($head);
            if (isGuest) {
                $type.addClass("res-notes-bubble-type--guest");
            }
            if (edited) {
                $("<span>").addClass("res-notes-bubble-edited").text(t("reservationDetail.notes.edited")).appendTo($head);
            }
            const noteText = `${note.noteText || ""}`.trim();
            if (noteText) {
                $("<div>").addClass("res-notes-bubble-text").text(noteText).appendTo($bubble);
            }
            appendNoteAttachmentToBubble($bubble, note);
            $("<div>").addClass("res-notes-bubble-time").text(formatDateTimeEn(note.createdAt)).appendTo($bubble);

            if (note.canEdit || note.canDelete) {
                const $actions = $("<div>").addClass("res-notes-bubble-actions").appendTo($bubble);
                if (note.canEdit) {
                    $("<div>").appendTo($actions).dxButton({
                        text: t("reservationDetail.notes.edit"),
                        icon: "edit",
                        stylingMode: "text",
                        type: "normal",
                        onClick() {
                            handlers.onEdit(note);
                        }
                    });
                }
                if (note.canDelete) {
                    $("<div>").appendTo($actions).dxButton({
                        text: t("reservationDetail.notes.delete"),
                        icon: "trash",
                        stylingMode: "text",
                        type: "danger",
                        onClick() {
                            handlers.onDelete(note);
                        }
                    });
                }
            }

        });

        const el = $thread[0];
        if (el) {
            requestAnimationFrame(() => {
                el.scrollTop = el.scrollHeight;
            });
        }
    }

    function openReservationNotesPopup() {
        if (!guardReservationModificationLocked()) {
            return;
        }

        const routeId = getReservationRouteIdForNotes();
        if (routeId == null) {
            DevExpress.ui.notify(t("reservationDetail.notes.requiresSave"), "warning", 3200);
            return;
        }

        const $host = $("<div>").appendTo("body");
        let notesList = { count: 0, notes: [] };
        let editingNoteId = null;
        let typeInst = null;
        let textInst = null;
        let sendBtnInst = null;
        let cancelEditBtnInst = null;
        let removeAttachBtnInst = null;
        let pendingFile = null;
        let removeAttachmentOnSave = false;
        let editingHasAttachment = false;
        let $fileHint = null;
        let $fileInput = null;
        let $panel = null;
        let $thread = null;

        function applyNotesList(listDto) {
            const payload = listDto || { count: 0, notes: [] };
            notesList = payload;
            refreshNotesBadge(resolveNotesListCount(payload));
            if (!$thread || !$thread.length) {
                return payload;
            }
            renderReservationNotesThread($thread, payload.notes, threadHandlers, $panel);
            return payload;
        }

        function syncFileHint() {
            if (!$fileHint) {
                return;
            }

            if (pendingFile) {
                $fileHint.text(`${t("reservationDetail.notes.selectedFile")}: ${pendingFile.name}`);
                return;
            }

            if (editingNoteId && editingHasAttachment && !removeAttachmentOnSave) {
                $fileHint.text(t("reservationDetail.notes.viewAttachment"));
                return;
            }

            $fileHint.text("");
        }

        const threadHandlers = {
            onEdit(note) {
                editingNoteId = note.noteId;
                editingHasAttachment = !!note.hasAttachment;
                pendingFile = null;
                removeAttachmentOnSave = false;
                if (typeInst) {
                    typeInst.option("value", note.noteType || "internal");
                }
                if (textInst) {
                    textInst.option("value", note.noteText || "");
                }
                if (sendBtnInst) {
                    sendBtnInst.option("text", t("reservationDetail.notes.saveEdit"));
                }
                if (cancelEditBtnInst) {
                    cancelEditBtnInst.option("visible", true);
                }
                if (removeAttachBtnInst) {
                    removeAttachBtnInst.option(
                        "visible",
                        editingHasAttachment
                    );
                }
                syncFileHint();
                textInst && textInst.focus();
            },
            onDelete: null
        };

        function resetComposer() {
            editingNoteId = null;
            editingHasAttachment = false;
            pendingFile = null;
            removeAttachmentOnSave = false;
            if ($fileInput && $fileInput.length) {
                $fileInput.val("");
            }
            if (typeInst) {
                typeInst.option("value", "internal");
            }
            if (textInst) {
                textInst.option("value", "");
            }
            if (sendBtnInst) {
                sendBtnInst.option("text", t("reservationDetail.notes.send"));
            }
            if (cancelEditBtnInst) {
                cancelEditBtnInst.option("visible", false);
            }
            if (removeAttachBtnInst) {
                removeAttachBtnInst.option("visible", false);
            }
            syncFileHint();
        }

        async function reloadThread() {
            if (!$thread || !$thread.length) {
                return notesList;
            }

            try {
                const data = await window.Zaaer.ReservationDetailService.loadReservationNotes(
                    routeId,
                    getNotesHotelId()
                );
                return applyNotesList(data || { count: 0, notes: [] });
            } catch (err) {
                DevExpress.ui.notify(
                    (err && err.message) || t("error.loadReservationDetail"),
                    "error",
                    4200
                );
                applyNotesList({ count: 0, notes: [] });
                throw err;
            }
        }

        threadHandlers.onDelete = function confirmDeleteNote(note) {
            DevExpress.ui.dialog
                .confirm(t("reservationDetail.notes.confirmDelete"), t("reservationDetail.notes.delete"))
                .done((ok) => {
                    if (!ok) {
                        return;
                    }

                    const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
                    lp.show();
                    window.Zaaer.ReservationDetailService.deleteReservationNote(
                        note.noteId,
                        routeId,
                        getNotesHotelId()
                    )
                        .then((result) => {
                            applyNotesList(result || { count: 0, notes: [] });
                            if (editingNoteId === note.noteId) {
                                resetComposer();
                            }
                            DevExpress.ui.notify(t("reservationDetail.notes.deleted"), "success", 2400);
                        })
                        .catch((err) => {
                            DevExpress.ui.notify(
                                (err && err.message) || t("error.loadReservationDetail"),
                                "error",
                                4200
                            );
                        })
                        .finally(() => lp.hide());
                });
        };

        async function submitComposer() {
            const text = `${(textInst && textInst.option("value")) || ""}`.trim();
            const willHaveAttachment =
                !!pendingFile ||
                (editingNoteId && editingHasAttachment && !removeAttachmentOnSave);

            if (!text && !willHaveAttachment) {
                DevExpress.ui.notify(t("reservationDetail.notes.contentRequired"), "warning", 2800);
                return;
            }

            const payload = {
                reservationId: routeId,
                hotelId: getNotesHotelId(),
                noteType: (typeInst && typeInst.option("value")) || "internal",
                noteText: text
            };

            const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
            lp.show();
            try {
                let mutation;
                if (editingNoteId) {
                    mutation = await window.Zaaer.ReservationDetailService.updateReservationNote(
                        editingNoteId,
                        payload,
                        pendingFile,
                        { removeAttachment: removeAttachmentOnSave }
                    );
                    DevExpress.ui.notify(t("reservationDetail.notes.updated"), "success", 2400);
                } else {
                    mutation = await window.Zaaer.ReservationDetailService.createReservationNote(
                        payload,
                        pendingFile
                    );
                    DevExpress.ui.notify(t("reservationDetail.notes.saved"), "success", 2400);
                }
                resetComposer();
                if (mutation && mutation.list) {
                    applyNotesList(mutation.list);
                } else {
                    await reloadThread();
                }
            } catch (err) {
                DevExpress.ui.notify((err && err.message) || t("error.loadReservationDetail"), "error", 4200);
            } finally {
                lp.hide();
            }
        }

        $host.dxPopup({
            title: t("reservationDetail.notes.title"),
            visible: true,
            width: Math.min(680, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "72vh",
            showCloseButton: true,
            dragEnabled: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-notes-popup res-extra-popup res-extra-select-popup" },
            contentTemplate(content) {
                $panel = $("<div>").addClass("res-notes-panel res-notes-panel--empty").appendTo(content);
                const $threadWrap = $("<div>").addClass("res-notes-thread-wrap").appendTo($panel);
                $thread = $("<div>").addClass("res-notes-thread").appendTo($threadWrap);
                $("<div>")
                    .addClass("res-notes-empty-banner")
                    .text(t("reservationDetail.notes.emptyCompact"))
                    .appendTo($panel);
                const $composer = $("<div>").addClass("res-notes-composer").appendTo($panel);
                const $row = $("<div>").addClass("res-notes-composer-row").appendTo($composer);
                const $typeWrap = $("<div>").addClass("res-notes-composer-type").appendTo($row);
                const $textWrap = $("<div>").addClass("res-notes-composer-text").appendTo($row);
                const $actWrap = $("<div>").addClass("res-notes-composer-actions").appendTo($row);

                $typeWrap.dxSelectBox({
                    items: actionOptionItems("noteType"),
                    valueExpr: "id",
                    displayExpr: "text",
                    value: "internal",
                    openOnFieldClick: true,
                    onInitialized(e) {
                        typeInst = e.component;
                    }
                });
                $textWrap.dxTextArea({
                    minHeight: 72,
                    maxLength: 2000,
                    placeholder: t("reservationDetail.actions.notes"),
                    onInitialized(e) {
                        textInst = e.component;
                    }
                });
                $("<div>").appendTo($actWrap).dxButton({
                    text: t("reservationDetail.notes.send"),
                    type: "default",
                    stylingMode: "contained",
                    onInitialized(e) {
                        sendBtnInst = e.component;
                    },
                    async onClick() {
                        await submitComposer();
                    }
                });
                $("<div>").appendTo($actWrap).dxButton({
                    text: t("reservationDetail.notes.cancelEdit"),
                    stylingMode: "text",
                    visible: false,
                    onInitialized(e) {
                        cancelEditBtnInst = e.component;
                    },
                    onClick() {
                        resetComposer();
                    }
                });

                const $attachRow = $("<div>").addClass("res-notes-composer-attach-row").appendTo($composer);
                $fileInput = $("<input>", {
                    type: "file",
                    accept: NOTE_ATTACHMENT_ACCEPT,
                    class: "res-notes-file-input",
                    css: { display: "none" }
                }).appendTo($attachRow);
                $fileInput.on("change", function () {
                    const file = this.files && this.files[0];
                    if (!file) {
                        pendingFile = null;
                        syncFileHint();
                        return;
                    }
                    if (!isAllowedNoteAttachmentFile(file)) {
                        DevExpress.ui.notify(t("reservationDetail.notes.invalidFileType"), "warning", 3200);
                        this.value = "";
                        pendingFile = null;
                        syncFileHint();
                        return;
                    }
                    pendingFile = file;
                    removeAttachmentOnSave = false;
                    syncFileHint();
                });

                $("<div>").appendTo($attachRow).dxButton({
                    text: t("reservationDetail.notes.attach"),
                    icon: "attach",
                    stylingMode: "outlined",
                    type: "normal",
                    onClick() {
                        $fileInput && $fileInput.trigger("click");
                    }
                });

                $fileHint = $("<span>").addClass("res-notes-file-hint").appendTo($attachRow);

                $("<div>")
                    .addClass("res-notes-composer-remove-attach")
                    .appendTo($attachRow)
                    .dxButton({
                        text: t("reservationDetail.notes.removeAttachment"),
                        icon: "clear",
                        stylingMode: "text",
                        type: "danger",
                        visible: false,
                        onInitialized(e) {
                            removeAttachBtnInst = e.component;
                        },
                        onClick() {
                            pendingFile = null;
                            if ($fileInput && $fileInput.length) {
                                $fileInput.val("");
                            }
                            if (editingNoteId && editingHasAttachment) {
                                removeAttachmentOnSave = true;
                            }
                            syncFileHint();
                        }
                    });

                void reloadThread();
            },
            onShown() {
                if ($thread && $thread.length && (!notesList.notes || !notesList.notes.length)) {
                    void reloadThread();
                }
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function reservationActionItems() {
        if (reservationGridsActionsDisabled()) {
            return [];
        }

        const catalog = [
            { id: "discount", text: t("reservationDetail.actions.addDiscount"), icon: "money" },
            { id: "penalty", text: t("reservationDetail.actions.addPenalty"), icon: "warning" },
            { id: "package", text: t("reservationDetail.extras.addPackage"), icon: "plus" }
        ];

        return catalog.filter((item) => {
            const map = RESERVATION_ADJUSTMENT_ACTIONS.find((x) => x.id === item.id);
            return map ? hasPmsPermission(map.permission) : true;
        });
    }

    function initReservationActions() {
        const items = reservationActionItems();
        $("#reservationActions").dxDropDownButton({
            text: t("reservationDetail.actions.more"),
            icon: "menu",
            type: "default",
            stylingMode: "contained",
            elementAttr: { class: "res-header-actions-dd" },
            visible: items.length > 0,
            items: items,
            keyExpr: "id",
            displayExpr: "text",
            showArrowIcon: true,
            rtlEnabled: isArabic(),
            dropDownOptions: {
                width: 260,
                wrapperAttr: { class: "res-actions-dropdown-popup" }
            },
            onItemClick(e) {
                handleReservationAction(e.itemData && e.itemData.id);
            }
        });
    }

    function handleReservationAction(actionId) {
        if (!guardReservationModificationLocked()) {
            return;
        }

        const perm = RESERVATION_ADJUSTMENT_ACTIONS.find((x) => x.id === actionId);
        if (perm && !requirePmsPermission(perm.permission)) {
            return;
        }

        if (actionId === "package") {
            openExtraPackagePopup();
            return;
        }

        if (actionId === "discount") {
            openReservationDiscountPopup(null);
            return;
        }

        if (actionId === "penalty") {
            openReservationActionPopup(actionId);
        }
    }

    function actionOptionItems(kind) {
        if (kind === "discountScope") {
            return [
                { id: "reservation", text: t("reservationDetail.actions.scopeReservation") },
                { id: "selectedUnits", text: t("reservationDetail.actions.scopeSelectedUnits") }
            ];
        }

        if (kind === "discountMethod") {
            return [
                { id: "amount", text: t("reservationDetail.actions.discountAmount") },
                { id: "percentage", text: t("reservationDetail.actions.discountPercent") }
            ];
        }

        if (kind === "noteType") {
            return [
                { id: "internal", text: t("reservationDetail.actions.noteInternal") },
                { id: "guest", text: t("reservationDetail.actions.noteGuest") }
            ];
        }

        return [];
    }

    function actionFormConfig(actionId) {
        if (actionId === "discount") {
            return {
                titleKey: "reservationDetail.actions.discountTitle",
                saveKey: "reservationDetail.actions.applyDiscount",
                doneKey: "reservationDetail.actions.discountPrepared",
                formData: { scope: "reservation", unitIds: [], method: "amount", value: 0, reason: "" },
                items: [
                    {
                        dataField: "scope",
                        label: { text: t("reservationDetail.actions.discountScope") },
                        editorType: "dxSelectBox",
                        editorOptions: {
                            items: actionOptionItems("discountScope"),
                            valueExpr: "id",
                            displayExpr: "text",
                            openOnFieldClick: true,
                            onValueChanged(e) {
                                const formInst = activeReservationActionForm;
                                if (!formInst) {
                                    return;
                                }

                                const showUnits = e.value === "selectedUnits";
                                formInst.itemOption("unitIds", "visible", showUnits);
                                if (!showUnits) {
                                    formInst.updateData("unitIds", []);
                                }
                            }
                        }
                    },
                    {
                        dataField: "unitIds",
                        label: { text: t("reservationDetail.actions.discountUnits") },
                        editorType: "dxTagBox",
                        visible: false,
                        editorOptions: {
                            dataSource: getCompanionUnitLookupRows(),
                            valueExpr: "unitId",
                            displayExpr: "label",
                            showSelectionControls: true,
                            searchEnabled: true,
                            openOnFieldClick: true,
                            placeholder: t("reservationDetail.actions.discountUnitsPlaceholder")
                        }
                    },
                    {
                        dataField: "method",
                        label: { text: t("reservationDetail.actions.discountMethod") },
                        editorType: "dxSelectBox",
                        editorOptions: {
                            items: actionOptionItems("discountMethod"),
                            valueExpr: "id",
                            displayExpr: "text",
                            openOnFieldClick: true
                        }
                    },
                    {
                        dataField: "value",
                        label: { text: t("reservationDetail.actions.discountValue") },
                        editorType: "dxNumberBox",
                        editorOptions: { min: 0, format: "#,##0.##", showSpinButtons: true }
                    },
                    {
                        dataField: "reason",
                        label: { text: t("reservationDetail.actions.reason") },
                        editorType: "dxTextArea",
                        editorOptions: { minHeight: 86, maxLength: 500 }
                    }
                ]
            };
        }

        if (actionId === "penalty") {
            return {
                titleKey: "reservationDetail.actions.penaltyTitle",
                saveKey: "reservationDetail.actions.preparePenalty",
                doneKey: "reservationDetail.actions.penaltyPrepared",
                formData: { penaltyCatalog: null, amount: 0, postingDate: new Date(), notes: "" },
                items: [
                    {
                        dataField: "penaltyCatalog",
                        label: { text: t("reservationDetail.actions.penaltyCatalog") },
                        editorType: "dxSelectBox",
                        editorOptions: {
                            dataSource: penaltySelectItems(),
                            valueExpr: "penaltyId",
                            displayExpr: penaltyDisplayExpr,
                            searchEnabled: true,
                            searchExpr: ["penaltyName", "penaltyNameAr", "penaltyType"],
                            placeholder: t("reservationDetail.actions.penaltyCatalogPlaceholder"),
                            noDataText: t("reservationDetail.actions.penaltyCatalogEmpty"),
                            openOnFieldClick: true,
                            onValueChanged(e) {
                                if (!activeReservationActionForm) {
                                    return;
                                }

                                if (e.value === "__ADD__") {
                                    e.component.option("value", null);
                                    openCreatePenaltyPopup((created) => {
                                        const editor = activeReservationActionForm.getEditor("penaltyCatalog");
                                        if (editor) {
                                            editor.option("dataSource", penaltySelectItems());
                                            editor.option("value", created.penaltyId);
                                        }
                                        activeReservationActionForm.updateData("amount", Number(created.baseAmount) || 0);
                                    });
                                    return;
                                }

                                const selected = findPenaltyCatalog(e.value);
                                if (selected) {
                                    activeReservationActionForm.updateData("amount", Number(selected.baseAmount) || 0);
                                }
                            }
                        },
                        validationRules: [
                            {
                                type: "required",
                                message: t("reservationDetail.actions.penaltyCatalogRequired")
                            }
                        ]
                    },
                    {
                        dataField: "amount",
                        label: { text: t("reservationDetail.actions.penaltyAmount") },
                        editorType: "dxNumberBox",
                        editorOptions: { min: 0, format: "#,##0.##", showSpinButtons: true }
                    },
                    {
                        dataField: "postingDate",
                        label: { text: t("reservationDetail.actions.postingDate") },
                        editorType: "dxDateBox",
                        editorOptions: { type: "date", displayFormat: "dd/MM/yyyy", openOnFieldClick: true }
                    },
                    {
                        dataField: "notes",
                        label: { text: t("reservationDetail.actions.notes") },
                        editorType: "dxTextArea",
                        editorOptions: { minHeight: 86, maxLength: 500 }
                    }
                ]
            };
        }

        return null;
    }

    async function applyReservationDiscountFromForm(formData, editDiscountId) {
        if (!requirePmsPermission("reservations.discount")) {
            return false;
        }

        const routeId =
            pageCtx.routeId ||
            (pageCtx.detail && pageCtx.detail.zaaerId) ||
            (pageCtx.detail && pageCtx.detail.reservationId);
        if (routeId == null || routeId === "") {
            DevExpress.ui.notify(t("reservationDetail.missingId"), "warning", 3200);
            return false;
        }

        const value = Number(formData && formData.value);
        if (!Number.isFinite(value) || value <= 0) {
            DevExpress.ui.notify(t("reservationDetail.actions.discountValueRequired"), "warning", 3200);
            return false;
        }

        const scope = (formData && formData.scope) || "reservation";
        const unitIds =
            scope === "selectedUnits" && Array.isArray(formData.unitIds)
                ? formData.unitIds.map((id) => Number(id)).filter((id) => Number.isFinite(id) && id > 0)
                : [];
        if (scope === "selectedUnits" && unitIds.length === 0) {
            DevExpress.ui.notify(t("reservationDetail.actions.discountUnitsRequired"), "warning", 3200);
            return false;
        }

        const payload = {
            reservationId: Number(routeId),
            hotelId: pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId),
            applyScope: scope,
            unitIds: scope === "selectedUnits" ? unitIds : undefined,
            calculationMethod: formData && formData.method === "percentage" ? "Percentage" : "Amount",
            calculationValue: value,
            description: formData && formData.reason != null ? `${formData.reason}`.trim() : ""
        };

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        lp.show();
        try {
            const editId = editDiscountId != null ? Number(editDiscountId) : NaN;
            const result =
                Number.isFinite(editId) && editId > 0
                    ? await window.Zaaer.ReservationDetailService.updateDiscount(editId, payload)
                    : await window.Zaaer.ReservationDetailService.applyDiscount(payload);

            applyDiscountMutationToPage(result);
            DevExpress.ui.notify(
                Number.isFinite(editId) && editId > 0
                    ? t("reservationDetail.discounts.updated")
                    : t("reservationDetail.actions.discountApplied"),
                "success",
                2800
            );
            return true;
        } catch (err) {
            const msg = err && err.message ? String(err.message) : t("error.loadReservationDetail");
            DevExpress.ui.notify(msg, "error", 4200);
            return false;
        } finally {
            lp.hide();
        }
    }

    function openReservationDiscountPopup(editRow) {
        if (!guardReservationModificationLocked()) {
            return;
        }

        if (!requirePmsPermission("reservations.discount")) {
            return;
        }

        const isEdit = !!(editRow && editRow.discountId);
        const cfg = actionFormConfig("discount");
        const methodRaw = editRow && editRow.calculationMethod ? `${editRow.calculationMethod}` : "Amount";
        const initialForm = isEdit
            ? {
                  scope: editRow.applyScope || (editRow.applyOn === "Rent" ? "selectedUnits" : "reservation"),
                  unitIds: editRow.unitId != null ? [editRow.unitId] : [],
                  method: methodRaw.toLowerCase() === "percentage" ? "percentage" : "amount",
                  value: Number(editRow.calculationValue) || 0,
                  reason: editRow.description || ""
              }
            : cfg.formData;

        const $host = $("<div>").appendTo("body");
        let formInstance = null;
        activeReservationActionForm = null;
        const editDiscountId = isEdit ? editRow.discountId : null;

        $host.dxPopup({
            title: isEdit ? t("reservationDetail.discounts.editTitle") : t(cfg.titleKey),
            visible: true,
            width: () => Math.min(560, Math.max(340, window.innerWidth - 32)),
            height: "auto",
            maxHeight: "78vh",
            showCloseButton: true,
            dragEnabled: true,
            wrapperAttr: { class: "res-action-popup" },
            contentTemplate(content) {
                $("<div>")
                    .addClass("res-action-form")
                    .appendTo(content)
                    .dxForm({
                        formData: initialForm,
                        colCount: 1,
                        labelLocation: "top",
                        items: cfg.items,
                        onInitialized(e) {
                            formInstance = e.component;
                            activeReservationActionForm = e.component;
                            const scope = (e.component.option("formData") || {}).scope;
                            e.component.itemOption("unitIds", "visible", scope === "selectedUnits");
                        }
                    });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "before",
                    options: {
                        text: t("reservationDetail.actions.close"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: isEdit ? t("reservationDetail.discounts.saveEdit") : t(cfg.saveKey),
                        type: "default",
                        stylingMode: "contained",
                        async onClick() {
                            const formData = formInstance ? formInstance.option("formData") : {};
                            const ok = await applyReservationDiscountFromForm(formData, editDiscountId);
                            if (ok) {
                                $host.dxPopup("instance").hide();
                            }
                        }
                    }
                }
            ],
            onHidden() {
                if (activeReservationActionForm === formInstance) {
                    activeReservationActionForm = null;
                }
                $host.remove();
            }
        });
    }

    function openReservationActionPopup(actionId) {
        if (!guardReservationModificationLocked()) {
            return;
        }

        if (actionId === "penalty" && !requirePmsPermission("reservations.penalty")) {
            return;
        }

        const cfg = actionFormConfig(actionId);
        if (!cfg) {
            return;
        }
        const $host = $("<div>").appendTo("body");
        let formInstance = null;
        activeReservationActionForm = null;

        $host.dxPopup({
            title: t(cfg.titleKey),
            visible: true,
            width: () => Math.min(560, Math.max(340, window.innerWidth - 32)),
            height: "auto",
            maxHeight: "78vh",
            showCloseButton: true,
            dragEnabled: true,
            wrapperAttr: { class: "res-action-popup" },
            contentTemplate(content) {
                $("<div>")
                    .addClass("res-action-form")
                    .appendTo(content)
                    .dxForm({
                        formData: cfg.formData,
                        colCount: 1,
                        labelLocation: "top",
                        items: cfg.items,
                        onInitialized(e) {
                            formInstance = e.component;
                            activeReservationActionForm = e.component;
                            if (actionId === "discount") {
                                const scope = (e.component.option("formData") || {}).scope;
                                e.component.itemOption("unitIds", "visible", scope === "selectedUnits");
                            }
                        }
                    });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "before",
                    options: {
                        text: t("reservationDetail.actions.close"),
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t(cfg.saveKey),
                        type: "default",
                        stylingMode: "contained",
                        async onClick() {
                            const formData = formInstance ? formInstance.option("formData") : {};
                            if (actionId === "discount") {
                                const ok = await applyReservationDiscountFromForm(formData);
                                if (ok) {
                                    $host.dxPopup("instance").hide();
                                }
                                return;
                            }

                            DevExpress.ui.notify(t(cfg.doneKey), "info", 3200);
                            $host.dxPopup("instance").hide();
                        }
                    }
                }
            ],
            onHidden() {
                if (activeReservationActionForm === formInstance) {
                    activeReservationActionForm = null;
                }
                $host.remove();
            }
        });
    }

    function reservationPageTabItems() {
        return [
            {
                id: "details",
                text: t("reservationDetail.pageTabs.details"),
                icon: "home"
            },
            {
                id: "payments",
                text: t("reservationDetail.pageTabs.payments"),
                icon: "money"
            }
        ];
    }

    function renderReservationPageTabTitle(item, _index, element) {
        const $el = $(element).empty();
        $("<div>")
            .addClass("res-page-tab-title")
            .append(
                $("<span>")
                    .addClass(`dx-icon dx-icon-${item.icon || "folder"}`)
                    .attr("aria-hidden", "true"),
                $("<span>").addClass("res-page-tab-title-text").text(item.text || "")
            )
            .appendTo($el);
    }

    /**
     * Payment toolbar actions — each item maps to pms_permissions.permission_code.
     * Grant/revoke via role-permissions page (pms_role_permissions.granted).
     */
    function paymentActionItems() {
        const catalog = [
            {
                id: "receipt",
                permission: "payments.create",
                text: t("reservationDetail.payments.actions.addReceipt"),
                icon: "plus"
            },
            {
                id: "disbursement",
                permission: "payments.refund",
                text: t("reservationDetail.payments.actions.addDisbursement"),
                icon: "minus"
            },
            {
                id: "invoice",
                permission: "finance.invoice.create",
                text: t("reservationDetail.payments.actions.addInvoice"),
                icon: "doc"
            },
            {
                id: "promissory",
                permission: "finance.promissory.create",
                text: t("reservationDetail.payments.actions.addPromissory"),
                icon: "card"
            }
        ];

        return catalog.filter((item) => hasPmsPermission(item.permission));
    }

    function isPaymentsMobileViewport() {
        return window.matchMedia("(max-width: 720px)").matches;
    }

    function resDetailColumnMatchesKey(col, key) {
        if (!col || !key) {
            return false;
        }

        if (col.name === key || col.dataField === key) {
            return true;
        }

        if (key === "unitNo" && col.cssClass && String(col.cssClass).includes("res-units-col-unit-no")) {
            return true;
        }

        return false;
    }

    function paymentGridMobileDataKeys(kind) {
        if (kind === "receipts" || kind === "disbursements") {
            return ["voucherCode", "amountPaid", "receiptNo"];
        }

        if (kind === "invoices") {
            return ["invoiceNo", "invoiceDate", "totalAmount"];
        }

        if (kind === "promissory") {
            return ["number", "maturityDate", "amount"];
        }

        return ["number", "date", "amount"];
    }

    /** On mobile, keep priority data columns (incl. amount) plus action/hidden columns. */
    function pickResDetailMobileColumns(allColumns, priorityKeys, maxDataCols) {
        const maxData = maxDataCols == null ? 3 : maxDataCols;
        if (!isPaymentsMobileViewport()) {
            return allColumns;
        }

        const out = [];
        const seen = new Set();
        const push = (col) => {
            if (!col || seen.has(col)) {
                return;
            }

            seen.add(col);
            out.push(col);
        };

        let dataAdded = 0;
        (priorityKeys || []).forEach((key) => {
            if (dataAdded >= maxData) {
                return;
            }

            const col = allColumns.find((c) => resDetailColumnMatchesKey(c, key));
            if (col && col.type !== "buttons" && col.visible !== false) {
                push(col);
                dataAdded += 1;
            }
        });

        allColumns.forEach((c) => {
            if (c.type === "buttons" || (c.name && /Actions$/i.test(String(c.name)))) {
                push(c);
            } else if (c.visible === false || c.showInColumnChooser === false) {
                push(c);
            }
        });

        return out.length ? out : allColumns.slice(0, Math.min(3, allColumns.length));
    }

    function refreshResDetailGridColumnsForViewport() {
        const ug = $("#unitsGrid").dxDataGrid("instance");
        if (ug) {
            ug.option("columns", buildUnitsGridColumns());
            applyUnitsGridLayoutOptions();
        }

        refreshGuestGridColumns();

        const cg = $("#companionsGrid").dxDataGrid("instance");
        if (cg) {
            cg.option("columns", buildCompanionGridColumns());
        }

        const xg = $("#extrasGrid").dxDataGrid("instance");
        if (xg) {
            xg.option("columns", buildExtrasGridColumns());
        }

        refreshCompanyGrid();

        refreshPeriodsGridColumns();

        $(".res-payment-grid").each(function () {
            const kind = $(this).data("paymentKind");
            if (!kind) {
                return;
            }

            try {
                const inst = $(this).dxDataGrid("instance");
                if (!inst) {
                    return;
                }

                inst.option("columns", paymentGridColumns(kind));
                const mobile = isPaymentsMobileViewport();
                const isVoucherGrid = kind === "receipts" || kind === "disbursements";
                inst.option("columnAutoWidth", mobile ? false : isVoucherGrid);
                inst.option("wordWrapEnabled", !mobile);
                if (mobile && isVoucherGrid) {
                    const po = window.Zaaer.PmsGridOptions;
                    inst.option(
                        "scrolling",
                        po.scrollingOptions({
                            mode: "standard",
                            rowRenderingMode: "standard",
                            columnRenderingMode: "standard",
                            useNative: false,
                            scrollByContent: true,
                            showScrollbar: "always"
                        })
                    );
                }
            } catch {
                /* grid not ready */
            }
        });
    }

    function paymentReceiptPopupWidth() {
        const margin = isPaymentsMobileViewport() ? 16 : 32;
        const maxDesktop = 560;
        const w = window.innerWidth - margin;
        return Math.min(isPaymentsMobileViewport() ? w : maxDesktop, Math.max(320, w));
    }

    function resetPaymentCreditNotesProbe() {
        pageCtx.paymentCreditNotes = { probed: false, visible: false, count: 0 };
    }

    function probeCreditNotesTabOnDemand() {
        const state = pageCtx.paymentCreditNotes || { probed: false, visible: false, count: 0 };
        pageCtx.paymentCreditNotes = state;

        if (state.probed) {
            return Promise.resolve(state.visible);
        }

        if (!hasPmsPermission("finance.credit_note.view")) {
            state.probed = true;
            state.visible = false;
            return Promise.resolve(false);
        }

        const routeId = resolvePaymentReservationRouteId(pageCtx.detail);
        if (!routeId) {
            state.probed = true;
            state.visible = false;
            return Promise.resolve(false);
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || typeof svc.countCreditNotesByReservation !== "function") {
            state.probed = true;
            state.visible = false;
            return Promise.resolve(false);
        }

        return svc.countCreditNotesByReservation(routeId).then((count) => {
            const wasVisible = !!state.visible;
            state.probed = true;
            state.count = Math.max(0, Number(count) || 0);
            state.visible = state.count > 0;
            pageCtx.paymentTabCounts = pageCtx.paymentTabCounts || {};
            if (state.visible) {
                pageCtx.paymentTabCounts.credit_notes = state.count;
            } else {
                delete pageCtx.paymentTabCounts.credit_notes;
            }

            if (state.visible !== wasVisible) {
                injectCreditNotesPaymentTab();
            } else {
                updatePaymentTabBadgesFromCounts();
            }

            if (state.visible) {
                return loadReservationPaymentRows("credit_notes").then((rows) => {
                    syncPaymentRowsCache("credit_notes", rows);
                    return state.visible;
                });
            }

            renderPaymentsSummaryBar();
            return state.visible;
        });
    }

    function injectCreditNotesPaymentTab() {
        const $host = $(".res-payment-tabpanel-host");
        if (!$host.length) {
            return;
        }

        let inst = null;
        try {
            inst = $host.dxTabPanel("instance");
        } catch {
            inst = null;
        }

        if (!inst) {
            return;
        }

        const selectedKey = inst.option("selectedItem") && inst.option("selectedItem").id;
        const items = getPaymentTabItemsWithCounts();
        const selectedIndex = selectedKey
            ? items.findIndex((x) => x.id === selectedKey)
            : inst.option("selectedIndex");

        inst.option("items", items);
        const nextIndex = selectedIndex >= 0 ? selectedIndex : 0;
        inst.option("selectedIndex", nextIndex);
        updatePaymentTabBadgesFromCounts();
    }

    function paymentGridTabs() {
        const tabs = [
            {
                id: "receipts",
                text: t("reservationDetail.payments.tabs.receipts"),
                icon: "plus",
                emptyKey: "reservationDetail.payments.empty.receipts"
            },
            {
                id: "disbursements",
                text: t("reservationDetail.payments.tabs.disbursements"),
                icon: "minus",
                emptyKey: "reservationDetail.payments.empty.disbursements"
            },
            {
                id: "promissory",
                text: t("reservationDetail.payments.tabs.promissory"),
                icon: "card",
                emptyKey: "reservationDetail.payments.empty.promissory"
            },
            {
                id: "invoices",
                text: t("reservationDetail.payments.tabs.invoices"),
                icon: "doc",
                emptyKey: "reservationDetail.payments.empty.invoices"
            }
        ];

        const cnState = pageCtx.paymentCreditNotes;
        if (cnState && cnState.visible) {
            tabs.push({
                id: "credit_notes",
                text: t("reservationDetail.payments.tabs.creditNotes"),
                icon: "undo",
                emptyKey: "reservationDetail.payments.empty.creditNotes"
            });
        }

        return tabs;
    }

    function getPaymentTabItemsWithCounts() {
        const counts = pageCtx.paymentTabCounts || {};
        return paymentGridTabs().map((tab) => ({
            ...tab,
            count: Number(counts[tab.id]) || 0
        }));
    }

    function updatePaymentTabBadgesFromCounts() {
        const $host = $(".res-payment-tabpanel-host");
        if (!$host.length) {
            return;
        }

        const items = getPaymentTabItemsWithCounts();
        $host.find(".dx-tab").each(function (index) {
            const item = items[index];
            if (!item) {
                return;
            }

            const $tab = $(this);
            const $wrap = $tab.find(".res-payment-tab-title-wrap");
            if (!$wrap.length) {
                return;
            }

            $wrap.find(".res-payment-tab-badge").remove();
            const count = Math.max(0, Number(item.count) || 0);
            if (count <= 0) {
                return;
            }

            let badgeClass = "res-payment-tab-badge";
            if (item.id === "receipts") {
                badgeClass += " res-payment-tab-badge--receipts";
            } else if (item.id === "disbursements") {
                badgeClass += " res-payment-tab-badge--disbursements";
            } else if (item.id === "invoices") {
                badgeClass += " res-payment-tab-badge--invoices";
            } else if (item.id === "credit_notes") {
                badgeClass += " res-payment-tab-badge--credit-notes";
            }

            $("<span>")
                .addClass(badgeClass)
                .attr("title", String(count))
                .text(count > 99 ? "99+" : String(count))
                .appendTo($wrap);
        });
    }

    function syncPaymentTabPanelItems() {
        const $host = $(".res-payment-tabpanel-host");
        if (!$host.length) {
            return;
        }

        let inst = null;
        try {
            inst = $host.dxTabPanel("instance");
        } catch {
            inst = null;
        }

        if (!inst) {
            return;
        }

        const selectedKey = inst.option("selectedItem") && inst.option("selectedItem").id;
        const items = getPaymentTabItemsWithCounts();
        const selectedIndex = selectedKey
            ? items.findIndex((x) => x.id === selectedKey)
            : inst.option("selectedIndex");

        const hasMountedGrid = $host.find(".res-payment-grid").length > 0;
        if (hasMountedGrid) {
            updatePaymentTabBadgesFromCounts();
            return;
        }

        inst.option("items", items);
        const nextIndex = selectedIndex >= 0 ? selectedIndex : 0;
        inst.option("selectedIndex", nextIndex);
        const selectedItem = items[nextIndex] || items[0];
        ensurePaymentGridForTab(inst, selectedItem, nextIndex);
    }

    function invalidatePaymentRowsCache() {
        pageCtx.paymentRowsCache = null;
    }

    function syncPaymentRowsCache(kind, rows) {
        if (!pageCtx.paymentRowsCache) {
            pageCtx.paymentRowsCache = {};
        }

        pageCtx.paymentRowsCache[kind] = Array.isArray(rows) ? rows : [];
        renderPaymentsSummaryBar();
    }

    function paymentSummaryRowAmount(row, fieldName) {
        const key = fieldName || "amountPaid";
        const n = Number(row && row[key]);
        return Number.isFinite(n) ? n : 0;
    }

    function buildPaymentsSummaryModel() {
        const cache = pageCtx.paymentRowsCache || {};
        const receipts = (cache.receipts || []).filter((r) => r && !isPaymentRowCancelled(r));
        const disbursements = (cache.disbursements || []).filter((r) => r && !isPaymentRowCancelled(r));
        const promissory = (cache.promissory || []).filter((r) => r && !isPromissoryRowCancelled(r));
        const invoices = cache.invoices || [];
        const creditNotes = cache.credit_notes || [];

        const sumRows = (rows, field) =>
            rows.reduce((total, row) => total + paymentSummaryRowAmount(row, field), 0);

        const rentReceipts = receipts.filter((r) => paymentRowUiReceiptKind(r) === "receipt");
        const depositReceipts = receipts.filter((r) => paymentRowUiReceiptKind(r) === "security_deposit");
        const rentDisbursements = disbursements.filter((r) => paymentRowUiReceiptKind(r) === "refund");
        const depositDisbursements = disbursements.filter(
            (r) => paymentRowUiReceiptKind(r) === "security_deposit_refund"
        );

        return {
            rentReceipts: { count: rentReceipts.length, total: sumRows(rentReceipts, "amountPaid") },
            depositReceipts: { count: depositReceipts.length, total: sumRows(depositReceipts, "amountPaid") },
            rentDisbursements: {
                count: rentDisbursements.length,
                total: sumRows(rentDisbursements, "amountPaid")
            },
            depositDisbursements: {
                count: depositDisbursements.length,
                total: sumRows(depositDisbursements, "amountPaid")
            },
            promissory: {
                count: promissory.length,
                total: sumRows(promissory, "amount"),
                numbers: promissory
                    .map((r) => r.promissoryNo || r.number || "")
                    .filter((x) => x !== "")
            },
            invoices: { count: invoices.length, total: sumRows(invoices, "totalAmount") },
            creditNotes: { count: creditNotes.length, total: sumRows(creditNotes, "creditAmount") }
        };
    }

    function formatPaymentsSummaryNumbers(numbers) {
        const list = (numbers || []).map((x) => String(x).trim()).filter(Boolean);
        if (!list.length) {
            return "";
        }

        const max = 3;
        if (list.length <= max) {
            return list.join(isArabic() ? "، " : ", ");
        }

        const head = list.slice(0, max).join(isArabic() ? "، " : ", ");
        return `${head} +${list.length - max}`;
    }

    function appendPaymentsSummaryCurrency($host) {
        if (isArabic()) {
            $("<img>")
                .attr("src", "/logo/sar-symbol.svg")
                .attr("alt", "ر.س")
                .addClass("res-payments-summary-currency-icon")
                .appendTo($host);
            return;
        }

        $("<span>").addClass("res-payments-summary-currency-code").text("SAR").appendTo($host);
    }

    function paymentsSummaryCardHasValue(data) {
        const total = Number(data && data.total) || 0;
        return Math.abs(total) > 0.001;
    }

    function appendPaymentsSummaryCard($grid, labelKey, data, extraMeta) {
        if (!paymentsSummaryCardHasValue(data)) {
            return false;
        }

        const $card = $("<div>").addClass("res-payments-summary-card").appendTo($grid);

        const $title = $("<div>").addClass("res-payments-summary-card-k").appendTo($card);
        $("<span>").addClass("res-payments-summary-card-label").text(t(labelKey)).appendTo($title);
        $("<span>")
            .addClass("res-payments-summary-card-count")
            .text(`(${data.count || 0})`)
            .appendTo($title);

        const $value = $("<div>").addClass("res-payments-summary-card-v").appendTo($card);
        $("<span>")
            .addClass("res-payments-summary-amt")
            .text(formatMoneyEn(data.total || 0))
            .appendTo($value);
        appendPaymentsSummaryCurrency($value);

        const meta = extraMeta || (data.numbers && data.numbers.length
            ? formatPaymentsSummaryNumbers(data.numbers)
            : "");
        if (meta) {
            $("<div>").addClass("res-payments-summary-meta").text(meta).appendTo($card);
        }

        return true;
    }

    function renderPaymentsSummaryBar() {
        const $host = $("#resPaymentsSummary");
        if (!$host.length) {
            return;
        }

        const model = buildPaymentsSummaryModel();
        $host.empty();

        const $head = $("<div>").addClass("res-payments-summary-head").appendTo($host);
        $("<div>")
            .addClass("res-payments-summary-title")
            .text(t("reservationDetail.payments.summary.title"))
            .appendTo($head);
        $("<div>")
            .addClass("res-payments-summary-sub")
            .text(t("reservationDetail.payments.summary.subtitle"))
            .appendTo($head);

        const $grid = $("<div>").addClass("res-payments-summary-grid").appendTo($host);

        const summaryCards = [
            {
                labelKey: "reservationDetail.payments.summary.rentReceipts",
                data: model.rentReceipts
            },
            {
                labelKey: "reservationDetail.payments.summary.depositReceipts",
                data: model.depositReceipts
            },
            {
                labelKey: "reservationDetail.payments.summary.rentDisbursements",
                data: model.rentDisbursements
            },
            {
                labelKey: "reservationDetail.payments.summary.depositDisbursements",
                data: model.depositDisbursements
            },
            {
                labelKey: "reservationDetail.payments.summary.promissory",
                data: model.promissory,
                meta: model.promissory.numbers.length
                    ? `${t("reservationDetail.payments.summary.numbers")}: ${formatPaymentsSummaryNumbers(model.promissory.numbers)}`
                    : ""
            },
            {
                labelKey: "reservationDetail.payments.summary.invoices",
                data: model.invoices
            }
        ];

        const cnState = pageCtx.paymentCreditNotes;
        if (!cnState || cnState.visible !== false) {
            summaryCards.push({
                labelKey: "reservationDetail.payments.summary.creditNotes",
                data: model.creditNotes
            });
        }

        let visibleCount = 0;
        summaryCards.forEach((card) => {
            if (appendPaymentsSummaryCard($grid, card.labelKey, card.data, card.meta)) {
                visibleCount += 1;
            }
        });

        if (!visibleCount) {
            $grid.remove();
        }
    }

    function refreshPaymentTabCounts() {
        const kinds = ["receipts", "disbursements", "promissory", "invoices"];
        pageCtx.paymentTabCounts = pageCtx.paymentTabCounts || {};
        if (!pageCtx.paymentRowsCache) {
            pageCtx.paymentRowsCache = {};
        }

        return Promise.all(
            kinds.map((kind) =>
                loadReservationPaymentRows(kind).then((rows) => {
                    const list = Array.isArray(rows) ? rows : [];
                    pageCtx.paymentTabCounts[kind] = list.length;
                    syncPaymentRowsCache(kind, list);
                })
            )
        ).then(() => {
            const cnState = pageCtx.paymentCreditNotes;
            if (cnState && cnState.probed && cnState.visible) {
                return loadReservationPaymentRows("credit_notes").then((rows) => {
                    const list = Array.isArray(rows) ? rows : [];
                    pageCtx.paymentTabCounts.credit_notes = list.length;
                    syncPaymentRowsCache("credit_notes", list);
                });
            }

            return null;
        }).then(() => {
            updatePaymentTabBadgesFromCounts();
            renderPaymentsSummaryBar();
        });
    }

    function renderPaymentTabTitle(item, _index, element) {
        const count = Math.max(0, Number(item && item.count) || 0);
        const $el = $(element).empty();
        const $wrap = $("<div>").addClass("res-payment-tab-title-wrap").appendTo($el);
        const $title = $("<div>").addClass("res-payment-tab-title res-payment-tab-title--top").appendTo($wrap);

        $("<span>")
            .addClass(`dx-icon dx-icon-${item.icon || "folder"}`)
            .attr("aria-hidden", "true")
            .appendTo($title);

        $("<span>").addClass("res-payment-tab-title-text").text(item.text || "").appendTo($title);

        if (count > 0) {
            let badgeClass = "res-payment-tab-badge";
            if (item.id === "receipts") {
                badgeClass += " res-payment-tab-badge--receipts";
            } else if (item.id === "disbursements") {
                badgeClass += " res-payment-tab-badge--disbursements";
            }

            $("<span>")
                .addClass(badgeClass)
                .attr("title", String(count))
                .text(count > 99 ? "99+" : String(count))
                .appendTo($wrap);
        }
    }

    function paymentReceiptContext(override) {
        if (override && typeof override === "object") {
            return override;
        }
        const detail = pageCtx.detail || {};
        const reservationRouteId = resolvePaymentReservationRouteId(detail);
        const reservationId = detail.reservationId;
        const reservationZaaerId = detail.zaaerId;
        const hotelId = detail.hotelId || pageCtx.hotelIdParam;
        const primaryGuest = Array.isArray(detail.guests)
            ? detail.guests.find((g) => g && g.isPrimary) || detail.guests[0] || null
            : null;
        const customerId = detail.customerId || (primaryGuest && primaryGuest.customerId) || null;
        const customerZaaerId =
            (primaryGuest && primaryGuest.customerZaaerId) || detail.customerZaaerId || null;
        const reservationNo = (detail.header && detail.header.reservationNo) || "";
        const corporateId =
            detail.corporateId != null && Number(detail.corporateId) > 0
                ? Number(detail.corporateId)
                : detail.company && detail.company.corporateId != null
                  ? Number(detail.company.corporateId)
                  : null;

        return {
            detail,
            reservationId,
            reservationRouteId,
            reservationZaaerId,
            hotelId,
            customerId,
            customerZaaerId,
            corporateId,
            reservationNo
        };
    }

    function refreshAllPaymentGrids() {
        invalidatePaymentRowsCache();
        $(".res-payment-grid").each(function () {
            try {
                $(this).dxDataGrid("instance").refresh();
            } catch {
                /* grid not initialized */
            }
        });
    }

    /** Returns a promise when grid refresh supports it (CustomStore). */
    function refreshAllPaymentGridsAsync() {
        invalidatePaymentRowsCache();
        const jobs = [];
        $(".res-payment-grid").each(function () {
            try {
                const inst = $(this).dxDataGrid("instance");
                const result = inst && typeof inst.refresh === "function" ? inst.refresh() : null;
                if (result && typeof result.then === "function") {
                    jobs.push(result);
                }
            } catch {
                /* grid not initialized */
            }
        });
        return jobs.length ? Promise.all(jobs) : Promise.resolve();
    }

    function schedulePaymentUiBackground(work) {
        const sg = window.Zaaer && window.Zaaer.SaveGuard;
        if (sg) {
            sg.scheduleBackground(work);
            return;
        }
        const chain = work && typeof work.then === "function" ? work : Promise.resolve(work);
        chain.catch((err) => {
            console.error("Payment UI background sync failed", err);
            DevExpress.ui.notify(
                (err && err.message) || t("reservationDetail.payments.grid.loadFailed"),
                "warning",
                3600
            );
        });
    }

    function hideFinancialPopupHost($host) {
        const sg = window.Zaaer && window.Zaaer.SaveGuard;
        if (sg) {
            sg.hidePopup($host);
            return;
        }
        try {
            if ($host && $host.length) {
                $host.dxPopup("instance").hide();
            }
        } catch {
            /* popup already disposed */
        }
    }

    /** Close popup immediately; refresh tabs/grids/financials in the background. */
    function completeFinancialPopupSuccess($host, syncWork) {
        const sg = window.Zaaer && window.Zaaer.SaveGuard;
        if (sg) {
            sg.closePopupThenRun($host, syncWork);
            return;
        }
        hideFinancialPopupHost($host);
        schedulePaymentUiBackground(syncWork);
    }

    function createFinancialSaveGuard() {
        const sg = window.Zaaer && window.Zaaer.SaveGuard;
        return sg ? sg.create() : { begin: () => true, end: () => {} };
    }

    const reservationSaveGuard = createFinancialSaveGuard();

    function affectsReservationRentBalance(receiptTypeOrVoucher) {
        const key = String(receiptTypeOrVoucher || "").toLowerCase();
        return key === "receipt" || key === "refund";
    }

    function paymentTabIdForMutation(kindOrReceiptType) {
        const key = String(kindOrReceiptType || "").toLowerCase();
        if (
            key === "disbursements" ||
            key === "refund" ||
            key === "security_deposit_refund" ||
            key === "expense"
        ) {
            return "disbursements";
        }
        if (key === "promissory" || key === "promissory_note" || key === "promissory_notes") {
            return "promissory";
        }
        if (key === "invoices" || key === "invoice") {
            return "invoices";
        }
        return "receipts";
    }

    function focusPaymentsTab(tabId) {
        const $host = $(".res-payment-tabpanel-host");
        if (!$host.length) {
            return Promise.resolve();
        }

        let inst = null;
        try {
            inst = $host.dxTabPanel("instance");
        } catch {
            return Promise.resolve();
        }

        if (!inst) {
            return Promise.resolve();
        }

        const items = getPaymentTabItemsWithCounts();
        const idx = items.findIndex((x) => x.id === tabId);
        if (idx < 0) {
            return Promise.resolve();
        }

        inst.option("selectedIndex", idx);
        const item = items[idx];
        ensurePaymentGridForTab(inst, item, idx);

        const $pane = $(inst.itemElements().eq(idx));
        const $grid = $pane.find(".res-payment-grid");
        if ($grid.length) {
            try {
                $grid.dxDataGrid("instance").refresh();
            } catch {
                refreshAllPaymentGrids();
            }
        } else {
            refreshAllPaymentGrids();
        }

        return Promise.resolve();
    }

    function afterPaymentReceiptMutation(receiptTypeOrVoucher, opts) {
        opts = opts || {};
        const tabId = paymentTabIdForMutation(receiptTypeOrVoucher);
        let chain = refreshPaymentTabCounts();
        if (!opts.quiet) {
            chain = chain.then(() => focusPaymentsTab(tabId));
        }
        return chain
            .then(() => refreshAllPaymentGridsAsync())
            .then(() => {
                if (affectsReservationRentBalance(receiptTypeOrVoucher)) {
                    return refreshReservationFinancialFromServer(
                        opts.reservationId,
                        opts.hotelId
                    ).then(() => {
                        if (!opts.quiet) {
                            syncFinancialUi({ skipFlash: true });
                        }
                    });
                }
            });
    }

    function afterPromissoryNoteMutation(opts) {
        opts = opts || {};
        let chain = refreshPaymentTabCounts();
        if (!opts.quiet) {
            chain = chain.then(() => focusPaymentsTab("promissory"));
        }
        return chain
            .then(() => refreshAllPaymentGridsAsync())
            .then(() => refreshReservationFinancialFromServer())
            .then(() => {
                syncFinancialUi({ skipFlash: true });
            });
    }

    function afterInvoiceMutation(opts) {
        opts = opts || {};
        resetPaymentCreditNotesProbe();
        let chain = refreshPaymentTabCounts();
        if (!opts.quiet) {
            chain = chain.then(() => focusPaymentsTab("invoices"));
        }
        return chain
            .then(() => probeCreditNotesTabOnDemand())
            .then(() => refreshAllPaymentGridsAsync())
            .then(() => refreshReservationFinancialFromServer())
            .then(() => {
                syncFinancialUi({ skipFlash: true });
            });
    }

    function isPromissoryRowCancelled(row) {
        const status = String((row && row.status) || "").toLowerCase();
        return status === "cancelled";
    }

    function isPromissoryRowCollectible(row) {
        if (!row || isPromissoryRowCancelled(row)) {
            return false;
        }

        const status = String(row.status || "").toLowerCase();
        if (status === "collected") {
            return false;
        }

        const due = Number(row.dueAmount != null ? row.dueAmount : row.amount) || 0;
        return due > 0 && !!row.zaaerId;
    }

    function paymentReceiptStatusLabel(status) {
        const key = status ? `reservationDetail.payments.status.${String(status).toLowerCase()}` : "";
        const localized = key ? t(key) : "";
        return localized && localized !== key ? localized : status || "";
    }

    function paymentReceiptStatusCellTemplate(container, options) {
        const status = String(options.value || "paid").toLowerCase();
        const $cell = $("<div>").addClass("res-payment-status-cell").appendTo(container);
        let badgeClass = "res-payment-status-badge";
        if (status === "paid" || status === "active") {
            badgeClass += " res-payment-status-badge--paid";
        } else if (status === "cancelled") {
            badgeClass += " res-payment-status-badge--cancelled";
        } else {
            badgeClass += " res-payment-status-badge--neutral";
        }
        $("<span>")
            .addClass(badgeClass)
            .text(paymentReceiptStatusLabel(status === "active" ? "paid" : status))
            .appendTo($cell);
    }

    function promissoryStatusCellTemplate(container, options) {
        const status = String(options.value || "open").toLowerCase();
        const $cell = $("<div>").addClass("res-payment-status-cell").appendTo(container);
        let badgeClass = "res-payment-status-badge";
        if (status === "collected") {
            badgeClass += " res-payment-status-badge--paid";
        } else if (status === "cancelled") {
            badgeClass += " res-payment-status-badge--cancelled";
        } else {
            badgeClass += " res-payment-status-badge--open";
        }
        $("<span>")
            .addClass(badgeClass)
            .text(paymentReceiptStatusLabel(status))
            .appendTo($cell);
    }

    function isPaymentRowCancelled(row) {
        const status = row && row.receiptStatus ? String(row.receiptStatus).toLowerCase() : "";
        return status === "cancelled";
    }

    function paymentMethodLabel(pm) {
        if (!pm) {
            return "";
        }
        if (isArabic() && pm.nameAr) {
            return pm.nameAr;
        }
        return pm.name || pm.nameAr || "";
    }

    function normalizePaymentMethodKey(name) {
        return String(name || "")
            .trim()
            .toLowerCase()
            .replace(/\s+/g, " ");
    }

    function mapPaymentMethodGridDisplay(name) {
        const raw = name == null ? "" : String(name);
        if (!raw) {
            return "";
        }
        if (!isArabic()) {
            return raw;
        }

        const key = normalizePaymentMethodKey(raw);
        const map = {
            cash: "نقدي",
            mada: "مدى",
            expedia: "اكسبيديا",
            "master card": "ماستر كارد",
            mastercard: "ماستر كارد",
            visa: "فيزا",
            agoda: "أجودا",
            "bank transfer": "تحويل بنكي",
            globaleit: "جلوبال ايت",
            wego: "ويجو",
            otherotas: "مواقع أخرى",
            webbeds: "ويب بيدز"
        };
        return map[key] || raw;
    }

    function mapVoucherCodeGridDisplay(code) {
        const raw = code == null ? "" : String(code).trim();
        if (!raw) {
            return "";
        }
        if (!isArabic()) {
            return raw;
        }

        const key = raw.toLowerCase();
        const map = {
            receipt: "سند قبض ايجار",
            service_receipt: "سند قبض خدمات",
            security_deposit: "سند قبض تأمين",
            refund: "سند صرف ايجار",
            security_deposit_refund: "سند صرف تأمين"
        };
        return map[key] || raw;
    }

    function bankLabel(bank) {
        if (!bank) {
            return "";
        }
        if (isArabic() && bank.nameAr) {
            return bank.nameAr;
        }
        return bank.name || bank.nameAr || "";
    }

    function resolveCashPaymentMethodId(paymentMethods) {
        const list = Array.isArray(paymentMethods) ? paymentMethods.slice() : [];
        if (!list.length) {
            return null;
        }

        list.sort((a, b) => {
            const ao = Number(a.sortOrder);
            const bo = Number(b.sortOrder);
            const aSort = Number.isFinite(ao) ? ao : 9999;
            const bSort = Number.isFinite(bo) ? bo : 9999;
            if (aSort !== bSort) {
                return aSort - bSort;
            }
            return paymentMethodLabel(a).localeCompare(paymentMethodLabel(b), isArabic() ? "ar" : "en");
        });

        const first = list[0];
        const code = String(first.code || first.methodCode || "").toLowerCase();
        const name = paymentMethodLabel(first).toLowerCase();
        if (code === "cash" || name.includes("cash") || name.includes("نقد")) {
            return first.id;
        }

        const cash = list.find((pm) => {
            const c = String(pm.code || pm.methodCode || "").toLowerCase();
            const n = paymentMethodLabel(pm).toLowerCase();
            return c === "cash" || n.includes("cash") || n.includes("نقد");
        });
        return cash ? cash.id : null;
    }

    function isCashPaymentMethod(paymentMethodId, cashMethodId, paymentMethods) {
        if (paymentMethodId == null) {
            return false;
        }

        const id = Number(paymentMethodId);
        if (cashMethodId != null && id === Number(cashMethodId)) {
            return true;
        }

        const pm = (paymentMethods || []).find((m) => Number(m.id) === id);
        if (!pm) {
            return false;
        }

        const code = String(pm.code || pm.methodCode || "").toLowerCase();
        const name = paymentMethodLabel(pm).toLowerCase();
        return code === "cash" || name.includes("cash") || name.includes("نقد");
    }

    function isNonCashPaymentMethod(paymentMethodId, cashMethodId, paymentMethods) {
        return paymentMethodId != null && !isCashPaymentMethod(paymentMethodId, cashMethodId, paymentMethods);
    }

    function formatLocalDateParam(value) {
        if (!value) {
            return null;
        }

        const d = value instanceof Date ? value : new Date(value);
        if (isNaN(d.getTime())) {
            return null;
        }

        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function toReceiptDateOnly(value) {
        if (!value) {
            return null;
        }

        const d = value instanceof Date ? new Date(value) : new Date(value);
        if (isNaN(d.getTime())) {
            return null;
        }

        return new Date(d.getFullYear(), d.getMonth(), d.getDate());
    }

    function formatReceiptDisplayDate(value) {
        if (!value) {
            return "";
        }

        const d = value instanceof Date ? value : new Date(value);
        if (isNaN(d.getTime())) {
            return "";
        }

        // Always Gregorian + Western digits (ar-SA alone may show Hijri on mobile).
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${day}/${m}/${y}`;
    }

    function defaultReceiptPeriodDates(ctx) {
        const detail = (ctx && ctx.detail) || {};
        const units = Array.isArray(detail.units) ? detail.units : [];
        let from = null;
        let to = null;

        if (units.length) {
            from = toReceiptDateOnly(units[0].checkInDate);
            to = toReceiptDateOnly(units[0].checkOutDate);
        }

        if (!from) {
            from = toReceiptDateOnly(getReservationCheckInCombined());
        }
        if (!to) {
            to = toReceiptDateOnly(getReservationCheckOutCombined());
        }

        const header = detail.header || {};
        if (!from) {
            from = toReceiptDateOnly(header.checkIn || header.arrivalDate || header.actualArrival);
        }
        if (!to) {
            to = toReceiptDateOnly(header.checkOut || header.departureDate);
        }

        return { from, to };
    }

    function paymentRowUiReceiptKind(row) {
        const vc = String((row && row.voucherCode) || "").toLowerCase();
        const rt = String((row && row.receiptType) || "").toLowerCase();
        if (vc === "security_deposit" || rt === "security_deposit") {
            return "security_deposit";
        }

        if (vc === "security_deposit_refund" || rt === "security_deposit_refund") {
            return "security_deposit_refund";
        }

        if (rt === "refund" || vc === "refund") {
            return "refund";
        }

        return "receipt";
    }

    /** Maps UI tab id to API/DB: receipt_type + voucher_code (see PmsPaymentReceiptService). */
    function mapReceiptStorageForApi(uiReceiptType) {
        switch (String(uiReceiptType || "").toLowerCase()) {
            case "security_deposit":
                return { receiptType: "receipt", voucherCode: "security_deposit" };
            case "security_deposit_refund":
                return { receiptType: "refund", voucherCode: "security_deposit_refund" };
            case "refund":
                return { receiptType: "refund", voucherCode: "refund" };
            default:
                return { receiptType: "receipt", voucherCode: "receipt" };
        }
    }

    function findLastRentReceipt(rows) {
        const list = (rows || []).filter((r) => {
            if (!r || isPaymentRowCancelled(r)) {
                return false;
            }

            const vc = String(r.voucherCode || "").toLowerCase();
            const rt = String(r.receiptType || "").toLowerCase();
            if (vc === "security_deposit" || rt === "security_deposit") {
                return false;
            }

            return rt === "receipt" && (vc === "receipt" || !vc);
        });
        list.sort(
            (a, b) =>
                new Date(b.receiptDate || b.date || 0) - new Date(a.receiptDate || a.date || 0)
        );
        return list[0] || null;
    }

    function refreshReservationFinancialFromServer(reservationIdOverride, hotelIdOverride) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const reservationId =
            reservationIdOverride != null && reservationIdOverride !== ""
                ? reservationIdOverride
                : pageCtx.detail && pageCtx.detail.reservationId;
        const hotelId =
            hotelIdOverride != null && hotelIdOverride !== ""
                ? hotelIdOverride
                : pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId);
        if (!svc || !reservationId || typeof svc.loadById !== "function") {
            return Promise.resolve();
        }

        return svc
            .loadById(reservationId, hotelId)
            .then((detail) => {
                if (detailPatchUsable(detail)) {
                    applyPostMutationReservationDetail(detail);
                }
                return detail;
            })
            .catch(() => {
                /* keep current UI if reload fails */
            });
    }

    function receiptFormItemOption(formInst, itemPath, option, value) {
        if (!formInst) {
            return;
        }
        const paths = Array.isArray(itemPath) ? itemPath : [itemPath];
        paths.forEach((path) => {
            try {
                formInst.itemOption(path, option, value);
            } catch {
                /* nested path may differ by form version */
            }
        });
    }

    const RENT_NOTE_PHRASE_KEYS = ["rent", "rentRenewal", "rentPartial", "rentBalance"];
    const RENT_NOTE_PHRASE_DETECT_ORDER = ["rentBalance", "rentPartial", "rentRenewal", "rent"];

    function rentNotePhraseLabel(key) {
        const phraseKey = key || "rent";
        return t("reservationDetail.payments.receipt.notesPhrase." + phraseKey);
    }

    function rentNotePhraseOptions() {
        return RENT_NOTE_PHRASE_KEYS.map((key) => ({
            key,
            label: rentNotePhraseLabel(key)
        }));
    }

    function translatePhraseKey(key, culture) {
        const i18nKey = "reservationDetail.payments.receipt.notesPhrase." + key;
        const dictionary = (window.ZaaerI18n && window.ZaaerI18n[culture]) || {};
        return dictionary[i18nKey] || i18nKey;
    }

    function detectRentNotePhraseKey(notes) {
        const text = String(notes || "");
        if (!text) {
            return "rent";
        }

        for (const key of RENT_NOTE_PHRASE_DETECT_ORDER) {
            const ar = translatePhraseKey(key, "ar");
            const en = translatePhraseKey(key, "en");
            if ((ar && text.includes(ar)) || (en && text.includes(en))) {
                return key;
            }
        }

        return "rent";
    }

    function buildRentReceiptNotes(formData, reservationNo) {
        const from = formatReceiptDisplayDate(formData && formData.receiptFrom);
        const to = formatReceiptDisplayDate(formData && formData.receiptTo);
        if (!from || !to) {
            return (formData && formData.notes) || "";
        }

        const phraseKey = (formData && formData.rentNotePhraseKey) || "rent";
        const phrase = rentNotePhraseLabel(phraseKey);
        return t("reservationDetail.payments.receipt.notesAuto")
            .replace("{phrase}", phrase)
            .replace("{from}", from)
            .replace("{to}", to)
            .replace("{number}", reservationNo || "");
    }

    function receiptRentDateChanged(formInst, formCtx) {
        if (!formInst || !formCtx || formCtx.receiptType !== "receipt") {
            return;
        }
        syncReceiptRentNotes(formInst, formCtx);
    }

    function receiptNotesTextAreaOptions(formCtx, hooks) {
        const extra = hooks && typeof hooks === "object" ? hooks : {};
        return {
            height: 88,
            minHeight: 72,
            maxLength: 500,
            rtlEnabled: isArabic(),
            autoResizeEnabled: true,
            onValueChanged(e) {
                const formInst = receiptFormFromEditor(e.element);
                if (!formInst || receiptFormIsSyncing(formInst) || formInst._notesEditorSyncing) {
                    return;
                }

                formInst._notesUserEdited = true;
                receiptFormBeginSync(formInst);
                try {
                    formInst.updateData("notes", e.value);
                } finally {
                    receiptFormEndSync(formInst);
                }

                if (typeof extra.onUserEdit === "function") {
                    extra.onUserEdit(formInst);
                }
            },
            onFocusIn(e) {
                const formInst = receiptFormFromEditor(e.element);
                if (formInst) {
                    formInst._notesUserEdited = true;
                }
            }
        };
    }

    function refreshReceiptNotesUi(formInst) {
        if (!formInst || typeof formInst._refreshReceiptNotesUi !== "function") {
            return;
        }

        formInst._refreshReceiptNotesUi();
    }

    function selectReceiptAmountInput(component) {
        if (!component || component.option("readOnly")) {
            return;
        }

        const selectInput = () => {
            let input = null;
            if (typeof component.field === "function") {
                input = component.field();
            }
            if (!input) {
                input = component.element().find("input.dx-texteditor-input").get(0);
            }
            if (!input || typeof input.select !== "function") {
                return;
            }

            input.focus();
            input.select();
        };

        selectInput();
        setTimeout(selectInput, 0);
        requestAnimationFrame(selectInput);
    }

    function receiptRentNotesFormItem(formCtx) {
        function mountPhraseSelect($host, formInst) {
            if (!$host || !$host.length || !formInst) {
                return;
            }

            try {
                if ($host.dxSelectBox("instance")) {
                    return;
                }
            } catch {
                /* not initialized yet */
            }

            const fd = formInst.option("formData") || {};
            const phraseKey = fd.rentNotePhraseKey || "rent";

            $host.dxSelectBox({
                dataSource: rentNotePhraseOptions(),
                valueExpr: "key",
                displayExpr: "label",
                value: phraseKey,
                width: isArabic() ? 156 : 172,
                rtlEnabled: isArabic(),
                openOnFieldClick: true,
                stylingMode: "filled",
                elementAttr: { class: "res-receipt-notes-phrase-select" },
                onValueChanged(e) {
                    if (receiptFormIsSyncing(formInst)) {
                        return;
                    }

                    receiptFormBeginSync(formInst);
                    try {
                        formInst.updateData("rentNotePhraseKey", e.value);
                    } finally {
                        receiptFormEndSync(formInst);
                    }

                    formInst._notesUserEdited = false;
                    syncReceiptRentNotes(formInst, formCtx);
                }
            });
        }

        return {
            colSpan: 2,
            cssClass: "res-receipt-notes-field res-receipt-notes-field--rent",
            label: { visible: false },
            template(data, itemElement) {
                const formInst = data && data.component;
                if (!formInst) {
                    return;
                }

                let $selectHost = null;

                const $panel = $("<div>").addClass("res-receipt-notes-composer").appendTo(itemElement);

                const $labelRow = $("<div>")
                    .addClass("res-receipt-notes-label-row")
                    .attr("dir", isArabic() ? "rtl" : "ltr")
                    .appendTo($panel);
                $("<span>")
                    .addClass("res-receipt-notes-label-text")
                    .text(t("reservationDetail.payments.receipt.notes"))
                    .appendTo($labelRow);
                $selectHost = $("<span>")
                    .addClass("res-receipt-notes-phrase-host")
                    .appendTo($labelRow);
                mountPhraseSelect($selectHost, formInst);

                const $textareaHost = $("<div>")
                    .addClass("res-receipt-notes-textarea-host")
                    .appendTo($panel);

                const fd = formInst.option("formData") || {};

                $textareaHost.dxTextArea(
                    Object.assign(
                        { value: fd.notes || "" },
                        receiptNotesTextAreaOptions(formCtx)
                    )
                );

                formInst._refreshReceiptNotesUi = function () {
                    const currentFd = formInst.option("formData") || {};
                    const nextPhrase = currentFd.rentNotePhraseKey || "rent";
                    const nextNotes = currentFd.notes || "";

                    if ($selectHost && $selectHost.length) {
                        try {
                            let phraseEditor = null;
                            try {
                                phraseEditor = $selectHost.dxSelectBox("instance");
                            } catch {
                                mountPhraseSelect($selectHost, formInst);
                                phraseEditor = $selectHost.dxSelectBox("instance");
                            }
                            if (phraseEditor && phraseEditor.option("value") !== nextPhrase) {
                                phraseEditor.option("value", nextPhrase);
                            }
                        } catch {
                            /* select not ready */
                        }
                    }

                    try {
                        const notesEditor = $textareaHost.dxTextArea("instance");
                        if (notesEditor && notesEditor.option("value") !== nextNotes) {
                            formInst._notesEditorSyncing = true;
                            try {
                                notesEditor.option("value", nextNotes);
                            } finally {
                                formInst._notesEditorSyncing = false;
                            }
                        }
                    } catch {
                        /* textarea not ready */
                    }
                };
            }
        };
    }

    function receiptFormBeginSync(formInst) {
        if (!formInst) {
            return;
        }
        formInst._receiptSyncDepth = (formInst._receiptSyncDepth || 0) + 1;
    }

    function receiptFormEndSync(formInst) {
        if (!formInst) {
            return;
        }
        formInst._receiptSyncDepth = Math.max(0, (formInst._receiptSyncDepth || 0) - 1);
    }

    function receiptFormIsSyncing(formInst) {
        return !!(formInst && formInst._receiptSyncDepth > 0);
    }

    function receiptFormFromEditor(editorElement) {
        try {
            return $(editorElement).closest(".dx-form").dxForm("instance");
        } catch {
            return null;
        }
    }

    function syncReceiptRentNotes(formInst, formCtx) {
        if (!formInst || !formCtx || formCtx.receiptType !== "receipt" || receiptFormIsSyncing(formInst)) {
            return;
        }

        if (formInst._notesUserEdited) {
            return;
        }

        const fd = formInst.option("formData") || {};
        const nextNotes = buildRentReceiptNotes(fd, formCtx.reservationNo);
        if (fd.notes === nextNotes) {
            return;
        }

        receiptFormBeginSync(formInst);
        try {
            formInst.updateData("notes", nextNotes);
        } finally {
            receiptFormEndSync(formInst);
        }

        refreshReceiptNotesUi(formInst);
    }

    function syncReceiptNonCashFields(formInst, paymentMethodId, formCtx, options) {
        if (!formInst || !formCtx || receiptFormIsSyncing(formInst)) {
            return;
        }

        const show = isNonCashPaymentMethod(
            paymentMethodId,
            formCtx.cashMethodId,
            formCtx.paymentMethods
        );
        const groupPaths = ["nonCashGroup", "paymentDetailsRow.nonCashGroup"];
        const bankPaths = [
            "bankId",
            "paymentDetailsRow.bankId",
            "nonCashGroup.bankId",
            "paymentDetailsRow.nonCashGroup.bankId"
        ];
        const txnPaths = [
            "transactionNo",
            "paymentDetailsRow.transactionNo",
            "nonCashGroup.transactionNo",
            "paymentDetailsRow.nonCashGroup.transactionNo"
        ];
        const pmPaths = ["paymentMethodId", "paymentDetailsRow.paymentMethodId"];

        groupPaths.forEach((path) => {
            receiptFormItemOption(formInst, path, "visible", show);
        });
        bankPaths.forEach((path) => {
            receiptFormItemOption(formInst, path, "visible", show);
        });
        txnPaths.forEach((path) => {
            receiptFormItemOption(formInst, path, "visible", show);
        });

        const nextColSpan = show ? 1 : isPaymentsMobileViewport() ? 1 : 3;
        receiptFormItemOption(formInst, pmPaths, "colSpan", nextColSpan);

        try {
            formInst.element().toggleClass("res-receipt-form--non-cash", show);
        } catch {
            /* optional */
        }

        const fd = formInst.option("formData") || {};
        const nextBankId = show ? fd.bankId || formCtx.defaultBankId || null : null;
        const nextTxn = show ? fd.transactionNo || "" : "";
        const bankChanged = Number(fd.bankId || 0) !== Number(nextBankId || 0);
        const txnChanged = String(fd.transactionNo || "") !== String(nextTxn || "");

        if (bankChanged || txnChanged) {
            receiptFormBeginSync(formInst);
            try {
                formInst.updateData({
                    bankId: nextBankId,
                    transactionNo: nextTxn
                });
            } finally {
                receiptFormEndSync(formInst);
            }
        }

        if (!options || options.repaint !== false) {
            try {
                formInst.repaint();
            } catch {
                /* optional */
            }
        }
    }

    function receiptDateFormItem(scope) {
        const showDate = canShowFinanceDocumentDate(scope || "receipt");
        return {
            dataField: "receiptDate",
            editorType: "dxDateBox",
            colSpan: 2,
            visible: showDate,
            label: { text: t("reservationDetail.payments.receipt.receiptDate") },
            isRequired: showDate,
            editorOptions: {
                type: "date",
                displayFormat: "yyyy-MM-dd",
                openOnFieldClick: true
            }
        };
    }

    function receiptAmountFormItem(formCtx) {
        const lockAmount = !!(formCtx && formCtx.lockReceiptAmount);
        const lockedValue =
            formCtx && formCtx.lockedAmount != null ? roundMoney(formCtx.lockedAmount) : null;
        const depositMax =
            formCtx && formCtx.depositRefundMaxAmount != null
                ? roundMoney(formCtx.depositRefundMaxAmount)
                : null;

        return {
            itemType: "simple",
            colSpan: 2,
            cssClass: "res-receipt-amount-row-item",
            label: { text: t("reservationDetail.payments.receipt.amount") },
            isRequired: true,
            validationRules: [
                {
                    type: "custom",
                    message: t("reservationDetail.payments.receipt.validationAmount"),
                    validationCallback() {
                        const $form = $(".res-receipt-form:visible").closest(".dx-form");
                        let formInst = null;
                        try {
                            formInst = $form.dxForm("instance");
                        } catch {
                            return false;
                        }
                        const fd = (formInst && formInst.option("formData")) || {};
                        return Number(fd.amountPaid) > 0;
                    }
                },
                {
                    type: "custom",
                    message: t("reservationDetail.payments.disbursement.validationDepositMax").replace(
                        "{max}",
                        depositMax != null ? formatMoneyEn(depositMax) : "0.00"
                    ),
                    validationCallback() {
                        if (depositMax == null) {
                            return true;
                        }

                        const $form = $(".res-receipt-form:visible").closest(".dx-form");
                        let formInst = null;
                        try {
                            formInst = $form.dxForm("instance");
                        } catch {
                            return true;
                        }

                        const fd = (formInst && formInst.option("formData")) || {};
                        const amt = Number(fd.amountPaid) || 0;
                        return amt <= depositMax + 0.001;
                    }
                }
            ],
            template(_data, itemElement) {
                const $form = $(itemElement).closest(".dx-form");
                let formInst = null;
                try {
                    formInst = $form.dxForm("instance");
                } catch {
                    return;
                }

                const fd = formInst.option("formData") || {};
                const amountValue =
                    lockedValue != null ? lockedValue : Number(fd.amountPaid) || 0;
                const $row = $("<div>").addClass("res-receipt-amount-inline").appendTo(itemElement);
                const $inputWrap = $("<div>").addClass("res-receipt-amount-input-wrap").appendTo($row);

                $inputWrap.dxNumberBox({
                    value: amountValue,
                    format: "#,##0.00",
                    min: 0,
                    max: depositMax != null && !lockAmount ? depositMax : undefined,
                    readOnly: lockAmount,
                    showSpinButtons: !lockAmount,
                    rtlEnabled: isArabic(),
                    elementAttr: { class: "res-receipt-amount-numberbox" },
                    onFocusIn(e) {
                        selectReceiptAmountInput(e.component);
                    },
                    onClick(e) {
                        selectReceiptAmountInput(e.component);
                    },
                    onValueChanged(e) {
                        if (lockAmount || receiptFormIsSyncing(formInst)) {
                            return;
                        }
                        receiptFormBeginSync(formInst);
                        try {
                            formInst.updateData("amountPaid", e.value);
                        } finally {
                            receiptFormEndSync(formInst);
                        }
                    }
                });

                if (lockAmount && lockedValue != null) {
                    receiptFormBeginSync(formInst);
                    try {
                        formInst.updateData("amountPaid", lockedValue);
                    } finally {
                        receiptFormEndSync(formInst);
                    }
                }

                $("<img>")
                    .attr("src", "/logo/sar-symbol.svg")
                    .attr("alt", "SAR")
                    .addClass("res-receipt-sar-icon")
                    .appendTo($row);

                if (depositMax != null) {
                    const hintKey =
                        depositMax > 0
                            ? "reservationDetail.payments.disbursement.depositMaxHint"
                            : "reservationDetail.payments.disbursement.depositNoBalance";
                    $("<div>")
                        .addClass("res-deposit-refund-hint")
                        .text(t(hintKey).replace("{amount}", formatMoneyEn(depositMax)))
                        .appendTo(itemElement);
                }
            }
        };
    }

    function receiptSharedFormTail(methods, bankList, formCtx) {
        const mobile = isPaymentsMobileViewport();
        const paymentRowColCount = mobile ? 1 : 3;

        return [
            {
                itemType: "group",
                name: "paymentDetailsRow",
                colSpan: 2,
                colCount: paymentRowColCount,
                cssClass: "res-receipt-payment-details-row",
                items: [
                    {
                        dataField: "paymentMethodId",
                        editorType: "dxSelectBox",
                        colSpan: mobile ? 1 : 3,
                        label: { text: t("reservationDetail.payments.receipt.paymentMethod") },
                        isRequired: true,
                        validationRules: [
                            {
                                type: "custom",
                                message: t(
                                    "reservationDetail.payments.receipt.validationPaymentMethod"
                                ),
                                validationCallback(e) {
                                    return e.value != null && Number(e.value) > 0;
                                }
                            }
                        ],
                        editorOptions: {
                            dataSource: methods,
                            valueExpr: "id",
                            displayExpr: paymentMethodLabel,
                            searchEnabled: true,
                            placeholder: t(
                                "reservationDetail.payments.receipt.paymentMethodPlaceholder"
                            ),
                            rtlEnabled: isArabic(),
                            onValueChanged(e) {
                                const formInst = receiptFormFromEditor(e.element);
                                if (formInst && formCtx) {
                                    syncReceiptNonCashFields(formInst, e.value, formCtx);
                                }
                            }
                        }
                    },
                    {
                        itemType: "group",
                        name: "nonCashGroup",
                        colSpan: mobile ? 1 : 2,
                        colCount: mobile ? 1 : 2,
                        visible: false,
                        cssClass: "res-receipt-non-cash-group",
                        items: [
                            {
                                dataField: "bankId",
                                editorType: "dxSelectBox",
                                label: { text: t("reservationDetail.payments.receipt.bank") },
                                editorOptions: {
                                    dataSource: bankList,
                                    valueExpr: "id",
                                    displayExpr: bankLabel,
                                    searchEnabled: true,
                                    placeholder: t(
                                        "reservationDetail.payments.receipt.bankPlaceholder"
                                    ),
                                    rtlEnabled: isArabic()
                                }
                            },
                            {
                                dataField: "transactionNo",
                                label: {
                                    text: t("reservationDetail.payments.receipt.transactionNo")
                                },
                                editorOptions: {
                                    maxLength: 100
                                }
                            }
                        ]
                    }
                ]
            },
            formCtx && formCtx.receiptType === "receipt"
                ? receiptRentNotesFormItem(formCtx)
                : {
                      dataField: "notes",
                      editorType: "dxTextArea",
                      colSpan: 2,
                      cssClass: "res-receipt-notes-field",
                      label: { text: t("reservationDetail.payments.receipt.notes") },
                      editorOptions: receiptNotesTextAreaOptions(formCtx)
                  }
        ];
    }

    function paymentReceiptRentFormItems(paymentMethods, banks, formCtx) {
        const methods = paymentMethods || [];
        const bankList = banks || [];
        const last = formCtx && formCtx.lastRentReceipt;
        const lastFrom = last ? formatReceiptDisplayDate(last.receiptFrom) : "";
        const lastTo = last ? formatReceiptDisplayDate(last.receiptTo) : "";
        const showLastHint = !!(last && lastFrom && lastTo);

        const items = [];
        if (showLastHint) {
            items.push({
                itemType: "simple",
                colSpan: 2,
                template() {
                    const receiptNo = last.receiptNo || last.number || "";
                    const text = t("reservationDetail.payments.receipt.lastRentHint")
                        .replace("{from}", lastFrom)
                        .replace("{to}", lastTo)
                        .replace("{number}", receiptNo);
                    return $("<div>").addClass("res-receipt-last-hint").text(text);
                }
            });
        }

        if (canUseBuildingGuardRent()) {
            const guardMobile = isPaymentsMobileViewport();
            if (!guardMobile) {
                items.push({ itemType: "empty", colSpan: 1 });
            }

            items.push({
                dataField: "isBuildingGuardRent",
                editorType: "dxCheckBox",
                colSpan: guardMobile ? 2 : 1,
                cssClass: "res-receipt-guard-rent-row",
                label: { visible: false },
                editorOptions: {
                    text: t("reservationDetail.payments.receipt.buildingGuardRent"),
                    elementAttr: { class: "res-receipt-guard-rent-checkbox" }
                }
            });
        }

        items.push(
            receiptDateFormItem("receipt"),
            {
                dataField: "receiptFrom",
                editorType: "dxDateBox",
                colSpan: 1,
                label: { text: t("reservationDetail.payments.receipt.receiptFrom") },
                isRequired: true,
                editorOptions: {
                    type: "date",
                    displayFormat: "yyyy-MM-dd",
                    openOnFieldClick: true,
                    onValueChanged(e) {
                        const formInst = receiptFormFromEditor(e.element);
                        receiptRentDateChanged(formInst, formCtx);
                    }
                }
            },
            {
                dataField: "receiptTo",
                editorType: "dxDateBox",
                colSpan: 1,
                label: { text: t("reservationDetail.payments.receipt.receiptTo") },
                isRequired: true,
                editorOptions: {
                    type: "date",
                    displayFormat: "yyyy-MM-dd",
                    openOnFieldClick: true,
                    onValueChanged(e) {
                        const formInst = receiptFormFromEditor(e.element);
                        receiptRentDateChanged(formInst, formCtx);
                    }
                }
            },
            receiptAmountFormItem(formCtx)
        );
        return items.concat(receiptSharedFormTail(methods, bankList, formCtx));
    }

    function paymentReceiptDepositFormItems(paymentMethods, banks, formCtx) {
        const methods = paymentMethods || [];
        const bankList = banks || [];
        return [receiptDateFormItem("receipt"), receiptAmountFormItem(formCtx)].concat(
            receiptSharedFormTail(methods, bankList, formCtx)
        );
    }

    function disbursementReasonFormItem() {
        return {
            dataField: "reason",
            editorType: "dxTextArea",
            colSpan: 2,
            label: { text: t("reservationDetail.payments.disbursement.reason") },
            isRequired: true,
            validationRules: [
                {
                    type: "custom",
                    message: t("reservationDetail.payments.disbursement.validationReason"),
                    validationCallback(e) {
                        return e.value && String(e.value).trim().length > 0;
                    }
                }
            ],
            editorOptions: {
                height: 56,
                maxLength: 500
            }
        };
    }

    function paymentDisbursementFormItems(paymentMethods, banks, formCtx) {
        const methods = paymentMethods || [];
        const bankList = banks || [];
        return [
            receiptDateFormItem("disbursement"),
            receiptAmountFormItem(formCtx),
            receiptSharedFormTail(methods, bankList, formCtx)[0],
            disbursementReasonFormItem()
        ];
    }

    function buildDisbursementInitialForm(receiptType, reservationNo) {
        const isDeposit = receiptType === "security_deposit_refund";
        const reasonKey = isDeposit
            ? "reservationDetail.payments.disbursement.reasonDeposit"
            : "reservationDetail.payments.disbursement.reasonRent";
        return {
            receiptDate: defaultReceiptDateValue(),
            amountPaid: 0,
            paymentMethodId: null,
            bankId: null,
            transactionNo: "",
            reason: t(reasonKey).replace("{number}", reservationNo || ""),
            notes: ""
        };
    }

    function defaultReceiptDateValue() {
        const now = new Date();
        return new Date(now.getFullYear(), now.getMonth(), now.getDate());
    }

    function paymentReceiptRowToForm(row) {
        const r = row || {};
        const base = {
            receiptDate: r.receiptDate ? new Date(r.receiptDate) : defaultReceiptDateValue(),
            amountPaid: Number(r.amountPaid ?? r.amount) || 0,
            paymentMethodId: r.paymentMethodId,
            bankId: r.bankId || null,
            transactionNo: r.transactionNo || "",
            notes: r.notes || ""
        };

        if (String(r.receiptType).toLowerCase() === "receipt") {
            base.receiptFrom = r.receiptFrom ? new Date(r.receiptFrom) : null;
            base.receiptTo = r.receiptTo ? new Date(r.receiptTo) : null;
            base.isBuildingGuardRent = !!(
                r.isBuildingGuardRent ?? r.IsBuildingGuardRent
            );
            base.rentNotePhraseKey = detectRentNotePhraseKey(base.notes);
        }

        return base;
    }

    function paymentDisbursementRowToForm(row) {
        const r = row || {};
        const rawAmount = Number(r.amountPaid ?? r.amount) || 0;
        return {
            receiptDate: r.receiptDate ? new Date(r.receiptDate) : defaultReceiptDateValue(),
            amountPaid: Math.abs(rawAmount),
            paymentMethodId: r.paymentMethodId,
            bankId: r.bankId || null,
            transactionNo: r.transactionNo || "",
            reason: r.reason || "",
            notes: r.notes || ""
        };
    }

    function buildPaymentReceiptInitialForm(receiptType, reservationNo, formCtx) {
        const base = {
            receiptDate: defaultReceiptDateValue(),
            amountPaid: 0,
            paymentMethodId: formCtx.cashMethodId,
            bankId: null,
            transactionNo: "",
            notes: ""
        };

        if (receiptType !== "receipt") {
            return base;
        }

        const period = defaultReceiptPeriodDates(formCtx);
        base.receiptFrom = period.from;
        base.receiptTo = period.to;
        base.isBuildingGuardRent = false;
        base.rentNotePhraseKey = "rent";
        base.notes = buildRentReceiptNotes(base, reservationNo);
        return base;
    }

    function wirePaymentReceiptForm(formInst, formCtx, options) {
        if (!formInst) {
            return;
        }

        const opts = options && typeof options === "object" ? options : {};
        formInst._receiptSyncDepth = 0;
        formInst._notesUserEdited = !!opts.preserveNotes;
        const fd = formInst.option("formData") || {};
        syncReceiptNonCashFields(formInst, fd.paymentMethodId, formCtx, { repaint: false });
        if (formCtx && formCtx.receiptType === "receipt") {
            syncReceiptRentNotes(formInst, formCtx);
            refreshReceiptNotesUi(formInst);
            setTimeout(() => refreshReceiptNotesUi(formInst), 0);
        }
    }

    function openPaymentReceiptPopup(options) {
        options = options || {};
        const editRow = options.editRow || null;
        const isEdit = !!(editRow && editRow.zaaerId);
        let isViewOnly = !!options.readOnly;

        if (isEdit) {
            if (!isViewOnly && !canEditPaymentReceiptVoucher()) {
                if (canViewPaymentReceiptVoucher()) {
                    isViewOnly = true;
                } else {
                    DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                    return;
                }
            } else if (isViewOnly && !canViewPaymentReceiptVoucher() && !canEditPaymentReceiptVoucher()) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
        } else {
            if (!hasPmsPermission("payments.create")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
            if (!ensureReservationCompleteForOperations()) {
                return;
            }
        }

        const ctx = paymentReceiptContext(options.externalContext);
        const fromExternal = !!(isEdit && options.externalContext && ctx.hotelId);
        if (!ctx.hotelId) {
            DevExpress.ui.notify(
                isEdit
                    ? t("reservationDetail.payments.receipt.updateFailed")
                    : t("reservationDetail.payments.receipt.createFailed"),
                "error",
                3200
            );
            return;
        }
        if (!fromExternal && !ctx.reservationRouteId && !ctx.reservationId) {
            DevExpress.ui.notify(
                isEdit
                    ? t("reservationDetail.payments.receipt.updateFailed")
                    : t("reservationDetail.payments.receipt.createFailed"),
                "error",
                3200
            );
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const loadAll = Promise.all([
            svc && typeof svc.loadPaymentMethods === "function"
                ? svc.loadPaymentMethods()
                : Promise.resolve([]),
            svc && typeof svc.loadBanks === "function" ? svc.loadBanks() : Promise.resolve([]),
            !isEdit && svc && typeof svc.loadLastRentReceipt === "function"
                ? svc.loadLastRentReceipt(ctx.reservationRouteId || ctx.reservationId)
                : Promise.resolve(null)
        ]);

        loadAll.then(([paymentMethods, banks, lastRentReceipt]) => {
            function paymentMutationOpts() {
                const mutationOpts = { quiet: !!options.externalContext };
                if (options.externalContext) {
                    mutationOpts.reservationId =
                        options.externalContext.reservationRouteId
                        || options.externalContext.reservationId;
                    mutationOpts.hotelId = options.externalContext.hotelId;
                }
                return mutationOpts;
            }

            function receiptEditAfterSave(updated) {
                let chain = afterPaymentReceiptMutation(activeReceiptType, paymentMutationOpts());
                if (typeof options.afterSave === "function") {
                    chain = chain.then(() => Promise.resolve(options.afterSave(updated)));
                }
                return chain;
            }
            const checkoutFlow = options.checkoutFlow || null;
            const promissoryCollection = options.promissoryCollection || null;
            const methods = Array.isArray(paymentMethods) ? paymentMethods : [];
            const bankList = Array.isArray(banks) ? banks : [];
            const cashMethodId = resolveCashPaymentMethodId(methods);
            const defaultBank = bankList.find((b) => b && b.isDefault) || bankList[0] || null;
            const formCtx = {
                detail: ctx.detail,
                reservationNo: ctx.reservationNo,
                receiptType: "receipt",
                cashMethodId,
                paymentMethods: methods,
                defaultBankId: defaultBank ? defaultBank.id : null,
                lastRentReceipt: lastRentReceipt
            };

            if (checkoutFlow) {
                formCtx.lockReceiptAmount = true;
                formCtx.lockedAmount = roundMoney(checkoutFlow.amount);
            }

            if (promissoryCollection) {
                formCtx.lockReceiptAmount = true;
                formCtx.lockedAmount = roundMoney(promissoryCollection.dueAmount);
                formCtx.promissoryNoteZaaerId = promissoryCollection.zaaerId;
                formCtx.promissoryNo = promissoryCollection.promissoryNo || "";
            }

            const $host = $("<div>").appendTo("body");
            const formInstances = { rent: null, deposit: null, edit: null };
            let activeReceiptType = isEdit
                ? paymentRowUiReceiptKind(editRow)
                : checkoutFlow
                  ? checkoutFlow.receiptType || "receipt"
                  : "receipt";

            const receiptSaveGuard = createFinancialSaveGuard();

            function finishReceiptPopupMutation(receiptType) {
                const chain = checkoutFlow
                    ? afterPaymentReceiptMutation(receiptType, { quiet: true }).then(() => {
                          if (typeof checkoutFlow.onSettled === "function") {
                              return checkoutFlow.onSettled();
                          }
                      })
                    : promissoryCollection
                      ? afterPromissoryNoteMutation({ quiet: true })
                      : afterPaymentReceiptMutation(receiptType);
                completeFinancialPopupSuccess($host, chain);
                return true;
            }

            function getActiveFormInstance() {
                if (isEdit) {
                    return formInstances.edit;
                }
                return activeReceiptType === "security_deposit" ? formInstances.deposit : formInstances.rent;
            }

            function buildReceiptPayload(fd, uiReceiptType) {
                const nonCash = isNonCashPaymentMethod(
                    fd.paymentMethodId,
                    formCtx.cashMethodId,
                    formCtx.paymentMethods
                );
                const storage = mapReceiptStorageForApi(uiReceiptType);

                const payload = {
                    hotelId: ctx.hotelId,
                    reservationId: ctx.reservationRouteId || ctx.reservationId,
                    customerId: ctx.customerId,
                    receiptType: storage.receiptType,
                    voucherCode: storage.voucherCode,
                    amountPaid: Number(fd.amountPaid) || 0,
                    receiptDate: formatLocalDateParam(fd.receiptDate || defaultReceiptDateValue()),
                    paymentMethodId: fd.paymentMethodId,
                    bankId: nonCash ? fd.bankId : null,
                    transactionNo: nonCash ? fd.transactionNo || null : null,
                    notes: fd.notes || null
                };

                if (storage.voucherCode === "receipt") {
                    payload.receiptFrom = formatLocalDateParam(fd.receiptFrom);
                    payload.receiptTo = formatLocalDateParam(fd.receiptTo);
                    if (canUseBuildingGuardRent()) {
                        payload.isBuildingGuardRent = !!fd.isBuildingGuardRent;
                    }
                    const rebuiltNotes = buildRentReceiptNotes(fd, ctx.reservationNo);
                    if (rebuiltNotes) {
                        payload.notes = rebuiltNotes;
                    }
                }

                if (formCtx.promissoryNoteZaaerId) {
                    payload.promissoryNoteZaaerId = formCtx.promissoryNoteZaaerId;
                    if (!payload.receiptFrom || !payload.receiptTo) {
                        payload.receiptFrom = formatLocalDateParam(fd.receiptFrom);
                        payload.receiptTo = formatLocalDateParam(fd.receiptTo);
                    }
                    if (!String(payload.notes || "").trim() && formCtx.promissoryNo) {
                        payload.notes = formCtx.promissoryNo;
                    }
                }

                return { payload, nonCash };
            }

            function submitActiveReceipt() {
                if (!receiptSaveGuard.begin()) {
                    return Promise.resolve(false);
                }

                const formInst = getActiveFormInstance();
                if (!formInst) {
                    receiptSaveGuard.end();
                    return Promise.resolve(false);
                }

                const validation = formInst.validate();
                if (!validation || !validation.isValid) {
                    receiptSaveGuard.end();
                    return Promise.resolve(false);
                }

                const fd = formInst.option("formData") || {};
                const receiptType = activeReceiptType;
                const built = buildReceiptPayload(fd, receiptType);
                const payload = built.payload;
                const nonCash = built.nonCash;

                const storage = mapReceiptStorageForApi(receiptType);
                if (
                    storage.voucherCode === "receipt" &&
                    (!fd.receiptFrom || !fd.receiptTo)
                ) {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.receipt.validationPeriod"),
                        "warning",
                        2800
                    );
                    receiptSaveGuard.end();
                    return Promise.resolve(false);
                }

                if (nonCash && (!fd.bankId || Number(fd.bankId) <= 0)) {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.receipt.validationBank"),
                        "warning",
                        2800
                    );
                    receiptSaveGuard.end();
                    return Promise.resolve(false);
                }

                if (isEdit) {
                    if (!svc || typeof svc.updatePaymentReceipt !== "function") {
                        DevExpress.ui.notify(
                            t("reservationDetail.payments.receipt.updateFailed"),
                            "error",
                            3200
                        );
                        receiptSaveGuard.end();
                        return Promise.resolve(false);
                    }

                    return svc
                        .updatePaymentReceipt(editRow.zaaerId, payload)
                        .then((updated) => {
                            const number =
                                (updated && (updated.receiptNo || updated.ReceiptNo)) ||
                                editRow.receiptNo ||
                                "";
                            DevExpress.ui.notify(
                                t("reservationDetail.payments.receipt.updateSuccess").replace(
                                    "{number}",
                                    number
                                ),
                                "success",
                                3200
                            );
                            completeFinancialPopupSuccess($host, receiptEditAfterSave(updated));
                            return true;
                        })
                        .catch((err) => {
                            DevExpress.ui.notify(
                                (err && err.message) ||
                                    t("reservationDetail.payments.receipt.updateFailed"),
                                "error",
                                3600
                            );
                            return false;
                        })
                        .finally(() => {
                            receiptSaveGuard.end();
                        });
                }

                if (!svc || typeof svc.createPaymentReceipt !== "function") {
                    DevExpress.ui.notify(t("reservationDetail.payments.receipt.createFailed"), "error", 3200);
                    receiptSaveGuard.end();
                    return Promise.resolve(false);
                }

                return svc
                    .createPaymentReceipt(payload)
                    .then((created) => {
                        const number =
                            (created && (created.receiptNo || created.ReceiptNo)) || "";
                        DevExpress.ui.notify(
                            t("reservationDetail.payments.receipt.createSuccess").replace("{number}", number),
                            "success",
                            3200
                        );
                        return finishReceiptPopupMutation(receiptType);
                    })
                    .catch((err) => {
                        DevExpress.ui.notify(
                            (err && err.message) || t("reservationDetail.payments.receipt.createFailed"),
                            "error",
                            3600
                        );
                        return false;
                    })
                    .finally(() => {
                        receiptSaveGuard.end();
                    });
            }

            function mountReceiptForm($container, receiptType, initialFormData) {
                const tabFormCtx = Object.assign({}, formCtx, { receiptType: receiptType });
                const formItems =
                    receiptType === "security_deposit"
                        ? paymentReceiptDepositFormItems(methods, bankList, tabFormCtx)
                        : paymentReceiptRentFormItems(methods, bankList, tabFormCtx);

                try {
                    const existingForm = $container.dxForm("instance");
                    if (existingForm) {
                        existingForm.dispose();
                    }
                } catch {
                    /* container has no form yet */
                }
                $container.empty();

                $container.dxForm({
                    formData: initialFormData,
                    colCount: isPaymentsMobileViewport() ? 1 : 2,
                    labelLocation: "top",
                    readOnly: isViewOnly,
                    items: formItems,
                    onInitialized(e) {
                        const formInst = e.component;
                        wirePaymentReceiptForm(formInst, tabFormCtx, { preserveNotes: isEdit });
                        if (isEdit) {
                            formInstances.edit = formInst;
                        } else if (receiptType === "security_deposit") {
                            formInstances.deposit = formInst;
                        } else {
                            formInstances.rent = formInst;
                        }
                    }
                });
            }

            $host.dxPopup({
                title:
                    checkoutFlow && checkoutFlow.titleKey
                        ? t(checkoutFlow.titleKey)
                        : isEdit
                          ? isViewOnly
                              ? t("reservationDetail.payments.receipt.popupTitleView")
                              : t("reservationDetail.payments.receipt.popupTitleEdit")
                          : t("reservationDetail.payments.receipt.popupTitle"),
                visible: true,
                width: paymentReceiptPopupWidth(),
                height: "auto",
                maxHeight: "78vh",
                showCloseButton: true,
                hideOnOutsideClick: false,
                dragEnabled: false,
                rtlEnabled: isArabic(),
                wrapperAttr: {
                    class: checkoutFlow
                        ? "res-receipt-popup res-action-popup checkout-payment-popup"
                        : "res-receipt-popup res-action-popup"
                },
                contentTemplate(contentElem) {
                    const $content = $(contentElem).empty().addClass("res-receipt-popup-body");

                    if (isEdit) {
                        const receiptType = activeReceiptType;
                        const $formHost = $("<div>")
                            .addClass("res-action-form res-receipt-form")
                            .appendTo($content);
                        const initialData = paymentReceiptRowToForm(editRow);
                        if (receiptType === "receipt") {
                            initialData.notes =
                                initialData.notes ||
                                buildRentReceiptNotes(initialData, ctx.reservationNo);
                        }
                        mountReceiptForm($formHost, receiptType, initialData);
                        return;
                    }

                    if (checkoutFlow) {
                        const receiptType = checkoutFlow.receiptType || "receipt";
                        activeReceiptType = receiptType;
                        const tabFormCtx = Object.assign({}, formCtx, { receiptType: receiptType });
                        const initial = buildPaymentReceiptInitialForm(
                            receiptType,
                            ctx.reservationNo,
                            tabFormCtx
                        );
                        initial.amountPaid = formCtx.lockedAmount;
                        const $formHost = $("<div>")
                            .addClass("res-action-form res-receipt-form")
                            .appendTo($content);
                        mountReceiptForm($formHost, receiptType, initial);
                        return;
                    }

                    if (promissoryCollection) {
                        activeReceiptType = "receipt";
                        const tabFormCtx = Object.assign({}, formCtx, { receiptType: "receipt" });
                        const initial = buildPaymentReceiptInitialForm(
                            "receipt",
                            ctx.reservationNo,
                            tabFormCtx
                        );
                        initial.amountPaid = formCtx.lockedAmount;
                        const $formHost = $("<div>")
                            .addClass("res-action-form res-receipt-form")
                            .appendTo($content);
                        mountReceiptForm($formHost, "receipt", initial);
                        return;
                    }

                    function createReceiptTab(receiptType, text) {
                        return {
                            id: receiptType,
                            text: text,
                            receiptType: receiptType,
                            template() {
                                const $pane = $("<div>").empty();
                                const $form = $("<div>")
                                    .addClass("res-action-form res-receipt-form")
                                    .appendTo($pane);
                                const tabFormCtx = Object.assign({}, formCtx, {
                                    receiptType: receiptType
                                });

                                mountReceiptForm(
                                    $form,
                                    receiptType,
                                    buildPaymentReceiptInitialForm(
                                        receiptType,
                                        ctx.reservationNo,
                                        tabFormCtx
                                    )
                                );

                                return $pane;
                            }
                        };
                    }

                    const tabs = [
                        createReceiptTab(
                            "receipt",
                            t("reservationDetail.payments.receipt.tabRent")
                        ),
                        createReceiptTab(
                            "security_deposit",
                            t("reservationDetail.payments.receipt.tabDeposit")
                        )
                    ];

                    $("<div>")
                        .appendTo($content)
                        .dxTabPanel({
                            items: tabs,
                            keyExpr: "id",
                            selectedIndex: 0,
                            deferRendering: true,
                            animationEnabled: false,
                            stylingMode: "secondary",
                            iconPosition: "top",
                            elementAttr: { class: "res-receipt-type-tabs" },
                            itemTitleTemplate(item, _index, element) {
                                $(element).text(item.text || "");
                            },
                            onSelectionChanged(e) {
                                const item = e.addedItems && e.addedItems[0];
                                activeReceiptType = (item && item.receiptType) || "receipt";
                            }
                        });
                },
                toolbarItems: [
                    {
                        widget: "dxButton",
                        toolbar: "bottom",
                        location: "after",
                        options: {
                            text: isViewOnly
                                ? t("reservationDetail.payments.receipt.close")
                                : t("reservationDetail.payments.receipt.cancel"),
                            icon: "close",
                            stylingMode: "outlined",
                            onClick() {
                                $host.dxPopup("instance").hide();
                            }
                        }
                    },
                    ...(isViewOnly
                        ? []
                        : [
                              {
                                  widget: "dxButton",
                                  toolbar: "bottom",
                                  location: "after",
                                  options: {
                                      text: isEdit
                                          ? t("reservationDetail.payments.receipt.update")
                                          : t("reservationDetail.payments.receipt.create"),
                                      icon: "check",
                                      type: "default",
                                      stylingMode: "contained",
                                      onClick() {
                                          submitActiveReceipt();
                                      }
                                  }
                              }
                          ])
                ],
                onHidden() {
                    $host.remove();
                }
            });
        });
    }

    function openPaymentDisbursementPopup(options) {
        options = options || {};
        const editRow = options.editRow || null;
        const isEdit = !!(editRow && editRow.zaaerId);
        let isViewOnly = !!options.readOnly;

        if (isEdit) {
            if (!isViewOnly && !canEditPaymentRefundVoucher()) {
                if (canViewPaymentRefundVoucher()) {
                    isViewOnly = true;
                } else {
                    DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                    return;
                }
            } else if (isViewOnly && !canViewPaymentRefundVoucher() && !canEditPaymentRefundVoucher()) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
        } else {
            if (!hasPmsPermission("payments.refund")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
            if (!ensureReservationCompleteForOperations()) {
                return;
            }
        }

        const ctx = paymentReceiptContext(options.externalContext);
        const fromExternal = !!(isEdit && options.externalContext && ctx.hotelId);
        if (!ctx.hotelId) {
            DevExpress.ui.notify(
                isEdit
                    ? t("reservationDetail.payments.disbursement.updateFailed")
                    : t("reservationDetail.payments.disbursement.createFailed"),
                "error",
                3200
            );
            return;
        }
        if (!fromExternal && !ctx.reservationRouteId && !ctx.reservationId) {
            DevExpress.ui.notify(
                isEdit
                    ? t("reservationDetail.payments.disbursement.updateFailed")
                    : t("reservationDetail.payments.disbursement.createFailed"),
                "error",
                3200
            );
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const loadAll = Promise.all([
            svc && typeof svc.loadPaymentMethods === "function"
                ? svc.loadPaymentMethods()
                : Promise.resolve([]),
            svc && typeof svc.loadBanks === "function" ? svc.loadBanks() : Promise.resolve([])
        ]);

        loadAll.then(([paymentMethods, banks]) => {
            function paymentMutationOpts() {
                const mutationOpts = { quiet: !!options.externalContext };
                if (options.externalContext) {
                    mutationOpts.reservationId =
                        options.externalContext.reservationRouteId
                        || options.externalContext.reservationId;
                    mutationOpts.hotelId = options.externalContext.hotelId;
                }
                return mutationOpts;
            }

            function disbursementEditAfterSave(updated) {
                let chain = afterPaymentReceiptMutation(activeReceiptType, paymentMutationOpts());
                if (typeof options.afterSave === "function") {
                    chain = chain.then(() => Promise.resolve(options.afterSave(updated)));
                }
                return chain;
            }

            const checkoutFlow = options.checkoutFlow || null;
            const methods = Array.isArray(paymentMethods) ? paymentMethods : [];
            const bankList = Array.isArray(banks) ? banks : [];
            const cashMethodId = resolveCashPaymentMethodId(methods);
            const defaultBank = bankList.find((b) => b && b.isDefault) || bankList[0] || null;

            const excludeDepositRow =
                isEdit && paymentRowUiReceiptKind(editRow) === "security_deposit_refund"
                    ? editRow
                    : null;

            resolveAvailableSecurityDepositRefund({ excludeRow: excludeDepositRow }).then(
                (availableDepositRefund) => {
            const formCtx = {
                cashMethodId,
                paymentMethods: methods,
                defaultBankId: defaultBank ? defaultBank.id : null,
                depositRefundMaxAmount: roundMoney(availableDepositRefund)
            };

            if (checkoutFlow) {
                formCtx.lockReceiptAmount = true;
                formCtx.lockedAmount = roundMoney(checkoutFlow.amount);
            }

            const $host = $("<div>").appendTo("body");
            const formInstances = { rent: null, deposit: null, edit: null };
            let activeReceiptType = isEdit
                ? paymentRowUiReceiptKind(editRow)
                : checkoutFlow
                  ? checkoutFlow.receiptType || "security_deposit_refund"
                  : "refund";

            const disbursementSaveGuard = createFinancialSaveGuard();

            function finishDisbursementPopupMutation(receiptType) {
                const chain = checkoutFlow
                    ? afterPaymentReceiptMutation(receiptType, { quiet: true }).then(() => {
                          if (typeof checkoutFlow.onSettled === "function") {
                              return checkoutFlow.onSettled();
                          }
                      })
                    : afterPaymentReceiptMutation(receiptType);
                completeFinancialPopupSuccess($host, chain);
            }

            function getActiveDisbursementForm() {
                if (isEdit) {
                    return formInstances.edit;
                }
                return activeReceiptType === "security_deposit_refund"
                    ? formInstances.deposit
                    : formInstances.rent;
            }

            function mountDisbursementForm($container, receiptType, initialFormData) {
                const tabFormCtx = Object.assign({}, formCtx, {
                    receiptType: receiptType,
                    depositRefundMaxAmount:
                        receiptType === "security_deposit_refund"
                            ? formCtx.depositRefundMaxAmount
                            : null
                });
                try {
                    const existingForm = $container.dxForm("instance");
                    if (existingForm) {
                        existingForm.dispose();
                    }
                } catch {
                    /* container has no form yet */
                }
                $container.empty();

                $container.dxForm({
                    formData: initialFormData,
                    colCount: isPaymentsMobileViewport() ? 1 : 2,
                    labelLocation: "top",
                    readOnly: isViewOnly,
                    items: paymentDisbursementFormItems(methods, bankList, tabFormCtx),
                    onInitialized(e) {
                        const formInst = e.component;
                        wirePaymentReceiptForm(formInst, tabFormCtx);
                        if (initialFormData.paymentMethodId == null) {
                            formInst.updateData("paymentMethodId", cashMethodId);
                        }
                        if (isEdit) {
                            formInstances.edit = formInst;
                        } else if (receiptType === "security_deposit_refund") {
                            formInstances.deposit = formInst;
                        } else {
                            formInstances.rent = formInst;
                        }
                    }
                });
            }

            function submitDisbursement() {
                if (!disbursementSaveGuard.begin()) {
                    return;
                }

                const formInst = getActiveDisbursementForm();
                if (!formInst) {
                    disbursementSaveGuard.end();
                    return;
                }

                const validation = formInst.validate();
                if (!validation || !validation.isValid) {
                    disbursementSaveGuard.end();
                    return;
                }

                const fd = formInst.option("formData") || {};
                const receiptType = activeReceiptType;
                const nonCash = isNonCashPaymentMethod(
                    fd.paymentMethodId,
                    formCtx.cashMethodId,
                    formCtx.paymentMethods
                );

                if (!fd.reason || !String(fd.reason).trim()) {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.disbursement.validationReason"),
                        "warning",
                        2800
                    );
                    disbursementSaveGuard.end();
                    return;
                }

                if (nonCash && (!fd.bankId || Number(fd.bankId) <= 0)) {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.receipt.validationBank"),
                        "warning",
                        2800
                    );
                    disbursementSaveGuard.end();
                    return;
                }

                if (receiptType === "security_deposit_refund") {
                    const maxRefund = formCtx.depositRefundMaxAmount;
                    const amt = Number(fd.amountPaid) || 0;
                    if (maxRefund != null && amt > maxRefund + 0.001) {
                        DevExpress.ui.notify(
                            t("reservationDetail.payments.disbursement.validationDepositMax").replace(
                                "{max}",
                                formatMoneyEn(maxRefund)
                            ),
                            "warning",
                            3200
                        );
                        disbursementSaveGuard.end();
                        return;
                    }
                }

                const storage = mapReceiptStorageForApi(receiptType);
                const payload = {
                    hotelId: ctx.hotelId,
                    reservationId: ctx.reservationRouteId || ctx.reservationId,
                    customerId: ctx.customerId,
                    receiptType: storage.receiptType,
                    voucherCode: storage.voucherCode,
                    amountPaid: Number(fd.amountPaid) || 0,
                    receiptDate: formatLocalDateParam(fd.receiptDate || defaultReceiptDateValue()),
                    paymentMethodId: fd.paymentMethodId,
                    bankId: nonCash ? fd.bankId : null,
                    transactionNo: nonCash ? fd.transactionNo || null : null,
                    reason: String(fd.reason).trim(),
                    notes: fd.notes || null
                };

                if (isEdit) {
                    if (!svc || typeof svc.updatePaymentReceipt !== "function") {
                        DevExpress.ui.notify(
                            t("reservationDetail.payments.disbursement.updateFailed"),
                            "error",
                            3200
                        );
                        disbursementSaveGuard.end();
                        return;
                    }

                    svc
                        .updatePaymentReceipt(editRow.zaaerId, payload)
                        .then((updated) => {
                            const number =
                                (updated && (updated.receiptNo || updated.ReceiptNo)) ||
                                editRow.receiptNo ||
                                "";
                            DevExpress.ui.notify(
                                t("reservationDetail.payments.disbursement.updateSuccess").replace(
                                    "{number}",
                                    number
                                ),
                                "success",
                                3200
                            );
                            completeFinancialPopupSuccess($host, disbursementEditAfterSave(updated));
                        })
                        .catch((err) => {
                            DevExpress.ui.notify(
                                (err && err.message) ||
                                    t("reservationDetail.payments.disbursement.updateFailed"),
                                "error",
                                3600
                            );
                        })
                        .finally(() => {
                            disbursementSaveGuard.end();
                        });
                    return;
                }

                if (!svc || typeof svc.createPaymentReceipt !== "function") {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.disbursement.createFailed"),
                        "error",
                        3200
                    );
                    disbursementSaveGuard.end();
                    return;
                }

                svc
                    .createPaymentReceipt(payload)
                    .then((created) => {
                        const number =
                            (created && (created.receiptNo || created.ReceiptNo)) || "";
                        DevExpress.ui.notify(
                            t("reservationDetail.payments.disbursement.createSuccess").replace(
                                "{number}",
                                number
                            ),
                            "success",
                            3200
                        );
                        finishDisbursementPopupMutation(receiptType);
                    })
                    .catch((err) => {
                        DevExpress.ui.notify(
                            (err && err.message) ||
                                t("reservationDetail.payments.disbursement.createFailed"),
                            "error",
                            3600
                        );
                    })
                    .finally(() => {
                        disbursementSaveGuard.end();
                    });
            }

            $host.dxPopup({
                title:
                    checkoutFlow && checkoutFlow.titleKey
                        ? t(checkoutFlow.titleKey)
                        : isEdit
                          ? isViewOnly
                              ? t("reservationDetail.payments.disbursement.popupTitleView")
                              : t("reservationDetail.payments.disbursement.popupTitleEdit")
                          : t("reservationDetail.payments.disbursement.popupTitle"),
                visible: true,
                width: paymentReceiptPopupWidth(),
                height: "auto",
                maxHeight: "78vh",
                showCloseButton: true,
                hideOnOutsideClick: false,
                dragEnabled: false,
                rtlEnabled: isArabic(),
                wrapperAttr: {
                    class: checkoutFlow
                        ? "res-receipt-popup res-disbursement-popup res-action-popup checkout-payment-popup"
                        : "res-receipt-popup res-disbursement-popup res-action-popup"
                },
                contentTemplate(contentElem) {
                    const $content = $(contentElem).empty().addClass("res-receipt-popup-body");

                    if (isEdit) {
                        const receiptType = activeReceiptType;
                        const $formHost = $("<div>")
                            .addClass("res-action-form res-receipt-form")
                            .appendTo($content);
                        mountDisbursementForm(
                            $formHost,
                            receiptType,
                            paymentDisbursementRowToForm(editRow)
                        );
                        return;
                    }

                    if (checkoutFlow) {
                        const receiptType = checkoutFlow.receiptType || "security_deposit_refund";
                        activeReceiptType = receiptType;
                        const initial = buildDisbursementInitialForm(receiptType, ctx.reservationNo);
                        initial.amountPaid = formCtx.lockedAmount;
                        initial.paymentMethodId = cashMethodId;
                        const $formHost = $("<div>")
                            .addClass("res-action-form res-receipt-form")
                            .appendTo($content);
                        mountDisbursementForm($formHost, receiptType, initial);
                        return;
                    }

                    function createDisbursementTab(receiptType, text) {
                        return {
                            id: receiptType,
                            text: text,
                            receiptType: receiptType,
                            template() {
                                const $pane = $("<div>").empty();
                                const $form = $("<div>")
                                    .addClass("res-action-form res-receipt-form")
                                    .appendTo($pane);
                                const initial = buildDisbursementInitialForm(
                                    receiptType,
                                    ctx.reservationNo
                                );
                                initial.paymentMethodId = cashMethodId;
                                if (
                                    receiptType === "security_deposit_refund" &&
                                    formCtx.depositRefundMaxAmount > 0
                                ) {
                                    initial.amountPaid = formCtx.depositRefundMaxAmount;
                                }
                                mountDisbursementForm($form, receiptType, initial);

                                return $pane;
                            }
                        };
                    }

                    const tabs = [
                        createDisbursementTab(
                            "refund",
                            t("reservationDetail.payments.disbursement.tabRent")
                        ),
                        createDisbursementTab(
                            "security_deposit_refund",
                            t("reservationDetail.payments.disbursement.tabDeposit")
                        )
                    ];

                    $("<div>")
                        .appendTo($content)
                        .dxTabPanel({
                            items: tabs,
                            keyExpr: "id",
                            selectedIndex: 0,
                            deferRendering: true,
                            animationEnabled: false,
                            stylingMode: "secondary",
                            iconPosition: "top",
                            elementAttr: { class: "res-receipt-type-tabs" },
                            itemTitleTemplate(item, _index, element) {
                                $(element).text(item.text || "");
                            },
                            onSelectionChanged(e) {
                                const item = e.addedItems && e.addedItems[0];
                                activeReceiptType = (item && item.receiptType) || "refund";
                            }
                        });
                },
                toolbarItems: [
                    {
                        widget: "dxButton",
                        toolbar: "bottom",
                        location: "after",
                        options: {
                            text: isViewOnly
                                ? t("reservationDetail.payments.receipt.close")
                                : t("reservationDetail.payments.receipt.cancel"),
                            icon: "close",
                            stylingMode: "outlined",
                            onClick() {
                                $host.dxPopup("instance").hide();
                            }
                        }
                    },
                    ...(isViewOnly
                        ? []
                        : [
                              {
                                  widget: "dxButton",
                                  toolbar: "bottom",
                                  location: "after",
                                  options: {
                                      text: isEdit
                                          ? t("reservationDetail.payments.disbursement.update")
                                          : t("reservationDetail.payments.disbursement.create"),
                                      icon: "check",
                                      type: "default",
                                      stylingMode: "contained",
                                      onClick() {
                                          submitDisbursement();
                                      }
                                  }
                              }
                          ])
                ],
                onHidden() {
                    $host.remove();
                }
            });
                }
            );
        });
    }

    function openPaymentReceiptCancelPopup(row, kind) {
        if (!canCancelPaymentVoucher(kind)) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!row || !row.zaaerId) {
            DevExpress.ui.notify(t("reservationDetail.payments.receipt.missingZaaerId"), "warning", 3200);
            return;
        }

        if (isPaymentRowCancelled(row)) {
            DevExpress.ui.notify(t("reservationDetail.payments.cancel.alreadyCancelled"), "warning", 2800);
            return;
        }

        const ctx = paymentReceiptContext();
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const $host = $("<div>").appendTo("body");
        const summary = {
            receiptNo: row.receiptNo || row.number || "",
            receiptDate: formatReceiptDisplayDate(row.receiptDate || row.date),
            amountPaid: Number(row.amountPaid ?? row.amount) || 0,
            paymentMethod: mapPaymentMethodGridDisplay(
                row.paymentMethod || row.paymentMethodName || ""
            )
        };

        $host.dxPopup({
            title: t("reservationDetail.payments.cancel.popupTitle"),
            visible: true,
            width: paymentReceiptPopupWidth(),
            height: "auto",
            maxHeight: "72vh",
            showCloseButton: true,
            hideOnOutsideClick: false,
            dragEnabled: false,
            rtlEnabled: isArabic(),
            wrapperAttr: { class: "res-receipt-cancel-popup res-action-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty().addClass("res-receipt-popup-body");
                const $summary = $("<div>")
                    .addClass("res-receipt-cancel-summary")
                    .appendTo($content);
                $("<div>")
                    .addClass("res-receipt-cancel-summary-title")
                    .text(t("reservationDetail.payments.cancel.summaryTitle"))
                    .appendTo($summary);
                const rows = [
                    [t("reservationDetail.payments.cancel.summaryNo"), summary.receiptNo],
                    [t("reservationDetail.payments.cancel.summaryDate"), summary.receiptDate],
                    [
                        t("reservationDetail.payments.cancel.summaryAmount"),
                        DevExpress.localization.formatNumber(summary.amountPaid, "#,##0.00")
                    ],
                    [t("reservationDetail.payments.cancel.summaryMethod"), summary.paymentMethod]
                ];
                rows.forEach(([label, value]) => {
                    const $row = $("<div>").addClass("res-receipt-cancel-summary-row").appendTo($summary);
                    $("<span>").addClass("res-receipt-cancel-summary-label").text(label).appendTo($row);
                    $("<span>").addClass("res-receipt-cancel-summary-value").text(value || "—").appendTo($row);
                });

                $("<div>")
                    .addClass("res-action-form res-receipt-cancel-form")
                    .appendTo($content)
                    .dxForm({
                        formData: { reason: "" },
                        colCount: 1,
                        labelLocation: "top",
                        items: [
                            {
                                dataField: "reason",
                                editorType: "dxTextArea",
                                label: { text: t("reservationDetail.payments.cancel.reason") },
                                isRequired: true,
                                validationRules: [
                                    {
                                        type: "custom",
                                        message: t("reservationDetail.payments.cancel.validationReason"),
                                        validationCallback(e) {
                                            return e.value && String(e.value).trim().length > 0;
                                        }
                                    }
                                ],
                                editorOptions: {
                                    height: 72,
                                    maxLength: 500
                                }
                            }
                        ],
                        onInitialized(e) {
                            $host.data("cancelForm", e.component);
                        }
                    });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "before",
                    options: {
                        text: t("reservationDetail.payments.receipt.cancel"),
                        icon: "close",
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("reservationDetail.payments.cancel.confirm"),
                        icon: "trash",
                        type: "danger",
                        stylingMode: "contained",
                        onClick() {
                            let formInst = null;
                            try {
                                formInst = $host.data("cancelForm");
                            } catch {
                                formInst = null;
                            }
                            const validation = formInst && formInst.validate();
                            if (!validation || !validation.isValid) {
                                return;
                            }
                            const reason = String(
                                (formInst.option("formData") || {}).reason || ""
                            ).trim();
                            if (!svc || typeof svc.cancelPaymentReceipt !== "function") {
                                DevExpress.ui.notify(
                                    t("reservationDetail.payments.cancel.failed"),
                                    "error",
                                    3200
                                );
                                return;
                            }
                            svc
                                .cancelPaymentReceipt(row.zaaerId, {
                                    hotelId: ctx.hotelId,
                                    reservationId: ctx.reservationRouteId || ctx.reservationId,
                                    reason: reason
                                })
                                .then(() => {
                                    DevExpress.ui.notify(
                                        t("reservationDetail.payments.cancel.success").replace(
                                            "{number}",
                                            summary.receiptNo
                                        ),
                                        "success",
                                        3200
                                    );
                                    const voucherKey =
                                        row.voucherCode || row.receiptType || "receipt";
                                    completeFinancialPopupSuccess(
                                        $host,
                                        afterPaymentReceiptMutation(voucherKey)
                                    );
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        (err && err.message) ||
                                            t("reservationDetail.payments.cancel.failed"),
                                        "error",
                                        3600
                                    );
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function formatInvoiceHintDate(value) {
        if (!value) {
            return "—";
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "—";
        }

        return DevExpress.localization.formatDate(d, "dd/MM/yyyy");
    }

    function buildLastInvoiceHintText(lastInvoice) {
        if (!lastInvoice || !lastInvoice.invoiceNo) {
            return t("reservationDetail.payments.invoice.noLastInvoice");
        }

        const total = Number(lastInvoice.totalAmount) || 0;
        const from = formatInvoiceHintDate(lastInvoice.periodFrom);
        const to = formatInvoiceHintDate(lastInvoice.periodTo);
        return t("reservationDetail.payments.invoice.lastInvoiceHint")
            .replace("{no}", lastInvoice.invoiceNo)
            .replace("{total}", total.toFixed(2))
            .replace("{from}", from)
            .replace("{to}", to);
    }

    function openInvoicePopup(options) {
        options = options && typeof options === "object" ? options : {};

        if (!ensureReservationCompleteForOperations()) {
            return;
        }

        const ctx = paymentReceiptContext();
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const reservationRouteId = ctx.reservationRouteId || ctx.reservationId;
        if (!reservationRouteId || !ctx.hotelId || !svc) {
            DevExpress.ui.notify(t("reservationDetail.payments.invoice.createFailed"), "error", 3200);
            return;
        }

        const afterSave =
            typeof options.onSaved === "function" ? options.onSaved : afterInvoiceMutation;

        svc.loadInvoiceCreateContext(reservationRouteId)
            .then((createCtx) => {
                const prefill =
                    options.prefillAmount != null && options.prefillAmount !== ""
                        ? roundMoney(Number(options.prefillAmount))
                        : null;
                const invoiceDue =
                    prefill != null && prefill > 0
                        ? prefill
                        : roundMoney(
                              Number(
                                  (createCtx && createCtx.invoiceRemainingAmount) ||
                                      (createCtx && createCtx.balanceAmount)
                              ) || 0
                          );
                if (invoiceDue <= 0) {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.invoice.noBalance"),
                        "warning",
                        3200
                    );
                    return;
                }

                const minDate = createCtx.defaultPeriodFrom
                    ? new Date(createCtx.defaultPeriodFrom)
                    : null;
                const maxDate = createCtx.defaultPeriodTo
                    ? new Date(createCtx.defaultPeriodTo)
                    : null;

                const formData = {
                    totalAmount: invoiceDue,
                    periodFrom: minDate ? new Date(minDate) : new Date(),
                    periodTo: maxDate ? new Date(maxDate) : new Date(),
                    notes: ""
                };

                const $host = $("<div>").appendTo("body");
                let formInst = null;
                const invoiceSaveGuard = createFinancialSaveGuard();
                const hintParts = [buildLastInvoiceHintText(
                    createCtx && createCtx.lastInvoice ? createCtx.lastInvoice : null
                )];
                if (createCtx && Number(createCtx.creditNotesTotal) > 0) {
                    hintParts.push(
                        t("reservationDetail.payments.invoice.creditNotesHint").replace(
                            "{amount}",
                            roundMoney(createCtx.creditNotesTotal).toFixed(2)
                        )
                    );
                }
                const hintText = hintParts.filter(Boolean).join(" ");

                $host.dxPopup({
                    title: t("reservationDetail.payments.invoice.popupTitle"),
                    visible: true,
                    showCloseButton: true,
                    dragEnabled: false,
                    width: Math.min(720, Math.max(360, window.innerWidth - 24)),
                    height: "auto",
                    maxHeight: "62vh",
                    shading: true,
                    shadingColor: "rgba(15, 23, 42, 0.24)",
                    wrapperAttr: { class: "res-extra-popup res-fin-invoice-popup" },
                    contentTemplate(contentEl) {
                        const $content = $(contentEl);
                        $("<div>")
                            .addClass("res-fin-hint")
                            .text(hintText)
                            .appendTo($content);
                        $("<div>")
                            .addClass("res-fin-balance-label")
                            .text(
                                t("reservationDetail.payments.invoice.balanceDue").replace(
                                    "{amount}",
                                    invoiceDue.toFixed(2)
                                )
                            )
                            .appendTo($content);
                        const $formHost = $("<div>").appendTo($content);
                        $formHost.dxForm({
                            formData: formData,
                            labelLocation: "top",
                            colCount: isPaymentsMobileViewport() ? 1 : 2,
                            items: [
                                {
                                    dataField: "totalAmount",
                                    colSpan: isPaymentsMobileViewport() ? 1 : 2,
                                    editorType: "dxNumberBox",
                                    label: { text: t("reservationDetail.payments.invoice.amount") },
                                    editorOptions: {
                                        min: 0.01,
                                        max: invoiceDue,
                                        format: "#,##0.00",
                                        showSpinButtons: true
                                    },
                                    validationRules: [{ type: "required" }]
                                },
                                {
                                    dataField: "periodFrom",
                                    editorType: "dxDateBox",
                                    label: { text: t("reservationDetail.payments.invoice.periodFrom") },
                                    editorOptions: {
                                        type: "date",
                                        openOnFieldClick: true,
                                        min: minDate || undefined,
                                        max: maxDate || undefined
                                    },
                                    validationRules: [{ type: "required" }]
                                },
                                {
                                    dataField: "periodTo",
                                    editorType: "dxDateBox",
                                    label: { text: t("reservationDetail.payments.invoice.periodTo") },
                                    editorOptions: {
                                        type: "date",
                                        openOnFieldClick: true,
                                        min: minDate || undefined,
                                        max: maxDate || undefined
                                    },
                                    validationRules: [{ type: "required" }]
                                },
                                {
                                    dataField: "notes",
                                    colSpan: isPaymentsMobileViewport() ? 1 : 2,
                                    editorType: "dxTextArea",
                                    label: { text: t("reservationDetail.payments.grid.notes") },
                                    editorOptions: { height: 72, maxLength: 500 }
                                }
                            ],
                            onInitialized(e) {
                                formInst = e.component;
                            }
                        });
                    },
                    toolbarItems: [
                        {
                            widget: "dxButton",
                            toolbar: "bottom",
                            location: "after",
                            options: {
                                text: t("common.cancel"),
                                onClick() {
                                    $host.dxPopup("instance").hide();
                                }
                            }
                        },
                        {
                            widget: "dxButton",
                            toolbar: "bottom",
                            location: "after",
                            options: {
                                text: t("common.save"),
                                type: "default",
                                onClick() {
                                    if (!invoiceSaveGuard.begin()) {
                                        return;
                                    }

                                    if (!formInst) {
                                        invoiceSaveGuard.end();
                                        return;
                                    }

                                    const validation = formInst.validate();
                                    if (!validation || !validation.isValid) {
                                        invoiceSaveGuard.end();
                                        return;
                                    }

                                    const fd = formInst.option("formData") || {};
                                    const amount = Number(fd.totalAmount) || 0;
                                    if (amount <= 0 || amount > invoiceDue + 0.01) {
                                        DevExpress.ui.notify(
                                            t("reservationDetail.payments.invoice.amountInvalid"),
                                            "warning",
                                            3000
                                        );
                                        invoiceSaveGuard.end();
                                        return;
                                    }

                                    const payload = {
                                        hotelId: ctx.hotelId,
                                        reservationId: ctx.reservationRouteId || ctx.reservationId,
                                        totalAmount: amount,
                                        periodFrom: formatLocalDateParam(fd.periodFrom),
                                        periodTo: formatLocalDateParam(fd.periodTo),
                                        notes: fd.notes || null
                                    };

                                    svc.createInvoice(payload)
                                        .then(() => {
                                            DevExpress.ui.notify(
                                                t("reservationDetail.payments.invoice.createSuccess"),
                                                "success",
                                                2800
                                            );
                                            completeFinancialPopupSuccess($host, afterSave());
                                        })
                                        .catch((err) => {
                                            DevExpress.ui.notify(
                                                (err && err.message) ||
                                                    t("reservationDetail.payments.invoice.createFailed"),
                                                "error",
                                                3600
                                            );
                                        })
                                        .finally(() => {
                                            invoiceSaveGuard.end();
                                        });
                                }
                            }
                        }
                    ],
                    onHidden() {
                        $host.remove();
                    }
                });
            })
            .catch(() => {
                DevExpress.ui.notify(
                    t("reservationDetail.payments.invoice.createFailed"),
                    "error",
                    3200
                );
            });
    }

    function openAdjustmentAmountPopup(options) {
        options = options || {};
        const kind = options.kind === "debit_note" ? "debit_note" : "credit_note";
        const row = options.invoiceRow;
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const ctx = paymentReceiptContext();
        if (!row || !row.invoiceId || !svc || !ctx.hotelId) {
            return;
        }

        const isCredit = kind === "credit_note";
        const remaining = invoiceAdjustmentRemaining(row);
        const defaultAmount = remaining > 0 ? remaining : roundMoney(Number(row.totalAmount || row.amount) || 0);
        const formData = {
            amount: defaultAmount,
            reason: "",
            notes: ""
        };

        const $host = $("<div>").appendTo("body");
        let formInst = null;
        const adjustmentSaveGuard = createFinancialSaveGuard();
        const warnParent = !row.parentZatcaSubmitted
            ? t("reservationDetail.payments.invoice.zatcaParentRequired")
            : "";

        $host.dxPopup({
            title: isCredit
                ? t("reservationDetail.payments.creditNote.popupTitle")
                : t("reservationDetail.payments.debitNote.popupTitle"),
            visible: true,
            showCloseButton: true,
            dragEnabled: false,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-fin-adjustment-popup" },
            contentTemplate(contentEl) {
                const $content = $(contentEl);
                if (warnParent) {
                    $("<div>").addClass("res-fin-hint res-fin-hint--warn").text(warnParent).appendTo($content);
                }

                $("<div>")
                    .addClass("res-fin-hint")
                    .text(
                        t("reservationDetail.payments.invoice.adjustmentFor").replace(
                            "{no}",
                            row.invoiceNo || row.number || ""
                        )
                    )
                    .appendTo($content);

                const $formHost = $("<div>").appendTo($content);
                $formHost.dxForm({
                    formData: formData,
                    labelLocation: "top",
                    items: [
                        {
                            dataField: "amount",
                            editorType: "dxNumberBox",
                            label: {
                                text: isCredit
                                    ? t("reservationDetail.payments.creditNote.amount")
                                    : t("reservationDetail.payments.debitNote.amount")
                            },
                            editorOptions: {
                                min: 0.01,
                                max: defaultAmount,
                                format: "#,##0.00",
                                showSpinButtons: true
                            },
                            validationRules: [{ type: "required" }]
                        },
                        {
                            dataField: "reason",
                            editorType: "dxTextArea",
                            label: { text: t("reservationDetail.payments.invoice.reason") },
                            editorOptions: { height: 72, maxLength: 500 },
                            validationRules: [{ type: "required" }]
                        },
                        {
                            dataField: "notes",
                            editorType: "dxTextBox",
                            label: { text: t("reservationDetail.payments.grid.notes") },
                            editorOptions: { maxLength: 500 }
                        }
                    ],
                    onInitialized(e) {
                        formInst = e.component;
                    }
                });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.cancel"),
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.save"),
                        type: "default",
                        onClick() {
                            if (!adjustmentSaveGuard.begin()) {
                                return;
                            }

                            if (!formInst) {
                                adjustmentSaveGuard.end();
                                return;
                            }

                            const validation = formInst.validate();
                            if (!validation || !validation.isValid) {
                                adjustmentSaveGuard.end();
                                return;
                            }

                            const fd = formInst.option("formData") || {};
                            const amount = Number(fd.amount) || 0;
                            if (amount <= 0) {
                                adjustmentSaveGuard.end();
                                return;
                            }

                            const payload = {
                                hotelId: ctx.hotelId,
                                invoiceId: row.invoiceId,
                                reason: String(fd.reason || "").trim(),
                                notes: fd.notes || null
                            };

                            if (isCredit) {
                                payload.creditAmount = amount;
                            } else {
                                payload.debitAmount = amount;
                            }

                            const savePromise = isCredit
                                ? svc.createCreditNote(payload)
                                : svc.createDebitNote(payload);

                            savePromise
                                .then(() => {
                                    DevExpress.ui.notify(
                                        isCredit
                                            ? t("reservationDetail.payments.creditNote.createSuccess")
                                            : t("reservationDetail.payments.debitNote.createSuccess"),
                                        "success",
                                        2800
                                    );
                                    completeFinancialPopupSuccess(
                                        $host,
                                        afterInvoiceMutation({ quiet: true })
                                    );
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        (err && err.message) ||
                                            (isCredit
                                                ? t("reservationDetail.payments.creditNote.createFailed")
                                                : t("reservationDetail.payments.debitNote.createFailed")),
                                        "error",
                                        3600
                                    );
                                })
                                .finally(() => {
                                    adjustmentSaveGuard.end();
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function openCreditNotePopup(invoiceRow) {
        if (!hasPmsPermission("finance.credit_note.create")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        openAdjustmentAmountPopup({ kind: "credit_note", invoiceRow: invoiceRow });
    }

    function openDebitNotePopup(invoiceRow) {
        if (!hasPmsPermission("finance.debit_note.create")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        openAdjustmentAmountPopup({ kind: "debit_note", invoiceRow: invoiceRow });
    }

    function sendZatcaFromInvoiceRow(kind, row) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || !row) {
            return;
        }

        const docKind = kind || "invoice";
        const docId =
            docKind === "invoice"
                ? row.invoiceId
                : row.documentId;
        const perm =
            docKind === "credit_note"
                ? "finance.credit_note.send_zatca"
                : docKind === "debit_note"
                  ? "finance.debit_note.send_zatca"
                  : "finance.invoice.send_zatca";

        if (!hasPmsPermission(perm)) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        ensureZatcaDecryptReadyForManualSend().then((ready) => {
            if (!ready) {
                return;
            }

            DevExpress.ui.notify(t("reservationDetail.payments.zatca.sending"), "info", 2000);
            svc.sendZatcaDocument(docKind, docId)
                .then(() => {
                    DevExpress.ui.notify(
                        t("reservationDetail.payments.zatca.sendSuccess"),
                        "success",
                        3200
                    );
                    refreshAllPaymentGrids();
                })
                .catch((err) => {
                    DevExpress.ui.notify(
                        (err && err.message) || t("reservationDetail.payments.zatca.sendFailed"),
                        "error",
                        4000
                    );
                });
        });
    }

    function zatcaDecryptBannerStorageKey() {
        const code =
            window.Zaaer && window.Zaaer.ApiService && typeof window.Zaaer.ApiService.getHotelCode === "function"
                ? window.Zaaer.ApiService.getHotelCode()
                : "";
        return `pms-zatca-decrypt-banner-dismissed:${code || "default"}`;
    }

    function unwrapZatcaDevicePayload(res) {
        const raw = res && (res.data !== undefined ? res.data : res.Data !== undefined ? res.Data : res);
        if (raw && raw.data !== undefined && raw.data !== null) {
            return raw.data;
        }

        return raw;
    }

    function fetchZatcaDeviceHealth() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || typeof api.get !== "function") {
            return Promise.resolve(null);
        }

        return api
            .get("/api/v1/pms/integrations/zatca/device")
            .then((res) => unwrapZatcaDevicePayload(res))
            .catch(() => null);
    }

    function isZatcaPrivateKeyUndecryptable(device) {
        if (!device || typeof device !== "object") {
            return false;
        }

        return device.canDecryptPrivateKey === false;
    }

    function showZatcaDecryptWarningPopup() {
        const $host = $("<div>").appendTo("body");
        let popupInst = null;

        $host.dxPopup({
            title: t("reservationDetail.payments.zatca.decryptWarnTitle"),
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            visible: true,
            showCloseButton: true,
            showTitle: true,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup res-zatca-decrypt-popup-wrap" },
            contentTemplate(contentEl) {
                const $content = $(contentEl).empty();
                const $shell = $("<div>").addClass("res-zatca-decrypt-popup").appendTo($content);
                $("<div>")
                    .addClass("res-zatca-decrypt-popup__icon dx-icon dx-icon-warning")
                    .attr("aria-hidden", "true")
                    .appendTo($shell);
                $("<p>").addClass("res-zatca-decrypt-popup__lead").text(t("reservationDetail.payments.zatca.decryptWarnIntro")).appendTo($shell);
                $("<ul>")
                    .addClass("res-zatca-decrypt-popup__list")
                    .append($("<li>").text(t("integrations.zatca.masterKeyServerHint")))
                    .append($("<li>").text(t("integrations.zatca.alertDecrypt")))
                    .appendTo($shell);
                const $actions = $("<div>").addClass("res-zatca-decrypt-popup__actions").appendTo($shell);
                $("<div>")
                    .appendTo($actions)
                    .dxButton({
                        text: t("reservationDetail.payments.zatca.decryptWarnOpenSettings"),
                        type: "default",
                        stylingMode: "contained",
                        icon: "preferences",
                        onClick() {
                            window.location.href = "/integration-zatca-settings.html";
                        }
                    });
                $("<div>")
                    .appendTo($actions)
                    .dxButton({
                        text: t("common.close"),
                        stylingMode: "outlined",
                        onClick() {
                            if (popupInst) {
                                popupInst.hide();
                            }
                        }
                    });
            },
            onInitialized(e) {
                popupInst = e.component;
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function ensureZatcaDecryptReadyForManualSend() {
        return fetchZatcaDeviceHealth().then((device) => {
            if (!isZatcaPrivateKeyUndecryptable(device)) {
                return true;
            }

            showZatcaDecryptWarningPopup();
            return false;
        });
    }

    function mountZatcaDecryptBannerIfNeeded($root) {
        if (!$root || !$root.length) {
            return;
        }

        if (sessionStorage.getItem(zatcaDecryptBannerStorageKey()) === "1") {
            return;
        }

        if ($root.find(".res-zatca-decrypt-banner").length) {
            return;
        }

        fetchZatcaDeviceHealth().then((device) => {
            if (!isZatcaPrivateKeyUndecryptable(device)) {
                return;
            }

            if ($root.find(".res-zatca-decrypt-banner").length) {
                return;
            }

            const $banner = $("<div>").addClass("res-zatca-decrypt-banner").prependTo($root);
            const $body = $("<div>").addClass("res-zatca-decrypt-banner__body").appendTo($banner);
            $("<span>")
                .addClass("res-zatca-decrypt-banner__icon dx-icon dx-icon-warning")
                .attr("aria-hidden", "true")
                .appendTo($body);
            $("<div>")
                .addClass("res-zatca-decrypt-banner__text")
                .text(t("reservationDetail.payments.zatca.decryptWarnBanner"))
                .appendTo($body);
            const $actions = $("<div>").addClass("res-zatca-decrypt-banner__actions").appendTo($banner);
            $("<button>")
                .attr("type", "button")
                .addClass("res-zatca-decrypt-banner__link")
                .text(t("reservationDetail.payments.zatca.decryptWarnOpenSettings"))
                .on("click", () => {
                    window.location.href = "/integration-zatca-settings.html";
                })
                .appendTo($actions);
            $("<button>")
                .attr("type", "button")
                .addClass("res-zatca-decrypt-banner__dismiss")
                .text(t("reservationDetail.payments.zatca.decryptWarnDismiss"))
                .on("click", () => {
                    sessionStorage.setItem(zatcaDecryptBannerStorageKey(), "1");
                    $banner.remove();
                })
                .appendTo($actions);
        });
    }

    function openInvoiceRelatedPopup(invoiceRow) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!invoiceRow || !invoiceRow.invoiceId || !svc) {
            return;
        }

        svc.loadInvoiceAdjustments(invoiceRow.invoiceId).then((rows) => {
            const list = Array.isArray(rows) ? rows : [];
            const $host = $("<div>").appendTo("body");

            $host.dxPopup({
                title: t("reservationDetail.payments.invoice.relatedTitle").replace(
                    "{no}",
                    invoiceRow.invoiceNo || ""
                ),
                visible: true,
                showCloseButton: true,
                width: Math.min(860, Math.max(360, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "78vh",
                minHeight: Math.min(480, Math.max(320, Math.floor(window.innerHeight * 0.52))),
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                wrapperAttr: { class: "res-extra-popup res-fin-related-popup" },
                contentTemplate(contentEl) {
                    const $gridHost = $("<div>").appendTo(contentEl);
                    $gridHost.dxDataGrid(
                        reservationSectionDataGridOptions({
                            dataSource: list,
                            keyExpr: "documentId",
                            noDataText: t("reservationDetail.payments.invoice.relatedEmpty"),
                            columns: [
                                {
                                    dataField: "kind",
                                    caption: t("reservationDetail.payments.invoice.relatedKind"),
                                    width: 120,
                                    customizeText(e) {
                                        const k = e.value ? String(e.value) : "";
                                        const key = `reservationDetail.payments.invoice.kind.${k}`;
                                        const lbl = t(key);
                                        return lbl && lbl !== key ? lbl : k;
                                    }
                                },
                                { dataField: "documentNo", caption: t("reservationDetail.payments.grid.number"), width: 130 },
                                {
                                    dataField: "documentDate",
                                    caption: t("reservationDetail.payments.grid.date"),
                                    dataType: "date",
                                    format: "dd/MM/yyyy",
                                    width: 110
                                },
                                {
                                    dataField: "amount",
                                    caption: t("reservationDetail.payments.grid.amount"),
                                    dataType: "number",
                                    format: "#,##0.00",
                                    width: 110
                                },
                                {
                                    dataField: "zatcaStatus",
                                    caption: t("reservationDetail.payments.invoice.zatcaStatus"),
                                    cellTemplate: zatcaStatusCellTemplate,
                                    width: 110
                                },
                                { dataField: "reason", caption: t("reservationDetail.payments.invoice.reason"), minWidth: 140 },
                                {
                                    type: "buttons",
                                    width: 56,
                                    buttons: [
                                        {
                                            hint: t("reservationDetail.payments.zatca.send"),
                                            icon: "export",
                                            visible(e) {
                                                const data = e.row && e.row.data;
                                                if (!data) {
                                                    return false;
                                                }

                                                const perm =
                                                    data.kind === "credit_note"
                                                        ? "finance.credit_note.send_zatca"
                                                        : data.kind === "debit_note"
                                                          ? "finance.debit_note.send_zatca"
                                                          : "finance.invoice.send_zatca";
                                                return (
                                                    hasPmsPermission(perm) &&
                                                    isZatcaSendableStatus(data.zatcaStatus)
                                                );
                                            },
                                            onClick(e) {
                                                sendZatcaFromInvoiceRow(
                                                    e.row.data.kind,
                                                    e.row.data
                                                );
                                            }
                                        }
                                    ]
                                }
                            ]
                        })
                    );
                },
                onHidden() {
                    $host.remove();
                }
            });
        });
    }

    function notifyPaymentAction(actionId) {
        if (actionId === "receipt") {
            if (!hasPmsPermission("payments.create")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
            if (!ensureReservationCompleteForOperations()) {
                return;
            }

            openPaymentReceiptPopup();
            return;
        }

        if (actionId === "disbursement") {
            if (!hasPmsPermission("payments.refund")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
            if (!ensureReservationCompleteForOperations()) {
                return;
            }

            openPaymentDisbursementPopup();
            return;
        }

        if (actionId === "promissory") {
            if (!hasPmsPermission("finance.promissory.create")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
            if (!ensureReservationCompleteForOperations()) {
                return;
            }

            openPromissoryNotePopup();
            return;
        }

        if (actionId === "invoice") {
            if (!hasPmsPermission("finance.invoice.create")) {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                return;
            }
            if (!ensureReservationCompleteForOperations()) {
                return;
            }

            openInvoicePopup();
            return;
        }

        const item = paymentActionItems().find((x) => x.id === actionId);
        DevExpress.ui.notify(
            t("reservationDetail.payments.actionPending").replace("{action}", item ? item.text : ""),
            "info",
            2800
        );
    }

    function resolvePromissoryPayableTo(ctx) {
        const detail = (ctx && ctx.detail) || pageCtx.detail || {};
        const guests = Array.isArray(detail.guests) ? detail.guests : [];
        const primary = guests.find((g) => g && g.isPrimary) || guests[0] || null;
        if (primary && (primary.fullName || primary.customerName)) {
            return String(primary.fullName || primary.customerName).trim();
        }

        if (detail.header && detail.header.guestName) {
            return String(detail.header.guestName).trim();
        }

        return "";
    }

    function buildPromissoryNoteFormItems() {
        const mobile = isPaymentsMobileViewport();
        const fullSpan = mobile ? 1 : 2;

        return [
            {
                dataField: "payableTo",
                colSpan: fullSpan,
                editorType: "dxTextBox",
                label: { text: t("reservationDetail.payments.promissory.payableTo") },
                editorOptions: { readOnly: true }
            },
            {
                dataField: "reason",
                colSpan: fullSpan,
                editorType: "dxTextBox",
                label: { text: t("reservationDetail.payments.promissory.reason") }
            },
            {
                dataField: "placeOfMaturity",
                colSpan: fullSpan,
                editorType: "dxTextBox",
                label: { text: t("reservationDetail.payments.promissory.placeOfMaturity") }
            },
            {
                dataField: "maturityDate",
                colSpan: mobile ? fullSpan : 1,
                editorType: "dxDateBox",
                label: { text: t("reservationDetail.payments.promissory.maturityDate") },
                isRequired: true,
                editorOptions: {
                    type: "date",
                    displayFormat: "dd/MM/yyyy",
                    openOnFieldClick: true
                }
            },
            {
                dataField: "amount",
                colSpan: mobile ? fullSpan : 1,
                editorType: "dxNumberBox",
                label: { text: t("reservationDetail.payments.promissory.amount") },
                isRequired: true,
                editorOptions: {
                    format: "#,##0.00",
                    min: 0.01,
                    showSpinButtons: true
                }
            },
            {
                dataField: "notes",
                colSpan: fullSpan,
                editorType: "dxTextArea",
                label: { text: t("reservationDetail.payments.promissory.notes") },
                editorOptions: { height: 52, minHeight: 52, maxHeight: 52 }
            }
        ];
    }

    function openPromissoryNotePopup(options) {
        options = options || {};
        const editRow = options.editRow || null;
        const isEdit = !!(editRow && editRow.zaaerId);

        if (isEdit && !canEditPromissoryNoteVoucher()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!isEdit && !hasPmsPermission("finance.promissory.create")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!isEdit && !ensureReservationCompleteForOperations()) {
            return;
        }

        const ctx = paymentReceiptContext();
        if ((!ctx.reservationRouteId && !ctx.reservationId) || !ctx.hotelId) {
            DevExpress.ui.notify(
                isEdit
                    ? t("reservationDetail.payments.promissory.updateFailed")
                    : t("reservationDetail.payments.promissory.createFailed"),
                "error",
                3200
            );
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const snapshot = computeReservationFinancialSnapshot();
        const defaultAmount = isEdit
            ? roundMoney(editRow.amount)
            : roundMoney(Math.max(0, snapshot.balance || 0));
        const defaultReason = t("reservationDetail.payments.promissory.reasonDefault").replace(
            "{number}",
            ctx.reservationNo || ""
        );
        const maturityDefault =
            isEdit && editRow.maturityDate ? new Date(editRow.maturityDate) : new Date();

        const formData = {
            payableTo: isEdit
                ? editRow.payableTo || resolvePromissoryPayableTo(ctx)
                : resolvePromissoryPayableTo(ctx),
            reason: isEdit ? editRow.reason || defaultReason : defaultReason,
            placeOfMaturity: isEdit ? editRow.placeOfMaturity || "" : "",
            maturityDate: maturityDefault,
            amount: defaultAmount,
            notes: isEdit ? editRow.notes || "" : ""
        };

        const $host = $("<div>").appendTo("body");
        let formInst = null;
        const promissorySaveGuard = createFinancialSaveGuard();

        function submitPromissoryNote() {
            if (!promissorySaveGuard.begin()) {
                return;
            }

            if (!formInst) {
                promissorySaveGuard.end();
                return;
            }

            const validation = formInst.validate();
            if (!validation || !validation.isValid) {
                promissorySaveGuard.end();
                return;
            }

            const fd = formInst.option("formData") || {};
            if (!fd.maturityDate) {
                DevExpress.ui.notify(
                    t("reservationDetail.payments.promissory.validationMaturity"),
                    "warning",
                    2800
                );
                promissorySaveGuard.end();
                return;
            }

            const amount = Number(fd.amount) || 0;
            if (amount <= 0) {
                DevExpress.ui.notify(
                    t("reservationDetail.payments.promissory.validationAmount"),
                    "warning",
                    2800
                );
                promissorySaveGuard.end();
                return;
            }

            if (!svc) {
                DevExpress.ui.notify(
                    isEdit
                        ? t("reservationDetail.payments.promissory.updateFailed")
                        : t("reservationDetail.payments.promissory.createFailed"),
                    "error",
                    3200
                );
                promissorySaveGuard.end();
                return;
            }

            const payload = {
                hotelId: ctx.hotelId,
                    reservationId: ctx.reservationRouteId || ctx.reservationId,
                customerId: ctx.customerId,
                corporateId: ctx.corporateId,
                payableTo: fd.payableTo || null,
                reason: fd.reason || null,
                placeOfMaturity: fd.placeOfMaturity || null,
                maturityDate: formatLocalDateParam(fd.maturityDate),
                amount: amount,
                paymentLinkSent: false,
                notes: fd.notes || null
            };

            const savePromise = isEdit
                ? svc.updatePromissoryNote(editRow.zaaerId, payload)
                : svc.createPromissoryNote(payload);

            savePromise
                .then((saved) => {
                    const number =
                        (saved && (saved.promissoryNo || saved.number)) ||
                        (editRow && (editRow.promissoryNo || editRow.number)) ||
                        "";
                    DevExpress.ui.notify(
                        (isEdit
                            ? t("reservationDetail.payments.promissory.updateSuccess")
                            : t("reservationDetail.payments.promissory.createSuccess")
                        ).replace("{number}", number),
                        "success",
                        3200
                    );
                    completeFinancialPopupSuccess($host, afterPromissoryNoteMutation());
                })
                .catch((err) => {
                    DevExpress.ui.notify(
                        (err && err.message) ||
                            (isEdit
                                ? t("reservationDetail.payments.promissory.updateFailed")
                                : t("reservationDetail.payments.promissory.createFailed")),
                        "error",
                        3600
                    );
                })
                .finally(() => {
                    promissorySaveGuard.end();
                });
        }

        $host.dxPopup({
            title: isEdit
                ? t("reservationDetail.payments.promissory.popupTitleEdit")
                : t("reservationDetail.payments.promissory.popupTitle"),
            visible: true,
            width: paymentReceiptPopupWidth(),
            height: "auto",
            maxHeight: "78vh",
            showCloseButton: true,
            hideOnOutsideClick: false,
            dragEnabled: false,
            rtlEnabled: isArabic(),
            wrapperAttr: { class: "res-receipt-popup res-action-popup res-promissory-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty().addClass("res-receipt-popup-body");
                const $formHost = $("<div>")
                    .addClass("res-action-form res-promissory-form")
                    .appendTo($content);

                $formHost.dxForm({
                    formData: formData,
                    colCount: isPaymentsMobileViewport() ? 1 : 2,
                    labelLocation: "top",
                    items: buildPromissoryNoteFormItems(),
                    onInitialized(e) {
                        formInst = e.component;
                    }
                });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("reservationDetail.payments.receipt.cancel"),
                        icon: "close",
                        stylingMode: "outlined",
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: isEdit
                            ? t("reservationDetail.payments.receipt.update")
                            : t("reservationDetail.payments.receipt.create"),
                        icon: "check",
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            submitPromissoryNote();
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function openPromissoryNoteCancelPopup(row) {
        if (!canCancelPromissoryNoteVoucher()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!row || !row.zaaerId) {
            DevExpress.ui.notify(t("reservationDetail.payments.receipt.missingZaaerId"), "warning", 3200);
            return;
        }

        if (isPromissoryRowCancelled(row)) {
            DevExpress.ui.notify(
                t("reservationDetail.payments.promissory.cancel.alreadyCancelled"),
                "warning",
                2800
            );
            return;
        }

        const ctx = paymentReceiptContext();
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const $root = $("<div>").appendTo("body");

        $root.dxPopup({
            title: t("reservationDetail.payments.promissory.cancel.popupTitle"),
            visible: true,
            width: paymentReceiptPopupWidth(),
            height: "auto",
            maxHeight: "62vh",
            showCloseButton: true,
            hideOnOutsideClick: false,
            dragEnabled: false,
            rtlEnabled: isArabic(),
            wrapperAttr: { class: "res-receipt-cancel-popup res-receipt-popup res-action-popup" },
            contentTemplate(contentElem) {
                const $content = $(contentElem).empty().addClass("res-receipt-popup-body");
                $("<div>")
                    .addClass("res-action-form res-receipt-cancel-form")
                    .appendTo($content)
                    .dxForm({
                        formData: { reason: "" },
                        colCount: 1,
                        labelLocation: "top",
                        items: [
                            {
                                dataField: "reason",
                                editorType: "dxTextArea",
                                label: { text: t("reservationDetail.payments.promissory.cancel.reason") },
                                isRequired: true,
                                validationRules: [
                                    {
                                        type: "custom",
                                        message: t("reservationDetail.payments.cancel.validationReason"),
                                        validationCallback(e) {
                                            return e.value && String(e.value).trim().length > 0;
                                        }
                                    }
                                ],
                                editorOptions: { height: 88, maxLength: 500 }
                            }
                        ],
                        onInitialized(e) {
                            $root.data("cancelForm", e.component);
                        }
                    });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "before",
                    options: {
                        text: t("common.cancel"),
                        icon: "close",
                        stylingMode: "outlined",
                        onClick() {
                            $root.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("reservationDetail.payments.cancel.confirm"),
                        icon: "trash",
                        type: "danger",
                        stylingMode: "contained",
                        onClick() {
                            let formInst = null;
                            try {
                                formInst = $root.data("cancelForm");
                            } catch {
                                formInst = null;
                            }
                            const validation = formInst && formInst.validate();
                            if (!validation || !validation.isValid) {
                                return;
                            }

                            const reason = String((formInst.option("formData") || {}).reason || "").trim();
                            if (!reason) {
                                return;
                            }

                            if (!svc || typeof svc.cancelPromissoryNote !== "function") {
                                DevExpress.ui.notify(
                                    t("reservationDetail.payments.promissory.cancel.failed"),
                                    "error",
                                    3200
                                );
                                return;
                            }

                            svc
                                .cancelPromissoryNote(row.zaaerId, {
                                    hotelId: ctx.hotelId,
                                    reservationId: ctx.reservationRouteId || ctx.reservationId,
                                    reason: reason
                                })
                                .then(() => {
                                    DevExpress.ui.notify(
                                        t("reservationDetail.payments.promissory.cancel.success").replace(
                                            "{number}",
                                            row.promissoryNo || row.number || ""
                                        ),
                                        "success",
                                        3200
                                    );
                                    completeFinancialPopupSuccess(
                                        $root,
                                        afterPromissoryNoteMutation()
                                    );
                                })
                                .catch((err) => {
                                    DevExpress.ui.notify(
                                        (err && err.message) ||
                                            t("reservationDetail.payments.promissory.cancel.failed"),
                                        "error",
                                        3600
                                    );
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $root.remove();
            }
        });
    }

    function loadReservationPaymentRows(kind) {
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const reservationRouteId = resolvePaymentReservationRouteId(pageCtx.detail);
        if (svc && typeof svc.loadPaymentRows === "function") {
            return Promise.resolve(svc.loadPaymentRows({
                kind,
                reservationId: reservationRouteId,
                zaaerId: pageCtx.detail && pageCtx.detail.zaaerId,
                hotelId: pageCtx.hotelIdParam || (pageCtx.detail && pageCtx.detail.hotelId)
            })).then((res) => {
                if (Array.isArray(res)) {
                    return res;
                }

                if (res && Array.isArray(res.data)) {
                    return res.data;
                }

                if (res && Array.isArray(res.items)) {
                    return res.items;
                }

                return [];
            });
        }

        return Promise.resolve([]);
    }

    function paymentVoucherHiddenColumns() {
        return [
            { dataField: "zaaerId", visible: false },
            { dataField: "paymentMethodId", visible: false },
            { dataField: "bankId", visible: false },
            { dataField: "transactionNo", visible: false },
            { dataField: "receiptType", visible: false },
            { dataField: "hotelId", visible: false },
            { dataField: "reservationId", visible: false },
            { dataField: "customerId", visible: false },
            { dataField: "unitId", visible: false }
        ];
    }

    function paymentVoucherGridColumns(kind) {
        const mobile = isPaymentsMobileViewport();
        const isDisbursement = kind === "disbursements";
        const cols = [
            {
                dataField: "receiptNo",
                caption: t("reservationDetail.payments.grid.receiptNo"),
                minWidth: mobile ? 92 : 120
            },
            {
                dataField: "amountPaid",
                caption: t("reservationDetail.payments.grid.amount"),
                dataType: "number",
                format: "#,##0.00",
                minWidth: mobile ? 92 : 110
            },
            {
                dataField: "voucherCode",
                caption: t("reservationDetail.payments.grid.voucherCode"),
                minWidth: mobile ? 104 : 140,
                customizeText(e) {
                    return mapVoucherCodeGridDisplay(e.value);
                }
            },
            {
                dataField: "receiptDate",
                caption: t("reservationDetail.payments.grid.receiptDate"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: mobile ? 96 : 120
            },
            {
                dataField: "paymentMethod",
                caption: t("reservationDetail.payments.grid.paymentMethod"),
                minWidth: mobile ? 92 : 120,
                customizeText(e) {
                    return mapPaymentMethodGridDisplay(e.value);
                }
            },
            {
                dataField: "notes",
                caption: t("reservationDetail.payments.grid.notes"),
                minWidth: mobile ? 180 : 280,
                width: mobile ? 220 : 300,
                cssClass: "expenses-col-comment res-payment-grid-cell--notes",
                cellTemplate(container, options) {
                    const text = (options.value || "").toString().trim();
                    if (!text) {
                        $("<span>").text("—").appendTo(container);
                        return;
                    }
                    $("<span>")
                        .addClass("expenses-grid-comment-cell")
                        .attr("title", text)
                        .text(text)
                        .appendTo(container);
                }
            }
        ];

        if (!isDisbursement) {
            const receiptPeriodDateWidth = mobile ? 96 : 108;
            cols.push(
                {
                    dataField: "receiptFrom",
                    caption: t("reservationDetail.payments.grid.receiptFrom"),
                    dataType: "date",
                    format: "dd/MM/yyyy",
                    minWidth: receiptPeriodDateWidth,
                    width: receiptPeriodDateWidth,
                    visible: true,
                    cssClass: "res-payment-grid-cell--date"
                },
                {
                    dataField: "receiptTo",
                    caption: t("reservationDetail.payments.grid.receiptTo"),
                    dataType: "date",
                    format: "dd/MM/yyyy",
                    minWidth: receiptPeriodDateWidth,
                    width: receiptPeriodDateWidth,
                    visible: true,
                    cssClass: "res-payment-grid-cell--date res-payment-grid-cell--date-to"
                }
            );
        }

        cols.push({
            dataField: "receiptStatus",
            caption: t("reservationDetail.payments.grid.receiptStatus"),
            minWidth: mobile ? 84 : 92,
            width: mobile ? 92 : 92,
            allowFiltering: true,
            allowSorting: true,
            cssClass: "res-payment-grid-cell--status",
            cellTemplate: paymentReceiptStatusCellTemplate
        });

        return cols.concat(paymentVoucherHiddenColumns());
    }

    function isZatcaSendableStatus(status) {
        const s = String(status || "").toLowerCase();
        return s === "pending" || s === "failed";
    }

    function isInvoiceZatcaSubmitted(row) {
        if (!row) {
            return false;
        }
        if (row.parentZatcaSubmitted) {
            return true;
        }
        const s = String(row.zatcaStatus || "").toLowerCase();
        return s === "reported" || s === "cleared";
    }

    function invoiceAdjustmentRemaining(row) {
        if (!row) {
            return 0;
        }
        if (row.adjustmentRemainingAmount != null && row.adjustmentRemainingAmount !== "") {
            return roundMoney(Number(row.adjustmentRemainingAmount) || 0);
        }
        return roundMoney(Number(row.totalAmount || row.amount) || 0);
    }

    function canAdjustInvoice(row) {
        return invoiceAdjustmentRemaining(row) > 0.01;
    }

    function hasInvoiceRouteId(row) {
        if (!row) {
            return false;
        }
        return (
            parsePositiveInvoiceId(row.invoiceId) != null ||
            parsePositiveInvoiceId(row.zaaerId) != null
        );
    }

    function parsePositiveInvoiceId(value) {
        if (value == null || value === "") {
            return null;
        }
        const n = Number(value);
        if (!Number.isFinite(n) || n <= 0 || Math.floor(n) !== n) {
            return null;
        }
        return n;
    }

    function resolveInvoicePrintTarget(row) {
        if (!row) {
            return null;
        }
        const invoiceId = parsePositiveInvoiceId(row.invoiceId);
        if (invoiceId != null) {
            return { mode: "internal", id: invoiceId };
        }
        const zaaerId = parsePositiveInvoiceId(row.zaaerId);
        if (zaaerId != null) {
            return { mode: "zaaer", id: zaaerId };
        }
        return null;
    }

    function invoicePrintEndpoint(target) {
        if (!target || target.id == null || target.id === "") {
            return null;
        }
        if (target.mode === "zaaer") {
            return `/api/v1/pms/invoices/by-zaaer/${encodeURIComponent(target.id)}/print`;
        }
        return `/api/v1/pms/invoices/${encodeURIComponent(target.id)}/print`;
    }

    const invoicePdfBlobCache = new Map();
    let invoicePreviewWarmScheduled = false;
    let invoicePreviewLoadToken = 0;
    let invoicePreviewPopupHost = null;

    function scheduleWarmInvoicePreviewPopup() {
        if (invoicePreviewWarmScheduled || !hasPmsPermission("finance.invoice.view")) {
            return;
        }
        invoicePreviewWarmScheduled = true;
        window.setTimeout(function () {
            try {
                ensureInvoicePreviewPopup();
            } catch {
                /* ignore warm-up errors */
            }
        }, 0);
    }

    function loadInvoicePdfBlob(row) {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || typeof api.getBlob !== "function") {
            return Promise.reject(new Error("ApiService unavailable"));
        }

        const target = typeof row === "object" ? resolveInvoicePrintTarget(row) : { mode: "zaaer", id: row };
        const endpoint = invoicePrintEndpoint(target);
        if (!endpoint) {
            return Promise.reject(new Error("Missing invoice id"));
        }

        const cached = invoicePdfBlobCache.get(endpoint);
        if (cached) {
            return Promise.resolve(cached);
        }

        return api.getBlob(endpoint, { timeoutMs: 120000 }).then(function (blob) {
            if (blob && blob.size > 0) {
                invoicePdfBlobCache.set(endpoint, blob);
            }
            return blob;
        });
    }

    function invoicePreviewPopupDimensions() {
        const margin = 12;
        return {
            width: Math.min(1200, Math.max(720, window.innerWidth - margin * 2)),
            height: Math.min(940, Math.max(560, Math.floor(window.innerHeight * 0.9)))
        };
    }

    function applyInvoicePreviewPopupPdfUrl(blobUrl) {
        const frame = document.getElementById("invoicePreviewPopupFrame");
        if (!frame) {
            return;
        }
        const hash = "view=FitH&zoom=page-width";
        frame.src = blobUrl.indexOf("#") >= 0 ? blobUrl + "&" + hash : blobUrl + "#" + hash;
        frame.hidden = false;
        frame.removeAttribute("hidden");
    }

    function resizeInvoicePreviewViewer() {
        const $host = invoicePreviewPopupHost;
        if (!$host || !$host.length) {
            return;
        }
        let popup = null;
        try {
            popup = $host.dxPopup("instance");
        } catch {
            return;
        }
        if (!popup || !popup.option("visible")) {
            return;
        }
        const dims = invoicePreviewPopupDimensions();
        popup.option({ width: dims.width, height: dims.height });
        const $content = popup.content();
        const contentH = $content && $content.length ? $content.innerHeight() : dims.height - 56;
        const $body = $content.find(".invoice-preview-popup-body");
        if ($body.length) {
            $body.css({ height: Math.max(480, contentH), minHeight: Math.max(480, contentH) });
        }
        const frame = document.getElementById("invoicePreviewPopupFrame");
        if (frame) {
            frame.style.width = "100%";
            frame.style.height = Math.max(480, contentH) + "px";
        }
    }

    function ensureInvoicePreviewPopup() {
        if (invoicePreviewPopupHost) {
            return invoicePreviewPopupHost;
        }

        const initialDims = invoicePreviewPopupDimensions();
        const $host = $("<div>").attr("id", "invoicePdfPreviewPopup").appendTo(document.body);
        $host.dxPopup({
            title: t("reservationDetail.payments.invoice.preview", "Invoice preview"),
            visible: false,
            showTitle: true,
            showCloseButton: true,
            dragEnabled: true,
            resizeEnabled: true,
            hideOnOutsideClick: true,
            width: initialDims.width,
            height: initialDims.height,
            maxWidth: "98vw",
            maxHeight: "94vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup invoice-preview-popup" },
            contentTemplate: function (contentElement) {
                const $body = $("<div>").addClass("invoice-preview-popup-body").appendTo(contentElement);
                $("<div>")
                    .attr("id", "invoicePreviewPopupStatus")
                    .addClass("invoice-preview-popup-status")
                    .text(t("reservationDetail.payments.invoice.printLoading", "Loading invoice…"))
                    .appendTo($body);
                $("<iframe>")
                    .attr({ id: "invoicePreviewPopupFrame", title: "Invoice PDF" })
                    .addClass("invoice-preview-popup-frame")
                    .prop("hidden", true)
                    .appendTo($body);
            },
            onShowing: function () {
                const dims = invoicePreviewPopupDimensions();
                $host.dxPopup("instance").option({ width: dims.width, height: dims.height });
            },
            onShown: function () {
                window.setTimeout(resizeInvoicePreviewViewer, 0);
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("reservationDetail.payments.invoice.print", "Print"),
                        icon: "print",
                        type: "default",
                        stylingMode: "contained",
                        onClick: function () {
                            const frame = document.getElementById("invoicePreviewPopupFrame");
                            if (frame && frame.contentWindow) {
                                try {
                                    frame.contentWindow.print();
                                } catch {
                                    /* user can print from browser menu */
                                }
                            }
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.close", "Close"),
                        stylingMode: "outlined",
                        onClick: function () {
                            $host.dxPopup("instance").hide();
                        }
                    }
                }
            ],
            onHidden: function () {
                const blobUrl = $host.data("previewBlobUrl");
                if (blobUrl) {
                    try {
                        URL.revokeObjectURL(blobUrl);
                    } catch {
                        /* ignore */
                    }
                    $host.removeData("previewBlobUrl");
                }
                const frame = document.getElementById("invoicePreviewPopupFrame");
                if (frame) {
                    frame.src = "about:blank";
                    frame.hidden = true;
                }
                const status = document.getElementById("invoicePreviewPopupStatus");
                if (status) {
                    status.hidden = false;
                    status.textContent = t(
                        "reservationDetail.payments.invoice.printLoading",
                        "Loading invoice…"
                    );
                }
            }
        });

        invoicePreviewPopupHost = $host;
        return $host;
    }

    function openInvoicePreviewPopup(row) {
        const target = resolveInvoicePrintTarget(row);
        if (!target) {
            DevExpress.ui.notify(
                t("reservationDetail.payments.invoice.printFailed"),
                "error",
                3200
            );
            return;
        }

        const loadToken = ++invoicePreviewLoadToken;
        let $host;
        let popup;
        try {
            $host = ensureInvoicePreviewPopup();
            popup = $host.dxPopup("instance");
        } catch {
            DevExpress.ui.notify(
                t("reservationDetail.payments.invoice.printFailed"),
                "error",
                3200
            );
            return;
        }

        if (!popup) {
            DevExpress.ui.notify(
                t("reservationDetail.payments.invoice.printFailed"),
                "error",
                3200
            );
            return;
        }

        const dims = invoicePreviewPopupDimensions();
        const invoiceLabel =
            (row && (row.invoiceNo || row.number)) || String(target.id);
        popup.option({
            title:
                t("reservationDetail.payments.invoice.preview", "Invoice preview") +
                " — " +
                invoiceLabel,
            width: dims.width,
            height: dims.height
        });

        const statusEl = document.getElementById("invoicePreviewPopupStatus");
        const frame = document.getElementById("invoicePreviewPopupFrame");
        if (statusEl) {
            statusEl.hidden = false;
            statusEl.style.display = "flex";
            statusEl.textContent = t(
                "reservationDetail.payments.invoice.printLoading",
                "Loading invoice…"
            );
        }
        if (frame) {
            frame.hidden = true;
            frame.style.display = "none";
            frame.src = "about:blank";
        }

        try {
            popup.show();
        } catch {
            DevExpress.ui.notify(
                t("reservationDetail.payments.invoice.printFailed"),
                "error",
                3200
            );
            return;
        }

        loadInvoicePdfBlob(row)
            .then(function (blob) {
                if (loadToken !== invoicePreviewLoadToken) {
                    return;
                }
                const prevUrl = $host.data("previewBlobUrl");
                if (prevUrl) {
                    try {
                        URL.revokeObjectURL(prevUrl);
                    } catch {
                        /* ignore */
                    }
                }
                const url = URL.createObjectURL(blob);
                $host.data("previewBlobUrl", url);
                applyInvoicePreviewPopupPdfUrl(url);
                if (frame) {
                    frame.style.display = "block";
                }
                if (statusEl) {
                    statusEl.hidden = true;
                    statusEl.style.display = "none";
                }
                resizeInvoicePreviewViewer();
            })
            .catch(function () {
                if (loadToken !== invoicePreviewLoadToken) {
                    return;
                }
                if (statusEl) {
                    statusEl.hidden = false;
                    statusEl.textContent = t(
                        "reservationDetail.payments.invoice.printFailed",
                        "Could not load the invoice PDF."
                    );
                }
                DevExpress.ui.notify(
                    t("reservationDetail.payments.invoice.printFailed"),
                    "error",
                    3200
                );
            });
    }

    function openInvoiceReportViewer(row) {
        openInvoicePreviewPopup(row);
    }

    function printInvoicePdf(row) {
        openInvoicePreviewPopup(row);
    }

    function zatcaStatusBadgeClass(status) {
        const s = String(status || "pending").toLowerCase();
        if (s === "cleared" || s === "reported") {
            return "res-zatca-badge--success";
        }
        if (s === "failed") {
            return "res-zatca-badge--danger";
        }
        if (s === "pending") {
            return "res-zatca-badge--pending";
        }
        if (s === "skipped") {
            return "res-zatca-badge--muted";
        }
        return "res-zatca-badge--neutral";
    }

    function zatcaStatusCellTemplate(container, options) {
        const status = options.value ? String(options.value).toLowerCase() : "pending";
        const key = `reservationDetail.payments.zatca.status.${status}`;
        const label = t(key);
        const text = label && label !== key ? label : status;
        const $cell = $("<div>").addClass("res-zatca-status-cell").appendTo(container);
        $("<span>")
            .addClass("res-zatca-badge " + zatcaStatusBadgeClass(status))
            .text(text)
            .appendTo($cell);
    }

    function invoicePaymentGridColumns() {
        const mobile = isPaymentsMobileViewport();
        const cols = [
            {
                dataField: "invoiceNo",
                caption: t("reservationDetail.payments.invoice.invoiceNo"),
                minWidth: mobile ? 108 : 128
            },
            {
                dataField: "invoiceDate",
                caption: t("reservationDetail.payments.grid.date"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: mobile ? 100 : 110
            },
            {
                dataField: "totalAmount",
                caption: t("reservationDetail.payments.grid.amount"),
                dataType: "number",
                format: "#,##0.00",
                minWidth: mobile ? 96 : 110
            },
            {
                dataField: "periodFrom",
                caption: t("reservationDetail.payments.invoice.periodFrom"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: mobile ? 92 : 100
            },
            {
                dataField: "periodTo",
                caption: t("reservationDetail.payments.invoice.periodTo"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: mobile ? 92 : 100
            },
            {
                dataField: "zatcaStatus",
                caption: t("reservationDetail.payments.grid.status"),
                minWidth: mobile ? 96 : 112,
                alignment: "center",
                cssClass: "res-payment-grid-cell--center",
                cellTemplate: zatcaStatusCellTemplate
            },
            {
                dataField: "customerName",
                caption: t("reservationDetail.payments.grid.customer"),
                minWidth: mobile ? 120 : 150
            }
        ];

        const actionCol = {
            type: "buttons",
            caption: t("reservationDetail.payments.grid.actions"),
            width: mobile ? 228 : 288,
            minWidth: mobile ? 228 : 288,
            alignment: paymentGridActionsColumnAlignment(),
            cssClass: "res-payment-grid-actions-cell",
            buttons: [
                {
                    hint: t("reservationDetail.payments.invoice.preview"),
                    icon: "doc",
                    visible(e) {
                        return (
                            hasPmsPermission("finance.invoice.view") &&
                            e.row &&
                            e.row.data &&
                            hasInvoiceRouteId(e.row.data)
                        );
                    },
                    onClick(e) {
                        openInvoiceReportViewer(e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.invoice.print"),
                    icon: "print",
                    visible(e) {
                        return (
                            hasPmsPermission("finance.invoice.view") &&
                            e.row &&
                            e.row.data &&
                            hasInvoiceRouteId(e.row.data)
                        );
                    },
                    onClick(e) {
                        openInvoicePreviewPopup(e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.invoice.creditNote"),
                    icon: "undo",
                    visible(e) {
                        return (
                            hasPmsPermission("finance.credit_note.create") &&
                            e.row &&
                            e.row.data &&
                            isInvoiceZatcaSubmitted(e.row.data) &&
                            canAdjustInvoice(e.row.data)
                        );
                    },
                    onClick(e) {
                        openCreditNotePopup(e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.invoice.debitNote"),
                    icon: "redo",
                    visible(e) {
                        return (
                            hasPmsPermission("finance.debit_note.create") &&
                            e.row &&
                            e.row.data &&
                            isInvoiceZatcaSubmitted(e.row.data) &&
                            canAdjustInvoice(e.row.data)
                        );
                    },
                    onClick(e) {
                        openDebitNotePopup(e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.zatca.send"),
                    icon: "export",
                    visible(e) {
                        return (
                            e.row &&
                            e.row.data &&
                            hasPmsPermission("finance.invoice.send_zatca") &&
                            isZatcaSendableStatus(e.row.data.zatcaStatus)
                        );
                    },
                    onClick(e) {
                        sendZatcaFromInvoiceRow("invoice", e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.invoice.related"),
                    icon: "folder",
                    visible(e) {
                        const row = e.row && e.row.data;
                        if (!row) {
                            return false;
                        }
                        const totalCount =
                            row.relatedAdjustmentCount != null
                                ? Number(row.relatedAdjustmentCount)
                                : 0;
                        return (
                            totalCount > 0 &&
                            (hasPmsPermission("finance.credit_note.view") ||
                                hasPmsPermission("finance.debit_note.view"))
                        );
                    },
                    onClick(e) {
                        openInvoiceRelatedPopup(e.row && e.row.data);
                    }
                }
            ]
        };

        if (!mobile) {
            actionCol.fixed = true;
            actionCol.fixedPosition = reservationGridActionFixedPosition();
        }

        cols.push(actionCol);
        cols.push({ dataField: "invoiceId", visible: false });
        if (mobile) {
            return pickResDetailMobileColumns(cols, paymentGridMobileDataKeys("invoices"), 3);
        }

        return cols;
    }

    function paymentGridColCenter(col) {
        if (!col || col.type === "buttons") {
            return col;
        }

        return Object.assign({}, col, {
            alignment: "center",
            cssClass: "res-payment-grid-cell--center"
        });
    }

    function paymentGridWhatsappButton(kind) {
        return {
            hint: t("reservationDetail.payments.grid.whatsapp"),
            cssClass: "res-payment-grid-whatsapp-btn",
            text: "",
            visible() {
                return kind === "receipts";
            },
            onClick(e) {
                notifyPaymentRowAction("whatsapp", kind, e.row && e.row.data);
            }
        };
    }

    function creditNotePaymentGridColumns() {
        const mobile = isPaymentsMobileViewport();
        return [
            {
                dataField: "creditNoteNo",
                caption: t("reservationDetail.payments.grid.number"),
                minWidth: mobile ? 110 : 130
            },
            {
                dataField: "creditNoteDate",
                caption: t("reservationDetail.payments.grid.date"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: mobile ? 100 : 110
            },
            {
                dataField: "creditAmount",
                caption: t("reservationDetail.payments.grid.amount"),
                dataType: "number",
                format: "#,##0.00",
                minWidth: mobile ? 100 : 110
            },
            {
                dataField: "invoiceNo",
                caption: t("reservationDetail.payments.creditNote.invoiceNo"),
                minWidth: mobile ? 110 : 130
            },
            {
                dataField: "zatcaStatus",
                caption: t("reservationDetail.payments.grid.status"),
                minWidth: mobile ? 96 : 112,
                alignment: "center",
                cssClass: "res-payment-grid-cell--center",
                cellTemplate: zatcaStatusCellTemplate
            },
            {
                dataField: "reason",
                caption: t("reservationDetail.payments.invoice.reason"),
                minWidth: mobile ? 140 : 180
            },
            {
                type: "buttons",
                caption: t("reservationDetail.payments.grid.actions"),
                width: mobile ? 56 : 72,
                alignment: paymentGridActionsColumnAlignment(),
                cssClass: "res-payment-grid-actions-cell",
                buttons: [
                    {
                        hint: t("reservationDetail.payments.zatca.send"),
                        icon: "export",
                        visible(e) {
                            const row = e.row && e.row.data;
                            return (
                                !!row &&
                                hasPmsPermission("finance.credit_note.send_zatca") &&
                                isZatcaSendableStatus(row.zatcaStatus)
                            );
                        },
                        onClick(e) {
                            sendZatcaFromInvoiceRow("credit_note", e.row && e.row.data);
                        }
                    }
                ]
            }
        ];
    }

    function paymentGridColumns(kind) {
        if (kind === "credit_notes") {
            return creditNotePaymentGridColumns();
        }

        if (kind === "invoices") {
            return invoicePaymentGridColumns();
        }

        const baseCols =
            kind === "receipts" || kind === "disbursements"
                ? paymentVoucherGridColumns(kind)
                : [
            {
                dataField: "number",
                caption: t("reservationDetail.payments.grid.number"),
                minWidth: 110
            },
            {
                dataField: "date",
                caption: t("reservationDetail.payments.grid.date"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: 110
            },
            {
                dataField: "amount",
                caption: t("reservationDetail.payments.grid.amount"),
                dataType: "number",
                format: "#,##0.00",
                minWidth: 110
            },
            {
                dataField: "status",
                caption: t("reservationDetail.payments.grid.status"),
                minWidth: 110,
                customizeText(e) {
                    const key = e.value ? `reservationDetail.payments.status.${e.value}` : "";
                    const localized = key ? t(key) : "";
                    return localized && localized !== key ? localized : e.value || "";
                }
            },
            {
                dataField: "notes",
                caption: t("reservationDetail.payments.grid.notes"),
                minWidth: 180
            }
        ];

        if (kind === "promissory") {
            const statusCol = baseCols.find((c) => c.dataField === "status");
            if (statusCol) {
                delete statusCol.customizeText;
                statusCol.cellTemplate = promissoryStatusCellTemplate;
                statusCol.alignment = "center";
                statusCol.minWidth = 100;
            }
            baseCols.splice(3, 0, {
                dataField: "maturityDate",
                caption: t("reservationDetail.payments.grid.maturityDate"),
                dataType: "date",
                format: "dd/MM/yyyy",
                minWidth: 120,
                alignment: "center",
                cssClass: "res-payment-grid-cell--center"
            });
            baseCols.splice(4, 0, {
                dataField: "dueAmount",
                caption: t("reservationDetail.payments.grid.dueAmount"),
                dataType: "number",
                format: "#,##0.00",
                minWidth: 110,
                alignment: "center",
                cssClass: "res-payment-grid-cell--center"
            });
            baseCols.splice(5, 0, {
                dataField: "collectionReceiptNo",
                caption: t("reservationDetail.payments.grid.collectionReceiptNo"),
                minWidth: 120,
                alignment: "center",
                cssClass: "res-payment-grid-cell--center",
                customizeText(e) {
                    return e.value ? String(e.value) : "—";
                }
            });
            for (let i = 0; i < baseCols.length; i += 1) {
                if (baseCols[i].type !== "buttons") {
                    baseCols[i] = paymentGridColCenter(baseCols[i]);
                }
            }
        }

        const mobile = isPaymentsMobileViewport();
        const isVoucherGrid = kind === "receipts" || kind === "disbursements";
        const actionCol = {
            type: "buttons",
            caption: t("reservationDetail.payments.grid.actions"),
            width: mobile ? (isVoucherGrid ? 124 : 128) : isVoucherGrid ? 152 : 232,
            minWidth: mobile ? (isVoucherGrid ? 124 : 128) : isVoucherGrid ? 152 : 232,
            alignment: paymentGridActionsColumnAlignment(),
            cssClass: "res-payment-grid-actions-cell",
            buttons: [
                {
                    hint: t("reservationDetail.payments.grid.collect"),
                    icon: "money",
                    visible(e) {
                        return (
                            kind === "promissory" &&
                            hasPmsPermission("payments.create") &&
                            e.row &&
                            e.row.data &&
                            isPromissoryRowCollectible(e.row.data)
                        );
                    },
                    onClick(e) {
                        notifyPaymentRowAction("collect", kind, e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.grid.view"),
                    icon: "eyeopen",
                    visible(e) {
                        if (kind !== "receipts" && kind !== "disbursements") {
                            return false;
                        }

                        const canEdit = canEditPaymentVoucherByKind(kind);
                        const canView = kind === "disbursements"
                            ? canViewPaymentRefundVoucher()
                            : canViewPaymentReceiptVoucher();
                        return (
                            !canEdit &&
                            canView &&
                            e.row &&
                            e.row.data &&
                            e.row.data.zaaerId
                        );
                    },
                    onClick(e) {
                        notifyPaymentRowAction("view", kind, e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.grid.edit"),
                    icon: "edit",
                    visible(e) {
                        if (kind === "promissory") {
                            return (
                                canEditPaymentVoucherByKind(kind) &&
                                e.row &&
                                e.row.data &&
                                !isPromissoryRowCancelled(e.row.data) &&
                                String(e.row.data.status || "").toLowerCase() !== "collected"
                            );
                        }

                        return (
                            canEditPaymentVoucherByKind(kind) &&
                            (kind === "receipts" || kind === "disbursements") &&
                            e.row &&
                            e.row.data &&
                            !isPaymentRowCancelled(e.row.data)
                        );
                    },
                    onClick(e) {
                        notifyPaymentRowAction("edit", kind, e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.grid.delete"),
                    icon: "trash",
                    visible(e) {
                        if (kind === "promissory") {
                            return (
                                canCancelPaymentVoucher(kind) &&
                                e.row &&
                                e.row.data &&
                                !isPromissoryRowCancelled(e.row.data) &&
                                String(e.row.data.status || "").toLowerCase() !== "collected"
                            );
                        }

                        return (
                            canCancelPaymentVoucher(kind) &&
                            (kind === "receipts" || kind === "disbursements") &&
                            e.row &&
                            e.row.data &&
                            !isPaymentRowCancelled(e.row.data)
                        );
                    },
                    onClick(e) {
                        notifyPaymentRowAction("delete", kind, e.row && e.row.data);
                    }
                },
                {
                    hint: t("reservationDetail.payments.grid.print"),
                    icon: "print",
                    visible() {
                        return isVoucherGrid || kind === "invoices";
                    },
                    onClick(e) {
                        notifyPaymentRowAction("print", kind, e.row && e.row.data);
                    }
                },
                paymentGridWhatsappButton(kind)
            ]
        };
        if (!mobile) {
            actionCol.fixed = true;
            actionCol.fixedPosition = reservationGridActionFixedPosition();
        }
        baseCols.push(actionCol);

        if (mobile && !isVoucherGrid) {
            return pickResDetailMobileColumns(baseCols, paymentGridMobileDataKeys(kind), 3);
        }

        return baseCols;
    }

    function notifyPaymentRowAction(action, kind, row) {
        if (action === "collect" && kind === "promissory" && row) {
            openPaymentReceiptPopup({
                promissoryCollection: {
                    zaaerId: row.zaaerId,
                    dueAmount: Number(row.dueAmount != null ? row.dueAmount : row.amount) || 0,
                    promissoryNo: row.promissoryNo || row.number || ""
                }
            });
            return;
        }

        if (action === "view" && kind === "receipts" && row && row.zaaerId) {
            openPaymentReceiptPopup({ editRow: row, readOnly: true });
            return;
        }

        if (action === "view" && kind === "disbursements" && row && row.zaaerId) {
            openPaymentDisbursementPopup({ editRow: row, readOnly: true });
            return;
        }

        if (action === "edit" && kind === "promissory" && row && row.zaaerId) {
            openPromissoryNotePopup({ editRow: row });
            return;
        }

        if (action === "edit" && kind === "receipts" && row && row.zaaerId) {
            openPaymentReceiptPopup({ editRow: row });
            return;
        }

        if (action === "edit" && kind === "disbursements" && row && row.zaaerId) {
            openPaymentDisbursementPopup({ editRow: row });
            return;
        }

        if (
            action === "edit" &&
            (kind === "receipts" || kind === "disbursements") &&
            row &&
            !row.zaaerId
        ) {
            DevExpress.ui.notify(
                t("reservationDetail.payments.receipt.missingZaaerId"),
                "warning",
                3200
            );
            return;
        }

        if (
            action === "delete" &&
            kind === "promissory" &&
            row
        ) {
            openPromissoryNoteCancelPopup(row);
            return;
        }

        if (action === "delete" && kind === "invoices" && row) {
            DevExpress.ui.notify(
                t("reservationDetail.payments.invoice.cancelPending").replace(
                    "{number}",
                    row.invoiceNo || row.number || ""
                ),
                "info",
                3200
            );
            return;
        }

        if (
            action === "delete" &&
            (kind === "receipts" || kind === "disbursements") &&
            row
        ) {
            openPaymentReceiptCancelPopup(row, kind);
            return;
        }

        const number = row && (row.receiptNo || row.number) ? String(row.receiptNo || row.number) : "";
        DevExpress.ui.notify(
            t(`reservationDetail.payments.rowAction.${action}`).replace("{number}", number),
            action === "delete" ? "warning" : "info",
            2600
        );
    }

    function initPaymentGrid($host, tab) {
        const kind = tab.id;
        const mobile = isPaymentsMobileViewport();
        const isVoucherGrid = kind === "receipts" || kind === "disbursements";

        const po = window.Zaaer.PmsGridOptions;
        $host.dxDataGrid(
            po.merge(po.baseline(), {
            elementAttr: { class: "pms-grid-compact" },
            dataSource: new DevExpress.data.CustomStore({
                key: "id",
                load() {
                    return loadReservationPaymentRows(kind).then((rows) => {
                        const list = Array.isArray(rows) ? rows : [];
                        pageCtx.paymentTabCounts = pageCtx.paymentTabCounts || {};
                        if (pageCtx.paymentTabCounts[kind] !== list.length) {
                            pageCtx.paymentTabCounts[kind] = list.length;
                            updatePaymentTabBadgesFromCounts();
                        }
                        syncPaymentRowsCache(kind, list);
                        return list;
                    });
                }
            }),
            remoteOperations: false,
            repaintChangesOnly: true,
            cacheEnabled: true,
            rowAlternationEnabled: false,
            hoverStateEnabled: true,
            scrolling: po.scrollingOptions(
                isVoucherGrid
                    ? {
                          mode: "standard",
                          rowRenderingMode: "standard",
                          columnRenderingMode: "standard",
                          useNative: false,
                          scrollByContent: true,
                          showScrollbar: "always"
                      }
                    : mobile
                      ? { mode: "standard", rowRenderingMode: "standard", useNative: false }
                      : { mode: "standard", rowRenderingMode: "standard" }
            ),
            paging: {
                enabled: isVoucherGrid,
                pageSize: isVoucherGrid ? 50 : 20
            },
            pager: {
                visible: isVoucherGrid,
                showInfo: true,
                showNavigationButtons: true,
                allowedPageSizes: [25, 50, 100, 200]
            },
            loadPanel: {
                enabled: true,
                showIndicator: true,
                shading: false
            },
            showBorders: true,
            columnAutoWidth: mobile ? false : !isVoucherGrid,
            columnFixing: { enabled: !mobile },
            columnMinWidth: mobile ? 72 : 64,
            wordWrapEnabled: isVoucherGrid ? false : !mobile,
            rtlEnabled: isArabic(),
            noDataText: t(tab.emptyKey),
            columnHidingEnabled: false,
            allowColumnResizing: !mobile,
            allowColumnReordering: !mobile,
            headerFilter: { visible: !mobile, search: { enabled: true } },
            searchPanel: {
                visible: true,
                width: mobile ? "100%" : 260,
                placeholder: t("reservationDetail.payments.grid.search")
            },
            export: {
                enabled: !mobile,
                allowExportSelectedData: false
            },
            columnChooser: {
                enabled: !mobile
            },
            toolbar: {
                items: [
                    "searchPanel",
                    {
                        name: "columnChooserButton",
                        locateInMenu: "auto"
                    },
                    {
                        name: "exportButton",
                        locateInMenu: "auto"
                    },
                    {
                        widget: "dxButton",
                        location: "after",
                        locateInMenu: "auto",
                        options: {
                            icon: "refresh",
                            hint: t("reservationDetail.payments.grid.refresh"),
                            stylingMode: "text",
                            onClick() {
                                $host.dxDataGrid("instance").refresh();
                            }
                        }
                    }
                ]
            },
            columns: paymentGridColumns(kind)
            })
        );
    }

    function renderPaymentGridTab(item, _index, element) {
        const tabId = item && item.id ? item.id : "receipts";
        const $root = $(element).empty().addClass("res-payment-tab-pane");
        if (tabId === "invoices") {
            mountZatcaDecryptBannerIfNeeded($root);
        }
        const $gridHost = $("<div>")
            .addClass("res-payment-grid")
            .toggleClass("res-payment-grid--promissory", tabId === "promissory")
            .toggleClass("res-payment-grid--invoices", tabId === "invoices")
            .toggleClass("res-payment-grid--credit-notes", tabId === "credit_notes")
            .toggleClass("res-payment-grid--vouchers", tabId === "receipts" || tabId === "disbursements")
            .data("paymentKind", tabId)
            .appendTo($root);

        try {
            const existing = $gridHost.dxDataGrid("instance");
            if (existing) {
                existing.dispose();
            }
        } catch {
            /* not initialized */
        }

        try {
            initPaymentGrid($gridHost, item);
        } catch (err) {
            console.error("initPaymentGrid failed for tab:", tabId, err);
            $gridHost.remove();
            $("<div>")
                .addClass("res-payment-grid-fallback")
                .text(t("reservationDetail.payments.grid.loadFailed") || "Failed to load grid.")
                .appendTo($root);
        }
    }

    function ensurePaymentGridForTab(tabPanelInst, item, index) {
        if (!tabPanelInst || !item) {
            return;
        }

        if (item.id === "invoices") {
            scheduleWarmInvoicePreviewPopup();
            probeCreditNotesTabOnDemand();
        }

        const idx = typeof index === "number" ? index : tabPanelInst.option("selectedIndex");
        const $pane = $(tabPanelInst.itemElements().eq(idx));
        if (!$pane.length) {
            return;
        }

        if ($pane.find(".res-payment-grid").length) {
            return;
        }

        renderPaymentGridTab(item, idx, $pane.get(0));
    }

    function renderPaymentsWorkspace($root) {
        if ($root.data("paymentsWorkspaceReady")) {
            return;
        }

        $root.data("paymentsWorkspaceReady", true).addClass("res-payments-workspace");
        scheduleWarmInvoicePreviewPopup();

        const $toolbar = $("<div>").addClass("res-payments-toolbar").appendTo($root);

        $("<div>").attr("id", "resPaymentsActions").addClass("res-payments-actions").appendTo($toolbar).dxDropDownButton({
            text: t("reservationDetail.payments.actions.menu"),
            icon: "plus",
            type: "default",
            stylingMode: "contained",
            disabled: reservationGridsActionsDisabled(),
            items: paymentActionItems(),
            keyExpr: "id",
            displayExpr: "text",
            showArrowIcon: true,
            rtlEnabled: isArabic(),
            dropDownOptions: {
                width: isPaymentsMobileViewport()
                    ? Math.min(320, window.innerWidth - 24)
                    : 260,
                wrapperAttr: { class: "res-payments-actions-popup" }
            },
            onItemClick(e) {
                notifyPaymentAction(e.itemData && e.itemData.id);
            }
        });

        $("<div>").addClass("res-payments-body").appendTo($root);

        $("<div>").addClass("res-payment-tabpanel-host").appendTo($root.find(".res-payments-body")).dxTabPanel({
            items: getPaymentTabItemsWithCounts(),
            keyExpr: "id",
            selectedIndex: 0,
            deferRendering: true,
            animationEnabled: false,
            repaintChangesOnly: false,
            scrollingEnabled: true,
            scrollByContent: true,
            showNavButtons: true,
            stylingMode: "secondary",
            iconPosition: "top",
            tabsPosition: "top",
            swipeEnabled: isPaymentsMobileViewport(),
            rtlEnabled: isArabic(),
            elementAttr: { class: "res-payment-record-tabs" },
            itemTitleTemplate: renderPaymentTabTitle,
            itemTemplate: renderPaymentGridTab,
            onContentReady(e) {
                const inst = e.component;
                const idx = inst.option("selectedIndex");
                const item = inst.option("selectedItem");
                ensurePaymentGridForTab(inst, item, idx);
                updatePaymentTabBadgesFromCounts();
            },
            onSelectionChanged(e) {
                const item = e.addedItems && e.addedItems[0];
                if (!item) {
                    return;
                }
                const inst = e.component;
                const idx = inst.option("selectedIndex");
                if (item.id === "invoices") {
                    probeCreditNotesTabOnDemand();
                }
                ensurePaymentGridForTab(inst, item, idx);
            }
        });

        $("<div>")
            .attr("id", "resPaymentsSummary")
            .addClass("res-payments-summary")
            .appendTo($root.find(".res-payments-body"));

        renderPaymentsSummaryBar();

        const $tabPanelHost = $root.find(".res-payment-tabpanel-host");
        window.requestAnimationFrame(() => {
            try {
                const inst = $tabPanelHost.dxTabPanel("instance");
                const idx = inst.option("selectedIndex");
                ensurePaymentGridForTab(inst, inst.option("selectedItem"), idx);
                updatePaymentTabBadgesFromCounts();
            } catch {
                /* TabPanel not ready */
            }
        });
    }

    function renderReservationPageTab(item, _index, element) {
        const tabId = item && item.id ? item.id : "details";
        const $root = $(element).empty();

        if (tabId === "payments") {
            renderPaymentsWorkspace($root);
            refreshPaymentTabCounts();
            return;
        }

        $root.addClass("res-detail-tab-pane");
        buildReservationDetailSections($root);
    }

    function initReservationPageTabPanel($host) {
        $host.dxTabPanel({
            items: reservationPageTabItems(),
            keyExpr: "id",
            selectedIndex: 0,
            deferRendering: true,
            animationEnabled: false,
            repaintChangesOnly: true,
            scrollingEnabled: true,
            scrollByContent: true,
            showNavButtons: false,
            stylingMode: "primary",
            iconPosition: "start",
            tabsPosition: "top",
            swipeEnabled: false,
            rtlEnabled: isArabic(),
            elementAttr: { class: "res-page-tabs" },
            itemTitleTemplate: renderReservationPageTabTitle,
            itemTemplate: renderReservationPageTab,
            onSelectionChanged(e) {
                const added = e.addedItems && e.addedItems[0];
                if (added && added.id === "details") {
                    renderReservationPeriodsUi(pageCtx.detail);
                }
            }
        });
    }

    function buildReservationDetailSections($main) {
        pageCtx.financialTabsLoaded = {};

        const headerInner = $("<div>").addClass("res-kv-grid res-kv-row-5 res-kv-row-header").append(
            kvSpan(t("reservationDetail.reservationNo"), "resHeaderNo").addClass("res-kv--reservation-no"),
            kvSpan(t("reservationDetail.source"), "resHeaderSource").addClass("res-kv--hidden-field"),
            (() => {
                const $mainGuestKv = kvSpan(t("reservationDetail.mainGuest"), "resHeaderGuest").addClass(
                    "res-kv--main-guest"
                );
                $mainGuestKv
                    .find(".res-v")
                    .addClass("res-kv-main-guest-row")
                    .append($("<span>").attr("id", "resHeaderGuestEdit").addClass("res-kv-main-guest-edit-host"));
                return $mainGuestKv;
            })(),
            kvSpan(t("reservationDetail.actualArrival"), "resHeaderArrival").addClass("res-kv--hidden-field"),
            kvSpan(t("reservationDetail.actualDeparture"), "resHeaderDeparture")
                .attr("id", "resKvDepartureWrap")
                .addClass("res-kv--departure")
                .hide()
        );

        $main.append(sectionShell("res-section-header", "reservationDetail.section.header", headerInner));

        const $lodgingPartyCards = $("<div/>")
            .attr("id", "resLodgingPartyCards")
            .addClass("res-lodging-party-cards")
            .append(
                $("<div/>")
                    .addClass("res-lodging-party-row res-lodging-party-row--primary")
                    .append(buildLodgingPartyCardShell("guest"), buildLodgingPartyCardShell("companions")),
                $("<div/>")
                    .addClass("res-lodging-party-row res-lodging-party-row--company")
                    .append(buildLodgingPartyCardShell("company"))
            );
        $("#res-section-header .res-section-body").append($lodgingPartyCards);

        const $generalHeadArrival = $("<div>")
            .addClass("res-section-head-switch res-general-head-arrival")
            .append(
                $("<span>")
                    .attr("id", "resGuestArrivalLabel")
                    .addClass("res-section-head-switch-label"),
                $("<div>").attr("id", "resGeneralArrival")
            );

        const generalInner = $("<div>")
            .addClass("res-form-grid res-form-grid--general")
            .append(
                $("<div>").attr("id", "resGeneralStatus"),
                $("<div>").attr("id", "resGeneralKind"),
                $("<div>").attr("id", "resGeneralPurpose"),
                $("<div>").attr("id", "resGeneralSource"),
                $("<div>").attr("id", "resCmBookingNo")
            );
        $main.append(
            sectionShell("res-section-general", "reservationDetail.section.general", generalInner, $generalHeadArrival)
        );

        const $datesHeadAuto = $("<div>")
            .addClass("res-section-head-switch res-dates-head-auto")
            .append(
                $("<span>").addClass("res-section-head-switch-label").text(t("reservationDetail.autoExtend")),
                $("<div>").attr("id", "resAutoExtend")
            );

        const $datesHeadTogglesSlot = $("<div>").attr("id", "res-dates-head-toggles-slot").addClass("res-dates-head-toggles");

        const $datesHeadEnd = $("<div>")
            .addClass("res-dates-section-head-end")
            .append(
                $datesHeadTogglesSlot,
                $("<div>").attr("id", "btnAppendRentalPeriod").addClass("res-periods-head-action"),
                $datesHeadAuto
            );

        const $rentalCell = $("<div>")
            .addClass("res-date-cell res-date-cell--rental")
            .append(
                $("<div>").addClass("res-field-label").text(t("reservationDetail.rentalType")),
                $("<div>").attr("id", "resRentalGroup")
            );

        const $calendarCell = $("<div>")
            .addClass("res-date-cell res-date-cell--calendar")
            .attr("id", "res-date-cell-calendar")
            .append(
                $("<div>").addClass("res-field-label").text(t("reservationDetail.calendarType")),
                $("<div>").attr("id", "resCalendarGroup")
            );

        const $dateCoreTogglesSlot = $("<div>")
            .attr("id", "res-date-core-toggles-slot")
            .addClass("res-date-core-toggles")
            .append($rentalCell, $calendarCell);

        const $arrivalGroup = $("<div>")
            .addClass("res-date-stay-group res-date-stay-group--arrival")
            .append(
                $("<div>")
                    .addClass("res-date-cell res-date-cell--date")
                    .append($("<div>").attr("id", "resCheckInDate")),
                $("<div>")
                    .addClass("res-date-cell res-date-cell--hijri")
                    .attr("id", "res-date-cell-hijri")
                    .append($("<div>").attr("id", "resEventDateHijri")),
                $("<div>")
                    .addClass("res-date-cell res-date-cell--time")
                    .append($("<div>").attr("id", "resCheckInTime"))
            );

        const $nightsCell = $("<div>")
            .addClass("res-date-cell res-date-cell--duration res-date-cell--nights")
            .attr("id", "res-date-cell-nights")
            .append($("<div>").attr("id", "resNights"));

        const $monthsCell = $("<div>")
            .addClass("res-date-cell res-date-cell--duration res-date-cell--months")
            .attr("id", "res-date-cell-months")
            .append($("<div>").attr("id", "resMonths"));

        const $departureGroup = $("<div>")
            .addClass("res-date-stay-group res-date-stay-group--departure")
            .append(
                $("<div>")
                    .addClass("res-date-cell res-date-cell--date")
                    .append($("<div>").attr("id", "resCheckOutDate")),
                $("<div>")
                    .addClass("res-date-cell res-date-cell--time")
                    .append($("<div>").attr("id", "resCheckOutTime")),
                $nightsCell,
                $monthsCell
            );

        const $dateCore = $("<div>")
            .addClass("res-date-core")
            .append($dateCoreTogglesSlot, $arrivalGroup, $departureGroup);

        const $dateCoreScroll = $("<div>").addClass("res-date-core-scroll").append($dateCore);

        const $periodsToolbar = $("<div>")
            .addClass("res-periods-toolbar")
            .append($("<span>").attr("id", "resPeriodsBadge").addClass("res-periods-badge"));

        const $periodsWrap = $("<div>")
            .attr("id", "res-periods-wrap")
            .addClass("res-periods-wrap res-periods-wrap--hidden")
            .append($periodsToolbar, $("<div>").attr("id", "resPeriodsGrid"));

        const datesInner = $("<div>")
            .addClass("res-date-row res-dates-stack")
            .append($dateCoreScroll, $periodsWrap);
        $main.append(sectionShell("res-section-dates", "reservationDetail.section.dates", datesInner, $datesHeadEnd));

        const $resFinSummary = $("<div>").attr("id", "resFinGrid").addClass("res-fin-inline-summary");
        const $resDiscountsWrap = $("<div>")
            .attr("id", "resDiscountsWrap")
            .addClass("res-discounts-wrap res-discounts-wrap--hidden");
        const $finHeadBtn = $("<div>").attr("id", "btnUnitPricing").addClass("res-fin-head-action-btn");
        const $finHeadSummaryRow = $("<div>").addClass("res-fin-head-summary-row").append($resFinSummary);
        const $finHeadStack = $("<div>")
            .addClass("res-fin-head-stack")
            .append($finHeadSummaryRow, $resDiscountsWrap);
        const $finHeadEnd = $("<div>").addClass("res-fin-section-head-end").append($finHeadBtn, $finHeadStack);
        const finInner = $("<div>").addClass("res-fin-body-empty");
        $main.append(sectionShell("res-section-financial", "reservationDetail.section.financial", finInner, $finHeadEnd));

        const $addUnitHead = $("<div>").attr("id", "btnAddUnit");
        const unitsWrap = $("<div>").attr("id", "unitsGrid").addClass("res-section-body-grid pms-grid-compact");
        $main.append(sectionShell("res-section-units", "reservationDetail.section.units", unitsWrap, $addUnitHead));

        const $pickCompanyHead = $("<div>").attr("id", "btnPickCompany");
        const companyWrap = $("<div>")
            .attr("id", "companyGrid")
            .addClass("res-section-body-grid");
        $main.append(
            sectionShell("res-section-company", "reservationDetail.section.company", companyWrap, $pickCompanyHead)
        );

        const $addGuestHead = $("<div>").attr("id", "btnAddGuest");
        const guestsWrap = $("<div>").addClass("res-guests-section-body").append(
            $("<div>")
                .attr("id", "guestsGridShell")
                .addClass("guests-grid-shell guests-grid-shell--hidden res-section-body-grid")
                .append($("<div>").attr("id", "guestsGrid").addClass("pms-grid-compact")),
            $("<div>").addClass("res-guests-companions").append(
                $("<div>")
                    .addClass("res-companions-head")
                    .append(
                        $("<div>").addClass("res-field-label res-companions-heading").text(t("reservationDetail.guest.sectionCompanions")),
                        $("<div>").attr("id", "btnAddCompanion")
                    ),
                $("<div>")
                    .attr("id", "companionsGridShell")
                    .addClass("companions-grid-shell companions-grid-shell--hidden")
                    .append($("<div>").attr("id", "companionsGrid").addClass("companions-grid-wrap")),
                $("<div>")
                    .addClass("res-guests-extras")
                    .attr("id", "resGuestsExtrasRoot")
                    .append(
                    $("<div>")
                        .addClass("res-extras-head")
                        .append(
                            $("<div>").addClass("res-field-label res-extras-heading").text(t("reservationDetail.extras.sectionTitle")),
                            $("<div>").attr("id", "btnAddExtra")
                        ),
                    $("<div>")
                        .attr("id", "extrasGridShell")
                        .addClass("extras-grid-shell")
                        .append($("<div>").attr("id", "extrasGrid").addClass("extras-grid-wrap"))
                )
            )
        );
        $main.append(sectionShell("res-section-guests", "reservationDetail.section.guests", guestsWrap, $addGuestHead));
    }

    function buildStaticLayout($main) {
        $("<div>").attr("id", "resPageTabPanel").addClass("res-page-tabpanel-host").appendTo($main).each(function () {
            initReservationPageTabPanel($(this));
        });
    }

    function initEditorsAfterLayout() {
        $("#resGeneralArrival").dxSwitch({
            value: true,
            switchedOnText: t("reservationDetail.yes"),
            switchedOffText: t("reservationDetail.no"),
            onValueChanged(e) {
                if (pageCtx._suppressArrivalSwitchEvent) {
                    return;
                }

                syncGuestArrivalSwitchLabel();
                applyGuestArrivalSwitchSideEffects(!!e.value);
                refreshReservationPermissionUi({ force: true });
            }
        });
        syncGuestArrivalSwitchLabel();

        $("#resGeneralStatus").dxSelectBox({
            label: t("reservationDetail.status"),
            labelMode: "floating",
            items: [
                { id: "confirmed", text: t("reservationDetail.status.confirmed") },
                { id: "unconfirmed", text: t("reservationDetail.status.unconfirmed") }
            ],
            valueExpr: "id",
            displayExpr: "text",
            value: "confirmed",
            openOnFieldClick: true
        });

        $("#resGeneralKind").dxSelectBox({
            label: t("reservationDetail.reservationType"),
            labelMode: "floating",
            items: [
                { id: "individual", text: t("reservationDetail.kind.individual") },
                { id: "company", text: t("reservationDetail.kind.company") }
            ],
            valueExpr: "id",
            displayExpr: "text",
            onValueChanged(e) {
                setCompanySectionVisible(e.value === "company");
            }
        });

        $("#res-section-company").hide();

        $("#resGeneralPurpose").dxSelectBox({
            label: t("reservationDetail.visitPurpose"),
            labelMode: "floating",
            dataSource: pageCtx.purposes,
            valueExpr: "id",
            displayExpr: (item) => {
                if (!item) {
                    return "";
                }

                return isArabic() ? item.nameAr || item.name || "" : item.name || item.nameAr || "";
            },
            searchEnabled: true,
            showClearButton: true
        });

        $("#resGeneralSource").dxSelectBox({
            label: t("reservationDetail.source"),
            labelMode: "floating",
            dataSource: pageCtx.sources,
            valueExpr: "code",
            displayExpr: (item) => {
                if (!item) {
                    return "";
                }

                return isArabic() ? item.nameAr || item.name || "" : item.name || item.nameAr || "";
            },
            searchEnabled: true,
            searchExpr: ["code", "name", "nameAr"],
            openOnFieldClick: true,
            acceptCustomValue: true,
            onValueChanged(e) {
                syncCmBookingVisibility(e.value);
            }
        });

        $("#resCmBookingNo").dxTextBox({
            label: t("reservationDetail.cmBooking"),
            labelMode: "floating",
            maxLength: 120
        });
        syncCmBookingVisibility($("#resGeneralSource").dxSelectBox("instance").option("value"));

        $("#resRentalGroup").dxButtonGroup({
            items: [
                { text: t("reservationDetail.rental.daily"), key: "Daily" },
                { text: t("reservationDetail.rental.monthly"), key: "Monthly" }
            ],
            keyExpr: "key",
            stylingMode: "outlined",
            selectedItemKeys: ["Daily"],
            selectionMode: "single",
            onSelectionChanged() {
                const prevMode = pageCtx._pricingRentalMode;
                applyRentalDurationVisibility();
                const mode = getSelectedRentalKey();
                const modeChanged = prevMode !== mode;
                if (modeChanged) {
                    pageCtx.pricingRateByLineKey = {};
                }

                pageCtx._pricingRentalMode = mode;
                const ci = getReservationCheckInCombined();
                if (ci) {
                    suppressDateDurationSync = true;
                    try {
                        if (mode === "Monthly") {
                            $("#resMonths").dxNumberBox("instance").option("value", 1);
                            syncMonthlyCalendarControlFromEffectiveMode(MONTHLY_CALENDAR_THIRTY_DAY);
                            setReservationCheckOutFromDateTime(defaultCheckOutFromCheckInAndMonthsByMode(ci, 1));
                        } else {
                            $("#resNights").dxNumberBox("instance").option("value", 1);
                            setReservationCheckOutFromDateTime(defaultCheckOutFromCheckInAndNights(ci, 1));
                        }
                    } finally {
                        suppressDateDurationSync = false;
                    }
                }

                syncDurationFieldsFromDates({ flash: true, skipFinancialRecompute: true });

                // Re-fetch daily vs monthly gross from room_type_rates for all units on the reservation.
                applySuggestedGrossRatesFromPickerToUnits({ rentalMode: mode, clearPricingMap: false }).catch(
                    () => {
                        if (modeChanged) {
                            onReservationStayDatesChanged();
                        }
                    }
                );
            }
        });

        $("#resCalendarGroup").dxButtonGroup({
            items: [
                { text: t("reservationDetail.calendar.thirtyDay"), key: MONTHLY_CALENDAR_THIRTY_DAY },
                { text: t("reservationDetail.calendar.actual"), key: MONTHLY_CALENDAR_ACTUAL }
            ],
            keyExpr: "key",
            stylingMode: "outlined",
            selectedItemKeys: [MONTHLY_CALENDAR_THIRTY_DAY],
            selectionMode: "single",
            onSelectionChanged() {
                if (!isMonthlyRentalMode()) {
                    return;
                }

                pageCtx._monthlyCalendarMode = resolveEffectiveMonthlyCalendarMode(getSelectedMonthlyCalendarKey());
                const ci = getReservationCheckInCombined();
                const months = $("#resMonths").dxNumberBox("instance").option("value");
                if (ci && months !== undefined && months !== null) {
                    applyCheckOutFromMonths();
                    return;
                }

                syncDurationFieldsFromDates({ flash: true });
            }
        });
        syncMonthlyCalendarControlFromEffectiveMode();
        applyMonthlyCalendarVisibility();
        syncDatesTogglesPlacement();
        bindDatesSectionLayoutObserver();

        initAppendRentalPeriodButtonIfNeeded();
        initReservationPeriodsGrid();

        $("#resCheckInDate").dxDateBox({
            label: t("reservationDetail.checkInDate"),
            labelMode: "floating",
            type: "date",
            displayFormat: "dd/MM/yyyy",
            useMaskBehavior: true,
            openOnFieldClick: true
        });

        $("#resCheckInTime").dxDateBox({
            label: t("reservationDetail.checkInTime"),
            labelMode: "floating",
            type: "time",
            useMaskBehavior: true,
            openOnFieldClick: true
        });

        $("#resCheckOutDate").dxDateBox({
            label: t("reservationDetail.checkOutDate"),
            labelMode: "floating",
            type: "date",
            displayFormat: "dd/MM/yyyy",
            useMaskBehavior: true,
            openOnFieldClick: true
        });

        $("#resCheckOutTime").dxDateBox({
            label: t("reservationDetail.checkOutTime"),
            labelMode: "floating",
            type: "time",
            useMaskBehavior: true,
            openOnFieldClick: true
        });

        $("#resMonths").dxNumberBox({
            label: t("reservationDetail.months"),
            labelMode: "floating",
            min: 0,
            showSpinButtons: true,
            format: "#0"
        });

        $("#resNights").dxNumberBox({
            label: t("reservationDetail.nights"),
            labelMode: "floating",
            min: 0,
            showSpinButtons: true,
            format: "#0"
        });

        $("#resAutoExtend").dxSwitch({
            value: true,
            switchedOnText: t("reservationDetail.yes"),
            switchedOffText: t("reservationDetail.no")
        });

        initUnitPricingButton();

        $("#btnAddUnit").dxButton({
            text: t("reservationDetail.units.addRoom"),
            type: "default",
            stylingMode: "contained",
            onClick: openUnitPicker
        });

        $("#btnPickCompany").dxButton({
            text: t("reservationDetail.company.selectBtn"),
            type: "default",
            stylingMode: "contained",
            onClick: openCorporatePicker
        });

        $("#btnAddGuest").dxButton({
            text: t("reservationDetail.guest.addBtn"),
            type: "default",
            stylingMode: "contained",
            onClick: openGuestPicker
        });

        $("#btnAddCompanion").dxButton({
            text: t("reservationDetail.guest.addCompanion"),
            icon: "plus",
            type: "normal",
            stylingMode: "outlined",
            onClick() {
                openGuestPicker({
                    onPick(customerId, rowData) {
                        pageCtx.companions = pageCtx.companions || [];
                        pageCtx.companions.push(buildCompanionSlotFromCustomer(customerId, rowData));
                        refreshCompanionsGrid();
                        DevExpress.ui.notify(t("reservationDetail.companion.addedOk"), "success", 2200);
                    }
                });
            }
        });

        $("#unitsGrid").addClass("res-units-grid-host").dxDataGrid(
            reservationSectionDataGridOptions({
                keyExpr: "unitId",
                columns: buildUnitsGridColumns(),
                searchPanel: { visible: false },
                scrolling: unitsGridScrollOptions()
            })
        );
        applyUnitsGridLayoutOptions();

        $("#companyGrid").dxDataGrid(
            reservationSectionDataGridOptions({
                keyExpr: "corporateId",
                columns: buildCompanyGridColumns(),
                searchPanel: { visible: false },
                groupPanel: { visible: false }
            })
        );

        $("#guestsGrid").dxDataGrid(
            reservationSectionDataGridOptions({
                keyExpr: "customerId",
                columns: buildGuestGridColumns(),
                elementAttr: { class: "pms-grid-compact" },
                searchPanel: { visible: false },
                groupPanel: { visible: false }
            })
        );

        $("#companionsGrid").dxDataGrid(buildCompanionsDataGridOptions());

        updateCompanionsGridShellVisibility();
        updateGuestsGridShellVisibility();

        $("#btnAddExtra").dxButton({
            text: t("reservationDetail.extras.addPackage"),
            type: "default",
            stylingMode: "contained",
            onClick() {
                openExtraPackagePopup();
            }
        });

        $("#extrasGrid").dxDataGrid(
            reservationSectionDataGridOptions({
            keyExpr: "rowKey",
            dataSource: pageCtx.extras || [],
            height: 120,
            noDataText: t("reservationDetail.extras.emptyGrid"),
            searchPanel: { visible: false, width: 260 },
            editing: {
                mode: "cell",
                allowUpdating: true,
                allowDeleting: false,
                allowAdding: false
            },
            onEditorPreparing(e) {
                if (reservationGridsActionsDisabled()) {
                    e.cancel = true;
                    return;
                }

                if (e.parentType !== "dataRow") {
                    return;
                }

                if (e.dataField === "unitId") {
                    e.editorOptions.dataSource = getCompanionUnitLookupRows();
                    e.editorOptions.valueExpr = "unitId";
                    e.editorOptions.displayExpr = "label";
                    e.editorOptions.searchEnabled = true;
                    e.editorOptions.showClearButton = true;
                    e.editorOptions.openOnFieldClick = true;
                }

                const locked = ["postingRule", "serviceDate", "roomLabel", "subtotal", "taxAmount", "totalAmount"];
                if (locked.includes(e.dataField)) {
                    e.cancel = true;
                }
            },
            onCellValueChanged(e) {
                const key = e.key != null && e.key !== "" ? e.key : e.data && e.data.rowKey;
                if (key == null || reservationGridsActionsDisabled()) {
                    return;
                }

                const idx = (pageCtx.extras || []).findIndex((x) => x.rowKey === key);
                if (idx < 0) {
                    return;
                }

                Object.assign(pageCtx.extras[idx], e.data);
                if (e.column && e.column.dataField) {
                    let v = e.value;
                    if (e.column.dataField === "unitId") {
                        const coerced = coerceGridLookupScalar(e.value);
                        v =
                            coerced != null
                                ? coerced
                                : e.value === undefined || e.value === ""
                                  ? null
                                  : e.value;
                    }

                    pageCtx.extras[idx][e.column.dataField] = v;
                }

                const cur = pageCtx.extras[idx];
                const merged = buildLocalExtraRow(
                    {
                        packageId: cur.packageId,
                        itemName: cur.itemName,
                        postingRule: cur.postingRule,
                        serviceDate: cur.serviceDate,
                        unitId: cur.unitId,
                        guestCount: cur.guestCount,
                        nightCount: cur.nightCount,
                        unitPrice: cur.unitPrice
                    },
                    cur
                );
                pageCtx.extras[idx] = merged;
                refreshExtrasGrid();
                if (pageCtx.detail) {
                    syncFinancialUi({ skipFlash: true });
                }
            },
            columns: buildExtrasGridColumns()
            })
        );

        updateExtrasSectionVisibility();

        const kindInstAfterInit = $("#resGeneralKind").dxSelectBox("instance");
        setCompanySectionVisible(
            kindInstAfterInit ? kindInstAfterInit.option("value") === "company" : false
        );

        wireReservationDateDurationSync();
        wireHallHijriDateSync();
        initHallEventHijriEditorIfNeeded();
        renderReservationPeriodsUi(pageCtx.detail);
    }

    function rebuildPage() {
        pageCtx._hallHijriWireDone = false;
        pageCtx.hallEventHijriEditor = null;
        const $main = $("#reservationMain").empty();
        buildStaticLayout($main);
        initEditorsAfterLayout();
        renderDetails(pageCtx.detail);
        setReservationHeaderVisible(!pageCtx.isLocalNewReservation);
        initFooter();
        refreshNotesBadge(pageCtx.detail && pageCtx.detail.notesCount);
        refreshReservationPermissionUi({ force: true });
        prefetchReservationPosOutlets();
        applyHallReservationUi();
    }

    function maybeOpenHallReportPaymentDeepLink() {
        const params = new URLSearchParams(window.location.search);
        if (params.get("section") !== "payments") {
            return;
        }

        const docZaaerRaw = params.get("docZaaerId");
        if (docZaaerRaw == null || `${docZaaerRaw}`.trim() === "") {
            return;
        }

        const docZaaerId = Number(docZaaerRaw);
        if (!Number.isFinite(docZaaerId) || docZaaerId <= 0) {
            return;
        }

        const tab = `${params.get("tab") || "receipts"}`.trim().toLowerCase();
        switchReservationPageTab("payments");

        window.setTimeout(() => {
            if (tab === "credit_notes") {
                switchPaymentRecordTab("credit_notes");
                return loadReservationPaymentRows("credit_notes").then((rows) => {
                    const match = (rows || []).find((r) => Number(r.zaaerId ?? r.ZaaerId) === docZaaerId);
                    if (!match) {
                        DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                    }
                });
            }

            const kind = tab === "disbursements" ? "disbursements" : "receipts";
            switchPaymentRecordTab(kind);
            const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
            if (!svc || typeof svc.loadPaymentRows !== "function") {
                return;
            }

            svc.loadPaymentRows({
                kind,
                reservationId: pageCtx.routeId || pageCtx.reservationRouteId
            })
                .then((rows) => {
                    const match = (rows || []).find((r) => Number(r.zaaerId ?? r.ZaaerId) === docZaaerId);
                    if (!match) {
                        DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                        return;
                    }
                    if (kind === "disbursements") {
                        openPaymentDisbursementPopup({ editRow: match });
                    } else {
                        openPaymentReceiptPopup({ editRow: match });
                    }
                })
                .catch((err) => {
                    DevExpress.ui.notify(
                        (err && err.message) || t("activityLog.detailsNotFound"),
                        "error",
                        3200
                    );
                });
        }, 450);
    }

    function maybeOpenHallOpsPaymentEdit() {
        const params = new URLSearchParams(window.location.search);
        if (params.get("paymentEdit") !== "1") {
            return;
        }

        let payload = null;
        try {
            payload = JSON.parse(sessionStorage.getItem("zaaer.hallOps.paymentEdit") || "null");
        } catch (_) {
            payload = null;
        }
        sessionStorage.removeItem("zaaer.hallOps.paymentEdit");
        if (!payload || payload.zaaerId == null) {
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!svc || typeof svc.loadPaymentRows !== "function") {
            return;
        }

        const kind = payload.kind === "disbursement" ? "disbursements" : "receipts";
        switchReservationPageTab("payments");
        window.setTimeout(() => {
            switchPaymentRecordTab(kind);
            svc.loadPaymentRows({
                kind,
                reservationId: pageCtx.routeId || pageCtx.reservationRouteId
            })
                .then((rows) => {
                    const match = (rows || []).find((r) => Number(r.zaaerId) === Number(payload.zaaerId));
                    if (!match) {
                        DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                        return;
                    }
                    if (kind === "disbursements") {
                        openPaymentDisbursementPopup({ editRow: match });
                    } else {
                        openPaymentReceiptPopup({ editRow: match });
                    }
                })
                .catch((err) => {
                    DevExpress.ui.notify(
                        (err && err.message) || t("activityLog.detailsNotFound"),
                        "error",
                        3200
                    );
                });
        }, 450);
    }

    function loadPage(showSpinner) {
        const rid = getRouteId();
        const params = new URLSearchParams(window.location.search);
        const isClientNew = !rid && getNewReservationFromQuery();
        const newResCtx = isClientNew ? consumeNewReservationContext() : null;
        const hotelId =
            params.get("hotelId") ||
            (newResCtx && newResCtx.hotelId != null && `${newResCtx.hotelId}`.trim() !== ""
                ? String(newResCtx.hotelId)
                : null);
        const hotelCode =
            params.get("hotelCode") ||
            (newResCtx && newResCtx.hotelCode ? String(newResCtx.hotelCode).trim() : "") ||
            window.Zaaer.ApiService.getHotelCode();

        if (!rid && !isClientNew) {
            $("#reservationMain").html(`<div class="res-empty">${t("reservationDetail.missingId")}</div>`);
            return $.Deferred().reject().promise();
        }

        if (hotelCode) {
            window.Zaaer.ApiService.setHotelCode(hotelCode);
        }

        pageCtx.routeId = rid;
        pageCtx.hotelIdParam = hotelId;
        resetPaymentCreditNotesProbe();
        pageCtx.isClientNewReservation = isClientNew;
        pageCtx.isLocalNewReservation = isClientNew;
        pageCtx.checkoutUiPendingFirstSave = !!isClientNew;

        const lp = $("#reservationLoadPanel").dxLoadPanel("instance");
        if (showSpinner !== false) {
            lp.show();
        }

        if (isClientNew) {
            const ksaNowPromise =
                window.Zaaer.KsaTime && typeof window.Zaaer.KsaTime.fetchNow === "function"
                    ? window.Zaaer.KsaTime.fetchNow()
                    : Promise.resolve(new Date());

            return Promise.all([
                ksaNowPromise,
                window.Zaaer.ReservationDetailService.loadVisitPurposes().catch(() => []),
                window.Zaaer.ReservationDetailService.loadReservationSources().catch(() => []),
                window.Zaaer.ReservationDetailService.loadCustomerRelations().catch(() => []),
                fetchPropertyMode()
            ])
                .then(([ksaNow, purposes, sources, customerRelations, mode]) => {
                    applyPropertyMode(mode);
                    pageCtx.purposes = purposes || [];
                    pageCtx.sources = sources || [];
                    pageCtx.customerRelations = customerRelations || [];

                    pageCtx.detail = buildClientNewReservationDetail(params, newResCtx, ksaNow);
                    pageCtx.persistedDetail = null;

                    const finishNewReservationUi = () =>
                        Promise.all([
                            window.Zaaer.ReservationDetailService.loadReservationPackages(pageCtx.detail.hotelId).catch(
                                () => []
                            ),
                            window.Zaaer.ReservationDetailService.loadPenaltyCatalog(pageCtx.detail.hotelId).catch(() => [])
                        ]).then(([packages, penalties]) => {
                            pageCtx.reservationPackages = packages || [];
                            pageCtx.penaltyCatalog = penalties || [];
                            pageCtx.companions = [];
                            pageCtx.companionKeySeq = 1;
                            pageCtx.extras = [];
                            pageCtx.extraKeySeq = 1;
                            pageCtx.pricingRateByLineKey = {};
                            pageCtx.useLocalFinancialTotals = false;
                            ensurePricingRatesForAllLines();
                            if (!hasMeaningfulFinancial((pageCtx.detail && pageCtx.detail.financial) || {})) {
                                pageCtx.useLocalFinancialTotals = true;
                            }

                            rebuildPage();
                        });

                    return enrichNewReservationDetailWithSuggestedRates(pageCtx.detail).then(() => finishNewReservationUi());
                })
                .catch((err) => {
                    const msg = err && err.message ? String(err.message) : "";
                    console.error("reservation-detail: new reservation init failed", err);
                    $("#reservationMain").html(`<div class="res-empty">${t("reservationDetail.notFound")}</div>`);
                    DevExpress.ui.notify(msg || t("error.loadReservationDetail"), "error", 5000);
                })
                .finally(() => lp.hide());
        }

        return Promise.all([
            window.Zaaer.ReservationDetailService.loadById(rid, hotelId),
            window.Zaaer.ReservationDetailService.loadVisitPurposes().catch(() => []),
            window.Zaaer.ReservationDetailService.loadReservationSources().catch(() => []),
            window.Zaaer.ReservationDetailService.loadCustomerRelations().catch(() => []),
            fetchPropertyMode()
        ])
            .then(([detail, purposes, sources, customerRelations, mode]) => {
                applyPropertyMode(mode);
                return Promise.all([
                    window.Zaaer.ReservationDetailService.loadReservationPackages(detail.hotelId || hotelId).catch(() => []),
                    window.Zaaer.ReservationDetailService.loadPenaltyCatalog(detail.hotelId || hotelId).catch(() => []),
                    loadHallEventContextIfNeeded()
                ]).then(([packages, penalties]) => {
                        pageCtx.detail = detail;
                        markReservationBaseline(detail);
                        applyReservationRouteFromDetail(detail);
                        pageCtx.purposes = purposes || [];
                        pageCtx.sources = sources || [];
                        pageCtx.customerRelations = customerRelations || [];
                        pageCtx.reservationPackages = packages || [];
                        pageCtx.penaltyCatalog = penalties || [];
                        ingestCompanionsFromDetail(detail);
                        ingestExtrasFromDetail(detail);
                        ingestDiscountsFromDetail(detail);
                        pageCtx.pricingRateByLineKey = {};
                        pageCtx.useLocalFinancialTotals = false;
                        try {
                            rebuildPage();
                        } catch (e) {
                            const msg = e && e.message ? String(e.message) : "";
                            console.error("reservation-detail: rebuildPage failed", e);
                            $("#reservationMain").html(`<div class="res-empty">${t("reservationDetail.notFound")}</div>`);
                            DevExpress.ui.notify(msg || t("error.loadReservationDetail"), "error", 5000);
                            return undefined;
                        }

                        return hydratePricingFromServerDayRates().then(() => {
                            maybeOpenHallOpsPaymentEdit();
                            maybeOpenHallReportPaymentDeepLink();
                        });
                    });
            })
            .catch((err) => {
                const msg = err && err.message ? String(err.message) : "";
                console.error("reservation-detail: load failed", err);
                $("#reservationMain").html(`<div class="res-empty">${t("reservationDetail.notFound")}</div>`);
                DevExpress.ui.notify(msg || t("error.loadReservationDetail"), "error", 5000);
            })
            .finally(() => lp.hide());
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ReservationPaymentVoucherUi = {
        openReceiptEdit(options) {
            openPaymentReceiptPopup({
                editRow: options && options.editRow,
                externalContext: options && options.context,
                afterSave: options && options.afterSave,
                readOnly: !!(options && options.readOnly)
            });
        },
        openDisbursementEdit(options) {
            openPaymentDisbursementPopup({
                editRow: options && options.editRow,
                externalContext: options && options.context,
                afterSave: options && options.afterSave,
                readOnly: !!(options && options.readOnly)
            });
        },
        syncAfterPaymentMutation(receiptTypeOrVoucher, opts) {
            return afterPaymentReceiptMutation(receiptTypeOrVoucher, opts || {});
        },
        refreshReservationFinancialFromServer(reservationId, hotelId) {
            return refreshReservationFinancialFromServer(reservationId, hotelId);
        }
    };

    $(function () {
        window.Zaaer.LocalizationService.init();
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (api && typeof api.requireToken === "function" && !api.requireToken()) {
            return;
        }

        if (!$("#reservationMain").length) {
            return;
        }

        document.title = t("reservationDetail.editTitle");
        $("#pageTitle, #pageSubtitle").empty();

        $("#reservationLoadPanel").dxLoadPanel({
            shadingColor: "rgba(255,255,255,0.45)",
            position: { of: ".res-shell" },
            visible: false
        });

        $("#backToBoard").dxButton({
            text: t("reservationDetail.backToBoard"),
            icon: "arrowleft",
            type: "normal",
            stylingMode: "text",
            onClick() {
                window.location.href = resolveRoomBoardUrl();
            }
        });
        initReservationNotesButton();
        initReservationOtherOptions();
        initReservationActions();
        initReservationPosMessageListener();

        if (window.Zaaer.PmsTopChrome && typeof window.Zaaer.PmsTopChrome.initHeaderHotelPicker === "function") {
            window.Zaaer.PmsTopChrome.ensureHeaderHotelHost();
            window.Zaaer.PmsTopChrome.initHeaderHotelPicker({
                onHotelChanged() {
                    window.location.href = resolveRoomBoardUrl();
                }
            });
        }

        $(document).on("zaaer:permissions-refreshed", function (_e, permissionList) {
            refreshReservationPermissionUi({ permissions: permissionList });
        });

        if (api && typeof api.startPermissionAutoRefresh === "function") {
            api.startPermissionAutoRefresh({ intervalMs: 45000 });
        }

        const resDetailViewportMq = window.matchMedia("(max-width: 720px)");
        let resDetailViewportWasMobile = resDetailViewportMq.matches;
        function onResDetailViewportChange() {
            const mobile = resDetailViewportMq.matches;
            if (mobile !== resDetailViewportWasMobile) {
                resDetailViewportWasMobile = mobile;
                refreshResDetailGridColumnsForViewport();
            }

            syncDatesTogglesPlacement();
        }

        if (typeof resDetailViewportMq.addEventListener === "function") {
            resDetailViewportMq.addEventListener("change", onResDetailViewportChange);
        } else if (typeof resDetailViewportMq.addListener === "function") {
            resDetailViewportMq.addListener(onResDetailViewportChange);
        }

        ensureFreshPermissions().finally(function () {
            loadPage(true);
            initFooter();
        });
    });
})(window, jQuery, DevExpress);
