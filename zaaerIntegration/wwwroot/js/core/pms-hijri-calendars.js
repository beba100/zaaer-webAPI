(function (window, $) {
    "use strict";

    let regionalReady = false;

    function ummalquraCal(lang) {
        if (!$.calendars) {
            return null;
        }
        ensureRegional();
        try {
            return $.calendars.instance("ummalqura", lang || getHijriLanguage());
        } catch (_e) {
            return $.calendars.instance("ummalqura");
        }
    }

    function gregorianCal() {
        return $.calendars ? $.calendars.instance() : null;
    }

    function isReady() {
        return !!(ummalquraCal() && gregorianCal() && $.fn.calendarsPicker);
    }

    function isArabicUi() {
        return !!(
            window.Zaaer &&
            window.Zaaer.LocalizationService &&
            typeof window.Zaaer.LocalizationService.currentCulture === "function" &&
            window.Zaaer.LocalizationService.currentCulture() === "ar"
        );
    }

    function getHijriLanguage() {
        return isArabicUi() ? "ar" : "";
    }

    function ensureRegional() {
        if (regionalReady || !$.calendars || !$.calendars.calendars.ummalqura) {
            return;
        }
        const proto = $.calendars.calendars.ummalqura.prototype;
        if (!proto.regionalOptions.ar) {
            proto.regionalOptions.ar = {
                name: "أم القرى",
                epochs: ["ب.هـ", "هـ"],
                monthNames: [
                    "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
                    "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
                ],
                monthNamesShort: [
                    "محرم", "صفر", "ربيع١", "ربيع٢", "جمادى١", "جمادى٢",
                    "رجب", "شعبان", "رمضان", "شوال", "قعدة", "حجة"
                ],
                dayNames: ["الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"],
                dayNamesShort: ["أحد", "إثن", "ثلا", "أرب", "خمي", "جمع", "سبت"],
                dayNamesMin: ["ح", "ن", "ث", "ر", "خ", "ج", "س"],
                digits: null,
                dateFormat: "yyyy/mm/dd",
                firstDay: 6,
                isRTL: true
            };
        }
        if ($.calendarsPicker && !$.calendarsPicker.regionalOptions.ar) {
            $.calendarsPicker.regionalOptions.ar = $.extend({}, $.calendarsPicker.regionalOptions[""], {
                prevText: "السابق",
                nextText: "التالي",
                currentText: "الحالي",
                todayText: "اليوم",
                clearText: "مسح",
                closeText: "إغلاق",
                isRTL: true
            });
        }
        regionalReady = true;
    }

    function cDateToJs(cDate) {
        const calG = gregorianCal();
        if (!cDate || !calG) {
            return null;
        }
        const jd = cDate.toJD();
        const g = calG.fromJD(jd);
        return new Date(g.year(), g.month() - 1, g.day());
    }

    function jsToHijriCDate(value) {
        const calG = gregorianCal();
        const calH = ummalquraCal();
        if (!calG || !calH || value == null) {
            return null;
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        const g = calG.newDate(d.getFullYear(), d.getMonth() + 1, d.getDate());
        return calH.fromJD(g.toJD());
    }

    function hijriPartsFromGregorian(value) {
        const h = jsToHijriCDate(value);
        if (!h) {
            return null;
        }
        return { y: h.year(), m: h.month(), d: h.day() };
    }

    function formatStorageFromCDate(cDate) {
        if (!cDate) {
            return "";
        }
        return `${cDate.year()}-${String(cDate.month()).padStart(2, "0")}-${String(cDate.day()).padStart(2, "0")}`;
    }

    function formatSlashFromGregorian(value) {
        const p = hijriPartsFromGregorian(value);
        if (!p) {
            return "";
        }
        return `${p.y}/${String(p.m).padStart(2, "0")}/${String(p.d).padStart(2, "0")}`;
    }

    function formatDayMonthFromGregorian(value) {
        const p = hijriPartsFromGregorian(value);
        if (!p) {
            return "";
        }
        return `${String(p.d).padStart(2, "0")}/${String(p.m).padStart(2, "0")}`;
    }

    function formatDayMonthYearFromGregorian(value) {
        const p = hijriPartsFromGregorian(value);
        if (!p) {
            return "";
        }
        return `${String(p.d).padStart(2, "0")}/${String(p.m).padStart(2, "0")}/${p.y}`;
    }

    function formatStorageFromGregorian(value) {
        const h = jsToHijriCDate(value);
        return h ? formatStorageFromCDate(h) : "";
    }

    function formatHijriLongFromGregorian(value) {
        const h = jsToHijriCDate(value);
        if (!h) {
            return "";
        }
        const cal = ummalquraCal();
        return cal.formatDate("d MMMM yyyy", h);
    }

    function formatHijriLongFromStorage(storage) {
        const g = parseStorageToGregorian(storage);
        return g ? formatHijriLongFromGregorian(g) : "";
    }

    function formatHijriDisplayFromCDate(cDate) {
        if (!cDate) {
            return "";
        }
        const cal = ummalquraCal();
        const label = cal.formatDate("d MMMM yyyy", cDate);
        const suffix = isArabicUi() ? " هـ" : " H";
        return `${label}${suffix}`;
    }

    function parseStorageToGregorian(storage) {
        if (!storage || !isReady()) {
            return null;
        }
        const m = `${storage}`.trim().match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
        if (!m) {
            return null;
        }
        const calH = ummalquraCal();
        const calG = gregorianCal();
        const h = calH.newDate(Number(m[1]), Number(m[2]), Number(m[3]));
        const g = calG.fromJD(h.toJD());
        return new Date(g.year(), g.month() - 1, g.day());
    }

    function pickerRegional() {
        ensureRegional();
        return isArabicUi()
            ? $.calendarsPicker.regionalOptions.ar
            : $.calendarsPicker.regionalOptions[""];
    }

    function buildPickerApi($input, calH, refreshDisplay, opts) {
        return {
            open() {
                $input.calendarsPicker("show");
            },
            getStorageValue() {
                const dates = $input.calendarsPicker("getDate");
                return dates && dates[0] ? formatStorageFromCDate(dates[0]) : "";
            },
            getGregorianDate() {
                const dates = $input.calendarsPicker("getDate");
                return dates && dates[0] ? cDateToJs(dates[0]) : null;
            },
            setFromGregorian(value) {
                const h = jsToHijriCDate(value);
                if (h) {
                    $input.calendarsPicker("setDate", h);
                    refreshDisplay(h);
                } else {
                    this.clear();
                }
            },
            clear() {
                $input.calendarsPicker("clear");
                $input.val("");
                refreshDisplay(null);
            }
        };
    }

    /** Stack hijri popup above visible DevExtreme overlays (dxPopup, drop-downs, etc.). */
    function resolvePickerZIndex() {
        let max = 10000;
        $(".dx-overlay-wrapper:visible").each(function () {
            const z = parseInt(window.getComputedStyle(this).zIndex, 10);
            if (!Number.isNaN(z) && z > max) {
                max = z;
            }
        });
        return max + 100;
    }

    function applyPickerZIndex(el, z) {
        if (!el || el.length === 0) {
            return;
        }
        el.each(function () {
            this.style.setProperty("z-index", String(z), "important");
        });
    }

    function elevatePickerOverlay(inst, picker) {
        const z = resolvePickerZIndex();
        if (inst && inst.div) {
            $(inst.div).addClass("pms-hijri-picker-popup-shell");
            applyPickerZIndex($(inst.div), z);
            wirePickerShellEvents($(inst.div));
        }
        const $bodyPopup = $("body > div.calendars-popup:visible");
        applyPickerZIndex($bodyPopup, z);
        wirePickerShellEvents($bodyPopup);
        if (picker) {
            applyPickerZIndex($(picker), z);
            wirePickerShellEvents($(picker).closest("div.calendars-popup"));
        }
    }

    function pickerEventTargetInsideShell(target) {
        if (!target || typeof target.closest !== "function") {
            return false;
        }
        return !!target.closest(
            "div.calendars-popup, .calendars-popup, .pms-hijri-picker-popup, .pms-hijri-datebox, .pms-hijri-datebox__field"
        );
    }

    function isPickerVisible() {
        return $("body > div.calendars-popup:visible").length > 0;
    }

    /**
     * dxPopup hideOnOutsideClick handler — return false to keep the host popup open.
     */
    function hideOnOutsideClickForPopup(event) {
        const raw = event && (event.originalEvent || event);
        return !pickerEventTargetInsideShell(raw && raw.target);
    }

    function wirePickerShellEvents($shell) {
        if (!$shell || !$shell.length) {
            return;
        }
        $shell
            .off(".pmsHijriPickerGuard")
            .on("pointerdown.pmsHijriPickerGuard mousedown.pmsHijriPickerGuard click.pmsHijriPickerGuard", (e) => {
                e.stopPropagation();
            });
    }

    function installDocumentOutsideClickGuard() {
        if (installDocumentOutsideClickGuard._done) {
            return;
        }
        installDocumentOutsideClickGuard._done = true;

        const guard = (e) => {
            if (pickerEventTargetInsideShell(e.target)) {
                e.stopImmediatePropagation();
            }
        };

        document.addEventListener("pointerdown", guard, true);
        document.addEventListener("mousedown", guard, true);
    }

    function closeAllPickers() {
        $(".pms-hijri-datebox__input").each(function () {
            try {
                $(this).calendarsPicker("hide");
            } catch (_e) {
                /* not initialized */
            }
        });
        $("body > div.calendars-popup:visible").hide();
    }

    /**
     * dxDateBox-style Hijri picker: click field or calendar icon opens Umm al-Qura popup.
     */
    function attachDateBoxPicker($host, options) {
        const opts = options || {};
        if (!isReady()) {
            return null;
        }

        ensureRegional();
        $host.addClass("pms-hijri-picker-host pms-hijri-datebox-host");
        if (opts.label) {
            $("<label class='pms-hijri-picker-label'/>").text(opts.label).appendTo($host);
        }

        const $box = $("<div class='pms-hijri-datebox'/>").appendTo($host);
        const $field = $("<div class='pms-hijri-datebox__field'/>").appendTo($box);
        const $input = $("<input type='text' readonly class='pms-hijri-datebox__input' autocomplete='off'/>").appendTo($field);
        const $iconBtn = $("<button type='button' class='pms-hijri-datebox__icon' aria-label='Hijri calendar'/>")
            .append($("<span class='dx-icon dx-icon-event'/>"))
            .appendTo($field);

        const calH = ummalquraCal();
        const regional = pickerRegional();
        let syncing = false;

        function refreshDisplay(cDate) {
            if (!cDate) {
                $input.val("").removeClass("is-set");
                $field.removeClass("is-set");
                return;
            }
            $input.val(formatHijriDisplayFromCDate(cDate)).addClass("is-set");
            $field.addClass("is-set");
        }

        function openPicker() {
            $input.calendarsPicker("show");
            window.requestAnimationFrame(() => {
                const z = resolvePickerZIndex();
                const $shell = $("body > div.calendars-popup:visible");
                $shell.addClass("pms-hijri-picker-popup-shell");
                applyPickerZIndex($shell, z);
                wirePickerShellEvents($shell);
            });
        }

        $input.calendarsPicker(
            $.extend({}, regional, {
                calendar: calH,
                dateFormat: "yyyy/mm/dd",
                isRTL: isArabicUi(),
                showAnim: "",
                popupContainer: "body",
                pickerClass: "pms-hijri-picker-popup",
                onShow(picker, _calendar, inst) {
                    elevatePickerOverlay(inst, picker);
                },
                onSelect() {
                    if (syncing) {
                        return;
                    }
                    const dates = $input.calendarsPicker("getDate");
                    const h = dates && dates[0] ? dates[0] : null;
                    refreshDisplay(h);
                    if (typeof opts.onSelect === "function") {
                        opts.onSelect({
                            hijri: h,
                            storage: h ? formatStorageFromCDate(h) : "",
                            gregorian: h ? cDateToJs(h) : null
                        });
                    }
                }
            })
        );

        $input.on("click", (e) => {
            e.preventDefault();
            e.stopPropagation();
            openPicker();
        });
        $input.on("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                openPicker();
            }
        });
        $iconBtn.on("click", (e) => {
            e.preventDefault();
            e.stopPropagation();
            openPicker();
        });

        const api = buildPickerApi($input, calH, refreshDisplay, opts);
        const baseSetFromGregorian = api.setFromGregorian.bind(api);
        api.setFromGregorian = function (value) {
            syncing = true;
            baseSetFromGregorian(value);
            syncing = false;
        };
        return api;
    }

    /** @deprecated use attachDateBoxPicker */
    function attachPopupPicker($host, options) {
        return attachDateBoxPicker($host, options);
    }

    function attachPicker($host, options) {
        return attachDateBoxPicker($host, options);
    }

    installDocumentOutsideClickGuard();

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsHijriCalendars = {
        isReady,
        isArabicUi,
        getHijriLanguage,
        hijriPartsFromGregorian,
        formatSlashFromGregorian,
        formatDayMonthFromGregorian,
        formatDayMonthYearFromGregorian,
        formatStorageFromGregorian,
        formatHijriLongFromGregorian,
        formatHijriLongFromStorage,
        formatHijriDisplayFromCDate,
        parseStorageToGregorian,
        isPickerVisible,
        hideOnOutsideClickForPopup,
        closeAllPickers,
        attachPicker,
        attachPopupPicker,
        attachDateBoxPicker
    };
})(window, window.jQuery);
