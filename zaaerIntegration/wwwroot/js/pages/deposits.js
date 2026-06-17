(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const depositApi = window.Zaaer.PmsDepositService;
    const api = window.Zaaer.ApiService;
    const gridOpts = window.Zaaer.PmsGridOptions;

    let gridInstance;
    let loadPanel;
    let banksCache = [];
    let paymentMethodsCache = [];
    let filterFromDate;
    let filterToDate;
    let pendingUploadFiles = [];
    let lastGridItems = [];

    const DEPOSIT_DOCUMENT_DATE_PERMISSION = "finance.deposit.document_date";

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

    function todayDate() {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function monthStartDate() {
        const d = todayDate();
        d.setDate(1);
        return d;
    }

    function canView() {
        return api.hasPermission("finance.deposit.view");
    }

    function canCreate() {
        return api.hasPermission("finance.deposit.create");
    }

    function canUpdate() {
        return api.hasPermission("finance.deposit.update");
    }

    function canShowDepositDate() {
        return api.hasPermission(DEPOSIT_DOCUMENT_DATE_PERMISSION);
    }

    function bankLabel(row) {
        if (!row) {
            return "—";
        }
        if (loc.isArabic()) {
            return row.bankNameAr || row.BankNameAr || row.bankName || row.BankName || "—";
        }
        return row.bankNameEn || row.BankNameEn || row.bankName || row.BankName || "—";
    }

    function paymentMethodLabel(row) {
        if (!row) {
            return "—";
        }
        if (loc.isArabic()) {
            return row.paymentMethodAr || row.nameAr || row.paymentMethod || row.PaymentMethod || "—";
        }
        return row.paymentMethod || row.PaymentMethod || "—";
    }

    function withLoad(promise) {
        if (loadPanel) {
            loadPanel.show();
            return $.when(promise).always(() => loadPanel.hide());
        }
        return $.when(promise);
    }

    function applySummary(result) {
        const s = (result && (result.summary || result.Summary)) || {};
        $("#depositsSummaryTotalAmount").text(fmtMoney(s.totalAmount ?? s.TotalAmount));
        $("#depositsSummaryCount").text(String(s.count ?? s.Count ?? 0));
    }

    function appendSummaryCurrency($host, amountText) {
        $host.empty();
        const $value = $("<bdi>")
            .addClass("expenses-summary-total-value")
            .attr("dir", "ltr")
            .css({ direction: "ltr", flexDirection: "row-reverse" })
            .appendTo($host);
        $("<span>").text(amountText).appendTo($value);
        const $currency = $("<span>").addClass("expenses-summary-currency").attr("aria-hidden", "true").appendTo($value);
        if (loc.isArabic()) {
            $("<img>")
                .attr("src", "/logo/sar-symbol.svg")
                .attr("alt", "ر.س")
                .addClass("expenses-summary-sar-icon")
                .appendTo($currency);
        } else {
            $("<span>").addClass("expenses-summary-currency-code").text("SAR").appendTo($currency);
        }
    }

    function applyBankSummaries(items) {
        const $host = $("#depositsSummaryBanks");
        if (!$host.length) {
            return;
        }
        $host.empty();

        const totalsByBank = new Map();
        (items || []).forEach((row) => {
            const bankKey =
                row.bankId ||
                row.BankId ||
                row.bankZaaerId ||
                row.BankZaaerId ||
                bankLabel(row);
            const name = bankLabel(row);
            const amount = Number(row.displayAmount ?? Math.abs(row.amountPaid || row.AmountPaid || 0)) || 0;
            const existing = totalsByBank.get(bankKey) || { name, total: 0, count: 0 };
            existing.name = name;
            existing.total += amount;
            existing.count += 1;
            totalsByBank.set(bankKey, existing);
        });

        const banks = Array.from(totalsByBank.values()).sort((a, b) => b.total - a.total);
        banks.forEach((bank) => {
            const $card = $("<div>").addClass("expenses-summary-card expenses-summary-card--bank").appendTo($host);
            $("<span>").addClass("expenses-summary-label").text(bank.name).appendTo($card);
            const $amountHost = $("<strong>").appendTo($card);
            appendSummaryCurrency($amountHost, fmtMoney(bank.total));
        });
    }

    function applySummaryCurrency() {
        const $host = $("#depositsSummaryCurrency");
        if (!$host.length) {
            return;
        }
        $host.empty();
        const $total = $("#depositsSummaryTotal");
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

    function loadReferenceData() {
        return $.when(depositApi.getBanks(), depositApi.getPaymentMethods()).then((banks, methods) => {
            banksCache = banks || [];
            paymentMethodsCache = methods || [];
        });
    }

    function loadGrid() {
        const from = filterFromDate ? formatLocalDateParam(filterFromDate) : null;
        const to = filterToDate ? formatLocalDateParam(filterToDate) : null;
        return depositApi.list(from, to).then((result) => {
            const items = (result && (result.items || result.Items)) || [];
            lastGridItems = items;
            gridInstance.option("dataSource", items);
            applySummary(result);
            applyBankSummaries(items);
        });
    }

    function defaultBankZaaerId() {
        const preferred = (banksCache || []).find((b) => b.isDefault || b.IsDefault);
        if (preferred) {
            return preferred.id || preferred.Id;
        }
        const first = banksCache[0];
        return first ? first.id || first.Id : null;
    }

    function resolveBankName(bankZaaerId) {
        const bank = (banksCache || []).find((b) => (b.id || b.Id) === bankZaaerId);
        if (!bank) {
            return "";
        }
        return loc.isArabic() ? bank.nameAr || bank.NameAr || bank.name || bank.Name || "" : bank.name || bank.Name || "";
    }

    function bankSupportsDeposit(bankZaaerId) {
        const bank = (banksCache || []).find((b) => (b.id || b.Id) === bankZaaerId);
        if (!bank) {
            return false;
        }
        const slug = (bank.bankSlug || bank.BankSlug || "").toLowerCase();
        return slug === "bilad" || slug === "riyad";
    }

    function formatNotesAmount(amountPaid) {
        const n = Number(amountPaid);
        if (!Number.isFinite(n) || n <= 0) {
            return "0";
        }
        if (Math.abs(n - Math.round(n)) < 0.001) {
            return String(Math.round(n));
        }
        return fmtMoney(n);
    }

    function buildAutoNotes(amountPaid, bankZaaerId) {
        const bankName = resolveBankName(bankZaaerId);
        const amount = formatNotesAmount(amountPaid);
        const template = loc.isArabic() ? t("deposits.notesAutoAr") : t("deposits.notesAutoEn");
        return template.replace("{amount}", amount).replace("{bank}", bankName || "—");
    }

    function isAutoGeneratedNotes(notes, bankZaaerId) {
        const text = (notes || "").trim();
        if (!text) {
            return true;
        }
        const bankName = resolveBankName(bankZaaerId);
        if (!bankName || bankName === "—") {
            return false;
        }
        const arMarker = "إيداع بنكي بمبلغ";
        const enMarker = "bank deposit of";
        const lower = text.toLowerCase();
        return (
            (text.includes(arMarker) && text.includes(bankName)) ||
            (lower.includes(enMarker) && lower.includes(bankName.toLowerCase()))
        );
    }

    function disposePopup($host) {
        try {
            if ($host.data("dxPopup")) {
                $host.dxPopup("dispose");
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
                width: Math.min(640, Math.max(360, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "72vh",
                wrapperAttr: { class: "expense-form-popup res-extra-popup res-extra-select-popup" }
            },
            extra || {}
        );
    }

    function openImagePreview(url, title) {
        const $popup = $("#depositImagePreviewPopup");
        disposePopup($popup);
        $popup
            .dxPopup(
                popupBaseOptions(title || t("deposits.imagePreviewTitle"), {
                    contentTemplate() {
                        return $("<div>")
                            .addClass("expense-image-preview-shell")
                            .append($("<img>").addClass("expense-image-preview-img").attr("src", url).attr("alt", ""));
                    }
                })
            )
            .dxPopup("instance")
            .show();
    }

    function renderExistingImages($container, images, receiptId, onChange) {
        $container.empty();
        const $grid = $("<div>").addClass("expense-images-grid").appendTo($container);

        (images || []).forEach((img) => {
            const $thumb = $("<div>").addClass("expense-image-thumb").appendTo($grid);
            const path = img.imagePath || img.ImagePath;
            $("<img>").attr("src", path).attr("alt", "").appendTo($thumb);
            $("<button type='button'>")
                .addClass("expense-image-zoom")
                .attr("title", t("deposits.imageZoom"))
                .html("<span class='dx-icon dx-icon-fullscreen'></span>")
                .on("click", (evt) => {
                    evt.preventDefault();
                    evt.stopPropagation();
                    openImagePreview(path, t("deposits.imagePreviewTitle"));
                })
                .appendTo($thumb);
            if (canUpdate()) {
                $("<button type='button'>")
                    .addClass("expense-image-remove")
                    .attr("title", t("deposits.imageDelete"))
                    .text("×")
                    .on("click", () => {
                        withLoad(
                            depositApi
                                .deleteImage(receiptId, img.depositImageId || img.DepositImageId)
                                .then(onChange)
                        );
                    })
                    .appendTo($thumb);
            }
        });
    }

    function defaultPaymentMethodId() {
        const cash = (paymentMethodsCache || []).find((pm) => {
            const code = (pm.code || pm.Code || "").toLowerCase();
            const name = (pm.name || pm.Name || "").toLowerCase();
            return code === "cash" || name === "cash" || name.includes("cash") || name.includes("نقد");
        });
        if (cash) {
            return cash.id || cash.Id;
        }
        const first = paymentMethodsCache[0];
        return first ? first.id || first.Id : null;
    }

    function validateForm(formInstance) {
        const result = formInstance.validate();
        if (!result.isValid) {
            return false;
        }
        const formData = formInstance.option("formData");
        if (!bankSupportsDeposit(formData.bankZaaerId)) {
            DevExpress.ui.notify(t("deposits.validationBankUnsupported"), "warning", 3500);
            return false;
        }
        return true;
    }

    function buildPayload(formData, isEdit) {
        const payload = {
            amountPaid: Number(formData.amountPaid),
            bankZaaerId: formData.bankZaaerId,
            paymentMethodId: formData.paymentMethodId,
            notes: (formData.notes || "").trim() || null
        };
        if (canShowDepositDate()) {
            payload.receiptDate = formData.receiptDate
                ? formatLocalDateParam(formData.receiptDate)
                : formatLocalDateParam(todayDate());
        } else if (!isEdit) {
            payload.receiptDate = formatLocalDateParam(todayDate());
        }
        return payload;
    }

    function openDepositForm(row, options) {
        options = options || {};
        let readOnly = !!options.readOnly;
        const isEdit = !!row;

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

        let $popup = $("#depositFormPopup");
        if (!$popup.length) {
            $popup = $("<div id='depositFormPopup'/>").appendTo("body");
        }
        disposePopup($popup);

        let popupInstance;
        let formInstance;
        let existingImages = [];
        let suppressNotesAutoDisable = false;
        pendingUploadFiles = [];

        const $shell = $("<div>").addClass("expense-form-shell");
        const $formHost = $("<div>").appendTo($shell);
        const $imagesPanel = $("<div>").addClass("expense-form-panel").appendTo($shell);
        const $imagesHead = $("<div>").addClass("expense-form-panel-head").appendTo($imagesPanel);
        $("<h4>").addClass("expense-form-panel-title").text(t("deposits.imagesSection")).appendTo($imagesHead);
        const $uploaderHost = $("<div>").addClass("expense-uploader-host").appendTo($imagesHead);
        const $existingImagesHost = $("<div>").addClass("expense-images-existing").appendTo($imagesPanel);

        const defaultBankId = isEdit ? row.bankId || row.BankId : defaultBankZaaerId();
        const initial = {
            receiptDate: isEdit && row.receiptDate ? new Date(row.receiptDate) : todayDate(),
            amountPaid: isEdit ? row.displayAmount ?? Math.abs(row.amountPaid || 0) : null,
            bankZaaerId: defaultBankId,
            paymentMethodId: isEdit ? row.paymentMethodId || row.PaymentMethodId : defaultPaymentMethodId(),
            notes: isEdit ? row.notes || row.Notes || "" : buildAutoNotes(0, defaultBankId)
        };
        let autoNotesEnabled = !isEdit || isAutoGeneratedNotes(initial.notes, defaultBankId);

        function readAmountForNotes() {
            const fd = formInstance.option("formData") || {};
            const amountEditor = formInstance.getEditor("amountPaid");
            if (!amountEditor) {
                return fd.amountPaid;
            }
            try {
                const raw = amountEditor.element().find("input.dx-texteditor-input").val();
                const cleaned = String(raw || "")
                    .replace(/,/g, "")
                    .trim();
                if (cleaned) {
                    const parsed = Number(cleaned);
                    if (Number.isFinite(parsed)) {
                        return parsed;
                    }
                }
            } catch {
                /* optional */
            }
            return fd.amountPaid;
        }

        function syncAutoNotes() {
            if (!autoNotesEnabled || !formInstance) {
                return;
            }
            const fd = formInstance.option("formData") || {};
            const notes = buildAutoNotes(readAmountForNotes(), fd.bankZaaerId);
            suppressNotesAutoDisable = true;
            try {
                formInstance.updateData("notes", notes);
                const notesEditor = formInstance.getEditor("notes");
                if (notesEditor && notesEditor.option("value") !== notes) {
                    notesEditor.option("value", notes);
                }
            } finally {
                suppressNotesAutoDisable = false;
            }
        }

        function wireAutoNotesEditors() {
            const bankEditor = formInstance.getEditor("bankZaaerId");
            if (bankEditor) {
                bankEditor.option("onValueChanged", () => {
                    syncAutoNotes();
                });
            }

            const amountEditor = formInstance.getEditor("amountPaid");
            if (amountEditor) {
                amountEditor.option("onValueChanged", () => {
                    syncAutoNotes();
                });
                amountEditor
                    .element()
                    .find("input.dx-texteditor-input")
                    .off("input.depositAutoNotes")
                    .on("input.depositAutoNotes", () => {
                        syncAutoNotes();
                    });
            }

            const notesEditor = formInstance.getEditor("notes");
            if (notesEditor) {
                notesEditor.option("onValueChanged", () => {
                    if (suppressNotesAutoDisable) {
                        return;
                    }
                    autoNotesEnabled = false;
                });
            }
        }

        function buildFormItems() {
            const items = [];
            if (canShowDepositDate()) {
                items.push({
                    dataField: "receiptDate",
                    label: { text: t("deposits.fieldDate") },
                    isRequired: true,
                    validationRules: [{ type: "required", message: t("deposits.validationDate") }],
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
                    dataField: "bankZaaerId",
                    label: { text: t("deposits.fieldBank") },
                    isRequired: true,
                    validationRules: [{ type: "required", message: t("deposits.validationBank") }],
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: banksCache,
                        displayExpr: loc.isArabic() ? "nameAr" : "name",
                        valueExpr: "id",
                        searchEnabled: true,
                        showClearButton: false
                    }
                },
                {
                    dataField: "paymentMethodId",
                    label: { text: t("deposits.fieldPaymentMethod") },
                    isRequired: true,
                    validationRules: [{ type: "required", message: t("deposits.validationPaymentMethod") }],
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: paymentMethodsCache,
                        displayExpr: loc.isArabic() ? "nameAr" : "name",
                        valueExpr: "id",
                        searchEnabled: true,
                        showClearButton: false
                    }
                },
                {
                    dataField: "amountPaid",
                    label: { text: t("deposits.fieldAmount") },
                    isRequired: true,
                    validationRules: [
                        { type: "required", message: t("deposits.validationAmount") },
                        {
                            type: "range",
                            min: 0.01,
                            message: t("deposits.validationAmount")
                        }
                    ],
                    editorType: "dxNumberBox",
                    editorOptions: {
                        format: "#,##0.00",
                        min: 0.01,
                        showSpinButtons: false
                    }
                },
                {
                    dataField: "notes",
                    label: { text: t("deposits.fieldNotes") },
                    isRequired: true,
                    validationRules: [{ type: "required", message: t("deposits.validationNotes") }],
                    editorType: "dxTextArea",
                    colSpan: 2,
                    editorOptions: { height: 80, maxLength: 500 }
                }
            );
            return items;
        }

        popupInstance = $popup
            .dxPopup(
                popupBaseOptions(
                    isEdit
                        ? readOnly
                            ? t("deposits.view")
                            : t("deposits.edit")
                        : t("deposits.add"),
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
                                text: readOnly ? t("deposits.close") : t("deposits.cancel"),
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
                                          text: t("deposits.save"),
                                          type: "default",
                                          onClick() {
                                              if (!validateForm(formInstance)) {
                                                  return;
                                              }
                                              const formData = formInstance.option("formData");
                                              const payload = buildPayload(formData, isEdit);
                                              const savePromise = isEdit
                                                  ? depositApi.update(row.receiptId, payload)
                                                  : depositApi.create(payload);

                                              withLoad(
                                                  savePromise
                                                      .then((saved) => {
                                                          const receiptId = isEdit ? row.receiptId : saved.receiptId;
                                                          const uploads = pendingUploadFiles.slice();
                                                          pendingUploadFiles = [];
                                                          const chain = uploads.length
                                                              ? depositApi.uploadImages(receiptId, uploads)
                                                              : $.Deferred().resolve().promise();
                                                          return chain.then(() => {
                                                              DevExpress.ui.notify(t("deposits.saveOk"), "success", 2500);
                                                              popupInstance.hide();
                                                              if (gridInstance) {
                                                                  return loadGrid();
                                                              }
                                                          });
                                                      })
                                                      .catch((err) => {
                                                          const msg =
                                                              (err && err.responseJSON && err.responseJSON.message) ||
                                                              t("deposits.saveFailed");
                                                          DevExpress.ui.notify(msg, "error", 4000);
                                                      })
                                              );
                                          }
                                      }
                                  }
                              ])
                    ],
                    onShown() {
                        formInstance = $formHost
                            .dxForm({
                                formData: initial,
                                colCount: 2,
                                labelLocation: "top",
                                readOnly: readOnly,
                                showValidationSummary: !readOnly,
                                items: buildFormItems()
                            })
                            .dxForm("instance");

                        wireAutoNotesEditors();
                        if (autoNotesEnabled && !readOnly) {
                            syncAutoNotes();
                        }

                        if (!readOnly) {
                            $uploaderHost.dxFileUploader({
                                multiple: true,
                                accept: "image/*",
                                uploadMode: "useButtons",
                                selectButtonText: t("deposits.uploadImages"),
                                labelText: "",
                                onValueChanged(e) {
                                    pendingUploadFiles = (e.value || []).slice();
                                }
                            });
                        } else {
                            $uploaderHost.hide();
                        }

                        if (isEdit) {
                            function refreshImages() {
                                depositApi.getImages(row.receiptId).then((imgs) => {
                                    existingImages = imgs || [];
                                    renderExistingImages($existingImagesHost, existingImages, row.receiptId, readOnly ? null : refreshImages);
                                });
                            }
                            withLoad(refreshImages());
                        }
                    }
                })
            )
            .dxPopup("instance");

        popupInstance.show();
    }

    function initFilters() {
        filterFromDate = monthStartDate();
        filterToDate = todayDate();

        $("#depositsFilterFrom").dxDateBox({
            label: t("deposits.filterFrom"),
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

        $("#depositsFilterTo").dxDateBox({
            label: t("deposits.filterTo"),
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

        $("#depositsFilterApply").dxButton({
            text: t("deposits.filterApply"),
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
                          dataField: "receiptNo",
                          caption: t("deposits.colNo"),
                          width: 118,
                          allowSorting: false
                      },
                      {
                          dataField: "receiptDate",
                          caption: t("deposits.colDate"),
                          dataType: "date",
                          format: "dd/MM/yyyy",
                          width: 108
                      },
                      {
                          caption: t("deposits.colBank"),
                          width: 148,
                          allowResizing: false,
                          cellTemplate(container, options) {
                              $("<span>").text(bankLabel(options.data)).appendTo(container);
                          }
                      },
                      {
                          dataField: "displayAmount",
                          caption: t("deposits.colAmount"),
                          alignment: "right",
                          width: 100,
                          customizeText(e) {
                              return fmtMoney(e.value);
                          }
                      },
                      {
                          caption: t("deposits.colPaymentMethod"),
                          width: 110,
                          cellTemplate(container, options) {
                              $("<span>").text(paymentMethodLabel(options.data)).appendTo(container);
                          }
                      },
                      {
                          dataField: "notes",
                          caption: t("deposits.colNotes"),
                          minWidth: 260,
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
                          caption: t("deposits.colImage"),
                          width: 82,
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
                                  .text(t("deposits.viewImage"))
                                  .appendTo(container);
                          }
                      },
                      {
                          type: "buttons",
                          width: 88,
                          buttons: [
                              {
                                  hint: t("deposits.edit"),
                                  icon: "edit",
                                  visible: canUpdate(),
                                  onClick(e) {
                                      openDepositForm(e.row.data);
                                  }
                              },
                              {
                                  hint: t("deposits.delete"),
                                  icon: "trash",
                                  visible: canUpdate(),
                                  onClick(e) {
                                      const row = e.row.data;
                                      DevExpress.ui.dialog.confirm(t("deposits.deleteConfirm"), t("deposits.delete")).done(
                                          (ok) => {
                                              if (!ok) {
                                                  return;
                                              }
                                              withLoad(
                                                  depositApi
                                                      .remove(row.receiptId)
                                                      .then(() => {
                                                          DevExpress.ui.notify(t("deposits.deleteOk"), "success", 2500);
                                                          return loadGrid();
                                                      })
                                                      .catch(() => {
                                                          DevExpress.ui.notify(t("deposits.deleteFailed"), "error", 3500);
                                                      })
                                              );
                                          }
                                      );
                                  }
                              }
                          ]
                      }
                  ]
              })
            : {
                  dataSource: [],
                  columns: []
              };

        gridInstance = $("#depositsGrid").dxDataGrid(merged).dxDataGrid("instance");
    }

    function initFab() {
        const $host = $("#depositsFabHost");
        if (!$host.length) {
            return;
        }

        $host.empty();
        if (!canCreate()) {
            $host.prop("hidden", true);
            return;
        }

        const label = t("deposits.add");
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
                    openDepositForm(null);
                }
            });
    }

    function applyPageI18n() {
        $("[data-i18n]").each(function () {
            const key = $(this).attr("data-i18n");
            if (key) {
                $(this).text(t(key));
            }
        });
        applySummaryCurrency();
        applyBankSummaries(lastGridItems);
        initFab();
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.DepositFormUi = {
        open(row, opts) {
            openDepositForm(row, opts);
        },
        ensureReady() {
            return loadReferenceData();
        }
    };

    function init() {
        if (!$("#depositsGrid").length) {
            return;
        }

        if (!canView()) {
            return;
        }

        loadPanel = $("#depositsLoadPanel")
            .dxLoadPanel({
                shadingColor: "rgba(15, 23, 42, 0.12)",
                visible: false,
                showIndicator: true,
                showPane: true
            })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-finance-deposits",
            onRefresh: () => withLoad(loadGrid())
        });

        applyPageI18n();
        window.addEventListener("zaaer:culture-changed", () => {
            applySummaryCurrency();
            applyBankSummaries(lastGridItems);
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
                    const receiptId = Number(params.get("receiptId"));
                    if (Number.isFinite(receiptId) && receiptId > 0) {
                        return depositApi.getById(receiptId).then((row) => {
                            if (row) {
                                openDepositForm(row);
                            }
                        }).catch(() => undefined);
                    }
                    if (params.get("create") === "1" && canCreate()) {
                        openDepositForm(null);
                    }
                })
                .catch(() => {
                    DevExpress.ui.notify(t("deposits.loadFailed"), "error", 4000);
                })
        );
    }

    $(init);
})(window, jQuery);
