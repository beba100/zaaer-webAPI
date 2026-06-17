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

    function normalizeApiEnvironment(env) {
        const v = String(env || "production").trim().toLowerCase();
        if (v === "dev" || v === "development") return "dev";
        if (v === "staging" || v === "stage" || v === "stg") return "staging";
        return "production";
    }

    function envLabel(env) {
        const v = normalizeApiEnvironment(env);
        if (v === "dev") return t("integrations.ntmp.envDev");
        if (v === "staging") return t("integrations.ntmp.envStaging");
        return t("integrations.ntmp.envProduction");
    }

    function resetNavTreeAfterPopup() {
        if (window.Zaaer.PmsAdminShell && window.Zaaer.PmsAdminShell.resetNavTreeSearch) {
            window.Zaaer.PmsAdminShell.resetNavTreeSearch();
        }
    }

    function renderDetails(data) {
        const $host = $("#ntmpDetailsView").empty();
        const active = data && data.isActive;
        const rows = [
            {
                label: t("integrations.ntmp.status"),
                html: `<span class="pms-integration-status-badge ${active ? "pms-integration-status-badge--active" : "pms-integration-status-badge--inactive"}">${active ? t("integrations.ntmp.active") : t("integrations.ntmp.inactive")}</span>`
            },
            { label: t("integrations.ntmp.apiKey"), value: data?.gatewayApiKey || "—" },
            { label: t("integrations.ntmp.userName"), value: data?.userName || "—" },
            { label: t("integrations.ntmp.password"), value: data?.hasPassword ? "******" : "—" },
            { label: t("integrations.ntmp.environment"), value: envLabel(data?.apiEnvironment) },
            { label: t("integrations.ntmp.channel"), value: data?.channelName || "Aleairy PMS" }
        ];

        if (data && (data.hotelCode || data.hotelZaaerId)) {
            rows.push({
                label: t("integrations.ntmp.hotelScope"),
                value: [data.hotelCode, data.hotelZaaerId ? `(Zaaer: ${data.hotelZaaerId})` : null]
                    .filter(Boolean)
                    .join(" ")
            });
        }

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

    function openEditPopup(current) {
        resetNavTreeAfterPopup();

        const $host = $("<div/>").appendTo("body");
        let formInstance;
        let saveButton;
        const editSaveGuard = SG ? SG.create() : null;

        $host.dxPopup({
            ...pmsPopupOptions(t("integrations.ntmp.edit")),
            visible: true,
            container: "body",
            onShown: resetNavTreeAfterPopup,
            onHidden() {
                resetNavTreeAfterPopup();
                $host.remove();
            },
            contentTemplate() {
                const $form = $("<div/>");
                $form.dxForm({
                    formData: {
                        isActive: current?.isActive !== false,
                        gatewayApiKey: current?.gatewayApiKey || "",
                        userName: current?.userName || "",
                        password: "",
                        apiEnvironment: normalizeApiEnvironment(current?.apiEnvironment)
                    },
                    labelLocation: "top",
                    colCount: 1,
                    items: [
                        { dataField: "isActive", editorType: "dxSwitch", label: { text: t("integrations.ntmp.status") } },
                        { dataField: "gatewayApiKey", label: { text: t("integrations.ntmp.apiKey") } },
                        { dataField: "userName", label: { text: t("integrations.ntmp.userName") } },
                        {
                            dataField: "password",
                            editorType: "dxTextBox",
                            editorOptions: { mode: "password" },
                            label: { text: t("integrations.ntmp.password") },
                            helpText: t("integrations.ntmp.passwordHint")
                        },
                        {
                            dataField: "apiEnvironment",
                            editorType: "dxSelectBox",
                            editorOptions: {
                                items: [
                                    { value: "production", text: t("integrations.ntmp.envProduction") },
                                    { value: "staging", text: t("integrations.ntmp.envStaging") },
                                    { value: "dev", text: t("integrations.ntmp.envDev") }
                                ],
                                valueExpr: "value",
                                displayExpr: "text"
                            },
                            label: { text: t("integrations.ntmp.environment") }
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
                        text: t("integrations.common.save"),
                        type: "default",
                        onInitialized(e) {
                            saveButton = e.component;
                        },
                        onClick() {
                            const fd = Object.assign({}, formInstance.option("formData"));
                            if (!fd.password) {
                                delete fd.password;
                            }
                            const ran = runIntegrationPopupSave(editSaveGuard, saveButton, $host, () =>
                                api.put("/api/v1/pms/integrations/ntmp", fd).then((res) => {
                                    const data = unwrap(res);
                                    window.__ntmpSettings = data;
                                    renderDetails(data);
                                    DevExpress.ui.notify(t("integrations.ntmp.saved"), "success", 2500);
                                    return data;
                                })
                            );
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
                                        "Error";
                                    DevExpress.ui.notify(msg, "error", 4000);
                                });
                            }
                        }
                    }
                }
            ]
        });
    }

    function loadSettings() {
        return api.get("/api/v1/pms/integrations/ntmp").then((res) => {
            const data = unwrap(res);
            window.__ntmpSettings = data;
            renderDetails(data);
            return data;
        });
    }

    $(function () {
        loc.init();
        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-integrations-ntmp",
            onRefresh: loadSettings
        });

        $("#btnNtmpEdit").dxButton({
            text: t("integrations.ntmp.edit"),
            type: "default",
            stylingMode: "contained",
            onClick() {
                openEditPopup(window.__ntmpSettings);
            }
        });

        $("#btnNtmpTest").dxButton({
            text: t("integrations.ntmp.test"),
            stylingMode: "outlined",
            onClick() {
                api.post("/api/v1/pms/integrations/ntmp/test-connection", {}).then((res) => {
                    const payload = unwrap(res);
                    const data = payload && payload.data !== undefined ? payload.data : payload;
                    const ok = !!(res && res.success) || !!(data && data.success);
                    const message = (data && data.message) || (ok ? t("integrations.ntmp.testOk") : t("integrations.ntmp.testFail"));
                    DevExpress.ui.notify(message, ok ? "success" : "error", 5000);
                }).catch((err) => {
                    const msg = (err && err.responseJSON && err.responseJSON.message) || err?.message || t("integrations.ntmp.testFail");
                    DevExpress.ui.notify(msg, "error", 5000);
                });
            }
        });

        loadSettings();
    });
})(window, jQuery);
