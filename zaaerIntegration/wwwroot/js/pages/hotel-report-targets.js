(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;
    const targetSvc = window.Zaaer.HotelTargetsService;

    function t(key) {
        return common.t(key);
    }

    function fmtMoney(value) {
        return common.fmtMoney(value);
    }

    function fmtPercent(value) {
        const n = Number(value);
        if (Number.isNaN(n)) {
            return "0.00%";
        }
        return `${n.toLocaleString("en-GB", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
    }

    function formatMonthCaption(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const month = String(d.getMonth() + 1).padStart(2, "0");
        return `${month}/${d.getFullYear()}`;
    }

    function currentLocaleTag() {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        return loc && typeof loc.currentCulture === "function" && loc.currentCulture() === "ar"
            ? "ar-SA"
            : "en-GB";
    }

    function buildYearOptions() {
        const nowYear = new Date().getFullYear();
        const years = [];
        for (let year = nowYear - 5; year <= nowYear + 1; year += 1) {
            years.push({ value: year, text: String(year) });
        }
        return years;
    }

    function buildMonthOptions() {
        const locale = currentLocaleTag();
        return Array.from({ length: 12 }, (_, index) => {
            const monthValue = index + 1;
            const label = new Date(2000, index, 1).toLocaleString(locale, { month: "long" });
            return { value: monthValue, text: label };
        });
    }

    function getSelectedMonthRange(filterState) {
        const year = filterState.year;
        const month = filterState.month;
        const fromDate = new Date(year, month - 1, 1);
        const lastDay = new Date(year, month, 0).getDate();
        const toDate = new Date(year, month - 1, lastDay);
        return { fromDate, toDate };
    }

    function buildMonthDayRows(year, month, dailyItems) {
        const locale = currentLocaleTag();
        const dayNameFormatter = new Intl.DateTimeFormat(locale, { weekday: "long" });
        const lastDay = new Date(year, month, 0).getDate();
        const dailyByKey = {};
        (dailyItems || []).forEach((item) => {
            const raw = item.date || item.Date;
            const d = raw instanceof Date ? raw : new Date(raw);
            if (Number.isNaN(d.getTime())) {
                return;
            }
            dailyByKey[common.formatLocalDateParam(d)] = item;
        });

        const rows = [];
        for (let day = 1; day <= lastDay; day += 1) {
            const date = new Date(year, month - 1, day);
            const dateKey = common.formatLocalDateParam(date);
            const item = dailyByKey[dateKey];
            const grossNet = item ? Number(item.grossNet ?? item.GrossNet ?? 0) : 0;
            const netExTax = item ? Number(item.netExTax ?? item.NetExTax ?? 0) : 0;
            rows.push({
                rowKey: day,
                dateText: `${String(day).padStart(2, "0")}/${String(month).padStart(2, "0")}/${year}`,
                dayName: dayNameFormatter.format(date),
                grossNet,
                netExTax
            });
        }
        return rows;
    }

    function monthDayGridColumns() {
        return [
            {
                dataField: "dateText",
                caption: t("hotelReports.targets.days.date"),
                width: 120
            },
            {
                dataField: "dayName",
                caption: t("hotelReports.targets.days.dayName"),
                minWidth: 120
            },
            {
                dataField: "grossNet",
                caption: t("hotelReports.targets.days.grossNet"),
                width: 120,
                alignment: "right",
                calculateCellValue(row) {
                    return fmtMoney(row.grossNet);
                }
            },
            {
                dataField: "netExTax",
                caption: t("hotelReports.targets.days.achieved"),
                width: 120,
                alignment: "right",
                calculateCellValue(row) {
                    return fmtMoney(row.netExTax);
                }
            }
        ];
    }

    function renderMonthDaysGrid($host, filterState, gridInstRef, report) {
        const dailyItems = report && (report.dailyItems || report.DailyItems);
        const rows = buildMonthDayRows(filterState.year, filterState.month, dailyItems);
        if (gridInstRef.instance) {
            gridInstRef.instance.option("dataSource", rows);
            return;
        }

        $host.dxDataGrid({
            dataSource: rows,
            keyExpr: "rowKey",
            height: Math.min(340, Math.max(180, rows.length * 28 + 56)),
            showBorders: true,
            rowAlternationEnabled: true,
            noDataText: t("hotelReports.empty"),
            elementAttr: { class: "pms-grid-compact" },
            headerFilter: { visible: true, search: { enabled: true } },
            searchPanel: { visible: true, width: 260 },
            paging: { enabled: false },
            scrolling: { mode: "standard" },
            columns: monthDayGridColumns()
        });
        gridInstRef.instance = $host.dxDataGrid("instance");
    }

    function refreshMonthDayLabels(filterState, monthInst) {
        if (!monthInst) {
            return;
        }
        monthInst.option("items", buildMonthOptions());
    }

    function canManageTargets() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || typeof api.hasPermission !== "function") {
            return false;
        }
        return api.hasPermission("hotel.targets.manage")
            || api.hasPermission("resort.targets.manage");
    }

    function commissionValidationRules() {
        const message = t("hotelReports.targets.validation.commissionRequired");
        return [
            { type: "required", message },
            {
                type: "custom",
                message,
                validationCallback(e) {
                    const value = Number(e.value);
                    return Number.isFinite(value) && value > 0;
                }
            }
        ];
    }

    function resolveArabicBranchName() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || typeof api.getHotelName !== "function") {
            return "";
        }
        return `${api.getHotelName() || ""}`.trim();
    }

    function tierLabel(tierKey) {
        const key = `hotelReports.targets.tier.${tierKey}`;
        const label = t(key);
        return label !== key ? label : tierKey;
    }

    function renderSummary($host, report) {
        $host.empty();
        if (!report || !report.hasTarget) {
            return;
        }

        const cards = [
            { label: t("hotelReports.targets.kpi.targetAmount"), value: fmtMoney(report.target && report.target.targetAmount) },
            { label: t("hotelReports.targets.kpi.receiptsNet"), value: fmtMoney(report.achievedGrossNet ?? report.AchievedGrossNet) },
            { label: t("hotelReports.targets.kpi.achievedAmount"), value: fmtMoney(report.achievedAmount), tone: "positive" },
            { label: t("hotelReports.targets.kpi.achievementPercent"), value: fmtPercent(report.achievementPercent) },
            { label: t("hotelReports.targets.kpi.remainingAmount"), value: fmtMoney(report.remainingAmount) },
            { label: t("hotelReports.targets.kpi.commissionRate"), value: fmtPercent(report.activeCommissionRate) },
            { label: t("hotelReports.targets.kpi.estimatedCommission"), value: fmtMoney(report.estimatedCommissionAmount) }
        ];

        cards.forEach((card) => {
            const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
            $("<div class='hall-reports-kpi__label'/>").text(card.label).appendTo($card);
            const $value = $("<div class='hall-reports-kpi__value'/>").text(card.value).appendTo($card);
            if (card.tone === "positive") {
                $value.addClass("hall-reports-kpi__value--positive");
            }
        });
    }

    function renderTiers($host, report) {
        $host.empty();
        if (!report || !report.hasTarget) {
            return;
        }

        const tiers = report.tiers || report.Tiers || [];
        const $grid = $("<div class='target-report-tiers__grid'/>").appendTo($host);
        tiers.forEach((tier) => {
            const reached = !!(tier.isReached || tier.IsReached);
            const active = !!(tier.isActive || tier.IsActive);
            const $card = $("<div class='target-report-tier'/>")
                .addClass(reached ? "target-report-tier--reached" : "")
                .addClass(active ? "target-report-tier--active" : "")
                .appendTo($grid);

            const $head = $("<div class='target-report-tier__head'/>").appendTo($card);
            $("<div class='target-report-tier__title'/>")
                .text(tierLabel(tier.tierKey || tier.TierKey || tier.label || tier.Label))
                .appendTo($head);
            const $icon = $("<span class='target-report-tier__status'/>").appendTo($head);
            if (reached) {
                $icon.addClass("dx-icon dx-icon-check target-report-tier__status--ok");
            }

            $("<div class='target-report-tier__meta'/>")
                .text(`${t("hotelReports.targets.tierRate")}: ${fmtPercent(tier.commissionRate ?? tier.CommissionRate)}`)
                .appendTo($card);
            $("<div class='target-report-tier__meta'/>")
                .text(`${t("hotelReports.targets.tierAchieved")}: ${fmtMoney(tier.tierAchievedAmount ?? tier.TierAchievedAmount)}`)
                .appendTo($card);
            $("<div class='target-report-tier__amount'/>")
                .text(`${t("hotelReports.targets.tierCommission")}: ${fmtMoney(tier.commissionAmount ?? tier.CommissionAmount)}`)
                .appendTo($card);
        });
    }

    function renderEmpty($host, report) {
        if (report && report.hasTarget) {
            $host.prop("hidden", true).empty();
            return;
        }
        $host.prop("hidden", false).empty();
        $("<div class='target-report-empty__icon dx-icon dx-icon-info'/>").appendTo($host);
        $("<div class='target-report-empty__text'/>")
            .text(t("hotelReports.targets.noTargetForMonth"))
            .appendTo($host);
        if (canManageTargets()) {
            $("<div class='target-report-empty__hint'/>")
                .text(t("hotelReports.targets.noTargetHint"))
                .appendTo($host);
        }
    }

    function openSettingsPopup(reloadReport) {
        const $host = $("<div/>").appendTo("body");
        let gridInst = null;
        let formInst = null;
        let popupInst = null;
        let editingId = null;
        let settingsRows = [];

        function loadSettingsGrid() {
            return targetSvc.listSettings().then((rows) => {
                settingsRows = Array.isArray(rows) ? rows : (rows && rows.items) || [];
                if (gridInst) {
                    gridInst.option("dataSource", settingsRows.slice());
                }
            });
        }

        function resetForm() {
            editingId = null;
            if (!formInst) {
                return;
            }
            const now = new Date();
            formInst.option("formData", {
                monthYear: new Date(now.getFullYear(), now.getMonth(), 1),
                targetAmount: null,
                commissionBefore85: null,
                commissionAt85: null,
                commission86To100: null,
                branchName: resolveArabicBranchName()
            });
        }

        function closeSettingsPopup() {
            if (popupInst) {
                popupInst.hide();
            }
        }

        function saveForm() {
            const result = formInst.validate();
            if (!result.isValid) {
                return;
            }
            const data = formInst.option("formData") || {};
            const payload = {
                monthYear: common.formatLocalDateParam(data.monthYear),
                targetAmount: Number(data.targetAmount) || 0,
                commissionBefore85: Number(data.commissionBefore85) || 0,
                commissionAt85: Number(data.commissionAt85) || 0,
                commission86To100: Number(data.commission86To100) || 0,
                branchName: resolveArabicBranchName() || null
            };
            const req = editingId
                ? targetSvc.updateSetting(editingId, payload)
                : targetSvc.createSetting(payload);
            req.then(() => {
                DevExpress.ui.notify(t("common.saved"), "success", 2200);
                resetForm();
                return loadSettingsGrid().then(() => {
                    if (typeof reloadReport === "function") {
                        reloadReport();
                    }
                });
            }).catch((err) => {
                const msg = (err && (err.message || err.Message)) || t("common.error");
                DevExpress.ui.notify(msg, "error", 3200);
            });
        }

        $host.dxPopup({
            width: Math.min(920, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "82vh",
            showTitle: true,
            title: t("hotelReports.targets.settingsTitle"),
            visible: true,
            showCloseButton: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup guest-picker-popup target-settings-popup" },
            onHidden() {
                $host.remove();
            },
            contentTemplate(contentElement) {
                const $content = $(contentElement);
                const $formHost = $("<div class='target-settings-form-host'/>").appendTo($content);
                const $gridHost = $("<div class='target-settings-grid-host'/>").appendTo($content);

                $formHost.dxForm({
                    formData: {},
                    colCount: 2,
                    labelLocation: "top",
                    items: [
                        {
                            dataField: "monthYear",
                            label: { text: t("hotelReports.targets.field.month") },
                            editorType: "dxDateBox",
                            editorOptions: {
                                type: "date",
                                displayFormat: "MM/yyyy",
                                calendarOptions: { maxZoomLevel: "year", minZoomLevel: "century" },
                                openOnFieldClick: true
                            },
                            validationRules: [{ type: "required" }]
                        },
                        {
                            dataField: "branchName",
                            label: { text: t("hotelReports.targets.field.branch") },
                            editorOptions: { readOnly: true }
                        },
                        {
                            dataField: "targetAmount",
                            label: { text: t("hotelReports.targets.field.targetAmount") },
                            editorType: "dxNumberBox",
                            editorOptions: { min: 0, format: "#,##0.00", showSpinButtons: true },
                            validationRules: [
                                { type: "required" },
                                {
                                    type: "custom",
                                    message: t("hotelReports.targets.validation.targetAmountRequired"),
                                    validationCallback(e) {
                                        const value = Number(e.value);
                                        return Number.isFinite(value) && value > 0;
                                    }
                                }
                            ]
                        },
                        {
                            dataField: "commissionBefore85",
                            label: { text: t("hotelReports.targets.field.commissionBefore85") },
                            editorType: "dxNumberBox",
                            editorOptions: { min: 0, format: "#,##0.00", showSpinButtons: true },
                            validationRules: commissionValidationRules()
                        },
                        {
                            dataField: "commissionAt85",
                            label: { text: t("hotelReports.targets.field.commissionAt85") },
                            editorType: "dxNumberBox",
                            editorOptions: { min: 0, format: "#,##0.00", showSpinButtons: true },
                            validationRules: commissionValidationRules()
                        },
                        {
                            dataField: "commission86To100",
                            label: { text: t("hotelReports.targets.field.commission86To100") },
                            editorType: "dxNumberBox",
                            editorOptions: { min: 0, format: "#,##0.00", showSpinButtons: true },
                            validationRules: commissionValidationRules()
                        }
                    ]
                });
                formInst = $formHost.dxForm("instance");
                resetForm();

                const $actions = $("<div class='target-settings-form-actions'/>").appendTo($formHost.parent());
                $("<div/>").appendTo($actions).dxButton({
                    text: t("common.save"),
                    type: "default",
                    icon: "save",
                    onClick: saveForm
                });
                $("<div/>").appendTo($actions).dxButton({
                    text: t("common.cancel"),
                    stylingMode: "outlined",
                    onClick() {
                        resetForm();
                        closeSettingsPopup();
                    }
                });

                $gridHost.dxDataGrid({
                    dataSource: [],
                    width: "100%",
                    height: 260,
                    keyExpr: "hotelMonthlyTargetId",
                    noDataText: t("hotelReports.empty"),
                    elementAttr: { class: "pms-grid-compact target-settings-grid" },
                    headerFilter: { visible: true, search: { enabled: true } },
                    searchPanel: { visible: true, width: 220 },
                    paging: { pageSize: 20 },
                    wordWrapEnabled: true,
                    columnAutoWidth: false,
                    scrolling: { useNative: false, scrollByContent: false, scrollByThumb: true, showScrollbar: "onHover" },
                    columns: [
                        {
                            dataField: "monthYear",
                            caption: t("hotelReports.targets.field.month"),
                            width: "20%",
                            minWidth: 88,
                            calculateCellValue(row) {
                                return formatMonthCaption(row.monthYear || row.MonthYear);
                            }
                        },
                        {
                            dataField: "branchName",
                            caption: t("hotelReports.targets.field.branch"),
                            width: "42%",
                            minWidth: 120
                        },
                        {
                            dataField: "targetAmount",
                            caption: t("hotelReports.targets.field.targetAmount"),
                            width: "28%",
                            minWidth: 110,
                            alignment: "right",
                            calculateCellValue(row) {
                                return fmtMoney(row.targetAmount ?? row.TargetAmount);
                            }
                        },
                        {
                            type: "buttons",
                            width: 52,
                            minWidth: 52,
                            maxWidth: 52,
                            allowSorting: false,
                            allowFiltering: false,
                            allowHeaderFiltering: false,
                            buttons: [{
                                hint: t("common.edit"),
                                icon: "edit",
                                onClick(e) {
                                    const row = e.row && e.row.data;
                                    if (!row) {
                                        return;
                                    }
                                    editingId = row.hotelMonthlyTargetId || row.HotelMonthlyTargetId;
                                    formInst.option("formData", {
                                        monthYear: new Date(row.monthYear || row.MonthYear),
                                        targetAmount: row.targetAmount ?? row.TargetAmount,
                                        commissionBefore85: row.commissionBefore85 ?? row.CommissionBefore85,
                                        commissionAt85: row.commissionAt85 ?? row.CommissionAt85,
                                        commission86To100: row.commission86To100 ?? row.Commission86To100,
                                        branchName: row.branchName || row.BranchName || resolveArabicBranchName()
                                    });
                                }
                            }]
                        }
                    ]
                });
                gridInst = $gridHost.dxDataGrid("instance");
                loadSettingsGrid();
            }
        });
        popupInst = $host.dxPopup("instance");
    }

    $(function () {
        const loc = window.Zaaer.LocalizationService;
        const apiInst = window.Zaaer.ApiService;
        const now = new Date();
        const filterState = {
            year: now.getFullYear(),
            month: now.getMonth() + 1
        };
        let yearInst;
        let monthInst;
        const daysGridRef = { instance: null };

        const $filters = $("#hallReportsFilters");
        const $summary = $("#targetReportSummary");
        const $tiers = $("#targetReportTiers");
        const $daysGrid = $("#targetReportDaysGrid");
        const $empty = $("#targetReportEmpty");

        function syncMonthRange() {
            const range = getSelectedMonthRange(filterState);
            filterState.fromDate = range.fromDate;
            filterState.toDate = range.toDate;
        }

        function loadReport() {
            syncMonthRange();
            const from = common.formatLocalDateParam(filterState.fromDate);
            const to = common.formatLocalDateParam(filterState.toDate);
            if (!from || !to) {
                DevExpress.ui.notify(t("common.error"), "warning", 2500);
                return $.Deferred().reject().promise();
            }
            return targetSvc.getTargetReport(from, to).then((report) => {
                renderSummary($summary, report);
                renderTiers($tiers, report);
                renderEmpty($empty, report);
                renderMonthDaysGrid($daysGrid, filterState, daysGridRef, report);
            }).catch(() => {
                DevExpress.ui.notify(t("common.error"), "error", 3200);
            });
        }

        function resetFilters() {
            const today = new Date();
            filterState.year = today.getFullYear();
            filterState.month = today.getMonth() + 1;
            if (yearInst) {
                yearInst.option("value", filterState.year);
            }
            if (monthInst) {
                refreshMonthDayLabels(filterState, monthInst);
                monthInst.option("value", filterState.month);
            }
            loadReport();
        }

        if (!loc || !apiInst) {
            return;
        }
        loc.init();
        if (!apiInst.requireToken()) {
            return;
        }

        const rbac = window.Zaaer && window.Zaaer.PmsRbacNav;
        const permissionKeys = rbac && typeof rbac.resolveLodgingReportPermissionKeys === "function"
            ? rbac.resolveLodgingReportPermissionKeys("targets")
            : ["hotel.reports.targets", "hotel.reports"];
        if (!permissionKeys.some((key) => apiInst.hasPermission(key))) {
            DevExpress.ui.notify(t("hotelReports.forbidden"), "warning", 4000);
            return;
        }

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-hotel-report-targets",
            onRefresh: loadReport
        });

        $filters.empty();
        $("<div class='hall-reports-filter-field'/>").appendTo($filters).dxSelectBox({
            label: t("hotelReports.targets.filter.year"),
            dataSource: buildYearOptions(),
            displayExpr: "text",
            valueExpr: "value",
            value: filterState.year,
            searchEnabled: true,
            onValueChanged(e) {
                filterState.year = Number(e.value) || filterState.year;
            }
        });
        yearInst = $filters.find(".hall-reports-filter-field").first().dxSelectBox("instance");

        $("<div class='hall-reports-filter-field'/>").appendTo($filters).dxSelectBox({
            label: t("hotelReports.targets.filter.month"),
            dataSource: buildMonthOptions(),
            displayExpr: "text",
            valueExpr: "value",
            value: filterState.month,
            searchEnabled: true,
            onValueChanged(e) {
                filterState.month = Number(e.value) || filterState.month;
            }
        });
        monthInst = $filters.find(".hall-reports-filter-field").eq(1).dxSelectBox("instance");

        const $dateActions = $("<div class='hall-reports-filter-date-actions'/>").appendTo($filters);
        $("<div/>").appendTo($dateActions).dxButton({
            text: t("hallReports.filter.apply"),
            type: "default",
            stylingMode: "contained",
            icon: "find",
            onClick: loadReport
        });
        $("<div/>").appendTo($dateActions).dxButton({
            text: t("hallReports.filter.reset"),
            stylingMode: "outlined",
            icon: "refresh",
            onClick: resetFilters
        });

        if (canManageTargets()) {
            const $settingsActions = $("<div class='hall-reports-filter-export-actions'/>").appendTo($filters);
            $("<div/>").appendTo($settingsActions).dxButton({
                text: t("hotelReports.targets.settingsButton"),
                icon: "preferences",
                stylingMode: "contained",
                onClick() {
                    openSettingsPopup(loadReport);
                }
            });
        }

        loadReport();
    });
})(window, jQuery);
