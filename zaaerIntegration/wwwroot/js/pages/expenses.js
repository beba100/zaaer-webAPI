(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const expenseApi = window.Zaaer.PmsExpenseService;
    const api = window.Zaaer.ApiService;
    const taxApi = window.Zaaer.PmsPricingTax;
    const gridOpts = window.Zaaer.PmsGridOptions;

    let gridInstance;
    let loadPanel;
    let taxConfig = null;
    let pendingUploadFiles = [];
    let filterFromDate;
    let filterToDate;

    function t(key) {
        return loc.t(key);
    }

    function fmtMoney(n) {
        return DevExpress.localization.formatNumber(Number(n) || 0, "#,##0.00");
    }

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function startOfMonthDate() {
        const n = new Date();
        return new Date(n.getFullYear(), n.getMonth(), 1);
    }

    function todayDate() {
        return new Date();
    }

    function resolveExpenseNo(row) {
        if (!row) {
            return "";
        }
        const no = row.expenseNo || row.number || row.ExpenseNo || row.Number;
        if (no) {
            return String(no).trim();
        }
        const seq = row.expenseSeq || row.ExpenseSeq || 0;
        if (seq > 0) {
            return `EXP_${String(seq).padStart(4, "0")}`;
        }
        return "";
    }

    function serializeExpenseDateTime(dateOnly, originalDateTime) {
        const d = dateOnly instanceof Date ? new Date(dateOnly.getTime()) : new Date(dateOnly);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        if (originalDateTime) {
            const orig = new Date(originalDateTime);
            if (!Number.isNaN(orig.getTime())) {
                d.setHours(orig.getHours(), orig.getMinutes(), orig.getSeconds(), orig.getMilliseconds());
            }
        } else {
            const now = new Date();
            d.setHours(now.getHours(), now.getMinutes(), now.getSeconds(), now.getMilliseconds());
        }
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        const h = String(d.getHours()).padStart(2, "0");
        const min = String(d.getMinutes()).padStart(2, "0");
        const s = String(d.getSeconds()).padStart(2, "0");
        return `${y}-${m}-${day}T${h}:${min}:${s}`;
    }

    function statusLabel(status) {
        const s = (status || "").trim().toLowerCase();
        if (s === "accepted") {
            return t("expenses.status.accepted");
        }
        if (s === "rejected") {
            return t("expenses.status.rejected");
        }
        if (s === "auto-approved") {
            return t("expenses.status.autoApproved");
        }
        return t("expenses.status.pending");
    }

    function statusBadgeClass(status) {
        const s = (status || "").trim().toLowerCase().replace(/[\s-]+/g, "-");
        return `expense-status-badge expense-status-badge--${s || "pending"}`;
    }

    function canCreate() {
        return api.hasPermission("finance.expense.create");
    }

    function canView() {
        return api.hasPermission("finance.expense.view");
    }

    function canUpdate() {
        return api.hasPermission("finance.expense.update");
    }

    const FINANCE_EXPENSE_DOCUMENT_DATE_PERMISSION = "finance.expense.document_date";

    /** Forms only — grid always shows expense date. */
    function canShowExpenseDate() {
        return api.hasPermission(FINANCE_EXPENSE_DOCUMENT_DATE_PERMISSION);
    }

    function normalizeTaxConfig(raw) {
        return taxApi.normalizeConfig({
            vatRate: raw && (raw.vatRate ?? raw.VatRate),
            vatTaxIncluded: raw && (raw.vatTaxIncluded ?? raw.VatTaxIncluded),
            ewaRate: 0,
            ewaIncluded: true
        });
    }

    function computeTaxBreakdown(totalAmount, hasTax) {
        const total = Math.round((Number(totalAmount) || 0) * 100) / 100;
        if (!hasTax || total <= 0) {
            return {
                beforeTaxAmount: total,
                taxRate: null,
                taxAmount: 0,
                totalAmount: total
            };
        }

        const cfg = normalizeTaxConfig(taxConfig || {});
        const calc = taxApi.calculateAmounts(total, cfg);
        return {
            beforeTaxAmount: calc.net,
            taxRate: cfg.vatRate,
            taxAmount: calc.vat,
            totalAmount: calc.total
        };
    }

    function normalizeCategoryId(value) {
        if (value == null || value === "") {
            return null;
        }
        const n = Number(value);
        return Number.isFinite(n) && n > 0 ? n : null;
    }

    function ensureCategoryOption(categories, categoryId, categoryName) {
        const list = (categories || []).slice();
        const id = normalizeCategoryId(categoryId);
        if (!id) {
            return list;
        }
        if (!list.some((c) => normalizeCategoryId(c.expenseCategoryId) === id)) {
            list.push({
                expenseCategoryId: id,
                categoryName: categoryName || String(id)
            });
        }
        return list;
    }

    function mapDetailToFormData(detail, row) {
        const src = detail || row || {};
        return {
            dateTime: src.dateTime ? new Date(src.dateTime) : todayDate(),
            expenseCategoryId: normalizeCategoryId(src.expenseCategoryId ?? src.ExpenseCategoryId),
            comment: src.comment || "",
            totalAmount: src.totalAmount != null ? src.totalAmount : null,
            hasTax: !!(src.taxAmount && Number(src.taxAmount) > 0),
            taxNumber: (src.company && src.company.taxNumber) || "",
            companyName: (src.company && src.company.companyName) || "",
            companyId: (src.company && src.company.companyId) || null
        };
    }

    function withLoad(promise) {
        if (loadPanel) {
            loadPanel.show();
            return $.when(promise).always(() => loadPanel.hide());
        }
        return $.when(promise);
    }

    function loadReferenceData() {
        return expenseApi.getTaxConfig().then((cfg) => {
            taxConfig = cfg || {};
        });
    }

    function normalizeListResult(raw) {
        if (!raw) {
            return { items: [], summary: {} };
        }
        if (Array.isArray(raw)) {
            return { items: raw, summary: {} };
        }
        return {
            items: raw.items || raw.Items || [],
            summary: raw.summary || raw.Summary || {}
        };
    }

    function updateSummaryCards(summary) {
        const s = summary || {};
        $("#expensesSummaryTotalAmount").text(fmtMoney(s.totalAmount ?? s.TotalAmount));
        $("#expensesSummaryBeforeTax").text(fmtMoney(s.beforeTaxAmount ?? s.BeforeTaxAmount));
        $("#expensesSummaryTax").text(fmtMoney(s.taxAmount ?? s.TaxAmount));
        $("#expensesSummaryCount").text(String(s.count ?? s.Count ?? 0));
    }

    function loadGrid() {
        const from = formatLocalDateParam(filterFromDate);
        const to = formatLocalDateParam(filterToDate);
        return expenseApi.list(from, to).then((raw) => {
            const result = normalizeListResult(raw);
            gridInstance.option("dataSource", result.items);
            updateSummaryCards(result.summary);
        });
    }

    function disposePopup($host) {
        if (!$host || !$host.length) {
            return;
        }
        try {
            const inst = $host.dxPopup("instance");
            if (inst) {
                inst.dispose();
            }
        } catch {
            /* not initialized */
        }
        $host.empty();
    }

    function popupBaseOptions(title, extra) {
        return Object.assign(
            {
                title,
                visible: false,
                showCloseButton: true,
                showTitle: true,
                dragEnabled: false,
                hideOnOutsideClick: true,
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                container: document.body,
                width: Math.min(780, Math.max(360, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "72vh",
                wrapperAttr: { class: "expense-form-popup res-extra-popup res-extra-select-popup" }
            },
            extra || {}
        );
    }

    function buildPayload(formData, originalDateTime, isEdit) {
        const hasTax = !!formData.hasTax;
        const tax = computeTaxBreakdown(formData.totalAmount, hasTax);
        const payload = {
            dueDate: formData.dueDate ? formatLocalDateParam(formData.dueDate) : null,
            comment: (formData.comment || "").trim() || null,
            expenseCategoryId: formData.expenseCategoryId,
            hasTax,
            taxRate: tax.taxRate,
            taxAmount: tax.taxAmount,
            beforeTaxAmount: tax.beforeTaxAmount,
            totalAmount: tax.totalAmount
        };

        if (canShowExpenseDate()) {
            payload.dateTime = serializeExpenseDateTime(formData.dateTime, originalDateTime);
        } else if (!isEdit) {
            payload.dateTime = serializeExpenseDateTime(todayDate(), null);
        }

        if (hasTax) {
            payload.company = {
                taxNumber: (formData.taxNumber || "").trim(),
                companyName: (formData.companyName || "").trim()
            };
        }

        return payload;
    }

    function validateForm(formData, isEdit, existingImageCount) {
        if (canShowExpenseDate() && !formData.dateTime) {
            DevExpress.ui.notify(t("expenses.validationDate"), "warning", 3000);
            return false;
        }
        if (!formData.expenseCategoryId) {
            DevExpress.ui.notify(t("expenses.validationCategory"), "warning", 3000);
            return false;
        }
        if (!formData.totalAmount || Number(formData.totalAmount) <= 0) {
            DevExpress.ui.notify(t("expenses.validationTotal"), "warning", 3000);
            return false;
        }
        if (!(formData.comment || "").trim()) {
            DevExpress.ui.notify(t("expenses.validationComment"), "warning", 3000);
            return false;
        }
        if (formData.hasTax) {
            if (!(formData.taxNumber || "").trim() || !(formData.companyName || "").trim()) {
                DevExpress.ui.notify(t("expenses.validationCompany"), "warning", 3000);
                return false;
            }
        }
        const hasImages = (existingImageCount || 0) > 0 || (pendingUploadFiles || []).length > 0;
        if (!isEdit && !hasImages) {
            DevExpress.ui.notify(t("expenses.validationImage"), "warning", 3000);
            return false;
        }
        return true;
    }

    function formatDateTime(value) {
        if (!value) {
            return "";
        }
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        return DevExpress.localization.formatDate(d, "dd/MM/yyyy HH:mm");
    }

    function approvalActionLabel(action) {
        const key = `expenses.approvalAction.${(action || "").trim().toLowerCase()}`;
        const label = t(key);
        return label === key ? (action || "") : label;
    }

    function openImagePreview(imageUrl, title) {
        if (!imageUrl) {
            return;
        }

        const $popup = $("#expenseImagePreviewPopup");
        disposePopup($popup);

        $popup
            .dxPopup(
                popupBaseOptions(title || t("expenses.imagePreviewTitle"), {
                    width: Math.min(920, Math.max(360, window.innerWidth - 24)),
                    maxHeight: "82vh",
                    wrapperAttr: { class: "expense-form-popup res-extra-popup res-extra-select-popup" },
                    contentTemplate() {
                        return $("<div>")
                            .addClass("expense-image-preview-shell")
                            .append(
                                $("<img>")
                                    .addClass("expense-image-preview-img")
                                    .attr("src", imageUrl)
                                    .attr("alt", "")
                            );
                    }
                })
            )
            .dxPopup("instance")
            .show();
    }

    function openApprovalHistoryPopup(expenseId, expenseNo) {
        const $popup = $("#expenseApprovalHistoryPopup");
        disposePopup($popup);

        let popupInstance;
        let listInstance;

        popupInstance = $popup
            .dxPopup(
                popupBaseOptions(
                    expenseNo
                        ? `${t("expenses.approvalHistoryTitle")} · ${expenseNo}`
                        : t("expenses.approvalHistoryTitle"),
                    {
                    width: Math.min(640, Math.max(360, window.innerWidth - 24)),
                    maxHeight: "72vh",
                    showCloseButton: true,
                    wrapperAttr: { class: "expense-form-popup res-extra-popup res-extra-select-popup" },
                    contentTemplate() {
                        return $("<div>").addClass("expense-approval-history-shell").append($("<div id='expenseApprovalHistoryList'>"));
                    },
                    toolbarItems: [
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: t("common.close"),
                                type: "default",
                                stylingMode: "contained",
                                onClick() {
                                    popupInstance.hide();
                                }
                            }
                        }
                    ],
                    onShown() {
                        listInstance = $("#expenseApprovalHistoryList")
                            .dxList({
                                dataSource: [],
                                height: Math.min(480, Math.max(260, window.innerHeight * 0.5)),
                                focusStateEnabled: false,
                                hoverStateEnabled: false,
                                activeStateEnabled: false,
                                noDataText: t("expenses.approvalHistoryEmpty"),
                                itemTemplate(item) {
                                    const $item = $("<div>").addClass("expense-approval-step");
                                    const $head = $("<div>").addClass("expense-approval-step-head").appendTo($item);
                                    $("<span>")
                                        .addClass("expense-approval-step-action")
                                        .text(approvalActionLabel(item.action || item.Action))
                                        .appendTo($head);
                                    $("<span>")
                                        .addClass("expense-approval-step-date")
                                        .text(formatDateTime(item.actionAt || item.ActionAt))
                                        .appendTo($head);

                                    const byName = item.actionByFullName || item.ActionByFullName;
                                    if (byName) {
                                        $("<div>").addClass("expense-approval-step-by").text(byName).appendTo($item);
                                    }

                                    const comments = item.comments || item.Comments;
                                    if (comments) {
                                        $("<div>").addClass("expense-approval-step-comments").text(comments).appendTo($item);
                                    }

                                    const status = item.status || item.Status;
                                    if (status) {
                                        $("<span>")
                                            .addClass(statusBadgeClass(status))
                                            .text(statusLabel(status))
                                            .appendTo($item);
                                    }

                                    return $item;
                                }
                            })
                            .dxList("instance");

                        withLoad(
                            expenseApi
                                .getApprovalHistory(expenseId)
                                .then((rows) => {
                                    listInstance.option("dataSource", rows || []);
                                })
                                .catch(() => {
                                    DevExpress.ui.notify(t("expenses.approvalHistoryLoadFailed"), "error", 3500);
                                    listInstance.option("dataSource", []);
                                })
                        );
                    }
                })
            )
            .dxPopup("instance");

        popupInstance.show();
    }

    function renderExistingImages($container, images, expenseId, onChange) {
        $container.empty();
        const $grid = $("<div>").addClass("expense-images-grid").appendTo($container);

        (images || []).forEach((img) => {
            const $thumb = $("<div>").addClass("expense-image-thumb").appendTo($grid);
            const path = img.imagePath || img.ImagePath;
            $("<img>").attr("src", path).attr("alt", "").appendTo($thumb);
            $("<button type='button'>")
                .addClass("expense-image-zoom")
                .attr("title", t("expenses.imageZoom"))
                .html("<span class='dx-icon dx-icon-fullscreen'></span>")
                .on("click", (evt) => {
                    evt.preventDefault();
                    evt.stopPropagation();
                    openImagePreview(path, t("expenses.imagePreviewTitle"));
                })
                .appendTo($thumb);
            if (canUpdate()) {
                $("<button type='button'>")
                    .addClass("expense-image-remove")
                    .attr("title", t("expenses.imageDelete"))
                    .text("×")
                    .on("click", () => {
                        withLoad(
                            expenseApi.deleteImage(expenseId, img.expenseImageId || img.ExpenseImageId).then(() => {
                                onChange();
                            })
                        );
                    })
                    .appendTo($thumb);
            }
        });
    }

    function normalizeCompanyRow(row) {
        if (!row) {
            return null;
        }
        return {
            id: row.id ?? row.Id,
            taxNumber: row.taxNumber ?? row.TaxNumber ?? "",
            companyName: row.companyName ?? row.CompanyName ?? ""
        };
    }

    function normalizeTaxNumber(value) {
        return (value || "").trim();
    }

    function wireTaxNumberLookup(editorInstance, setCompanyFieldsFn, options) {
        const opts = options || {};
        let lookupSeq = 0;
        let lookupIndicator;

        function setLookupBusy(busy) {
            if (!lookupIndicator) {
                return;
            }
            try {
                lookupIndicator.option("visible", busy);
            } catch {
                /* not ready */
            }
        }

        function runLookup() {
            const taxNumber = normalizeTaxNumber(editorInstance.option("value"));
            if (taxNumber.length < 10) {
                return;
            }

            const seq = ++lookupSeq;
            setLookupBusy(true);
            editorInstance.option("readOnly", true);

            expenseApi
                .lookupCompanyByTax(taxNumber)
                .then((result) => {
                    if (seq !== lookupSeq) {
                        return;
                    }
                    if (result.found && result.data) {
                        const row = normalizeCompanyRow(result.data);
                        setCompanyFieldsFn({
                            taxNumber: row.taxNumber,
                            companyName: row.companyName
                        });
                        DevExpress.ui.notify(t("expenses.companyTaxFound"), "info", 2800);
                        if (typeof opts.onFound === "function") {
                            opts.onFound(row);
                        }
                    }
                })
                .always(() => {
                    if (seq !== lookupSeq) {
                        return;
                    }
                    editorInstance.option("readOnly", false);
                    setLookupBusy(false);
                });
        }

        editorInstance.option("onFocusOut", runLookup);

        return {
            attachIndicator($host) {
                $host.addClass("expense-company-field--lookup");
                if (!$host.hasClass("expense-company-field--tax")) {
                    $host.addClass("expense-company-field--tax");
                }
                if ($host.find(".expense-tax-lookup-indicator").length) {
                    lookupIndicator = $host.find(".expense-tax-lookup-indicator").dxLoadIndicator("instance");
                    return;
                }
                lookupIndicator = $("<div>")
                    .addClass("expense-tax-lookup-indicator")
                    .appendTo($host)
                    .dxLoadIndicator({ visible: false, height: 18, width: 18 })
                    .dxLoadIndicator("instance");
            }
        };
    }

    function openTenantCompanyForm(company, onSaved) {
        if (!canCreate()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3000);
            return;
        }

        const normalized = normalizeCompanyRow(company);
        const $popup = $("#expenseCompanyFormPopup");
        disposePopup($popup);

        let formInstance;
        let popupInstance;
        let taxLookupApi;
        const initial = {
            taxNumber: normalized ? normalized.taxNumber : "",
            companyName: normalized ? normalized.companyName : ""
        };

        function applyCompanyFormFields(data) {
            const fd = formInstance ? formInstance.option("formData") || {} : initial;
            fd.taxNumber = data.taxNumber || "";
            fd.companyName = data.companyName || "";
            if (formInstance) {
                formInstance.option("formData", fd);
                const taxEditor = formInstance.getEditor("taxNumber");
                const nameEditor = formInstance.getEditor("companyName");
                if (taxEditor) {
                    taxEditor.option("value", fd.taxNumber);
                }
                if (nameEditor) {
                    nameEditor.option("value", fd.companyName);
                }
            }
        }

        popupInstance = $popup
            .dxPopup(
                popupBaseOptions(t("expenses.companyAddTitle"), {
                    width: Math.min(520, Math.max(320, window.innerWidth - 24)),
                    maxHeight: "52vh",
                    wrapperAttr: { class: "expense-form-popup res-extra-popup res-extra-select-popup" },
                    contentTemplate() {
                        return $("<div>").addClass("expense-company-form-shell").append($("<div id='expenseCompanyFormHost'>"));
                    },
                    toolbarItems: [
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: t("expenses.cancel"),
                                onClick() {
                                    popupInstance.hide();
                                }
                            }
                        },
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: t("expenses.save"),
                                type: "default",
                                onClick() {
                                    const fd = formInstance.option("formData") || {};
                                    const taxNumber = normalizeTaxNumber(fd.taxNumber);
                                    const companyName = (fd.companyName || "").trim();
                                    if (!taxNumber || !companyName) {
                                        DevExpress.ui.notify(t("expenses.validationCompany"), "warning", 3000);
                                        return;
                                    }

                                    expenseApi
                                        .lookupCompanyByTax(taxNumber)
                                        .then((result) => {
                                            let finalName = companyName;
                                            if (result.found && result.data) {
                                                const existing = normalizeCompanyRow(result.data);
                                                finalName = existing.companyName || companyName;
                                                applyCompanyFormFields(existing);
                                                DevExpress.ui.notify(t("expenses.companyTaxFound"), "info", 2800);
                                            }
                                            popupInstance.hide();
                                            if (typeof onSaved === "function") {
                                                onSaved({ taxNumber, companyName: finalName });
                                            }
                                        })
                                        .fail(() => {
                                            popupInstance.hide();
                                            if (typeof onSaved === "function") {
                                                onSaved({ taxNumber, companyName });
                                            }
                                        });
                                }
                            }
                        }
                    ],
                    onShown() {
                        const $formShell = $("#expenseCompanyFormHost");
                        formInstance = $formShell
                            .dxForm({
                                formData: initial,
                                labelLocation: "top",
                                colCount: 1,
                                items: [
                                    {
                                        dataField: "taxNumber",
                                        label: { text: t("expenses.fieldTaxNumber") },
                                        editorOptions: { maxLength: 50 }
                                    },
                                    {
                                        dataField: "companyName",
                                        label: { text: t("expenses.fieldCompanyName") },
                                        editorOptions: { maxLength: 300 }
                                    }
                                ]
                            })
                            .dxForm("instance");

                        setTimeout(function () {
                            const taxEditor = formInstance.getEditor("taxNumber");
                            if (!taxEditor) {
                                return;
                            }
                            const $taxWrap = $formShell
                                .find(".dx-field-item[data-field='taxNumber']")
                                .first()
                                .addClass("expense-company-field--tax expense-company-field--lookup");
                            taxLookupApi = wireTaxNumberLookup(taxEditor, applyCompanyFormFields);
                            taxLookupApi.attachIndicator($taxWrap.length ? $taxWrap : $formShell);
                        }, 0);
                    }
                })
            )
            .dxPopup("instance");

        popupInstance.show();
    }

    function openCompanyPicker(onPick) {
        const $popup = $("#expenseCompanyPickerPopup");
        disposePopup($popup);

        let gridInst;
        let searchInst;
        let popupInst;

        function refreshCompanyGrid() {
            if (gridInst) {
                gridInst.refresh();
            }
        }

        popupInst = $popup
            .dxPopup(
                popupBaseOptions(t("expenses.companyPickerTitle"), {
                    width: Math.min(860, Math.max(420, window.innerWidth - 24)),
                    maxHeight: "68vh",
                    wrapperAttr: { class: "guest-picker-popup res-extra-popup res-extra-select-popup" },
                    contentTemplate() {
                        const $wrap = $("<div>").addClass("expense-company-picker");
                        const $toolbar = $("<div>").addClass("expense-company-picker-toolbar").appendTo($wrap);
                        const $addHost = $("<div>").addClass("expense-company-picker-toolbar__add").appendTo($toolbar);
                        const $searchHost = $("<div>").addClass("expense-company-picker-toolbar__search").appendTo($toolbar);
                        const $gridHost = $("<div>")
                            .addClass("guest-picker-grid guest-picker-grid--pl pms-grid-compact")
                            .appendTo($wrap);

                        if (canCreate()) {
                            $addHost.dxButton({
                                text: t("expenses.companyAdd"),
                                icon: "add",
                                type: "default",
                                stylingMode: "contained",
                                onClick() {
                                    openTenantCompanyForm(null, (saved) => {
                                        onPick(saved);
                                        popupInst.hide();
                                    });
                                }
                            });
                        }

                        $searchHost.dxTextBox({
                            placeholder: t("expenses.companySearchPlaceholder"),
                            mode: "search",
                            valueChangeEvent: "keyup",
                            onValueChanged() {
                                refreshCompanyGrid();
                            },
                            onInitialized(e) {
                                searchInst = e.component;
                            }
                        });

                        $gridHost.dxDataGrid({
                            dataSource: new DevExpress.data.CustomStore({
                                key: "taxNumber",
                                load() {
                                    const term = searchInst ? searchInst.option("value") : "";
                                    return expenseApi.searchCompanies(term).then((rows) =>
                                        (rows || []).map((r) => normalizeCompanyRow(r))
                                    );
                                }
                            }),
                            height: Math.min(420, Math.max(280, window.innerHeight * 0.45)),
                            showBorders: true,
                            rowAlternationEnabled: true,
                            elementAttr: { class: "pms-grid-compact" },
                            headerFilter: { visible: true, search: { enabled: true } },
                            searchPanel: { visible: false },
                            paging: { pageSize: 50 },
                            pager: {
                                visible: true,
                                showPageSizeSelector: true,
                                allowedPageSizes: [10, 20, 50],
                                showNavigationButtons: true,
                                showInfo: true
                            },
                            columns: [
                                {
                                    type: "buttons",
                                    width: 88,
                                    buttons: [
                                        {
                                            hint: t("expenses.companyPick"),
                                            icon: "check",
                                            onClick(e) {
                                                onPick(e.row.data);
                                                popupInst.hide();
                                            }
                                        },
                                    ]
                                },
                                {
                                    dataField: "taxNumber",
                                    caption: t("expenses.fieldTaxNumber"),
                                    width: 150
                                },
                                {
                                    dataField: "companyName",
                                    caption: t("expenses.fieldCompanyName"),
                                    minWidth: 200
                                }
                            ],
                            onInitialized(e) {
                                gridInst = e.component;
                            }
                        });

                        return $wrap;
                    }
                })
            )
            .dxPopup("instance");

        popupInst.show();
    }

    function openExpenseForm(row, options) {
        options = options || {};
        let readOnly = !!options.readOnly;
        const isEdit = !!(row && row.expenseId);

        if (isEdit && !readOnly && !canUpdate()) {
            if (canView()) {
                readOnly = true;
            } else {
                DevExpress.ui.notify(t("common.forbidden"), "warning", 3000);
                return;
            }
        } else if (isEdit && readOnly && !canView()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3000);
            return;
        }
        if (!isEdit && !canCreate()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3000);
            return;
        }

        pendingUploadFiles = [];
        let $popup = $("#expenseFormPopup");
        if (!$popup.length) {
            $popup = $("<div id='expenseFormPopup'/>").appendTo("body");
        }
        disposePopup($popup);

        let formInstance;
        let popupInstance;
        let existingImages = [];
        let originalDateTime = isEdit && row.dateTime ? row.dateTime : null;
        let taxNumberEditor;
        let companyNameEditor;

        const $shell = $("<div>").addClass("expense-form-shell");
        const $formHost = $("<div>").appendTo($shell);
        const $taxBlock = $("<div>").addClass("expense-form-tax-block").hide().appendTo($shell);
        const $companyRow = $("<div>").addClass("expense-form-company-row").hide().appendTo($shell);
        const $imagesPanel = $("<div>").addClass("expense-form-panel").appendTo($shell);
        const $imagesHead = $("<div>").addClass("expense-form-panel-head").appendTo($imagesPanel);
        $("<h4>").addClass("expense-form-panel-title").text(t("expenses.imagesSection")).appendTo($imagesHead);
        const $uploaderHost = $("<div>").addClass("expense-uploader-host").appendTo($imagesHead);
        const $existingImagesHost = $("<div>").addClass("expense-images-existing").appendTo($imagesPanel);

        const $taxGrid = $("<div>").addClass("expense-tax-readonly").appendTo($taxBlock);
        function taxKv(label, id) {
            const $kv = $("<div>").addClass("expense-kv").appendTo($taxGrid);
            $("<strong>").text(label).appendTo($kv);
            $("<span>").attr("id", id).appendTo($kv);
        }
        taxKv(t("expenses.fieldBeforeTax"), "expenseBeforeTaxVal");
        taxKv(t("expenses.fieldTaxRate"), "expenseTaxRateVal");
        taxKv(t("expenses.fieldTaxAmount"), "expenseTaxAmountVal");

        function getFormData() {
            const fd = Object.assign({}, formInstance.option("formData"));
            if (taxNumberEditor) {
                fd.taxNumber = taxNumberEditor.option("value");
            }
            if (companyNameEditor) {
                fd.companyName = companyNameEditor.option("value");
            }
            return fd;
        }

        function setCompanyFields(data) {
            if (taxNumberEditor) {
                taxNumberEditor.option("value", data.taxNumber || "");
            }
            if (companyNameEditor) {
                companyNameEditor.option("value", data.companyName || "");
            }
            const fd = formInstance.option("formData");
            fd.taxNumber = data.taxNumber || "";
            fd.companyName = data.companyName || "";
            fd.companyId = null;
            formInstance.option("formData", fd);
        }

        function refreshTaxDisplay(formData) {
            if (!formData.hasTax) {
                $("#expenseBeforeTaxVal").text(fmtMoney(0));
                $("#expenseTaxRateVal").text("0%");
                $("#expenseTaxAmountVal").text(fmtMoney(0));
                return;
            }

            const tax = computeTaxBreakdown(formData.totalAmount, true);
            $("#expenseBeforeTaxVal").text(fmtMoney(tax.beforeTaxAmount));
            $("#expenseTaxRateVal").text(tax.taxRate != null ? `${tax.taxRate}%` : "—");
            $("#expenseTaxAmountVal").text(fmtMoney(tax.taxAmount));
        }

        function syncTaxPanels(formData) {
            const show = !!formData.hasTax;
            $taxBlock.toggle(show);
            $companyRow.toggle(show);
            refreshTaxDisplay(formData);
        }

        function refreshImages() {
            if (isEdit) {
                renderExistingImages($existingImagesHost, existingImages, row.expenseId, () => {
                    expenseApi.getImages(row.expenseId).then((imgs) => {
                        existingImages = imgs || [];
                        refreshImages();
                    });
                });
            }
        }

        const initial = {
            dateTime: isEdit && row.dateTime ? new Date(row.dateTime) : todayDate(),
            expenseCategoryId: normalizeCategoryId(isEdit ? row.expenseCategoryId : null),
            comment: isEdit ? row.comment || "" : "",
            totalAmount: isEdit ? row.totalAmount : null,
            hasTax: isEdit ? !!(row.taxAmount && Number(row.taxAmount) > 0) : false,
            taxNumber: "",
            companyName: "",
            companyId: null
        };

        function buildFormItems(categories) {
            const items = [];
            if (canShowExpenseDate()) {
                items.push({
                    dataField: "dateTime",
                    label: { text: t("expenses.fieldDate") },
                    editorType: "dxDateBox",
                    editorOptions: {
                        type: "date",
                        openOnFieldClick: true,
                        displayFormat: "dd/MM/yyyy"
                    }
                });
            }
            items.push(
                {
                    dataField: "expenseCategoryId",
                    label: { text: t("expenses.fieldCategory") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: categories,
                        displayExpr: "categoryName",
                        valueExpr: "expenseCategoryId",
                        searchEnabled: true,
                        showClearButton: false
                    }
                },
                {
                    dataField: "comment",
                    label: { text: t("expenses.fieldComment") },
                    editorType: "dxTextArea",
                    colSpan: 2,
                    editorOptions: { height: 72, maxLength: 500 }
                },
                {
                    dataField: "totalAmount",
                    label: { text: t("expenses.fieldTotal") },
                    editorType: "dxNumberBox",
                    editorOptions: {
                        format: "#,##0.00",
                        min: 0,
                        showSpinButtons: false,
                        valueChangeEvent: "input",
                        onValueChanged(e) {
                            const fd = formInstance.option("formData");
                            fd.totalAmount = e.value;
                            if (fd.hasTax) {
                                refreshTaxDisplay(fd);
                            }
                        }
                    }
                },
                {
                    dataField: "hasTax",
                    label: { visible: false },
                    editorType: "dxCheckBox",
                    editorOptions: {
                        text: t("expenses.fieldHasTax"),
                        onValueChanged(e) {
                            const fd = formInstance.option("formData");
                            fd.hasTax = e.value;
                            refreshTaxDisplay(fd);
                            syncTaxPanels(fd);
                        }
                    }
                }
            );
            return items;
        }

        function initCompanyEditors() {
            const $taxNumHost = $("<div>").addClass("expense-company-field expense-company-field--tax").appendTo($companyRow);
            const $companyHost = $("<div>").addClass("expense-company-field expense-company-name-wrap").appendTo($companyRow);

            $("<label>").addClass("expense-inline-label").text(t("expenses.fieldTaxNumber")).appendTo($taxNumHost);
            taxNumberEditor = $("<div>")
                .addClass("expense-company-input")
                .appendTo($taxNumHost)
                .dxTextBox({ maxLength: 50, height: 36 })
                .dxTextBox("instance");

            wireTaxNumberLookup(taxNumberEditor, setCompanyFields).attachIndicator($taxNumHost);

            $("<label>").addClass("expense-inline-label").text(t("expenses.fieldCompanyName")).appendTo($companyHost);
            const $companyInputWrap = $("<div>").addClass("expense-company-input-row").appendTo($companyHost);
            companyNameEditor = $("<div>")
                .addClass("expense-company-input")
                .appendTo($companyInputWrap)
                .dxTextBox({ maxLength: 300, height: 36 })
                .dxTextBox("instance");
            $("<div>")
                .appendTo($companyInputWrap)
                .dxButton({
                    icon: "search",
                    hint: t("expenses.companyPick"),
                    stylingMode: "outlined",
                    type: "default",
                    onClick() {
                        openCompanyPicker((picked) => {
                            setCompanyFields({
                                taxNumber: picked.taxNumber,
                                companyName: picked.companyName
                            });
                        });
                    }
                });
        }

        function applyFormData(formData) {
            formInstance.option("formData", formData);
            const categoryEditor = formInstance.getEditor("expenseCategoryId");
            if (categoryEditor) {
                categoryEditor.option("value", formData.expenseCategoryId);
            }
            setCompanyFields(formData);
            syncTaxPanels(formData);
        }

        popupInstance = $popup
            .dxPopup(
                popupBaseOptions(
                    isEdit
                        ? readOnly
                            ? t("expenses.view")
                            : t("expenses.edit")
                        : t("expenses.add"),
                    {
                    contentTemplate() {
                        return $shell;
                    },
                    toolbarItems: [
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: readOnly ? t("expenses.close") : t("expenses.cancel"),
                                onClick() {
                                    popupInstance.hide();
                                }
                            }
                        },
                        ...(readOnly
                            ? []
                            : [
                                  {
                                      widget: "dxButton",
                                      location: "after",
                                      toolbar: "bottom",
                                      options: {
                                          text: t("expenses.save"),
                                          type: "default",
                                          onClick() {
                                              const formData = getFormData();
                                              if (!validateForm(formData, isEdit, existingImages.length)) {
                                                  return;
                                              }

                                              const payload = buildPayload(formData, originalDateTime, isEdit);
                                              const savePromise = isEdit
                                                  ? expenseApi.update(row.expenseId, payload)
                                                  : expenseApi.create(payload);

                                              withLoad(
                                                  savePromise
                                                      .then((saved) => {
                                                          const expenseId = isEdit ? row.expenseId : saved.expenseId;
                                                          const uploads = pendingUploadFiles.slice();
                                                          pendingUploadFiles = [];
                                                          const chain = uploads.length
                                                              ? expenseApi.uploadImages(expenseId, uploads)
                                                              : $.Deferred().resolve().promise();
                                                          return chain.then(() => {
                                                              DevExpress.ui.notify(t("expenses.saveOk"), "success", 2500);
                                                              popupInstance.hide();
                                                              if (gridInstance) {
                                                                  return loadGrid();
                                                              }
                                                          });
                                                      })
                                                      .catch((err) => {
                                                          const msg =
                                                              (err && err.responseJSON && err.responseJSON.message) ||
                                                              t("expenses.saveFailed");
                                                          DevExpress.ui.notify(msg, "error", 4000);
                                                      })
                                              );
                                          }
                                      }
                                  }
                              ])
                    ],
                    onShown() {
                        initCompanyEditors();

                        if (!readOnly) {
                            $uploaderHost.dxFileUploader({
                                multiple: true,
                                accept: "image/*",
                                uploadMode: "useButtons",
                                selectButtonText: t("expenses.uploadImages"),
                                labelText: "",
                                onValueChanged(e) {
                                    pendingUploadFiles = (e.value || []).slice();
                                }
                            });
                        } else {
                            $uploaderHost.hide();
                        }

                        const categoriesPromise = expenseApi.getCategories().then((rows) => rows || []);
                        const detailPromise = isEdit ? expenseApi.getById(row.expenseId) : $.Deferred().resolve(null).promise();

                        withLoad(
                            categoriesPromise
                                .then((categories) =>
                                    detailPromise.then((detail) => ({ categories, detail }))
                                )
                                .then(({ categories, detail }) => {
                                let formData = initial;
                                if (isEdit && detail) {
                                    originalDateTime = detail.dateTime;
                                    formData = mapDetailToFormData(detail, row);
                                    existingImages = detail.images || [];
                                    refreshImages();
                                }

                                const categoriesWithSelected = ensureCategoryOption(
                                    categories,
                                    formData.expenseCategoryId,
                                    (detail && detail.expenseCategoryName) || (row && row.expenseCategoryName)
                                );

                                formInstance = $formHost
                                    .dxForm({
                                        formData,
                                        labelLocation: "top",
                                        colCount: 2,
                                        readOnly: readOnly,
                                        items: buildFormItems(categoriesWithSelected),
                                        onFieldDataChanged(e) {
                                            if (readOnly) {
                                                return;
                                            }
                                            if (e.dataField === "totalAmount" || e.dataField === "hasTax") {
                                                const fd = formInstance.option("formData");
                                                refreshTaxDisplay(fd);
                                                syncTaxPanels(fd);
                                            }
                                        }
                                    })
                                    .dxForm("instance");

                                applyFormData(formData);
                            })
                        );
                    }
                })
            )
            .dxPopup("instance");

        popupInstance.show();
    }

    function confirmDelete(row) {
        if (!canUpdate()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3000);
            return;
        }

        DevExpress.ui.dialog.confirm(t("expenses.deleteConfirm"), t("expenses.delete")).done((ok) => {
            if (!ok) {
                return;
            }
            withLoad(
                expenseApi
                    .remove(row.expenseId)
                    .then(() => {
                        DevExpress.ui.notify(t("expenses.deleteOk"), "success", 2500);
                        return loadGrid();
                    })
                    .catch((err) => {
                        const msg =
                            (err && err.responseJSON && err.responseJSON.message) || t("expenses.deleteFailed");
                        DevExpress.ui.notify(msg, "error", 4000);
                    })
            );
        });
    }

    function initFilters() {
        filterFromDate = startOfMonthDate();
        filterToDate = todayDate();

        $("#expensesFilterFrom").dxDateBox({
            label: t("expenses.filterFrom"),
            labelMode: "static",
            type: "date",
            width: "100%",
            openOnFieldClick: true,
            displayFormat: "dd/MM/yyyy",
            value: filterFromDate,
            onValueChanged(e) {
                filterFromDate = e.value;
            }
        });

        $("#expensesFilterTo").dxDateBox({
            label: t("expenses.filterTo"),
            labelMode: "static",
            type: "date",
            width: "100%",
            openOnFieldClick: true,
            displayFormat: "dd/MM/yyyy",
            value: filterToDate,
            onValueChanged(e) {
                filterToDate = e.value;
            }
        });

        $("#expensesFilterApply").dxButton({
            text: t("expenses.filterApply"),
            type: "default",
            icon: "filter",
            stylingMode: "contained",
            onClick() {
                withLoad(loadGrid());
            }
        });
    }

    function initGrid() {
        const baseGrid = gridOpts ? gridOpts.adminBaseline() : {};
        const merged = gridOpts
            ? gridOpts.merge(baseGrid, {
                  dataSource: [],
                  elementAttr: { class: "pms-grid-compact" },
                  columnAutoWidth: false,
                  headerFilter: { visible: true, search: { enabled: true } },
                  searchPanel: { visible: true, width: 260 },
                  paging: { pageSize: 50 },
                  pager: {
                      visible: true,
                      showPageSizeSelector: true,
                      allowedPageSizes: [10, 20, 50],
                      showNavigationButtons: true,
                      showInfo: true
                  },
                  columns: [
                      {
                          caption: t("expenses.colNo"),
                          width: 128,
                          allowSorting: false,
                          cellTemplate(container, options) {
                              const row = options.data || {};
                              const no = resolveExpenseNo(row);
                              const expenseId = row.expenseId || row.ExpenseId;
                              const $wrap = $("<div>").addClass("expense-no-cell").appendTo(container);
                              $("<span>").addClass("expense-no-cell__no").text(no || "—").appendTo($wrap);
                              if (expenseId) {
                                  $("<div>")
                                      .addClass("expense-no-cell__history")
                                      .appendTo($wrap)
                                      .dxButton({
                                          icon: "orderedlist",
                                          hint: t("expenses.approvalHistoryHint"),
                                          stylingMode: "text",
                                          type: "default",
                                          elementAttr: { class: "expense-no-history-btn" },
                                          onClick() {
                                              openApprovalHistoryPopup(expenseId, no);
                                          }
                                      });
                              }
                          }
                      },
                      {
                          dataField: "dateTime",
                          caption: t("expenses.colDate"),
                          dataType: "date",
                          format: "dd/MM/yyyy",
                          width: 110
                      },
                      {
                          dataField: "expenseCategoryName",
                          caption: t("expenses.colCategory"),
                          width: 168,
                          allowResizing: false,
                          cssClass: "expenses-col-category",
                          cellTemplate(container, options) {
                              const text = (options.value || "").toString().trim();
                              $("<div>")
                                  .addClass("expenses-grid-category-cell")
                                  .attr("title", text || "")
                                  .text(text || "—")
                                  .appendTo(container);
                          }
                      },
                      {
                          dataField: "comment",
                          caption: t("expenses.colComment"),
                          minWidth: 320,
                          cssClass: "expenses-col-comment",
                          cellTemplate(container, options) {
                              const text = (options.value || "").toString().trim();
                              if (!text) {
                                  $("<span>").text("—").appendTo(container);
                                  return;
                              }
                              $("<div>")
                                  .addClass("expenses-grid-comment-cell")
                                  .attr("title", text)
                                  .text(text)
                                  .appendTo(container);
                          }
                      },
                      {
                          dataField: "totalAmount",
                          caption: t("expenses.colTotal"),
                          alignment: "right",
                          width: 100,
                          customizeText(e) {
                              return fmtMoney(e.value);
                          }
                      },
                      {
                          caption: t("expenses.colImage"),
                          width: 90,
                          allowFiltering: false,
                          allowSorting: false,
                          cellTemplate(container, options) {
                              const url = options.data.firstImageUrl || options.data.FirstImageUrl;
                              if (!url) {
                                  $("<span>").text("—").appendTo(container);
                                  return;
                              }
                              $("<a>")
                                  .attr("href", url)
                                  .attr("target", "_blank")
                                  .attr("rel", "noopener noreferrer")
                                  .addClass("expense-image-link")
                                  .text(t("expenses.viewImage"))
                                  .appendTo(container);
                          }
                      },
                      {
                          dataField: "approvalStatus",
                          caption: t("expenses.colStatus"),
                          width: 108,
                          minWidth: 108,
                          allowResizing: false,
                          cssClass: "expenses-col-status",
                          cellTemplate(container, options) {
                              $("<span>")
                                  .addClass(statusBadgeClass(options.value))
                                  .text(statusLabel(options.value))
                                  .appendTo(container);
                          }
                      },
                      {
                          type: "buttons",
                          width: 90,
                          buttons: [
                              {
                                  hint: t("expenses.edit"),
                                  icon: "edit",
                                  visible: canUpdate(),
                                  onClick(e) {
                                      openExpenseForm(e.row.data);
                                  }
                              },
                              {
                                  hint: t("expenses.delete"),
                                  icon: "trash",
                                  visible: canUpdate(),
                                  onClick(e) {
                                      confirmDelete(e.row.data);
                                  }
                              }
                          ]
                      }
                  ]
              })
            : {
                  dataSource: [],
                  elementAttr: { class: "pms-grid-compact" },
                  headerFilter: { visible: true, search: { enabled: true } },
                  searchPanel: { visible: true, width: 260 },
                  columns: []
              };

        gridInstance = $("#expensesGrid").dxDataGrid(merged).dxDataGrid("instance");
    }

    function initFab() {
        const $host = $("#expensesFabHost");
        if (!$host.length) {
            return;
        }

        $host.empty();
        if (!canCreate()) {
            $host.prop("hidden", true);
            return;
        }

        const label = t("expenses.add");
        $host.prop("hidden", false);
        $("<div>")
            .appendTo($host)
            .dxButton({
                icon: "add",
                text: label,
                type: "default",
                stylingMode: "contained",
                hint: label,
                elementAttr: { class: "pms-admin-fab-btn", "aria-label": label },
                onClick() {
                    openExpenseForm(null);
                }
            });
    }

    function applySummaryCurrency() {
        const $host = $("#expensesSummaryCurrency");
        if (!$host.length) {
            return;
        }

        $host.empty();
        const $total = $("#expensesSummaryTotal");
        if ($total.length) {
            $total.attr("dir", "ltr");
            $total.css({ direction: "ltr", flexDirection: "row-reverse" });
        }
        if (loc.isArabic()) {
            $("<img>")
                .attr("src", "/logo/sar-symbol.svg")
                .attr("alt", "ر.س")
                .addClass("expenses-summary-sar-icon")
                .appendTo($host);
        } else {
            $("<span>").addClass("expenses-summary-currency-code").text("SAR").appendTo($host);
        }
    }

    function applyPageI18n() {
        $("[data-i18n]").each(function () {
            const key = $(this).attr("data-i18n");
            if (key) {
                $(this).text(t(key));
            }
        });
        $("[data-i18n-title]").each(function () {
            const key = $(this).attr("data-i18n-title");
            if (key) {
                $(this).attr("title", t(key));
            }
        });
        applySummaryCurrency();
        initFab();
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ExpenseFormUi = {
        open(row, opts) {
            openExpenseForm(row, opts);
        },
        ensureReady() {
            return loadReferenceData();
        }
    };

    function init() {
        if (!$("#expensesGrid").length) {
            return;
        }

        loadPanel = $("#expensesLoadPanel")
            .dxLoadPanel({
                shadingColor: "rgba(15, 23, 42, 0.12)",
                visible: false,
                showIndicator: true,
                showPane: true
            })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-finance-expenses",
            onRefresh: () => withLoad(loadGrid())
        });

        applyPageI18n();
        window.addEventListener("zaaer:culture-changed", () => {
            applySummaryCurrency();
            initFab();
        });
        initFilters();
        initGrid();
        initFab();

        withLoad(
            loadReferenceData()
                .then(loadGrid)
                .then(() => {
                    const params = new URLSearchParams(window.location.search);
                    const expenseId = Number(params.get("expenseId"));
                    if (Number.isFinite(expenseId) && expenseId > 0) {
                        return expenseApi.getById(expenseId).then((row) => {
                            if (row) {
                                openExpenseForm(row);
                            }
                        }).catch(() => undefined);
                    }
                    if (params.get("create") === "1" && canCreate()) {
                        openExpenseForm(null);
                    }
                })
                .catch(() => {
                    DevExpress.ui.notify(t("expenses.loadFailed"), "error", 4000);
                })
        );
    }

    $(init);
})(window, jQuery);
