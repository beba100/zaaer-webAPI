(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const ratesApi = window.Zaaer.UnitRatesService;
    const api = window.Zaaer.ApiService;
    const SG = window.Zaaer && window.Zaaer.SaveGuard;

    const CALENDAR_DAYS = 14;

    let loadPanel;
    let calendarRangeStart = startOfToday();
    let calendarData = null;
    let calendarGrid = null;
    let baseGrid = null;

    let dragSelect = null;
    let calendarDragActive = false;
    let suppressPriceCellClick = false;
    let $calendarGridHost = null;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canView() {
        return api.hasPermission("property.rates.view");
    }

    function canManage() {
        return api.hasPermission("property.rates.manage");
    }

    function startOfToday() {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function dateFieldKey(isoDate) {
        return `d_${String(isoDate).replace(/-/g, "")}`;
    }

    function addDays(date, days) {
        const d = new Date(date.getTime());
        d.setDate(d.getDate() + days);
        return d;
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function roomTypeDisplay(row) {
        if (!row) {
            return "";
        }
        return !isAr() && row.roomTypeNameEn ? row.roomTypeNameEn : row.roomTypeName || "";
    }

    function parseDayHeader(isoDate) {
        const parts = String(isoDate || "").split("-");
        if (parts.length !== 3) {
            return { weekday: "", day: "", month: "" };
        }
        const d = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
        if (Number.isNaN(d.getTime())) {
            return { weekday: "", day: "", month: "" };
        }
        // ar-SA defaults to Hijri; force Gregorian for readable PMS calendar headers.
        const locale = isAr() ? "ar" : "en-GB";
        const cal = { calendar: "gregory" };
        return {
            weekday: new Intl.DateTimeFormat(locale, { ...cal, weekday: "short" }).format(d),
            day: String(d.getDate()),
            month: new Intl.DateTimeFormat(locale, { ...cal, month: "short" }).format(d)
        };
    }

    function renderDayHeader(headerContainer, day) {
        const h = parseDayHeader(day.date);
        const $wrap = $("<div/>").addClass("unit-rates-day-header");
        $("<div/>").addClass("unit-rates-day-header__dow").text(h.weekday).appendTo($wrap);
        $("<div/>").addClass("unit-rates-day-header__day").text(h.day).appendTo($wrap);
        $("<div/>").addClass("unit-rates-day-header__mon").text(h.month).appendTo($wrap);
        if (day.isWeekend) {
            $(headerContainer).closest("td").addClass("unit-rates-weekend-col");
        }
        $wrap.appendTo(headerContainer);
    }

    function flattenCalendarRows(data) {
        const rows = [];
        (data.rows || []).forEach((r) => {
            const flat = {
                roomTypeId: r.roomTypeId,
                roomTypeName: r.roomTypeName,
                rowKind: r.rowKind,
                rowLabel:
                    r.rowKind === "availability"
                        ? t("property.rates.row.availability")
                        : t("property.rates.row.price")
            };
            (r.cells || []).forEach((cell) => {
                flat[dateFieldKey(cell.date)] = cell;
            });
            rows.push(flat);
        });
        return rows;
    }

    function buildCalendarColumns(days) {
        const cols = [
            {
                dataField: "roomTypeName",
                caption: t("property.rates.col.unitType"),
                width: 200,
                fixed: true,
                fixedPosition: isAr() ? "right" : "left",
                cssClass: "unit-rates-room-type-cell",
                cellTemplate(container, info) {
                    if (info.data.rowKind !== "availability") {
                        return;
                    }
                    $("<span>").text(roomTypeDisplay(info.data)).appendTo(container);
                }
            },
            {
                dataField: "rowLabel",
                caption: "",
                width: 150,
                fixed: true,
                fixedPosition: isAr() ? "right" : "left",
                cssClass: "unit-rates-row-label",
                cellTemplate(container, info) {
                    $("<span>").text(info.value || "").appendTo(container);
                }
            }
        ];

        (days || []).forEach((day) => {
            const field = dateFieldKey(day.date);
            cols.push({
                name: field,
                dataField: field,
                caption: day.date,
                width: 84,
                alignment: "center",
                cssClass: day.isWeekend ? "unit-rates-weekend-col" : "",
                headerCellTemplate(headerContainer) {
                    renderDayHeader(headerContainer, day);
                },
                cellTemplate(container, info) {
                    const cell = info.value;
                    const $td = $(info.cellElement);
                    $td.removeClass(
                        "unit-rates-cell-zero unit-rates-cell-override unit-rates-cell-selected unit-rates-price-cell"
                    );

                    if (!cell) {
                        return;
                    }

                    if (day.isWeekend) {
                        $td.addClass("unit-rates-weekend-col");
                    }

                    if (info.data.rowKind === "availability") {
                        const avail = cell.availableUnits != null ? cell.availableUnits : 0;
                        const total = cell.totalUnits != null ? cell.totalUnits : 0;
                        const $wrap = $("<span/>").addClass("unit-rates-avail-cell");
                        $("<span/>").addClass("unit-rates-avail-total").text(total).appendTo($wrap);
                        $("<span/>").addClass("unit-rates-avail-count").text(` (${avail})`).appendTo($wrap);
                        $wrap.appendTo(container);
                        if (avail === 0 && total > 0) {
                            $td.addClass("unit-rates-cell-zero");
                        }
                        return;
                    }

                    if (info.data.rowKind === "price") {
                        $td.addClass("unit-rates-price-cell");
                        $td.attr({
                            "data-room-type-id": info.data.roomTypeId,
                            "data-room-type-name": info.data.roomTypeName,
                            "data-rate-date": day.date
                        });
                        if (cell.isOverride) {
                            $td.addClass("unit-rates-cell-override");
                        }
                        const priceText =
                            cell.price != null && cell.price !== "" ? Number(cell.price).toFixed(0) : "—";
                        $("<span>").text(priceText).appendTo(container);
                    }
                }
            });
        });

        return cols;
    }

    function clearDragSelect() {
        dragSelect = null;
        calendarDragActive = false;
        updateSelectionHighlight();
    }

    function updateSelectionHighlight() {
        if (!$calendarGridHost) {
            return;
        }
        $calendarGridHost.find("td.unit-rates-price-cell").each(function () {
            const $td = $(this);
            const rtId = Number($td.attr("data-room-type-id"));
            const dateIso = String($td.attr("data-rate-date") || "");
            const on =
                dragSelect &&
                dragSelect.roomTypeId === rtId &&
                dragSelect.dates &&
                dragSelect.dates.indexOf(dateIso) >= 0;
            $td.toggleClass("unit-rates-cell-selected", !!on);
        });
    }

    function colNameToIso(name) {
        const raw = String(name || "").replace(/^d_/, "");
        if (raw.length !== 8) {
            return "";
        }
        return `${raw.slice(0, 4)}-${raw.slice(4, 6)}-${raw.slice(6, 8)}`;
    }

    function readPriceCellMeta($td) {
        return {
            roomTypeId: Number($td.attr("data-room-type-id")),
            dateIso: String($td.attr("data-rate-date") || ""),
            roomTypeName: String($td.attr("data-room-type-name") || "")
        };
    }

    function extendDragSelectionToCell($td) {
        if (!calendarDragActive || !dragSelect || !dragSelect.anchor || !$td || !$td.length) {
            return;
        }

        const meta = readPriceCellMeta($td);
        if (meta.roomTypeId !== dragSelect.roomTypeId || !meta.dateIso) {
            return;
        }

        const days = (calendarData && calendarData.days) || [];
        const allDates = days.map((d) => d.date);
        const i0 = allDates.indexOf(dragSelect.anchor);
        const i1 = allDates.indexOf(meta.dateIso);
        if (i0 < 0 || i1 < 0) {
            return;
        }

        const lo = Math.min(i0, i1);
        const hi = Math.max(i0, i1);
        dragSelect.dates = allDates.slice(lo, hi + 1);
        updateSelectionHighlight();
    }

    function bindCalendarGridDrag($host) {
        $calendarGridHost = $host;
        $host.off(".unitRatesDrag");
        $(document).off(".unitRatesDrag");

        $host.on("mousedown.unitRatesDrag", "td.unit-rates-price-cell", function (e) {
            if (!canManage() || e.button !== 0) {
                return;
            }
            const meta = readPriceCellMeta($(this));
            if (!meta.roomTypeId || !meta.dateIso) {
                return;
            }
            calendarDragActive = true;
            dragSelect = {
                roomTypeId: meta.roomTypeId,
                roomTypeName: meta.roomTypeName,
                dates: [meta.dateIso],
                anchor: meta.dateIso
            };
            updateSelectionHighlight();
            e.preventDefault();
            e.stopPropagation();
        });

        $(document).on("mousemove.unitRatesDrag", function (e) {
            if (!calendarDragActive || !dragSelect || !dragSelect.anchor || (e.buttons & 1) !== 1) {
                return;
            }

            const el = document.elementFromPoint(e.clientX, e.clientY);
            if (!el) {
                return;
            }

            const $td = $(el).closest("td.unit-rates-price-cell");
            if (!$td.length || !$host[0].contains($td[0])) {
                return;
            }

            extendDragSelectionToCell($td);
        });

        $(document).on("mouseup.unitRatesDrag", function () {
            if (!calendarDragActive || !dragSelect) {
                calendarDragActive = false;
                return;
            }
            const sel = {
                roomTypeId: dragSelect.roomTypeId,
                roomTypeName: dragSelect.roomTypeName,
                dates: dragSelect.dates.slice()
            };
            clearDragSelect();
            suppressPriceCellClick = true;
            openRateEditPopup(sel);
            window.setTimeout(() => {
                suppressPriceCellClick = false;
            }, 80);
        });
    }

    function getInitialPriceForSelection(sel) {
        if (!calendarData || !sel.dates || !sel.dates.length) {
            return null;
        }
        const row = (calendarData.rows || []).find(
            (r) => r.roomTypeId === sel.roomTypeId && r.rowKind === "price"
        );
        if (!row) {
            return null;
        }
        const cell = (row.cells || []).find((c) => c.date === sel.dates[0]);
        return cell && cell.price != null ? cell.price : null;
    }

    function formatDurationLabel(dates) {
        if (!dates || !dates.length) {
            return "";
        }
        if (dates.length === 1) {
            return dates[0];
        }
        return `${dates[0]} — ${dates[dates.length - 1]}`;
    }

    function patchCalendarSelectionRates(sel, grossRate) {
        if (!calendarData || !sel.dates || !sel.dates.length) {
            return;
        }
        const dateSet = new Set(sel.dates);
        const priceRow = (calendarData.rows || []).find(
            (r) => r.roomTypeId === sel.roomTypeId && r.rowKind === "price"
        );
        if (!priceRow) {
            return;
        }
        (priceRow.cells || []).forEach((cell) => {
            if (dateSet.has(cell.date)) {
                cell.price = grossRate;
                cell.isOverride = true;
            }
        });
        if (calendarGrid) {
            calendarGrid.refresh();
            updateSelectionHighlight();
        }
    }

    function openRateEditPopup(sel) {
        if (!canManage()) {
            return;
        }

        const initialPrice = getInitialPriceForSelection(sel);
        const state = {
            grossRate: initialPrice != null ? initialPrice : null
        };

        const $shell = $("<div/>").addClass("unit-rates-edit-popup");
        $("<p/>").addClass("unit-rates-edit-hint").text(t("property.rates.hint")).appendTo($shell);
        $("<div/>")
            .addClass("unit-rates-edit-kv")
            .append($("<strong/>").text(t("property.rates.roomType")), $("<span/>").text(sel.roomTypeName))
            .appendTo($shell);
        $("<div/>")
            .addClass("unit-rates-edit-kv")
            .append($("<strong/>").text(t("property.rates.duration")), $("<span/>").text(formatDurationLabel(sel.dates)))
            .appendTo($shell);
        const $priceHost = $("<div/>").appendTo($shell);

        const $popup = $("<div>").appendTo("body");
        let applyBtn;
        const popupSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title: t("property.rates.editTitle"),
            visible: true,
            showCloseButton: true,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup unit-rates-edit-popup-wrap" },
            contentTemplate() {
                return $shell;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("property.rates.apply"),
                        type: "default",
                        stylingMode: "contained",
                        onInitialized(e) {
                            applyBtn = e.component;
                        },
                        onClick() {
                            const work = () =>
                                ratesApi
                                    .upsertDailyRates({
                                        roomTypeId: sel.roomTypeId,
                                        dateFrom: sel.dates[0],
                                        dateTo: sel.dates[sel.dates.length - 1],
                                        grossRate: state.grossRate
                                    })
                                    .then(() => {
                                        patchCalendarSelectionRates(sel, state.grossRate);
                                        if (SG && SG.hidePopup) {
                                            SG.hidePopup($popup);
                                        } else {
                                            $popup.dxPopup("instance").hide();
                                        }
                                        DevExpress.ui.notify(t("common.saved"), "success", 2200);
                                        clearDragSelect();
                                    })
                                    .catch((xhr) => {
                                        const msg =
                                            (xhr && xhr.responseJSON && (xhr.responseJSON.message || xhr.responseJSON.detail)) ||
                                            (xhr && xhr.responseJSON && xhr.responseJSON.error) ||
                                            t("common.error");
                                        DevExpress.ui.notify(msg, "error", 3600);
                                    });

                            if (popupSaveGuard && SG.run) {
                                SG.run(popupSaveGuard, work, { button: applyBtn });
                            } else {
                                work();
                            }
                        }
                    }
                }
            ],
            onHidden() {
                $popup.remove();
            }
        });

        $priceHost.dxNumberBox({
            value: state.grossRate,
            min: 0,
            format: "#,##0.##",
            showSpinButtons: true,
            label: t("property.rates.price"),
            labelMode: "floating",
            onValueChanged(e) {
                state.grossRate = e.value;
            }
        });
    }

    function reloadCalendar() {
        const from = formatLocalDateParam(calendarRangeStart);
        const to = formatLocalDateParam(addDays(calendarRangeStart, CALENDAR_DAYS - 1));
        return ratesApi.getRatesCalendar({ fromDate: from, toDate: to }).then((data) => {
            calendarData = data;
            const flat = flattenCalendarRows(data);
            if (calendarGrid) {
                calendarGrid.option("columns", buildCalendarColumns(data.days));
                calendarGrid.option("dataSource", flat);
                updateSelectionHighlight();
            }
        });
    }

    function initCalendarTab($panel) {
        const $toolbar = $("<div/>").addClass("unit-rates-calendar-toolbar").appendTo($panel);
        $("<span/>").addClass("unit-rates-range-label").text(t("property.rates.rangeStart")).appendTo($toolbar);

        const $rangeBox = $("<div/>").appendTo($toolbar);
        const $prev = $("<div/>").appendTo($toolbar);
        const $next = $("<div/>").appendTo($toolbar);

        const $gridHost = $("<div/>").addClass("unit-rates-calendar-host unit-rates-calendar-host--tall").appendTo($panel);

        $rangeBox.dxDateBox({
            type: "date",
            value: calendarRangeStart,
            openOnFieldClick: true,
            width: 160,
            onValueChanged(e) {
                if (e.value) {
                    calendarRangeStart = new Date(e.value);
                    calendarRangeStart.setHours(0, 0, 0, 0);
                    reloadCalendar();
                }
            }
        });

        $prev.dxButton({
            icon: isAr() ? "chevronnext" : "chevronprev",
            hint: t("property.rates.prev"),
            onClick() {
                calendarRangeStart = addDays(calendarRangeStart, -CALENDAR_DAYS);
                $rangeBox.dxDateBox("instance").option("value", calendarRangeStart);
                reloadCalendar();
            }
        });

        $next.dxButton({
            icon: isAr() ? "chevronprev" : "chevronnext",
            hint: t("property.rates.next"),
            onClick() {
                calendarRangeStart = addDays(calendarRangeStart, CALENDAR_DAYS);
                $rangeBox.dxDateBox("instance").option("value", calendarRangeStart);
                reloadCalendar();
            }
        });

        const po = window.Zaaer.PmsGridOptions;
        calendarGrid = $gridHost
            .dxDataGrid(
                po.merge(po.baseline(), {
                dataSource: [],
                columns: [],
                rowAlternationEnabled: false,
                columnAutoWidth: false,
                hoverStateEnabled: false,
                scrolling: po.scrollingOptions(),
                paging: { enabled: false },
                searchPanel: { visible: false },
                headerFilter: { visible: false },
                elementAttr: { class: "unit-rates-calendar-grid" },
                onRowPrepared(e) {
                    if (e.rowType !== "data") {
                        return;
                    }
                    if (e.data.rowKind === "price") {
                        e.rowElement.addClass("unit-rates-price-row");
                    } else {
                        e.rowElement.addClass("unit-rates-availability-row");
                    }
                },
                onCellPrepared(e) {
                    if (e.rowType !== "data" || e.data.rowKind !== "price") {
                        return;
                    }
                    const col = e.column;
                    if (!col || !col.name || String(col.name).indexOf("d_") !== 0) {
                        return;
                    }
                    const dateIso = colNameToIso(col.name);
                    const $cell = $(e.cellElement);
                    $cell.addClass("unit-rates-price-cell");
                    $cell.attr({
                        "data-room-type-id": e.data.roomTypeId,
                        "data-room-type-name": e.data.roomTypeName,
                        "data-rate-date": dateIso
                    });
                },
                onCellClick(e) {
                    if (suppressPriceCellClick || calendarDragActive || !canManage()) {
                        return;
                    }
                    if (e.rowType !== "data" || e.data.rowKind !== "price") {
                        return;
                    }
                    const col = e.column;
                    if (!col || !col.name || String(col.name).indexOf("d_") !== 0) {
                        return;
                    }
                    const dateIso = colNameToIso(col.name);
                    if (!dateIso) {
                        return;
                    }
                    openRateEditPopup({
                        roomTypeId: e.data.roomTypeId,
                        roomTypeName: e.data.roomTypeName,
                        dates: [dateIso]
                    });
                }
                })
            )
            .dxDataGrid("instance");

        bindCalendarGridDrag($gridHost);
        reloadCalendar();
    }

    function openBaseRateEditor(row) {
        const state = {
            roomTypeId: row.roomTypeId,
            dailyRateLowWeekdays: row.dailyRateLowWeekdays,
            dailyRateHighWeekdays: row.dailyRateHighWeekdays,
            dailyRateMin: row.dailyRateMin,
            monthlyRate: row.monthlyRate,
            monthlyRateMin: row.monthlyRateMin,
            otaRateLowWeekdays: row.otaRateLowWeekdays,
            otaRateHighWeekdays: row.otaRateHighWeekdays
        };

        const $shell = $("<div/>");
        const $form = $("<div/>").appendTo($shell);

        const $popup = $("<div>").appendTo("body");
        let saveBtn;
        const popupSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title: t("property.rates.base.edit"),
            visible: true,
            showCloseButton: true,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate() {
                return $shell;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.save"),
                        type: "default",
                        stylingMode: "contained",
                        onInitialized(e) {
                            saveBtn = e.component;
                        },
                        onClick() {
                            const work = () =>
                                ratesApi.updateRoomTypeRate(row.rateId || 0, state).then((updated) => {
                                    DevExpress.ui.notify(t("common.saved"), "success", 2200);
                                    if (baseGrid && updated) {
                                        const ds = baseGrid.option("dataSource") || [];
                                        const idx = ds.findIndex((r) => r.roomTypeId === updated.roomTypeId);
                                        if (idx >= 0) {
                                            ds[idx] = updated;
                                            baseGrid.option("dataSource", ds.slice());
                                        }
                                    }
                                });

                            if (popupSaveGuard && SG.run) {
                                SG.run(popupSaveGuard, work, { button: saveBtn });
                            } else {
                                work();
                            }
                        }
                    }
                }
            ],
            onHidden() {
                $popup.remove();
            }
        });

        $form.dxForm({
            formData: state,
            labelLocation: "top",
            colCount: 2,
            items: [
                {
                    itemType: "group",
                    caption: t("property.rates.group.daily"),
                    colCount: 2,
                    colSpan: 2,
                    items: [
                        { dataField: "dailyRateHighWeekdays", label: { text: t("property.rates.col.dailyHigh") }, editorType: "dxNumberBox", editorOptions: { min: 0, format: "#,##0.##" } },
                        { dataField: "dailyRateMin", label: { text: t("property.rates.col.dailyMin") }, editorType: "dxNumberBox", editorOptions: { min: 0, format: "#,##0.##" } }
                    ]
                },
                {
                    itemType: "group",
                    caption: t("property.rates.group.monthly"),
                    colCount: 2,
                    colSpan: 2,
                    items: [
                        { dataField: "monthlyRate", label: { text: t("property.rates.col.monthly") }, editorType: "dxNumberBox", editorOptions: { min: 0, format: "#,##0.##" } },
                        { dataField: "monthlyRateMin", label: { text: t("property.rates.col.monthlyMin") }, editorType: "dxNumberBox", editorOptions: { min: 0, format: "#,##0.##" } }
                    ]
                }
            ]
        });
    }

    function baseRateHeaderTemplate(colCaption) {
        return function (headerContainer) {
            const $container = $(headerContainer);
            $container.empty();
            if (!colCaption) {
                return;
            }

            $("<div/>")
                .addClass("unit-rates-col-header unit-rates-col-header--single")
                .append($("<div/>").addClass("unit-rates-col-header__label").text(colCaption))
                .appendTo($container);
        };
    }

    function baseRateNumberColumn(dataField, colCaption, width) {
        return {
            name: dataField,
            dataField,
            caption: "",
            dataType: "number",
            format: "#,##0.##",
            alignment: "center",
            width,
            allowHeaderFiltering: false,
            allowSorting: false,
            cssClass: "unit-rates-rate-col",
            headerCellTemplate: baseRateHeaderTemplate(colCaption)
        };
    }

    function buildBaseRateColumns() {
        return [
            {
                name: "actions",
                type: "buttons",
                width: 56,
                minWidth: 56,
                caption: "",
                fixed: true,
                fixedPosition: isAr() ? "right" : "left",
                allowHeaderFiltering: false,
                allowSorting: false,
                visible: canManage(),
                headerCellTemplate: baseRateHeaderTemplate(null),
                buttons: [
                    {
                        hint: t("property.rates.base.edit"),
                        icon: "edit",
                        onClick(e) {
                            openBaseRateEditor(e.row.data);
                        }
                    }
                ]
            },
            {
                name: "roomTypeName",
                dataField: "roomTypeName",
                caption: "",
                minWidth: 170,
                fixed: true,
                fixedPosition: isAr() ? "right" : "left",
                allowHeaderFiltering: false,
                allowSorting: false,
                cssClass: "unit-rates-unit-type-col",
                headerCellTemplate: baseRateHeaderTemplate(t("property.rates.col.unitType")),
                calculateDisplayValue: (r) => roomTypeDisplay(r)
            },
            baseRateNumberColumn("dailyRateHighWeekdays", t("property.rates.col.dailyHigh"), 120),
            baseRateNumberColumn("dailyRateMin", t("property.rates.col.dailyMin"), 140),
            baseRateNumberColumn("monthlyRate", t("property.rates.col.monthly"), 110),
            baseRateNumberColumn("monthlyRateMin", t("property.rates.col.monthlyMin"), 140)
        ];
    }

    function initBaseTab($panel) {
        const $gridHost = $("<div/>").addClass("unit-rates-base-host").appendTo($panel);

        const po = window.Zaaer.PmsGridOptions;
        baseGrid = $gridHost
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                elementAttr: { class: "unit-rates-base-grid" },
                columnAutoWidth: false,
                headerFilter: { visible: false },
                paging: { pageSize: 50 },
                pager: po.adminPager({ allowedPageSizes: [10, 20, 50] }),
                columns: buildBaseRateColumns()
                })
            )
            .dxDataGrid("instance");

        ratesApi.listRoomTypeRates().then((rows) => {
            baseGrid.option("dataSource", rows || []);
        });
    }

    function initTabs() {
        const $tabs = $("#unitRatesTabs");
        const $calendarPanel = $("<div/>");
        const $basePanel = $("<div/>");

        $tabs.dxTabPanel({
            deferRendering: false,
            animationEnabled: false,
            items: [
                { title: t("property.rates.tabs.calendar"), template: () => $calendarPanel },
                { title: t("property.rates.tabs.base"), template: () => $basePanel }
            ]
        });

        initCalendarTab($calendarPanel);
        initBaseTab($basePanel);
    }

    $(function () {
        if (!canView()) {
            DevExpress.ui.notify(t("common.forbidden") || "Forbidden", "error", 3000);
            return;
        }

        loadPanel = $("#unitRatesLoadPanel")
            .dxLoadPanel({ shadingColor: "rgba(0,0,0,0.12)", visible: false, showIndicator: true })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-property-rates",
            onRefresh() {
                withLoad($.when(reloadCalendar(), ratesApi.listRoomTypeRates().then((rows) => {
                    if (baseGrid) {
                        baseGrid.option("dataSource", rows || []);
                    }
                })));
            }
        });

        initTabs();
    });
})(window, jQuery);
