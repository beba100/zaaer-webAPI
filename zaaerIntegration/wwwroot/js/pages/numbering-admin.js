(function (window, $, DevExpress) {
    "use strict";

    const API_BASE = "/api/v1/pms/numbering-admin";
    const PERM = "admin.numbering.manage";

    function t(key) {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        return loc && loc.t ? loc.t(key) : key;
    }

    function notifyError(message) {
        DevExpress.ui.notify(message || "Error", "error", 4200);
    }

    function notifyOk(message) {
        DevExpress.ui.notify(message || "OK", "success", 2600);
    }

    function canManage() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        return api && api.hasPermission && api.hasPermission(PERM);
    }

    async function getStatus(hotelZaaerId) {
        const qs = hotelZaaerId ? `?hotelZaaerId=${encodeURIComponent(hotelZaaerId)}` : "";
        const response = await window.Zaaer.ApiService.get(`${API_BASE}/status${qs}`);
        return response && response.data ? response.data : response;
    }

    async function seedAll() {
        const response = await window.Zaaer.ApiService.post(`${API_BASE}/seed/all`, {});
        return response && response.data ? response.data : response;
    }

    async function seedTenant(body) {
        const response = await window.Zaaer.ApiService.post(`${API_BASE}/seed/tenant`, body || {});
        return response && response.data ? response.data : response;
    }

    async function ensureCounters(body) {
        const response = await window.Zaaer.ApiService.post(`${API_BASE}/ensure-document-counters`, body || {});
        return response && response.data ? response.data : response;
    }

    async function saveTenant(body) {
        const response = await window.Zaaer.ApiService.post(`${API_BASE}/tenants`, body || {});
        return response && response.data ? response.data : response;
    }

    async function syncFromAudit(docCode) {
        const response = await window.Zaaer.ApiService.post(
            `${API_BASE}/sync-entity-from-audit?docCode=${encodeURIComponent(docCode)}`,
            {}
        );
        return response && response.data ? response.data : response;
    }

    async function syncDocumentCountersFromAudit(hotelZaaerId, docCode) {
        const response = await window.Zaaer.ApiService.post(
            `${API_BASE}/sync-document-counters-from-audit?hotelZaaerId=${encodeURIComponent(hotelZaaerId)}&docCode=${encodeURIComponent(docCode)}`,
            {}
        );
        return response && response.data ? response.data : response;
    }

    function initGrids(payload) {
        const data = payload && payload.data ? payload.data : payload;
        const d = data || {};
        const po = window.Zaaer.PmsGridOptions;
        const gridBase = po.baseline();
        const pager = {
            visible: true,
            showInfo: true,
            showNavigationButtons: true,
            showPageSizeSelector: true,
            allowedPageSizes: [10, 20, 50]
        };

        $("#naEntityCountersGrid").dxDataGrid(
            po.merge(gridBase, {
            dataSource: d.entityZaaerCounters || [],
            keyExpr: "entity_code",
            searchPanel: po.searchPanelOptions({ width: 280 }),
            paging: { pageSize: 50 },
            pager,
            columns: [
                { dataField: "entity_code", caption: "entity_code" },
                { dataField: "current_value", caption: "current_value", dataType: "number" },
                { dataField: "updated_at", caption: "updated_at", dataType: "datetime" }
            ]
            })
        );

        $("#naDocumentTypesGrid").dxDataGrid(
            po.merge(gridBase, {
            dataSource: d.documentTypes || [],
            keyExpr: "doc_code",
            searchPanel: po.searchPanelOptions({ width: 280 }),
            paging: { pageSize: 50 },
            pager,
            columns: [
                { dataField: "doc_code", caption: "doc_code" },
                { dataField: "prefix", caption: "prefix" },
                { dataField: "padding", caption: "padding", dataType: "number" },
                { dataField: "scope_level", caption: "scope_level" },
                { dataField: "include_hotel_in_number", caption: "include_hotel_in_number", dataType: "boolean" },
                { dataField: "uses_global_zaaer_id", caption: "uses_global_zaaer_id", dataType: "boolean" },
                { dataField: "separator", caption: "separator" },
                { dataField: "zaaer_entity_code", caption: "zaaer_entity_code" },
                { dataField: "is_active", caption: "is_active", dataType: "boolean" }
            ]
            })
        );

        $("#naAuditGrid").dxDataGrid(
            po.merge(gridBase, {
            dataSource: d.audit || [],
            keyExpr: "audit_id",
            searchPanel: po.searchPanelOptions({ width: 280 }),
            paging: { pageSize: 50 },
            pager,
            columns: [
                { dataField: "audit_id", caption: "audit_id", dataType: "number", width: 90 },
                { dataField: "doc_code", caption: "doc_code", width: 140 },
                { dataField: "zaaer_id", caption: "zaaer_id", dataType: "number", width: 120 },
                { dataField: "numeric_value", caption: "numeric_value", dataType: "number", width: 120 },
                { dataField: "document_no", caption: "document_no", width: 160 },
                { dataField: "status", caption: "status", width: 120 },
                { dataField: "tenant_id", caption: "tenant_id", dataType: "number", width: 90 },
                { dataField: "hotel_zaaer_id", caption: "hotel_zaaer_id", dataType: "number", width: 120 },
                { dataField: "local_hotel_id", caption: "local_hotel_id", dataType: "number", width: 120 },
                { dataField: "request_ref", caption: "request_ref", minWidth: 260 },
                { dataField: "created_at", caption: "created_at", dataType: "datetime", width: 180 }
            ]
            })
        );
    }

    function initForms() {
        const rtl = DevExpress.localization.locale() === "ar";

        $("#naSeedTenantForm").dxForm({
            rtlEnabled: rtl,
            colCount: 2,
            labelLocation: "top",
            formData: { tenantId: null, tenantCode: "" },
            items: [
                { dataField: "tenantId", label: { text: "TenantId" }, editorType: "dxNumberBox" },
                { dataField: "tenantCode", label: { text: "TenantCode" }, editorType: "dxTextBox" }
            ]
        });

        $("#naEnsureForm").dxForm({
            rtlEnabled: rtl,
            colCount: 3,
            labelLocation: "top",
            formData: { tenantId: null, hotelZaaerId: null, localHotelId: null },
            items: [
                { dataField: "tenantId", label: { text: "TenantId" }, editorType: "dxNumberBox" },
                { dataField: "hotelZaaerId", label: { text: "HotelZaaerId" }, editorType: "dxNumberBox" },
                { dataField: "localHotelId", label: { text: "LocalHotelId" }, editorType: "dxNumberBox" }
            ]
        });

        $("#naTenantForm").dxForm({
            rtlEnabled: rtl,
            colCount: 2,
            labelLocation: "top",
            formData: { id: null, code: "", name: "", nameEn: "", databaseName: "", zaaerId: null },
            items: [
                { dataField: "id", label: { text: "Id (optional)" }, editorType: "dxNumberBox" },
                { dataField: "zaaerId", label: { text: "ZaaerId (hotel_settings.zaaer_id)" }, editorType: "dxNumberBox" },
                { dataField: "code", label: { text: "Code" }, editorType: "dxTextBox" },
                { dataField: "databaseName", label: { text: "DatabaseName" }, editorType: "dxTextBox" },
                { dataField: "name", label: { text: "Name" }, editorType: "dxTextBox" },
                { dataField: "nameEn", label: { text: "NameEn" }, editorType: "dxTextBox" }
            ]
        });

        $("#naSyncAuditForm").dxForm({
            rtlEnabled: rtl,
            colCount: 3,
            labelLocation: "top",
            showColonAfterLabel: false,
            formData: { docCode: "reservation", hotelZaaerId: null },
            items: [
                {
                    dataField: "docCode",
                    label: { text: t("numberingAdmin.docCode") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: [
                            "all",
                            "customer",
                            "corporate",
                            "reservation",
                            "payment_receipt",
                            "payment_refund",
                            "invoice",
                            "order",
                            "credit_note",
                            "debit_note",
                            "promissory_note",
                            "expense",
                            "building",
                            "floor",
                            "apartment",
                            "room_type",
                            "facility"
                        ],
                        searchEnabled: true
                    }
                },
                {
                    dataField: "hotelZaaerId",
                    label: { text: t("numberingAdmin.hotelZaaerId") },
                    editorType: "dxNumberBox"
                }
            ]
        });
    }

    async function refreshAll(loadPanel) {
        const seedForm = $("#naSeedTenantForm").dxForm("instance");
        const ensureForm = $("#naEnsureForm").dxForm("instance");
        const hotelZaaerId = ensureForm && ensureForm.option("formData") ? ensureForm.option("formData").hotelZaaerId : null;

        loadPanel.show();
        try {
            const status = await getStatus(hotelZaaerId);
            initGrids({ data: status });
            notifyOk(t("numberingAdmin.loaded"));
        } catch (e) {
            notifyError(e && e.message ? e.message : t("numberingAdmin.loadFailed"));
        } finally {
            loadPanel.hide();
        }
    }

    function initButtons(loadPanel) {
        $("#naSeedAllBtn").dxButton({
            text: t("numberingAdmin.seedAll"),
            icon: "refresh",
            type: "default",
            disabled: !canManage(),
            onClick: async () => {
                loadPanel.show();
                try {
                    await seedAll();
                    notifyOk(t("numberingAdmin.seedStarted"));
                    await refreshAll(loadPanel);
                } catch (e) {
                    notifyError(e && e.message ? e.message : t("numberingAdmin.seedFailed"));
                    loadPanel.hide();
                }
            }
        });

        $("#naRefreshBtn").dxButton({
            text: t("numberingAdmin.refresh"),
            icon: "repeat",
            onClick: async () => refreshAll(loadPanel)
        });

        $("#naSeedTenantBtn").dxButton({
            text: t("numberingAdmin.seedTenantRun"),
            icon: "runner",
            type: "default",
            disabled: !canManage(),
            onClick: async () => {
                const form = $("#naSeedTenantForm").dxForm("instance");
                const body = form ? form.option("formData") : {};
                loadPanel.show();
                try {
                    await seedTenant(body);
                    notifyOk(t("numberingAdmin.seedTenantOk"));
                    await refreshAll(loadPanel);
                } catch (e) {
                    notifyError(e && e.message ? e.message : t("numberingAdmin.seedFailed"));
                    loadPanel.hide();
                }
            }
        });

        $("#naEnsureBtn").dxButton({
            text: t("numberingAdmin.ensureRun"),
            icon: "check",
            type: "default",
            disabled: !canManage(),
            onClick: async () => {
                const form = $("#naEnsureForm").dxForm("instance");
                const body = form ? form.option("formData") : {};
                loadPanel.show();
                try {
                    await ensureCounters(body);
                    notifyOk(t("numberingAdmin.ensureOk"));
                    await refreshAll(loadPanel);
                } catch (e) {
                    notifyError(e && e.message ? e.message : t("numberingAdmin.ensureFailed"));
                    loadPanel.hide();
                }
            }
        });

        $("#naSaveTenantBtn").dxButton({
            text: t("numberingAdmin.saveTenant"),
            icon: "save",
            type: "default",
            disabled: !canManage(),
            onClick: async () => {
                const form = $("#naTenantForm").dxForm("instance");
                const body = form ? form.option("formData") : {};
                loadPanel.show();
                try {
                    await saveTenant(body);
                    notifyOk(t("numberingAdmin.saveTenantOk"));
                } catch (e) {
                    notifyError(e && e.message ? e.message : t("numberingAdmin.saveTenantFailed"));
                } finally {
                    loadPanel.hide();
                }
            }
        });

        $("#naSyncEntityBtn").dxButton({
            text: t("numberingAdmin.syncEntityFromAudit"),
            icon: "arrowup",
            type: "normal",
            disabled: !canManage(),
            onClick: async () => {
                const form = $("#naSyncAuditForm").dxForm("instance");
                const d = form ? form.option("formData") : {};
                const docCode = d && d.docCode ? String(d.docCode) : "";
                loadPanel.show();
                try {
                    await syncFromAudit(docCode);
                    notifyOk(t("numberingAdmin.syncOk"));
                    await refreshAll(loadPanel);
                } catch (e) {
                    notifyError(e && e.message ? e.message : t("numberingAdmin.syncFailed"));
                    loadPanel.hide();
                }
            }
        });

        $("#naSyncDocBtn").dxButton({
            text: t("numberingAdmin.syncDocCountersFromAudit"),
            icon: "arrowup",
            type: "normal",
            disabled: !canManage(),
            onClick: async () => {
                const form = $("#naSyncAuditForm").dxForm("instance");
                const d = form ? form.option("formData") : {};
                const docCode = d && d.docCode ? String(d.docCode) : "";
                const hotelZaaerId = d && d.hotelZaaerId != null ? Number(d.hotelZaaerId) : 0;
                if (!hotelZaaerId || hotelZaaerId <= 0) {
                    notifyError(t("numberingAdmin.hotelZaaerIdRequired"));
                    return;
                }
                loadPanel.show();
                try {
                    await syncDocumentCountersFromAudit(hotelZaaerId, docCode);
                    notifyOk(t("numberingAdmin.syncOk"));
                    await refreshAll(loadPanel);
                } catch (e) {
                    notifyError(e && e.message ? e.message : t("numberingAdmin.syncFailed"));
                    loadPanel.hide();
                }
            }
        });
    }

    function initLoadPanel() {
        return $("#naLoadPanel").dxLoadPanel({
            shading: true,
            showIndicator: true,
            showPane: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            message: t("numberingAdmin.loading"),
            visible: false
        }).dxLoadPanel("instance");
    }

    $(async function () {
        if (!window.Zaaer || !window.Zaaer.ApiService || !window.Zaaer.ApiService.requireToken || !window.Zaaer.ApiService.requireToken()) {
            return;
        }

        // Back to Room Board button (same behavior as other admin pages)
        const $backBtn = $("#naBackToBoardBtn");
        if ($backBtn.length) {
            const label = t("numberingAdmin.backToRoomBoard");
            $backBtn.attr({ title: label, "aria-label": label });
            $backBtn.on("click", () => {
                const api = window.Zaaer && window.Zaaer.ApiService;
                const hotelCode = api && typeof api.getHotelCode === "function" ? api.getHotelCode() : "";
                const url = hotelCode
                    ? `/room-board.html?hotelCode=${encodeURIComponent(hotelCode)}`
                    : "/room-board.html";
                window.location.href = url;
            });
        }

        if (!canManage()) {
            notifyError(t("numberingAdmin.permissionDenied"));
            return;
        }

        const loadPanel = initLoadPanel();
        initForms();
        initButtons(loadPanel);
        await refreshAll(loadPanel);
    });
})(window, window.jQuery, window.DevExpress);

