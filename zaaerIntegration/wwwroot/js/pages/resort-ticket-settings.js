(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const service = window.Zaaer.ResortTicketService;
    const api = window.Zaaer.ApiService;
    const SG = window.Zaaer && window.Zaaer.SaveGuard;

    let loadPanel;
    let grid;
    let lookups = { ticketCategories: [], pricingTax: { vatRate: 15, vatTaxIncluded: true } };

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canManage() {
        return api.hasPermission("resort_tickets.manage_types");
    }

    function canManageSettings() {
        return (
            api.hasPermission("resort_tickets.manage_settings")
            || api.hasPermission("resort_tickets.manage_types")
        );
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function categoryLabel(code) {
        return t(`resortTickets.category.${code}`) || code;
    }

    function categoryOptions() {
        const codes = (lookups.ticketCategories || []).map((x) => x.id || x.Id);
        const list = codes.length ? codes : ["entry", "games", "pool", "other"];
        return list.map((id) => ({ id, name: categoryLabel(id) }));
    }

    function vatFieldLabel() {
        const tax = lookups.pricingTax || lookups.PricingTax || {};
        const rate = Number(tax.vatRate ?? tax.VatRate) || 0;
        const included = (tax.vatTaxIncluded ?? tax.VatTaxIncluded) !== false;
        const mode = included ? t("resortTickets.settings.vatIncluded") : t("resortTickets.settings.vatExclusive");
        return `${t("resortTickets.settings.vat")} (${rate}% · ${mode})`;
    }

    function validityModeOptions() {
        return [
            { id: "business_day", name: t("resortTickets.settings.validityMode.businessDay") },
            { id: "from_first_scan", name: t("resortTickets.settings.validityMode.fromFirstScan") }
        ];
    }

    function resolveValidForMinutes(row) {
        const mins = Number(row && (row.validForMinutes ?? row.ValidForMinutes));
        if (mins > 0) {
            return mins;
        }
        const hours = Number(row && (row.validForHours ?? row.ValidForHours)) || 24;
        return hours * 60;
    }

    function normalizeTypeFormData(data) {
        const minutes = Number(data.validForMinutes) || resolveValidForMinutes(data);
        const category = data.ticketCategory || "entry";
        let validityMode = data.validityMode || "business_day";
        if (!data.validityMode && category === "games" && !data.isGeneric) {
            validityMode = "from_first_scan";
        }
        return {
            ...data,
            validForMinutes: minutes,
            validForHours: Math.max(1, Math.ceil(minutes / 60)),
            validityMode
        };
    }

    function typeFormItems(isEdit) {
        return [
            {
                dataField: "code",
                label: { text: t("resortTickets.settings.code") },
                editorOptions: { readOnly: !!isEdit },
                validationRules: [{ type: "required" }]
            },
            {
                dataField: "nameAr",
                label: { text: t("resortTickets.settings.nameAr") },
                validationRules: [{ type: "required" }]
            },
            {
                dataField: "nameEn",
                label: { text: t("resortTickets.settings.nameEn") }
            },
            {
                dataField: "ticketCategory",
                label: { text: t("resortTickets.settings.category") },
                editorType: "dxSelectBox",
                editorOptions: {
                    dataSource: categoryOptions(),
                    valueExpr: "id",
                    displayExpr: "name"
                },
                validationRules: [{ type: "required" }]
            },
            {
                dataField: "unitPrice",
                label: { text: t("resortTickets.settings.price") },
                editorType: "dxNumberBox",
                editorOptions: { min: 0, format: "#,##0.00" }
            },
            {
                dataField: "vatRate",
                label: { text: vatFieldLabel() },
                editorType: "dxNumberBox",
                editorOptions: { readOnly: true, format: "#0.##" },
                helpText: t("resortTickets.settings.vatFromHotel")
            },
            {
                dataField: "validityMode",
                label: { text: t("resortTickets.settings.validityMode") },
                editorType: "dxSelectBox",
                editorOptions: {
                    dataSource: validityModeOptions(),
                    valueExpr: "id",
                    displayExpr: "name"
                }
            },
            {
                dataField: "validForMinutes",
                label: { text: t("resortTickets.settings.validMinutes") },
                editorType: "dxNumberBox",
                editorOptions: { min: 1, max: 10080, step: 5 }
            },
            {
                dataField: "sortOrder",
                label: { text: t("resortTickets.settings.sortOrder") },
                editorType: "dxNumberBox",
                editorOptions: { min: 0, max: 9999 }
            },
            {
                dataField: "isGeneric",
                label: { text: t("resortTickets.settings.isGeneric") },
                editorType: "dxCheckBox"
            },
            {
                dataField: "isActive",
                label: { text: t("resortTickets.settings.active") },
                editorType: "dxCheckBox"
            },
            {
                dataField: "description",
                label: { text: t("common.notes") },
                editorType: "dxTextArea",
                colSpan: 2,
                editorOptions: { height: 72 }
            }
        ];
    }

    function openTypePopup(row) {
        const isEdit = !!row;
        const tax = lookups.pricingTax || lookups.PricingTax || {};
        const initial = row
            ? {
                  ...row,
                  vatRate: Number(tax.vatRate ?? tax.VatRate) || row.vatRate,
                  validForMinutes: resolveValidForMinutes(row),
                  validityMode: row.validityMode || row.ValidityMode || "business_day"
              }
            : {
                code: "",
                nameAr: "",
                nameEn: "",
                ticketCategory: "entry",
                unitPrice: 0,
                vatRate: Number(tax.vatRate ?? tax.VatRate) || 15,
                validForMinutes: 1440,
                validityMode: "business_day",
                sortOrder: 0,
                isGeneric: false,
                isActive: true,
                description: ""
            };

        const $popup = $("<div/>").appendTo("body");
        const $form = $("<div/>").appendTo($popup);
        let formInstance;
        let saveButton;
        const popupSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title: isEdit ? t("resortTickets.settings.editType") : t("resortTickets.settings.addType"),
            visible: true,
            showCloseButton: true,
            width: Math.min(640, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate() {
                return $form;
            },
            onShown() {
                formInstance = $form
                    .dxForm({
                        formData: { ...initial },
                        labelLocation: "top",
                        colCount: 2,
                        items: typeFormItems(isEdit)
                    })
                    .dxForm("instance");
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
                        onInitialized(e) {
                            saveButton = e.component;
                        },
                        onClick() {
                            const validation = formInstance.validate();
                            if (!validation.isValid) {
                                return;
                            }
                            const data = normalizeTypeFormData(formInstance.option("formData"));
                            const work = () =>
                                (isEdit
                                    ? service.updateType(row.ticketTypeId, data)
                                    : service.createType(data)
                                ).then(() => {
                                    DevExpress.ui.notify(t("common.saved"), "success", 2200);
                                });

                            let ran;
                            if (SG && popupSaveGuard) {
                                ran = SG.run(
                                    popupSaveGuard,
                                    () =>
                                        work().then(() => {
                                            SG.closePopupThenRun($popup, refreshGrid);
                                        }),
                                    { button: saveButton }
                                );
                            } else {
                                ran = withLoad(work()).then(() => {
                                    $popup.dxPopup("instance").hide();
                                    return refreshGrid();
                                });
                            }

                            if (ran === false) {
                                return;
                            }

                            if (ran && typeof ran.catch === "function") {
                                ran.catch((err) => {
                                    const msg =
                                        (err && err.responseJSON && err.responseJSON.message) ||
                                        t("common.error");
                                    DevExpress.ui.notify(msg, "error", 3500);
                                });
                            }
                        }
                    }
                }
            ],
            onHidden() {
                $popup.remove();
            }
        });
    }

    function refreshGrid() {
        if (!grid) {
            return $.Deferred().resolve().promise();
        }
        return withLoad(
            service.listTypes().then((rows) => {
                grid.option("dataSource", Array.isArray(rows) ? rows : []);
            })
        );
    }

    function formatTimeField(value) {
        if (!value) {
            return "00:00";
        }
        if (value instanceof Date) {
            const h = String(value.getHours()).padStart(2, "0");
            const m = String(value.getMinutes()).padStart(2, "0");
            return `${h}:${m}`;
        }
        return String(value).slice(0, 5);
    }

    function parseTimeField(value) {
        const parts = String(value || "00:00").split(":");
        const d = new Date();
        d.setHours(Number(parts[0]) || 0, Number(parts[1]) || 0, 0, 0);
        return d;
    }

    function initBusinessHoursTab() {
        const $host = $("<div class='resort-ticket-hours-form'/>");
        const $form = $("<div/>").appendTo($host);
        let formInstance;

        $form.dxForm({
            formData: {
                issueStartTime: parseTimeField("16:00"),
                ticketValidityEndTime: parseTimeField("04:00"),
                gamesValidityEndTime: null,
                dailyCloseTime: parseTimeField("04:00")
            },
            labelLocation: "top",
            colCount: 2,
            items: [
                {
                    dataField: "issueStartTime",
                    label: { text: t("resortTickets.settings.issueStartTime") },
                    editorType: "dxDateBox",
                    editorOptions: { type: "time", openOnFieldClick: true, displayFormat: "HH:mm" }
                },
                {
                    dataField: "dailyCloseTime",
                    label: { text: t("resortTickets.settings.dailyCloseTime") },
                    editorType: "dxDateBox",
                    editorOptions: { type: "time", openOnFieldClick: true, displayFormat: "HH:mm" }
                },
                {
                    dataField: "ticketValidityEndTime",
                    label: { text: t("resortTickets.settings.ticketValidityEndTime") },
                    editorType: "dxDateBox",
                    editorOptions: { type: "time", openOnFieldClick: true, displayFormat: "HH:mm" }
                },
                {
                    dataField: "gamesValidityEndTime",
                    label: { text: t("resortTickets.settings.gamesValidityEndTime") },
                    editorType: "dxDateBox",
                    editorOptions: { type: "time", openOnFieldClick: true, displayFormat: "HH:mm" }
                }
            ],
            onInitialized(e) {
                formInstance = e.component;
                service.getBusinessConfig().then((cfg) => {
                    if (!cfg || !formInstance) {
                        return;
                    }
                    formInstance.option("formData", {
                        issueStartTime: parseTimeField(cfg.issueStartTime),
                        ticketValidityEndTime: parseTimeField(cfg.ticketValidityEndTime),
                        gamesValidityEndTime: cfg.gamesValidityEndTime
                            ? parseTimeField(cfg.gamesValidityEndTime)
                            : null,
                        dailyCloseTime: parseTimeField(cfg.dailyCloseTime)
                    });
                });
            }
        });

        $("<div class='resort-ticket-hours-actions'/>")
            .appendTo($host)
            .dxButton({
                text: t("common.save"),
                type: "default",
                icon: "save",
                visible: canManageSettings(),
                onClick() {
                    const data = formInstance.option("formData") || {};
                    const body = {
                        issueStartTime: formatTimeField(data.issueStartTime),
                        ticketValidityEndTime: formatTimeField(data.ticketValidityEndTime),
                        gamesValidityEndTime: data.gamesValidityEndTime
                            ? formatTimeField(data.gamesValidityEndTime)
                            : null,
                        dailyCloseTime: formatTimeField(data.dailyCloseTime)
                    };
                    withLoad(service.updateBusinessConfig(body)).then(() => {
                        DevExpress.ui.notify(t("common.saved"), "success", 2000);
                    });
                }
            });

        $("<p/>")
            .addClass("resort-ticket-hours-station-hint")
            .text(t("resortTickets.gate.stationUrlHint"))
            .appendTo($host);

        return $host;
    }

    function initTabs() {
        $("#resortTicketSettingsTabs").dxTabPanel({
            deferRendering: false,
            rtlEnabled: isAr(),
            animationEnabled: true,
            items: [
                {
                    title: t("resortTickets.settings.tab.types"),
                    template() {
                        const $wrap = $("<div class='resort-ticket-types-tab'/>");
                        const $gridHost = $("<div class='resort-ticket-types-grid'/>").appendTo($wrap);
                        initGrid($gridHost);
                        return $wrap;
                    }
                },
                {
                    title: t("resortTickets.settings.tab.hours"),
                    visible: canManageSettings(),
                    template: initBusinessHoursTab
                }
            ]
        });
    }

    function initGrid($host) {
        if (grid || !$host || !$host.length) {
            return;
        }
        const po = window.Zaaer.PmsGridOptions;
        grid = $host
            .dxDataGrid(
                po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                    dataSource: [],
                    keyExpr: "ticketTypeId",
                    height: "calc(100vh - 260px)",
                    headerFilter: { visible: true, search: { enabled: true } },
                    searchPanel: { visible: true, width: 280 },
                    elementAttr: { class: "pms-grid-compact" },
                    groupPanel: { visible: true },
                    grouping: { autoExpandAll: true },
                    columns: [
                        {
                            dataField: "ticketCategory",
                            caption: t("resortTickets.settings.category"),
                            groupIndex: 0,
                            lookup: {
                                dataSource: categoryOptions(),
                                valueExpr: "id",
                                displayExpr: "name"
                            }
                        },
                        { dataField: "code", caption: t("resortTickets.settings.code"), width: 130 },
                        { dataField: "nameAr", caption: t("resortTickets.settings.nameAr"), width: 180 },
                        { dataField: "nameEn", caption: t("resortTickets.settings.nameEn"), width: 160 },
                        {
                            dataField: "unitPrice",
                            caption: t("resortTickets.settings.price"),
                            dataType: "number",
                            format: "#,##0.00",
                            width: 100
                        },
                        {
                            dataField: "vatRate",
                            caption: vatFieldLabel(),
                            dataType: "number",
                            format: "#0.##",
                            width: 120,
                            allowEditing: false
                        },
                        {
                            dataField: "validityMode",
                            caption: t("resortTickets.settings.validityMode"),
                            width: 230,
                            minWidth: 200,
                            lookup: {
                                dataSource: validityModeOptions(),
                                valueExpr: "id",
                                displayExpr: "name"
                            }
                        },
                        {
                            dataField: "validForMinutes",
                            caption: t("resortTickets.settings.validMinutes"),
                            width: 165,
                            minWidth: 145,
                            calculateCellValue(row) {
                                return resolveValidForMinutes(row);
                            }
                        },
                        {
                            dataField: "sortOrder",
                            caption: t("resortTickets.settings.sortOrder"),
                            width: 80
                        },
                        {
                            dataField: "isGeneric",
                            caption: t("resortTickets.settings.isGeneric"),
                            dataType: "boolean",
                            width: 90
                        },
                        {
                            dataField: "isActive",
                            caption: t("resortTickets.settings.active"),
                            dataType: "boolean",
                            width: 80
                        },
                        {
                            type: "buttons",
                            width: 120,
                            visible: canManage(),
                            buttons: [
                                {
                                    icon: "edit",
                                    hint: t("common.edit"),
                                    onClick(e) {
                                        openTypePopup(e.row.data);
                                    }
                                },
                                {
                                    icon: "check",
                                    hint: t("resortTickets.settings.activate"),
                                    visible(e) {
                                        return e.row.data.isActive === false;
                                    },
                                    onClick(e) {
                                        withLoad(service.setTypeActive(e.row.data.ticketTypeId, true)).then(refreshGrid);
                                    }
                                },
                                {
                                    icon: "close",
                                    hint: t("resortTickets.settings.deactivate"),
                                    visible(e) {
                                        return e.row.data.isActive !== false;
                                    },
                                    onClick(e) {
                                        withLoad(service.setTypeActive(e.row.data.ticketTypeId, false)).then(refreshGrid);
                                    }
                                }
                            ]
                        }
                    ],
                    toolbar: {
                        items: [
                            {
                                location: "before",
                                widget: "dxButton",
                                visible: canManage(),
                                options: {
                                    text: t("resortTickets.settings.addType"),
                                    icon: "add",
                                    type: "default",
                                    onClick() {
                                        openTypePopup(null);
                                    }
                                }
                            }
                        ]
                    }
                })
            )
            .dxDataGrid("instance");
    }

    function ensureResortProperty() {
        const prop = window.Zaaer.PropertySettingsService;
        if (!prop || !prop.getLookups) {
            return $.Deferred().resolve(false).promise();
        }
        return prop.getLookups().then((data) => {
            const ok = !!(data && data.isResort);
            if (!ok) {
                DevExpress.ui.notify(t("resortTickets.resortOnly"), "warning", 4000);
            }
            return ok;
        });
    }

    function applyTaxToolbarHint() {
        const tax = lookups.pricingTax || lookups.PricingTax || {};
        const included = (tax.vatTaxIncluded ?? tax.VatTaxIncluded) !== false;
        const hint = included
            ? t("resortTickets.settings.priceTaxIncludedHint")
            : t("resortTickets.settings.priceTaxExclusiveHint");
        $(".pms-admin-toolbar-sub").text(`${t("resortTickets.settings.subtitle")} · ${hint}`);
    }

    $(function () {
        loadPanel = $("#resortTicketSettingsLoadPanel")
            .dxLoadPanel({ visible: false, showIndicator: true, shading: true })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({ onRefresh: refreshGrid });

        ensureResortProperty().then((isResort) => {
            if (!isResort) {
                return;
            }
            return withLoad(
                service.getLookups().then((data) => {
                    lookups = data || lookups;
                    applyTaxToolbarHint();
                    initTabs();
                    return refreshGrid();
                })
            );
        }).catch((err) => {
            const msg = (err && err.responseJSON && err.responseJSON.message) || t("common.error");
            DevExpress.ui.notify(msg, "error", 3500);
        });
    });
})(window, jQuery);
