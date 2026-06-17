(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    /** Hotel role editor: system RBAC grants are managed elsewhere, not per hotel role. */
    const HIDDEN_HOTEL_ROLE_MODULE_CODES = new Set(["admin", "rbac"]);
    const RESORT_MODULE_CODE = "resort";

    const state = {
        roleId: null,
        roleDisplayName: "",
        modules: [],
        selected: new Set(),
        checkboxes: new Map(),
        masterCheckbox: null,
        moduleCheckboxes: new Map(),
        submoduleCheckboxes: new Map(),
        saving: false,
        suppressCheckboxEvents: false,
        collapsedModules: new Set(),
        gateStationCodes: [],
        gateCatalog: [],
        gateTagBox: null
    };

    function t(key, ...args) {
        const text = loc.t(key);
        if (!args.length) {
            return text;
        }

        return args.reduce((acc, val, idx) => acc.replace(`{${idx}}`, val), text);
    }

    function isAr() {
        if (loc.currentCulture) {
            return loc.currentCulture() === "ar";
        }

        const lang = (document.documentElement.lang || "").toLowerCase();
        return lang.startsWith("ar") || document.documentElement.dir === "rtl";
    }

    function localizedField(row, arKeys, enKeys) {
        const pick = (keys) => {
            for (let i = 0; i < keys.length; i += 1) {
                const value = row[keys[i]];
                if (value != null && `${value}`.trim()) {
                    return `${value}`.trim();
                }
            }

            return "";
        };

        const ar = pick(arKeys);
        const en = pick(enKeys);
        return isAr() ? (ar || en) : (en || ar);
    }

    function permName(p) {
        return localizedField(
            p,
            ["nameAr", "NameAr", "permissionNameAr", "PermissionNameAr"],
            ["nameEn", "NameEn", "permissionNameEn", "PermissionNameEn"]
        );
    }

    function humanizeCatalogCode(code) {
        return `${code || ""}`.trim().replace(/_/g, " ");
    }

    function localizedCatalogLabel(prefix, code, row, arKeys, enKeys) {
        const normalized = `${code || ""}`.trim().toLowerCase();
        if (normalized) {
            const key = `${prefix}.${normalized}`;
            const tx = t(key);
            if (tx !== key) {
                return tx;
            }
        }

        const field = localizedField(row, arKeys, enKeys);
        if (field) {
            return field;
        }

        return humanizeCatalogCode(code);
    }

    function moduleName(mod) {
        return localizedCatalogLabel(
            "rbac.modules",
            mod.moduleCode,
            mod,
            ["moduleNameAr", "ModuleNameAr"],
            ["moduleNameEn", "ModuleNameEn"]
        );
    }

    function submoduleName(sub) {
        return localizedCatalogLabel(
            "rbac.submodules",
            sub.submoduleCode,
            sub,
            ["submoduleNameAr", "SubmoduleNameAr"],
            ["submoduleNameEn", "SubmoduleNameEn"]
        );
    }

    function gateStationLabel(row) {
        return localizedField(
            row,
            ["nameAr", "NameAr"],
            ["nameEn", "NameEn"]
        ) || row.stationCode || row.StationCode || "";
    }

    function normalizeMatrixResponse(matrix) {
        if (!matrix || typeof matrix !== "object") {
            return null;
        }

        if (Array.isArray(matrix)) {
            return matrix[0] || null;
        }

        if (matrix.data && matrix.data.roleId) {
            return matrix.data;
        }

        return matrix;
    }

    function resolveLoadErrorMessage(xhrOrError) {
        const xhr = xhrOrError && xhrOrError.responseJSON ? xhrOrError : null;
        if (xhr && xhr.responseJSON) {
            const body = xhr.responseJSON;
            return body.message || body.error || body.title || t("common.error");
        }

        if (xhrOrError && xhrOrError.message === "not_found") {
            return t("rbac.rolePerms.loadFailed");
        }

        if (xhrOrError && xhrOrError.message && xhrOrError.message !== "not_found") {
            return xhrOrError.message;
        }

        if (xhr && xhr.status) {
            return `${t("common.error")} (HTTP ${xhr.status})`;
        }

        return t("common.error");
    }

    function safeApiGet(url) {
        return api.get(url).then(
            (data) => ({ ok: true, data }),
            (xhr) => {
                if (window.console && console.warn) {
                    console.warn("[role-permissions] optional request failed:", url, xhr && xhr.status, xhr);
                }
                return { ok: false, xhr };
            }
        );
    }

    function isResortModule(mod) {
        return `${(mod && mod.moduleCode) || ""}`.trim().toLowerCase() === RESORT_MODULE_CODE;
    }

    function appendResortGateStationsShell($body) {
        const $panel = $("<section/>")
            .addClass("pms-role-perms-gate-panel")
            .attr("id", "roleGateStationsSection")
            .attr("hidden", true)
            .appendTo($body);

        const $head = $("<header/>").addClass("pms-role-perms-gate-panel-head").appendTo($panel);
        $("<span/>")
            .addClass("dx-icon dx-icon-map pms-role-perms-gate-panel-icon")
            .attr("aria-hidden", "true")
            .appendTo($head);

        const $titles = $("<div/>").addClass("pms-role-perms-gate-panel-titles").appendTo($head);
        $("<h3/>").attr("id", "roleGateStationsTitle").addClass("pms-role-perms-gate-title").appendTo($titles);
        $("<p/>").attr("id", "roleGateStationsHint").addClass("pms-role-perms-gate-hint").appendTo($titles);

        $("<div/>").attr("id", "roleGateStationsTagBox").addClass("pms-role-perms-gate-tagbox").appendTo($panel);
    }

    function initGateStationsUi() {
        $("#roleGateStationsTitle").text(t("rbac.rolePerms.gateStationsTitle"));
        $("#roleGateStationsHint").text(t("rbac.rolePerms.gateStationsHint"));

        const $host = $("#roleGateStationsTagBox");
        if (!$host.length) {
            return;
        }

        if ($host.data("dxTagBox")) {
            state.gateTagBox = $host.dxTagBox("instance");
            state.gateTagBox.option({
                dataSource: state.gateCatalog,
                value: state.gateStationCodes.slice()
            });
            return;
        }

        state.gateTagBox = $host
            .dxTagBox({
                dataSource: state.gateCatalog,
                valueExpr: "stationCode",
                displayExpr: (row) => gateStationLabel(row),
                value: state.gateStationCodes.slice(),
                searchEnabled: true,
                showSelectionControls: true,
                applyValueMode: "instantly",
                placeholder: t("rbac.rolePerms.gateStationsPlaceholder"),
                onValueChanged(e) {
                    state.gateStationCodes = Array.isArray(e.value) ? e.value.slice() : [];
                }
            })
            .dxTagBox("instance");
    }

    function loadGateStations(roleId) {
        return safeApiGet(`/api/rbac/roles/${roleId}/gate-stations`)
            .then((roleResp) =>
                safeApiGet("/api/rbac/gate-station-catalog").then((catalogResp) => {
                    if (roleResp.ok) {
                        state.gateStationCodes = (roleResp.data && roleResp.data.stationCodes) || [];
                    }

                    if (catalogResp.ok) {
                        state.gateCatalog = (catalogResp.data && catalogResp.data.data) || [];
                    }

                    if (!state.gateCatalog.length) {
                        $("#roleGateStationsSection").attr("hidden", true);
                        return;
                    }

                    $("#roleGateStationsSection").removeAttr("hidden");
                    initGateStationsUi();
                })
            )
            .catch((err) => {
                if (window.console && console.warn) {
                    console.warn("[role-permissions] gate stations section unavailable", err);
                }
                $("#roleGateStationsSection").attr("hidden", true);
            });
    }

    function saveGateStations() {
        if (!state.roleId || !state.gateCatalog.length) {
            return $.when();
        }

        return api.put(`/api/rbac/roles/${state.roleId}/gate-stations`, {
            stationCodes: state.gateStationCodes
        });
    }

    function roleTitle(matrix) {
        return isAr()
            ? (matrix.roleNameAr || matrix.roleNameEn || matrix.roleCode)
            : (matrix.roleNameEn || matrix.roleNameAr || matrix.roleCode);
    }

    function filterModulesForHotelRoleEditor(modules) {
        return (modules || []).filter((mod) => {
            const code = `${mod.moduleCode || ""}`.trim().toLowerCase();
            return code && !HIDDEN_HOTEL_ROLE_MODULE_CODES.has(code);
        });
    }

    const HALL_NAV_SUBMODULES = new Set(["hall"]);
    const RESORT_NAV_SUBMODULES = new Set(["resort", "resort_tickets"]);
    const HOTEL_NAV_SUBMODULES = new Set(["hotel"]);

    const NAV_SUBMODULE_REMAP = {
        hall: "nav_menu_hall",
        resort: "nav_menu_resort",
        resort_tickets: "nav_menu_resort_tickets",
        hotel: "nav_menu_hotel"
    };

    const HALL_SUBMODULE_ORDER = [
        "events",
        "function_sheet",
        "finance",
        "reports",
        "nav_menu_hall"
    ];

    const RESORT_SUBMODULE_ORDER = [
        "tickets",
        "reports",
        "nav_menu_resort_tickets",
        "nav_menu_resort"
    ];

    const HOTEL_SUBMODULE_ORDER = [
        "reports",
        "nav_menu_hotel"
    ];

    const MODULE_DISPLAY_ORDER = {
        room_board: 10,
        reservations: 20,
        guests: 30,
        property: 40,
        booking_engine: 50,
        pos: 60,
        hall: 70,
        resort: 75,
        hotel: 80,
        finance: 90,
        integrations: 100,
        nav_menu: 110
    };

    function cloneSubmodule(sub) {
        return {
            submoduleCode: sub.submoduleCode,
            submoduleNameAr: sub.submoduleNameAr,
            submoduleNameEn: sub.submoduleNameEn,
            SubmoduleNameAr: sub.SubmoduleNameAr,
            SubmoduleNameEn: sub.SubmoduleNameEn,
            permissions: (sub.permissions || []).slice()
        };
    }

    function cloneModule(mod) {
        return {
            moduleCode: mod.moduleCode,
            moduleNameAr: mod.moduleNameAr,
            moduleNameEn: mod.moduleNameEn,
            ModuleNameAr: mod.ModuleNameAr,
            ModuleNameEn: mod.ModuleNameEn,
            submodules: (mod.submodules || []).map(cloneSubmodule)
        };
    }

    function sortSubmodules(mod, order) {
        const rank = new Map(order.map((code, index) => [code, index]));
        mod.submodules.sort((a, b) => {
            const aKey = `${a.submoduleCode || ""}`.toLowerCase();
            const bKey = `${b.submoduleCode || ""}`.toLowerCase();
            const aRank = rank.has(aKey) ? rank.get(aKey) : 999;
            const bRank = rank.has(bKey) ? rank.get(bKey) : 999;
            if (aRank !== bRank) {
                return aRank - bRank;
            }
            return aKey.localeCompare(bKey);
        });
    }

    function ensureHubModule(list, code) {
        let mod = list.find((entry) => `${entry.moduleCode || ""}`.toLowerCase() === code);
        if (!mod) {
            mod = cloneModule({ moduleCode: code, submodules: [] });
            list.push(mod);
        }
        return mod;
    }

    function extractNavSubmodules(navMod, codes) {
        const pulled = [];
        const kept = [];
        (navMod.submodules || []).forEach((sub) => {
            const key = `${sub.submoduleCode || ""}`.toLowerCase();
            if (codes.has(key)) {
                pulled.push(
                    cloneSubmodule(
                        Object.assign({}, sub, {
                            submoduleCode: NAV_SUBMODULE_REMAP[key] || `nav_menu_${key}`,
                            submoduleNameAr: "",
                            submoduleNameEn: ""
                        })
                    )
                );
            } else {
                kept.push(cloneSubmodule(sub));
            }
        });
        navMod.submodules = kept;
        return pulled;
    }

    function consolidatePropertyHubModules(modules) {
        const list = (modules || []).map(cloneModule);
        const navMod = list.find((entry) => `${entry.moduleCode || ""}`.toLowerCase() === "nav_menu");
        if (!navMod) {
            return sortModulesForDisplay(list);
        }

        const hallNav = extractNavSubmodules(navMod, HALL_NAV_SUBMODULES);
        if (hallNav.length) {
            const hallMod = ensureHubModule(list, "hall");
            hallMod.submodules = hallMod.submodules.concat(hallNav);
            sortSubmodules(hallMod, HALL_SUBMODULE_ORDER);
        }

        const resortNav = extractNavSubmodules(navMod, RESORT_NAV_SUBMODULES);
        if (resortNav.length) {
            const resortMod = ensureHubModule(list, "resort");
            resortMod.submodules = resortMod.submodules.concat(resortNav);
            sortSubmodules(resortMod, RESORT_SUBMODULE_ORDER);
        }

        const hotelNav = extractNavSubmodules(navMod, HOTEL_NAV_SUBMODULES);
        if (hotelNav.length) {
            const hotelMod = ensureHubModule(list, "hotel");
            hotelMod.submodules = hotelMod.submodules.concat(hotelNav);
            sortSubmodules(hotelMod, HOTEL_SUBMODULE_ORDER);
        }

        return sortModulesForDisplay(list);
    }

    function sortModulesForDisplay(modules) {
        return (modules || []).slice().sort((a, b) => {
            const aKey = `${a.moduleCode || ""}`.toLowerCase();
            const bKey = `${b.moduleCode || ""}`.toLowerCase();
            const aRank = MODULE_DISPLAY_ORDER[aKey] != null ? MODULE_DISPLAY_ORDER[aKey] : 500;
            const bRank = MODULE_DISPLAY_ORDER[bKey] != null ? MODULE_DISPLAY_ORDER[bKey] : 500;
            if (aRank !== bRank) {
                return aRank - bRank;
            }
            return aKey.localeCompare(bKey);
        });
    }

    function isPropertyHubModule(mod) {
        const code = `${(mod && mod.moduleCode) || ""}`.toLowerCase();
        return code === "hall" || code === "resort" || code === "hotel";
    }

    function allPermissions(modules) {
        const list = [];
        (modules || []).forEach((mod) => {
            (mod.submodules || []).forEach((sub) => {
                (sub.permissions || []).forEach((p) => list.push(p));
            });
        });
        return list;
    }

    function permissionIdsForModule(mod) {
        const ids = [];
        (mod.submodules || []).forEach((sub) => {
            (sub.permissions || []).forEach((p) => ids.push(p.permissionId));
        });
        return ids;
    }

    function permissionIdsForSubmodule(sub) {
        return (sub.permissions || []).map((p) => p.permissionId);
    }

    function setIds(ids, checked) {
        state.suppressCheckboxEvents = true;
        try {
            ids.forEach((id) => {
                if (checked) {
                    state.selected.add(id);
                } else {
                    state.selected.delete(id);
                }

                const box = state.checkboxes.get(id);
                if (box) {
                    box.option("value", checked);
                }
            });
        } finally {
            state.suppressCheckboxEvents = false;
        }

        refreshGroupStates();
        updateCountLabel();
    }

    function triState(ids) {
        if (!ids.length) {
            return false;
        }

        let granted = 0;
        ids.forEach((id) => {
            if (state.selected.has(id)) {
                granted += 1;
            }
        });

        if (granted === 0) {
            return false;
        }

        if (granted === ids.length) {
            return true;
        }

        return undefined;
    }

    function applyTriState(checkbox, value) {
        if (!checkbox) {
            return;
        }

        state.suppressCheckboxEvents = true;
        try {
            checkbox.option("value", value === true);
        } finally {
            state.suppressCheckboxEvents = false;
        }

        const el = checkbox.element();
        el.toggleClass("pms-role-perms-indeterminate", value === undefined);
    }

    function refreshGroupStates() {
        const allIds = allPermissions(state.modules).map((p) => p.permissionId);
        applyTriState(state.masterCheckbox, triState(allIds));

        state.moduleCheckboxes.forEach((checkbox, moduleCode) => {
            const mod = state.modules.find((m) => m.moduleCode === moduleCode);
            if (mod) {
                applyTriState(checkbox, triState(permissionIdsForModule(mod)));
            }
        });

        state.submoduleCheckboxes.forEach((checkbox, key) => {
            const [moduleCode, submoduleCode] = key.split("::");
            const mod = state.modules.find((m) => m.moduleCode === moduleCode);
            const sub = mod && (mod.submodules || []).find((s) => s.submoduleCode === submoduleCode);
            if (sub) {
                applyTriState(checkbox, triState(permissionIdsForSubmodule(sub)));
            }
        });

        refreshModuleBadges();
    }

    function updateCountLabel() {
        const total = allPermissions(state.modules).length;
        const granted = state.selected.size;
        const label = t("rbac.rolePerms.grantedCount", granted, total);
        $("#rolePermsCount").text(label);
        $("#rolePermsFooterMeta").text(label);
    }

    function buildPayload() {
        return allPermissions(state.modules).map((p) => ({
            permissionId: p.permissionId,
            granted: state.selected.has(p.permissionId)
        }));
    }

    function submoduleParentLabel(mod, sub) {
        const subCode = `${(sub && sub.submoduleCode) || ""}`.toLowerCase();
        if (subCode.startsWith("nav_menu_")) {
            return moduleName({ moduleCode: "nav_menu" });
        }

        return moduleName(mod);
    }

    function renderSubmodule($parent, mod, sub) {
        const subIds = permissionIdsForSubmodule(sub);
        const subKey = `${mod.moduleCode}::${sub.submoduleCode}`;
        const parentLabel = submoduleParentLabel(mod, sub);

        const $wrap = $("<div />")
            .addClass("pms-role-perms-submodule-wrap")
            .attr("data-module-code", mod.moduleCode)
            .appendTo($parent);

        const $section = $("<section />").addClass("pms-role-perms-submodule").appendTo($wrap);

        const $parentTag = $("<div />")
            .addClass("pms-role-perms-submodule-parent")
            .attr("title", parentLabel)
            .appendTo($section);

        $("<span />")
            .addClass("pms-role-perms-submodule-parent-arrow")
            .attr("aria-hidden", "true")
            .appendTo($parentTag);

        $("<span />")
            .addClass("pms-role-perms-submodule-parent-label")
            .text(parentLabel)
            .appendTo($parentTag);

        const $head = $("<header />").addClass("pms-role-perms-submodule-head").appendTo($section);

        const $subAllHost = $("<div />").appendTo($head);
        const subCheckbox = $subAllHost.dxCheckBox({
            text: submoduleName(sub),
            value: triState(subIds) === true,
            onValueChanged(e) {
                if (state.suppressCheckboxEvents || !e.event) {
                    return;
                }

                setIds(subIds, !!e.value);
            }
        }).dxCheckBox("instance");
        $subAllHost.on("click", (evt) => evt.stopPropagation());
        state.submoduleCheckboxes.set(subKey, subCheckbox);

        const $grid = $("<div />").addClass("pms-role-perms-grid").appendTo($section);
        (sub.permissions || []).forEach((p) => {
            const $cell = $("<div />").addClass("pms-role-perms-cell").appendTo($grid);
            const checkbox = $cell.dxCheckBox({
                text: permName(p),
                value: state.selected.has(p.permissionId),
                onValueChanged(e) {
                    if (state.suppressCheckboxEvents || !e.event) {
                        return;
                    }

                    if (e.value) {
                        state.selected.add(p.permissionId);
                    } else {
                        state.selected.delete(p.permissionId);
                    }

                    refreshGroupStates();
                    updateCountLabel();
                }
            }).dxCheckBox("instance");
            state.checkboxes.set(p.permissionId, checkbox);
        });
    }

    function moduleGrantedLabel(mod) {
        const ids = permissionIdsForModule(mod);
        const granted = ids.filter((id) => state.selected.has(id)).length;
        return `${granted}/${ids.length}`;
    }

    function refreshModuleBadges() {
        state.modules.forEach((mod) => {
            const $card = $(`.pms-role-perms-module-card[data-module-code="${mod.moduleCode}"]`);
            $card.find(".pms-role-perms-module-badge").text(moduleGrantedLabel(mod));
        });
    }

    function isModuleCollapsed(moduleCode) {
        return state.collapsedModules.has(moduleCode);
    }

    function syncModuleCardCollapsedUi($card, moduleCode) {
        const collapsed = isModuleCollapsed(moduleCode);
        $card.toggleClass("pms-role-perms-module-card--collapsed", collapsed);
        $card.find(".pms-role-perms-module-card-head").attr("aria-expanded", collapsed ? "false" : "true");
        $card
            .find(".pms-role-perms-module-card-toggle")
            .attr("aria-expanded", collapsed ? "false" : "true");
        $card
            .find(".pms-role-perms-module-card-chevron")
            .toggleClass("pms-role-perms-module-card-chevron--collapsed", collapsed);
    }

    function toggleModuleCardCollapsed(moduleCode) {
        if (state.collapsedModules.has(moduleCode)) {
            state.collapsedModules.delete(moduleCode);
        } else {
            state.collapsedModules.add(moduleCode);
        }

        const $card = $(`.pms-role-perms-module-card[data-module-code="${moduleCode}"]`);
        if ($card.length) {
            syncModuleCardCollapsedUi($card, moduleCode);
        }
    }

    function confirmSelectAllPermissions() {
        const roleName =
            state.roleDisplayName ||
            $("#rolePermsTitle").text().replace(/^[^:]+:\s*/, "").trim() ||
            "";

        return DevExpress.ui.dialog.confirm(
            t("rbac.rolePerms.confirmSelectAll", roleName),
            t("rbac.rolePerms.confirmSelectAllTitle")
        );
    }

    function setAllModulesCollapsed(collapsed) {
        state.collapsedModules.clear();
        if (collapsed) {
            state.modules.forEach((mod) => state.collapsedModules.add(mod.moduleCode));
        }

        $(".pms-role-perms-module-card").each(function () {
            const code = $(this).attr("data-module-code");
            if (code) {
                syncModuleCardCollapsedUi($(this), code);
            }
        });
    }

    function renderModuleCards() {
        const $host = $("#rolePermsCards").empty();
        state.moduleCheckboxes.clear();
        state.submoduleCheckboxes.clear();
        state.checkboxes.clear();

        state.modules.forEach((mod) => {
            const modIds = permissionIdsForModule(mod);
            const $card = $("<article />")
                .addClass("pms-role-perms-module-card")
                .attr("data-module-code", mod.moduleCode)
                .attr("role", "listitem")
                .appendTo($host);

            if ((mod.moduleCode || "").toLowerCase() === "pos") {
                $card.addClass("pms-role-perms-module-card--pos");
            }

            if (isPropertyHubModule(mod)) {
                $card.addClass("pms-role-perms-module-card--property-hub");
                $card.addClass(`pms-role-perms-module-card--${(mod.moduleCode || "").toLowerCase()}-hub`);
            }

            if (isModuleCollapsed(mod.moduleCode)) {
                $card.addClass("pms-role-perms-module-card--collapsed");
            }

            const collapsed = isModuleCollapsed(mod.moduleCode);
            const $head = $("<header />")
                .addClass("pms-role-perms-module-card-head")
                .attr("aria-expanded", collapsed ? "false" : "true")
                .appendTo($card);

            const $toggleBtn = $("<button />")
                .attr("type", "button")
                .addClass("pms-role-perms-module-card-toggle")
                .attr("aria-expanded", collapsed ? "false" : "true")
                .attr("aria-label", `${moduleName(mod)} — ${t("rbac.rolePerms.toggleModule")}`)
                .appendTo($head);

            $("<span />")
                .addClass("pms-role-perms-module-card-chevron dx-icon dx-icon-chevrondown")
                .toggleClass("pms-role-perms-module-card-chevron--collapsed", collapsed)
                .attr("aria-hidden", "true")
                .appendTo($toggleBtn);

            $toggleBtn.on("click", (evt) => {
                evt.preventDefault();
                evt.stopPropagation();
                toggleModuleCardCollapsed(mod.moduleCode);
            });

            $toggleBtn.on("keydown", (evt) => {
                if (evt.key === "Enter" || evt.key === " ") {
                    evt.preventDefault();
                    evt.stopPropagation();
                    toggleModuleCardCollapsed(mod.moduleCode);
                }
            });

            const $headMain = $("<div />").addClass("pms-role-perms-module-card-head-main").appendTo($head);
            const $checkHost = $("<div />").addClass("pms-role-perms-module-card-check").appendTo($headMain);
            const moduleCheckbox = $checkHost.dxCheckBox({
                text: moduleName(mod),
                value: triState(modIds) === true,
                onValueChanged(e) {
                    if (state.suppressCheckboxEvents || !e.event) {
                        return;
                    }

                    setIds(modIds, !!e.value);
                }
            }).dxCheckBox("instance");
            $checkHost.on("click mousedown", (evt) => evt.stopPropagation());
            state.moduleCheckboxes.set(mod.moduleCode, moduleCheckbox);

            $("<span />")
                .addClass("pms-role-perms-module-badge")
                .text(moduleGrantedLabel(mod))
                .appendTo($head);

            const $body = $("<div />")
                .addClass("pms-role-perms-module-card-body")
                .attr("id", `rolePermsModuleBody-${mod.moduleCode}`)
                .appendTo($card);
            (mod.submodules || []).forEach((sub) => renderSubmodule($body, mod, sub));

            if (isResortModule(mod)) {
                $card.addClass("pms-role-perms-module-card--resort");
                appendResortGateStationsShell($body);
            }
        });

        refreshGroupStates();
        updateCountLabel();
    }

    function loadMatrix(roleId) {
        return api.get(`/api/rbac/roles/${roleId}/permissions/matrix`).then((rawMatrix) => {
            const matrix = normalizeMatrixResponse(rawMatrix);
            const roleKey = matrix && (matrix.roleId || matrix.RoleId);
            if (!matrix || !roleKey) {
                throw new Error("not_found");
            }

            state.roleId = roleKey;
            state.modules = consolidatePropertyHubModules(
                filterModulesForHotelRoleEditor(matrix.modules || matrix.Modules)
            );
            state.selected.clear();
            state.collapsedModules.clear();
            state.modules.forEach((mod) => state.collapsedModules.add(mod.moduleCode));

            allPermissions(state.modules).forEach((p) => {
                if (p.granted) {
                    state.selected.add(p.permissionId);
                }
            });

            const title = roleTitle(matrix);
            state.roleDisplayName = title;
            $("#rolePermsTitle").text(`${t("rbac.roles.permissions")}: ${title}`);
            $("#rolePermsSummary").text("").attr("hidden", true);
            document.title = `${t("rbac.roles.permissions")} — ${title}`;

            renderModuleCards();
            loadGateStations(roleId);
        });
    }

    const SG = window.Zaaer && window.Zaaer.SaveGuard;
    const permissionsSaveGuard = SG ? SG.create() : null;

    function saveAllRoleSettings() {
        return $.when(
            api.put(`/api/rbac/roles/${state.roleId}/permissions`, buildPayload()),
            saveGateStations()
        );
    }

    function savePermissions() {
        if (!state.roleId) {
            return;
        }

        const saveBtn = $("#rolePermsSaveButton").dxButton("instance");
        const ran =
            SG && permissionsSaveGuard
                ? SG.run(
                      permissionsSaveGuard,
                      () =>
                          saveAllRoleSettings().then(() => {
                              DevExpress.ui.notify(t("common.saved"), "success", 1600);
                              window.setTimeout(() => {
                                  window.location.href = "/roles.html";
                              }, 500);
                          }),
                      { button: saveBtn }
                  )
                : (function () {
                      if (state.saving) {
                          return false;
                      }
                      state.saving = true;
                      if (saveBtn) {
                          saveBtn.option("disabled", true);
                      }
                      return saveAllRoleSettings()
                          .then(() => {
                              DevExpress.ui.notify(t("common.saved"), "success", 1600);
                              window.setTimeout(() => {
                                  window.location.href = "/roles.html";
                              }, 500);
                          })
                          .always(() => {
                              state.saving = false;
                              if (saveBtn) {
                                  saveBtn.option("disabled", false);
                              }
                          });
                  })();

        if (ran === false) {
            return;
        }
        if (ran && typeof ran.catch === "function") {
            ran.catch((xhr) => {
                const err = xhr.responseJSON && (xhr.responseJSON.error || xhr.responseJSON.message);
                DevExpress.ui.notify(err || t("common.error"), "error", 4000);
            });
        }
    }

    function initToolbar() {
        $("#rolePermsBackButton").dxButton({
            icon: "back",
            text: t("rbac.rolePerms.backToRoles"),
            stylingMode: "outlined",
            onClick: () => {
                window.location.href = "/roles.html";
            }
        });

        $("#rolePermsSaveButton").dxButton({
            icon: "save",
            text: t("rbac.rolePerms.save"),
            type: "default",
            stylingMode: "contained",
            width: "auto",
            height: 42,
            onClick: savePermissions
        });

        state.masterCheckbox = $("#rolePermsSelectAll").dxCheckBox({
            text: t("rbac.rolePerms.selectAll"),
            value: false,
            onValueChanged(e) {
                if (state.suppressCheckboxEvents || !e.event) {
                    return;
                }

                const ids = allPermissions(state.modules).map((p) => p.permissionId);
                const wantGrant = !!e.value;

                if (wantGrant) {
                    const alreadyAll = ids.length > 0 && ids.every((id) => state.selected.has(id));
                    if (alreadyAll) {
                        return;
                    }

                    state.suppressCheckboxEvents = true;
                    try {
                        e.component.option("value", false);
                    } finally {
                        state.suppressCheckboxEvents = false;
                    }

                    confirmSelectAllPermissions().done((yes) => {
                        if (yes) {
                            setIds(ids, true);
                        } else {
                            refreshGroupStates();
                        }
                    });
                    return;
                }

                setIds(ids, false);
            }
        }).dxCheckBox("instance");

        const $accHost = $("#rolePermsAccordionActions").empty();
        if ($accHost.length) {
            $("<button />")
                .attr("type", "button")
                .addClass("pms-role-perms-accordion-btn")
                .text(t("rbac.rolePerms.expandAll"))
                .on("click", () => setAllModulesCollapsed(false))
                .appendTo($accHost);

            $("<button />")
                .attr("type", "button")
                .addClass("pms-role-perms-accordion-btn")
                .text(t("rbac.rolePerms.collapseAll"))
                .on("click", () => setAllModulesCollapsed(true))
                .appendTo($accHost);
        }
    }

    $(function () {
        loc.init();
        if (!api.requireToken()) {
            return;
        }

        window.Zaaer.PmsAdminShell.init();
        $("#pageTitle").empty();

        $("[data-i18n]").each(function () {
            const key = $(this).attr("data-i18n");
            $(this).text(t(key));
        });

        initToolbar();

        const roleId = Number(new URLSearchParams(window.location.search).get("roleId"));
        if (!roleId) {
            DevExpress.ui.notify(t("rbac.rolePerms.missingRole"), "error", 4000);
            window.setTimeout(() => {
                window.location.href = "/roles.html";
            }, 1200);
            return;
        }

        loadMatrix(roleId).catch((err) => {
            if (window.console && console.error) {
                console.error("[role-permissions] failed to load role matrix", err);
            }
            DevExpress.ui.notify(resolveLoadErrorMessage(err), "error", 5000);
            window.setTimeout(() => {
                window.location.href = "/roles.html";
            }, 1200);
        });
    });
})(window, jQuery);
