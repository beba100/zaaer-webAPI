(function (window) {
    "use strict";

    if (!window.DevExpress || typeof window.DevExpress.config !== "function") {
        return;
    }

    try {
        const xhr = new XMLHttpRequest();
        xhr.open("GET", "/api/config/devextreme-license", false);
        xhr.setRequestHeader("Accept", "application/json");
        xhr.send();

        if (xhr.status < 200 || xhr.status >= 300) {
            return;
        }

        const payload = JSON.parse(xhr.responseText || "{}");
        const licenseKey = payload.licenseKey || payload.LicenseKey;
        if (licenseKey) {
            window.DevExpress.config({ licenseKey });
        }
    } catch (error) {
        // DevExtreme will show its own licensing warning if the key cannot be loaded.
        window.console && window.console.warn && window.console.warn("DevExtreme license key was not loaded.", error);
    }
})(window);
