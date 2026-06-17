(function (window, $) {
    "use strict";

    const svc = () => window.Zaaer && window.Zaaer.HallEventsService;
    const t = (key) => window.Zaaer.LocalizationService.t(key);

    const HALL_OPS_LOCALE_PATCH = {
        ar: {
            "hallOps.col.eventStatus": "حالة الفعالية",
            "hallOps.col.referenceNo": "رقم المرجع",
            "hallOps.col.type": "النوع",
            "hallOps.col.date": "التاريخ",
            "hallOps.col.paymentMethod": "طريقة الدفع",
            "hallOps.col.amount": "المبلغ",
            "hallOps.col.notes": "ملاحظات",
            "hallOps.col.received": "المقبوض",
            "hallOps.col.disbursed": "المصروف",
            "hallOps.col.disbursement": "الصرف",
            "hallOps.col.creditNote": "إشعار دائن",
            "hallOps.action.openReservation": "الدخول الى الحجز",
            "hallOps.action.checkIn": "تسجيل الوصول",
            "hallOps.action.operations": "إجراءات التشغيل",
            "hallOps.payments.title": "مدفوعات الحجز",
            "hallOps.payments.noRows": "لا توجد سندات لهذا الحجز",
            "hallOps.payments.docType.receipt": "سند قبض",
            "hallOps.payments.docType.disbursement": "سند صرف",
            "hallOps.statusUpdated": "تم تحديث الحالة",
            "hallOps.checkInOk": "تم تسجيل الوصول وتحديث حالة الحجز",
            "hallOps.unpaidNotify.title": "حجوزات بمبالغ مستحقة",
            "hallOps.unpaidNotify.hint": "الرصيد = إيجار القاعة − سندات القبض − سندات الصرف (محسوب من السندات)",
            "hallOps.unpaidNotify.empty": "لا توجد حجوزات بمبالغ مستحقة",
            "hallOps.unpaidNotify.loadMore": "تحميل المزيد",
            "hallOps.unpaidNotify.badgeHint": "حجوزات بمبالغ لم تُسدَّد بالكامل",
            "hallOps.settlement.blockClose": "لا يمكن إغلاق الحجز — متبقي {amount} من إيجار القاعة حسب السندات",
            "hallOps.calendarLayout.month": "التقويم",
            "hallOps.calendarLayout.timeline": "جدول القاعات",
            "hallOps.calendarLayout.agenda": "قائمة الفعاليات",
            "hallOps.calendarLayout.noHall": "بدون قاعة",
            "hallOps.calendarLayout.agendaEmpty": "لا توجد فعاليات في هذه الفترة",
            "hallOps.calendarSpan.label": "مدة العرض",
            "hallOps.calendarSpan.1": "شهر",
            "hallOps.calendarSpan.2": "شهران",
            "hallOps.calendarSpan.3": "3 أشهر",
            "hallOps.calendarSpan.6": "6 أشهر"
        },
        en: {
            "hallOps.col.eventStatus": "Event status",
            "hallOps.col.referenceNo": "Reference #",
            "hallOps.col.type": "Type",
            "hallOps.col.date": "Date",
            "hallOps.col.paymentMethod": "Payment method",
            "hallOps.col.amount": "Amount",
            "hallOps.col.notes": "Notes",
            "hallOps.col.received": "Received",
            "hallOps.col.disbursed": "Disbursed",
            "hallOps.col.disbursement": "Disbursements",
            "hallOps.col.creditNote": "Credit note",
            "hallOps.action.openReservation": "Go to reservation",
            "hallOps.action.checkIn": "Check in",
            "hallOps.action.operations": "Operations",
            "hallOps.payments.title": "Reservation payments",
            "hallOps.payments.noRows": "No vouchers for this reservation",
            "hallOps.payments.docType.receipt": "Receipt",
            "hallOps.payments.docType.disbursement": "Disbursement",
            "hallOps.statusUpdated": "Status updated",
            "hallOps.checkInOk": "Check-in completed and reservation status updated",
            "hallOps.unpaidNotify.title": "Outstanding hall balances",
            "hallOps.unpaidNotify.hint": "Balance = hall rent − receipts − disbursements (voucher-based)",
            "hallOps.unpaidNotify.empty": "No reservations with outstanding balance",
            "hallOps.unpaidNotify.loadMore": "Load more",
            "hallOps.unpaidNotify.badgeHint": "Reservations with unpaid hall rent",
            "hallOps.settlement.blockClose": "Cannot close — {amount} hall rent remains per vouchers",
            "hallOps.calendarLayout.month": "Calendar",
            "hallOps.calendarLayout.timeline": "Hall timeline",
            "hallOps.calendarLayout.agenda": "Event list",
            "hallOps.calendarLayout.noHall": "No hall",
            "hallOps.calendarLayout.agendaEmpty": "No events in this period",
            "hallOps.calendarSpan.label": "Display span",
            "hallOps.calendarSpan.1": "1 month",
            "hallOps.calendarSpan.2": "2 months",
            "hallOps.calendarSpan.3": "3 months",
            "hallOps.calendarSpan.6": "6 months"
        }
    };

    function ensureHallOpsLocale() {
        if (!window.ZaaerI18n) {
            return;
        }
        ["ar", "en"].forEach((culture) => {
            const patch = HALL_OPS_LOCALE_PATCH[culture];
            if (!patch) {
                return;
            }
            window.ZaaerI18n[culture] = window.ZaaerI18n[culture] || {};
            Object.keys(patch).forEach((key) => {
                const current = window.ZaaerI18n[culture][key];
                if (!current || current === key) {
                    window.ZaaerI18n[culture][key] = patch[key];
                }
            });
        });
    }

    ensureHallOpsLocale();

    let lookups = null;
    let hallHotelId = null;
    let eventsCache = [];
    let unpaidNotifyState = { count: 0, loading: false, refreshTimer: null, popupOpen: false };
    const UNPAID_NOTIFY_PAGE_SIZE = 50;
    let selectedReservationId = null;
    const calendarNavFilter = {
        mode: "gregorian",
        applied: false,
        anchorGregorian: null,
        gregorianDate: null,
        eventDateHijri: null,
        displayLabel: ""
    };

    let filterAppliedBannerEl = null;
    const periodFilter = {
        from: null,
        to: null,
        applied: false,
        visible: false
    };
    const statusFilter = {
        eventStatus: null
    };
    const CARD_THEME_STORAGE_KEY = "hallOpsCardTheme";
    const TAB_STORAGE_KEY = "hallOpsActiveTab";
    const CALENDAR_LAYOUT_STORAGE_KEY = "hallOpsCalendarLayout";
    const CALENDAR_MONTH_SPAN_STORAGE_KEY = "hallOpsCalendarMonthSpan";
    const CALENDAR_MONTH_SPAN_OPTIONS = [1, 2, 3, 6];
    const CALENDAR_LAYOUT_MOBILE_MQ = "(max-width: 768px)";
    const CARD_THEMES = ["soft", "classic", "bold"];
    let cardTheme = "soft";
    let eventsLoadRequestSeq = 0;
    let eventsLoadAppliedSeq = 0;
    let suppressSchedulerOptionChanged = false;
    let isApplyingSchedulerData = false;
    let dashboardRenderSeq = 0;
    let occupancyRenderSeq = 0;

    function safeLocalStorageGet(key) {
        try {
            return window.localStorage ? window.localStorage.getItem(key) : null;
        } catch (_) {
            return null;
        }
    }

    function safeLocalStorageSet(key, value) {
        try {
            if (window.localStorage) {
                window.localStorage.setItem(key, value);
            }
        } catch (_) {
            /* ignore storage errors */
        }
    }

    function normalizeCardTheme(value) {
        const v = `${value || ""}`.trim().toLowerCase();
        return CARD_THEMES.indexOf(v) >= 0 ? v : "soft";
    }

    function applyCardThemeClass(themeName) {
        const $body = $("body");
        $body.removeClass("hall-ops-card-theme-soft hall-ops-card-theme-classic hall-ops-card-theme-bold");
        $body.addClass(`hall-ops-card-theme-${themeName}`);
    }

    function setCardTheme(themeName) {
        cardTheme = normalizeCardTheme(themeName);
        applyCardThemeClass(cardTheme);
        safeLocalStorageSet(CARD_THEME_STORAGE_KEY, cardTheme);
    }

    function schedulerNavigatorText(data) {
        const d = data && data.startDate ? data.startDate : new Date();
        return forceEnglishDigits(d.toLocaleDateString("en-GB", { month: "long", year: "numeric" }));
    }

    function getCardThemeOptions() {
        return [
            { id: "soft", text: "Soft" },
            { id: "classic", text: "Classic" },
            { id: "bold", text: "Bold" }
        ];
    }

    function normalizeLocalDate(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        return new Date(d.getFullYear(), d.getMonth(), d.getDate());
    }

    function formatLocalDateParam(value) {
        const dp = window.Zaaer && window.Zaaer.PmsDateParam;
        if (dp && typeof dp.formatLocalDateParam === "function") {
            return dp.formatLocalDateParam(value);
        }
        const d = normalizeLocalDate(value);
        if (!d) {
            return value;
        }
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function notify(msg, type) {
        DevExpress.ui.notify(msg, type || "info", 2800);
    }

    function can(code) {
        const api = window.Zaaer && window.Zaaer.ApiService;
        return api && typeof api.hasPermission === "function" ? api.hasPermission(code) : true;
    }

    function resolveReservationRouteId(item) {
        if (item == null) {
            return null;
        }
        if (typeof item === "number" || typeof item === "string") {
            return item;
        }
        return item.reservationId || item.zaaerId || item.ZaaerId || null;
    }

    function openReservation(item) {
        const id = resolveReservationRouteId(item);
        if (!id) {
            return;
        }
        window.location.href = `/reservation-detail.html?id=${encodeURIComponent(id)}`;
    }

    function formatEventHijriLabel(eventDate, storedHijri) {
        const hijri = window.Zaaer && window.Zaaer.PmsHijriDate;
        if (!hijri) {
            return storedHijri || "";
        }
        if (storedHijri) {
            return storedHijri;
        }
        return hijri.formatHijriSlashFromDate(eventDate);
    }

    function schedulerTemplateCellData(model) {
        return (model && (model.cellData || model.data)) || model || {};
    }

    function schedulerHijriDayLabel(date) {
        if (!date) {
            return "";
        }
        const lib = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (lib && typeof lib.formatDayMonthFromGregorian === "function" && lib.isReady()) {
            return lib.formatDayMonthFromGregorian(date);
        }
        const hijri = window.Zaaer && window.Zaaer.PmsHijriDate;
        if (!hijri) {
            return "";
        }
        const slash = hijri.formatHijriSlashFromDate(date);
        if (!slash) {
            return "";
        }
        const parts = slash.split("/");
        if (parts.length === 3) {
            return `${parts[2]}/${parts[1]}`;
        }
        return slash;
    }

    function forceEnglishDigits(value) {
        return `${value == null ? "" : value}`
            .replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d)))
            .replace(/[۰-۹]/g, (d) => String("۰۱۲۳۴۵۶۷۸۹".indexOf(d)));
    }

    function schedulerHijriFullDateLabel(date) {
        if (!date) {
            return "";
        }
        const lib = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (lib && typeof lib.formatDayMonthYearFromGregorian === "function" && lib.isReady()) {
            return forceEnglishDigits(lib.formatDayMonthYearFromGregorian(date));
        }
        const hijri = window.Zaaer && window.Zaaer.PmsHijriDate;
        if (!hijri) {
            return "";
        }
        const slash = hijri.formatHijriSlashFromDate(date);
        if (!slash) {
            return "";
        }
        const parts = slash.split("/");
        if (parts.length === 3) {
            return forceEnglishDigits(`${parts[2]}/${parts[1]}/${parts[0]}`);
        }
        return forceEnglishDigits(slash);
    }

    function formatWeekdayName(value, style) {
        if (!value) {
            return "";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const culture = isArabicUi() ? "ar-SA" : "en-GB";
        const weekdayStyle = style === "short" ? "short" : "long";
        return d.toLocaleDateString(culture, { weekday: weekdayStyle });
    }

    function schedulerHijriCellTemplates() {
        const suffix = t("hallOps.hijriSuffix") || "H";
        return {
            dataCellTemplate(model) {
                const data = schedulerTemplateCellData(model);
                const start = data.startDate;
                if (!start) {
                    return $("<div/>");
                }
                const greg = forceEnglishDigits(formatGregorianShort(start));
                const hijri = forceEnglishDigits(schedulerHijriFullDateLabel(start));
                const $cell = $("<div class='hall-ops-month-cell'/>");
                const $stack = $("<div class='hall-ops-month-cell__dates' dir='ltr'/>");
                if (greg) {
                    $stack.append($("<span class='hall-ops-month-cell__g'/>").text(greg));
                }
                if (hijri) {
                    const $h = $("<span class='hall-ops-month-cell__h'/>").text(hijri);
                    if (suffix) {
                        $h.append(" ").append($("<span class='hall-ops-month-cell__suffix'/>").text(suffix));
                    }
                    $stack.append($h);
                }
                $cell.append($stack);
                const dayEvents = schedulerEventsForDate(start);
                if (dayEvents.length) {
                    const $cards = $("<div class='hall-ops-month-cell__cards'/>");
                    dayEvents.slice(0, 2).forEach((ev) => {
                        $cards.append(buildMonthCellEventCard(ev));
                    });
                    if (dayEvents.length > 2) {
                        $("<div class='hall-ops-month-cell__more'/>")
                            .text(forceEnglishDigits(`+${dayEvents.length - 2}`))
                            .appendTo($cards);
                    }
                    $cell.append($cards);
                }
                return $cell;
            },
            dateCellTemplate(model) {
                const data = schedulerTemplateCellData(model);
                const date = data.date;
                if (!date) {
                    return $("<div/>");
                }
                const rawText = data.text != null ? `${data.text}`.trim() : "";
                if (rawText && /\d/.test(rawText)) {
                    return $("<div/>");
                }
                const dow = formatWeekdayName(date) || rawText;
                const $cell = $("<div class='hall-ops-sched-datehdr'/>");
                if (dow) {
                    $cell.append($("<span class='hall-ops-sched-datehdr__dow'/>").text(dow));
                }
                return $cell;
            }
        };
    }

    function schedulerEventsForDate(dayDate) {
        const dayKey = formatLocalDateParam(dayDate);
        if (!dayKey) {
            return [];
        }
        return eventsCache
            .filter((e) => formatLocalDateParam(e.eventDate != null ? e.eventDate : e.EventDate) === dayKey)
            .sort((a, b) => {
                const ta = readEventField(a, "eventStartTime", "EventStartTime") || "00:00";
                const tb = readEventField(b, "eventStartTime", "EventStartTime") || "00:00";
                return ta.localeCompare(tb);
            });
    }

    function eventRouteReservationId(e) {
        return e && (e.reservationId != null ? e.reservationId : e.ReservationId);
    }

    function eventDepositAmountValue(e) {
        return Number(e && (e.depositAmount != null ? e.depositAmount : e.DepositAmount) || 0);
    }

    function eventTotalAmountValue(e) {
        return Number(e && (e.totalAmount != null ? e.totalAmount : e.TotalAmount) || 0);
    }

    function resolveEventAccentColor(e) {
        if (!e) {
            return "#94a3b8";
        }
        const code = normalizeEventStatusCode(e);
        return e.eventStatusColor || e.EventStatusColor || eventStatusColor(code) || "#94a3b8";
    }

    function eventStatusIconName(rowOrCode) {
        const row = typeof rowOrCode === "string" ? { eventStatus: rowOrCode } : rowOrCode;
        if (!row) {
            return "clock";
        }
        const raw = `${row.eventStatus || row.EventStatus || ""}`.trim().toLowerCase();
        if (raw === "event_running") {
            return "video";
        }
        if (raw === "event_today") {
            return "event";
        }
        const code = normalizeEventStatusCode(row);
        if (code === "confirmed") {
            return "check";
        }
        if (code === "closed") {
            return "lock";
        }
        return "clock";
    }

    function appendEventStatusIcon($parent, rowOrCode, extraClass) {
        const row = typeof rowOrCode === "string" ? { eventStatus: rowOrCode } : rowOrCode;
        const statusCode = normalizeEventStatusCode(row);
        const iconName = eventStatusIconName(row);
        const label = eventStatusLabel(statusCode);
        const color = eventStatusColor(statusCode);
        const cls = extraClass || "hall-ops-appt__status-icon";
        return $("<span/>")
            .addClass(`${cls} dx-icon dx-icon-${iconName}`)
            .attr("title", label)
            .attr("aria-label", label)
            .css("color", color)
            .appendTo($parent);
    }

    function stampHallOpsEventCard($el, data) {
        const reservationId = data && data.reservationId;
        $el.addClass("hall-ops-event-card");
        if (reservationId != null && reservationId !== "") {
            $el.attr("data-reservation-id", reservationId);
        }
        return $el;
    }

    function buildMonthCellEventCard(e) {
        const owner = eventCustomerName(e);
        const hall = readEventField(e, "hallName", "HallName");
        const occasion = readEventField(e, "occasionName", "OccasionName");
        const deposit = formatMoney(eventDepositAmountValue(e));
        const rent = formatMoney(eventTotalAmountValue(e));
        const accent = resolveEventAccentColor(e);

        const reservationId = eventRouteReservationId(e);
        const $card = $("<button type='button' class='hall-ops-cell-card hall-ops-event-card'/>")
            .css("--appt-accent", accent)
            .attr("data-reservation-id", reservationId != null ? reservationId : "");
        $("<span class='hall-ops-cell-card__bar' aria-hidden='true'/>").appendTo($card);
        const $body = $("<span class='hall-ops-cell-card__body'/>").appendTo($card);
        const $head = $("<span class='hall-ops-cell-card__head'/>").appendTo($body);
        appendEventStatusIcon($head, e, "hall-ops-cell-card__status-icon");
        $("<span class='hall-ops-cell-card__title'/>").text(forceEnglishDigits(owner)).appendTo($head);

        const metaParts = [];
        if (hall) {
            metaParts.push(hall);
        }
        if (occasion && occasion !== owner) {
            metaParts.push(occasion);
        }
        if (metaParts.length) {
            $("<span class='hall-ops-cell-card__meta'/>")
                .text(forceEnglishDigits(metaParts.join(" · ")))
                .appendTo($body);
        }

        $("<span class='hall-ops-cell-card__money'/>")
            .text(forceEnglishDigits(`${t("hallOps.col.deposit")}: ${deposit} · ${t("hallOps.col.rent")}: ${rent}`))
            .appendTo($body);

        $card.on("click", (evt) => {
            evt.preventDefault();
            evt.stopPropagation();
            if (reservationId != null && reservationId !== "") {
                openReservation(reservationId);
            }
        });
        return $card;
    }

    function unwrapApiData(res) {
        if (!res) {
            return res;
        }
        return res.data !== undefined ? res.data : (res.Data !== undefined ? res.Data : res);
    }

    function findEventByReservationId(reservationId) {
        const rid = Number(reservationId);
        if (!Number.isFinite(rid) || rid <= 0) {
            return null;
        }
        return eventsCache.find((e) => Number(eventRouteReservationId(e)) === rid) || null;
    }

    function eventAllowedTransitions(e) {
        if (!e) {
            return [];
        }
        const codes = e.allowedTransitions || e.AllowedTransitions || [];
        return Array.isArray(codes) ? codes.filter(Boolean) : [];
    }

    function paymentDocTypeLabel(docType) {
        if (docType === "receipt") {
            return t("hallOps.payments.docType.receipt");
        }
        if (docType === "disbursement") {
            return t("hallOps.payments.docType.disbursement");
        }
        return docType || "";
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
        if (!isArabicUi()) {
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

    function receiptRowAmount(row) {
        if (!row) {
            return 0;
        }
        const raw = row.amountPaid ?? row.AmountPaid ?? row.amount ?? row.Amount ?? row.totalAmount ?? row.TotalAmount;
        return Number(raw) || 0;
    }

    function classifyPaymentReceiptRow(row) {
        const amount = receiptRowAmount(row);
        const receiptType = `${row.receiptType || row.ReceiptType || ""}`.trim().toLowerCase();
        const voucherCode = `${row.voucherCode || row.VoucherCode || ""}`.trim().toLowerCase();
        const docNo = `${row.receiptNo || row.ReceiptNo || row.number || row.Number || ""}`.trim().toUpperCase();
        const isDisbursement = amount < 0
            || receiptType === "refund"
            || receiptType === "security_deposit_refund"
            || voucherCode === "refund"
            || voucherCode === "security_deposit_refund"
            || docNo.startsWith("PAY");
        return isDisbursement ? "disbursement" : "receipt";
    }

    function mapReceiptToPaymentGridRow(r) {
        const amount = receiptRowAmount(r);
        const docType = classifyPaymentReceiptRow(r);
        return {
            sourceKind: "receipt",
            receiptZaaerId: r.zaaerId ?? r.ZaaerId ?? null,
            receiptId: r.receiptId ?? r.ReceiptId ?? null,
            docNo: r.receiptNo || r.ReceiptNo || r.documentNo || r.number || r.Number || "",
            docType,
            amount,
            displayAmount: docType === "disbursement" ? Math.abs(amount) : amount,
            date: r.receiptDate || r.ReceiptDate || r.date || r.Date || r.createdAt || r.CreatedAt,
            paymentMethod: mapPaymentMethodGridDisplay(r.paymentMethod || r.PaymentMethod || r.paymentMethodName || r.PaymentMethodName || ""),
            notes: r.notes || r.Notes || r.reason || r.Reason || "",
            raw: r
        };
    }

    function summarizePaymentRows(rows, hallRent) {
        let received = 0;
        let disbursed = 0;
        (rows || []).forEach((row) => {
            const amount = Number(row.amount) || 0;
            if (row.docType === "disbursement" || amount < 0) {
                disbursed += Math.abs(amount);
            } else if (amount > 0) {
                received += amount;
            }
        });
        const rent = Number(hallRent) || 0;
        return {
            received,
            disbursed,
            balance: rent - received - disbursed
        };
    }

    function hallSettlementBlockMessage(settlement) {
        const due = settlement && settlement.balanceDue != null
            ? formatMoney(settlement.balanceDue)
            : formatMoney(0);
        return (t("hallOps.settlement.blockClose") || "Cannot close — {amount} remains")
            .replace("{amount}", forceEnglishDigits(due));
    }

    function assertHallEventCanClose(reservationId) {
        return svc().getSettlement(reservationId).then((settlement) => {
            if (!settlement || settlement.canClose !== true) {
                const err = new Error(hallSettlementBlockMessage(settlement));
                err.settlement = settlement;
                throw err;
            }
            return settlement;
        });
    }

    function attemptHallEventStatusChange(reservationId, statusCode) {
        const code = `${statusCode || ""}`.toLowerCase();
        const runTransition = () => svc().transitionStatus(reservationId, { eventStatus: statusCode })
            .then(() => {
                notify(t("hallOps.statusUpdated") || "Updated", "success");
                refreshAll();
            });

        if (code === "closed") {
            return assertHallEventCanClose(reservationId)
                .then(() => runTransition())
                .catch((err) => {
                    notify((err && err.message) || t("common.error"), "error", 4200);
                });
        }

        return runTransition().catch((err) => notify((err && err.message) || t("common.error"), "error"));
    }

    function scheduleUnpaidNotifyBadgeRefresh() {
        if (unpaidNotifyState.refreshTimer) {
            clearTimeout(unpaidNotifyState.refreshTimer);
        }
        unpaidNotifyState.refreshTimer = window.setTimeout(() => {
            unpaidNotifyState.refreshTimer = null;
            refreshUnpaidNotifyBadge();
        }, 800);
    }

    function updateUnpaidNotifyBadgeUi() {
        const $host = $("#hallOpsUnpaidNotifyBtn");
        if (!$host.length) {
            return;
        }

        const count = unpaidNotifyState.count;
        const $badge = $host.find(".hall-ops-unpaid-notify-btn__badge");
        if (count > 0) {
            $badge
                .text(count > 99 ? "99+" : forceEnglishDigits(String(count)))
                .attr("data-visible", "true");
            $host.addClass("hall-ops-unpaid-notify-host--active");
        } else {
            $badge.text("").removeAttr("data-visible");
            $host.removeClass("hall-ops-unpaid-notify-host--active");
        }
    }

    function refreshUnpaidNotifyBadge() {
        const $host = $("#hallOpsUnpaidNotifyBtn");
        if (!$host.length || !svc() || typeof svc().getUnpaidBalances !== "function") {
            return $.Deferred().resolve().promise();
        }

        unpaidNotifyState.loading = true;
        return svc().getUnpaidBalances({ countOnly: true, take: 1, skip: 0 })
            .then((data) => {
                unpaidNotifyState.count = Number(data && (data.totalCount ?? data.TotalCount)) || 0;
                updateUnpaidNotifyBadgeUi();
            })
            .catch(() => {
                /* keep previous badge on transient errors */
            })
            .always(() => {
                unpaidNotifyState.loading = false;
            });
    }

    function showHallUnpaidNotifyPopup() {
        if (unpaidNotifyState.popupOpen) {
            return;
        }
        unpaidNotifyState.popupOpen = true;

        const $host = $("<div class='hall-ops-unpaid-popup'/>");
        const $hint = $("<p class='hall-ops-unpaid-popup__hint'/>")
            .text(t("hallOps.unpaidNotify.hint"))
            .appendTo($host);
        const $summary = $("<div class='hall-ops-unpaid-popup__summary'/>").appendTo($host);
        const $gridHost = $("<div class='hall-ops-unpaid-popup__grid'/>").appendTo($host);
        const $loadMoreHost = $("<div class='hall-ops-unpaid-popup__footer'/>").appendTo($host);

        let skip = 0;
        let totalCount = 0;
        let rows = [];
        let loading = false;

        function renderSummary() {
            $summary.text(
                totalCount > 0
                    ? `${t("hallOps.unpaidNotify.title")} (${forceEnglishDigits(String(totalCount))})`
                    : t("hallOps.unpaidNotify.title")
            );
        }

        function bindGrid() {
            if ($gridHost.data("dxDataGrid")) {
                $gridHost.dxDataGrid("instance").option("dataSource", rows.slice());
                return;
            }

            $gridHost.dxDataGrid(
                hallPaymentsGridOptions({
                    dataSource: rows,
                    height: Math.min(460, Math.max(280, Math.floor(window.innerHeight * 0.5))),
                    noDataText: t("hallOps.unpaidNotify.empty"),
                    columns: [
                        {
                            dataField: "reservationNo",
                            caption: t("hallOps.col.referenceNo"),
                            width: "12%",
                            minWidth: 96
                        },
                        {
                            dataField: "customerName",
                            caption: t("hallOps.col.occasionOwner") || "Customer",
                            width: "18%",
                            minWidth: 120
                        },
                        {
                            dataField: "eventDate",
                            caption: t("hallOps.col.date"),
                            dataType: "date",
                            width: "11%",
                            customizeText(cellInfo) {
                                return formatGregorianShort(cellInfo.value);
                            }
                        },
                        {
                            dataField: "totalAmount",
                            caption: t("hallOps.col.rent"),
                            width: "12%",
                            customizeText(cellInfo) {
                                return formatMoney(cellInfo.value);
                            }
                        },
                        {
                            dataField: "balanceDue",
                            caption: t("hallOps.col.balance"),
                            width: "12%",
                            cssClass: "hall-ops-unpaid-balance-cell",
                            customizeText(cellInfo) {
                                return formatMoney(cellInfo.value);
                            }
                        },
                        {
                            dataField: "hallName",
                            caption: t("hallOps.col.hall") || "Hall",
                            minWidth: 120
                        }
                    ],
                    onRowClick(e) {
                        const row = e && e.data;
                        const rid = row && (row.reservationId != null ? row.reservationId : row.ReservationId);
                        if (rid != null && rid !== "") {
                            openReservation(rid);
                        }
                    }
                })
            );
        }

        function loadPage(append) {
            if (loading) {
                return $.Deferred().resolve().promise();
            }
            loading = true;
            if (!append) {
                skip = 0;
                rows = [];
            }

            return svc().getUnpaidBalances({
                skip: skip,
                take: UNPAID_NOTIFY_PAGE_SIZE,
                countOnly: false
            }).then((data) => {
                totalCount = Number(data && (data.totalCount ?? data.TotalCount)) || 0;
                unpaidNotifyState.count = totalCount;
                const page = (data && (data.items || data.Items)) || [];
                rows = append ? rows.concat(page) : page.slice();
                skip = rows.length;
                renderSummary();
                bindGrid();
                unpaidNotifyState.count = totalCount;
                updateUnpaidNotifyBadgeUi();

                const $loadBtn = $loadMoreHost.find(".hall-ops-unpaid-load-more");
                if ($loadBtn.length && $loadBtn.data("dxButton")) {
                    $loadBtn.dxButton("instance").option("visible", skip < totalCount);
                }
            }).catch((err) => {
                notify((err && err.message) || t("common.error"), "error");
            }).always(() => {
                loading = false;
            });
        }

        $loadMoreHost.dxButton({
            text: t("hallOps.unpaidNotify.loadMore"),
            stylingMode: "outlined",
            type: "default",
            visible: false,
            elementAttr: { class: "hall-ops-unpaid-load-more" },
            onClick() {
                loadPage(true);
            }
        });

        const $popup = $("<div/>").appendTo("body");
        $popup.dxPopup({
            title: t("hallOps.unpaidNotify.title"),
            contentTemplate: () => $host,
            width: Math.min(980, Math.max(560, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "78vh",
            showCloseButton: true,
            rtlEnabled: isArabicUi(),
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "guest-picker-popup hall-ops-unpaid-notify-popup" },
            onShown() {
                loadPage(false);
            },
            onHidden() {
                unpaidNotifyState.popupOpen = false;
                $popup.remove();
            }
        });
        $popup.dxPopup("instance").show();
    }

    function initHallUnpaidNotifyButton() {
        const $host = $("#hallOpsUnpaidNotifyBtn");
        if (!$host.length || $host.data("hallUnpaidNotifyReady")) {
            return;
        }

        $host.data("hallUnpaidNotifyReady", true).empty();

        const hint = t("hallOps.unpaidNotify.badgeHint");
        const $btn = $("<button>", { type: "button", class: "hall-ops-unpaid-notify-btn" });
        $btn.attr("aria-label", hint);
        $btn.append($("<span class='hall-ops-unpaid-notify-btn__icon dx-icon dx-icon-bell' aria-hidden='true'/>"));
        $btn.append($("<span class='hall-ops-unpaid-notify-btn__badge' aria-hidden='true'/>"));
        $btn.on("click", () => {
            showHallUnpaidNotifyPopup();
        });
        $host.append($btn);

        refreshUnpaidNotifyBadge();
    }

    function buildHallPaymentVoucherContext(reservationId, eventRow, editRow) {
        const routeId = reservationId;
        const reservationNo = eventReservationDisplayNo(eventRow) || "";
        const customerId = eventRow && (eventRow.customerId != null ? eventRow.customerId : eventRow.CustomerId);
        const zaaerId = eventRow && (eventRow.zaaerId != null ? eventRow.zaaerId : eventRow.ZaaerId);
        const hotelId = (editRow && editRow.hotelId) || null;
        const reservationRouteId = zaaerId || routeId;

        return {
            detail: {
                reservationId: routeId,
                zaaerId: zaaerId,
                hotelId: hotelId,
                customerId: customerId,
                header: { reservationNo: reservationNo }
            },
            reservationId: routeId,
            reservationRouteId: reservationRouteId,
            reservationZaaerId: zaaerId,
            hotelId: hotelId,
            customerId: customerId || (editRow && editRow.customerId) || null,
            customerZaaerId: null,
            corporateId: null,
            reservationNo: reservationNo
        };
    }

    function openHallPaymentVoucherEdit(reservationId, row, eventRow, onSaved) {
        if (!row || !row.receiptZaaerId) {
            notify(t("reservationDetail.payments.receipt.missingZaaerId") || t("common.error"), "warning");
            return;
        }

        const voucherUi = window.Zaaer && window.Zaaer.ReservationPaymentVoucherUi;
        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        if (!voucherUi || !svc || typeof svc.loadPaymentRows !== "function") {
            notify(t("common.error"), "error");
            return;
        }

        const isDisbursement = row.docType === "disbursement";
        const kind = isDisbursement ? "disbursements" : "receipts";

        svc.loadPaymentRows({ kind, reservationId })
            .then((rows) => {
                const match = (rows || []).find((r) => Number(r.zaaerId) === Number(row.receiptZaaerId));
                if (!match || !match.zaaerId) {
                    notify(t("activityLog.detailsNotFound") || t("common.error"), "warning");
                    return;
                }

                const context = buildHallPaymentVoucherContext(reservationId, eventRow, match);
                if (!context.hotelId) {
                    notify(t("common.error"), "error");
                    return;
                }

                const afterSave = typeof onSaved === "function"
                    ? () => onSaved()
                    : null;

                if (isDisbursement) {
                    voucherUi.openDisbursementEdit({ context, editRow: match, afterSave });
                } else {
                    voucherUi.openReceiptEdit({ context, editRow: match, afterSave });
                }
            })
            .catch((err) => {
                notify((err && err.message) || t("common.error"), "error");
            });
    }

    function openHallPaymentVoucherPrint(row) {
        const number = row && row.docNo ? String(row.docNo) : "";
        notify(
            (t("reservationDetail.payments.rowAction.print") || "Print {number}").replace("{number}", number),
            "info",
            2600
        );
    }

    function normalizeEventStatusCode(row) {
        if (!row) {
            return "unconfirmed";
        }
        const reservationStatus = `${row.reservationStatus || row.ReservationStatus || ""}`
            .trim()
            .toLowerCase()
            .replace(/[\s_-]+/g, "");
        if (reservationStatus === "checkedout") {
            return "closed";
        }
        if (reservationStatus === "checkedin") {
            return "confirmed";
        }
        const code = `${row.eventStatus || row.EventStatus || ""}`.trim().toLowerCase();
        if (code === "closed" || code === "completed" || code === "cancelled") {
            return "closed";
        }
        if (code === "confirmed" || code === "event_today" || code === "event_running") {
            return "confirmed";
        }
        return "unconfirmed";
    }

    function canCheckInEvent(row) {
        if (!row) {
            return false;
        }
        if (normalizeEventStatusCode(row) !== "unconfirmed") {
            return false;
        }
        const reservationStatus = `${row.reservationStatus || row.ReservationStatus || ""}`
            .toLowerCase()
            .replace(/[\s_-]/g, "");
        return reservationStatus !== "checkedin" && reservationStatus !== "checkedout";
    }

    function creditNoteRowAmount(row) {
        if (!row) {
            return 0;
        }
        const raw = row.creditAmount ?? row.CreditAmount ?? row.amount ?? row.Amount ?? row.totalAmount ?? row.TotalAmount;
        return Number(raw) || 0;
    }

    function eventReservationDisplayNo(e) {
        return readEventField(e, "reservationNo", "ReservationNo") || "";
    }

    function hallPaymentsGridOptions(extra) {
        const po = window.Zaaer && window.Zaaer.PmsGridOptions;
        if (po && typeof po.merge === "function" && typeof po.baseline === "function") {
            return po.merge(po.baseline(), extra || {});
        }
        return Object.assign(
            {
                rtlEnabled: isArabicUi(),
                showBorders: true,
                columnAutoWidth: true,
                wordWrapEnabled: false,
                rowAlternationEnabled: true,
                hoverStateEnabled: true,
                showColumnLines: true,
                showRowLines: true,
                width: "100%",
                columnMinWidth: 64,
                elementAttr: { class: "pms-grid-compact hall-ops-pay-grid" },
                headerFilter: { visible: true, search: { enabled: true } },
                searchPanel: { visible: true, width: 280 },
                scrolling: { mode: "standard", useNative: isArabicUi() }
            },
            extra || {}
        );
    }

    function appendPaySummaryItem($summary, labelKey, value, extraClass) {
        const $item = $("<div/>").addClass(extraClass || "hall-ops-pay-summary__item");
        $("<span class='hall-ops-pay-summary__label'/>").text(`${t(labelKey)}:`).appendTo($item);
        $("<span class='hall-ops-pay-summary__value'/>").text(formatMoney(value)).appendTo($item);
        $summary.append($item);
    }

    function loadHallPaymentsPopupRows(reservationId, eventRow) {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || typeof api.get !== "function") {
            return Promise.reject(new Error("ApiService unavailable"));
        }

        return Promise.all([
            api.get(`/api/v1/pms/payment-receipts/reservation/${encodeURIComponent(reservationId)}`).then(unwrapApiData).catch(() => []),
            api.get(`/api/v1/pms/credit-notes/reservation/${encodeURIComponent(reservationId)}`).then(unwrapApiData).catch(() => [])
        ]).then(([receiptsRaw, creditsRaw]) => {
            const receipts = Array.isArray(receiptsRaw) ? receiptsRaw : [];
            const credits = Array.isArray(creditsRaw) ? creditsRaw : [];
            const receiptRows = receipts.map(mapReceiptToPaymentGridRow);
            const creditRows = credits.map((c) => ({
                sourceKind: "creditNote",
                receiptZaaerId: c.zaaerId ?? c.ZaaerId ?? null,
                receiptId: c.creditNoteId ?? c.CreditNoteId ?? null,
                docNo: c.creditNoteNo || c.CreditNoteNo || c.documentNo || c.DocumentNo || "",
                docType: "disbursement",
                amount: creditNoteRowAmount(c),
                displayAmount: creditNoteRowAmount(c),
                date: c.creditNoteDate || c.CreditNoteDate || c.issueDate || c.IssueDate || c.documentDate || c.DocumentDate || c.createdAt || c.CreatedAt,
                paymentMethod: t("hallOps.col.creditNote"),
                notes: c.reason || c.Reason || "",
                raw: c
            }));
            const rows = receiptRows.concat(creditRows);
            const rent = eventTotalAmountValue(eventRow);
            const totals = summarizePaymentRows(rows, rent);
            const profileDeposit = eventDepositAmountValue(eventRow);
            return { rows, rent, totals, profileDeposit };
        });
    }

    function showHallPaymentsPopup(eventRow) {
        const reservationId = eventRouteReservationId(eventRow);
        if (!reservationId) {
            notify(t("common.error"), "error");
            return;
        }

        loadHallPaymentsPopupRows(reservationId, eventRow).then(({ rows, rent, totals, profileDeposit }) => {
            const reservationNo = eventReservationDisplayNo(eventRow);

            const $host = $("<div class='hall-ops-pay-popup'/>");
            const $summary = $("<div class='hall-ops-pay-summary'/>");
            appendPaySummaryItem($summary, "hallOps.col.rent", rent);
            appendPaySummaryItem($summary, "hallOps.col.deposit", profileDeposit);
            appendPaySummaryItem($summary, "hallOps.col.received", totals.received);
            appendPaySummaryItem($summary, "hallOps.col.disbursement", totals.disbursed);
            appendPaySummaryItem($summary, "hallOps.col.balance", totals.balance, "hall-ops-pay-summary__item hall-ops-pay-summary__item--balance");
            $host.append($summary);

            const $gridHost = $("<div class='hall-ops-pay-grid-host'/>").appendTo($host);

            function refreshPaymentsPopupContent() {
                return loadHallPaymentsPopupRows(reservationId, eventRow).then((payload) => {
                    $summary.empty();
                    appendPaySummaryItem($summary, "hallOps.col.rent", payload.rent);
                    appendPaySummaryItem($summary, "hallOps.col.deposit", payload.profileDeposit);
                    appendPaySummaryItem($summary, "hallOps.col.received", payload.totals.received);
                    appendPaySummaryItem($summary, "hallOps.col.disbursement", payload.totals.disbursed);
                    appendPaySummaryItem(
                        $summary,
                        "hallOps.col.balance",
                        payload.totals.balance,
                        "hall-ops-pay-summary__item hall-ops-pay-summary__item--balance"
                    );
                    const grid = $gridHost.dxDataGrid("instance");
                    if (grid) {
                        grid.option("dataSource", payload.rows);
                    }
                });
            }

            $gridHost.dxDataGrid(
                hallPaymentsGridOptions({
                    dataSource: rows,
                    height: Math.min(420, Math.max(300, Math.floor(window.innerHeight * 0.42))),
                    wordWrapEnabled: true,
                    columnAutoWidth: false,
                    noDataText: t("hallOps.payments.noRows"),
                    columns: [
                        {
                            type: "buttons",
                            width: 96,
                            minWidth: 96,
                            caption: "",
                            cssClass: "hall-ops-pay-grid-actions",
                            buttons: [
                                {
                                    hint: t("reservationDetail.payments.grid.print") || "Print",
                                    icon: "print",
                                    onClick(e) {
                                        openHallPaymentVoucherPrint(e.row.data);
                                    }
                                },
                                {
                                    hint: t("common.edit") || "Edit",
                                    icon: "edit",
                                    visible(e) {
                                        return !!(e.row && e.row.data && e.row.data.receiptZaaerId && e.row.data.sourceKind === "receipt");
                                    },
                                    onClick(e) {
                                        openHallPaymentVoucherEdit(
                                            reservationId,
                                            e.row.data,
                                            eventRow,
                                            () => refreshPaymentsPopupContent()
                                                .then(() => refreshAll())
                                        );
                                    }
                                }
                            ]
                        },
                        {
                            dataField: "docNo",
                            caption: t("hallOps.col.referenceNo"),
                            width: "12%",
                            minWidth: 108,
                            alignment: "center"
                        },
                        {
                            dataField: "docType",
                            caption: t("hallOps.col.type"),
                            width: "11%",
                            minWidth: 100,
                            alignment: "center",
                            customizeText(cellInfo) {
                                return paymentDocTypeLabel(cellInfo.value);
                            }
                        },
                        {
                            dataField: "date",
                            caption: t("hallOps.col.date"),
                            dataType: "date",
                            width: "11%",
                            minWidth: 104,
                            alignment: "center",
                            customizeText(cellInfo) {
                                return formatGregorianShort(cellInfo.value);
                            }
                        },
                        {
                            dataField: "paymentMethod",
                            caption: t("hallOps.col.paymentMethod"),
                            width: "12%",
                            minWidth: 108,
                            alignment: "center"
                        },
                        {
                            dataField: "displayAmount",
                            caption: t("hallOps.col.amount"),
                            dataType: "number",
                            width: "10%",
                            minWidth: 96,
                            alignment: "center",
                            customizeText(cellInfo) {
                                const row = cellInfo.data || {};
                                if (row.docType === "disbursement" || Number(row.amount) < 0) {
                                    return forceEnglishDigits(`-${formatMoney(Math.abs(Number(row.amount) || 0))}`);
                                }
                                return formatMoney(cellInfo.value);
                            }
                        },
                        {
                            dataField: "notes",
                            caption: t("hallOps.col.notes"),
                            width: "44%",
                            minWidth: 220,
                            alignment: "center",
                            cssClass: "hall-ops-pay-grid-notes"
                        }
                    ]
                })
            );

            const $popup = $("<div/>").appendTo("body");
            $popup.dxPopup({
                title: `${t("hallOps.payments.title")} #${reservationNo || reservationId}`,
                contentTemplate: () => $host,
                width: Math.min(1180, Math.max(560, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "78vh",
                showCloseButton: true,
                rtlEnabled: isArabicUi(),
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                wrapperAttr: { class: "guest-picker-popup hall-ops-payments-popup" },
                onHidden() {
                    $popup.remove();
                }
            });
            $popup.dxPopup("instance").show();
        }).catch((err) => {
            notify((err && err.message) || t("common.error"), "error");
        });
    }

    function buildCardContextMenuItems(reservationId) {
        const rid = Number(reservationId);
        if (!Number.isFinite(rid) || rid <= 0) {
            return [];
        }
        const row = findEventByReservationId(rid);
        const transitions = row
            ? eventAllowedTransitions(row).map((code) => ({
                text: eventStatusLabel(code),
                icon: eventStatusIconName(code),
                action: "transition",
                statusCode: code,
                reservationId: rid
            }))
            : [];
        const operationItems = [];
        if (row && canCheckInEvent(row)) {
            operationItems.push({
                text: t("hallOps.action.checkIn"),
                icon: "check",
                action: "checkIn",
                reservationId: rid
            });
        }
        return [
            { text: t("hallOps.action.openReservation"), icon: "doc", action: "open", reservationId: rid },
            { text: t("hallOps.editEvent"), icon: "edit", action: "edit", reservationId: rid },
            { text: t("hallOps.payments.title"), icon: "money", action: "payments", reservationId: rid },
            operationItems.length ? { text: t("hallOps.action.operations"), icon: "preferences", items: operationItems } : null,
            transitions.length ? { text: t("hallOps.col.eventStatus"), icon: "event", items: transitions } : null
        ].filter(Boolean);
    }

    function extractRackAppointmentData($appointmentEl, schedulerInst) {
        if (!$appointmentEl || !$appointmentEl.length) {
            return null;
        }
        const stored = $appointmentEl.data("hallOpsAppt");
        if (stored) {
            return stored;
        }
        const $card = $appointmentEl.find(".hall-ops-event-card").first();
        const rid = Number($card.attr("data-reservation-id"));
        if (Number.isFinite(rid) && rid > 0) {
            return { reservationId: rid };
        }
        if (schedulerInst && typeof schedulerInst.getTargetedAppointmentData === "function") {
            try {
                return schedulerInst.getTargetedAppointmentData($appointmentEl);
            } catch (_) {
                return null;
            }
        }
        return null;
    }

    function resolveHallOpsEventCardReservationId($from) {
        if (!$from || !$from.length) {
            return null;
        }
        let rid = Number($from.attr("data-reservation-id"));
        if (Number.isFinite(rid) && rid > 0) {
            return rid;
        }
        if ($from.is(".dx-scheduler-appointment")) {
            const $pane = $from.closest(".hall-ops-calendar-pane");
            const inst = $pane.length && $pane.data("dxScheduler")
                ? $pane.dxScheduler("instance")
                : null;
            const data = extractRackAppointmentData($from, inst);
            rid = Number(data && (data.reservationId != null ? data.reservationId : data.ReservationId));
            if (Number.isFinite(rid) && rid > 0) {
                return rid;
            }
        }
        const $card = $from.closest(".hall-ops-event-card, .hall-ops-cell-card, .hall-ops-agenda-card");
        if ($card.length) {
            rid = Number($card.attr("data-reservation-id"));
            if (Number.isFinite(rid) && rid > 0) {
                return rid;
            }
        }
        return null;
    }

    function initMonthCardContextMenu() {
        if (window.__hallOpsCardContextMenuInited) {
            return;
        }
        window.__hallOpsCardContextMenuInited = true;

        let currentMenuReservationId = null;
        let currentMenuAnchor = null;
        let currentMenuPositionEvt = null;
        let checkInBusy = false;
        let menuInst = null;
        let contextMenuOpenGuardTs = 0;

        const $menuHost = $("<div id='hallOpsCardContextMenu'/>").appendTo("body");
        $menuHost.dxContextMenu({
            // Do NOT set target — it overrides position.of and pins the menu to body.
            showEvent: "",
            width: 280,
            rtlEnabled: isArabicUi(),
            hideOnOutsideClick: true,
            focusStateEnabled: false,
            cssClass: "hall-ops-card-context-menu",
            elementAttr: { class: "hall-ops-card-context-menu" },
            items: [],
            onShowing(e) {
                const reservationId = Number(currentMenuReservationId);
                const items = buildCardContextMenuItems(reservationId);
                if (!items.length) {
                    e.cancel = true;
                    return;
                }
                e.component.option("items", items);
                e.component.option("position", contextMenuPositionForAnchor(currentMenuAnchor, currentMenuPositionEvt));
            },
            onItemClick(e) {
                const item = e.itemData || {};
                const reservationId = Number(item.reservationId);
                const row = findEventByReservationId(reservationId);
                if (!reservationId) {
                    return;
                }
                if (item.action === "open") {
                    openReservation(reservationId);
                    return;
                }
                if (item.action === "edit") {
                    if (row) {
                        showEditEventPopup(row);
                    } else {
                        openReservation(reservationId);
                    }
                    return;
                }
                if (item.action === "payments") {
                    if (row) {
                        showHallPaymentsPopup(row);
                    } else {
                        openReservation(reservationId);
                    }
                    return;
                }
                if (item.action === "checkIn") {
                    if (!row || checkInBusy) {
                        return;
                    }
                    checkInBusy = true;
                    svc().checkInEvent(reservationId).then(() => {
                        notify(t("hallOps.checkInOk") || t("hallOps.statusUpdated"), "success");
                        refreshAll();
                    }).catch((err) => {
                        const msg = (err && (err.message || err.Message))
                            || (err && err.responseJSON && (err.responseJSON.message || err.responseJSON.Message))
                            || t("common.error");
                        notify(msg, "error");
                    }).always(() => {
                        checkInBusy = false;
                    });
                    return;
                }
                if (item.action === "transition" && item.statusCode && row) {
                    attemptHallEventStatusChange(reservationId, item.statusCode);
                }
            }
        });
        menuInst = $menuHost.dxContextMenu("instance");

        function toContextMenuJqEvent(evt) {
            if (!evt) {
                return null;
            }
            if (evt.originalEvent != null || evt.pageX != null) {
                return evt;
            }
            return $.Event("contextmenu", evt);
        }

        function contextMenuPositionForAnchor($anchor, evt) {
            if ($anchor && $anchor.length) {
                return {
                    of: $anchor,
                    my: "left top",
                    at: "right top",
                    collision: "flip fit"
                };
            }
            const jqEvt = toContextMenuJqEvent(evt);
            if (jqEvt) {
                return {
                    of: jqEvt,
                    my: "left top",
                    at: "right top",
                    collision: "flip fit"
                };
            }
            return {
                my: "left top",
                at: "left top",
                of: window,
                collision: "flip fit"
            };
        }

        function openEventCardContextMenu(reservationId, evt, $anchor) {
            const rid = Number(reservationId);
            if (!Number.isFinite(rid) || rid <= 0 || !menuInst) {
                return;
            }
            const now = Date.now();
            if (now - contextMenuOpenGuardTs < 120) {
                return;
            }
            const items = buildCardContextMenuItems(rid);
            if (!items.length) {
                return;
            }
            contextMenuOpenGuardTs = now;
            currentMenuReservationId = rid;
            currentMenuAnchor = $anchor && $anchor.length ? $anchor : null;
            currentMenuPositionEvt = evt || null;
            try {
                menuInst.hide();
            } catch (_) {
                /* not open */
            }
            menuInst.option("items", items);
            menuInst.option("position", contextMenuPositionForAnchor(currentMenuAnchor, evt));
            menuInst.show();
        }

        window.__hallOpsOpenEventCardContextMenu = openEventCardContextMenu;

        function handleHallOpsEventCardContextMenu(evt) {
            const $target = $(evt.target);
            const $card = $target.closest(".hall-ops-event-card, .hall-ops-cell-card, .hall-ops-agenda-card");
            const $appt = $card.length ? $() : $target.closest(".dx-scheduler-appointment");
            const $host = $card.length ? $card : $appt;
            if (!$host.length) {
                return;
            }
            const rid = resolveHallOpsEventCardReservationId($host);
            if (!rid) {
                return;
            }
            evt.preventDefault();
            evt.stopPropagation();
            openEventCardContextMenu(rid, evt, $host);
        }

        window.__hallOpsHandleEventCardContextMenu = handleHallOpsEventCardContextMenu;

        if (!window.__hallOpsCardContextMenuCapture) {
            window.__hallOpsCardContextMenuCapture = true;
            document.addEventListener("contextmenu", (evt) => {
                if (!evt || !evt.target || typeof evt.target.closest !== "function") {
                    return;
                }
                if (!evt.target.closest(".hall-operations-page")) {
                    return;
                }
                const cardEl = evt.target.closest(
                    ".hall-ops-event-card, .hall-ops-cell-card, .hall-ops-agenda-card, .dx-scheduler-appointment"
                );
                if (!cardEl) {
                    return;
                }
                if (typeof window.__hallOpsHandleEventCardContextMenu === "function") {
                    window.__hallOpsHandleEventCardContextMenu(evt);
                }
            }, true);
        }
    }

    function wireHallOpsCalendarContextMenu($root) {
        if (!$root || !$root.length) {
            return;
        }
        ensureHallOpsEventContextMenu();
        $root.off("contextmenu.hallOpsEventCtx");
        $root.on(
            "contextmenu.hallOpsEventCtx",
            ".hall-ops-event-card, .hall-ops-cell-card, .hall-ops-agenda-card, .dx-scheduler-appointment",
            function (evt) {
                if (typeof window.__hallOpsHandleEventCardContextMenu === "function") {
                    window.__hallOpsHandleEventCardContextMenu(evt);
                }
            }
        );
    }

    function ensureHallOpsEventContextMenu() {
        initMonthCardContextMenu();
    }

    function hallOpsSchedulerContextMenuHandler(e) {
        const evt = e && e.event;
        if (!evt) {
            return;
        }
        ensureHallOpsEventContextMenu();

        const appt = e && (e.appointmentData || e.targetedAppointmentData);
        const ridFromAppt = appt && Number(
            appt.reservationId != null ? appt.reservationId : appt.ReservationId
        );
        if (Number.isFinite(ridFromAppt) && ridFromAppt > 0) {
            evt.preventDefault();
            evt.stopPropagation();
            let $anchor = e.appointmentElement ? $(e.appointmentElement) : $();
            if (!$anchor.length) {
                $anchor = $(evt.target).closest(".dx-scheduler-appointment, .hall-ops-event-card, .hall-ops-cell-card");
            }
            if (typeof window.__hallOpsOpenEventCardContextMenu === "function") {
                window.__hallOpsOpenEventCardContextMenu(ridFromAppt, evt, $anchor);
            }
            return;
        }

        if (typeof window.__hallOpsHandleEventCardContextMenu === "function") {
            window.__hallOpsHandleEventCardContextMenu(evt);
        }
    }

    function dashboardWindowRange() {
        const now = normalizeLocalDate(new Date()) || new Date();
        if (periodFilter.visible && periodFilter.applied && periodFilter.from && periodFilter.to) {
            return {
                from: normalizeLocalDate(periodFilter.from) || now,
                to: normalizeLocalDate(periodFilter.to) || now
            };
        }
        const from = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 90);
        return { from, to: now };
    }

    function eventsInWindow(range) {
        const fromKey = formatLocalDateParam(range.from);
        const toKey = formatLocalDateParam(range.to);
        return eventsCache.filter((e) => {
            const k = formatLocalDateParam(e.eventDate != null ? e.eventDate : e.EventDate);
            return k && k >= fromKey && k <= toKey;
        });
    }

    function readEventField(item, camelKey, pascalKey) {
        if (!item) {
            return "";
        }
        const value = item[camelKey] != null ? item[camelKey] : item[pascalKey];
        return value == null ? "" : `${value}`.trim();
    }

    function eventCustomerName(e) {
        if (!e) {
            return "";
        }
        return (
            readEventField(e, "occasionOwner", "OccasionOwner") ||
            readEventField(e, "customerName", "CustomerName") ||
            readEventField(e, "reservationNo", "ReservationNo") ||
            (e.reservationId != null ? `#${e.reservationId}` : "")
        );
    }

    function formatAppointmentShort(e) {
        return eventCustomerName(e);
    }

    function formatMoney(value) {
        const n = Number(value || 0);
        return forceEnglishDigits(n.toLocaleString("en-US", { minimumFractionDigits: 0, maximumFractionDigits: 2 }));
    }

    function formatGregorianShort(value) {
        if (!value) {
            return "";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const day = String(d.getDate()).padStart(2, "0");
        const month = String(d.getMonth() + 1).padStart(2, "0");
        return forceEnglishDigits(`${day}/${month}/${d.getFullYear()}`);
    }

    function formatGregorianLong(value) {
        if (!value) {
            return "";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const culture = (window.Zaaer && window.Zaaer.LocalizationService
            && window.Zaaer.LocalizationService.currentCulture() === "ar") ? "ar-SA" : "en-GB";
        return forceEnglishDigits(d.toLocaleDateString(culture, { day: "numeric", month: "long", year: "numeric" }));
    }

    function normalizeSchedulerTimeString(value, fallbackTime) {
        const raw = `${value || ""}`.trim();
        if (!raw) {
            return fallbackTime;
        }
        const hms = /^(\d{1,2}):(\d{2})(?::(\d{2}))?$/;
        const dateTime = /T(\d{1,2}):(\d{2})(?::(\d{2}))?/;
        const m = raw.match(hms) || raw.match(dateTime);
        if (!m) {
            return fallbackTime;
        }
        const hh = String(Math.min(23, Math.max(0, Number(m[1]) || 0))).padStart(2, "0");
        const mm = String(Math.min(59, Math.max(0, Number(m[2]) || 0))).padStart(2, "0");
        const ss = String(Math.min(59, Math.max(0, Number(m[3]) || 0))).padStart(2, "0");
        return `${hh}:${mm}:${ss}`;
    }

    function buildSchedulerDateTime(eventDateRaw, timeRaw, fallbackTime) {
        const dateKey = formatLocalDateParam(eventDateRaw);
        const timeKey = normalizeSchedulerTimeString(timeRaw, fallbackTime);
        const parsed = new Date(`${dateKey}T${timeKey}`);
        if (!Number.isNaN(parsed.getTime())) {
            return parsed;
        }
        const base = eventDateRaw instanceof Date ? eventDateRaw : new Date(eventDateRaw);
        if (Number.isNaN(base.getTime())) {
            return new Date();
        }
        const parts = timeKey.split(":");
        base.setHours(Number(parts[0]) || 0, Number(parts[1]) || 0, Number(parts[2]) || 0, 0);
        return base;
    }

    function formatEventDualDateLabel(e) {
        const eventDate = e.eventDate != null ? e.eventDate : e.EventDate;
        const greg = formatGregorianShort(eventDate);
        const hijri = e.eventDateHijriDisplay || e.eventDateHijri || e.EventDateHijri
            || formatEventHijriLabel(eventDate, e.eventDateHijri || e.EventDateHijri);
        const suffix = t("hallOps.hijriSuffix") || "H";
        if (greg && hijri) {
            return `${greg} · ${hijri} ${suffix}`;
        }
        return greg || hijri || "";
    }

    function buildAppointmentMeta(e) {
        return {
            customerLabel: eventCustomerName(e),
            occasionLabel: readEventField(e, "occasionName", "OccasionName"),
            hallLabel: readEventField(e, "hallName", "HallName"),
            dateLabel: formatEventDualDateLabel(e),
            rentLabel: formatMoney(e.totalAmount != null ? e.totalAmount : e.TotalAmount),
            depositLabel: formatMoney(e.depositAmount != null ? e.depositAmount : e.DepositAmount)
        };
    }

    function buildSchedulerAppointmentTemplate() {
        return function appointmentTemplate(model, index, element) {
            const data = (model && model.appointmentData)
                || (model && model.targetedAppointmentData)
                || model
                || {};
            const owner = (data.customerLabel || data.text || data.shortText || "").trim();
            const hall = (data.hallLabel || "").trim();
            const occasion = (data.occasionLabel || "").trim();
            const rent = (data.rentLabel || "").trim();
            const deposit = (data.depositLabel || "").trim();
            const accent = data.color || "var(--pms-primary, #3f6f9f)";
            const $appt = stampHallOpsEventCard(
                $("<div class='hall-ops-appt hall-ops-appt--month hall-ops-appt--rich'/>"),
                data
            ).css("--appt-accent", accent);
            $appt.append($("<span class='hall-ops-appt__bar' aria-hidden='true'/>"));
            const $body = $("<div class='hall-ops-appt__body'/>").appendTo($appt);
            const $head = $("<div class='hall-ops-appt__head'/>").appendTo($body);
            appendEventStatusIcon($head, { eventStatus: data.eventStatusRaw || data.eventStatus }, "hall-ops-appt__status-icon");
            if (owner) {
                $("<div class='hall-ops-appt__title'/>").text(owner).attr("title", owner).appendTo($head);
            }
            const metaParts = [];
            if (hall) {
                metaParts.push(hall);
            }
            if (occasion && occasion !== owner) {
                metaParts.push(occasion);
            }
            if (metaParts.length) {
                const metaText = metaParts.join(" · ");
                $("<div class='hall-ops-appt__meta'/>").text(metaText).attr("title", metaText).appendTo($body);
            }
            if (deposit || rent) {
                const $money = $("<div class='hall-ops-appt__money'/>").appendTo($body);
                if (deposit) {
                    $("<span/>").text(`${t("hallOps.col.deposit")}: ${deposit}`).appendTo($money);
                }
                if (rent) {
                    $("<span/>").text(`${t("hallOps.col.rent")}: ${rent}`).appendTo($money);
                }
            }
            if (element) {
                $(element).empty().append($appt);
                return;
            }
            return $appt;
        };
    }

    function syncSchedulerAfterData(inst) {
        if (!inst || typeof inst.updateDimensions !== "function") {
            return;
        }
        window.requestAnimationFrame(() => {
            try {
                const result = inst.updateDimensions();
                const repaint = () => {
                    try {
                        if (typeof inst.repaint === "function") {
                            inst.repaint();
                        }
                    } catch (_) {
                        /* ignore */
                    }
                };
                if (result && typeof result.done === "function") {
                    result.done(repaint);
                } else {
                    repaint();
                }
            } catch (_) {
                /* ignore layout sync errors */
            }
        });
    }

    function repaintHallSchedulers() {
        eachCalendarScheduler((inst) => {
            syncSchedulerAfterData(inst);
        });
    }

    function initSchedulerLayoutSync() {
        if (window.__hallOpsSchedulerLayoutSync) {
            return;
        }
        window.__hallOpsSchedulerLayoutSync = true;
        const navToggle = document.getElementById("roomBoardNavToggle");
        if (navToggle) {
            navToggle.addEventListener("click", () => {
                window.setTimeout(repaintHallSchedulers, 320);
            });
        }
        const schedShell = document.querySelector(".hall-ops-calendar-shell");
        if (schedShell && window.ResizeObserver) {
            let timer;
            new ResizeObserver(() => {
                clearTimeout(timer);
                timer = window.setTimeout(repaintHallSchedulers, 80);
            }).observe(schedShell);
        }
        $(window).on("resize.hallOpsScheduler", () => {
            clearTimeout(window.__hallOpsSchedResize);
            window.__hallOpsSchedResize = window.setTimeout(repaintHallSchedulers, 120);
        });
    }

    function buildListEventsParams(from, to) {
        const params = { fromDate: from, toDate: to };
        if (periodFilter.visible && periodFilter.applied && periodFilter.from && periodFilter.to) {
            params.fromDate = periodFilter.from;
            params.toDate = periodFilter.to;
        }
        if (statusFilter.eventStatus) {
            params.eventStatus = statusFilter.eventStatus;
        }
        return params;
    }

    function resolveEventLoadRange(anchorDate, viewName) {
        if (periodFilter.visible && periodFilter.applied && periodFilter.from && periodFilter.to) {
            return { from: periodFilter.from, to: periodFilter.to };
        }
        const anchor = calendarNavFilter.applied && calendarNavFilter.anchorGregorian
            ? calendarNavFilter.anchorGregorian
            : anchorDate;
        return calendarLoadRange(viewName || schedulerRangeViewName(), normalizeRangeAnchor(anchor));
    }

    function isArabicUi() {
        return !!(
            window.Zaaer &&
            window.Zaaer.LocalizationService &&
            typeof window.Zaaer.LocalizationService.currentCulture === "function" &&
            window.Zaaer.LocalizationService.currentCulture() === "ar"
        );
    }

    function eventStatusLabel(code) {
        if (!code || !lookups || !lookups.eventStatuses) {
            return code || "";
        }
        const row = lookups.eventStatuses.find((s) => s.value === code || s.Value === code);
        if (!row) {
            return code;
        }
        return isArabicUi() ? (row.labelAr || row.LabelAr || code) : (row.labelEn || row.LabelEn || code);
    }

    function eventStatusColor(code) {
        if (!code || !lookups || !lookups.eventStatuses) {
            return "#94a3b8";
        }
        const row = lookups.eventStatuses.find((s) => s.value === code || s.Value === code);
        return (row && (row.color || row.Color)) || "#94a3b8";
    }

    function eventTypeLabel(code) {
        if (!code) {
            return "";
        }
        const key = `hallOps.eventType.${code}`;
        const label = t(key);
        return label === key ? code : label;
    }

    function eventTypeSelectOptions() {
        const codes = (lookups && lookups.eventTypes) || [];
        return codes.map((code) => ({
            value: code,
            text: eventTypeLabel(code)
        }));
    }

    function eventTypeSelectBoxOptions() {
        return {
            dataSource: eventTypeSelectOptions(),
            valueExpr: "value",
            displayExpr: "text",
            searchEnabled: true
        };
    }

    function buildAppliedDateLabel() {
        if (!calendarNavFilter.applied || !calendarNavFilter.anchorGregorian) {
            return "";
        }
        const greg = formatGregorianLong(calendarNavFilter.anchorGregorian);
        const hijriLib = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        let hijri = "";
        if (hijriLib && typeof hijriLib.formatHijriLongFromGregorian === "function") {
            hijri = hijriLib.formatHijriLongFromGregorian(calendarNavFilter.anchorGregorian);
        }
        if (!hijri && calendarNavFilter.eventDateHijri) {
            hijri = calendarNavFilter.eventDateHijri;
        }
        const suffix = t("hallOps.hijriSuffix") || "H";
        return hijri ? `${greg} — ${hijri} ${suffix}` : greg;
    }

    function updateFilterAppliedBanner() {
        if (!filterAppliedBannerEl || !filterAppliedBannerEl.length) {
            return;
        }
        if (!calendarNavFilter.applied) {
            filterAppliedBannerEl.hide().empty();
            return;
        }
        calendarNavFilter.displayLabel = buildAppliedDateLabel();
        filterAppliedBannerEl
            .show()
            .empty()
            .append($("<span class='hall-ops-filter-applied__label'/>").text(`${t("hallOps.filter.applied")}:`))
            .append($("<strong class='hall-ops-filter-applied__value'/>").text(calendarNavFilter.displayLabel));
    }

    function getCalendarViewHost() {
        return $(".hall-ops-view-host[data-view='calendar']").first();
    }

    function isMobileCalendarLayout() {
        return window.matchMedia && window.matchMedia(CALENDAR_LAYOUT_MOBILE_MQ).matches;
    }

    function normalizeCalendarLayout(value) {
        const v = `${value || ""}`.trim().toLowerCase();
        if (v === "timeline" || v === "agenda" || v === "month") {
            return v;
        }
        return "month";
    }

    function resolveEffectiveCalendarLayout(preferred) {
        const stored = preferred != null ? preferred : safeLocalStorageGet(CALENDAR_LAYOUT_STORAGE_KEY);
        const norm = normalizeCalendarLayout(stored);
        if (isMobileCalendarLayout()) {
            return norm === "timeline" ? "agenda" : (norm === "agenda" ? "agenda" : "month");
        }
        if (norm === "agenda") {
            return "timeline";
        }
        return norm === "timeline" ? "timeline" : "month";
    }

    function getActiveCalendarLayout() {
        const $host = getCalendarViewHost();
        if ($host.length && $host.data("activeCalendarLayout")) {
            return $host.data("activeCalendarLayout");
        }
        return resolveEffectiveCalendarLayout();
    }

    function normalizeCalendarMonthSpan(value) {
        const n = Number(value);
        return CALENDAR_MONTH_SPAN_OPTIONS.includes(n) ? n : 1;
    }

    function getCalendarMonthSpan() {
        return normalizeCalendarMonthSpan(safeLocalStorageGet(CALENDAR_MONTH_SPAN_STORAGE_KEY));
    }

    function calendarMonthSpanOptions() {
        return CALENDAR_MONTH_SPAN_OPTIONS.map((n) => ({
            value: n,
            text: t(`hallOps.calendarSpan.${n}`)
        }));
    }

    function monthCalendarHeight() {
        const span = getCalendarMonthSpan();
        return Math.min(920, 380 + span * 280);
    }

    function patchSchedulerViewIntervalCount(views, viewName, intervalCount) {
        return (views || []).map((view) => {
            if (view.name === viewName) {
                return Object.assign({}, view, { intervalCount: intervalCount });
            }
            return view;
        });
    }

    function applyCalendarMonthSpanToSchedulers(span) {
        const normalized = normalizeCalendarMonthSpan(span);
        safeLocalStorageSet(CALENDAR_MONTH_SPAN_STORAGE_KEY, normalized);

        const $month = $(".hall-ops-calendar-pane[data-calendar-pane='month']");
        if ($month.data("dxScheduler")) {
            const inst = $month.dxScheduler("instance");
            const views = patchSchedulerViewIntervalCount(inst.option("views"), "month", normalized);
            suppressSchedulerOptionChanged = true;
            inst.option("views", views);
            if (inst.option("currentView") === "month") {
                inst.option("height", monthCalendarHeight());
            }
            suppressSchedulerOptionChanged = false;
            reloadMonthCalendarData($month);
        }

        const $timeline = $(".hall-ops-calendar-pane[data-calendar-pane='timeline']");
        if ($timeline.data("dxScheduler")) {
            const inst = $timeline.dxScheduler("instance");
            const views = patchSchedulerViewIntervalCount(inst.option("views"), "hallRack", normalized);
            suppressSchedulerOptionChanged = true;
            inst.option("views", views);
            suppressSchedulerOptionChanged = false;
            reloadTimelineCalendarData($timeline);
        }
    }

    function syncCalendarMonthSpanUi($root) {
        const $calendarRoot = $root && $root.length ? $root : getCalendarViewHost();
        if (!$calendarRoot.length) {
            return;
        }
        const layout = getActiveCalendarLayout();
        const $host = $calendarRoot.find(".hall-ops-calendar-span-host");
        $host.toggleClass("hall-ops-calendar-span-host--hidden", layout === "agenda");

        const $select = $calendarRoot.find(".hall-ops-calendar-span-select");
        if (!$select.data("dxSelectBox")) {
            return;
        }
        const selectInst = $select.dxSelectBox("instance");
        selectInst.option("value", getCalendarMonthSpan());

        let disabled = layout === "agenda";
        if (layout === "month") {
            const $month = $calendarRoot.find("[data-calendar-pane='month']");
            if ($month.data("dxScheduler")) {
                disabled = $month.dxScheduler("instance").option("currentView") === "year";
            }
        }
        selectInst.option("disabled", disabled);
    }

    function initCalendarMonthSpanSelect($host, $calendarRoot) {
        if ($host.data("dxSelectBox")) {
            syncCalendarMonthSpanUi($calendarRoot);
            return;
        }
        $host.addClass("hall-ops-calendar-span-host");
        const $select = $("<div class='hall-ops-calendar-span-select'/>").appendTo($host);
        $select.dxSelectBox({
            dataSource: calendarMonthSpanOptions(),
            valueExpr: "value",
            displayExpr: "text",
            value: getCalendarMonthSpan(),
            width: 148,
            label: t("hallOps.calendarSpan.label"),
            labelMode: "floating",
            elementAttr: { class: "hall-ops-calendar-span-field" },
            onValueChanged(e) {
                const span = normalizeCalendarMonthSpan(e.value);
                if (span === getCalendarMonthSpan()) {
                    return;
                }
                applyCalendarMonthSpanToSchedulers(span);
            }
        });
        syncCalendarMonthSpanUi($calendarRoot);
    }

    function calendarLayoutSwitchItems() {
        if (isMobileCalendarLayout()) {
            return [
                { id: "month", icon: "event", text: t("hallOps.calendarLayout.month") },
                { id: "agenda", icon: "bulletlist", text: t("hallOps.calendarLayout.agenda") }
            ];
        }
        return [
            { id: "month", icon: "event", text: t("hallOps.calendarLayout.month") },
            { id: "timeline", icon: "chart", text: t("hallOps.calendarLayout.timeline") }
        ];
    }

    function formatEventTimeLabel(timeRaw) {
        const timeKey = normalizeSchedulerTimeString(timeRaw, "00:00:00");
        const parts = timeKey.split(":");
        const h = Number(parts[0]);
        const m = Number(parts[1]);
        if (!Number.isFinite(h)) {
            return "";
        }
        const d = new Date(2000, 0, 1, h, m || 0, 0);
        const culture = isArabicUi() ? "ar-SA" : "en-GB";
        return forceEnglishDigits(d.toLocaleTimeString(culture, { hour: "numeric", minute: "2-digit" }));
    }

    function formatEventTimeRangeLabel(e) {
        const start = formatEventTimeLabel(readEventField(e, "eventStartTime", "EventStartTime"));
        const end = formatEventTimeLabel(readEventField(e, "eventEndTime", "EventEndTime"));
        if (start && end) {
            return `${start} – ${end}`;
        }
        return start || end || "";
    }

    function hallResourceDataSource() {
        const map = new Map();
        const halls = (lookups && lookups.halls) || [];
        halls.forEach((h) => {
            const id = Number(h.hallId != null ? h.hallId : h.HallId != null ? h.HallId : h.id);
            if (!Number.isFinite(id) || id <= 0) {
                return;
            }
            map.set(id, {
                id: id,
                text: h.hallName || h.HallName || h.hallCode || h.HallCode || String(id),
                color: "var(--pms-primary, #3f6f9f)"
            });
        });
        eventsCache.forEach((e) => {
            const id = Number(e.hallId != null ? e.hallId : e.HallId);
            if (!Number.isFinite(id) || id <= 0) {
                return;
            }
            if (!map.has(id)) {
                map.set(id, {
                    id: id,
                    text: readEventField(e, "hallName", "HallName") || String(id),
                    color: resolveEventAccentColor(e)
                });
            }
        });
        const list = Array.from(map.values()).sort((a, b) =>
            `${a.text || ""}`.localeCompare(`${b.text || ""}`, isArabicUi() ? "ar" : "en")
        );
        if (!list.length) {
            list.push({
                id: 0,
                text: t("hallOps.calendarLayout.noHall"),
                color: "#94a3b8"
            });
        }
        return list;
    }

    function timelineSchedulerHeight() {
        const count = hallResourceDataSource().length;
        return Math.min(900, Math.max(420, count * 72 + 96));
    }

    function buildTimelineAppointmentTemplate() {
        return function appointmentTemplate(model, _index, element) {
            const data = (model && model.appointmentData)
                || (model && model.targetedAppointmentData)
                || model
                || {};
            const owner = (data.customerLabel || data.text || "").trim();
            const occasion = (data.occasionLabel || "").trim();
            const rent = (data.rentLabel || "").trim();
            const deposit = (data.depositLabel || "").trim();
            const timeLabel = (data.timeLabel || "").trim();
            const accent = data.color || "var(--pms-primary, #3f6f9f)";
            const $appt = stampHallOpsEventCard(
                $("<div class='hall-ops-appt hall-ops-appt--rack hall-ops-appt--rich'/>"),
                data
            ).css("--appt-accent", accent);
            $appt.append($("<span class='hall-ops-appt__bar' aria-hidden='true'/>"));
            const $body = $("<div class='hall-ops-appt__body'/>").appendTo($appt);
            if (timeLabel) {
                $("<div class='hall-ops-appt__time'/>").text(forceEnglishDigits(timeLabel)).appendTo($body);
            }
            const $head = $("<div class='hall-ops-appt__head'/>").appendTo($body);
            appendEventStatusIcon($head, { eventStatus: data.eventStatusRaw || data.eventStatus }, "hall-ops-appt__status-icon");
            if (owner) {
                $("<div class='hall-ops-appt__title'/>").text(owner).attr("title", owner).appendTo($head);
            }
            if (occasion && occasion !== owner) {
                $("<div class='hall-ops-appt__meta'/>").text(occasion).attr("title", occasion).appendTo($body);
            }
            if (deposit || rent) {
                const $money = $("<div class='hall-ops-appt__money'/>").appendTo($body);
                if (deposit) {
                    $("<span/>").text(`${t("hallOps.col.deposit")}: ${deposit}`).appendTo($money);
                }
                if (rent) {
                    $("<span/>").text(`${t("hallOps.col.rent")}: ${rent}`).appendTo($money);
                }
            }
            if (element) {
                const $root = $(element).empty();
                $root.append($appt);
                $root.data("hallOpsAppt", data);
                $appt.data("hallOpsAppt", data);
                $root.closest(".dx-scheduler-appointment").data("hallOpsAppt", data);
                return;
            }
            return $appt;
        };
    }

    function schedulerRackDateCellTemplates() {
        const suffix = t("hallOps.hijriSuffix") || "H";
        return {
            dateCellTemplate(model) {
                const data = schedulerTemplateCellData(model);
                const date = data.date;
                if (!date) {
                    return $("<div/>");
                }
                const dow = formatWeekdayName(date, "long");
                const greg = forceEnglishDigits(formatGregorianShort(date));
                const hijri = forceEnglishDigits(schedulerHijriFullDateLabel(date));
                const $cell = $("<div class='hall-ops-rack-datehdr'/>");
                if (dow) {
                    $cell.append($("<span class='hall-ops-rack-datehdr__dow'/>").text(dow));
                }
                const $dates = $("<div class='hall-ops-rack-datehdr__dates' dir='ltr'/>");
                $dates.append($("<span class='hall-ops-rack-datehdr__day'/>").text(greg));
                if (hijri) {
                    $dates.append(
                        $("<span class='hall-ops-rack-datehdr__h'/>").text(`${hijri} ${suffix}`.trim())
                    );
                }
                $cell.append($dates);
                return $cell;
            }
        };
    }

    function schedulerRackAppointments() {
        return eventsCache.map((e) => {
            const meta = buildAppointmentMeta(e);
            const eventDateRaw = e.eventDate != null ? e.eventDate : e.EventDate;
            const startDate = buildSchedulerDateTime(eventDateRaw, "00:00:00", "00:00:00");
            const endDate = buildSchedulerDateTime(eventDateRaw, "23:59:00", "23:59:00");
            const hallRaw = e.hallId != null ? e.hallId : e.HallId;
            const hallId = Number(hallRaw);
            return {
                reservationId: e.reservationId != null ? e.reservationId : e.ReservationId,
                text: meta.customerLabel || t("hallOps.col.occasionOwner"),
                shortText: meta.customerLabel,
                customerLabel: meta.customerLabel,
                occasionLabel: meta.occasionLabel,
                hallLabel: meta.hallLabel,
                dateLabel: meta.dateLabel,
                rentLabel: meta.rentLabel,
                depositLabel: meta.depositLabel,
                timeLabel: formatEventTimeRangeLabel(e),
                startDate: startDate,
                endDate: endDate,
                hallId: Number.isFinite(hallId) && hallId > 0 ? hallId : 0,
                eventStatus: normalizeEventStatusCode(e),
                eventStatusRaw: `${e.eventStatus || e.EventStatus || ""}`.trim().toLowerCase()
                    || normalizeEventStatusCode(e),
                color: resolveEventAccentColor(e),
                eventDateHijri: e.eventDateHijriDisplay || e.eventDateHijri || e.EventDateHijri
            };
        });
    }

    function buildAgendaEventCard(e) {
        const meta = buildAppointmentMeta(e);
        const statusCode = normalizeEventStatusCode(e);
        const reservationId = eventRouteReservationId(e);
        const $card = stampHallOpsEventCard(
            $("<button type='button' class='hall-ops-agenda-card hall-ops-cell-card'/>"),
            { reservationId }
        ).css("--appt-accent", resolveEventAccentColor(e));
        $card.append($("<span class='hall-ops-agenda-card__bar' aria-hidden='true'/>"));
        const $body = $("<span class='hall-ops-agenda-card__body'/>").appendTo($card);
        const $head = $("<span class='hall-ops-agenda-card__head'/>").appendTo($body);
        $("<span class='hall-ops-agenda-card__time'/>")
            .text(forceEnglishDigits(formatEventTimeRangeLabel(e)))
            .appendTo($head);
        const $status = $("<span class='hall-ops-agenda-card__status'/>")
            .css("background", eventStatusColor(statusCode))
            .appendTo($head);
        appendEventStatusIcon($status, e, "hall-ops-agenda-card__status-icon");
        $("<span class='hall-ops-agenda-card__status-text'/>")
            .text(eventStatusLabel(statusCode))
            .appendTo($status);
        $("<span class='hall-ops-agenda-card__title'/>")
            .text(meta.customerLabel || t("hallOps.col.occasionOwner"))
            .appendTo($body);
        const metaParts = [meta.hallLabel, meta.occasionLabel].filter(Boolean);
        if (metaParts.length) {
            $("<span class='hall-ops-agenda-card__meta'/>")
                .text(forceEnglishDigits(metaParts.join(" · ")))
                .appendTo($body);
        }
        $("<span class='hall-ops-agenda-card__money'/>")
            .text(forceEnglishDigits(`${t("hallOps.col.deposit")}: ${meta.depositLabel} · ${t("hallOps.col.rent")}: ${meta.rentLabel}`))
            .appendTo($body);
        $card.on("click", (evt) => {
            evt.preventDefault();
            if (reservationId != null && reservationId !== "") {
                openReservation(reservationId);
            }
        });
        return $card;
    }

    function sortEventsForAgenda(rows) {
        return (rows || []).slice().sort((a, b) => {
            const da = formatLocalDateParam(a.eventDate != null ? a.eventDate : a.EventDate);
            const db = formatLocalDateParam(b.eventDate != null ? b.eventDate : b.EventDate);
            if (da !== db) {
                return da.localeCompare(db);
            }
            const ta = readEventField(a, "eventStartTime", "EventStartTime") || "00:00";
            const tb = readEventField(b, "eventStartTime", "EventStartTime") || "00:00";
            return ta.localeCompare(tb);
        });
    }

    function renderAgendaList($pane) {
        $pane.empty().addClass("hall-ops-agenda-host").data("agendaReady", true);
        const rows = sortEventsForAgenda(eventsCache);
        if (!rows.length) {
            $("<div class='hall-ops-agenda-empty'/>")
                .text(t("hallOps.calendarLayout.agendaEmpty"))
                .appendTo($pane);
            return;
        }

        let lastDayKey = null;
        const $list = $("<div class='hall-ops-agenda-list'/>").appendTo($pane);
        rows.forEach((e) => {
            const eventDate = e.eventDate != null ? e.eventDate : e.EventDate;
            const dayKey = formatLocalDateParam(eventDate);
            if (dayKey && dayKey !== lastDayKey) {
                lastDayKey = dayKey;
                const d = normalizeLocalDate(eventDate);
                const $sep = $("<div class='hall-ops-agenda-day'/>").appendTo($list);
                $("<span class='hall-ops-agenda-day__greg'/>")
                    .text(forceEnglishDigits(formatGregorianLong(d)))
                    .appendTo($sep);
                const hijri = schedulerHijriFullDateLabel(d);
                if (hijri) {
                    const suffix = t("hallOps.hijriSuffix") || "H";
                    $("<span class='hall-ops-agenda-day__hijri' dir='ltr'/>")
                        .text(`${hijri} ${suffix}`)
                        .appendTo($sep);
                }
            }
            $list.append(buildAgendaEventCard(e));
        });
    }

    function eachCalendarScheduler(callback) {
        $(".hall-ops-calendar-pane").each(function () {
            const $pane = $(this);
            if (!$pane.data("dxScheduler")) {
                return;
            }
            callback($pane.dxScheduler("instance"), $pane);
        });
    }

    function calendarPaneAnchorDate() {
        const $month = $(".hall-ops-calendar-pane[data-calendar-pane='month']");
        if ($month.length && $month.data("dxScheduler")) {
            return $month.dxScheduler("instance").option("currentDate");
        }
        const $timeline = $(".hall-ops-calendar-pane[data-calendar-pane='timeline']");
        if ($timeline.length && $timeline.data("dxScheduler")) {
            return $timeline.dxScheduler("instance").option("currentDate");
        }
        return calendarNavFilter.applied && calendarNavFilter.anchorGregorian
            ? calendarNavFilter.anchorGregorian
            : new Date();
    }

    function applyMonthSchedulerData(inst) {
        if (!inst || isApplyingSchedulerData) {
            return;
        }
        isApplyingSchedulerData = true;
        try {
            const templates = schedulerHijriCellTemplates();
            inst.beginUpdate();
            inst.option("dataSource", schedulerAppointments());
            inst.option("dataCellTemplate", templates.dataCellTemplate);
            inst.option("dateCellTemplate", templates.dateCellTemplate);
            inst.option("customizeDateNavigatorText", schedulerNavigatorText);
            inst.endUpdate();
        } finally {
            isApplyingSchedulerData = false;
        }
    }

    function applyTimelineSchedulerData(inst) {
        if (!inst || isApplyingSchedulerData) {
            return;
        }
        isApplyingSchedulerData = true;
        try {
            inst.beginUpdate();
            inst.option("resources", [{
                fieldExpr: "hallId",
                dataSource: hallResourceDataSource(),
                label: t("hallOps.col.hall")
            }]);
            inst.option("dataSource", schedulerRackAppointments());
            inst.option("height", timelineSchedulerHeight());
            inst.option("appointmentTemplate", buildTimelineAppointmentTemplate());
            const rackTemplates = schedulerRackDateCellTemplates();
            inst.option("dateCellTemplate", rackTemplates.dateCellTemplate);
            inst.endUpdate();
        } finally {
            isApplyingSchedulerData = false;
        }
    }

    function applySchedulerDataAndTemplates(inst) {
        if (!inst) {
            return;
        }
        const view = inst.option("currentView") || "";
        if (view === "hallRack" || `${view}`.indexOf("timeline") >= 0) {
            applyTimelineSchedulerData(inst);
            return;
        }
        applyMonthSchedulerData(inst);
    }

    function reloadMonthCalendarData($pane) {
        const inst = $pane.dxScheduler("instance");
        const currentDate = normalizeRangeAnchor(inst.option("currentDate"));
        const schedulerDate = inst.option("currentDate");
        const schedulerTime = schedulerDate instanceof Date ? schedulerDate.getTime() : NaN;
        if (schedulerTime !== currentDate.getTime()) {
            suppressSchedulerOptionChanged = true;
            inst.option("currentDate", currentDate);
            suppressSchedulerOptionChanged = false;
        }
        const range = resolveEventLoadRange(currentDate, inst.option("currentView") || "month");
        return loadEvents(range.from, range.to).then(() => {
            if (inst.option("currentView") === "month") {
                inst.option("height", monthCalendarHeight());
            }
            applyMonthSchedulerData(inst);
            syncSchedulerAfterData(inst);
        });
    }

    function reloadTimelineCalendarData($pane) {
        const inst = $pane.dxScheduler("instance");
        const currentDate = normalizeRangeAnchor(inst.option("currentDate"));
        suppressSchedulerOptionChanged = true;
        inst.option("currentDate", currentDate);
        suppressSchedulerOptionChanged = false;
        const range = resolveEventLoadRange(currentDate, "month");
        return loadEvents(range.from, range.to).then(() => {
            applyTimelineSchedulerData(inst);
            syncSchedulerAfterData(inst);
        });
    }

    function refreshCalendarShellFromCache($root) {
        const layout = getActiveCalendarLayout();
        const $month = $root.find("[data-calendar-pane='month']");
        const $timeline = $root.find("[data-calendar-pane='timeline']");
        const $agenda = $root.find("[data-calendar-pane='agenda']");

        if ($month.data("dxScheduler")) {
            applyMonthSchedulerData($month.dxScheduler("instance"));
            syncSchedulerAfterData($month.dxScheduler("instance"));
        }
        if ($timeline.data("dxScheduler")) {
            applyTimelineSchedulerData($timeline.dxScheduler("instance"));
            syncSchedulerAfterData($timeline.dxScheduler("instance"));
        }
        if (layout === "agenda") {
            renderAgendaList($agenda);
        }
        wireHallOpsCalendarContextMenu($root);
        syncCalendarMonthSpanUi($root);
    }

    function refreshCalendarShellData($root) {
        const layout = getActiveCalendarLayout();
        const $month = $root.find("[data-calendar-pane='month']");
        const $timeline = $root.find("[data-calendar-pane='timeline']");
        const $agenda = $root.find("[data-calendar-pane='agenda']");

        if (layout === "month" && $month.data("dxScheduler")) {
            return reloadMonthCalendarData($month);
        }
        if (layout === "timeline" && $timeline.data("dxScheduler")) {
            return reloadTimelineCalendarData($timeline);
        }
        if (layout === "agenda") {
            const anchor = calendarPaneAnchorDate();
            const range = resolveEventLoadRange(anchor, "week");
            return loadEvents(range.from, range.to).then(() => {
                renderAgendaList($agenda);
            });
        }
        return $.Deferred().resolve().promise();
    }

    function setCalendarLayout($root, layout, options) {
        const opts = options || {};
        const effective = resolveEffectiveCalendarLayout(layout);
        if (opts.persist !== false) {
            safeLocalStorageSet(CALENDAR_LAYOUT_STORAGE_KEY, layout);
        }
        $root.data("activeCalendarLayout", effective);

        $root.find("[data-calendar-pane]").each(function () {
            const $pane = $(this);
            const paneLayout = $pane.attr("data-calendar-pane");
            $pane.toggleClass("hall-ops-calendar-pane--hidden", paneLayout !== effective);
        });

        const $month = $root.find("[data-calendar-pane='month']");
        const $timeline = $root.find("[data-calendar-pane='timeline']");
        const $agenda = $root.find("[data-calendar-pane='agenda']");

        if (effective === "month") {
            initMonthCalendarPane($month);
        } else if (effective === "timeline") {
            initTimelineCalendarPane($timeline);
        } else if (effective === "agenda") {
            initAgendaCalendarPane($agenda);
        }

        const $switch = $root.find(".hall-ops-calendar-layout-switch");
        if ($switch.data("dxButtonGroup")) {
            suppressCalendarLayoutSwitchEvent = true;
            $switch.dxButtonGroup("instance").option("selectedItemKeys", [effective]);
            suppressCalendarLayoutSwitchEvent = false;
        }

        syncCalendarMonthSpanUi($root);

        return refreshCalendarShellData($root);
    }

    let suppressCalendarLayoutSwitchEvent = false;

    function initCalendarLayoutSwitch($host, $calendarRoot) {
        const layout = resolveEffectiveCalendarLayout();
        $calendarRoot.data("activeCalendarLayout", layout);
        $host.addClass("hall-ops-calendar-layout-switch");

        if ($host.data("dxButtonGroup")) {
            $host.dxButtonGroup("instance").option("items", calendarLayoutSwitchItems());
            $host.dxButtonGroup("instance").option("selectedItemKeys", [layout]);
            return;
        }

        $host.dxButtonGroup({
            items: calendarLayoutSwitchItems(),
            keyExpr: "id",
            stylingMode: "contained",
            selectionMode: "single",
            selectedItemKeys: [layout],
            focusStateEnabled: false,
            elementAttr: { class: "hall-ops-calendar-layout-btn-group" },
            onSelectionChanged(e) {
                if (suppressCalendarLayoutSwitchEvent) {
                    return;
                }
                const keys = e.component.option("selectedItemKeys") || [];
                const key = keys[0];
                if (!key || key === getActiveCalendarLayout()) {
                    return;
                }
                setCalendarLayout($calendarRoot, key);
            }
        });
    }

    function onCalendarViewportChange() {
        const $root = getCalendarViewHost();
        if (!$root.length || !$root.data("hallCalendarShellReady")) {
            return;
        }
        const preferred = normalizeCalendarLayout(safeLocalStorageGet(CALENDAR_LAYOUT_STORAGE_KEY));
        const effective = resolveEffectiveCalendarLayout(preferred);
        const $switch = $root.find(".hall-ops-calendar-layout-switch");
        if ($switch.data("dxButtonGroup")) {
            $switch.dxButtonGroup("instance").option("items", calendarLayoutSwitchItems());
        }
        setCalendarLayout($root, effective, { persist: false });
    }

    function initCalendarLayoutViewportSync() {
        if (window.__hallOpsCalendarLayoutViewportSync) {
            return;
        }
        window.__hallOpsCalendarLayoutViewportSync = true;
        const mq = window.matchMedia(CALENDAR_LAYOUT_MOBILE_MQ);
        let timer;
        const schedule = () => {
            clearTimeout(timer);
            timer = window.setTimeout(onCalendarViewportChange, 120);
        };
        if (typeof mq.addEventListener === "function") {
            mq.addEventListener("change", schedule);
        } else if (typeof mq.addListener === "function") {
            mq.addListener(schedule);
        }
        $(window).on("resize.hallOpsCalendarLayout", schedule);
    }

    function navigateSchedulers(anchorDate, viewName) {
        if (!anchorDate) {
            return;
        }
        const normalizedAnchor = normalizeRangeAnchor(anchorDate);
        $(".hall-ops-calendar-pane").each(function () {
            const $pane = $(this);
            const paneType = $pane.attr("data-calendar-pane");
            if (paneType === "agenda") {
                if ($pane.data("agendaReady")) {
                    renderAgendaList($pane);
                }
                return;
            }
            if (!$pane.data("dxScheduler")) {
                return;
            }
            const inst = $pane.dxScheduler("instance");
            suppressSchedulerOptionChanged = true;
            inst.option("currentDate", normalizedAnchor);
            if (viewName && paneType === "month") {
                inst.option("currentView", viewName);
            }
            suppressSchedulerOptionChanged = false;
            if (paneType === "timeline") {
                applyTimelineSchedulerData(inst);
            } else {
                applyMonthSchedulerData(inst);
            }
            syncSchedulerAfterData(inst);
        });
    }

    function loadEvents(rangeStart, rangeEnd) {
        const service = svc();
        if (!service) {
            return $.Deferred().reject().promise();
        }
        const from = rangeStart || new Date(new Date().getFullYear(), new Date().getMonth(), 1);
        const to = rangeEnd || new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0);
        const requestSeq = ++eventsLoadRequestSeq;
        return service.listEvents(buildListEventsParams(from, to)).then((data) => {
            const rows = Array.isArray(data) ? data : [];
            if (requestSeq >= eventsLoadAppliedSeq) {
                eventsLoadAppliedSeq = requestSeq;
                eventsCache = rows;
            }
            return eventsCache;
        });
    }

    function schedulerAppointments() {
        return eventsCache.map((e) => {
            const meta = buildAppointmentMeta(e);
            const eventDateRaw = e.eventDate != null ? e.eventDate : e.EventDate;
            const startTime = readEventField(e, "eventStartTime", "EventStartTime");
            const endTime = readEventField(e, "eventEndTime", "EventEndTime");
            const startDate = buildSchedulerDateTime(eventDateRaw, startTime, "00:00:00");
            let endDate = buildSchedulerDateTime(eventDateRaw, endTime, "23:59:00");
            if (endDate <= startDate) {
                // Prevent zero/negative duration records from being skipped by Scheduler.
                endDate = new Date(startDate.getTime() + (60 * 60 * 1000));
            }
            const hallRaw = e.hallId != null ? e.hallId : e.HallId;
            const hallId = Number(hallRaw);
            return {
                reservationId: e.reservationId != null ? e.reservationId : e.ReservationId,
                text: meta.customerLabel || t("hallOps.col.occasionOwner"),
                shortText: meta.customerLabel,
                customerLabel: meta.customerLabel,
                occasionLabel: meta.occasionLabel,
                hallLabel: meta.hallLabel,
                dateLabel: meta.dateLabel,
                rentLabel: meta.rentLabel,
                depositLabel: meta.depositLabel,
                startDate: startDate,
                endDate: endDate,
                hallId: Number.isFinite(hallId) && hallId > 0 ? hallId : 0,
                eventStatus: normalizeEventStatusCode(e),
                eventStatusRaw: `${e.eventStatus || e.EventStatus || ""}`.trim().toLowerCase()
                    || normalizeEventStatusCode(e),
                color: resolveEventAccentColor(e),
                eventDateHijri: e.eventDateHijriDisplay || e.eventDateHijri || e.EventDateHijri
            };
        });
    }

    function calendarLoadRange(viewName, anchorDate) {
        const d = anchorDate instanceof Date ? anchorDate : new Date(anchorDate || Date.now());
        if (viewName === "year") {
            return {
                from: new Date(d.getFullYear(), 0, 1),
                to: new Date(d.getFullYear(), 11, 31)
            };
        }
        if (viewName === "week" || viewName === "day") {
            const start = new Date(d.getFullYear(), d.getMonth(), d.getDate() - 7);
            const end = new Date(d.getFullYear(), d.getMonth(), d.getDate() + 14);
            return { from: start, to: end };
        }
        const span = Math.max(1, getCalendarMonthSpan());
        return {
            from: new Date(d.getFullYear(), d.getMonth(), 1),
            to: new Date(d.getFullYear(), d.getMonth() + span + 1, 0)
        };
    }

    function normalizeRangeAnchor(anchorDate) {
        return normalizeLocalDate(anchorDate) || new Date();
    }

    function schedulerRangeViewName() {
        const layout = getActiveCalendarLayout();
        if (layout === "timeline") {
            return "month";
        }
        if (layout === "agenda") {
            return "week";
        }
        const $month = $(".hall-ops-calendar-pane[data-calendar-pane='month']");
        if ($month.length && $month.data("dxScheduler")) {
            return $month.dxScheduler("instance").option("currentView") || "month";
        }
        return "month";
    }

    function schedulerViewsConfig() {
        const yearCaption = t("common.year");
        const span = getCalendarMonthSpan();
        return [{
            type: "month",
            name: "month",
            caption: t("hallOps.view.month"),
            intervalCount: span
        }, {
            type: "month",
            name: "year",
            // Built-in Scheduler month view with 12-month interval.
            intervalCount: 12,
            caption: yearCaption === "common.year" ? "Year" : yearCaption
        }];
    }

    function initMonthCalendarPane($pane) {
        if ($pane.data("dxScheduler")) {
            reloadMonthCalendarData($pane);
            return;
        }

        $pane.empty();
        const normalizedAnchor = normalizeRangeAnchor(calendarPaneAnchorDate());
        const range = resolveEventLoadRange(normalizedAnchor, "month");

        loadEvents(range.from, range.to).then(() => {
            const hijriTemplates = schedulerHijriCellTemplates();
            $pane.addClass("hall-ops-scheduler-host hall-ops-scheduler-host--month");
            $pane.dxScheduler({
                dataSource: schedulerAppointments(),
                views: schedulerViewsConfig(),
                currentView: "month",
                currentDate: normalizedAnchor,
                height: monthCalendarHeight(),
                width: "100%",
                firstDayOfWeek: 6,
                useDropDownViewSwitcher: true,
                adaptivityEnabled: false,
                maxAppointmentsPerCell: 5,
                startDayHour: 0,
                endDayHour: 24,
                showAllDayPanel: false,
                editing: false,
                dataCellTemplate: hijriTemplates.dataCellTemplate,
                dateCellTemplate: hijriTemplates.dateCellTemplate,
                appointmentTemplate: buildSchedulerAppointmentTemplate(),
                customizeDateNavigatorText: schedulerNavigatorText,
                onAppointmentClick(e) {
                    e.cancel = true;
                    openReservation(e.appointmentData);
                },
                onCellClick(e) {
                    const $target = e.event && e.event.target ? $(e.event.target) : $();
                    const $card = $target.closest(".hall-ops-event-card, .hall-ops-cell-card");
                    if ($card.length) {
                        e.cancel = true;
                        const reservationId = Number($card.attr("data-reservation-id"));
                        if (!Number.isNaN(reservationId) && reservationId > 0) {
                            openReservation(reservationId);
                        }
                        return;
                    }
                    if (!can("hall.events.manage")) {
                        return;
                    }
                    const cellDate = e.cellData && e.cellData.startDate;
                    if (cellDate) {
                        showCreatePopup(new Date(cellDate));
                    }
                },
                onCellContextMenu(e) {
                    const $target = e.event && e.event.target ? $(e.event.target) : $();
                    const onCard = $target.closest(".hall-ops-event-card, .hall-ops-cell-card, .hall-ops-agenda-card").length > 0;
                    if (!onCard) {
                        return;
                    }
                    e.cancel = true;
                    hallOpsSchedulerContextMenuHandler(e);
                },
                onContentReady(e) {
                    syncSchedulerAfterData(e.component);
                    wireHallOpsCalendarContextMenu($pane.closest(".hall-ops-calendar-view-host"));
                },
                onOptionChanged(e) {
                    if (suppressSchedulerOptionChanged) {
                        return;
                    }
                    if (e.name === "currentDate" || e.name === "currentView") {
                        if (e.name === "currentView") {
                            syncCalendarMonthSpanUi($pane.closest(".hall-ops-calendar-view-host"));
                        }
                        reloadMonthCalendarData($pane);
                    }
                }
            });
        });
    }

    function initTimelineCalendarPane($pane) {
        if ($pane.data("dxScheduler")) {
            const inst = $pane.dxScheduler("instance");
            if (inst && inst.option("currentView") === "hallRack") {
                reloadTimelineCalendarData($pane);
                return;
            }
            try {
                inst.dispose();
            } catch (_) {
                /* ignore */
            }
            $pane.removeData("dxScheduler").empty();
        }

        $pane.empty();
        const normalizedAnchor = normalizeRangeAnchor(calendarPaneAnchorDate());
        const range = resolveEventLoadRange(normalizedAnchor, "month");

        loadEvents(range.from, range.to).then(() => {
            const rackTemplates = schedulerRackDateCellTemplates();
            $pane.addClass("hall-ops-scheduler-host hall-ops-scheduler-host--timeline hall-ops-scheduler-host--rack");
            $pane.dxScheduler({
                dataSource: schedulerRackAppointments(),
                views: [{
                    type: "timelineMonth",
                    name: "hallRack",
                    caption: t("hallOps.calendarLayout.timeline"),
                    groupOrientation: "vertical",
                    intervalCount: getCalendarMonthSpan()
                }],
                currentView: "hallRack",
                groups: ["hallId"],
                resources: [{
                    fieldExpr: "hallId",
                    dataSource: hallResourceDataSource(),
                    label: t("hallOps.col.hall")
                }],
                currentDate: normalizedAnchor,
                height: timelineSchedulerHeight(),
                width: "100%",
                firstDayOfWeek: 6,
                crossScrollingEnabled: true,
                showAllDayPanel: false,
                maxAppointmentsPerCell: "unlimited",
                showCurrentTimeIndicator: false,
                editing: false,
                useDropDownViewSwitcher: false,
                dateCellTemplate: rackTemplates.dateCellTemplate,
                appointmentTemplate: buildTimelineAppointmentTemplate(),
                customizeDateNavigatorText: schedulerNavigatorText,
                onAppointmentClick(e) {
                    e.cancel = true;
                    openReservation(e.appointmentData);
                },
                onAppointmentContextMenu(e) {
                    e.cancel = true;
                    hallOpsSchedulerContextMenuHandler(e);
                },
                onCellClick(e) {
                    const cellDate = e.cellData && e.cellData.startDate;
                    if (cellDate) {
                        showCreatePopup(new Date(cellDate));
                    }
                },
                onContentReady(e) {
                    syncSchedulerAfterData(e.component);
                    wireHallOpsCalendarContextMenu($pane.closest(".hall-ops-calendar-view-host"));
                },
                onOptionChanged(e) {
                    if (suppressSchedulerOptionChanged) {
                        return;
                    }
                    if (e.name === "currentDate") {
                        reloadTimelineCalendarData($pane);
                    }
                }
            });
        });
    }

    function initAgendaCalendarPane($pane) {
        const normalizedAnchor = normalizeRangeAnchor(calendarPaneAnchorDate());
        const range = resolveEventLoadRange(normalizedAnchor, "week");
        if ($pane.data("agendaReady")) {
            loadEvents(range.from, range.to).then(() => renderAgendaList($pane));
            return;
        }
        loadEvents(range.from, range.to).then(() => renderAgendaList($pane));
    }

    function initCalendarView($host) {
        if ($host.data("hallCalendarShellReady")) {
            refreshCalendarShellFromCache($host);
            return;
        }

        $host.data("hallCalendarShellReady", true).empty().addClass("hall-ops-calendar-view-host");
        const $shell = $("<div class='hall-ops-calendar-shell'/>").appendTo($host);
        const $toolbar = $("<div class='hall-ops-calendar-layout-bar'/>").appendTo($shell);
        const $spanHost = $("<div class='hall-ops-calendar-span-host'/>").appendTo($toolbar);
        const $switchHost = $("<div class='hall-ops-calendar-layout-switch-host'/>").appendTo($toolbar);
        $("<div class='hall-ops-calendar-pane' data-calendar-pane='month'/>").appendTo($shell);
        $("<div class='hall-ops-calendar-pane hall-ops-calendar-pane--hidden' data-calendar-pane='timeline'/>").appendTo($shell);
        $("<div class='hall-ops-calendar-pane hall-ops-calendar-pane--hidden' data-calendar-pane='agenda'/>").appendTo($shell);

        initCalendarMonthSpanSelect($spanHost, $host);
        initCalendarLayoutSwitch($switchHost, $host);
        initCalendarLayoutViewportSync();
        initSchedulerLayoutSync();
        ensureHallOpsEventContextMenu();
        wireHallOpsCalendarContextMenu($host);
        setCalendarLayout($host, resolveEffectiveCalendarLayout(), { persist: false });
    }

    function kanbanColumns() {
        return [
            { key: "unconfirmed", match: (e) => normalizeEventStatusCode(e) === "unconfirmed" },
            { key: "confirmed", match: (e) => normalizeEventStatusCode(e) === "confirmed" },
            { key: "closed", match: (e) => normalizeEventStatusCode(e) === "closed" }
        ];
    }

    function initKanbanView($host) {
        $host.empty();
        $host.append(
            $("<p class='hall-ops-kanban-hint'/>").text(t("hallOps.kanban.workflowHint"))
        );
        const cols = kanbanColumns();
        const $grid = $("<div class='hall-ops-kanban'/>");
        cols.forEach((col) => {
            const $col = $("<div class='hall-ops-kanban-col'/>");
            $col.append($("<h4/>").text(t(`hallOps.kanban.${col.key}`)));
            const $list = $("<div class='hall-ops-kanban-list'/>");
            eventsCache
                .filter((e) => typeof col.match === "function" && col.match(e))
                .forEach((e) => {
                    const statusCode = normalizeEventStatusCode(e);
                    const $card = $("<div class='hall-ops-kanban-card'/>")
                        .append(
                            $("<span class='hall-ops-kanban-card__status'/>")
                                .text(eventStatusLabel(statusCode))
                                .css("background", eventStatusColor(statusCode))
                        )
                        .append($("<strong/>").text(eventCustomerName(e)))
                        .append($("<span/>").text(`${e.hallName || ""} · ${formatEventDualDateLabel(e)}`))
                        .append(
                            $("<small/>").text(
                                `${t("hallOps.col.deposit")}: ${formatMoney(eventDepositAmountValue(e))} · ${t("hallOps.col.rent")}: ${formatMoney(eventTotalAmountValue(e))}`
                            )
                        )
                        .on("click", () => openReservation(e.reservationId));
                    $list.append($card);
                });
            $col.append($list);
            $grid.append($col);
        });
        $host.append($grid);
    }

    function initDashboardView($host) {
        $host.empty();
        const service = svc();
        if (!service) {
            return;
        }
        const seq = ++dashboardRenderSeq;
        $host.data("dashboardRenderSeq", seq);
        const range = dashboardWindowRange();
        Promise.all([
            service.getDashboard().catch(() => ({})),
            service.listEvents(buildListEventsParams(range.from, range.to)).catch(() => [])
        ]).then(([dash, rows]) => {
            if (($host.data("dashboardRenderSeq") || 0) !== seq) {
                return;
            }
            const arrivalEvents = Array.isArray(rows) ? rows : [];
            const depositsTotal = arrivalEvents.reduce((sum, e) => sum + eventDepositAmountValue(e), 0);
            const rentsTotal = arrivalEvents.reduce((sum, e) => sum + eventTotalAmountValue(e), 0);
            const balanceTotal = Math.max(0, rentsTotal - depositsTotal);
            const widgets = [
                { label: t("hallOps.dashboard.today"), value: arrivalEvents.filter((e) => formatLocalDateParam(e.eventDate || e.EventDate) === formatLocalDateParam(new Date())).length },
                { label: t("hallOps.dashboard.tomorrow"), value: arrivalEvents.filter((e) => formatLocalDateParam(e.eventDate || e.EventDate) === formatLocalDateParam(new Date(new Date().getFullYear(), new Date().getMonth(), new Date().getDate() + 1))).length },
                { label: t("hallOps.dashboard.month"), value: arrivalEvents.length },
                { label: t("hallOps.dashboard.latePayments"), value: dash.latePaymentCount },
                { label: t("hallOps.dashboard.collectionsToday"), value: formatMoney(dash.todayRevenue || 0) },
                { label: t("hallOps.dashboard.deposits"), value: formatMoney(depositsTotal) },
                { label: t("hallOps.dashboard.balance"), value: formatMoney(balanceTotal) }
            ];
            const $widgets = $("<div class='hall-ops-widgets'/>");
            widgets.forEach((w) => {
                $widgets.append(
                    $("<div class='hall-ops-widget'/>")
                        .append($("<strong/>").text(w.value))
                        .append($("<span/>").text(w.label))
                );
            });
            $host.append($widgets);

            const upcoming = arrivalEvents;

            const $gridHost = $("<div/>");
            $host.append($("<h4/>").text(t("hallOps.dashboard.upcomingEvents")));
            $host.append($gridHost);
            $gridHost.dxDataGrid({
                dataSource: upcoming,
                showBorders: true,
                columnAutoWidth: true,
                elementAttr: { class: "pms-grid-compact" },
                headerFilter: { visible: true, search: { enabled: true } },
                searchPanel: { visible: true, width: 260 },
                columns: [
                    { dataField: "reservationNo", caption: t("hallOps.col.reservationNo") },
                    {
                        caption: t("hallOps.col.occasionOwner"),
                        calculateCellValue(row) {
                            return eventCustomerName(row);
                        }
                    },
                    { dataField: "hallName", caption: t("hallOps.col.hall") },
                    {
                        dataField: "eventType",
                        caption: t("hallOps.col.eventType"),
                        calculateCellValue(row) {
                            return eventTypeLabel(row.eventType);
                        }
                    },
                    {
                        dataField: "eventDate",
                        caption: t("hallOps.col.eventDate"),
                        dataType: "date",
                        format: "dd/MM/yyyy"
                    },
                    {
                        caption: t("hallOps.col.eventDateHijri"),
                        calculateCellValue(row) {
                            return row.eventDateHijriDisplay || row.eventDateHijri || "";
                        }
                    },
                    { dataField: "eventStartTime", caption: t("hallOps.col.start") },
                    { dataField: "remainingBalance", caption: t("hallOps.col.balance"), format: { type: "fixedPoint", precision: 2 } },
                    {
                        type: "buttons",
                        width: 88,
                        buttons: [
                            {
                                hint: t("hallOps.editEvent"),
                                icon: "edit",
                                visible: can("hall.events.manage"),
                                onClick(e) {
                                    showEditEventPopup(e.row.data);
                                }
                            },
                            {
                                hint: t("hallOps.action.openReservation"),
                                icon: "doc",
                                onClick(e) {
                                    openReservation(e.row.data.reservationId);
                                }
                            }
                        ]
                    }
                ],
                onRowDblClick(e) {
                    if (can("hall.events.manage")) {
                        showEditEventPopup(e.data);
                    } else {
                        openReservation(e.data.reservationId);
                    }
                }
            });
        });
    }

    function initOccupancyView($host) {
        $host.empty();
        const seq = ++occupancyRenderSeq;
        $host.data("occupancyRenderSeq", seq);
        svc().getOccupancy().then((cards) => {
            if (($host.data("occupancyRenderSeq") || 0) !== seq) {
                return;
            }
            const $grid = $("<div class='hall-ops-occupancy-grid'/>");
            (cards || []).forEach((c) => {
                const ev = c.currentEvent;
                const $card = $("<div class='hall-ops-occ-card'/>");
                $card.append($("<div class='hall-ops-occ-card__code'/>").text(c.hallName || c.hallCode));
                $card.append(
                    $("<div class='hall-ops-occ-card__status'/>").text(
                        t(`hallOps.preparation.${c.preparationStatus || "ready"}`)
                    )
                );
                const displayEv = ev || c.nextEvent;
                if (displayEv) {
                    const isUpcoming = c.occupancyState === "upcoming";
                    if (isUpcoming) {
                        $card.append(
                            $("<div class='hall-ops-occ-card__badge hall-ops-occ-card__badge--upcoming'/>").text(
                                t("hallOps.occupancy.upcoming")
                            )
                        );
                    }
                    $card.append($("<p/>").text(eventCustomerName(displayEv)));
                    $card.append(
                        $("<small/>").text(
                            isUpcoming
                                ? (c.nextEventLabel || "")
                                : (c.timeRemainingLabel || "")
                        )
                    );
                    $card.on("click", () => openReservation(displayEv.reservationId));
                } else {
                    $card.append($("<p/>").text(t("hallOps.occupancy.vacant")));
                }
                $grid.append($card);
            });
            $host.append($grid);
        });
    }

    function initCompletionView($host) {
        $host.empty();
        const $formHost = $("<div class='hall-ops-completion-form'/>");
        $host.append($formHost);

        const todayEvents = eventsCache.filter((e) => normalizeEventStatusCode(e) === "confirmed");

        $formHost.dxForm({
            formData: {
                reservationId: selectedReservationId || (todayEvents[0] && todayEvents[0].reservationId),
                eventCompleted: true,
                hallDelivered: true,
                noIssues: true,
                actualGuests: null,
                completionNotes: ""
            },
            items: [
                {
                    dataField: "reservationId",
                    label: { text: t("hallOps.completion.reservation") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: todayEvents,
                        displayExpr: (item) => item ? `${item.reservationNo} — ${eventCustomerName(item)}` : "",
                        valueExpr: "reservationId",
                        searchEnabled: true
                    }
                },
                { dataField: "eventCompleted", label: { text: t("hallOps.completion.eventDone") }, editorType: "dxCheckBox" },
                { dataField: "hallDelivered", label: { text: t("hallOps.completion.hallDelivered") }, editorType: "dxCheckBox" },
                { dataField: "noIssues", label: { text: t("hallOps.completion.noIssues") }, editorType: "dxCheckBox" },
                { dataField: "actualGuests", label: { text: t("hallOps.completion.actualGuests") }, editorType: "dxNumberBox" },
                { dataField: "completionNotes", label: { text: t("hallOps.completion.notes") }, editorType: "dxTextArea" }
            ]
        });

        $("<div/>").css({ marginTop: "12px" }).dxButton({
            text: t("hallOps.completion.submit"),
            type: "default",
            stylingMode: "contained",
            disabled: !can("hall.events.manage"),
            onClick() {
                const form = $formHost.dxForm("instance");
                const data = form.option("formData");
                if (!data.reservationId) {
                    notify(t("hallOps.completion.selectReservation"), "warning");
                    return;
                }
                assertHallEventCanClose(data.reservationId).then(() => {
                    return svc().completeEvent(data.reservationId, {
                        eventCompleted: !!data.eventCompleted,
                        hallDelivered: !!data.hallDelivered,
                        noIssues: !!data.noIssues,
                        actualGuests: data.actualGuests,
                        completionNotes: data.completionNotes
                    });
                }).then(() => {
                    notify(t("hallOps.completion.done"), "success");
                    refreshAll();
                }).catch((err) => notify((err && err.message) || t("common.error"), "error"));
            }
        }).appendTo($host);
    }

    function setPeriodBarVisible(show) {
        periodFilter.visible = !!show;
        $("#hallOpsReportsBar").toggleClass("is-visible", periodFilter.visible);
        if (!periodFilter.visible) {
            periodFilter.applied = false;
            updateFilterAppliedBanner();
            refreshAll();
        }
    }

    function initFilterBar($host) {
        $host.empty();
        const $bar = $("<div class='hall-ops-filter-bar'/>");
        $bar.append($("<div class='hall-ops-filter-bar__label'/>").text(t("hallOps.filter.title")));

        const $group = $("<div class='hall-ops-filter-bar__group'/>");
        const $mode = $("<div/>");
        const $gregHost = $("<div class='hall-ops-filter-date-host'/>");
        const $hijriHost = $("<div class='hall-ops-filter-date-host'/>");
        const $themeHost = $("<div/>");
        let hijriPicker = null;
        let gregDateBox = null;

        $mode.dxSelectBox({
            label: t("hallOps.filter.calendarType"),
            labelMode: "floating",
            dataSource: [
                { id: "gregorian", text: t("hallOps.filter.gregorian") },
                { id: "hijri", text: t("hallOps.filter.hijri") }
            ],
            valueExpr: "id",
            displayExpr: "text",
            value: calendarNavFilter.mode,
            width: 160,
            onValueChanged(e) {
                calendarNavFilter.mode = e.value || "gregorian";
                syncFilterDatePickers();
            }
        });

        $gregHost.dxDateBox({
            type: "date",
            label: t("hallOps.filter.searchDate"),
            labelMode: "floating",
            openOnFieldClick: true,
            displayFormat: "dd/MM/yyyy",
            width: 200,
            value: calendarNavFilter.gregorianDate || new Date()
        });
        gregDateBox = $gregHost.dxDateBox("instance");

        const hijriCal = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (hijriCal && typeof hijriCal.attachDateBoxPicker === "function" && hijriCal.isReady()) {
            hijriPicker = hijriCal.attachDateBoxPicker($hijriHost, {
                label: t("hallOps.filter.searchDate"),
                onSelect(sel) {
                    if (sel && sel.gregorian && gregDateBox) {
                        gregDateBox.option("value", sel.gregorian);
                    }
                }
            });
            if (calendarNavFilter.eventDateHijri && hijriCal.parseStorageToGregorian) {
                const g = hijriCal.parseStorageToGregorian(calendarNavFilter.eventDateHijri);
                if (g) {
                    hijriPicker.setFromGregorian(g);
                }
            }
        }

        gregDateBox.option("onValueChanged", (e) => {
            if (calendarNavFilter.mode === "hijri" && hijriPicker && e.value) {
                hijriPicker.setFromGregorian(e.value);
            }
        });

        const $statusHost = $("<div/>");
        const statusItems = [{ value: null, text: t("hallOps.filter.allStatuses") }];
        ((lookups && lookups.eventStatuses) || []).forEach((s) => {
            statusItems.push({
                value: s.value || s.Value,
                text: isArabicUi() ? (s.labelAr || s.LabelAr) : (s.labelEn || s.LabelEn)
            });
        });
        $statusHost.dxSelectBox({
            label: t("hallOps.filter.eventStatus"),
            labelMode: "floating",
            dataSource: statusItems,
            valueExpr: "value",
            displayExpr: "text",
            value: statusFilter.eventStatus,
            width: 190,
            searchEnabled: true,
            onValueChanged(e) {
                statusFilter.eventStatus = e.value || null;
                refreshAll();
            }
        });

        $themeHost.dxSelectBox({
            label: "Card Theme",
            labelMode: "floating",
            dataSource: getCardThemeOptions(),
            valueExpr: "id",
            displayExpr: "text",
            value: cardTheme,
            width: 150,
            onValueChanged(e) {
                setCardTheme(e.value || "soft");
            }
        });

        function syncFilterDatePickers() {
            const isHijri = calendarNavFilter.mode === "hijri";
            $gregHost.toggle(!isHijri);
            $hijriHost.toggle(isHijri);
            if (isHijri && hijriPicker && gregDateBox) {
                const g = gregDateBox.option("value");
                if (g) {
                    hijriPicker.setFromGregorian(g);
                }
            }
        }

        function setAnchorFromGregorian(anchor) {
            const normalized = normalizeLocalDate(anchor);
            if (!normalized) {
                return false;
            }
            const hijriLib = window.Zaaer && window.Zaaer.PmsHijriCalendars;
            calendarNavFilter.gregorianDate = normalized;
            calendarNavFilter.anchorGregorian = normalized;
            calendarNavFilter.eventDateHijri = hijriLib && typeof hijriLib.formatStorageFromGregorian === "function"
                ? hijriLib.formatStorageFromGregorian(normalized)
                : null;
            return true;
        }

        function applyCalendarFilter() {
            if (calendarNavFilter.mode === "hijri") {
                if (!hijriPicker) {
                    notify(t("common.error"), "error");
                    return;
                }
                const storage = hijriPicker.getStorageValue();
                const anchor = hijriPicker.getGregorianDate();
                if (!storage || !anchor) {
                    notify(t("hallOps.filter.searchDate"), "warning");
                    return;
                }
                calendarNavFilter.eventDateHijri = storage;
                if (!setAnchorFromGregorian(anchor)) {
                    notify(t("hallOps.filter.searchDate"), "warning");
                    return;
                }
            } else {
                const picked = gregDateBox.option("value");
                if (!picked) {
                    notify(t("hallOps.filter.searchDate"), "warning");
                    return;
                }
                if (!setAnchorFromGregorian(picked)) {
                    notify(t("hallOps.filter.searchDate"), "warning");
                    return;
                }
            }
            periodFilter.applied = false;
            calendarNavFilter.applied = true;
            calendarNavFilter.displayLabel = buildAppliedDateLabel();
            updateFilterAppliedBanner();
            navigateSchedulers(calendarNavFilter.anchorGregorian, schedulerRangeViewName());
            refreshAll();
        }

        function clearCalendarFilter() {
            calendarNavFilter.mode = "gregorian";
            calendarNavFilter.applied = false;
            calendarNavFilter.anchorGregorian = null;
            calendarNavFilter.gregorianDate = null;
            calendarNavFilter.eventDateHijri = null;
            calendarNavFilter.displayLabel = "";
            $mode.dxSelectBox("instance").option("value", "gregorian");
            gregDateBox.option("value", new Date());
            if (hijriPicker && typeof hijriPicker.clear === "function") {
                hijriPicker.clear();
            }
            syncFilterDatePickers();
            updateFilterAppliedBanner();
            navigateSchedulers(new Date(), "month");
            refreshAll();
        }

        $group.append($mode, $gregHost, $hijriHost, $statusHost, $themeHost);
        $bar.append($group);

        $("<div/>").dxButton({
            text: t("hallOps.filter.apply"),
            type: "default",
            stylingMode: "contained",
            onClick: applyCalendarFilter
        }).appendTo($bar);

        $("<div/>").dxButton({
            text: t("hallOps.filter.clear"),
            stylingMode: "text",
            onClick: clearCalendarFilter
        }).appendTo($bar);

        const $periodToggle = $("<div class='hall-ops-filter-period-toggle'/>");
        $periodToggle.dxCheckBox({
            text: t("hallOps.period.show"),
            value: periodFilter.visible,
            onValueChanged(e) {
                setPeriodBarVisible(!!e.value);
            }
        });
        $bar.append($periodToggle);

        $host.append($bar);
        filterAppliedBannerEl = $("<div id='hallOpsFilterApplied' class='hall-ops-filter-applied' style='display:none'/>");
        $host.append(filterAppliedBannerEl);
        syncFilterDatePickers();
        updateFilterAppliedBanner();
    }

    function initPeriodFilterBar($host) {
        $host.empty().removeClass("is-visible");
        const now = new Date();
        periodFilter.from = periodFilter.from || new Date(now.getFullYear(), now.getMonth(), 1);
        periodFilter.to = periodFilter.to || now;
        periodFilter.applied = false;

        const $bar = $("<div class='hall-ops-period-bar'/>");
        $bar.append($("<span class='hall-ops-period-bar__label'/>").text(t("hallOps.period.title")));
        const $from = $("<div/>").dxDateBox({
            type: "date",
            label: t("hallOps.period.from"),
            labelMode: "floating",
            value: periodFilter.from,
            openOnFieldClick: true,
            width: 150
        });
        const $to = $("<div/>").dxDateBox({
            type: "date",
            label: t("hallOps.period.to"),
            labelMode: "floating",
            value: periodFilter.to,
            openOnFieldClick: true,
            width: 150
        });
        $bar.append($from, $to);
        $("<div/>").addClass("hall-ops-period-apply-btn").dxButton({
            text: t("hallOps.period.apply"),
            type: "default",
            stylingMode: "contained",
            onClick() {
                const from = $from.dxDateBox("instance").option("value");
                const to = $to.dxDateBox("instance").option("value");
                if (!from || !to) {
                    return;
                }
                periodFilter.from = from instanceof Date ? from : new Date(from);
                periodFilter.to = to instanceof Date ? to : new Date(to);
                periodFilter.applied = true;
                calendarNavFilter.applied = false;
                updateFilterAppliedBanner();
                refreshAll();
            }
        }).appendTo($bar);
        $("<div/>").addClass("hall-ops-period-util-btn").dxButton({
            text: "",
            hint: t("hallOps.reports.utilization"),
            icon: "chart",
            stylingMode: "outlined",
            disabled: !can("hall.reports"),
            onClick() {
                const from = $from.dxDateBox("instance").option("value");
                const to = $to.dxDateBox("instance").option("value");
                svc().getUtilizationReport(from, to).then((report) => {
                    const lines = (report.lines || []).map((l) => `${l.hallName}: ${l.utilizationPercent}%`).join(", ");
                    notify(lines || t("hallOps.reports.noData"), "info", 5000);
                });
            }
        }).appendTo($bar);
        $host.append($bar);
        if (periodFilter.visible) {
            $host.addClass("is-visible");
        }
    }

    function initTabs() {
        const $tabs = $("#hallOpsTabPanel");
        if (!$tabs.length) {
            return;
        }
        const savedTab = Number(safeLocalStorageGet(TAB_STORAGE_KEY));
        const startIndex = Number.isInteger(savedTab) && savedTab >= 0 && savedTab <= 4 ? savedTab : 0;

        $tabs.dxTabPanel({
            items: [
                { title: t("hallOps.tabs.calendar"), template: () => $("<div class='hall-ops-view-host' data-view='calendar'/>") },
                { title: t("hallOps.tabs.kanban"), template: () => $("<div class='hall-ops-view-host' data-view='kanban'/>") },
                { title: t("hallOps.tabs.dashboard"), template: () => $("<div class='hall-ops-view-host' data-view='dashboard'/>") },
                { title: t("hallOps.tabs.occupancy"), template: () => $("<div class='hall-ops-view-host' data-view='occupancy'/>") },
                { title: t("hallOps.tabs.completion"), template: () => $("<div class='hall-ops-view-host' data-view='completion'/>") }
            ],
            selectedIndex: startIndex,
            height: "auto",
            onSelectionChanged(e) {
                const idx = e.component.option("selectedIndex") || 0;
                safeLocalStorageSet(TAB_STORAGE_KEY, `${idx}`);
                renderActiveView(idx);
            },
            onContentReady(e) {
                renderActiveView(e.component.option("selectedIndex") || 0);
            }
        });
    }

    function renderActiveView(index) {
        const $panel = $("#hallOpsTabPanel").dxTabPanel("instance");
        const $content = $panel && $panel.itemElements().eq(index).find("[data-view]");
        if (!$content.length) {
            return;
        }
        const view = $content.data("view");
        if (view === "calendar") {
            initCalendarView($content);
        } else if (view === "kanban") {
            initKanbanView($content);
        } else if (view === "dashboard") {
            initDashboardView($content);
        } else if (view === "occupancy") {
            initOccupancyView($content);
        } else if (view === "completion") {
            initCompletionView($content);
        }
    }

    function refreshAll() {
        const anchor = calendarNavFilter.applied && calendarNavFilter.anchorGregorian
            ? calendarNavFilter.anchorGregorian
            : new Date();
        const range = resolveEventLoadRange(anchor, schedulerRangeViewName());
        loadEvents(range.from, range.to).then(() => {
            svc().syncStatuses().catch(() => null);
            updateFilterAppliedBanner();
            if (calendarNavFilter.applied && calendarNavFilter.anchorGregorian) {
                navigateSchedulers(calendarNavFilter.anchorGregorian, schedulerRangeViewName());
            }
            const $panel = $("#hallOpsTabPanel");
            if ($panel.data("dxTabPanel")) {
                renderActiveView($panel.dxTabPanel("instance").option("selectedIndex") || 0);
            }
            scheduleUnpaidNotifyBadgeRefresh();
        });
    }

    function initCreateButton() {
        $("#hallOpsCreateBtn").dxButton({
            text: t("hallOps.createEvent"),
            icon: "add",
            type: "default",
            stylingMode: "contained",
            visible: can("hall.events.manage"),
            onClick() {
                showCreatePopup();
            }
        });
    }

    function refreshCustomerDisplay($display, formData) {
        const name = (formData && formData.customerName) || "";
        const mobile = (formData && formData.customerMobile) || "";
        $display.empty().addClass("is-visible");
        if (formData && formData.customerId) {
            $display.addClass("has-customer").removeClass("is-empty");
            $("<strong class='hall-ops-customer-display__name'/>")
                .text(name || `#${formData.customerId}`)
                .appendTo($display);
            if (mobile) {
                $("<span class='hall-ops-customer-display__mobile'/>").text(mobile).appendTo($display);
            }
        } else {
            $display.removeClass("has-customer").addClass("is-empty");
            const $ph = $("<span class='hall-ops-customer-display__placeholder'/>").appendTo($display);
            $("<span class='dx-icon dx-icon-user' aria-hidden='true'/>").appendTo($ph);
            $ph.append(document.createTextNode(` ${t("hallOps.customer.pick")}`));
        }
    }

    function buildCreateEventPayload(data) {
        const customerId = data.customerId != null && `${data.customerId}`.trim() !== ""
            ? Number(data.customerId)
            : null;
        const payload = {
            hallId: Number(data.hallId),
            eventType: data.eventType,
            eventDate: formatLocalDateParam(data.eventDate),
            eventStartTime: data.eventStartTime,
            eventEndTime: data.eventEndTime,
            expectedGuests: Number(data.expectedGuests) || 0,
            occasionName: data.occasionName || null,
            occasionOwner: data.occasionOwner || null,
            hallRentAmount: Number(data.hallRentAmount) || 0,
            depositAmount: Number(data.depositAmount) || 0
        };
        if (customerId != null && Number.isFinite(customerId) && customerId > 0) {
            payload.customerId = customerId;
        }
        return payload;
    }

    function bindCustomerFieldItem(formInstance) {
        return {
            itemType: "simple",
            colSpan: 2,
            label: { text: t("hallOps.customer.label") },
            template(_data, itemElement) {
                const $wrap = $("<div class='hall-ops-customer-field hall-ops-customer-field--create'/>").appendTo(itemElement);
                const $display = $("<div class='hall-ops-customer-display'/>").appendTo($wrap);
                refreshCustomerDisplay($display, formInstance.option("formData"));

                const $actions = $("<div class='hall-ops-customer-actions'/>").appendTo($wrap);

                $("<div/>").appendTo($actions).dxButton({
                    text: t("hallOps.customer.pick"),
                    icon: "user",
                    stylingMode: "outlined",
                    type: "default",
                    onClick() {
                        const picker = window.Zaaer && window.Zaaer.HallCustomerPicker;
                        if (!picker || typeof picker.open !== "function") {
                            return;
                        }
                        picker.open({
                            hotelId: hallHotelId,
                            onPick(customer) {
                                picker.applyToForm(formInstance, customer);
                                refreshCustomerDisplay($display, formInstance.option("formData"));
                            }
                        });
                    }
                });

                $("<div/>").appendTo($actions).dxButton({
                    text: t("hallOps.customer.create"),
                    icon: "plus",
                    stylingMode: "contained",
                    type: "default",
                    onClick() {
                        const picker = window.Zaaer && window.Zaaer.HallCustomerPicker;
                        if (!picker || typeof picker.openCreateCustomer !== "function") {
                            return;
                        }
                        picker.openCreateCustomer((customer) => {
                            picker.applyToForm(formInstance, customer);
                            refreshCustomerDisplay($display, formInstance.option("formData"));
                        }, null, hallHotelId);
                    }
                });

                $("<div/>").appendTo($actions).dxButton({
                    text: t("hallOps.customer.clear"),
                    icon: "clear",
                    stylingMode: "text",
                    onClick() {
                        formInstance.updateData({
                            customerId: null,
                            customerName: "",
                            customerMobile: "",
                            occasionOwner: ""
                        });
                        refreshCustomerDisplay($display, formInstance.option("formData"));
                    }
                });
            }
        };
    }

    function syncCreateFormHijriHint(formInstance) {
        if (!formInstance) {
            return;
        }
        const $hint = $(".hall-ops-hijri-hint").last();
        if (!$hint.length) {
            return;
        }
        const data = formInstance.option("formData") || {};
        const hijri = window.Zaaer && window.Zaaer.PmsHijriDate;
        const isAr =
            window.Zaaer &&
            window.Zaaer.LocalizationService &&
            typeof window.Zaaer.LocalizationService.currentCulture === "function" &&
            window.Zaaer.LocalizationService.currentCulture() === "ar";
        const label =
            hijri && typeof hijri.formatDualDateLabel === "function"
                ? hijri.formatDualDateLabel(data.eventDate, null, isAr)
                : "";
        $hint.text(label ? `${t("hallOps.col.eventDateHijri")}: ${label}` : "");
    }

    function showCreatePopup(presetDate) {
        const halls = (lookups && lookups.halls) || [];
        if (!halls.length) {
            notify(t("hallOps.noHallsSetup"), "warning", 5000);
            return;
        }
        if (!can("hall.events.manage")) {
            notify(t("common.forbidden") || t("common.error"), "warning");
            return;
        }
        const eventTypes = (lookups && lookups.eventTypes) || [];
        let $popup;
        let $form;
        let createHijriEditor = null;

        $popup = $("<div/>").appendTo("body").dxPopup({
            title: t("hallOps.createEvent"),
            visible: true,
            showCloseButton: true,
            width: Math.min(760, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: Math.min(680, window.innerHeight - 20),
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup hall-ops-event-popup hall-ops-event-popup--compact" },
            onShown() {
                setTimeout(() => {
                    if (createHijriEditor && $form) {
                        const fd = $form.dxForm("instance").option("formData");
                        if (fd && fd.eventDate) {
                            createHijriEditor.setFromGregorian(fd.eventDate);
                        }
                    }
                }, 80);
            },
            onHidden() {
                $popup.remove();
            },
            contentTemplate() {
                const $content = $("<div class='hall-ops-event-form-shell'/>");
                $form = $("<div class='hall-ops-event-form'/>");
                $content.append($form);
                $form.dxForm({
                    validationGroup: "hallCreateEventForm",
                    labelLocation: "top",
                    showColonAfterLabel: false,
                    minColWidth: 110,
                    formData: {
                        customerId: null,
                        customerName: "",
                        customerMobile: "",
                        hallId: halls[0] && halls[0].hallId,
                        eventType: eventTypes[0] || "wedding",
                        eventDate: presetDate instanceof Date ? presetDate : new Date(),
                        eventStartTime: "18:00",
                        eventEndTime: "23:00",
                        expectedGuests: 100,
                        occasionName: "",
                        occasionOwner: "",
                        hallRentAmount: 0,
                        depositAmount: 0
                    },
                    colCount: 2,
                    items: []
                });
                const formInstance = $form.dxForm("instance");
                formInstance.option("items", [
                    bindCustomerFieldItem(formInstance),
                    { dataField: "customerId", visible: false },
                    { dataField: "customerName", visible: false },
                    { dataField: "customerMobile", visible: false },
                    { dataField: "occasionOwner", visible: false },
                    {
                        dataField: "hallId",
                        label: { text: t("hallOps.col.hall") },
                        editorType: "dxSelectBox",
                        editorOptions: {
                            dataSource: halls,
                            displayExpr(item) {
                                return item ? (item.hallName || item.hallCode || "") : "";
                            },
                            valueExpr: "hallId"
                        }
                    },
                    {
                        dataField: "eventType",
                        label: { text: t("hallOps.col.eventType") },
                        editorType: "dxSelectBox",
                        editorOptions: eventTypeSelectBoxOptions()
                    },
                    {
                        itemType: "group",
                        colCount: 2,
                        colSpan: 2,
                        cssClass: "hall-ops-form-row--dates",
                        items: [
                            {
                                dataField: "eventDate",
                                label: { text: t("hallOps.col.eventDate") },
                                editorType: "dxDateBox",
                                editorOptions: {
                                    type: "date",
                                    openOnFieldClick: true,
                                    displayFormat: "dd/MM/yyyy",
                                    onValueChanged(e) {
                                        if (createHijriEditor && e.value) {
                                            createHijriEditor.setFromGregorian(e.value);
                                        }
                                    }
                                }
                            },
                            {
                                itemType: "simple",
                                label: { text: t("hallOps.col.eventDateHijri") },
                                template(_data, itemElement) {
                                    const hijriCal = window.Zaaer && window.Zaaer.PmsHijriCalendars;
                                    const $wrap = $("<div class='hall-ops-create-hijri hall-ops-create-hijri--form'/>").appendTo(itemElement);
                                    if (hijriCal && hijriCal.isReady()) {
                                        createHijriEditor = hijriCal.attachDateBoxPicker($wrap, {
                                            onSelect(sel) {
                                                if (sel && sel.gregorian) {
                                                    formInstance.updateData("eventDate", sel.gregorian);
                                                }
                                            }
                                        });
                                    } else {
                                        $("<div class='hall-ops-create-hijri__fallback'/>")
                                            .text("—")
                                            .appendTo($wrap);
                                    }
                                    const preset = formInstance.option("formData").eventDate;
                                    if (createHijriEditor && preset) {
                                        createHijriEditor.setFromGregorian(preset);
                                    }
                                }
                            }
                        ]
                    },
                    {
                        itemType: "group",
                        colCount: 2,
                        colSpan: 2,
                        cssClass: "hall-ops-form-row--times",
                        items: [
                            { dataField: "eventStartTime", label: { text: t("hallOps.col.start") } },
                            { dataField: "eventEndTime", label: { text: t("hallOps.col.end") } }
                        ]
                    },
                    {
                        itemType: "group",
                        colCount: 3,
                        colSpan: 2,
                        cssClass: "hall-ops-form-row--meta",
                        items: [
                            { dataField: "expectedGuests", label: { text: t("hallOps.col.guests") }, editorType: "dxNumberBox" },
                            { dataField: "occasionName", label: { text: t("hallOps.col.occasion") } },
                            {
                                itemType: "simple",
                                label: { text: t("hallOps.col.occasionOwner") },
                                template(_data, itemElement) {
                                    const fd = formInstance.option("formData") || {};
                                    const name = fd.occasionOwner || fd.customerName || "";
                                    $("<div class='hall-ops-owner-readonly'/>").text(name || "—").appendTo(itemElement);
                                }
                            }
                        ]
                    },
                    {
                        itemType: "group",
                        colCount: 2,
                        colSpan: 2,
                        cssClass: "hall-ops-form-row--money",
                        items: [
                            { dataField: "hallRentAmount", label: { text: t("hallOps.col.rent") }, editorType: "dxNumberBox" },
                            { dataField: "depositAmount", label: { text: t("hallOps.col.deposit") }, editorType: "dxNumberBox" }
                        ]
                    }
                ]);
                formInstance.on("fieldDataChanged", (e) => {
                    if (["customerName", "occasionOwner", "customerId"].indexOf(e.dataField) >= 0) {
                        const fd = formInstance.option("formData") || {};
                        $content.find(".hall-ops-owner-readonly").text(fd.occasionOwner || fd.customerName || "—");
                    }
                });
                return $content;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.save") || "Save",
                        type: "default",
                        onClick() {
                            const formInstance = $form.dxForm("instance");
                            const data = formInstance.option("formData");
                            if (!data.customerId) {
                                notify(t("hallOps.customer.required"), "warning");
                                return;
                            }
                            if (DevExpress.validationEngine && typeof DevExpress.validationEngine.validateGroup === "function") {
                                const validation = DevExpress.validationEngine.validateGroup("hallCreateEventForm");
                                if (validation && !validation.isValid) {
                                    return;
                                }
                            }
                            const payload = buildCreateEventPayload(data);
                            svc().createEvent(payload).then(() => {
                                notify(t("hallOps.createOk"), "success");
                                $popup.dxPopup("instance").hide();
                                refreshAll();
                            }).catch((err) => notify((err && err.message) || t("common.error"), "error"));
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.cancel") || "Cancel",
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                }
            ]
        });
    }

    function showEditEventPopup(eventRow) {
        if (!eventRow || !eventRow.reservationId) {
            return;
        }
        if (!can("hall.events.manage")) {
            notify(t("common.forbidden") || t("common.error"), "warning");
            return;
        }
        const halls = (lookups && lookups.halls) || [];
        let $popup;
        let $form;
        let editHijriEditor = null;

        svc().getEvent(eventRow.reservationId).then((detail) => {
            const data = detail || eventRow;
            const eventDate = data.eventDate ? new Date(data.eventDate) : new Date();

            $popup = $("<div/>").appendTo("body").dxPopup({
                title: `${t("hallOps.editEvent")} — ${data.reservationNo || ""}`,
                visible: true,
                showCloseButton: true,
                width: Math.min(720, Math.max(360, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "72vh",
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                wrapperAttr: { class: "res-extra-popup res-extra-select-popup hall-ops-event-popup" },
                onShown() {
                    setTimeout(() => {
                        if (editHijriEditor) {
                            editHijriEditor.setFromGregorian(eventDate);
                        }
                    }, 80);
                },
                onHidden() {
                    $popup.remove();
                },
                contentTemplate() {
                    const $content = $("<div/>").css({ padding: "14px", background: "var(--pms-panel-bg-strong)", borderRadius: "12px" });
                    $form = $("<div/>");
                    $content.append($form);
                    $form.dxForm({
                        formData: {
                            hallId: data.hallId,
                            eventType: data.eventType || "wedding",
                            eventDate: eventDate,
                            eventStartTime: data.eventStartTime || "18:00",
                            eventEndTime: data.eventEndTime || "23:00",
                            expectedGuests: data.expectedGuests || 0,
                            occasionName: data.occasionName || "",
                            occasionOwner: eventCustomerName(data),
                            hallRentAmount: Number(data.totalAmount != null ? data.totalAmount : data.TotalAmount) || 0,
                            depositAmount: Number(data.depositAmount != null ? data.depositAmount : data.DepositAmount) || 0
                        },
                        colCount: 2,
                        items: [
                            {
                                itemType: "simple",
                                colSpan: 2,
                                template() {
                                    return $("<div class='hall-ops-owner-readonly'/>").text(eventCustomerName(data));
                                },
                                label: { text: t("hallOps.col.occasionOwner") }
                            },
                            {
                                dataField: "hallId",
                                label: { text: t("hallOps.col.hall") },
                                editorType: "dxSelectBox",
                                editorOptions: {
                                    dataSource: halls,
                                    displayExpr(item) {
                                        return item ? (item.hallName || item.hallCode || "") : "";
                                    },
                                    valueExpr: "hallId"
                                }
                            },
                            {
                                dataField: "eventType",
                                label: { text: t("hallOps.col.eventType") },
                                editorType: "dxSelectBox",
                                editorOptions: eventTypeSelectBoxOptions()
                            },
                            {
                                itemType: "group",
                                colCount: 2,
                                colSpan: 2,
                                items: [
                                    {
                                        dataField: "eventDate",
                                        label: { text: t("hallOps.col.eventDate") },
                                        editorType: "dxDateBox",
                                        editorOptions: {
                                            type: "date",
                                            openOnFieldClick: true,
                                            displayFormat: "dd/MM/yyyy",
                                            onValueChanged(e) {
                                                if (editHijriEditor && e.value) {
                                                    editHijriEditor.setFromGregorian(e.value);
                                                }
                                            }
                                        }
                                    },
                                    {
                                        itemType: "simple",
                                        label: { text: t("hallOps.col.eventDateHijri") },
                                        template(_d, itemElement) {
                                            const hijriCal = window.Zaaer && window.Zaaer.PmsHijriCalendars;
                                            const $wrap = $("<div class='hall-ops-create-hijri'/>").appendTo(itemElement);
                                            if (hijriCal && hijriCal.isReady()) {
                                                const formInst = $form.dxForm("instance");
                                                editHijriEditor = hijriCal.attachDateBoxPicker($wrap, {
                                                    onSelect(sel) {
                                                        if (sel && sel.gregorian) {
                                                            formInst.updateData("eventDate", sel.gregorian);
                                                        }
                                                    }
                                                });
                                                editHijriEditor.setFromGregorian(eventDate);
                                            }
                                        }
                                    }
                                ]
                            },
                            { dataField: "eventStartTime", label: { text: t("hallOps.col.start") } },
                            { dataField: "eventEndTime", label: { text: t("hallOps.col.end") } },
                            { dataField: "expectedGuests", label: { text: t("hallOps.col.guests") }, editorType: "dxNumberBox" },
                            { dataField: "occasionName", label: { text: t("hallOps.col.occasion") }, colSpan: 2 },
                            {
                                itemType: "group",
                                colCount: 2,
                                colSpan: 2,
                                items: [
                                    {
                                        dataField: "hallRentAmount",
                                        label: { text: t("hallOps.col.rent") },
                                        editorType: "dxNumberBox",
                                        editorOptions: { min: 0, format: "#,##0.##" }
                                    },
                                    {
                                        dataField: "depositAmount",
                                        label: { text: t("hallOps.col.deposit") },
                                        editorType: "dxNumberBox",
                                        editorOptions: { min: 0, format: "#,##0.##" }
                                    }
                                ]
                            }
                        ]
                    });
                    return $content;
                },
                toolbarItems: [
                    {
                        widget: "dxButton",
                        toolbar: "bottom",
                        location: "after",
                        options: {
                            text: t("common.save") || "Save",
                            type: "default",
                            onClick() {
                                const formData = $form.dxForm("instance").option("formData");
                                const hijriCal = window.Zaaer && window.Zaaer.PmsHijriCalendars;
                                const payload = {
                                    hallId: Number(formData.hallId),
                                    eventType: formData.eventType,
                                    eventDate: formatLocalDateParam(formData.eventDate),
                                    eventDateHijri: editHijriEditor && hijriCal
                                        ? editHijriEditor.getStorageValue()
                                        : null,
                                    eventStartTime: formData.eventStartTime,
                                    eventEndTime: formData.eventEndTime,
                                    expectedGuests: Number(formData.expectedGuests) || 0,
                                    occasionName: formData.occasionName || null,
                                    hallRentAmount: Number(formData.hallRentAmount) || 0,
                                    depositAmount: Number(formData.depositAmount) || 0
                                };
                                svc().updateEvent(data.reservationId, payload).then(() => {
                                    notify(t("hallOps.editOk"), "success");
                                    $popup.dxPopup("instance").hide();
                                    refreshAll();
                                }).catch((err) => notify((err && err.message) || t("common.error"), "error"));
                            }
                        }
                    },
                    {
                        widget: "dxButton",
                        toolbar: "bottom",
                        location: "after",
                        options: {
                            text: t("common.cancel") || "Cancel",
                            onClick() {
                                $popup.dxPopup("instance").hide();
                            }
                        }
                    }
                ]
            });
        }).catch((err) => notify((err && err.message) || t("common.error"), "error"));
    }

    function initGuestFormLoadPanel() {
        const $lp = $("#reservationLoadPanel");
        if (!$lp.length || $lp.data("dxLoadPanel")) {
            return;
        }
        $lp.dxLoadPanel({
            shadingColor: "rgba(255,255,255,0.45)",
            position: { of: "body" },
            visible: false
        });
    }

    function boot() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        cardTheme = normalizeCardTheme(safeLocalStorageGet(CARD_THEME_STORAGE_KEY));
        applyCardThemeClass(cardTheme);
        initGuestFormLoadPanel();
        const permReady = api && typeof api.refreshPermissions === "function"
            ? api.refreshPermissions()
            : $.when();

        permReady.always(() => {
            svc().getLookups().then((data) => {
                lookups = data;
                hallHotelId = data && (data.hotelId ?? data.HotelId) ? Number(data.hotelId ?? data.HotelId) : null;
                initFilterBar($("#hallOpsFilterBar"));
                initPeriodFilterBar($("#hallOpsReportsBar"));
                initCreateButton();
                initHallUnpaidNotifyButton();
                initTabs();
                initSchedulerLayoutSync();
                ensureHallOpsEventContextMenu();
                refreshAll();
            }).catch(() => {
                notify(t("hallOps.notHallProperty"), "error", 4000);
            });
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HallOperationsPage = { boot, refreshAll };
})(window, jQuery);
