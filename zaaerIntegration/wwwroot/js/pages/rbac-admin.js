(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        if (loc && typeof loc.isArabic === "function") {
            return loc.isArabic();
        }
        if (loc && typeof loc.currentCulture === "function") {
            return loc.currentCulture() === "ar";
        }

        const lang = (document.documentElement.lang || "").toLowerCase();
        return lang.startsWith("ar") || document.documentElement.dir === "rtl";
    }

    function roleLabel(r) {
        if (!r) return "";
        return isAr() ? (r.roleNameAr || r.roleNameEn || r.roleName) : (r.roleNameEn || r.roleNameAr || r.roleName);
    }

    function permLabel(p) {
        if (!p) return "";
        return isAr()
            ? (p.permissionNameAr || p.nameAr || p.permissionName)
            : (p.permissionNameEn || p.nameEn || p.permissionName);
    }

    function asArray(data) {
        if (Array.isArray(data)) return data;
        if (data && Array.isArray(data.data)) return data.data;
        if (data && data.error) DevExpress.ui.notify(data.error, "error", 4000);
        return [];
    }

    function normalizeTenants(rows) {
        const lookup = window.Zaaer.PmsHotelLookup;
        if (lookup && typeof lookup.normalizePickerHotels === "function") {
            return lookup.normalizePickerHotels(rows);
        }

        return rows;
    }

    function loadTenants() {
        if (!window.__rbacTenantsPromise) {
            window.__rbacTenantsPromise = api.get("/api/rbac/tenants").then((data) => normalizeTenants(asArray(data)));
        }
        return window.__rbacTenantsPromise;
    }

    function tenantDisplayLabel(item) {
        const lookup = window.Zaaer.PmsHotelLookup;
        if (lookup && typeof lookup.tenantDisplayName === "function") {
            return lookup.tenantDisplayName(item, isAr());
        }

        if (!item) {
            return "";
        }

        return isAr() ? (item.name || item.code || "") : (item.code || item.nameEn || item.name || "");
    }

    function buildUserHotelsTagBoxOptions(tenants) {
        const byId = new Map(
            (tenants || []).map((row) => [Number(row.tenantId || row.TenantId), row])
        );

        function resolveTenantItem(item) {
            if (item && typeof item === "object") {
                return item;
            }

            const id = Number(item);
            return id > 0 ? byId.get(id) : null;
        }

        return {
            dataSource: tenants,
            valueExpr: "tenantId",
            displayExpr: (item) => tenantDisplayLabel(resolveTenantItem(item)),
            searchEnabled: true,
            searchExpr: "searchText",
            showSelectionControls: true,
            selectAllMode: "allPages",
            selectAllText: t("rbac.users.selectAllHotels"),
            applyValueMode: "instantly",
            openOnFieldClick: true,
            multiline: true,
            maxDisplayedTags: 4,
            rtlEnabled: isAr(),
            dropDownOptions: {
                maxHeight: Math.min(420, Math.max(280, Math.floor(window.innerHeight * 0.45)))
            }
        };
    }

    function loadRoles() {
        if (!window.__rbacRolesPromise) {
            window.__rbacRolesPromise = api.get("/api/rbac/roles").then(asArray);
        }
        return window.__rbacRolesPromise;
    }

    function renderPageHelp() {
        $("#rbacPageHelp").hide().empty();
    }

    function pmsPopupOptions(title, width) {
        return {
            title: title || t("rbac.adminTitle"),
            width: width || Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup pms-rbac-editor-popup" }
        };
    }

    function sessionsPopupOptions(title, width) {
        return {
            ...pmsPopupOptions(title, width),
            height: 560,
            maxHeight: "78vh",
            showCloseButton: true,
            hideOnOutsideClick: true,
            animationEnabled: false,
            deferRendering: true,
            wrapperAttr: {
                class: "res-extra-popup res-extra-select-popup pms-rbac-editor-popup pms-rbac-sessions-popup-wrap"
            }
        };
    }

    const USER_TYPES = [
        { value: "owner", labelEn: "Owner" },
        { value: "employee", labelEn: "Employee" },
        { value: "external_employee", labelEn: "External employee" }
    ];

    const DEPARTMENTS = [
        { value: "reception", labelEn: "Reception" },
        { value: "accounting", labelEn: "Accounting" },
        { value: "management", labelEn: "Management" },
        { value: "cleaning", labelEn: "Cleaning" },
        { value: "marketing", labelEn: "Marketing" },
        { value: "residential_units", labelEn: "Residential units" },
        { value: "cafe", labelEn: "Cafe" },
        { value: "delivery", labelEn: "Delivery" }
    ];

    function englishLookupSelectOptions(dataSource) {
        return {
            dataSource,
            valueExpr: "value",
            displayExpr: "labelEn",
            openOnFieldClick: true,
            searchEnabled: true,
            showClearButton: true
        };
    }

    function switchEditorOptions() {
        return {
            width: 72,
            switchedOnText: isAr() ? "نعم" : "ON",
            switchedOffText: isAr() ? "لا" : "OFF"
        };
    }

    function activeSwitchField(colSpan) {
        return {
            itemType: "simple",
            colSpan,
            cssClass: "pms-rbac-form-active-row",
            template(data, itemElement) {
                const $row = $("<div class='pms-rbac-active-switch-row' />").appendTo(itemElement);
                $("<span class='pms-rbac-active-switch-label' />")
                    .text(t("rbac.users.active"))
                    .appendTo($row);
                const $control = $("<div class='pms-rbac-active-switch-control' />").appendTo($row);
                $control.dxSwitch({
                    value: data.component.option("formData").isActive !== false,
                    ...switchEditorOptions(),
                    onValueChanged(e) {
                        data.component.updateData("isActive", e.value);
                    }
                });
            }
        };
    }

    function userTypeField() {
        return {
            dataField: "userType",
            label: { text: t("rbac.users.userType") },
            editorType: "dxSelectBox",
            editorOptions: englishLookupSelectOptions(USER_TYPES)
        };
    }

    function departmentField() {
        return {
            dataField: "department",
            label: { text: t("rbac.users.department") },
            editorType: "dxSelectBox",
            editorOptions: englishLookupSelectOptions(DEPARTMENTS)
        };
    }

    function userEditorFormItems(isEdit, tenants, roles, formState) {
        return [
            activeSwitchField(3),
            { dataField: "username", isRequired: true, label: { text: t("rbac.users.username") } },
            { dataField: "employeeNumber", label: { text: t("rbac.users.employeeNumber") } },
            {
                dataField: "password",
                label: { text: t("auth.password") },
                isRequired: !isEdit,
                editorType: "dxTextBox",
                editorOptions: { mode: "password" }
            },
            {
                dataField: "roleId",
                label: { text: t("rbac.users.role") },
                isRequired: true,
                editorType: "dxSelectBox",
                editorOptions: {
                    dataSource: roles,
                    valueExpr: "roleId",
                    displayExpr: (item) => roleLabel(item),
                    searchEnabled: true
                }
            },
            {
                dataField: "tenantIds",
                colSpan: 2,
                label: { text: t("rbac.users.hotels") },
                isRequired: true,
                editorType: "dxTagBox",
                editorOptions: buildUserHotelsTagBoxOptions(tenants)
            },
            userTypeField(),
            { dataField: "firstName", isRequired: true, label: { text: t("rbac.users.firstName") } },
            { dataField: "lastName", isRequired: true, label: { text: t("rbac.users.lastName") } },
            { dataField: "email", isRequired: true, label: { text: t("rbac.users.email") } },
            { dataField: "phoneNumber", label: { text: t("rbac.users.phone") } },
            departmentField()
        ];
    }

    function roleEditorFormItems() {
        return [
            activeSwitchField(3),
            { dataField: "roleNameAr", isRequired: true, label: { text: t("rbac.roles.nameAr") } },
            { dataField: "roleNameEn", isRequired: true, label: { text: t("rbac.roles.nameEn") } },
            { dataField: "roleCode", label: { text: t("rbac.roles.code") } },
            {
                dataField: "roleDescription",
                colSpan: 3,
                label: { text: t("rbac.roles.description") }
            }
        ];
    }

    function normalizeUserType(value) {
        const v = `${value || ""}`.trim().toLowerCase();
        if (!v) {
            return "employee";
        }

        const match = USER_TYPES.find((x) => x.value === v || x.labelEn.toLowerCase() === v);
        return match ? match.value : v;
    }

    function normalizeHotelCode(code) {
        return `${code || ""}`.trim().toLowerCase();
    }

    function tenantLabelByCode(tenants, code) {
        if (!code) {
            return "";
        }

        const key = normalizeHotelCode(code);
        const tenant = (tenants || []).find((x) => normalizeHotelCode(x.code) === key);
        return tenant ? tenantDisplayLabel(tenant) : code;
    }

    function parseHotelCodes(summary) {
        return [...new Set(
            `${summary || ""}`
                .replace(/[()]/g, "")
                .split(",")
                .map((s) => s.trim())
                .filter(Boolean)
        )];
    }

    function userHasAllHotels(codes, tenants) {
        const available = (tenants || [])
            .map((x) => normalizeHotelCode(x.code))
            .filter(Boolean);
        if (!available.length || !codes.length) {
            return false;
        }

        if (codes.length < available.length) {
            return false;
        }

        const assigned = new Set(codes.map(normalizeHotelCode));
        return available.every((code) => assigned.has(code));
    }

    function formatHotelsCompact(summary, tenants) {
        const codes = parseHotelCodes(summary);
        if (!codes.length) {
            return "";
        }

        if (codes.length === 1) {
            return tenantLabelByCode(tenants, codes[0]);
        }

        if (userHasAllHotels(codes, tenants)) {
            return t("rbac.users.hotelsAll");
        }

        const firstLabel = tenantLabelByCode(tenants, codes[0]);
        return t("rbac.users.hotelsCompact", firstLabel, codes.length - 1);
    }

    function fabButtonLabel(page, config) {
        if (page === "roles") {
            return t("rbac.roles.addTitle");
        }

        if (config.useCustomUserEditor) {
            return t("rbac.users.addTitle");
        }

        return t("common.add");
    }

    function normalizeDepartment(value) {
        const v = `${value || ""}`.trim();
        if (!v) {
            return null;
        }

        const lower = v.toLowerCase().replace(/\s+/g, "_");
        const match = DEPARTMENTS.find((x) => x.value === lower || x.labelEn.toLowerCase() === v.toLowerCase());
        return match ? match.value : v;
    }

    function buildActionColumn(extraButtons) {
        const buttons = [{ name: "edit", hint: t("common.edit") }];
        if (extraButtons && extraButtons.length) buttons.push(...extraButtons);
        return {
            type: "buttons",
            caption: t("rbac.actions"),
            width: 36 + buttons.length * 36,
            minWidth: 108,
            fixed: true,
            fixedPosition: document.documentElement.dir === "rtl" ? "left" : "right",
            buttons
        };
    }

    function openUserEditorPopup(detail, tenants, roles, grid) {
        const isEdit = !!(detail && detail.userId);
        const formState = {
            username: detail.username || "",
            employeeNumber: detail.employeeNumber || "",
            password: "",
            roleId: (detail.roleIds && detail.roleIds[0]) || detail.roleId || null,
            tenantIds: (detail.tenantIds || []).slice(),
            userType: normalizeUserType(detail.userType),
            firstName: detail.firstName || "",
            lastName: detail.lastName || "",
            email: detail.email || "",
            phoneNumber: detail.phoneNumber || "",
            department: normalizeDepartment(detail.department),
            isActive: detail.isActive !== false
        };

        const $host = $("<div />").appendTo("body");

        $host.dxPopup({
            ...pmsPopupOptions(
                isEdit ? t("rbac.users.editTitle") : t("rbac.users.addTitle"),
                Math.min(920, window.innerWidth - 24)
            ),
            visible: true,
            onHidden: () => $host.remove(),
            contentTemplate(contentElement) {
                $("<div />").appendTo(contentElement).dxForm({
                    formData: formState,
                    colCount: 3,
                    labelLocation: "top",
                    items: userEditorFormItems(isEdit, tenants, roles, formState)
                });
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.cancel"),
                        stylingMode: "outlined",
                        onClick: () => $host.dxPopup("instance").hide()
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.save"),
                        type: "default",
                        onInitialized(e) {
                            formState._saveButton = e.component;
                        },
                        onClick() {
                            if (!formState.tenantIds || !formState.tenantIds.length) {
                                DevExpress.ui.notify(t("rbac.users.hotelsRequired"), "warning", 2800);
                                return;
                            }

                            const payload = {
                                username: formState.username,
                                employeeNumber: formState.employeeNumber,
                                password: formState.password || undefined,
                                roleId: formState.roleId,
                                tenantIds: formState.tenantIds,
                                userType: normalizeUserType(formState.userType),
                                firstName: formState.firstName,
                                lastName: formState.lastName,
                                email: formState.email,
                                phoneNumber: formState.phoneNumber,
                                department: normalizeDepartment(formState.department),
                                isActive: formState.isActive !== false
                            };

                            const req = isEdit
                                ? api.put(`/api/rbac/users/${detail.userId}`, payload)
                                : api.post("/api/rbac/users", payload);

                            const SG = window.Zaaer && window.Zaaer.SaveGuard;
                            const userSaveGuard = formState._userSaveGuard || (SG ? SG.create() : null);
                            formState._userSaveGuard = userSaveGuard;

                            const finish = () => {
                                DevExpress.ui.notify(t("common.saved"), "success", 2000);
                                if (SG) {
                                    SG.closePopupThenRun($host, () => grid.refresh());
                                } else {
                                    $host.dxPopup("instance").hide();
                                    grid.refresh();
                                }
                            };

                            let ran;
                            if (SG && userSaveGuard) {
                                ran = SG.run(userSaveGuard, () => req.then(finish), {
                                    button: formState._saveButton
                                });
                            } else {
                                ran = req.then(finish);
                            }

                            if (ran === false) {
                                return;
                            }
                            if (ran && typeof ran.catch === "function") {
                                ran.catch((xhr) => {
                                    const err =
                                        xhr.responseJSON &&
                                        (xhr.responseJSON.error || xhr.responseJSON.message);
                                    DevExpress.ui.notify(err || t("common.error"), "error", 4000);
                                });
                            }
                        }
                    }
                }
            ]
        });
    }

    function canManageSessions() {
        return api.hasPermission && api.hasPermission("security.sessions.manage");
    }

    function formatSessionDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return `${value}`;
        }

        return date.toLocaleString(isAr() ? "ar-SA" : "en-GB", {
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function parseSessionsResponse(data) {
        const rows = (data && (data.sessions || data.activeSessions || data.Sessions || data.ActiveSessions)) || [];
        return {
            rows: Array.isArray(rows) ? rows : [],
            meta: {
                sessionVersion: (data && (data.sessionVersion ?? data.SessionVersion)) || 0,
                isLocked: !!(data && (data.isLocked ?? data.IsLocked))
            }
        };
    }

    function sessionsEmptyHint(rows) {
        if (!rows.length) {
            return t("rbac.sessions.emptyHint");
        }

        const activeCount = rows.filter((row) => row.isActive === true || row.status === "Active").length;
        if (activeCount === 0) {
            return t("rbac.sessions.allRevokedHint");
        }

        return undefined;
    }

    function sessionStatusLabel(status) {
        const key = String(status || "").toLowerCase();
        if (key === "active") {
            return t("rbac.sessions.statusActive");
        }
        if (key === "revoked") {
            return t("rbac.sessions.statusRevoked");
        }
        if (key === "expired") {
            return t("rbac.sessions.statusExpired");
        }
        return status || "";
    }

    function openUserSessionsPopup(userId, username) {
        api.get(`/api/rbac/users/${userId}/sessions`)
            .then((data) => {
                const initial = parseSessionsResponse(data);
                const $host = $("<div />").appendTo("body");
                let sessionsGrid = null;
                let $meta = null;

                function updateMeta(meta) {
                    if (!$meta) {
                        return;
                    }

                    $meta.text(
                        `${t("rbac.sessions.version")}: ${meta.sessionVersion || 0} · ${t("rbac.sessions.locked")}: ${meta.isLocked ? t("rbac.sessions.yes") : t("rbac.sessions.no")}`
                    );
                }

                function reloadSessions() {
                    return api.get(`/api/rbac/users/${userId}/sessions`).then((payload) => {
                        const parsed = parseSessionsResponse(payload);
                        updateMeta(parsed.meta);

                        if (sessionsGrid) {
                            sessionsGrid.option("dataSource", parsed.rows);
                            sessionsGrid.option("noDataText", sessionsEmptyHint(parsed.rows));
                        }

                        return parsed.meta;
                    });
                }

                function runSessionAction(requestPromise, successMessage) {
                    return requestPromise
                        .then(() => {
                            DevExpress.ui.notify(successMessage || t("common.saved"), "success", 2200);
                            return reloadSessions();
                        })
                        .catch((xhr) => {
                            const err = xhr.responseJSON && (xhr.responseJSON.error || xhr.responseJSON.message);
                            DevExpress.ui.notify(err || t("common.error"), "error", 4000);
                        });
                }

                $host.dxPopup({
                    ...sessionsPopupOptions(
                        `${t("rbac.sessions.title")} — ${username || userId}`,
                        Math.min(980, window.innerWidth - 24)
                    ),
                    visible: true,
                    onHidden: () => {
                        safeDxDispose($host.find(".pms-rbac-sessions-grid"), "dxDataGrid");
                        $host.remove();
                    },
                    contentTemplate(contentElement) {
                        const $root = $("<div class='pms-rbac-sessions-popup' />").appendTo(contentElement);

                        $meta = $("<div class='pms-rbac-sessions-meta' />").appendTo($root);
                        updateMeta(initial.meta);

                        const $toolbar = $("<div class='pms-rbac-sessions-toolbar' />").appendTo($root);
                        const $forceBtn = $("<div />").appendTo($toolbar);
                        const $lockBtn = $("<div />").appendTo($toolbar);
                        const $unlockBtn = $("<div />").appendTo($toolbar);

                        $forceBtn.dxButton({
                            text: t("rbac.sessions.forceLogoutAll"),
                            icon: "runner",
                            type: "danger",
                            stylingMode: "contained",
                            onClick: () => {
                                DevExpress.ui.dialog.confirm(
                                    `${t("rbac.sessions.forceLogoutConfirm")} (${username || userId})`,
                                    t("rbac.sessions.forceLogoutAll")
                                ).done((confirmed) => {
                                    if (!confirmed) {
                                        return;
                                    }

                                    runSessionAction(
                                        api.post(`/api/rbac/users/${userId}/force-logout`, {}),
                                        t("rbac.sessions.forceLogoutDone")
                                    );
                                });
                            }
                        });

                        $lockBtn.dxButton({
                            text: t("rbac.sessions.lockUser"),
                            icon: "lock",
                            stylingMode: "outlined",
                            onClick: () => {
                                DevExpress.ui.dialog.confirm(
                                    `${t("rbac.sessions.lockConfirm")} (${username || userId})`,
                                    t("rbac.sessions.lockUser")
                                ).done((confirmed) => {
                                    if (!confirmed) {
                                        return;
                                    }

                                    runSessionAction(
                                        api.post(`/api/rbac/users/${userId}/lock`, { reason: "AdminLocked" }),
                                        t("rbac.sessions.lockDone")
                                    );
                                });
                            }
                        });

                        $unlockBtn.dxButton({
                            text: t("rbac.sessions.unlockUser"),
                            icon: "unlock",
                            type: "default",
                            stylingMode: "contained",
                            onClick: () => {
                                runSessionAction(
                                    api.post(`/api/rbac/users/${userId}/unlock`, {}),
                                    t("rbac.sessions.unlockDone")
                                );
                            }
                        });

                        const $gridHost = $("<div class='pms-rbac-sessions-grid' />").appendTo($root);
                        sessionsGrid = $gridHost.dxDataGrid({
                            dataSource: initial.rows,
                            showBorders: true,
                            rowAlternationEnabled: true,
                            columnAutoWidth: true,
                            wordWrapEnabled: true,
                            height: 340,
                            noDataText: sessionsEmptyHint(initial.rows),
                            elementAttr: { class: "pms-grid-compact" },
                            headerFilter: { visible: true, search: { enabled: true } },
                            searchPanel: { visible: true, width: 260 },
                            paging: { pageSize: 20 },
                            pager: {
                                showPageSizeSelector: true,
                                allowedPageSizes: [10, 20, 50],
                                showInfo: true,
                                showNavigationButtons: true
                            },
                            columns: [
                                {
                                    dataField: "deviceName",
                                    caption: t("rbac.sessions.device"),
                                    minWidth: 140
                                },
                                {
                                    dataField: "ipAddress",
                                    caption: t("rbac.sessions.ip"),
                                    width: 120
                                },
                                {
                                    dataField: "lastActivityAt",
                                    caption: t("rbac.sessions.lastActivity"),
                                    width: 150,
                                    calculateDisplayValue: (row) => formatSessionDate(row.lastActivityAt)
                                },
                                {
                                    dataField: "createdAt",
                                    caption: t("rbac.sessions.createdAt"),
                                    width: 150,
                                    calculateDisplayValue: (row) => formatSessionDate(row.createdAt)
                                },
                                {
                                    dataField: "expiresAt",
                                    caption: t("rbac.sessions.expiresAt"),
                                    width: 150,
                                    calculateDisplayValue: (row) => formatSessionDate(row.expiresAt)
                                },
                                {
                                    dataField: "status",
                                    caption: t("rbac.sessions.status"),
                                    width: 110,
                                    alignment: "center",
                                    cellTemplate(container, options) {
                                        const status = options.value || (options.data.isActive ? "Active" : "Revoked");
                                        const cssClass = String(status).toLowerCase();
                                        $("<span />")
                                            .addClass(`pms-rbac-session-status-badge pms-rbac-session-status-badge--${cssClass}`)
                                            .text(sessionStatusLabel(status))
                                            .appendTo(container);
                                    }
                                },
                                {
                                    dataField: "isCurrent",
                                    caption: t("rbac.sessions.current"),
                                    width: 90,
                                    alignment: "center",
                                    cellTemplate(container, options) {
                                        if (options.value) {
                                            $("<span class='pms-rbac-session-current-badge' />")
                                                .text(t("rbac.sessions.currentBadge"))
                                                .appendTo(container);
                                        }
                                    }
                                },
                                {
                                    type: "buttons",
                                    width: 56,
                                    buttons: [{
                                        hint: t("rbac.sessions.revokeDevice"),
                                        icon: "trash",
                                        visible(e) {
                                            return e.row.data.isActive === true || e.row.data.status === "Active";
                                        },
                                        onClick(e) {
                                            const sessionId = e.row.data.sessionId;
                                            DevExpress.ui.dialog.confirm(
                                                t("rbac.sessions.revokeConfirm"),
                                                t("rbac.sessions.revokeDevice")
                                            ).done((confirmed) => {
                                                if (!confirmed) {
                                                    return;
                                                }

                                                runSessionAction(
                                                    api.post(`/api/rbac/users/${userId}/sessions/${sessionId}/revoke`, {}),
                                                    t("rbac.sessions.revokeDone")
                                                );
                                            });
                                        }
                                    }]
                                }
                            ]
                        }).dxDataGrid("instance");
                    },
                    toolbarItems: [
                        {
                            widget: "dxButton",
                            toolbar: "bottom",
                            location: "after",
                            options: {
                                text: t("common.close"),
                                type: "default",
                                stylingMode: "contained",
                                onClick: () => $host.dxPopup("instance").hide()
                            }
                        }
                    ]
                });
            })
            .catch((xhr) => {
                const err = xhr.responseJSON && (xhr.responseJSON.error || xhr.responseJSON.message);
                DevExpress.ui.notify(err || t("common.error"), "error", 4000);
            });
    }

    function safeDxInstance($el, widgetName) {
        try {
            return $el[widgetName]("instance");
        } catch (err) {
            return null;
        }
    }

    function safeDxDispose($el, widgetName) {
        if (safeDxInstance($el, widgetName)) {
            $el[widgetName]("dispose");
        }
    }

    function triggerRbacAdd(grid, config, tenants, roles) {
        if (config.useCustomUserEditor) {
            openUserEditorPopup({ isActive: true, userType: "employee", tenantIds: [] }, tenants, roles, grid);
        } else {
            grid.addRow();
        }
    }

    function initRbacPageActions(grid, config, tenants, roles, page) {
        $("#adminRefreshButton").hide();

        const $refresh = $("#rbacGridRefresh");
        if ($refresh.length) {
            $refresh.dxButton({
                icon: "refresh",
                hint: t("common.refresh"),
                stylingMode: "text",
                type: "default",
                elementAttr: { class: "pms-admin-grid-refresh-btn" },
                onClick: () => grid.refresh()
            });
        }

        const $fab = $("#rbacFabAdd");
        if (!$fab.length) {
            return;
        }

        if (config.readOnly) {
            $fab.hide();
            return;
        }

        const fabText = fabButtonLabel(page, config);
        safeDxDispose($fab, "dxSpeedDialAction");
        safeDxDispose($fab, "dxButton");

        $fab.removeAttr("hidden").empty().show().dxButton({
            icon: "add",
            text: fabText,
            type: "default",
            stylingMode: "contained",
            hint: fabText,
            height: 38,
            elementAttr: { class: "pms-admin-fab-btn", "aria-label": fabText },
            onClick: () => triggerRbacAdd(grid, config, tenants, roles)
        });
    }

    const configs = {
        users: {
            helpKey: "rbac.pages.usersHelp",
            endpoint: "/api/rbac/users",
            key: "userId",
            useCustomUserEditor: true,
            columns: (tenants, roles) => [
                { dataField: "userId", caption: "ID", width: 64, allowEditing: false },
                { dataField: "employeeNumber", caption: t("rbac.users.employeeNumber") },
                { dataField: "username", caption: t("rbac.users.username") },
                { dataField: "firstName", caption: t("rbac.users.firstName") },
                { dataField: "lastName", caption: t("rbac.users.lastName") },
                { dataField: "email", caption: t("rbac.users.email") },
                { dataField: "phoneNumber", caption: t("rbac.users.phone") },
                {
                    dataField: "department",
                    caption: t("rbac.users.department"),
                    calculateCellValue: (row) => {
                        const match = DEPARTMENTS.find((x) => x.value === row.department);
                        return match ? match.labelEn : (row.department || "");
                    }
                },
                { dataField: "roleSummary", caption: t("rbac.users.role"), allowEditing: false },
                {
                    name: "hotelsCompact",
                    caption: t("rbac.users.hotels"),
                    allowEditing: false,
                    minWidth: 148,
                    cssClass: "pms-admin-hotels-cell",
                    calculateCellValue: (row) => formatHotelsCompact(row.hotelsSummary, tenants),
                    calculateDisplayValue: (row) => formatHotelsCompact(row.hotelsSummary, tenants),
                    cellTemplate(container, options) {
                        const row = options.data;
                        const compact = formatHotelsCompact(row.hotelsSummary, tenants);
                        const full = row.hotelsSummary || "";
                        const $cell = $("<div class='pms-admin-hotels-cell-inner' />").appendTo(container);

                        $("<span />")
                            .addClass("pms-admin-hotels-text")
                            .text(compact)
                            .attr("title", full)
                            .appendTo($cell);
                    }
                },
                { dataField: "isActive", caption: t("rbac.users.active"), dataType: "boolean" },
                {
                    type: "buttons",
                    caption: t("rbac.actions"),
                    width: canManageSessions() ? 108 : 72,
                    fixed: true,
                    fixedPosition: document.documentElement.dir === "rtl" ? "left" : "right",
                    buttons: [
                        {
                            hint: t("common.edit"),
                            icon: "edit",
                            onClick(e) {
                                api.get(`/api/rbac/users/${e.row.data.userId}`).then((detail) => {
                                    if (detail) {
                                        openUserEditorPopup(detail, tenants, roles, window.__rbacGrid);
                                    }
                                });
                            }
                        },
                        {
                            hint: t("rbac.sessions.manage"),
                            icon: "group",
                            visible: canManageSessions(),
                            onClick(e) {
                                const row = e.row.data;
                                openUserSessionsPopup(row.userId, row.username || row.employeeNumber);
                            }
                        }
                    ]
                }
            ],
            formItems(tenants, roles) {
                return [
                    { dataField: "username", isRequired: true },
                    { dataField: "employeeNumber" },
                    {
                        dataField: "password",
                        editorType: "dxTextBox",
                        editorOptions: { mode: "password" }
                    },
                    {
                        dataField: "roleId",
                        label: { text: t("rbac.users.role") },
                        editorType: "dxSelectBox",
                        isRequired: true,
                        editorOptions: {
                            dataSource: roles,
                            valueExpr: "roleId",
                            displayExpr: (item) => roleLabel(item)
                        }
                    },
                    {
                        dataField: "tenantIds",
                        label: { text: t("rbac.users.hotels") },
                        editorType: "dxTagBox",
                        isRequired: true,
                        editorOptions: buildUserHotelsTagBoxOptions(tenants)
                    },
                    userTypeField(),
                    { dataField: "firstName", isRequired: true },
                    { dataField: "lastName", isRequired: true },
                    { dataField: "email", isRequired: true },
                    { dataField: "phoneNumber" },
                    departmentField(),
                    activeSwitchField(2)
                ];
            },
            mapPayload(values) {
                return {
                    username: values.username,
                    employeeNumber: values.employeeNumber,
                    password: values.password,
                    roleId: values.roleId,
                    tenantIds: values.tenantIds || [],
                    userType: normalizeUserType(values.userType),
                    firstName: values.firstName,
                    lastName: values.lastName,
                    email: values.email,
                    phoneNumber: values.phoneNumber,
                    department: normalizeDepartment(values.department),
                    isActive: values.isActive !== false
                };
            }
        },
        roles: {
            helpKey: "rbac.roles.help",
            endpoint: "/api/rbac/roles",
            key: "roleId",
            columns: () => [
                { dataField: "roleId", caption: "ID", width: 64, allowEditing: false },
                {
                    dataField: "roleNameAr",
                    caption: t("rbac.roles.nameAr"),
                    calculateCellValue: (r) => r.roleNameAr || r.roleName
                },
                {
                    dataField: "roleNameEn",
                    caption: t("rbac.roles.nameEn"),
                    calculateCellValue: (r) => r.roleNameEn || r.roleName
                },
                { dataField: "roleCode", caption: t("rbac.roles.code") },
                { dataField: "isActive", caption: t("rbac.users.active"), dataType: "boolean" },
                buildActionColumn([{
                    hint: t("rbac.roles.permissions"),
                    icon: "key",
                    onClick(e) {
                        const roleId = e.row.data.roleId;
                        window.location.href = `/role-permissions.html?roleId=${encodeURIComponent(roleId)}`;
                    }
                }])
            ],
            formItems() {
                return roleEditorFormItems();
            },
            mapPayload(values) {
                return {
                    roleNameAr: values.roleNameAr,
                    roleNameEn: values.roleNameEn,
                    roleCode: values.roleCode,
                    roleDescription: values.roleDescription,
                    isActive: values.isActive !== false
                };
            }
        },
        permissions: {
            helpKey: "rbac.permissions.help",
            endpoint: "/api/rbac/permissions",
            key: "permissionId",
            readOnly: true,
            columns: () => [
                { dataField: "permissionId", caption: "ID", width: 52 },
                { dataField: "moduleName", caption: t("rbac.permissions.module"), width: 88 },
                { dataField: "submoduleName", caption: t("rbac.permissions.submodule"), width: 100 },
                { dataField: "actionName", caption: t("rbac.permissions.action"), width: 88 },
                { dataField: "permissionCode", caption: t("rbac.roles.code"), minWidth: 180 },
                {
                    caption: t("rbac.roles.name"),
                    minWidth: 160,
                    calculateCellValue: (row) => permLabel({
                        permissionNameAr: row.permissionNameAr,
                        permissionNameEn: row.permissionNameEn,
                        permissionName: row.permissionNameEn || row.permissionNameAr
                    })
                },
                { dataField: "isActive", caption: t("rbac.users.active"), dataType: "boolean", width: 72 }
            ]
        }
    };

    function storeFor(config) {
        let rows = [];
        return new DevExpress.data.CustomStore({
            key: config.key,
            load: () => api.get(config.endpoint).then((data) => {
                rows = asArray(data);
                return rows;
            }),
            insert: (values) => {
                const payload = config.mapPayload ? config.mapPayload(values) : values;
                return api.post(config.endpoint, payload);
            },
            update: (key, values) => {
                const current = rows.find((x) => x[config.key] === key) || {};
                const merged = { ...current, ...values };
                const payload = config.mapPayload ? config.mapPayload(merged) : merged;
                return api.put(`${config.endpoint}/${key}`, payload);
            }
        });
    }

    function setRolePopupTitle(grid, isNew) {
        const popup = grid.option("editing.popup") || {};
        grid.option("editing.popup", {
            ...popup,
            title: t(isNew ? "rbac.roles.addTitle" : "rbac.roles.editTitle")
        });
    }

    function buildGrid(config, tenants, roles, page) {
        renderPageHelp();
        const formItems = typeof config.formItems === "function"
            ? config.formItems(tenants, roles)
            : (config.formItems || []);
        const formColCount = page === "roles" ? 3 : 2;
        let rolePopupIsNew = false;

        const po = window.Zaaer.PmsGridOptions;
        const grid = $("#rbacGrid").dxDataGrid(
            po.merge(po.adminBaseline(), {
            dataSource: storeFor(config),
            keyExpr: config.key,
            height: "calc(100vh - 248px)",
            paging: { pageSize: 50 },
            pager: po.adminPager(),
            editing: config.readOnly || config.useCustomUserEditor ? {
                allowAdding: false,
                allowUpdating: false,
                allowDeleting: false
            } : {
                mode: "popup",
                allowAdding: true,
                allowUpdating: true,
                allowDeleting: false,
                useIcons: true,
                popup: {
                    ...pmsPopupOptions(
                        page === "roles" ? t("rbac.roles.addTitle") : null,
                        Math.min(920, window.innerWidth - 24)
                    ),
                    width: Math.min(920, window.innerWidth - 24),
                    onShowing(e) {
                        if (page === "roles" && rolePopupIsNew) {
                            e.component.option("title", t("rbac.roles.addTitle"));
                        } else if (page === "roles") {
                            e.component.option("title", t("rbac.roles.editTitle"));
                        }
                    }
                },
                form: {
                    colCount: formColCount,
                    labelLocation: "top",
                    items: formItems
                }
            },
            columns: typeof config.columns === "function" ? config.columns(tenants, roles) : config.columns,
            onInitNewRow(e) {
                e.data.isActive = true;
                e.data.userType = "employee";
                e.data.tenantIds = [];
                if (page === "roles") {
                    rolePopupIsNew = true;
                    setRolePopupTitle(grid, true);
                }
            },
            onEditingStart(e) {
                if (page === "roles") {
                    const isNew = rolePopupIsNew || !e.data || !e.data[config.key];
                    setRolePopupTitle(grid, isNew);
                    rolePopupIsNew = false;
                }
            },
            onToolbarPreparing(e) {
                e.toolbarOptions.visible = false;
            }
            })
        ).dxDataGrid("instance");

        window.__rbacGrid = grid;
        initRbacPageActions(grid, config, tenants, roles, page);
        return grid;
    }

    $(function () {
        loc.init();
        if (!api.requireToken()) return;

        window.Zaaer.PmsAdminShell.init({
            onRefresh: () => window.__rbacGrid && window.__rbacGrid.refresh()
        });

        $("[data-i18n]").each(function () {
            const key = $(this).attr("data-i18n");
            $(this).text(t(key));
        });

        const page = document.body.getAttribute("data-rbac-page") || "users";
        const config = configs[page] || configs.users;

        if (page === "users") {
            $("#pageTitle, #pageSubtitle").empty();
        }

        if (!$("#rbacPageHelp").length) {
            $(".pms-admin-toolbar-host").after("<div id=\"rbacPageHelp\" class=\"pms-admin-help\" hidden></div>");
        }
        $("#rbacPageHelp").hide();

        Promise.all([loadTenants(), loadRoles()]).then(([tenants, roles]) => {
            buildGrid(config, tenants, roles, page);
        });
    });
})(window, jQuery);
