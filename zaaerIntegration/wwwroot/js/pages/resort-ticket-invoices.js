(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const service = window.Zaaer.ResortTicketService;
    const api = window.Zaaer.ApiService;
    const po = window.Zaaer.PmsGridOptions;

    let loadPanel;
    let grid;
    let filterFrom;
    let filterTo;

    function t(key) {
        return loc.t(key);
    }

    function canFinance() {
        return api.hasPermission("resort_tickets.finance");
    }

    function canSendZatca() {
        return api.hasPermission("finance.invoice.send_zatca");
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function today() {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function refreshGrid() {
        const params = {
            fromDate: filterFrom,
            toDate: filterTo
        };
        if (service.formatLocalDateParam) {
            params.fromDate = service.formatLocalDateParam(filterFrom);
            params.toDate = service.formatLocalDateParam(filterTo);
        }
        return withLoad(
            service.listTicketInvoices(params).then((rows) => {
                grid.option("dataSource", rows || []);
            })
        );
    }

    function initToolbar() {
        filterFrom = today();
        filterTo = today();
        $("#resortTicketInvoicesToolbar").dxForm({
            colCount: 3,
            labelLocation: "top",
            formData: { fromDate: filterFrom, toDate: filterTo },
            items: [
                {
                    dataField: "fromDate",
                    label: { text: t("common.fromDate") },
                    editorType: "dxDateBox",
                    editorOptions: { type: "date", openOnFieldClick: true }
                },
                {
                    dataField: "toDate",
                    label: { text: t("common.toDate") },
                    editorType: "dxDateBox",
                    editorOptions: { type: "date", openOnFieldClick: true }
                },
                {
                    itemType: "button",
                    horizontalAlignment: "left",
                    buttonOptions: {
                        text: t("common.refresh"),
                        icon: "refresh",
                        onClick() {
                            const data = $("#resortTicketInvoicesToolbar").dxForm("instance").option("formData");
                            filterFrom = data.fromDate;
                            filterTo = data.toDate;
                            refreshGrid();
                        }
                    }
                }
            ]
        });
    }

    function initGrid() {
        grid = $("#resortTicketInvoicesGrid")
            .dxDataGrid(
                po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                    dataSource: [],
                    keyExpr: "invoiceId",
                    height: "calc(100vh - 280px)",
                    headerFilter: { visible: true, search: { enabled: true } },
                    searchPanel: { visible: true, width: 280 },
                    elementAttr: { class: "pms-grid-compact" },
                    columns: [
                        { dataField: "invoiceNo", caption: t("resortTickets.invoices.invoiceNo"), width: 130 },
                        { dataField: "orderNo", caption: t("roomBoard.resortTickets.orderNo"), width: 120 },
                        {
                            dataField: "invoiceDate",
                            caption: t("resortTickets.invoices.date"),
                            dataType: "date",
                            width: 110
                        },
                        {
                            dataField: "totalAmount",
                            caption: t("roomBoard.resortTickets.total"),
                            dataType: "number",
                            format: "#,##0.00",
                            width: 100
                        },
                        {
                            dataField: "zatcaStatus",
                            caption: t("resortTickets.invoices.zatcaStatus"),
                            width: 120
                        },
                        {
                            dataField: "creditNoteNo",
                            caption: t("resortTickets.invoices.creditNoteNo"),
                            width: 130
                        },
                        {
                            dataField: "creditNoteZatcaStatus",
                            caption: t("resortTickets.invoices.creditNoteZatca"),
                            width: 130
                        },
                        {
                            type: "buttons",
                            width: 120,
                            buttons: [
                                {
                                    icon: "export",
                                    hint: t("resortTickets.invoices.sendZatca"),
                                    visible(e) {
                                        return canSendZatca() && e.row.data && !e.row.data.sentToZatca;
                                    },
                                    onClick(e) {
                                        withLoad(service.sendInvoiceToZatca(e.row.data.invoiceId)).then(() => {
                                            DevExpress.ui.notify(t("resortTickets.invoices.zatcaQueued"), "success", 2500);
                                            refreshGrid();
                                        });
                                    }
                                },
                                {
                                    icon: "undo",
                                    hint: t("resortTickets.invoices.sendCreditZatca"),
                                    visible(e) {
                                        return (
                                            canSendZatca()
                                            && e.row.data
                                            && e.row.data.creditNoteId
                                            && !e.row.data.creditNoteSentToZatca
                                        );
                                    },
                                    onClick(e) {
                                        withLoad(service.sendCreditNoteToZatca(e.row.data.creditNoteId)).then(() => {
                                            DevExpress.ui.notify(t("resortTickets.invoices.zatcaQueued"), "success", 2500);
                                            refreshGrid();
                                        });
                                    }
                                }
                            ]
                        }
                    ]
                })
            )
            .dxDataGrid("instance");
    }

    $(function () {
        if (!canFinance()) {
            DevExpress.ui.notify(t("common.forbidden"), "error", 3500);
            return;
        }
        loadPanel = $("#resortTicketInvoicesLoadPanel")
            .dxLoadPanel({ visible: false, showIndicator: true, shading: true })
            .dxLoadPanel("instance");
        window.Zaaer.PmsAdminShell.init({ onRefresh: refreshGrid });
        initToolbar();
        initGrid();
        refreshGrid();
    });
})(window, jQuery);
