(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-disbursements",
            reportKey: "disbursements",
            titleKey: "hotelReports.title.disbursements",
            exportPrefix: "hotel-disbursements",
            keyExpr: "documentId",
            load(query) {
                return common.hotelSvc().getDisbursementsReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "amount");
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi($host, summary, t, fmtMoney) {
                [
                    { label: t("hotelReports.kpi.count"), value: summary.count ?? summary.Count ?? 0 },
                    { label: t("hotelReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount ?? summary.TotalAmount) }
                ].forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hotelReports.col.docNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
                    { caption: t("hotelReports.col.docDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
                    { caption: t("hotelReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
                    {
                        caption: t("hotelReports.col.voucherCode"),
                        field: "receiptType",
                        value: (r) => common.mapVoucherLabelDisplay(r)
                    },
                    { caption: t("hotelReports.col.reason"), field: "reason", value: (r) => r.reason || r.Reason || "" }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "documentNo",
                        caption: ctx.t("hotelReports.col.docNo"),
                        cellTemplate(c, info) {
                            ctx.renderVoucherLink(c, info.data, "disbursements");
                        }
                    },
                    ctx.dateColumn("documentDate", "hotelReports.col.docDate"),
                    ctx.moneyColumn("amount", "hotelReports.col.amount"),
                    {
                        dataField: "receiptType",
                        caption: ctx.t("hotelReports.col.voucherCode"),
                        calculateCellValue(row) {
                            return ctx.mapVoucherLabelDisplay(row);
                        }
                    },
                    { dataField: "reason", caption: ctx.t("hotelReports.col.reason") },
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "unitLabel", caption: ctx.t("hotelReports.col.unit") },
                    { dataField: "customerName", caption: ctx.t("hotelReports.col.customer") },
                    {
                        dataField: "paymentMethod",
                        caption: ctx.t("hotelReports.col.paymentMethod"),
                        calculateCellValue(row) {
                            return ctx.mapPaymentMethodDisplay(row.paymentMethod ?? row.PaymentMethod);
                        }
                    },
                    {
                        dataField: "status",
                        caption: ctx.t("hotelReports.col.status"),
                        width: 100,
                        calculateCellValue(row) {
                            return ctx.mapReceiptStatusDisplay(row.status ?? row.Status);
                        }
                    },
                    { dataField: "notes", caption: ctx.t("hotelReports.col.notes") }
                ];
            }
        });
    });
})(window, jQuery);
