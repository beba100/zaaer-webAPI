(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    function t(key) {
        if (loc && typeof loc.t === "function") {
            return loc.t(key);
        }
        if (loc && typeof loc.get === "function") {
            return loc.get(key);
        }
        return key;
    }

    function isArabic() {
        if (loc && typeof loc.isArabic === "function") {
            return loc.isArabic();
        }
        return document.documentElement.dir === "rtl";
    }

    function ensureDevExtremeWidget(widgetName) {
        const plugin = $ && $.fn && $.fn[widgetName];
        if (typeof plugin !== "function") {
            throw new Error(`DevExtreme widget "${widgetName}" is unavailable. Ensure /Lib/js/dx.all.js loaded before this page script.`);
        }
    }

    function createWidget($host, widgetName, options) {
        ensureDevExtremeWidget(widgetName);
        return $host[widgetName](options);
    }

    function unwrapList(res) {
        if (Array.isArray(res)) return res;
        if (res && Array.isArray(res.data)) return res.data;
        return [];
    }

    const now = new Date();

    function maxMonthForYear(year) {
        const currentYear = now.getFullYear();
        if (year < currentYear) {
            return 12;
        }

        if (year > currentYear) {
            return 0;
        }

        // Current year: only completed months (hide the in-progress month).
        return now.getMonth();
    }

    function defaultBaladyFilter() {
        const year = now.getFullYear();
        const maxMonth = maxMonthForYear(year);
        if (maxMonth >= 1) {
            return { year, month: maxMonth };
        }

        return { year: year - 1, month: 12 };
    }

    const filterState = defaultBaladyFilter();

    let lastReportRows = [];

    function buildYearItems() {
        const current = now.getFullYear();
        const items = [];
        for (let y = current - 5; y <= current; y += 1) {
            items.push(y);
        }
        return items;
    }

    function buildMonthItems(year) {
        const gregorianMonthsAr = [
            "يناير",
            "فبراير",
            "مارس",
            "أبريل",
            "مايو",
            "يونيو",
            "يوليو",
            "أغسطس",
            "سبتمبر",
            "أكتوبر",
            "نوفمبر",
            "ديسمبر"
        ];
        const gregorianMonthsEn = [
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        ];
        const names = isArabic() ? gregorianMonthsAr : gregorianMonthsEn;
        const maxMonth = maxMonthForYear(year);
        return Array.from({ length: maxMonth }, (_, index) => ({
            value: index + 1,
            text: names[index]
        }));
    }

    function clampMonthForYear(year, month) {
        const max = maxMonthForYear(year);
        if (max <= 0) {
            return 1;
        }

        return Math.min(Math.max(1, month), max);
    }

    function syncMonthSelectBox(year, preferredMonth) {
        if (!monthSelectBox) {
            return;
        }

        let effectiveYear = year;
        let maxMonth = maxMonthForYear(effectiveYear);
        if (maxMonth <= 0) {
            const fallback = defaultBaladyFilter();
            effectiveYear = fallback.year;
            maxMonth = maxMonthForYear(effectiveYear);
            filterState.year = fallback.year;
            filterState.month = fallback.month;
            if (yearSelectBox) {
                yearSelectBox.option("value", fallback.year);
            }
        }

        const items = buildMonthItems(effectiveYear);
        const month = clampMonthForYear(effectiveYear, preferredMonth);
        filterState.month = month;
        monthSelectBox.option("items", items);
        monthSelectBox.option("value", month);
    }

    function buildQuery() {
        return {
            year: filterState.year,
            month: filterState.month
        };
    }

    function buildExportBasename() {
        const hotel = api.getHotelCode() || api.getHotelName() || "hotel";
        const month = String(filterState.month).padStart(2, "0");
        return `balady-${hotel}-${filterState.year}-${month}`;
    }

    function formatDateCell(value) {
        if (!value) return "";
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) return "";
        const month = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${month}/${day}/${d.getFullYear()}`;
    }

    function formatAmount(value) {
        const n = Number(value);
        if (Number.isNaN(n)) return "";
        return n.toLocaleString("en-GB", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function getJsPdfConstructor() {
        if (window.jspdf && typeof window.jspdf.jsPDF === "function") {
            return window.jspdf.jsPDF;
        }
        if (typeof window.jsPDF === "function") {
            return window.jsPDF;
        }
        return null;
    }

    function baladyGridHostHeight() {
        const host = document.getElementById("baladyGrid");
        if (!host) {
            return 0;
        }

        const measured = Math.floor(host.clientHeight);
        if (measured > 80) {
            return measured;
        }

        const top = host.getBoundingClientRect().top;
        return Math.max(280, Math.floor(window.innerHeight - top - 16));
    }

    function applyBaladyGridHeight() {
        if (!window.__baladyGrid) {
            return;
        }

        const height = baladyGridHostHeight();
        window.__baladyGrid.option("height", height);
        window.__baladyGrid.updateDimensions();
    }

    function scheduleBaladyGridResize() {
        applyBaladyGridHeight();
        requestAnimationFrame(applyBaladyGridHeight);
    }

    function bindBaladyGridResize() {
        if (window.__baladyGridResizeBound) {
            scheduleBaladyGridResize();
            return;
        }

        window.__baladyGridResizeBound = true;
        $(window).on("resize.baladyReport", scheduleBaladyGridResize);

        const host = document.getElementById("baladyGrid");
        const workspace = host && host.closest(".room-board-workspace");
        const card = host && host.closest(".pms-integration-balady-card");
        const shell = document.querySelector(".room-board-shell");

        if (typeof ResizeObserver !== "undefined") {
            const observer = new ResizeObserver(() => scheduleBaladyGridResize());
            [host, card, workspace].forEach((el) => {
                if (el) {
                    observer.observe(el);
                }
            });
            window.__baladyGridResizeObserver = observer;
        }

        if (shell) {
            const navToggle = document.getElementById("roomBoardNavToggle");
            if (navToggle) {
                navToggle.addEventListener("click", () => {
                    setTimeout(scheduleBaladyGridResize, 80);
                    setTimeout(scheduleBaladyGridResize, 280);
                });
            }
        }
    }

    function displayRoomType(name) {
        const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;
        if (labeler && typeof labeler.display === "function") {
            return labeler.display(name, t);
        }
        return name;
    }

    function gridColumns() {
        return [
            { dataField: "roomNumber", caption: t("integrations.balady.col.roomNumber") },
            {
                dataField: "periodFrom",
                caption: t("integrations.balady.col.periodFrom"),
                dataType: "string",
                allowHeaderFiltering: false,
                calculateCellValue(row) {
                    return formatDateCell(row.periodFrom);
                }
            },
            {
                dataField: "periodTo",
                caption: t("integrations.balady.col.periodTo"),
                dataType: "string",
                allowHeaderFiltering: false,
                calculateCellValue(row) {
                    return formatDateCell(row.periodTo);
                }
            },
            {
                dataField: "amount",
                caption: t("integrations.balady.col.amount"),
                dataType: "number",
                width: 118,
                alignment: "right",
                cssClass: "pms-balady-amount-col pms-balady-amount-en",
                customizeText(info) {
                    return formatAmount(info.value);
                }
            },
            {
                dataField: "customerName",
                caption: t("integrations.balady.col.customerName"),
                cssClass: "pms-balady-customer-col",
                minWidth: 160
            },
            { dataField: "bookingNumber", caption: t("integrations.balady.col.bookingNumber") },
            {
                dataField: "roomType",
                caption: t("integrations.balady.col.roomType"),
                calculateDisplayValue(row) {
                    return displayRoomType(row.roomType);
                }
            },
            { dataField: "notes", caption: t("integrations.balady.col.notes") }
        ];
    }

    function loadScriptOnce(src) {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing) {
                if (existing.getAttribute("data-loaded") === "true") {
                    resolve();
                    return;
                }
                existing.addEventListener("load", () => resolve(), { once: true });
                existing.addEventListener("error", () => reject(new Error(`Failed to load ${src}`)), { once: true });
                return;
            }

            const script = document.createElement("script");
            script.src = src;
            script.onload = () => {
                script.setAttribute("data-loaded", "true");
                resolve();
            };
            script.onerror = () => reject(new Error(`Failed to load ${src}`));
            document.head.appendChild(script);
        });
    }

    function ensurePdfExportLibs() {
        const jsPdfCtor = getJsPdfConstructor();
        if (jsPdfCtor && typeof jsPdfCtor.prototype.autoTable === "function") {
            return Promise.resolve();
        }

        return loadScriptOnce("/Lib/js/jspdf.umd.min.js")
            .then(() => loadScriptOnce("/Lib/js/jspdf.plugin.autotable.min.js"));
    }

    function exportBaladyExcel(e) {
        if (!window.ExcelJS || !DevExpress.excelExporter || typeof DevExpress.excelExporter.exportDataGrid !== "function") {
            DevExpress.ui.notify(t("integrations.balady.exportExcelFailed") || "Excel export is unavailable.", "error", 3000);
            if (e) {
                e.cancel = true;
            }
            return;
        }

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet("Balady");

        DevExpress.excelExporter.exportDataGrid({
            component: e.component,
            worksheet,
            autoFilterEnabled: true,
            customizeCell(options) {
                const field = options.gridCell.column && options.gridCell.column.dataField;
                if (options.gridCell.rowType !== "data" || !field) {
                    return;
                }
                if (field === "amount") {
                    const amount = Number(options.gridCell.value);
                    if (!Number.isNaN(amount)) {
                        options.excelCell.value = amount;
                        options.excelCell.numFmt = "#,##0.00";
                    }
                }
                if (field === "periodFrom" || field === "periodTo") {
                    options.excelCell.value = formatDateCell(options.gridCell.value);
                }
                if (field === "roomType") {
                    options.excelCell.value = displayRoomType(options.gridCell.value);
                }
            }
        }).then(() => workbook.xlsx.writeBuffer())
            .then((buffer) => {
                saveAs(
                    new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }),
                    `${buildExportBasename()}.xlsx`
                );
            })
            .catch((err) => {
                console.error(err);
                DevExpress.ui.notify(t("integrations.balady.exportExcelFailed") || "Excel export failed.", "error", 3000);
            });

        e.cancel = true;
    }

    function exportBaladyPdf() {
        ensurePdfExportLibs().then(() => {
            const JsPdfCtor = getJsPdfConstructor();
            if (!JsPdfCtor) {
                DevExpress.ui.notify(t("integrations.balady.exportPdfFailed") || "PDF export library not loaded.", "error", 3000);
                return;
            }

            exportBaladyPdfDocument(JsPdfCtor);
        }).catch((err) => {
            console.error(err);
            DevExpress.ui.notify(t("integrations.balady.exportPdfFailed") || "PDF export library not loaded.", "error", 3000);
        });
    }

    function exportBaladyPdfDocument(JsPdfCtor) {

        const rows = lastReportRows.length
            ? lastReportRows
            : (window.__baladyGrid ? window.__baladyGrid.getDataSource().items() : []);

        if (!rows.length) {
            DevExpress.ui.notify(t("common.noData") || "No data", "warning", 2500);
            return;
        }

        const columns = gridColumns();
        const headers = columns.map((col) => col.caption);
        const body = rows.map((row) => columns.map((col) => {
            const value = row[col.dataField];
            if (col.dataField === "periodFrom" || col.dataField === "periodTo") {
                return formatDateCell(value);
            }
            if (col.dataField === "amount") {
                return formatAmount(value);
            }
            if (col.dataField === "roomType") {
                return displayRoomType(value);
            }
            return value == null ? "" : String(value);
        }));

        const doc = new JsPdfCtor({ orientation: "landscape", unit: "pt", format: "a4" });
        doc.setFontSize(12);
        doc.text(`Balady Report ${filterState.year}-${String(filterState.month).padStart(2, "0")}`, 40, 32);
        doc.setFontSize(9);
        doc.text(`Year: ${filterState.year}   Month: ${filterState.month}`, 40, 48);

        if (typeof doc.autoTable !== "function") {
            DevExpress.ui.notify(t("integrations.balady.exportPdfFailed") || "PDF export failed.", "error", 3000);
            return;
        }

        doc.autoTable({
            head: [headers],
            body,
            startY: 58,
            styles: { fontSize: 8, cellPadding: 3 },
            headStyles: { fillColor: [63, 111, 159] },
            margin: { left: 24, right: 24 }
        });

        doc.save(`${buildExportBasename()}.pdf`);
    }

    let yearSelectBox;
    let monthSelectBox;

    function resetBaladyGridState() {
        const grid = window.__baladyGrid;
        if (!grid) {
            return;
        }

        try {
            grid.clearFilter();
        } catch {
            /* ignore */
        }

        try {
            grid.clearSorting();
        } catch {
            /* ignore */
        }

        try {
            grid.option("filterValue", null);
            grid.option("searchPanel.text", "");
            grid.option("paging.pageIndex", 0);
        } catch {
            /* ignore */
        }
    }

    function initFilters() {
        const $f = $("#baladyFilters").empty();

        yearSelectBox = createWidget($("<div/>").appendTo($f), "dxSelectBox", {
            label: t("integrations.balady.year"),
            value: filterState.year,
            items: buildYearItems(),
            searchEnabled: false,
            onValueChanged(e) {
                filterState.year = e.value;
                syncMonthSelectBox(e.value, filterState.month);
            }
        }).dxSelectBox("instance");

        monthSelectBox = createWidget($("<div/>").appendTo($f), "dxSelectBox", {
            label: t("integrations.balady.month"),
            value: filterState.month,
            items: buildMonthItems(filterState.year),
            valueExpr: "value",
            displayExpr: "text",
            searchEnabled: false,
            onValueChanged(e) {
                filterState.month = e.value;
            }
        }).dxSelectBox("instance");

        createWidget($("<div/>").appendTo($f), "dxButton", {
            text: t("integrations.balady.search"),
            type: "default",
            onClick() {
                if (window.__baladyGrid) {
                    window.__baladyGrid.refresh();
                }
            }
        });

        createWidget($("<div/>").appendTo($f), "dxButton", {
            text: t("integrations.balady.reset"),
            onClick() {
                const defaults = defaultBaladyFilter();
                filterState.year = defaults.year;
                filterState.month = defaults.month;
                if (yearSelectBox) {
                    yearSelectBox.option("value", filterState.year);
                }
                syncMonthSelectBox(filterState.year, filterState.month);
                resetBaladyGridState();
                if (window.__baladyGrid) {
                    window.__baladyGrid.refresh();
                }
            }
        });
    }

    function initGrid() {
        const po = window.Zaaer.PmsGridOptions;
        window.__baladyGrid = $("#baladyGrid").dxDataGrid(
            po.merge(po.adminBaseline(), {
            dataSource: new DevExpress.data.CustomStore({
                load() {
                    return api
                        .get("/api/v1/pms/integrations/balady/report", buildQuery())
                        .then((res) => {
                            lastReportRows = unwrapList(res);
                            return lastReportRows;
                        });
                }
            }),
            height: baladyGridHostHeight(),
            wordWrapEnabled: true,
            paging: { pageSize: 50 },
            pager: po.adminPager(),
            export: {
                enabled: true,
                allowExportSelectedData: false,
                fileName: buildExportBasename()
            },
            toolbar: {
                items: [
                    "searchPanel",
                    {
                        name: "exportButton",
                        locateInMenu: "never"
                    },
                    {
                        widget: "dxButton",
                        location: "after",
                        locateInMenu: "never",
                        options: {
                            icon: "exportpdf",
                            hint: t("integrations.balady.exportPdf"),
                            stylingMode: "text",
                            onClick() {
                                exportBaladyPdf();
                            }
                        }
                    }
                ]
            },
            onExporting: exportBaladyExcel,
            onContentReady() {
                scheduleBaladyGridResize();
            },
            columns: gridColumns()
            })
        ).dxDataGrid("instance");

        bindBaladyGridResize();
    }

    function initPage() {
        if (!loc || typeof loc.init !== "function") {
            console.error("LocalizationService is not loaded.");
            return;
        }

        loc.init();

        try {
            ensureDevExtremeWidget("dxSelectBox");
            ensureDevExtremeWidget("dxButton");
            ensureDevExtremeWidget("dxDataGrid");
        } catch (err) {
            console.error(err);
            DevExpress.ui.notify(err.message || "DevExtreme failed to load.", "error", 5000);
            return;
        }

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-integrations-balady",
            onRefresh() {
                if (window.__baladyGrid) {
                    window.__baladyGrid.refresh();
                }
            }
        });
        initFilters();
        syncMonthSelectBox(filterState.year, filterState.month);
        initGrid();
        scheduleBaladyGridResize();
        setTimeout(scheduleBaladyGridResize, 80);
        setTimeout(scheduleBaladyGridResize, 320);
    }

    $(initPage);
})(window, jQuery);
