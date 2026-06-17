/**
 * Prevents duplicate form saves and supports immediate popup close with background refresh.
 * @namespace window.Zaaer.SaveGuard
 */
(function (window) {
    "use strict";

    function createSaveGuard() {
        let inFlight = false;
        return {
            get isInFlight() {
                return inFlight;
            },
            begin() {
                if (inFlight) {
                    return false;
                }
                inFlight = true;
                return true;
            },
            end() {
                inFlight = false;
            },
            reset() {
                inFlight = false;
            }
        };
    }

    function setButtonDisabled(button, disabled) {
        if (!button) {
            return;
        }
        try {
            if (typeof button.option === "function") {
                button.option("disabled", !!disabled);
            }
        } catch {
            /* widget disposed */
        }
    }

    function finalizeAsync(chain, onComplete) {
        const wrapped = Promise.resolve(chain);
        if (typeof wrapped.finally === "function") {
            return wrapped.finally(onComplete);
        }

        return wrapped.then(
            (value) => {
                onComplete();
                return value;
            },
            (err) => {
                onComplete();
                throw err;
            }
        );
    }

    /**
     * Runs guarded async work. Returns false if a save is already in flight.
     * @param {ReturnType<createSaveGuard>} guard
     * @param {function(): *} work
     * @param {{ button?: object, onStart?: function, onEnd?: function }} [opts]
     * @returns {false|Promise<*>}
     */
    function runGuardedSave(guard, work, opts) {
        opts = opts || {};
        if (!guard || !guard.begin()) {
            return false;
        }

        const onStart = opts.onStart || (opts.button ? () => setButtonDisabled(opts.button, true) : null);
        const onEnd = opts.onEnd || (opts.button ? () => setButtonDisabled(opts.button, false) : null);

        if (typeof onStart === "function") {
            onStart();
        }

        let result;
        try {
            result = work();
        } catch (err) {
            guard.end();
            if (typeof onEnd === "function") {
                onEnd();
            }
            throw err;
        }

        const chain = result && typeof result.then === "function" ? result : Promise.resolve(result);
        return finalizeAsync(chain, () => {
            guard.end();
            if (typeof onEnd === "function") {
                onEnd();
            }
        });
    }

    function hidePopupHost($host) {
        try {
            if ($host && $host.length) {
                const popup = $host.dxPopup("instance");
                if (popup) {
                    popup.hide();
                }
            }
        } catch {
            /* popup already disposed */
        }
    }

    /**
     * Fire-and-forget background work after popup close; logs and notifies on failure only.
     * @param {function(): *} [work]
     */
    function scheduleBackground(work) {
        if (typeof work !== "function") {
            return;
        }

        let chain;
        try {
            chain = work();
        } catch (err) {
            console.error("SaveGuard background sync failed", err);
            return;
        }

        const p = chain && typeof chain.then === "function" ? chain : Promise.resolve(chain);
        p.catch((err) => {
            console.error("SaveGuard background sync failed", err);
            if (window.DevExpress && DevExpress.ui && typeof DevExpress.ui.notify === "function") {
                const loc = window.Zaaer && window.Zaaer.LocalizationService;
                const msg =
                    (err && err.message) ||
                    (loc && typeof loc.t === "function" ? loc.t("common.error") : "Error");
                DevExpress.ui.notify(msg, "warning", 3600);
            }
        });
    }

    /**
     * Closes popup immediately, then runs optional background refresh.
     * @param {jQuery} $host
     * @param {function(): *} [backgroundWork]
     */
    function closePopupThenRun($host, backgroundWork) {
        hidePopupHost($host);
        scheduleBackground(backgroundWork);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.SaveGuard = {
        create: createSaveGuard,
        createSaveGuard: createSaveGuard,
        run: runGuardedSave,
        runGuardedSave: runGuardedSave,
        hidePopup: hidePopupHost,
        hidePopupHost: hidePopupHost,
        scheduleBackground: scheduleBackground,
        closePopupThenRun: closePopupThenRun,
        setButtonDisabled: setButtonDisabled
    };
})(window);
