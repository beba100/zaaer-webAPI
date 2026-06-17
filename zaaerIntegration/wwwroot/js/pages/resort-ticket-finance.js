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
    let filterForm;

    function t(key) {
        return loc.t(key);
    }

    function canFinance() {
        return api.hasPermission("resort_tickets.finance");
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

    function startOfMonth() {
        const d = today();
        d.setDate(1);
        return d;
    }

    function readFilterDates() {
        if (filterForm) {
            const data = filterForm.option("formData") || {};
            filterFrom = data.fromDate || startOfMonth();
            filterTo = data.toDate || today();
        }
    }

    function refreshGrid() {
        readFilterDates();
        const params = {
            fromDate: filterFrom,
            toDate: filterTo
        };
        if (service.formatLocalDateParam) {
            params.fromDate = service.formatLocalDateParam(filterFrom);
            params.toDate = service.formatLocalDateParam(filterTo);
        }
        return withLoad(
            service.listPendingInvoiceOrders(params).then((rows) => {
                grid.option("dataSource", rows || []);
            })
        );
    }

    function createInvoicesForOrders(orderIds) {
        if (!orderIds.length) {
            DevExpress.ui.notify(t("resortTickets.finance.selectOrders"), "warning", 2500);
            return;
        }

        return withLoad(service.createTicketInvoices({ ticketOrderIds: orderIds })).then(() => {
            DevExpress.ui.notify(t("resortTickets.finance.invoicesCreated"), "success", 2500);
            return refreshGrid();
        });
    }

    function issueInvoices() {
        let selected = grid.getSelectedRowsData();
        if (!selected.length) {
            const allRows = grid.option("dataSource") || [];
            if (!allRows.length) {
                DevExpress.ui.notify(t("resortTickets.finance.selectOrders"), "warning", 2500);
                return;
            }

            const msg = t("resortTickets.finance.createAllConfirm").replace("{count}", String(allRows.length));
            const deferred = DevExpress.ui.dialog.confirm(msg, t("resortTickets.finance.createInvoices"));
            deferred.done((confirmed) => {
                if (!confirmed) {
                    return;
                }
                const ids = allRows.map((r) => r.ticketOrderId);
                createInvoicesForOrders(ids);
            });
            return;
        }

        const ids = selected.map((r) => r.ticketOrderId);
        createInvoicesForOrders(ids);
    }

    function initToolbar() {
        filterFrom = startOfMonth();
        filterTo = today();
        filterForm = $("#resortTicketFinanceToolbar")
            .dxForm({
                colCount: 4,
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
                            text: t("resortTickets.ordersFilter.apply"),
                            icon: "filter",
                            type: "default",
                            onClick: refreshGrid
                        }
                    },
                    {
                        itemType: "button",
                        horizontalAlignment: "left",
                        buttonOptions: {
                            text: t("resortTickets.finance.createInvoices"),
                            icon: "doc",
                            type: "default",
                            visible: canFinance(),
                            onClick: issueInvoices
                        }
                    }
                ]
            })
            .dxForm("instance");
    }

    function initGrid() {
        grid = $("#resortTicketPendingGrid")
            .dxDataGrid(
                po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                    dataSource: [],
                    keyExpr: "ticketOrderId",
                    height: "calc(100vh - 280px)",
                    selection: { mode: "multiple", showCheckBoxesMode: "always" },
                    headerFilter: { visible: true, search: { enabled: true } },
                    searchPanel: { visible: true, width: 280 },
                    elementAttr: { class: "pms-grid-compact" },
                    columns: [
                        { dataField: "orderNo", caption: t("roomBoard.resortTickets.orderNo"), width: 120 },
                        {
                            dataField: "serviceDate",
                            caption: t("roomBoard.resortTickets.serviceDate"),
                            dataType: "date",
                            width: 120
                        },
                        { dataField: "receiptNo", caption: t("resortTickets.finance.receiptNo"), width: 120 },
                        {
                            dataField: "ticketCount",
                            caption: t("roomBoard.resortTickets.count"),
                            width: 80
                        },
                        {
                            dataField: "totalAmount",
                            caption: t("roomBoard.resortTickets.total"),
                            dataType: "number",
                            format: "#,##0.00",
                            width: 110
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
        loadPanel = $("#resortTicketFinanceLoadPanel")
            .dxLoadPanel({ visible: false, showIndicator: true, shading: true })
            .dxLoadPanel("instance");
        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-resort-ticket-finance",
            onRefresh: refreshGrid
        });
        initToolbar();
        initGrid();
        refreshGrid();
    });
})(window, jQuery);
