(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    function t(key) {
        return loc.t(key);
    }

    function unwrap(res) {
        if (res && res.data !== undefined) return res.data;
        return res;
    }

    const SG = window.Zaaer && window.Zaaer.SaveGuard;
    const zatcaPageActionGuard = SG ? SG.create() : null;

    function runIntegrationPopupSave(guard, saveButton, $popupHost, saveWork) {
        const work = () =>
            Promise.resolve(saveWork()).then((result) => {
                if (SG) {
                    SG.closePopupThenRun($popupHost);
                } else {
                    $popupHost.dxPopup("instance").hide();
                }
                return result;
            });

        if (!SG || !guard) {
            return work();
        }

        const ran = SG.run(guard, work, { button: saveButton });
        return ran === false ? false : ran;
    }

    function runZatcaPageAction(work) {
        if (!SG || !zatcaPageActionGuard) {
            return Promise.resolve(work());
        }
        const ran = SG.run(zatcaPageActionGuard, work);
        return ran === false ? false : ran;
    }

    function normalizeZatcaEnv(env) {
        const v = String(env || "sandbox").trim().toLowerCase();
        if (v === "simulation" || v === "sim") return "simulation";
        if (v === "production" || v === "prod" || v === "core") return "production";
        return "sandbox";
    }

    function envLabel(env) {
        const v = normalizeZatcaEnv(env);
        if (v === "simulation") return t("integrations.zatca.envSimulation");
        if (v === "production") return t("integrations.zatca.envProduction");
        return t("integrations.zatca.envSandbox");
    }

    function computeZatcaSetupState(settings, device) {
        const env = normalizeZatcaEnv(device?.apiEnvironment || settings?.apiEnvironment);
        const active = settings && settings.isActive !== false;
        const hasSettings = !!(settings && settings.taxNumber && settings.companyName);
        const status = String(device?.deviceStatus || "not_onboarded").toLowerCase();
        const needsProduction = env === "simulation" || env === "production";

        if (!active || !hasSettings) {
            return { step: 1, nextAction: "edit", env, alert: null };
        }
        if (!device?.hasComplianceCsid || device?.canDecryptPrivateKey === false) {
            return {
                step: 2,
                nextAction: "onboard",
                env,
                alert: device?.canDecryptPrivateKey === false ? "decrypt" : null
            };
        }
        if (status !== "compliance_tests_passed" && status !== "production_active") {
            return { step: 3, nextAction: "compliance", env, alert: null };
        }
        if (needsProduction && !device?.hasProductionCsid) {
            return { step: 4, nextAction: "production", env, alert: null };
        }
        let alert = null;
        if (!device?.usesDurablePrivateKey) {
            alert = "legacyKey";
        }
        if (device?.hasProductionCsid && !device?.usesDurablePrivateKey && status === "production_active") {
            alert = "keyMismatch";
        }
        return { step: 5, nextAction: "ready", env, alert };
    }

    function setupNextMessage(state) {
        if (!state) {
            return "";
        }
        if (state.nextAction === "edit") {
            return t("integrations.zatca.setupNextEdit");
        }
        if (state.nextAction === "onboard") {
            return t("integrations.zatca.setupNextOtp");
        }
        if (state.nextAction === "compliance") {
            return t("integrations.zatca.setupNextCompliance");
        }
        if (state.nextAction === "production") {
            return t("integrations.zatca.setupNextProduction");
        }
        return t("integrations.zatca.setupReady");
    }

    function setupAlertMessage(state) {
        if (!state || !state.alert) {
            return null;
        }
        if (state.alert === "decrypt") {
            return t("integrations.zatca.alertDecrypt");
        }
        if (state.alert === "keyMismatch") {
            return t("integrations.zatca.alertKeyMismatch");
        }
        if (state.alert === "legacyKey") {
            return t("integrations.zatca.alertLegacyKey");
        }
        return null;
    }

    function renderSetupWizard(settings, device) {
        const $host = $("#zatcaSetupWizard");
        if (!$host.length) {
            return;
        }

        const state = computeZatcaSetupState(settings, device);
        window.__zatcaSetupState = state;
        const env = state.env;
        const needsProduction = env === "simulation" || env === "production";
        const steps = [
            { n: 1, title: t("integrations.zatca.setupStep1"), desc: t("integrations.zatca.setupStep1Desc") },
            { n: 2, title: t("integrations.zatca.setupStep2"), desc: t("integrations.zatca.setupStep2Desc") },
            { n: 3, title: t("integrations.zatca.setupStep3"), desc: t("integrations.zatca.setupStep3Desc") },
            {
                n: 4,
                title: t("integrations.zatca.setupStep4"),
                desc: needsProduction
                    ? t("integrations.zatca.setupStep4Desc")
                    : t("integrations.zatca.setupStep4Skip")
            },
            { n: 5, title: t("integrations.zatca.setupStep5"), desc: t("integrations.zatca.setupStep5Desc") }
        ];

        $host.empty().removeAttr("hidden");
        $host.append($("<h3/>", { class: "pms-zatca-setup-wizard-title", text: t("integrations.zatca.setupWizardTitle") }));
        const $ul = $("<ul/>", { class: "pms-zatca-setup-steps" });
        steps.forEach((s) => {
            let cls = "pms-zatca-setup-step";
            if (s.n < state.step) {
                cls += " pms-zatca-setup-step--done";
            } else if (s.n === state.step) {
                cls += " pms-zatca-setup-step--current";
            } else {
                cls += " pms-zatca-setup-step--pending";
            }
            $ul.append(
                $("<li/>", { class: cls }).append(
                    $("<strong/>", { text: s.title }),
                    document.createTextNode(s.desc)
                )
            );
        });
        $host.append($ul);
        $host.append(
            $("<p/>", { class: "pms-integration-hint", text: `${envLabel(env)} — ${setupNextMessage(state)}` })
        );
    }

    function renderSetupAlert(settings, device) {
        const $host = $("#zatcaSetupAlert");
        if (!$host.length) {
            return;
        }

        const state = computeZatcaSetupState(settings, device);
        const msg = setupAlertMessage(state);
        if (!msg) {
            if (state.step === 5 && state.nextAction === "ready") {
                $host
                    .removeAttr("hidden")
                    .attr("class", "pms-zatca-setup-alert pms-zatca-setup-alert--ok")
                    .text(t("integrations.zatca.setupReady"));
                return;
            }
            $host.attr("hidden", "hidden").empty();
            return;
        }

        $host
            .removeAttr("hidden")
            .attr("class", "pms-zatca-setup-alert pms-zatca-setup-alert--warn")
            .text(msg);
    }

    function highlightNextActionButton(state) {
        const map = {
            edit: "#btnZatcaEdit",
            onboard: "#btnZatcaOnboard",
            compliance: "#btnZatcaCompliance",
            production: "#btnZatcaProduction"
        };
        Object.values(map).forEach((sel) => {
            $(sel).closest(".dx-button").parent().removeClass("pms-zatca-action-highlight");
        });
        if (!state || state.nextAction === "ready") {
            return;
        }
        const sel = map[state.nextAction];
        if (sel) {
            $(sel).closest(".dx-button").parent().addClass("pms-zatca-action-highlight");
        }
    }

    function renderMasterKeyBanner(device) {
        const $host = $("#zatcaMasterKeyBanner");
        if (!$host.length) {
            return;
        }
        if (device?.isMasterKeyConfigured) {
            $host.attr("hidden", "hidden").empty();
            return;
        }
        $host
            .removeAttr("hidden")
            .html(
                `<strong>${t("integrations.zatca.masterKeyServerTitle")}</strong><br>${t("integrations.zatca.masterKeyServerHint")}`
            );
    }

    function deviceStatusBadge(status) {
        const s = String(status || "not_onboarded").toLowerCase();
        let cls = "pms-integration-status-badge--inactive";
        if (s === "compliance_active" || s === "compliance_tests_passed" || s === "production_active") {
            cls = "pms-integration-status-badge--active";
        }
        const labelKey = `integrations.zatca.deviceStatus.${s}`;
        const label = t(labelKey);
        const text = label === labelKey ? status : label;
        return `<span class="pms-integration-status-badge ${cls}">${text}</span>`;
    }

    function pmsPopupOptions(title) {
        return {
            title: title,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" }
        };
    }

    function renderDetails(settings, device) {
        const active = settings && settings.isActive !== false;
        const rows = [
            {
                label: t("integrations.ntmp.status"),
                html: `<span class="pms-integration-status-badge ${active ? "pms-integration-status-badge--active" : "pms-integration-status-badge--inactive"}">${active ? t("integrations.ntmp.active") : t("integrations.ntmp.inactive")}</span>`
            },
            { label: t("integrations.zatca.companyName"), value: settings?.companyName || "—" },
            { label: t("integrations.zatca.taxNumber"), value: settings?.taxNumber || "—" },
            { label: t("integrations.zatca.groupTaxId"), value: settings?.groupTaxId || "—" },
            { label: t("integrations.zatca.crNumber"), value: settings?.corporateRegistrationNumber || "—" },
            { label: t("integrations.zatca.deviceCommonName"), value: settings?.deviceCommonName || "—" },
            { label: t("integrations.zatca.apiEnvironment"), value: envLabel(device?.apiEnvironment || settings?.apiEnvironment) },
            {
                label: t("integrations.zatca.deviceStatus"),
                html: deviceStatusBadge(device?.deviceStatus)
            },
            { label: t("integrations.zatca.deviceUuid"), value: device?.deviceUuid || settings?.deviceUuid || "—" },
            {
                label: t("integrations.zatca.complianceCsid"),
                value: device?.hasComplianceCsid ? t("integrations.zatca.configured") : t("integrations.zatca.notConfigured")
            },
            {
                label: t("integrations.zatca.productionCsid"),
                value: device?.hasProductionCsid ? t("integrations.zatca.configured") : t("integrations.zatca.notConfigured")
            },
            {
                label: t("integrations.zatca.privateKeyHealth"),
                html:
                    device?.canDecryptPrivateKey === false
                        ? `<span class="pms-integration-status-badge pms-integration-status-badge--inactive">${t("integrations.zatca.privateKeyNeedsReregister")}</span>`
                        : device?.canDecryptPrivateKey === true
                          ? `<span class="pms-integration-status-badge pms-integration-status-badge--active">${device?.usesDurablePrivateKey ? t("integrations.zatca.privateKeyDurable") : t("integrations.zatca.privateKeyLegacy")}</span>`
                          : `<span class="pms-integration-kv-value">—</span>`
            },
            { label: t("integrations.zatca.address"), value: settings?.address || "—" }
        ];
        const $host = $("#zatcaDetailsView").empty();
        rows.forEach((r) => {
            const $cell = $("<div/>");
            $cell.append($("<span/>", { class: "pms-integration-kv-label", text: r.label }));
            if (r.html) {
                $cell.append($("<div/>", { class: "pms-integration-kv-value" }).html(r.html));
            } else {
                $cell.append($("<div/>", { class: "pms-integration-kv-value", text: r.value }));
            }
            $host.append($cell);
        });
    }

    function openEditPopup(current) {
        const $host = $("#zatcaEditPopupHost").empty();
        const $popup = $("<div/>").appendTo($host);
        let formInstance;
        let saveButton;
        const editSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            ...pmsPopupOptions(t("integrations.ntmp.edit")),
            visible: true,
            contentTemplate() {
                const $form = $("<div/>");
                $form.dxForm({
                    formData: {
                        isActive: current?.isActive !== false,
                        companyName: current?.companyName || "",
                        taxNumber: current?.taxNumber || "",
                        groupTaxId: current?.groupTaxId || "",
                        corporateRegistrationNumber: current?.corporateRegistrationNumber || "",
                        deviceCommonName: current?.deviceCommonName || "",
                        environment: current?.environment || "",
                        apiEnvironment: normalizeZatcaEnv(current?.apiEnvironment),
                        deviceUuid: current?.deviceUuid || "",
                        address: current?.address || "",
                        streetName: current?.streetName || "",
                        buildingNumber: current?.buildingNumber || "",
                        city: current?.city || "",
                        citySubdivisionName: current?.citySubdivisionName || "",
                        postalZone: current?.postalZone || ""
                    },
                    labelLocation: "top",
                    colCount: 2,
                    items: [
                        {
                            dataField: "isActive",
                            colSpan: 2,
                            editorType: "dxSwitch",
                            label: { text: t("integrations.ntmp.status") }
                        },
                        { dataField: "companyName", colSpan: 2, label: { text: t("integrations.zatca.companyName") }, isRequired: true },
                        { dataField: "taxNumber", label: { text: t("integrations.zatca.taxNumber") }, isRequired: true },
                        { dataField: "groupTaxId", label: { text: t("integrations.zatca.groupTaxId") } },
                        { dataField: "corporateRegistrationNumber", label: { text: t("integrations.zatca.crNumber") } },
                        {
                            dataField: "deviceCommonName",
                            colSpan: 2,
                            label: { text: t("integrations.zatca.deviceCommonName") },
                            editorOptions: { maxLength: 200 },
                            helpText: t("integrations.zatca.deviceCommonNameHint")
                        },
                        {
                            dataField: "apiEnvironment",
                            label: { text: t("integrations.zatca.apiEnvironment") },
                            editorType: "dxSelectBox",
                            editorOptions: {
                                items: [
                                    { id: "sandbox", name: t("integrations.zatca.envSandbox") },
                                    { id: "simulation", name: t("integrations.zatca.envSimulation") },
                                    { id: "production", name: t("integrations.zatca.envProduction") }
                                ],
                                displayExpr: "name",
                                valueExpr: "id"
                            }
                        },
                        { dataField: "deviceUuid", label: { text: t("integrations.zatca.deviceUuid") }, editorOptions: { readOnly: !!current?.deviceUuid } },
                        { dataField: "address", colSpan: 2, label: { text: t("integrations.zatca.address") } },
                        { dataField: "streetName", label: { text: t("integrations.zatca.streetName") } },
                        { dataField: "buildingNumber", label: { text: t("integrations.zatca.buildingNumber") } },
                        { dataField: "citySubdivisionName", label: { text: t("integrations.zatca.citySubdivision") } },
                        { dataField: "city", label: { text: t("integrations.zatca.city") } },
                        { dataField: "postalZone", label: { text: t("integrations.zatca.postalZone") } }
                    ],
                    onInitialized(e) {
                        formInstance = e.component;
                    }
                });
                return $form;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.cancel"),
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("integrations.common.save"),
                        type: "default",
                        onInitialized(e) {
                            saveButton = e.component;
                        },
                        onClick() {
                            const ran = runIntegrationPopupSave(editSaveGuard, saveButton, $popup, () => {
                                const fd = formInstance.option("formData");
                                const prevEnv = normalizeZatcaEnv(current?.apiEnvironment);
                                const nextEnv = normalizeZatcaEnv(fd.apiEnvironment);
                                return api.put("/api/v1/pms/integrations/zatca", fd).then((res) => {
                                    const data = unwrap(res);
                                    window.__zatcaSettings = data;
                                    DevExpress.ui.notify(t("integrations.ntmp.saved"), "success", 2500);
                                    if (prevEnv !== nextEnv) {
                                        DevExpress.ui.notify(t("integrations.zatca.envChangeHint"), "warning", 6000);
                                    }
                                    if (SG) {
                                        SG.scheduleBackground(loadAll);
                                    } else {
                                        return loadAll();
                                    }
                                    return data;
                                });
                            });
                            if (ran === false) {
                                return;
                            }
                            if (ran && typeof ran.catch === "function") {
                                ran.catch((err) => {
                                    const msg =
                                        (err &&
                                            err.responseJSON &&
                                            (err.responseJSON.message || err.responseJSON.error)) ||
                                        err?.message ||
                                        t("common.error");
                                    DevExpress.ui.notify(msg, "error", 4000);
                                });
                            }
                        }
                    }
                }
            ]
        });
    }

    function openOnboardPopup() {
        const $host = $("<div/>").appendTo("body");
        let formInstance;
        let submitButton;
        const onboardSaveGuard = SG ? SG.create() : null;

        $host.dxPopup({
            ...pmsPopupOptions(t("integrations.zatca.onboardTitle")),
            visible: true,
            container: "body",
            onHidden() {
                $host.remove();
            },
            contentTemplate() {
                const $form = $("<div/>");
                $form.dxForm({
                    formData: {
                        otp: "",
                        commonName: window.__zatcaSettings?.deviceCommonName || "",
                        apiEnvironment: normalizeZatcaEnv(window.__zatcaDevice?.apiEnvironment || window.__zatcaSettings?.apiEnvironment)
                    },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        {
                            itemType: "simple",
                            template() {
                                return $("<p/>", {
                                    class: "pms-integration-hint",
                                    text: t("integrations.zatca.onboardHint")
                                });
                            }
                        },
                        {
                            dataField: "otp",
                            label: { text: t("integrations.zatca.otp") },
                            isRequired: false,
                            editorOptions: { mode: "password" }
                        },
                        {
                            dataField: "commonName",
                            label: { text: t("integrations.zatca.deviceCommonName") },
                            editorOptions: { maxLength: 64 },
                            helpText: t("integrations.zatca.onboardCommonNameHint")
                        },
                        {
                            dataField: "apiEnvironment",
                            label: { text: t("integrations.zatca.apiEnvironment") },
                            editorType: "dxSelectBox",
                            editorOptions: {
                                items: [
                                    { id: "sandbox", name: t("integrations.zatca.envSandbox") },
                                    { id: "simulation", name: t("integrations.zatca.envSimulation") },
                                    { id: "production", name: t("integrations.zatca.envProduction") }
                                ],
                                displayExpr: "name",
                                valueExpr: "id"
                            }
                        }
                    ],
                    onInitialized(e) {
                        formInstance = e.component;
                    }
                });
                return $form;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.cancel"),
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("integrations.zatca.onboardSubmit"),
                        type: "default",
                        onInitialized(e) {
                            submitButton = e.component;
                        },
                        onClick() {
                            const fd = formInstance.option("formData");
                            const env = normalizeZatcaEnv(fd.apiEnvironment);
                            if (!fd.otp && env === "simulation") {
                                DevExpress.ui.notify(t("integrations.zatca.simulationOtpDeferred"), "info", 5000);
                                return;
                            }
                            if (!fd.otp) {
                                DevExpress.ui.notify(t("integrations.zatca.otpRequired"), "warning", 3000);
                                return;
                            }
                            const ran = runIntegrationPopupSave(onboardSaveGuard, submitButton, $host, () =>
                                api.post("/api/v1/pms/integrations/zatca/onboard", fd).then((res) => {
                                    const body = res && res.data !== undefined ? res : { data: res };
                                    const result = unwrap(body);
                                    const msg =
                                        body.message || result?.message || t("integrations.zatca.onboardSuccess");
                                    DevExpress.ui.notify(msg, "success", 4000);
                                    if (SG) {
                                        SG.scheduleBackground(loadAll);
                                    } else {
                                        return loadAll();
                                    }
                                    return result;
                                })
                            );
                            if (ran === false) {
                                return;
                            }
                            if (ran && typeof ran.catch === "function") {
                                ran.catch((err) => {
                                    const msg =
                                        err?.responseJSON?.message ||
                                        err?.responseJSON?.data?.message ||
                                        t("integrations.zatca.onboardFailed");
                                    DevExpress.ui.notify(msg, "error", 5000);
                                });
                            }
                        }
                    }
                }
            ]
        });
    }

    function complianceDocLabel(type) {
        const key = `integrations.zatca.complianceDoc.${type}`;
        const label = t(key);
        return label === key ? type : label;
    }

    function openComplianceResultsPopup(result) {
        const $host = $("#zatcaCompliancePopupHost").empty();
        const $popup = $("<div/>").appendTo($host);
        const items = (result && result.items) || [];

        $popup.dxPopup({
            ...pmsPopupOptions(t("integrations.zatca.complianceResultsTitle")),
            visible: true,
            hideOnOutsideClick: true,
            contentTemplate() {
                const $wrap = $("<div/>", { class: "pms-integration-compliance-results" });
                items.forEach((item) => {
                    const ok = !!item.success;
                    const $row = $("<div/>", { class: "pms-integration-compliance-row" });
                    $row.append(
                        $("<span/>", {
                            class: ok
                                ? "dx-icon dx-icon-check pms-integration-compliance-icon--ok"
                                : "dx-icon dx-icon-close pms-integration-compliance-icon--fail"
                        })
                    );
                    $row.append(
                        $("<span/>", { text: complianceDocLabel(item.documentType) })
                    );
                    if (!ok && item.errorMessage) {
                        $row.append($("<p/>", { class: "pms-integration-hint", text: item.errorMessage }));
                    }
                    $wrap.append($row);
                });
                if (result && result.message) {
                    $wrap.prepend($("<p/>", { class: "pms-integration-hint", text: result.message }));
                }
                return $wrap;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.close"),
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                }
            ]
        });
    }

    function runComplianceTests() {
        const env = normalizeZatcaEnv(
            window.__zatcaDevice?.apiEnvironment || window.__zatcaSettings?.apiEnvironment
        );
        DevExpress.ui.dialog.confirm(
            t("integrations.zatca.complianceConfirm").replace("{env}", envLabel(env)),
            t("integrations.zatca.runCompliance")
        ).done((ok) => {
            if (!ok) return;
            const ran = runZatcaPageAction(() =>
                api.post("/api/v1/pms/integrations/zatca/run-compliance", { apiEnvironment: env }).then(
                    (res) => {
                        const body = res && res.data !== undefined ? res : { data: res };
                        const result = unwrap(body);
                        const msg = body.message || result?.message || t("integrations.zatca.complianceSuccess");
                        DevExpress.ui.notify(msg, result?.allPassed ? "success" : "warning", 5000);
                        openComplianceResultsPopup(result);
                        return loadAll();
                    },
                    (err) => {
                        const payload = err?.responseJSON?.data || err?.responseJSON;
                        const result = payload?.data || payload;
                        if (result && result.items) {
                            openComplianceResultsPopup(result);
                        }
                        const msg =
                            err?.responseJSON?.message ||
                            payload?.message ||
                            t("integrations.zatca.complianceFailed");
                        DevExpress.ui.notify(msg, "error", 6000);
                        return loadAll();
                    }
                )
            );
            if (ran === false) {
                return;
            }
            if (ran && typeof ran.catch === "function") {
                ran.catch(() => {
                    /* errors handled in request handlers */
                });
            }
        });
    }

    function requestProductionCsid() {
        DevExpress.ui.dialog.confirm(
            t("integrations.zatca.productionConfirm"),
            t("integrations.zatca.productionCsid")
        ).done((ok) => {
            if (!ok) return;
            const ran = runZatcaPageAction(() =>
                api.post("/api/v1/pms/integrations/zatca/production-csid", {}).then(
                    (res) => {
                        const body = res && res.data !== undefined ? res : { data: res };
                        const msg = body.message || unwrap(body)?.message || t("integrations.zatca.productionSuccess");
                        DevExpress.ui.notify(msg, "success", 4000);
                        return loadAll();
                    },
                    (err) => {
                        const msg =
                            err?.responseJSON?.message ||
                            err?.responseJSON?.data?.message ||
                            t("integrations.zatca.productionFailed");
                        DevExpress.ui.notify(msg, "error", 5000);
                        throw err;
                    }
                )
            );
            if (ran === false) {
                return;
            }
            if (ran && typeof ran.catch === "function") {
                ran.catch(() => {
                    /* errors handled in request handlers */
                });
            }
        });
    }

    function loadAll() {
        return Promise.all([
            api.get("/api/v1/pms/integrations/zatca").then((res) => {
                window.__zatcaSettings = unwrap(res);
            }),
            api.get("/api/v1/pms/integrations/zatca/device").then((res) => {
                window.__zatcaDevice = unwrap(res);
            })
        ]).then(() => {
            renderMasterKeyBanner(window.__zatcaDevice);
            renderSetupWizard(window.__zatcaSettings, window.__zatcaDevice);
            renderSetupAlert(window.__zatcaSettings, window.__zatcaDevice);
            renderDetails(window.__zatcaSettings, window.__zatcaDevice);
            highlightNextActionButton(window.__zatcaSetupState);
            const device = window.__zatcaDevice;
            const prodBtn = $("#btnZatcaProduction").dxButton("instance");
            const complianceBtn = $("#btnZatcaCompliance").dxButton("instance");
            const status = String(device?.deviceStatus || "").toLowerCase();
            if (prodBtn) {
                prodBtn.option(
                    "disabled",
                    !device?.hasComplianceCsid
                        || device?.hasProductionCsid
                        || status !== "compliance_tests_passed"
                );
            }
            if (complianceBtn) {
                complianceBtn.option("disabled", !device?.hasComplianceCsid);
            }
        });
    }

    $(function () {
        loc.init();
        window.Zaaer.PmsAdminShell.init({ navKey: "nav-integrations-zatca", onRefresh: loadAll });

        $("#btnZatcaEdit").dxButton({
            text: t("integrations.ntmp.edit"),
            type: "default",
            stylingMode: "contained",
            onClick() {
                openEditPopup(window.__zatcaSettings);
            }
        });

        $("#btnZatcaCompliance").dxButton({
            text: t("integrations.zatca.runCompliance"),
            type: "default",
            stylingMode: "contained",
            disabled: true,
            onClick: runComplianceTests
        });

        $("#btnZatcaOnboard").dxButton({
            text: t("integrations.zatca.onboardDevice"),
            type: "default",
            stylingMode: "outlined",
            onClick() {
                if (!window.__zatcaSettings?.taxNumber) {
                    DevExpress.ui.notify(t("integrations.zatca.saveSettingsFirst"), "warning", 3500);
                    return;
                }
                openOnboardPopup();
            }
        });

        $("#btnZatcaProduction").dxButton({
            text: t("integrations.zatca.requestProduction"),
            type: "normal",
            stylingMode: "outlined",
            disabled: true,
            onClick: requestProductionCsid
        });

        loadAll();
    });
})(window, jQuery);
