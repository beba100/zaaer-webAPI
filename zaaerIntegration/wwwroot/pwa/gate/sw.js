/* Resort ticket gate PWA — shell cache only; API always network. */
const CACHE_NAME = "resort-ticket-gate-v2";
const PRECACHE_URLS = [
    "/resort-ticket-gate.html",
    "/css/pms-theme.css",
    "/css/resort-tickets.css",
    "/js/i18n/ar.js",
    "/js/i18n/en.js",
    "/js/core/api-service.js",
    "/js/core/resort-ticket-scan-audio.js",
    "/js/core/resort-ticket-gate-station.js",
    "/js/services/resort-ticket-service.js",
    "/js/components/resort-ticket-scanner-panel.js",
    "/js/pages/resort-ticket-gate.js",
    "/js/pwa/resort-ticket-gate-pwa.js",
    "/Lib/js/jquery.min.js",
    "/Lib/js/html5-qrcode.min.js",
    "/pwa/gate/manifest.webmanifest",
    "/pwa/gate/icon-192.png",
    "/pwa/gate/icon-512.png"
];

self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => cache.addAll(PRECACHE_URLS)).then(() => self.skipWaiting())
    );
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches
            .keys()
            .then((keys) =>
                Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
            )
            .then(() => self.clients.claim())
    );
});

self.addEventListener("fetch", (event) => {
    const request = event.request;
    const url = new URL(request.url);

    if (request.method !== "GET") {
        return;
    }

    if (url.pathname.startsWith("/api/")) {
        event.respondWith(fetch(request));
        return;
    }

    event.respondWith(
        caches.match(request).then((cached) => {
            const network = fetch(request)
                .then((response) => {
                    if (response && response.status === 200 && url.origin === self.location.origin) {
                        const copy = response.clone();
                        caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
                    }
                    return response;
                })
                .catch(() => cached);

            return cached || network;
        })
    );
});
