(function (window, $) {
    "use strict";

    const service = window.Zaaer && window.Zaaer.ResortTicketService;
    const api = window.Zaaer && window.Zaaer.ApiService;

    function ticketStatusLabel(t, status) {
        const key = `resortTickets.ticketStatus.${status}`;
        return t(key) || status || "";
    }

    function blockMessage(t, blockReason) {
        return t(`resortTickets.validate.block.${blockReason}`) || t("common.error");
    }

    function validityLabel(t, status) {
        const key = `resortTickets.validate.validity.${status}`;
        return t(key) || status || "";
    }

    function formatRemainingMinutes(t, minutes) {
        const n = Number(minutes);
        if (!Number.isFinite(n) || n < 0) {
            return "";
        }
        const template = t("resortTickets.validate.remainingMinutes");
        return template.indexOf("{0}") >= 0 ? template.replace("{0}", String(n)) : `${n} min`;
    }

    function buildValidityLine(t, result) {
        const status = result && (result.validityStatus || result.ValidityStatus);
        const validTo = result && (result.validTo || result.ValidTo);
        const remaining = result && (result.remainingMinutes ?? result.RemainingMinutes);
        if (!status) {
            return "";
        }

        const parts = [`${t("resortTickets.validate.validityLabel")}: ${validityLabel(t, status)}`];
        if (status === "pending_activation") {
            return parts.join(" · ");
        }
        if (remaining != null && Number(remaining) >= 0) {
            parts.push(formatRemainingMinutes(t, remaining));
        }
        const until = validTo ? formatDateTime(validTo) : "—";
        parts.push(`${t("resortTickets.orderDetail.validTo")}: ${until}`);
        return parts.join(" · ");
    }

    function formatDateTime(value) {
        if (!value) {
            return "—";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return String(value);
        }
        return d.toLocaleString();
    }

    function vibrate(pattern) {
        if (navigator.vibrate) {
            navigator.vibrate(pattern);
        }
    }

    function playScanFeedback(kind) {
        const audio = window.Zaaer && window.Zaaer.ResortTicketScanAudio;
        if (!audio) {
            return;
        }
        if (kind === "session") {
            audio.playSessionStart();
        } else if (kind === "success") {
            audio.playSuccess();
        } else if (kind === "expired") {
            audio.playExpired();
        } else if (kind === "error") {
            audio.playError();
        }
    }

    function parseValidTo(value) {
        if (!value) {
            return null;
        }
        const d = value instanceof Date ? value : new Date(value);
        return Number.isNaN(d.getTime()) ? null : d;
    }

    function formatCountdown(ms) {
        const totalSec = Math.max(0, Math.ceil(ms / 1000));
        const min = Math.floor(totalSec / 60);
        const sec = totalSec % 60;
        return `${String(min).padStart(2, "0")}:${String(sec).padStart(2, "0")}`;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ResortTicketScannerPanel = {
        mount(host, options) {
            const opts = options || {};
            const t = opts.t || function (k) {
                return k;
            };
            const mode = opts.mode === "gate" ? "gate" : "staff";
            const isGate = mode === "gate";
            const stationCode = String(opts.stationCode || "").trim().toLowerCase();
            const stationLabel = String(opts.stationLabel || "").trim();
            const scanAudio = window.Zaaer && window.Zaaer.ResortTicketScanAudio;

            const state = {
                lookupOnly: false,
                recent: [],
                busy: false,
                lastQr: "",
                lastQrAt: 0,
                scanPausedUntil: 0,
                cameraOn: false,
                sessionTimerId: null,
                sessionExpiresAt: null
            };

            const $host = $(host).empty().addClass(isGate ? "resort-ticket-gate" : "resort-ticket-scanner");
            let qrInput;
            let lookupSwitch;
            let cameraScanner = null;
            const readerId = `resortTicketQrReader_${Date.now()}`;

            const $layout = isGate
                ? $host
                : $("<div/>").addClass("resort-ticket-scanner").appendTo($host);

            const $scanPanel = $("<div/>")
                .addClass(isGate ? "resort-ticket-gate__scan" : "resort-ticket-scanner__scan")
                .appendTo($layout);

            if (isGate && stationLabel) {
                $("<div/>")
                    .addClass("resort-ticket-gate__station-hint")
                    .text(stationLabel)
                    .appendTo($scanPanel);
            }

            if (!isGate) {
                $("<p/>").addClass("resort-ticket-scanner__hint").text(t("resortTickets.scanner.hint")).appendTo($scanPanel);
            }

            const $cameraHost = $("<div/>")
                .addClass(isGate ? "resort-ticket-gate__camera" : "resort-ticket-scanner__camera")
                .appendTo($scanPanel);
            const $reader = $("<div/>").attr("id", readerId).appendTo($cameraHost);
            const $cameraActions = $("<div/>")
                .addClass("resort-ticket-scanner__camera-actions")
                .appendTo($scanPanel);

            const $inputHost = $("<div/>")
                .addClass(isGate ? "resort-ticket-gate__manual" : "")
                .appendTo($scanPanel);
            const $optionsHost = isGate ? $() : $("<div/>").appendTo($scanPanel);
            const $resultHost = $("<div/>")
                .addClass(isGate ? "resort-ticket-gate__result" : "resort-ticket-scanner__result")
                .appendTo($scanPanel);

            const $recentPanel = isGate
                ? $("<div/>").addClass("resort-ticket-gate__recent").appendTo($layout)
                : $("<div/>").addClass("resort-ticket-scanner__recent").appendTo($layout);

            if (!isGate) {
                $("<h3/>")
                    .addClass("resort-ticket-scanner__recent-title")
                    .text(t("resortTickets.scanner.recent"))
                    .appendTo($recentPanel);
            } else {
                $("<div/>")
                    .addClass("resort-ticket-gate__recent-title")
                    .text(t("resortTickets.gate.recentShort"))
                    .appendTo($recentPanel);
            }

            const $recentList = $("<div/>")
                .addClass(isGate ? "resort-ticket-gate__recent-list" : "resort-ticket-scanner__recent-list")
                .appendTo($recentPanel);

            const $gateOverlay = isGate
                ? $("<div/>").addClass("resort-ticket-gate__overlay").attr("hidden", true).appendTo("body")
                : $();

            const $sessionTimer = isGate
                ? $("<div/>")
                      .addClass("resort-ticket-gate__session-timer")
                      .attr("hidden", true)
                      .appendTo($layout)
                : $();

            function clearSessionTimer() {
                if (state.sessionTimerId) {
                    window.clearInterval(state.sessionTimerId);
                    state.sessionTimerId = null;
                }
                state.sessionExpiresAt = null;
                if ($sessionTimer.length) {
                    $sessionTimer.attr("hidden", true).empty();
                }
            }

            function renderSessionTimer(ticket, validTo) {
                if (!isGate || ! $sessionTimer.length) {
                    return;
                }

                const expires = parseValidTo(validTo);
                if (!expires) {
                    clearSessionTimer();
                    return;
                }

                state.sessionExpiresAt = expires.getTime();
                const ticketNo = ticket && (ticket.ticketNo || ticket.TicketNo);
                const typeName = ticket && (ticket.ticketTypeName || ticket.TicketTypeName);

                function tick() {
                    const remaining = state.sessionExpiresAt - Date.now();
                    if (remaining <= 0) {
                        clearSessionTimer();
                        playScanFeedback("expired");
                        showGateOverlay("error", t("resortTickets.gate.sessionEnded"), ticketNo || "");
                        return;
                    }

                    const urgent = remaining <= 60000;
                    $sessionTimer
                        .removeClass("resort-ticket-gate__session-timer--urgent")
                        .toggleClass("resort-ticket-gate__session-timer--urgent", urgent)
                        .prop("hidden", false)
                        .empty()
                        .append(
                            $("<div/>")
                                .addClass("resort-ticket-gate__session-timer-label")
                                .text(t("resortTickets.gate.activeSession"))
                        )
                        .append(
                            $("<div/>")
                                .addClass("resort-ticket-gate__session-timer-meta")
                                .text([ticketNo, typeName].filter(Boolean).join(" · "))
                        )
                        .append(
                            $("<div/>")
                                .addClass("resort-ticket-gate__session-timer-clock")
                                .text(formatCountdown(remaining))
                        );
                }

                if (state.sessionTimerId) {
                    window.clearInterval(state.sessionTimerId);
                }
                tick();
                state.sessionTimerId = window.setInterval(tick, 1000);
            }

            function maybeStartSessionTimer(result, ticket) {
                const status = result && (result.validityStatus || result.ValidityStatus);
                const validTo = result && (result.validTo || result.ValidTo);
                const remaining = result && (result.remainingMinutes ?? result.RemainingMinutes);

                if (status === "valid" && remaining != null && parseValidTo(validTo)) {
                    renderSessionTimer(ticket, validTo);
                    return;
                }
                if (status === "pending_activation") {
                    clearSessionTimer();
                }
            }

            function focusInput() {
                if (!isGate && qrInput && qrInput.focus) {
                    qrInput.focus();
                }
            }

            function showGateOverlay(kind, message, subline, audioKind) {
                if (!isGate) {
                    return;
                }

                $gateOverlay
                    .removeClass("resort-ticket-gate__overlay--ok resort-ticket-gate__overlay--error")
                    .addClass(`resort-ticket-gate__overlay--${kind}`)
                    .empty()
                    .append($("<div/>").addClass("resort-ticket-gate__overlay-icon").text(kind === "ok" ? "✓" : "✕"))
                    .append($("<div/>").addClass("resort-ticket-gate__overlay-msg").text(message))
                    .prop("hidden", false);

                if (subline) {
                    $("<div/>").addClass("resort-ticket-gate__overlay-sub").text(subline).appendTo($gateOverlay);
                }

                vibrate(kind === "ok" ? [80, 40, 80] : [200]);
                if (kind === "ok") {
                    playScanFeedback(audioKind || "success");
                } else {
                    playScanFeedback("error");
                }

                window.clearTimeout(showGateOverlay._timer);
                const holdMs = audioKind === "session" ? 3200 : kind === "ok" ? 2400 : 3200;
                showGateOverlay._timer = window.setTimeout(() => {
                    $gateOverlay.prop("hidden", true);
                }, holdMs);
            }

            function renderResult(kind, message, ticket, orderNo, extraLine, audioKind) {
                if (isGate) {
                    const parts = [];
                    if (ticket) {
                        parts.push(
                            `${ticket.ticketNo || ticket.TicketNo} · ${ticket.ticketTypeName || ticket.TicketTypeName}`
                        );
                    }
                    if (extraLine) {
                        parts.push(extraLine);
                    }
                    showGateOverlay(kind, message, parts.join(" · "), audioKind);
                    return;
                }

                if (kind === "ok") {
                    playScanFeedback(audioKind || "success");
                } else {
                    playScanFeedback("error");
                }

                $resultHost.empty();
                const $card = $("<div/>")
                    .addClass("resort-ticket-scanner__result-card")
                    .addClass(`resort-ticket-scanner__result-card--${kind}`)
                    .appendTo($resultHost);
                $("<div/>").addClass("resort-ticket-scanner__result-msg").text(message).appendTo($card);
                if (ticket) {
                    const lines = [
                        `${t("resortTickets.orderDetail.ticketNo")}: ${ticket.ticketNo || ticket.TicketNo}`,
                        `${t("resortTickets.orderDetail.ticketType")}: ${ticket.ticketTypeName || ticket.TicketTypeName}`,
                        `${t("resortTickets.orderDetail.ticketStatus")}: ${ticketStatusLabel(
                            t,
                            ticket.ticketStatus || ticket.TicketStatus
                        )}`,
                        `${t("roomBoard.resortTickets.orderNo")}: ${orderNo || "—"}`,
                        `${t("resortTickets.orderDetail.validTo")}: ${formatDateTime(ticket.validTo || ticket.ValidTo)}`
                    ];
                    lines.forEach((line) => {
                        $("<div/>").addClass("resort-ticket-scanner__result-line").text(line).appendTo($card);
                    });
                }
            }

            function pushRecent(entry) {
                state.recent.unshift(entry);
                state.recent = state.recent.slice(0, isGate ? 5 : 12);
                renderRecent();
            }

            function renderRecent() {
                $recentList.empty();
                if (!state.recent.length) {
                    if (!isGate) {
                        $("<div/>")
                            .addClass("resort-ticket-scanner__recent-empty")
                            .text(t("resortTickets.scanner.emptyRecent"))
                            .appendTo($recentList);
                    }
                    return;
                }

                state.recent.forEach((item) => {
                    $("<div/>")
                        .addClass(isGate ? "resort-ticket-gate__recent-row" : "resort-ticket-scanner__recent-row")
                        .addClass(
                            isGate
                                ? `resort-ticket-gate__recent-row--${item.kind}`
                                : `resort-ticket-scanner__recent-row--${item.kind}`
                        )
                        .text(`${item.time} · ${item.label}`)
                        .appendTo($recentList);
                });
            }

            function pauseCameraBriefly() {
                state.scanPausedUntil = Date.now() + 1800;
            }

            function handleQrSubmit(raw) {
                const qr = String(raw || "").trim();
                if (!qr || state.busy || Date.now() < state.scanPausedUntil) {
                    return;
                }

                if (qr === state.lastQr && Date.now() - state.lastQrAt < 2800) {
                    return;
                }

                state.busy = true;
                state.lastQr = qr;
                state.lastQrAt = Date.now();
                pauseCameraBriefly();

                const action = state.lookupOnly
                    ? service.lookupByQr(qr, stationCode || undefined)
                    : service.redeemTicket(qr, stationCode || undefined);
                action
                    .always(() => {
                        state.busy = false;
                        if (qrInput) {
                            qrInput.option("value", "");
                        }
                        if (!isGate) {
                            focusInput();
                        }
                    })
                    .then((result) => {
                        const success = !!(result && result.success);
                        const block = result && (result.blockReason || result.BlockReason);
                        const ticket = result && (result.ticket || result.Ticket);
                        const orderNo = result && (result.orderNo || result.OrderNo);
                        const isReentry = !!(result && (result.isReentry || result.IsReentry));
                        const validityLine = buildValidityLine(t, result);
                        const nowLabel = new Date().toLocaleTimeString();

                        if (success) {
                            const validityStatus = result && (result.validityStatus || result.ValidityStatus);
                            const sessionJustStarted =
                                !state.lookupOnly
                                && !isReentry
                                && validityStatus === "valid"
                                && (result.remainingMinutes != null || result.RemainingMinutes != null);
                            const msg = state.lookupOnly
                                ? t("resortTickets.scanner.previewOk")
                                : sessionJustStarted
                                  ? t("resortTickets.scanner.sessionStarted")
                                  : isReentry
                                    ? t("resortTickets.scanner.successReentry")
                                    : t("resortTickets.scanner.success");
                            const audioKind = sessionJustStarted ? "session" : "success";
                            renderResult("ok", msg, ticket, orderNo, validityLine, audioKind);
                            maybeStartSessionTimer(result, ticket);
                            if (!isGate && window.DevExpress) {
                                DevExpress.ui.notify(msg, "success", 2200);
                            }
                            pushRecent({
                                kind: "ok",
                                time: nowLabel,
                                label: `${ticket && (ticket.ticketNo || ticket.TicketNo)} · ${validityLabel(
                                    t,
                                    result && (result.validityStatus || result.ValidityStatus)
                                )}`
                            });
                            return;
                        }

                        const msg = blockMessage(t, block);
                        renderResult("error", msg, ticket, orderNo, validityLine);
                        if (!isGate && window.DevExpress) {
                            DevExpress.ui.notify(msg, "error", 3500);
                        }
                        pushRecent({
                            kind: "error",
                            time: nowLabel,
                            label: `${ticket && (ticket.ticketNo || ticket.TicketNo) || qr} · ${msg}`
                        });
                    })
                    .catch(() => {
                        const msg = t("common.error");
                        renderResult("error", msg, null, null);
                        if (!isGate && window.DevExpress) {
                            DevExpress.ui.notify(msg, "error", 3500);
                        }
                    });
            }

            function stopCamera() {
                if (!cameraScanner) {
                    return $.Deferred().resolve().promise();
                }

                const scanner = cameraScanner;
                cameraScanner = null;
                state.cameraOn = false;
                return scanner
                    .stop()
                    .then(() => scanner.clear())
                    .catch(() => {});
            }

            function startCamera() {
                if (!window.Html5Qrcode) {
                    if (!isGate) {
                        DevExpress.ui.notify(t("resortTickets.scanner.cameraUnavailable"), "warning", 3500);
                    }
                    return $.Deferred().reject().promise();
                }

                return stopCamera().then(() => {
                    const scanner = new Html5Qrcode(readerId);
                    return Html5Qrcode.getCameras().then((devices) => {
                        if (!devices || !devices.length) {
                            throw new Error("no_camera");
                        }

                        const preferred =
                            devices.find((d) => /back|rear|environment/i.test(d.label || "")) ||
                            devices[devices.length - 1];
                        const qrSize = Math.min(isGate ? 300 : 260, Math.max(180, window.innerWidth - 56));

                        return scanner.start(
                            preferred.id,
                            {
                                fps: 10,
                                qrbox: { width: qrSize, height: qrSize },
                                aspectRatio: 1
                            },
                            (decodedText) => {
                                handleQrSubmit(decodedText);
                            },
                            () => {}
                        ).then(() => {
                            cameraScanner = scanner;
                            state.cameraOn = true;
                        });
                    });
                });
            }

            function buildCameraButtons() {
                $cameraActions.empty();

                if (isGate) {
                    return;
                }

                $("<div/>").dxButton({
                    text: state.cameraOn
                        ? t("resortTickets.scanner.cameraStop")
                        : t("resortTickets.scanner.cameraStart"),
                    icon: state.cameraOn ? "close" : "photo",
                    stylingMode: "outlined",
                    onClick() {
                        if (state.cameraOn) {
                            stopCamera().then(() => buildCameraButtons());
                        } else {
                            startCamera()
                                .then(() => buildCameraButtons())
                                .catch(() => {
                                    DevExpress.ui.notify(
                                        t("resortTickets.scanner.cameraUnavailable"),
                                        "warning",
                                        3500
                                    );
                                });
                        }
                    }
                }).appendTo($cameraActions);

                if (canOpenGate()) {
                    $("<div/>")
                        .addClass("resort-ticket-scanner__gate-link")
                        .dxButton({
                            text: t("resortTickets.scanner.openGateMode"),
                            icon: "fullscreen",
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                const url = window.Zaaer.ResortTicketGateStation
                                    ? window.Zaaer.ResortTicketGateStation.buildGateUrl(stationCode)
                                    : "/resort-ticket-gate.html";
                                window.location.href = url;
                            }
                        })
                        .appendTo($cameraActions);
                }
            }

            function canOpenGate() {
                return api && api.hasPermission("resort_tickets.validate");
            }

            if (!isGate) {
                qrInput = $inputHost
                    .dxTextBox({
                        label: t("resortTickets.scanner.inputLabel"),
                        placeholder: t("resortTickets.scanner.inputPlaceholder"),
                        showClearButton: true,
                        valueChangeEvent: "input",
                        onEnterKey(e) {
                            handleQrSubmit(e.component.option("value"));
                        }
                    })
                    .dxTextBox("instance");

                lookupSwitch = $optionsHost
                    .dxCheckBox({
                        text: t("resortTickets.scanner.lookupOnly"),
                        value: false,
                        onValueChanged(e) {
                            state.lookupOnly = !!e.value;
                        }
                    })
                    .dxCheckBox("instance");

                buildCameraButtons();

                $(document)
                    .off("click.resortTicketScannerFocus")
                    .on("click.resortTicketScannerFocus", (ev) => {
                        const tag = String((ev.target && ev.target.tagName) || "").toLowerCase();
                        if (
                            tag !== "input" &&
                            tag !== "textarea" &&
                            !$(ev.target).closest(".dx-checkbox, .dx-button, #" + readerId).length
                        ) {
                            focusInput();
                        }
                    });

                setTimeout(focusInput, 300);
            } else {
                $("<label/>")
                    .addClass("resort-ticket-gate__manual-label")
                    .attr("for", `${readerId}_manual`)
                    .text(t("resortTickets.gate.manualEntry"))
                    .appendTo($inputHost);
                $("<input/>")
                    .attr({
                        id: `${readerId}_manual`,
                        type: "text",
                        autocomplete: "off",
                        autocapitalize: "off",
                        spellcheck: "false",
                        placeholder: t("resortTickets.scanner.inputPlaceholder")
                    })
                    .addClass("resort-ticket-gate__manual-input")
                    .on("keydown", (ev) => {
                        if (ev.key === "Enter") {
                            ev.preventDefault();
                            handleQrSubmit(ev.target.value);
                            ev.target.value = "";
                        }
                    })
                    .appendTo($inputHost);

                startCamera().catch(() => {
                    $reader.append(
                        $("<div/>")
                            .addClass("resort-ticket-gate__camera-fallback")
                            .text(t("resortTickets.scanner.cameraUnavailable"))
                    );
                });
            }

            renderRecent();

            return {
                destroy() {
                    clearSessionTimer();
                    stopCamera();
                    if ($gateOverlay.length) {
                        $gateOverlay.remove();
                    }
                    $(document).off("click.resortTicketScannerFocus");
                },
                reload() {
                    return $.Deferred().resolve().promise();
                }
            };
        }
    };
})(window, jQuery);
