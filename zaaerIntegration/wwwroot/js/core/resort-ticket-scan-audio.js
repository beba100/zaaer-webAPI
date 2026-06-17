(function (window) {
    "use strict";

    let audioContext = null;

    function getContext() {
        if (!window.AudioContext && !window.webkitAudioContext) {
            return null;
        }
        if (!audioContext) {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            audioContext = new Ctx();
        }
        if (audioContext.state === "suspended") {
            audioContext.resume().catch(() => {});
        }
        return audioContext;
    }

    function playTone(freq, startAt, duration, type, gainValue) {
        const ctx = getContext();
        if (!ctx) {
            return;
        }

        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = type || "sine";
        osc.frequency.setValueAtTime(freq, startAt);
        gain.gain.setValueAtTime(0.0001, startAt);
        gain.gain.exponentialRampToValueAtTime(gainValue || 0.12, startAt + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, startAt + duration);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(startAt);
        osc.stop(startAt + duration + 0.05);
    }

    function playSuccess() {
        const ctx = getContext();
        if (!ctx) {
            return;
        }
        const t0 = ctx.currentTime + 0.01;
        playTone(523.25, t0, 0.12, "sine", 0.1);
        playTone(659.25, t0 + 0.1, 0.16, "sine", 0.11);
    }

    function playSessionStart() {
        const ctx = getContext();
        if (!ctx) {
            return;
        }
        const t0 = ctx.currentTime + 0.01;
        playTone(440, t0, 0.1, "triangle", 0.09);
        playTone(554.37, t0 + 0.08, 0.1, "triangle", 0.1);
        playTone(659.25, t0 + 0.16, 0.18, "triangle", 0.11);
    }

    function playError() {
        const ctx = getContext();
        if (!ctx) {
            return;
        }
        const t0 = ctx.currentTime + 0.01;
        playTone(220, t0, 0.22, "sawtooth", 0.06);
        playTone(185, t0 + 0.14, 0.28, "sawtooth", 0.05);
    }

    function playExpired() {
        const ctx = getContext();
        if (!ctx) {
            return;
        }
        const t0 = ctx.currentTime + 0.01;
        playTone(330, t0, 0.14, "sine", 0.07);
        playTone(277.18, t0 + 0.12, 0.2, "sine", 0.06);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ResortTicketScanAudio = {
        playSuccess,
        playSessionStart,
        playError,
        playExpired,
        prime() {
            getContext();
        }
    };
})(window);
