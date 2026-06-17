(function (window, $) {
    "use strict";

    let detailHandler = null;

    function activityLogT(key) {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        if (loc && typeof loc.t === "function") {
            return loc.t(key);
        }

        if (typeof window.t === "function") {
            return window.t(key);
        }

        return key;
    }

    function isActivityLogArabic() {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        if (loc && typeof loc.currentCulture === "function") {
            return loc.currentCulture() === "ar";
        }

        return document.documentElement.lang === "ar" || document.documentElement.dir === "rtl";
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
            const locale = isActivityLogArabic() ? "ar-SA-u-ca-gregory" : "en-GB";
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
            return d.toLocaleDateString();
        }
    }

    function formatActivityLogMoney(amount) {
        const n = Number(amount);
        if (!Number.isFinite(n)) {
            return "";
        }

        return `${DevExpress.localization.formatNumber(n, "#,##0.00")} SAR`;
    }

    function formatActivityLogDateOnly(value) {
        if (!value) {
            return "";
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            const s = String(value).trim();
            if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
                const parts = s.slice(0, 10).split("-");
                return `${parts[2]}/${parts[1]}/${parts[0]}`;
            }

            return s;
        }

        const dd = String(d.getDate()).padStart(2, "0");
        const mm = String(d.getMonth() + 1).padStart(2, "0");
        const yyyy = d.getFullYear();
        return `${dd}/${mm}/${yyyy}`;
    }

    function formatActivityLogRentalType(value) {
        const raw = String(value || "")
            .trim()
            .toLowerCase();

        if (!raw) {
            return "";
        }

        if (raw.includes("month")) {
            return activityLogT("roomBoard.rentalType.monthly");
        }

        if (raw.includes("day") || raw.includes("daily")) {
            return activityLogT("roomBoard.rentalType.daily");
        }

        if (raw.includes("year")) {
            return activityLogT("roomBoard.rentalType.yearly");
        }

        if (raw.includes("hour")) {
            return activityLogT("roomBoard.rentalType.inhour");
        }

        return String(value);
    }

    function parseJsonIfNeeded(value) {
        if (typeof value !== "string") {
            return value;
        }

        const trimmed = value.trim();
        if (!trimmed || (trimmed[0] !== "{" && trimmed[0] !== "[")) {
            return value;
        }

        try {
            return JSON.parse(trimmed);
        } catch {
            return value;
        }
    }

    function unwrapScalar(value) {
        if (value == null) {
            return value;
        }

        if (typeof value === "object" && !Array.isArray(value) && value.valueKind != null) {
            if (value.valueKind === 3 && value.value != null) {
                return value.value;
            }

            if (value.valueKind === 4 && value.value != null) {
                return value.value;
            }
        }

        return value;
    }

    function readPayloadField(payload, keys) {
        if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
            return null;
        }

        const list = Array.isArray(keys) ? keys : [keys];
        for (let i = 0; i < list.length; i += 1) {
            const key = list[i];
            if (payload[key] != null && payload[key] !== "") {
                return unwrapScalar(payload[key]);
            }
        }

        const lower = Object.create(null);
        Object.keys(payload).forEach((k) => {
            lower[String(k).toLowerCase()] = payload[k];
        });

        for (let i = 0; i < list.length; i += 1) {
            const key = String(list[i]).toLowerCase();
            if (lower[key] != null && lower[key] !== "") {
                return unwrapScalar(lower[key]);
            }
        }

        return null;
    }

    function normalizeActivityPayload(row) {
        if (!row || typeof row !== "object") {
            return {};
        }

        let p = row.payload != null ? row.payload : row.Payload;
        p = parseJsonIfNeeded(p);
        if (!p || typeof p !== "object" || Array.isArray(p)) {
            return {};
        }

        return p;
    }

    function normalizeActivityRow(row) {
        if (!row || typeof row !== "object") {
            return row;
        }

        return {
            ...row,
            eventKey: row.eventKey || row.EventKey || "",
            refType: row.refType || row.RefType || "",
            refId: row.refId != null ? row.refId : row.RefId,
            refNo: row.refNo || row.RefNo || "",
            reservationNo: row.reservationNo || row.ReservationNo || "",
            amountFrom: row.amountFrom != null ? row.amountFrom : row.AmountFrom,
            amountTo: row.amountTo != null ? row.amountTo : row.AmountTo,
            createdAt: row.createdAt || row.CreatedAt,
            payload: normalizeActivityPayload(row)
        };
    }

    function resolveActivityLogActorLabel(row) {
        const payload = normalizeActivityPayload(row);
        if (isActivityLogArabic()) {
            const first = row.actorFirstName || payload.actorFirstName || "";
            const last = row.actorLastName || payload.actorLastName || "";
            const full = `${first} ${last}`.trim();
            if (full) {
                return full;
            }

            const ar = payload.actorNameAr;
            if (ar) {
                return String(ar);
            }
        }

        return (
            row.actorUsername ||
            payload.actorUsername ||
            row.actorDisplayName ||
            payload.actorName ||
            row.createdBy ||
            "—"
        );
    }

    function applyActivityLogReplacements(template, replacements) {
        let html = $("<span>").text(template).html();
        replacements.forEach((r) => {
            if (r.value == null || r.value === "") {
                return;
            }

            const safe = $("<span>").text(String(r.value)).html();
            const mark = `<span class="${r.cls}">${safe}</span>`;
            html = html.split(r.token).join(mark);
            if (r.altToken) {
                html = html.split(r.altToken).join(mark);
            }
        });

        return html;
    }

    function buildActivityLogMessageHtml(row) {
        const normalized = normalizeActivityRow(row);
        const payload = normalized.payload || {};
        const key = activityLogI18nKey(normalized.eventKey);
        let template = activityLogT(key);
        if (!template || template === key) {
            template = normalized.eventKey || "";
        }

        const actorName = resolveActivityLogActorLabel(normalized);
        const reservationNo =
            readPayloadField(payload, ["reservationNo", "ReservationNo"]) || normalized.reservationNo || "";
        const receiptNo =
            readPayloadField(payload, ["receiptNo", "ReceiptNo"]) || normalized.refNo || "";
        const promissoryNo =
            readPayloadField(payload, ["promissoryNo", "PromissoryNo"]) || normalized.refNo || "";
        const invoiceNo = readPayloadField(payload, ["invoiceNo", "InvoiceNo"]) || "";
        const creditNoteNo =
            readPayloadField(payload, ["creditNoteNo", "CreditNoteNo"]) || normalized.refNo || "";
        const debitNoteNo =
            readPayloadField(payload, ["debitNoteNo", "DebitNoteNo"]) || normalized.refNo || "";
        const amountRaw =
            readPayloadField(payload, ["amount", "Amount", "grossRate", "GrossRate"]) ??
            (normalized.amountTo != null ? normalized.amountTo : null);
        const amount = amountRaw != null ? formatActivityLogMoney(amountRaw) : "";
        const amountFromRaw =
            normalized.amountFrom != null
                ? normalized.amountFrom
                : readPayloadField(payload, ["amountFrom", "AmountFrom"]);
        const amountToRaw =
            normalized.amountTo != null
                ? normalized.amountTo
                : readPayloadField(payload, ["amountTo", "AmountTo", "amount", "Amount", "grossRate", "GrossRate"]) ??
                  amountRaw;
        const amountFrom = amountFromRaw != null ? formatActivityLogMoney(amountFromRaw) : "";
        const amountTo = amountToRaw != null ? formatActivityLogMoney(amountToRaw) : "";
        const date = formatActivityLogDateTime(normalized.createdAt);
        const fromDate = formatActivityLogDateOnly(
            readPayloadField(payload, ["fromDate", "FromDate"])
        );
        const toDate = formatActivityLogDateOnly(readPayloadField(payload, ["toDate", "ToDate"]));
        const rentalType = formatActivityLogRentalType(
            readPayloadField(payload, ["rentalType", "RentalType"])
        );

        const replacements = [
            { token: "{actorName}", altToken: "{{actorName}}", value: actorName, cls: "activity-log-hl activity-log-hl--actor" },
            { token: "{reservationNo}", altToken: "{{reservationNo}}", value: reservationNo, cls: "activity-log-hl activity-log-hl--res" },
            { token: "{receiptNo}", altToken: "{{receiptNo}}", value: receiptNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{promissoryNo}", altToken: "{{promissoryNo}}", value: promissoryNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{invoiceNo}", altToken: "{{invoiceNo}}", value: invoiceNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{creditNoteNo}", altToken: "{{creditNoteNo}}", value: creditNoteNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{debitNoteNo}", altToken: "{{debitNoteNo}}", value: debitNoteNo, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{amount}", altToken: "{{amount}}", value: amount, cls: "activity-log-hl activity-log-hl--money" },
            { token: "{amountFrom}", altToken: "{{amountFrom}}", value: amountFrom, cls: "activity-log-hl activity-log-hl--money" },
            { token: "{amountTo}", altToken: "{{amountTo}}", value: amountTo, cls: "activity-log-hl activity-log-hl--money" },
            { token: "{fromDate}", altToken: "{{fromDate}}", value: fromDate, cls: "activity-log-hl activity-log-hl--date" },
            { token: "{toDate}", altToken: "{{toDate}}", value: toDate, cls: "activity-log-hl activity-log-hl--date" },
            { token: "{rentalType}", altToken: "{{rentalType}}", value: rentalType, cls: "activity-log-hl activity-log-hl--ref" },
            { token: "{date}", altToken: "{{date}}", value: date, cls: "activity-log-hl activity-log-hl--date" }
        ];

        return applyActivityLogReplacements(template, replacements);
    }

    function canShowActivityLogDetails(row) {
        if (!row) {
            return false;
        }

        const normalized = normalizeActivityRow(row);
        const payload = normalized.payload || {};
        const refType = String(normalized.refType || "").toLowerCase();
        const eventKey = String(normalized.eventKey || "").toLowerCase();
        const hasRefId = normalized.refId != null && normalized.refId !== "";
        const receiptNo =
            readPayloadField(payload, ["receiptNo", "ReceiptNo"]) || normalized.refNo || "";
        const promissoryNo =
            readPayloadField(payload, ["promissoryNo", "PromissoryNo"]) || normalized.refNo || "";

        if (
            hasRefId &&
            (refType === "paymentreceipt" ||
                refType === "paymentrefund" ||
                refType.includes("receipt") ||
                refType.includes("refund"))
        ) {
            return true;
        }

        if (eventKey.startsWith("payment.") || eventKey.includes("refund") || eventKey.includes("receipt")) {
            if (hasRefId || receiptNo) {
                return true;
            }
        }

        if (hasRefId && (refType === "promissorynote" || refType.includes("promissory"))) {
            return true;
        }

        if (eventKey.startsWith("promissory.") || eventKey.includes("promissory")) {
            if (hasRefId || promissoryNo) {
                return true;
            }
        }

        if (hasRefId && refType === "note") {
            return true;
        }

        if (hasRefId && refType === "discount") {
            return true;
        }

        return false;
    }

    function renderActivityLogTimeline($host, rows) {
        $host.empty();
        const list = Array.isArray(rows) ? rows.slice() : [];
        const emptyText = activityLogT("activityLog.empty");

        if (!list.length) {
            $("<p>").addClass("activity-log-empty").text(emptyText).appendTo($host);
            return;
        }

        const $timeline = $("<div>").addClass("activity-log-timeline").appendTo($host);
        list.forEach((rawRow) => {
            const row = normalizeActivityRow(rawRow);
            const icon = row.iconKey || row.IconKey || "info";
            const $item = $("<article>").addClass("activity-log-item").appendTo($timeline);
            $("<div>")
                .addClass("activity-log-item__rail")
                .append(
                    $("<span>")
                        .addClass(`activity-log-item__icon dx-icon dx-icon-${icon}`)
                        .attr("aria-hidden", "true")
                )
                .appendTo($item);

            const $body = $("<div>").addClass("activity-log-item__body").appendTo($item);
            const actor = resolveActivityLogActorLabel(row);
            $("<div>").addClass("activity-log-item__actor").text(actor).appendTo($body);
            $("<div>")
                .addClass("activity-log-item__when")
                .text(formatActivityLogDateTime(row.createdAt))
                .appendTo($body);
            $("<div>")
                .addClass("activity-log-item__text")
                .html(buildActivityLogMessageHtml(row))
                .appendTo($body);

            if (canShowActivityLogDetails(row)) {
                $("<button>", { type: "button" })
                    .addClass("activity-log-item__details-btn")
                    .text(activityLogT("activityLog.viewDetails"))
                    .on("click", () => {
                        if (typeof detailHandler === "function") {
                            detailHandler(row);
                            return;
                        }

                        DevExpress.ui.notify(activityLogT("activityLog.detailsPending"), "info", 2600);
                    })
                    .appendTo($body);
            }
        });
    }

    window.PmsActivityLogRender = {
        renderActivityLogTimeline,
        setDetailHandler(handler) {
            detailHandler = typeof handler === "function" ? handler : null;
        }
    };
})(window, jQuery);
