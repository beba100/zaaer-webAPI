/* global jQuery, DevExpress */
(function (window, $, DevExpress) {
    "use strict";

    let rowKeySeq = 1;

    const HIJRI_TZ = "Asia/Riyadh";

    /** Date-only API fields: local yyyy-MM-dd (never toISOString — KSA day shift). */
    function formatLocalDateParam(value) {
        if (value == null || value === "") {
            return null;
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }

        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function hijriPartsAtUtcMs(ts) {
        const fmt = new Intl.DateTimeFormat("en-US-u-ca-islamic-umalqura", {
            timeZone: HIJRI_TZ,
            day: "numeric",
            month: "numeric",
            year: "numeric"
        });
        const o = {};
        fmt.formatToParts(new Date(ts)).forEach((p) => {
            if (p.type !== "literal") {
                o[p.type] = Number(p.value);
            }
        });
        return { y: o.year, m: o.month, d: o.day };
    }

    function cmpHijriParts(p, y, m, d) {
        if (p.y !== y) {
            return p.y - y;
        }
        if (p.m !== m) {
            return p.m - m;
        }
        return p.d - d;
    }

    /**
     * Map Hijri y/m/d (Um Al Qura, same rules as Intl islamic-umalqura) to a JS Date (millisecond instant).
     */
    function hijriYmdToGregorianDate(y, m, d) {
        if (!Number.isFinite(y) || !Number.isFinite(m) || !Number.isFinite(d)) {
            return null;
        }

        const epoch = Date.UTC(1925, 0, 1) + 43200000;
        const last = Math.floor((Date.UTC(2075, 11, 31) - Date.UTC(1925, 0, 1)) / 86400000);
        let lo = 0;
        let hi = last;
        let foundTs = null;

        while (lo <= hi) {
            const mid = Math.floor((lo + hi) / 2);
            const ts = epoch + mid * 86400000;
            const p = hijriPartsAtUtcMs(ts);
            const c = cmpHijriParts(p, y, m, d);
            if (c === 0) {
                foundTs = ts;
                break;
            }
            if (c < 0) {
                lo = mid + 1;
            } else {
                hi = mid - 1;
            }
        }

        return foundTs == null ? null : new Date(foundTs);
    }

    function normalizeWesternDigits(s) {
        return `${s || ""}`
            .replace(/[\u0660-\u0669]/g, (ch) => String(ch.charCodeAt(0) - 0x0660))
            .replace(/[\u06f0-\u06f9]/g, (ch) => String(ch.charCodeAt(0) - 0x06f0));
    }

    function countHijriDigits(text) {
        return normalizeWesternDigits(`${text || ""}`)
            .replace(/\D/g, "")
            .length;
    }

    function isPartialHijriDigitCount(text) {
        const n = countHijriDigits(text);
        return n >= 1 && n < 8;
    }

    function showHijriEightDigitsHintDialog(t) {
        DevExpress.ui.dialog.alert(
            t("reservationDetail.guest.hijriEightDigitsHint"),
            t("reservationDetail.guest.hijriEightDigitsTitle")
        );
    }

    function showHijriEightDigitsToast(t) {
        DevExpress.ui.notify(t("reservationDetail.guest.hijriEightDigitsToast"), "warning", 5200);
    }

    /**
     * Accepts Hijri yyyy/MM/dd, dd/MM/yyyy (year last = 4 digits), or 8 digits (tries yyyy mm dd then dd mm yyyy).
     */
    function parseFlexibleHijriToGregorian(text) {
        const s = normalizeWesternDigits(`${text || ""}`.trim()).replace(/-/g, "/");
        if (!s) {
            return null;
        }

        let m = /^(\d{4})\s*\/\s*(\d{1,2})\s*\/\s*(\d{1,2})$/.exec(s);
        if (m) {
            return hijriYmdToGregorianDate(Number(m[1]), Number(m[2]), Number(m[3]));
        }

        m = /^(\d{1,2})\s*\/\s*(\d{1,2})\s*\/\s*(\d{4})$/.exec(s);
        if (m) {
            return hijriYmdToGregorianDate(Number(m[3]), Number(m[2]), Number(m[1]));
        }

        const digits = s.replace(/\D/g, "");
        if (digits.length === 8) {
            const gy = hijriYmdToGregorianDate(
                Number(digits.slice(0, 4)),
                Number(digits.slice(4, 6)),
                Number(digits.slice(6, 8))
            );
            if (gy) {
                return gy;
            }

            return hijriYmdToGregorianDate(
                Number(digits.slice(4, 8)),
                Number(digits.slice(2, 4)),
                Number(digits.slice(0, 2))
            );
        }

        return null;
    }

    function parseHijriInputToGregorian(text) {
        return parseFlexibleHijriToGregorian(text);
    }

    function insertSlashesYmdDigits(digitsRaw) {
        const digits = `${digitsRaw || ""}`.replace(/\D/g, "").slice(0, 8);
        if (digits.length <= 4) {
            return digits;
        }

        if (digits.length <= 6) {
            return `${digits.slice(0, 4)}/${digits.slice(4)}`;
        }

        return `${digits.slice(0, 4)}/${digits.slice(4, 6)}/${digits.slice(6, 8)}`;
    }

    function finalizeEightDigitHijri(digitsRaw) {
        const digits = `${digitsRaw || ""}`.replace(/\D/g, "").slice(0, 8);
        if (digits.length !== 8) {
            return null;
        }

        const ymd = `${digits.slice(0, 4)}/${digits.slice(4, 6)}/${digits.slice(6, 8)}`;
        if (hijriYmdToGregorianDate(Number(digits.slice(0, 4)), Number(digits.slice(4, 6)), Number(digits.slice(6, 8)))) {
            return ymd;
        }

        const dmy = `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4, 8)}`;
        if (hijriYmdToGregorianDate(Number(digits.slice(4, 8)), Number(digits.slice(2, 4)), Number(digits.slice(0, 2)))) {
            return dmy;
        }

        return ymd;
    }

    function formatHijriSlashFromDate(value) {
        if (!value) {
            return "";
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }

        const p = hijriPartsAtUtcMs(d.getTime());
        return `${p.y}/${String(p.m).padStart(2, "0")}/${String(p.d).padStart(2, "0")}`;
    }

    function hijriStorageStringFromDate(value) {
        if (!value) {
            return null;
        }

        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }

        const s = formatHijriSlashFromDate(d);
        return s || null;
    }

    function resolveInitialBirthGregorian(customer) {
        if (customer && (customer.birthdateGregorian || customer.birthday)) {
            const raw = customer.birthdateGregorian || customer.birthday;
            const d = new Date(raw);
            return Number.isNaN(d.getTime()) ? null : d;
        }

        if (customer && customer.birthdateHijri) {
            return parseHijriInputToGregorian(`${customer.birthdateHijri}`);
        }

        return null;
    }

    function wireGuestBirthDualEditors($wrap, formInstance, t) {
        const $gHost = $wrap.find(".js-guest-birth-g");
        const $hHost = $wrap.find(".js-guest-birth-h");
        if (!$gHost.length || !$hHost.length) {
            return;
        }

        let syncing = false;
        let hijriPicker = null;
        const fd0 = formInstance.option("formData") || {};
        const hijriCal = window.Zaaer && window.Zaaer.PmsHijriCalendars;

        function applyBirth(gDate) {
            formInstance.updateData("birthdateGregorian", gDate);
        }

        function syncGregorianFromHijri(gDate) {
            if (!$gHost.data("dxDateBox")) {
                return;
            }

            $gHost.dxDateBox("instance").option("value", gDate || null);
        }

        function syncHijriFromGregorian(gDate) {
            if (!hijriPicker) {
                return;
            }

            if (gDate) {
                hijriPicker.setFromGregorian(gDate);
            } else {
                hijriPicker.clear();
            }
        }

        $gHost.dxDateBox({
            value: fd0.birthdateGregorian,
            type: "date",
            displayFormat: "dd/MM/yyyy",
            useMaskBehavior: true,
            showClearButton: true,
            openOnFieldClick: true,
            inputAttr: { "aria-label": t("reservationDetail.guest.birthGregorian") },
            onValueChanged(e) {
                if (syncing) {
                    return;
                }

                syncing = true;
                applyBirth(e.value || null);
                syncHijriFromGregorian(e.value || null);
                syncing = false;
            }
        });

        $hHost.empty().addClass("guest-birth-hijri-picker-host");

        if (hijriCal && typeof hijriCal.attachDateBoxPicker === "function" && hijriCal.isReady()) {
            hijriPicker = hijriCal.attachDateBoxPicker($hHost, {
                onSelect(sel) {
                    if (syncing) {
                        return;
                    }

                    syncing = true;
                    if (sel && sel.gregorian) {
                        applyBirth(sel.gregorian);
                        syncGregorianFromHijri(sel.gregorian);
                    } else {
                        applyBirth(null);
                        syncGregorianFromHijri(null);
                    }
                    syncing = false;
                }
            });
            $hHost.data("hijriPicker", hijriPicker);

            if (fd0.birthdateGregorian) {
                hijriPicker.setFromGregorian(fd0.birthdateGregorian);
            }
            return;
        }

        $hHost.dxTextBox({
            value: formatHijriSlashFromDate(fd0.birthdateGregorian) || "",
            placeholder: "yyyy/MM/dd",
            showClearButton: true,
            stylingMode: "outlined",
            inputAttr: { "aria-label": t("reservationDetail.guest.birthHijri"), autocomplete: "off" },
            onFocusOut() {
                if (syncing) {
                    return;
                }

                const v = `${$hHost.dxTextBox("instance").option("value") || ""}`;
                if (isPartialHijriDigitCount(v)) {
                    showHijriEightDigitsToast(t);
                }
            },
            onValueChanged(e) {
                if (syncing) {
                    return;
                }

                let val = `${e.value ?? ""}`;
                const hasSlash = val.includes("/") || val.includes("-");

                if (!hasSlash) {
                    const digits = normalizeWesternDigits(val).replace(/\D/g, "");
                    let nextVal = val;
                    if (digits.length === 8) {
                        const fin = finalizeEightDigitHijri(digits);
                        if (fin != null) {
                            nextVal = fin;
                        }
                    } else {
                        nextVal = insertSlashesYmdDigits(digits);
                    }

                    if (nextVal !== val) {
                        syncing = true;
                        e.component.option("value", nextVal);
                        syncing = false;
                        val = nextVal;
                    }
                }

                const trimmed = `${val}`.trim();
                const g = trimmed ? parseFlexibleHijriToGregorian(trimmed) : null;

                syncing = true;
                if (g) {
                    applyBirth(g);
                    syncGregorianFromHijri(g);
                } else if (!trimmed) {
                    applyBirth(null);
                    syncGregorianFromHijri(null);
                }

                syncing = false;
            }
        });
    }

    function guestLookupDisplayExpr(isArabic, item) {
        if (!item) {
            return "";
        }
        const ar =
            typeof isArabic === "function"
                ? isArabic()
                : !!isArabic;
        return ar ? item.nameAr || item.name || "" : item.name || item.nameAr || "";
    }

    function splitNameForForm(fullName) {
        const p = `${fullName || ""}`.trim().split(/\s+/).filter(Boolean);
        if (p.length === 0) {
            return { firstName: "", familyName: "" };
        }
        if (p.length === 1) {
            return { firstName: p[0], familyName: "" };
        }
        // Keep it compact: first + (rest as family)
        return { firstName: p[0], familyName: p.slice(1).join(" ") };
    }

    function joinNamePartsFromForm(fd) {
        return [fd.firstName, fd.familyName]
            .map((x) => `${x || ""}`.trim())
            .filter(Boolean)
            .join(" ");
    }

    function stripMobileDial(full) {
        const s = `${full || ""}`.trim();
        if (!s) {
            return { dial: "+966", local: "" };
        }
        const m = s.match(/^(\+\d{1,4})\s*(.*)$/);
        if (m) {
            return { dial: m[1], local: `${m[2] || ""}`.replace(/\s/g, "") };
        }
        return { dial: "+966", local: s.replace(/\s/g, "") };
    }

    function unwrapCustomerApi(res) {
        if (!res) {
            return null;
        }
        if (res.data !== undefined) {
            return res.data;
        }
        if (res.Data !== undefined) {
            return res.Data;
        }
        return res;
    }

    function extractWorkPhone(comments) {
        const m = /workPhone:\s*([^\n]+)/i.exec(comments || "");
        return m ? `${m[1]}`.trim() : "";
    }

    function mergeCommentsWithWorkPhone(baseComments, workPhone) {
        const wp = `${workPhone || ""}`.trim();
        let c = `${baseComments || ""}`.replace(/\n?workPhone:\s*[^\n]*/i, "").trim();
        if (!wp) {
            return c || null;
        }
        const line = `workPhone: ${wp}`;
        return c ? `${c}\n${line}` : line;
    }

    function hasGuestPermission(code) {
        const policy = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        if (policy && typeof policy.has === "function") {
            return policy.has(code);
        }

        const svc = window.Zaaer && window.Zaaer.ApiService;
        return !!(svc && typeof svc.hasPermission === "function" && svc.hasPermission(code));
    }

    function canSaveGuestMode(mode) {
        if (mode === "edit") {
            return hasGuestPermission("guests.update");
        }

        return hasGuestPermission("guests.create");
    }

    function guestPopupHideOnOutsideClick(event) {
        const hijri = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (hijri && typeof hijri.hideOnOutsideClickForPopup === "function") {
            return hijri.hideOnOutsideClickForPopup(event);
        }
        return true;
    }

    function closeGuestPopupHijriPickers() {
        const hijri = window.Zaaer && window.Zaaer.PmsHijriCalendars;
        if (hijri && typeof hijri.closeAllPickers === "function") {
            hijri.closeAllPickers();
        }
    }

    function open(ctx) {
        const mode = ctx.mode === "edit" ? "edit" : "create";
        const customerId = ctx.customerId;
        const onDone = typeof ctx.onDone === "function" ? ctx.onDone : null;
        const onGuestUpdated = typeof ctx.onGuestUpdated === "function" ? ctx.onGuestUpdated : null;
        const t = ctx.t;
        const isArabic = ctx.isArabic;
        const pageCtx = ctx.pageCtx;
        const assignGuest = ctx.assignGuest;
        const loadPage = ctx.loadPage;
        const hotelCode = ctx.hotelCode || window.Zaaer.ApiService.getHotelCode();

        const hotelId = pageCtx.detail && pageCtx.detail.hotelId;
        if (mode === "create" && !hotelId) {
            DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 2600);
            return;
        }

        if (hotelCode) {
            window.Zaaer.ApiService.setHotelCode(hotelCode);
        }

        const $p = $("<div>").appendTo("body");
        const title =
            mode === "create"
                ? t("reservationDetail.guest.visitorCreateTitle")
                : t("reservationDetail.guest.editTitle");

        $p.dxPopup({
            width: Math.min(880, Math.max(320, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "85vh",
            showTitle: true,
            title,
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: guestPopupHideOnOutsideClick,
            wrapperAttr: { class: "guest-visitor-popup res-guest-visitor-popup" },
            onShowing(e) {
                const $content = $(e.component.content()).empty();
                const $wait = $("<div>")
                    .css({ padding: "20px", textAlign: "center" })
                    .text("…")
                    .appendTo($content);

                function scrollToEl(el) {
                    if (!el) {
                        return;
                    }
                    const node = el.get ? el.get(0) : el;
                    if (node && node.scrollIntoView) {
                        node.scrollIntoView({ behavior: "smooth", block: "center" });
                    }
                }

                function notifyAndFocus(msg, type, focusTarget) {
                    DevExpress.ui.notify(msg, type || "warning", 3400);
                    if (!focusTarget) {
                        return;
                    }

                    if (focusTarget.host && focusTarget.host.length) {
                        const $el = focusTarget.host.first();
                        scrollToEl($el);
                        const db = $el.dxDateBox("instance");
                        if (db) {
                            try {
                                db.focus();
                            } catch {
                                // ignore
                            }
                        } else {
                            const tb = $el.dxTextBox("instance");
                            if (tb) {
                                try {
                                    tb.focus();
                                } catch {
                                    // ignore
                                }
                            }
                        }

                        return;
                    }

                    // dxForm editor by dataField
                    if (focusTarget.form && focusTarget.field) {
                        const ed = focusTarget.form.getEditor(focusTarget.field);
                        if (ed && ed.element) {
                            const $el = $(ed.element());
                            scrollToEl($el);
                            try {
                                ed.focus();
                            } catch {
                                // ignore
                            }
                        }
                        return;
                    }

                    // raw element
                    scrollToEl(focusTarget);
                }

                const build = (lk, customer) => {
                    $wait.remove();
                    const baseCommentsWithoutWork = customer && customer.comments
                        ? `${customer.comments}`.replace(/\n?workPhone:\s*[^\n]*/i, "").trim()
                        : "";

                    const names =
                        mode === "edit" && customer
                            ? splitNameForForm(customer.customerName)
                            : { firstName: "", familyName: "" };
                    const dialLocal = stripMobileDial(customer && customer.mobileNo);
                    const idList =
                        mode === "edit" &&
                        customer &&
                        Array.isArray(customer.identifications) &&
                        customer.identifications.length > 0
                            ? customer.identifications.map((x) => ({
                                  rowKey: ++rowKeySeq,
                                  idTypeId: x.idTypeId,
                                  idNumber: x.idNumber || "",
                                  versionNumber: x.versionNumber || "",
                                  isPrimary: !!x.isPrimary
                              }))
                            : [
                                  {
                                      rowKey: ++rowKeySeq,
                                      idTypeId: null,
                                      idNumber: "",
                                      versionNumber: "",
                                      isPrimary: true
                                  }
                              ];

                    const formData = {
                        customerId: customer ? customer.customerId : null,
                        firstName: names.firstName,
                        familyName: names.familyName,
                        gender: customer && customer.gender ? customer.gender : null,
                        guestCategoryId: customer && customer.guestCategoryId != null ? customer.guestCategoryId : null,
                        nId: customer && customer.nId != null ? customer.nId : null,
                        gtypeId: customer && customer.gtypeId != null ? customer.gtypeId : null,
                        birthdateGregorian: resolveInitialBirthGregorian(customer),
                        mobileDial: dialLocal.dial,
                        mobileLocal: dialLocal.local,
                        workPhone: customer ? extractWorkPhone(customer.comments) : "",
                        email: customer && customer.email ? customer.email : "",
                        address: customer && customer.address ? customer.address : ""
                    };

                    const $scroll = $("<div>").addClass("guest-visitor-scroll").appendTo($content);
                    const $wrap = $("<div>").addClass("guest-visitor-body").appendTo($scroll);

                    const $form = $("<div>").appendTo($wrap);
                    let $gidGrid = null;
                    $form.dxForm({
                        formData,
                        labelLocation: "top",
                        colCount: 2,
                        showColonAfterLabel: false,
                        showRequiredMark: true,
                        requiredMark: "*",
                        items: [
                            {
                                itemType: "simple",
                                colSpan: 2,
                                template(data, itemElement) {
                                    $("<div>")
                                        .addClass("res-field-label")
                                        .css({ marginBottom: "4px", fontWeight: "600" })
                                        .text(t("reservationDetail.guest.sectionGuest"))
                                        .appendTo(itemElement);
                                }
                            },
                            {
                                dataField: "firstName",
                                label: { text: t("reservationDetail.guest.firstName") },
                                isRequired: true
                            },
                            {
                                dataField: "familyName",
                                label: { text: t("reservationDetail.guest.familyName") },
                                isRequired: true
                            },
                            {
                                dataField: "gender",
                                label: { text: t("reservationDetail.guest.genderLabel") },
                                isRequired: true,
                                editorType: "dxSelectBox",
                                editorOptions: {
                                    dataSource: [
                                        { id: "M", text: t("reservationDetail.guest.genderMale") },
                                        { id: "F", text: t("reservationDetail.guest.genderFemale") }
                                    ],
                                    valueExpr: "id",
                                    displayExpr: "text",
                                    searchEnabled: false
                                }
                            },
                            {
                                dataField: "guestCategoryId",
                                label: { text: t("reservationDetail.guest.category") },
                                editorType: "dxSelectBox",
                                editorOptions: {
                                    dataSource: lk.guestCategories,
                                    valueExpr: "id",
                                    displayExpr: (item) => guestLookupDisplayExpr(isArabic, item),
                                    searchEnabled: true,
                                    showClearButton: true
                                }
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                template(data, itemElement) {
                                    const $row = $("<div>").addClass("guest-birth-dual").appendTo(itemElement);
                                    $("<div>")
                                        .addClass("guest-birth-field")
                                        .append(
                                            $("<div>")
                                                .addClass("res-field-label")
                                                .append(
                                                    document.createTextNode(`${t("reservationDetail.guest.birthGregorian")} `),
                                                    $("<span>").addClass("guest-birth-req").text("*")
                                                ),
                                            $("<div>").addClass("js-guest-birth-g")
                                        )
                                        .appendTo($row);
                                    $("<div>")
                                        .addClass("guest-birth-field")
                                        .append(
                                            $("<div>").addClass("res-field-label").text(t("reservationDetail.guest.birthHijri")),
                                            $("<div>").addClass("js-guest-birth-h guest-birth-hijri-picker")
                                        )
                                        .appendTo($row);
                                }
                            },
                            {
                                dataField: "gtypeId",
                                label: { text: t("reservationDetail.guest.visitorType") },
                                isRequired: true,
                                editorType: "dxSelectBox",
                                editorOptions: {
                                    dataSource: lk.guestTypes,
                                    valueExpr: "id",
                                    displayExpr: (item) => guestLookupDisplayExpr(isArabic, item),
                                    searchEnabled: true
                                }
                            },
                            {
                                dataField: "nId",
                                label: { text: t("reservationDetail.guest.nationality") },
                                isRequired: true,
                                editorType: "dxSelectBox",
                                editorOptions: {
                                    dataSource: lk.nationalities,
                                    valueExpr: "id",
                                    displayExpr: (item) => guestLookupDisplayExpr(isArabic, item),
                                    searchEnabled: true
                                }
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                template(data, itemElement) {
                                    // intentionally empty: user requested removing the "Verification & ID" section label
                                }
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                template(data, itemElement) {
                                    const $h = $("<div>").addClass("js-visitor-id-grid").appendTo(itemElement);
                                    $gidGrid = $h;
                                    const po = window.Zaaer.PmsGridOptions;
                                    $h.dxDataGrid(
                                        po.merge(po.baseline(), {
                                        dataSource: idList,
                                        keyExpr: "rowKey",
                                        height: 132,
                                        searchPanel: { visible: false },
                                        headerFilter: { visible: false },
                                        editing: {
                                            mode: "cell",
                                            allowAdding: true,
                                            allowDeleting: true,
                                            allowUpdating: true
                                        },
                                        columns: [
                                            {
                                                dataField: "idTypeId",
                                                caption: t("reservationDetail.guest.idTypeCol"),
                                                validationRules: [{ type: "required" }],
                                                lookup: {
                                                    dataSource: lk.idTypes,
                                                    valueExpr: "id",
                                                    displayExpr: (x) => guestLookupDisplayExpr(isArabic, x)
                                                }
                                            },
                                            {
                                                dataField: "idNumber",
                                                caption: t("reservationDetail.guest.idNo"),
                                                validationRules: [{ type: "required" }]
                                            },
                                            { dataField: "versionNumber", caption: t("reservationDetail.guest.issueNo") }
                                        ],
                                        onInitNewRow(e) {
                                            e.data.rowKey = ++rowKeySeq;
                                            e.data.idTypeId = null;
                                            e.data.idNumber = "";
                                            e.data.versionNumber = "";
                                            e.data.isPrimary = false;
                                        }
                                        })
                                    );
                                }
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                template(data, itemElement) {
                                    $("<div>")
                                        .addClass("res-field-label")
                                        .css({ marginTop: "6px", marginBottom: "4px", fontWeight: "600" })
                                        .text(t("reservationDetail.guest.sectionContact"))
                                        .appendTo(itemElement);
                                }
                            },
                            {
                                dataField: "mobileDial",
                                label: { text: t("reservationDetail.guest.mobileDial") }
                            },
                            {
                                dataField: "mobileLocal",
                                label: { text: t("reservationDetail.guest.mobileLocal") },
                                isRequired: true
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                dataField: "workPhone",
                                label: { text: t("reservationDetail.guest.workPhone") }
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                dataField: "email",
                                label: { text: t("reservationDetail.guest.email") }
                            },
                            {
                                itemType: "simple",
                                colSpan: 2,
                                dataField: "address",
                                label: { text: t("reservationDetail.guest.address") },
                                editorType: "dxTextArea",
                                editorOptions: { height: 56 }
                            }
                        ]
                    });

                    const $footer = $("<div>").addClass("guest-visitor-footer").appendTo($content);
                    const formInstance = $form.dxForm("instance");
                    wireGuestBirthDualEditors($wrap, formInstance, t);

                    function validateAndBuildPayload() {
                        const fd = formInstance.option("formData");

                        if (!(`${fd.firstName || ""}`.trim() && `${fd.familyName || ""}`.trim())) {
                            notifyAndFocus(t("reservationDetail.guest.validationName"), "warning", { form: formInstance, field: "firstName" });
                            return null;
                        }
                        if (!fd.gender) {
                            notifyAndFocus(t("reservationDetail.guest.validationGender"), "warning", { form: formInstance, field: "gender" });
                            return null;
                        }
                        if (!fd.birthdateGregorian) {
                            notifyAndFocus(t("reservationDetail.guest.validationBirth"), "warning", { host: $wrap.find(".js-guest-birth-g") });
                            return null;
                        }

                        const hijriPicker = $wrap.find(".js-guest-birth-h").data("hijriPicker");
                        if (!hijriPicker) {
                            const hInst = $wrap.find(".js-guest-birth-h").dxTextBox("instance");
                            const hijriText = hInst ? `${hInst.option("value") || ""}` : "";
                            if (isPartialHijriDigitCount(hijriText)) {
                                showHijriEightDigitsHintDialog(t);
                                const el = $wrap.find(".js-guest-birth-h").get(0);
                                if (el && el.scrollIntoView) {
                                    el.scrollIntoView({ behavior: "smooth", block: "center" });
                                }

                                try {
                                    hInst.focus();
                                } catch {
                                    /* ignore */
                                }

                                return null;
                            }
                        }

                        if (!fd.gtypeId) {
                            notifyAndFocus(t("reservationDetail.guest.validationLookups"), "warning", { form: formInstance, field: "gtypeId" });
                            return null;
                        }
                        if (!fd.nId) {
                            notifyAndFocus(t("reservationDetail.guest.validationLookups"), "warning", { form: formInstance, field: "nId" });
                            return null;
                        }

                        const dial = (`${fd.mobileDial || ""}`.trim() || "+966").replace(/\s/g, "");
                        const local = `${fd.mobileLocal || ""}`.trim().replace(/\s/g, "");
                        if (!local) {
                            notifyAndFocus(t("reservationDetail.guest.validationMobile"), "warning", { form: formInstance, field: "mobileLocal" });
                            return null;
                        }
                        const mobileNo = `${dial}${local}`;

                        const customerName = joinNamePartsFromForm(fd);

                        const gridInstance = $gidGrid ? $gidGrid.dxDataGrid("instance") : null;
                        if (!gridInstance) {
                            notifyAndFocus(t("reservationDetail.guest.validationIds"), "warning", $wrap);
                            return null;
                        }

                        // Commit cell edits
                        if (gridInstance.hasEditData && gridInstance.hasEditData()) {
                            gridInstance.saveEditData();
                        }

                        const idRows = gridInstance.option("dataSource") || [];
                        const validRows = idRows.filter((r) => r && r.idTypeId && `${r.idNumber || ""}`.trim());
                        if (validRows.length === 0) {
                            notifyAndFocus(t("reservationDetail.guest.validationIds"), "warning", $gidGrid);
                            return null;
                        }

                        const identifications = validRows.map((r, idx) => ({
                            idTypeId: r.idTypeId,
                            idNumber: `${r.idNumber}`.trim(),
                            versionNumber: r.versionNumber ? `${r.versionNumber}`.trim() : null,
                            isPrimary: idx === 0,
                            isActive: true
                        }));

                        const commentsForCreate =
                            mode === "create" && `${fd.workPhone || ""}`.trim()
                                ? `workPhone: ${fd.workPhone.trim()}`
                                : null;

                        const hijriPickerInst = $wrap.find(".js-guest-birth-h").data("hijriPicker");
                        const hijriStored =
                            hijriPickerInst && typeof hijriPickerInst.getStorageValue === "function"
                                ? hijriPickerInst.getStorageValue()
                                : hijriStorageStringFromDate(fd.birthdateGregorian);
                        const birthGregorian = formatLocalDateParam(fd.birthdateGregorian);

                        if (mode === "create") {
                            return {
                                kind: "create",
                                body: {
                                    customerName,
                                    gtypeId: fd.gtypeId,
                                    nId: fd.nId,
                                    guestCategoryId: fd.guestCategoryId || null,
                                    gender: fd.gender || null,
                                    birthdateGregorian: birthGregorian,
                                    birthday: birthGregorian,
                                    birthdateHijri: hijriStored,
                                    mobileNo,
                                    email: fd.email || null,
                                    address: fd.address || null,
                                    comments: commentsForCreate,
                                    hotelId,
                                    identifications
                                }
                            };
                        }

                        return {
                            kind: "update",
                            customerKey: fd.customerId,
                            body: {
                                customerId: fd.customerId,
                                customerName,
                                gtypeId: fd.gtypeId,
                                nId: fd.nId,
                                guestCategoryId: fd.guestCategoryId || null,
                                gender: fd.gender || null,
                                birthdateGregorian: birthGregorian,
                                birthday: birthGregorian,
                                birthdateHijri: hijriStored,
                                mobileNo,
                                email: fd.email || null,
                                address: fd.address || null,
                                comments: mergeCommentsWithWorkPhone(baseCommentsWithoutWork, fd.workPhone),
                                identifications
                            }
                        };
                    }

                    $("<div>")
                        .appendTo($footer)
                        .dxButton({
                            text: t("common.close"),
                            icon: "close",
                            stylingMode: "outlined",
                            type: "normal",
                            onClick() {
                                $p.dxPopup("instance").hide();
                            }
                        });

                    const guestSaveAllowed = canSaveGuestMode(mode);
                    $("<div>")
                        .appendTo($footer)
                        .dxButton({
                            text: t("reservationDetail.guest.saveVisitor"),
                            icon: "save",
                            type: "default",
                            stylingMode: "contained",
                            visible: guestSaveAllowed,
                            disabled: !guestSaveAllowed,
                            onClick() {
                                if (!canSaveGuestMode(mode)) {
                                    DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
                                    return;
                                }

                                const payload = validateAndBuildPayload();
                                if (!payload) {
                                    return;
                                }

                                let lp = null;
                                const $lpHost = $("#reservationLoadPanel");
                                if ($lpHost.length) {
                                    try {
                                        lp = $lpHost.dxLoadPanel("instance");
                                    } catch {
                                        lp = null;
                                    }
                                }
                                if (lp) {
                                    lp.show();
                                }

                                const pmsCust = window.Zaaer.PmsCustomerService;
                                if (!pmsCust) {
                                    DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3600);
                                    if (lp) {
                                        lp.hide();
                                    }
                                    return;
                                }

                                if (payload.kind === "create") {
                                    pmsCust
                                        .createCustomer(payload.body)
                                        .then((cust) => {
                                            const id = pmsCust.reservationCustomerId(cust);
                                            DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                                            $p.dxPopup("instance").hide();
                                            if (id) {
                                                assignGuest(id, cust);
                                            }
                                            if (onDone) {
                                                onDone();
                                            }
                                        })
                                        .catch((xhr) => {
                                            console.error("create customer failed", xhr);
                                            DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3600);
                                        })
                                        .finally(() => lp && lp.hide());
                                } else {
                                    pmsCust
                                        .updateCustomer(payload.customerKey, payload.body, hotelId)
                                        .then((cust) => {
                                            DevExpress.ui.notify(t("reservationDetail.savedOk"), "success", 2000);
                                            $p.dxPopup("instance").hide();
                                            const id = pmsCust.reservationCustomerId(cust);
                                            if (onGuestUpdated && id) {
                                                onGuestUpdated(id, cust);
                                            } else if (id && assignGuest) {
                                                assignGuest(id, cust);
                                            } else if (loadPage) {
                                                loadPage(false);
                                            }
                                            if (onDone) {
                                                onDone();
                                            }
                                        })
                                        .catch((xhr) => {
                                            console.error("update customer failed", xhr);
                                            DevExpress.ui.notify(t("error.saveReservationDetail"), "error", 3600);
                                        })
                                        .finally(() => lp && lp.hide());
                                }
                            }
                        });
                };

                if (mode === "edit" && customerId) {
                    Promise.all([
                        window.Zaaer.ReservationDetailService.loadGuestFormLookups(),
                        window.Zaaer.PmsCustomerService.getCustomer(customerId, hotelId)
                    ])
                        .then(([lk, c]) => {
                            if (!c || (!c.customerId && !c.CustomerId)) {
                                DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 3200);
                                $p.dxPopup("instance").hide();
                                return;
                            }
                            build(lk, c);
                        })
                        .catch(() => {
                            DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 3200);
                            $p.dxPopup("instance").hide();
                        });
                } else {
                    window.Zaaer.ReservationDetailService.loadGuestFormLookups()
                        .then((lk) => build(lk, null))
                        .catch(() => {
                            DevExpress.ui.notify(t("error.loadReservationDetail"), "error", 3200);
                            $p.dxPopup("instance").hide();
                        });
                }
            },
            onHidden() {
                closeGuestPopupHijriPickers();
                $p.remove();
            }
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.GuestVisitorForm = { open };
})(window, jQuery, DevExpress);
