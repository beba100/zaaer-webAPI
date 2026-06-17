(function (window, $, DevExpress) {
    "use strict";

    function sanitizePublicHtml(html) {
        if (!html) {
            return "";
        }
        let value = String(html);
        value = value.replace(/<\s*\/?\s*(script|iframe|object|embed|link|meta|base|form)\b[^>]*>/gi, "");
        value = value.replace(/\s+on[a-z]+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi, "");
        value = value.replace(/(href|src)\s*=\s*("javascript:[^"]*"|'javascript:[^']*')/gi, "");
        return value;
    }

    const svc = window.Zaaer.BookingEngineService;
    const state = {
        hotelCode: "",
        hotelList: [],
        profile: null,
        offers: [],
        cart: {},
        appliedCoupon: null,
        couponUiStatus: "none",
        rentalType: "daily",
        checkIn: new Date(),
        checkOut: new Date(Date.now() + 86400000)
    };

    let loadPanel;
    let searchFormReady = false;
    let langSwitching = false;
    let cardCarouselTimers = [];
    let celebrationInterval = null;
    let celebrationInitialTimer = null;

    function clearCardCarousels() {
        cardCarouselTimers.forEach(clearInterval);
        cardCarouselTimers = [];
    }

    function t(key, ...args) {
        let s = window.Zaaer.LocalizationService.t(key);
        if (s === key) {
            const fb = (isArabic() ? I18N_FALLBACK.ar : I18N_FALLBACK.en)[key];
            if (fb) {
                s = fb;
            }
        }
        if (args.length) {
            args.forEach((a, i) => {
                s = s.replace(`{${i}}`, a);
            });
        }
        return s;
    }

    const I18N_FALLBACK = {
        ar: {
            "bookingEngine.soldOut": "نفدت",
            "bookingEngine.soldOutHint": "غير متاحة لهذه التواريخ"
        },
        en: {
            "bookingEngine.soldOut": "Sold out",
            "bookingEngine.soldOutHint": "Not available for these dates"
        }
    };

    function isArabic() {
        return window.Zaaer.LocalizationService.currentCulture() === "ar";
    }

    function isMonthlyRental() {
        return state.rentalType === "monthly";
    }

    function getQueryHotel() {
        const params = new URLSearchParams(window.location.search);
        return (params.get("hotel") || params.get("hotelCode") || "").trim();
    }

    function updateUrlHotel(code) {
        const params = new URLSearchParams(window.location.search);
        if (code) {
            params.set("hotel", code);
        } else {
            params.delete("hotel");
        }
        const qs = params.toString();
        const next = `${window.location.pathname}${qs ? `?${qs}` : ""}`;
        window.history.replaceState({}, "", next);
    }

    function normalizeHotelList(raw) {
        const list = Array.isArray(raw) ? raw : [];
        return list
            .map((h) => ({
                code: (h.code || h.Code || "").trim(),
                name: h.name || h.Name || "",
                nameEn: h.nameEn || h.NameEn || ""
            }))
            .filter((h) => h.code);
    }

    function normalizeOffer(offer) {
        return {
            roomTypeId: offer.roomTypeId ?? offer.RoomTypeId,
            name: offer.name ?? offer.Name ?? "",
            nameEn: offer.nameEn ?? offer.NameEn ?? "",
            description: offer.description ?? offer.Description ?? "",
            pricePerNight: offer.pricePerNight ?? offer.PricePerNight ?? 0,
            totalPrice: offer.totalPrice ?? offer.TotalPrice ?? 0,
            taxAmount: offer.taxAmount ?? offer.TaxAmount ?? 0,
            grandTotal: offer.grandTotal ?? offer.GrandTotal ?? 0,
            nights: offer.nights ?? offer.Nights ?? 1,
            availableUnits: offer.availableUnits ?? offer.AvailableUnits ?? 0,
            images: offer.images ?? offer.Images ?? [],
            highlights: offer.highlights ?? offer.Highlights ?? [],
            facilities: normalizeOfferFacilities(offer.facilities ?? offer.Facilities),
            services: normalizeOfferServices(offer.services ?? offer.Services),
            areaSqm: offer.areaSqm ?? offer.AreaSqm ?? null
        };
    }

    function normalizeOfferServices(raw) {
        const list = Array.isArray(raw) ? raw : [];
        const normalized = list.map((s) => String(s || "").trim().toLowerCase()).filter(Boolean);
        if (normalized.length) {
            return normalized;
        }
        return ["wifi", "ac", "tv", "safe"];
    }

    const BOOKING_SERVICE_ORDER = ["wifi", "ac", "tv", "safe", "minibar", "balcony", "parking"];

    /** Flat gray amenity icons (Agoda-style), inline SVG */
    const BOOKING_SERVICE_SVG = {
        wifi:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M12 18c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2zm-4.24-2.76a6 6 0 0 1 8.48 0l1.42-1.42a8 8 0 0 0-11.32 0l1.42 1.42zm-3.54-3.54a10 10 0 0 1 14.14 0l1.41-1.41a12 12 0 0 0-17 0l1.45 1.41zM1.41 9.59a14 14 0 0 1 21.16 0l-1.42 1.42a12 12 0 0 0-18.3 0L1.41 9.59z"/></svg>',
        ac: '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M22 11h-4.17l3.24-3.24-1.41-1.42L15 11h-2V9l5.66-5.66-1.42-1.41L13 6.17V2h-2v4.17L8.76 4.93 7.34 6.34 13 12v2H9.66L3.24 5.34 1.83 6.76 6.17 11H2v2h4.17l-3.24 3.24 1.41 1.42L9 13h2v2l-5.66 5.66 1.42 1.41L11 17.83V22h2v-4.17l3.24 3.24 1.42-1.41L13 15v-2h2.34l6.42 6.42 1.41-1.42L17.83 13H22z"/></svg>',
        tv: '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M21 3H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h5v2h8v-2h5c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 14H3V5h18v12z"/></svg>',
        safe:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z"/></svg>',
        minibar:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M6 3v12c0 2.21 1.79 4 4 4s4-1.79 4-4V3H6zm2 2h4v10c0 1.1-.9 2-2 2s-2-.9-2-2V5zm10 0h2v14h-2V5z"/></svg>',
        balcony:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M19 4H5c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-9-2-2zm0 14H5V8h14v10zm-7-2h2v2h-2v-2z"/></svg>',
        parking:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M18.92 6.01C17.73 4.74 15.95 4 14 4H8v16h2v-6h4c1.95 0 3.73-.74 4.92-2.01C20.17 10.26 20.17 7.74 18.92 6.01zM14 12h-4V6h4c1.65 0 3 1.35 3 3s-1.35 3-3 3z"/></svg>'
    };

    function serviceLabel(code) {
        return t(`property.service.${code}`) || code;
    }

    function appendOfferServiceIcons($host, services) {
        const set = new Set((services || []).map((c) => String(c).toLowerCase()));
        const display = BOOKING_SERVICE_ORDER.filter((c) => set.has(c));
        (services || []).forEach((c) => {
            const code = String(c).toLowerCase();
            if (!display.includes(code) && BOOKING_SERVICE_SVG[code]) {
                display.push(code);
            }
        });
        if (!display.length) {
            return;
        }

        const $row = $("<div/>").addClass("be-offer-service-icons").attr("role", "list").appendTo($host);
        display.forEach((code) => {
            const svg = BOOKING_SERVICE_SVG[code];
            if (!svg) {
                return;
            }
            const label = serviceLabel(code);
            $("<span/>")
                .addClass("be-offer-service-icon")
                .attr("role", "listitem")
                .attr("title", label)
                .attr("aria-label", label)
                .html(svg)
                .appendTo($row);
        });
    }

    function normalizeOfferFacilities(raw) {
        const list = Array.isArray(raw) ? raw : [];
        return list
            .map((f) => ({
                label: f.label ?? f.Label ?? "",
                labelEn: f.labelEn ?? f.LabelEn ?? ""
            }))
            .filter((f) => f.label || f.labelEn);
    }

    function facilityDisplayName(f) {
        if (!f) {
            return "";
        }
        return isArabic() ? f.label || f.labelEn : f.labelEn || f.label;
    }

    function normalizeProfile(raw) {
        const p = raw && typeof raw === "object" ? raw : {};
        return {
            code: p.code ?? p.Code ?? "",
            name: p.name ?? p.Name ?? "",
            nameEn: p.nameEn ?? p.NameEn ?? "",
            city: p.city ?? p.City ?? "",
            countryCode: p.countryCode ?? p.CountryCode ?? "",
            logoUrl: p.logoUrl ?? p.LogoUrl ?? "",
            faviconUrl: p.faviconUrl ?? p.FaviconUrl ?? "",
            bannerUrl: p.bannerUrl ?? p.BannerUrl ?? "",
            buttonColor: p.buttonColor ?? p.ButtonColor,
            borderColor: p.borderColor ?? p.BorderColor,
            backgroundColor: p.backgroundColor ?? p.BackgroundColor,
            topFilterHtml: p.topFilterHtml ?? p.TopFilterHtml ?? "",
            downFilterHtml: p.downFilterHtml ?? p.DownFilterHtml ?? "",
            contactEmail: p.contactEmail ?? p.ContactEmail ?? "",
            contactPhone: p.contactPhone ?? p.ContactPhone ?? "",
            contactDescription: p.contactDescription ?? p.ContactDescription ?? "",
            depositMode: p.depositMode ?? p.DepositMode ?? "optional",
            onlineDepositEnabled: p.onlineDepositEnabled ?? p.OnlineDepositEnabled ?? false,
            minimumStayNights: p.minimumStayNights ?? p.MinimumStayNights ?? 1,
            salesClosed: !!(p.salesClosed ?? p.SalesClosed),
            salesClosedMessage: p.salesClosedMessage ?? p.SalesClosedMessage ?? "",
            rentalTypeMode: p.rentalTypeMode ?? p.RentalTypeMode ?? "both",
            promoBanner: normalizePromoBanner(p.promoBanner ?? p.PromoBanner),
            hasActiveCoupons: !!(p.hasActiveCoupons ?? p.HasActiveCoupons),
            hotels: normalizeHotelList(p.hotels ?? p.Hotels)
        };
    }

    function couponsEnabled() {
        return !!(state.profile && state.profile.hasActiveCoupons);
    }

    function normalizePromoBanner(raw) {
        if (!raw || typeof raw !== "object") {
            return null;
        }
        const html = raw.html ?? raw.Html ?? "";
        const imageUrl = raw.imageUrl ?? raw.ImageUrl ?? "";
        if (!html && !imageUrl) {
            return null;
        }
        return {
            html,
            imageUrl,
            endsAt: raw.endsAt ?? raw.EndsAt ?? null
        };
    }

    function promoBannerStorageKey() {
        return `be_promo_dismiss_${(state.hotelCode || "").trim().toLowerCase()}`;
    }

    function promoPlainTeaser(html, maxLen) {
        const limit = maxLen || 120;
        const tmp = document.createElement("div");
        tmp.innerHTML = html || "";
        const text = (tmp.textContent || "").replace(/\s+/g, " ").trim();
        if (!text) {
            return "";
        }
        return text.length > limit ? `${text.slice(0, limit)}…` : text;
    }

    function isPromoCelebrationActive() {
        const promo = state.profile && state.profile.promoBanner;
        if (!promo) {
            return false;
        }

        try {
            if (localStorage.getItem(promoBannerStorageKey()) === "1") {
                return false;
            }
        } catch (_) {
            /* ignore */
        }

        if (promo.endsAt) {
            const end = new Date(promo.endsAt);
            if (!Number.isNaN(end.getTime()) && end < new Date()) {
                return false;
            }
        }

        return true;
    }

    function prefersReducedMotion() {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function stopCelebrationScheduler() {
        if (celebrationInitialTimer) {
            clearTimeout(celebrationInitialTimer);
            celebrationInitialTimer = null;
        }
        if (celebrationInterval) {
            clearInterval(celebrationInterval);
            celebrationInterval = null;
        }
    }

    function runCelebrationConfetti(fromUserClick) {
        if (prefersReducedMotion() || document.hidden) {
            return;
        }
        if (!fromUserClick && !isPromoCelebrationActive()) {
            return;
        }

        let $layer = $("#beCelebrationLayer");
        if (!$layer.length) {
            $layer = $("<div/>").attr("id", "beCelebrationLayer").addClass("be-celebration-layer").appendTo("body");
        }

        const colors = ["#f59e0b", "#ef4444", "#22c55e", "#3b82f6", "#a855f7", "#ec4899", "#fde047", "#14b8a6"];
        const count = fromUserClick ? 96 : 64;

        for (let i = 0; i < count; i += 1) {
            const w = 5 + Math.random() * 7;
            const h = 7 + Math.random() * 11;
            const isCircle = Math.random() > 0.45;
            const $piece = $("<span/>")
                .addClass(`be-confetti${isCircle ? " be-confetti--circle" : ""}`)
                .css({
                    left: `${Math.random() * 100}%`,
                    width: `${w}px`,
                    height: `${h}px`,
                    backgroundColor: colors[i % colors.length],
                    animationDelay: `${Math.random() * 1.2}s`,
                    animationDuration: `${2.6 + Math.random() * 2.4}s`,
                    opacity: 0.85 + Math.random() * 0.15
                })
                .appendTo($layer);

            $piece.one("animationend", () => $piece.remove());
        }

        setTimeout(() => $layer.empty(), 6500);
    }

    function syncCelebrationScheduler() {
        stopCelebrationScheduler();
        if (!isPromoCelebrationActive() || prefersReducedMotion()) {
            return;
        }

        celebrationInitialTimer = setTimeout(() => runCelebrationConfetti(), 2500);
        celebrationInterval = setInterval(() => runCelebrationConfetti(), 10 * 60 * 1000);
    }

    function renderPromoBanner() {
        const $host = $("#beHeroPromo");
        $host.empty().attr("hidden", true);

        const promo = state.profile && state.profile.promoBanner;
        if (!promo) {
            stopCelebrationScheduler();
            return;
        }

        const imageUrl = promo.imageUrl && String(promo.imageUrl).trim();
        const html = promo.html && String(promo.html).trim();
        if (!imageUrl && !html) {
            stopCelebrationScheduler();
            return;
        }

        try {
            if (localStorage.getItem(promoBannerStorageKey()) === "1") {
                stopCelebrationScheduler();
                return;
            }
        } catch (_) {
            /* ignore */
        }

        const teaserFull = promoPlainTeaser(html, 500) || t("bookingEngine.promoChipLabel");

        const $chip = $("<button/>")
            .attr("type", "button")
            .addClass("be-hero-promo-chip")
            .attr("aria-label", teaserFull)
            .attr("title", teaserFull)
            .appendTo($host);

        const $orbit = $("<span/>").addClass("be-hero-promo-orbit").appendTo($chip);
        if (imageUrl) {
            $("<img/>", { src: imageUrl, alt: "", loading: "lazy", decoding: "async" })
                .addClass("be-hero-promo-thumb")
                .appendTo($orbit);
        } else {
            $("<span/>").addClass("be-hero-promo-fallback").text("★").appendTo($orbit);
        }

        $("<span/>").addClass("be-hero-promo-caption").text(teaserFull).appendTo($chip);

        $chip.on("click", () => {
            runCelebrationConfetti(true);
            openPromoPopup(promo);
        });
        $host.removeAttr("hidden");
        syncCelebrationScheduler();
    }

    function openPromoPopup(promo) {
        const imageUrl = promo.imageUrl && String(promo.imageUrl).trim();
        const html = promo.html && String(promo.html).trim();
        const rtl = isArabic();

        const $popupHost = $("#bePromoPopupHost").empty();
        const $content = $("<div/>").addClass("be-promo-popup-content").attr("dir", rtl ? "rtl" : "ltr");

        if (imageUrl) {
            const $media = $("<div/>").addClass("be-promo-popup-media").appendTo($content);
            $("<img/>", { src: imageUrl, alt: "", class: "be-promo-popup-img", loading: "lazy", decoding: "async" }).appendTo(
                $media
            );
        }
        if (html) {
            $("<div/>").addClass("be-promo-popup-text").html(sanitizePublicHtml(html)).appendTo($content);
        }

        const $actions = $("<div/>").addClass("be-promo-popup-actions be-promo-popup-actions--compact").appendTo($content);
        $("<button/>")
            .attr("type", "button")
            .addClass("be-btn-primary be-promo-popup-close-btn")
            .text(t("common.close"))
            .on("click", () => {
                const inst = $popupHost.dxPopup("instance");
                if (inst) {
                    inst.hide();
                }
            })
            .appendTo($actions);

        $popupHost.dxPopup({
            visible: true,
            showTitle: true,
            showCloseButton: true,
            title: t("bookingEngine.promoChipLabel"),
            width: Math.min(580, Math.max(340, window.innerWidth - 32)),
            height: "auto",
            maxHeight: "72vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.35)",
            wrapperAttr: { class: "be-promo-popup-wrap" },
            contentTemplate: () => $content,
            onHidden() {
                $popupHost.dxPopup("dispose");
                $popupHost.empty();
            }
        });
    }

    function isSalesClosed() {
        return !!(state.profile && state.profile.salesClosed);
    }

    function salesClosedMessage() {
        const custom = state.profile && state.profile.salesClosedMessage;
        if (custom && String(custom).trim()) {
            return String(custom).trim();
        }
        return t("bookingEngine.salesClosedDefault");
    }

    function applySalesClosedUi() {
        const closed = isSalesClosed();
        const $banner = $("#beSalesClosed");
        const $searchHost = $("#beSearchHost");
        const $main = $("#beMain");

        if (!closed) {
            $banner.attr("hidden", true).empty();
            $searchHost.removeClass("be-search-card--disabled");
            $main.removeClass("be-main--sales-closed");
            return;
        }

        state.cart = {};
        renderCartBar();
        $("#beOffers").empty();
        $("#beResultsHead").attr("hidden", true);
        $main.addClass("be-main--sales-closed");
        $searchHost.addClass("be-search-card--disabled");

        $banner
            .removeAttr("hidden")
            .empty()
            .append(
                $("<div/>")
                    .addClass("be-sales-closed-inner")
                    .append($("<span/>").addClass("be-sales-closed-icon").text("⏸"))
                    .append($("<h2/>").addClass("be-sales-closed-title").text(t("bookingEngine.salesClosedTitle")))
                    .append($("<p/>").addClass("be-sales-closed-text").text(salesClosedMessage()))
            );
    }

    function showHotelPicker() {
        return state.hotelList.length > 1;
    }

    function fallbackRoomImage() {
        return (state.profile && state.profile.logoUrl) || "/logo/logo.jpeg";
    }

    function applyTheme(profile) {
        const root = document.documentElement;
        if (profile.buttonColor) {
            root.style.setProperty("--be-primary", profile.buttonColor);
        }
        if (profile.borderColor) {
            root.style.setProperty("--be-border", profile.borderColor);
        }
        if (profile.backgroundColor) {
            root.style.setProperty("--be-header-bg", profile.backgroundColor);
        }
        if (profile.faviconUrl) {
            let link = document.querySelector('link[rel="icon"]');
            if (!link) {
                link = document.createElement("link");
                link.rel = "icon";
                document.head.appendChild(link);
            }
            link.href = profile.faviconUrl;
        }
    }

    function renderBrand(profile) {
        const name = isArabic() ? profile.name || profile.nameEn : profile.nameEn || profile.name;
        const $brand = $("#beBrand").empty();
        if (profile.logoUrl) {
            $("<img/>", { src: profile.logoUrl, alt: name || "" }).appendTo($brand);
        }
        const $text = $("<div/>").appendTo($brand);
        $("<h1/>").text(name || "").appendTo($text);
        const meta = [profile.city, profile.countryCode].filter(Boolean).join(" · ");
        if (meta) {
            $("<p/>").text(meta).appendTo($text);
        }
    }

    function getRentalTypeMode() {
        const mode = state.profile && state.profile.rentalTypeMode;
        const v = String(mode || "both").trim().toLowerCase();
        if (v === "daily_only" || v === "monthly_only" || v === "hidden") {
            return v;
        }
        return "both";
    }

    function showRentalTypeDropdown() {
        return getRentalTypeMode() === "both";
    }

    function applyRentalTypeFromProfile() {
        const mode = getRentalTypeMode();
        if (mode === "monthly_only") {
            state.rentalType = "monthly";
        } else {
            state.rentalType = "daily";
        }
    }

    function rentalItems() {
        const mode = getRentalTypeMode();
        const items = [
            { value: "daily", text: t("bookingEngine.rental.daily") },
            { value: "monthly", text: t("bookingEngine.rental.monthly") }
        ];
        if (mode === "daily_only") {
            return items.filter((i) => i.value === "daily");
        }
        if (mode === "monthly_only") {
            return items.filter((i) => i.value === "monthly");
        }
        return items;
    }

    function syncSearchEditors() {
        const checkIn = $("#beCheckIn").dxDateBox("instance");
        const checkOut = $("#beCheckOut").dxDateBox("instance");
        const rental = $("#beRentalType").dxSelectBox("instance");
        const hotel = $("#beHotelPicker").dxSelectBox("instance");

        if (checkIn) {
            checkIn.option("value", state.checkIn);
        }
        if (checkOut) {
            checkOut.option("value", state.checkOut);
        }
        if (rental) {
            rental.option("value", state.rentalType);
        }
        if (hotel) {
            hotel.option("value", state.hotelCode);
        }

        refreshSearchFormLabels();
    }

    function refreshSearchFormLabels() {
        const checkIn = $("#beCheckIn").dxDateBox("instance");
        const checkOut = $("#beCheckOut").dxDateBox("instance");
        const rental = $("#beRentalType").dxSelectBox("instance");
        const hotel = $("#beHotelPicker").dxSelectBox("instance");
        const searchBtn = $("#beSearchBtn").dxButton("instance");

        if (checkIn) {
            checkIn.option({
                label: t("bookingEngine.checkIn"),
                displayFormat: formatWesternDate
            });
        }
        if (checkOut) {
            checkOut.option({
                label: t("bookingEngine.checkOut"),
                displayFormat: formatWesternDate
            });
        }
        if (rental) {
            rental.option({
                label: t("bookingEngine.rentalType"),
                dataSource: rentalItems(),
                displayExpr: "text",
                value: state.rentalType
            });
        }
        if (hotel) {
            hotel.option({
                label: t("bookingEngine.hotel"),
                dataSource: state.hotelList.slice(),
                displayExpr: hotelPickerDisplayExpr,
                value: state.hotelCode
            });
        }
        if (searchBtn) {
            searchBtn.option("text", t("bookingEngine.search"));
        }
    }

    function disposeSearchForm() {
        ["beHotelPicker", "beRentalType", "beCheckIn", "beCheckOut", "beSearchBtn"].forEach((id) => {
            const $el = $("#" + id);
            if (!$el.length) {
                return;
            }
            if ($el.data("dxSelectBox")) {
                $el.dxSelectBox("dispose");
            }
            if ($el.data("dxDateBox")) {
                $el.dxDateBox("dispose");
            }
            if ($el.data("dxButton")) {
                $el.dxButton("dispose");
            }
        });
        $("#beSearchHost").empty().removeClass(
            "be-search-card be-search-card--no-hotel be-search-card--no-rental be-search-card--disabled"
        );
        searchFormReady = false;
    }

    function refreshAfterLanguageChange() {
        const nextCulture = isArabic() ? "en" : "ar";
        window.Zaaer.LocalizationService.setCulture(nextCulture);
        $("#beLangToggle").text(nextCulture === "ar" ? "EN" : "AR");

        closeCheckoutDrawer();
        closeImageLightbox();
        disposeSearchForm();
        buildSearchForm();
        refreshSearchFormLabels();

        if (state.profile) {
            renderBrand(state.profile);
            renderFilterHtml(state.profile);
            renderPromoBanner();
            applySalesClosedUi();
            const brandName = isArabic()
                ? state.profile.name || state.profile.nameEn
                : state.profile.nameEn || state.profile.name;
            if (brandName) {
                document.title = brandName;
            }
        }

        const onSuccessScreen = $("#beSuccessHost").length && !$("#beSuccessHost").is("[hidden]");
        if (!onSuccessScreen && !isSalesClosed() && state.hotelCode) {
            runSearch();
        } else if (!onSuccessScreen && !isSalesClosed() && state.offers.length) {
            renderOffers(state.offers);
        }

        renderCartBar();
    }

    function buildSearchForm() {
        if (searchFormReady) {
            syncSearchEditors();
            return;
        }

        applyRentalTypeFromProfile();
        const rentalVisible = showRentalTypeDropdown();

        const $form = $("#beSearchHost")
            .empty()
            .addClass("be-search-card be-animate-in")
            .toggleClass("be-search-card--no-hotel", !showHotelPicker())
            .toggleClass("be-search-card--no-rental", !rentalVisible);

        if (showHotelPicker()) {
            $("<div/>").attr("id", "beHotelPicker").addClass("be-search-field be-search-field--hotel").appendTo($form);
        }

        if (rentalVisible) {
            $("<div/>").attr("id", "beRentalType").addClass("be-search-field be-search-field--rental").appendTo($form);
        }
        $("<div/>").attr("id", "beCheckIn").addClass("be-search-field").appendTo($form);
        $("<div/>").attr("id", "beCheckOut").addClass("be-search-field").appendTo($form);
        $("<div/>").addClass("be-search-btn-wrap be-search-field").attr("id", "beSearchBtn").appendTo($form);

        if (showHotelPicker()) {
            $("#beHotelPicker").dxSelectBox({
                label: t("bookingEngine.hotel"),
                dataSource: state.hotelList,
                valueExpr: "code",
                displayExpr: hotelPickerDisplayExpr,
                searchEnabled: true,
                searchExpr: ["code", "name", "nameEn"],
                value: state.hotelCode,
                openOnFieldClick: true,
                onValueChanged(e) {
                    if (!e.value || e.value === state.hotelCode) {
                        return;
                    }
                    state.hotelCode = e.value;
                    updateUrlHotel(state.hotelCode);
                    refreshHotelContext();
                }
            });
        }

        if (rentalVisible) {
            $("#beRentalType").dxSelectBox({
                label: t("bookingEngine.rentalType"),
                dataSource: rentalItems(),
                valueExpr: "value",
                displayExpr: "text",
                value: state.rentalType,
                openOnFieldClick: true,
                onValueChanged(e) {
                    const next = e.value === "monthly" ? "monthly" : "daily";
                    if (next !== state.rentalType) {
                        state.rentalType = next;
                        runSearch();
                    }
                }
            });
        }

        $("#beCheckIn").dxDateBox({
            label: t("bookingEngine.checkIn"),
            type: "date",
            value: state.checkIn,
            displayFormat: formatWesternDate,
            openOnFieldClick: true,
            onValueChanged(e) {
                state.checkIn = e.value || new Date();
                if (state.checkOut && state.checkIn >= state.checkOut) {
                    const next = new Date(state.checkIn);
                    next.setDate(next.getDate() + 1);
                    state.checkOut = next;
                    $("#beCheckOut").dxDateBox("instance").option("value", state.checkOut);
                }
            }
        });

        $("#beCheckOut").dxDateBox({
            label: t("bookingEngine.checkOut"),
            type: "date",
            value: state.checkOut,
            displayFormat: formatWesternDate,
            openOnFieldClick: true,
            onValueChanged(e) {
                state.checkOut = e.value || new Date();
            }
        });

        $("#beSearchBtn").dxButton({
            text: t("bookingEngine.search"),
            type: "default",
            width: "100%",
            onClick: runSearch
        });

        searchFormReady = true;
    }

    function renderFilterHtml(profile) {
        $("#beTopHtml").html(sanitizePublicHtml(profile.topFilterHtml || ""));
        $("#beDownHtml").html(sanitizePublicHtml(profile.downFilterHtml || ""));
        renderContactFooter(profile);
    }

    function renderContactFooter(profile) {
        const $footer = $("#beContact").empty().removeClass("is-visible");
        const phone = String(profile.contactPhone || "").trim();
        const email = String(profile.contactEmail || "").trim();
        const desc = String(profile.contactDescription || "").trim();

        if (!phone && !email && !desc) {
            $footer.attr("hidden", true);
            return;
        }

        const $card = $("<div/>").addClass("be-footer-card").appendTo($footer);

        const $head = $("<div/>").addClass("be-footer-head").appendTo($card);
        const $headIcon = $("<div/>").addClass("be-footer-head-icon").attr("aria-hidden", "true").appendTo($head);
        $("<i/>").addClass("dx-icon dx-icon-comment").appendTo($headIcon);
        const $headText = $("<div/>").addClass("be-footer-head-text").appendTo($head);
        $("<h2/>").addClass("be-footer-title").text(t("bookingEngine.contactFooterTitle")).appendTo($headText);
        $("<p/>").addClass("be-footer-sub").text(t("bookingEngine.contactFooterSub")).appendTo($headText);

        const $body = $("<div/>").addClass("be-footer-body").appendTo($card);
        const $grid = $("<div/>").addClass("be-footer-grid").appendTo($body);

        if (phone) {
            appendContactTile($grid, "tel", "bookingEngine.contactPhone", phone, "tel:");
        }
        if (email) {
            appendContactTile($grid, "email", "bookingEngine.contactEmail", email, "mailto:");
        }

        if (desc) {
            $("<div/>").addClass("be-footer-note").text(desc).appendTo($body);
        }

        $footer.removeAttr("hidden").addClass("is-visible");
    }

    function appendContactTile($grid, icon, labelKey, value, hrefPrefix) {
        const text = String(value || "").trim();
        if (!text) {
            return;
        }

        const href =
            hrefPrefix === "tel:"
                ? `tel:${text.replace(/[^\d+]/g, "")}`
                : `${hrefPrefix}${encodeURIComponent(text)}`;

        const $tile = $("<a/>")
            .addClass("be-footer-tile")
            .attr("href", href)
            .attr("rel", hrefPrefix === "mailto:" ? "noopener" : undefined)
            .appendTo($grid);

        const $iconWrap = $("<span/>").addClass(`be-footer-tile-icon be-footer-tile-icon--${icon}`).appendTo($tile);
        $("<i/>").addClass(`dx-icon dx-icon-${icon}`).attr("aria-hidden", "true").appendTo($iconWrap);

        const $meta = $("<span/>").addClass("be-footer-tile-meta").appendTo($tile);
        $("<span/>").addClass("be-footer-tile-label").text(t(labelKey)).appendTo($meta);
        $("<span/>").addClass("be-footer-tile-value").text(text).appendTo($meta);
    }

    const BE_NUM_LOCALE = "en-US";

    function formatWesternInteger(value) {
        const n = Number(value);
        if (!Number.isFinite(n)) {
            return "0";
        }
        return n.toLocaleString(BE_NUM_LOCALE, { maximumFractionDigits: 0 });
    }

    function formatMoney(amount) {
        const n = Number(amount);
        if (!Number.isFinite(n)) {
            return "0";
        }
        return n.toLocaleString(BE_NUM_LOCALE, {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2
        });
    }

    function formatWesternDate(value) {
        if (!value) {
            return "";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const day = String(d.getDate()).padStart(2, "0");
        const month = String(d.getMonth() + 1).padStart(2, "0");
        return `${day}/${month}/${d.getFullYear()}`;
    }

    function offerDisplayName(offer) {
        return isArabic() ? offer.name : offer.nameEn || offer.name;
    }

    function hotelPickerDisplayExpr(item) {
        if (!item) {
            return "";
        }

        return isArabic() ? item.name || item.code : item.code || item.nameEn || item.name;
    }

    function getCartQty(roomTypeId) {
        return state.cart[roomTypeId] || 0;
    }

    function setCartQty(offer, qty) {
        const max = offer.availableUnits || 0;
        const next = Math.max(0, Math.min(max, qty));
        state.appliedCoupon = null;
        if (next <= 0) {
            delete state.cart[offer.roomTypeId];
        } else {
            state.cart[offer.roomTypeId] = next;
        }
        renderCartBar();
        syncOfferQtyUi(offer.roomTypeId);
    }

    function syncOfferQtyUi(roomTypeId) {
        const qty = getCartQty(roomTypeId);
        const $val = $(`.be-qty-value[data-room-type="${roomTypeId}"]`);
        $val.text(formatWesternInteger(qty));
        const $card = $(`.be-offer-card[data-room-type="${roomTypeId}"]`);
        $card.toggleClass("be-offer-card--selected", qty > 0);
        const offer = state.offers.find((o) => o.roomTypeId === roomTypeId);
        if (offer) {
            $card.find(".be-qty-minus").prop("disabled", qty <= 0);
            $card.find(".be-qty-plus").prop("disabled", qty >= offer.availableUnits);
        }
    }

    function getCartLines() {
        return state.offers
            .filter((o) => getCartQty(o.roomTypeId) > 0)
            .map((o) => ({
                offer: o,
                quantity: getCartQty(o.roomTypeId),
                lineTotal: o.grandTotal * getCartQty(o.roomTypeId),
                lineSubtotal: o.totalPrice * getCartQty(o.roomTypeId),
                lineTax: o.taxAmount * getCartQty(o.roomTypeId)
            }));
    }

    function getCartTotals() {
        const lines = getCartLines();
        const rooms = lines.reduce((s, l) => s + l.quantity, 0);
        const subtotal = lines.reduce((s, l) => s + l.lineSubtotal, 0);
        const tax = lines.reduce((s, l) => s + l.lineTax, 0);
        const grand = lines.reduce((s, l) => s + l.lineTotal, 0);
        const discount = state.appliedCoupon ? Number(state.appliedCoupon.discountAmount) || 0 : 0;
        const grandAfter = Math.max(0, Math.round((grand - discount) * 100) / 100);
        return { lines, rooms, subtotal, tax, grand, discount, grandAfter };
    }

    function pruneCart() {
        const valid = new Set(state.offers.map((o) => o.roomTypeId));
        Object.keys(state.cart).forEach((id) => {
            const numId = Number(id);
            if (!valid.has(numId)) {
                delete state.cart[numId];
                return;
            }
            const offer = state.offers.find((o) => o.roomTypeId === numId);
            if (offer && state.cart[numId] > offer.availableUnits) {
                state.cart[numId] = offer.availableUnits;
            }
        });
    }

    function clearCartSelections() {
        closeCheckoutDrawer();
        state.cart = {};
        state.appliedCoupon = null;
        state.couponUiStatus = "none";
        state.offers.forEach((o) => syncOfferQtyUi(o.roomTypeId));
        renderCartBar();
    }

    function renderCartBar() {
        const totals = getCartTotals();
        const $bar = $("#beCartBar");
        const $clear = $("#beCartClearBtn");
        if (!totals.rooms) {
            $bar.attr("hidden", true);
            $clear.attr("hidden", true);
            $("body").removeClass("be-has-cart");
            return;
        }

        $bar.removeAttr("hidden");
        $clear.removeAttr("hidden").attr("aria-label", t("bookingEngine.clearSelection")).attr("title", t("bookingEngine.clearSelection"));
        $("body").addClass("be-has-cart");
        $("#beCartSummary").html(
            `<span class="be-cart-rooms">${t("bookingEngine.cartRooms", formatWesternInteger(totals.rooms))}</span>` +
                `<strong class="be-cart-total be-num-en">${formatMoney(totals.grand)} SAR</strong>`
        );
        $("#beCartBookBtn")
            .text(t("bookingEngine.bookNow"))
            .off("click")
            .on("click", openCheckoutDrawer);
    }

    function buildSelectionSummaryHtml(totals, compact) {
        const monthly = isMonthlyRental();
        const unitLabel = monthly ? t("bookingEngine.perMonth") : t("bookingEngine.perNight");
        const $box = $("<div/>").addClass(compact ? "be-summary-box be-summary-box--compact" : "be-summary-box");
        $("<h3/>").addClass("be-summary-title").text(t("bookingEngine.selectionSummary")).appendTo($box);

        totals.lines.forEach((line) => {
            const $row = $("<div/>").addClass("be-summary-line").appendTo($box);
            $("<div/>")
                .addClass("be-summary-line-main")
                .append($("<strong/>").text(offerDisplayName(line.offer)))
                .append(
                    $("<span/>")
                        .addClass("be-num-en")
                        .text(
                            t(
                                "bookingEngine.lineQtyPrice",
                                formatWesternInteger(line.quantity),
                                formatMoney(line.offer.grandTotal),
                                "SAR"
                            )
                        )
                )
                .appendTo($row);
            $("<div/>")
                .addClass("be-summary-line-meta be-num-en")
                .text(`${formatMoney(line.offer.pricePerNight)} SAR ${unitLabel}`)
                .appendTo($row);
        });

        $("<div/>")
            .addClass("be-summary-row")
            .append(
                $("<span/>").text(t("bookingEngine.subtotal")),
                $("<span/>").addClass("be-num-en").text(`${formatMoney(totals.subtotal)} SAR`)
            )
            .appendTo($box);
        $("<div/>")
            .addClass("be-summary-row")
            .append(
                $("<span/>").text(t("bookingEngine.tax")),
                $("<span/>").addClass("be-num-en").text(`${formatMoney(totals.tax)} SAR`)
            )
            .appendTo($box);
        if (totals.discount > 0) {
            $("<div/>")
                .addClass("be-summary-row be-summary-row--discount")
                .append(
                    $("<span/>").text(t("bookingEngine.couponDiscount")),
                    $("<span/>").addClass("be-num-en").text(`- ${formatMoney(totals.discount)} SAR`)
                )
                .appendTo($box);
        }
        const totalLabel = totals.discount > 0 ? t("bookingEngine.totalAfterDiscount") : t("bookingEngine.total");
        const totalValue = totals.discount > 0 ? totals.grandAfter : totals.grand;
        $("<div/>")
            .addClass("be-summary-row be-summary-row--total")
            .append(
                $("<strong/>").text(totalLabel),
                $("<strong/>").addClass("be-num-en").text(`${formatMoney(totalValue)} SAR`)
            )
            .appendTo($box);
        return $box;
    }

    let returningGuestLookupTimer = null;
    let returningGuestLookupSeq = 0;

    function normalizePhoneDigits(value) {
        return String(value || "").replace(/\D/g, "");
    }

    function hideWelcomeBanner() {
        $("#beWelcomeBanner").removeClass("is-visible").empty();
    }

    function showWelcomeBanner(displayName) {
        const $b = $("#beWelcomeBanner");
        if (!$b.length) {
            return;
        }
        $b.empty();
        const $line = $("<div/>").appendTo($b);
        $line.append(document.createTextNode(t("bookingEngine.welcomeBack")));
        if (displayName) {
            $line.append(document.createTextNode(" "));
            $("<strong/>").text(displayName).appendTo($line);
        }
        $("<p/>").addClass("be-welcome-hint").text(t("bookingEngine.welcomeBackHint")).appendTo($b);
        $b.addClass("is-visible");
    }

    function applyReturningGuestFields(data, onlyIfEmpty) {
        if (!data || !data.found) {
            return;
        }

        function setIf(sel, value) {
            if (!value) {
                return;
            }
            const inst = $(sel).dxTextBox("instance");
            if (!inst) {
                return;
            }
            if (onlyIfEmpty && inst.option("value")) {
                return;
            }
            inst.option("value", value);
        }

        setIf("#beFirstName", data.firstName);
        setIf("#beLastName", data.lastName);
        setIf("#beEmail", data.email);
    }

    function scheduleReturningGuestLookup(phone) {
        clearTimeout(returningGuestLookupTimer);
        hideWelcomeBanner();

        const digits = normalizePhoneDigits(phone);
        if (digits.length < 9 || !state.hotelCode) {
            return;
        }

        returningGuestLookupTimer = setTimeout(() => {
            runReturningGuestLookup(phone);
        }, 550);
    }

    async function runReturningGuestLookup(phone) {
        const seq = ++returningGuestLookupSeq;
        try {
            const raw = await svc.lookupReturningGuest({ hotel: state.hotelCode, phone });
            if (seq !== returningGuestLookupSeq) {
                return;
            }

            const data = raw && typeof raw === "object" ? raw : {};
            const found = !!(data.found ?? data.Found);
            if (!found) {
                hideWelcomeBanner();
                return;
            }

            const guest = {
                found: true,
                firstName: data.firstName ?? data.FirstName ?? "",
                lastName: data.lastName ?? data.LastName ?? "",
                email: data.email ?? data.Email ?? "",
                displayName: data.displayName ?? data.DisplayName ?? ""
            };

            applyReturningGuestFields(guest, true);
            showWelcomeBanner(guest.displayName || [guest.firstName, guest.lastName].filter(Boolean).join(" "));
        } catch {
            /* silent — lookup is optional UX */
        }
    }

    function normalizeCouponCode(code) {
        return String(code || "")
            .trim()
            .toUpperCase();
    }

    /** Map API English coupon errors to UI language (ar/en). */
    function localizeCouponMessage(serverMessage) {
        const m = String(serverMessage || "").trim();
        if (!m) {
            return t("bookingEngine.couponInvalid");
        }
        const low = m.toLowerCase();

        const minStay = /^minimum stay is (\d+)/i.exec(m);
        if (minStay) {
            return t("bookingEngine.couponMinStay", minStay[1]);
        }

        const minAmt = /^minimum booking amount is ([\d.,]+)/i.exec(m);
        if (minAmt) {
            return t("bookingEngine.couponMinAmount", minAmt[1]);
        }

        if (low.includes("invalid coupon") || low.includes("inactive")) {
            return t("bookingEngine.couponInvalid");
        }
        if (low.includes("expired")) {
            return t("bookingEngine.couponExpired");
        }
        if (low.includes("not valid yet")) {
            return t("bookingEngine.couponNotYetValid");
        }
        if (low.includes("usage limit")) {
            return t("bookingEngine.couponLimitReached");
        }
        if (low.includes("room types")) {
            return t("bookingEngine.couponRoomTypes");
        }
        if (low.includes("select rooms")) {
            return t("bookingEngine.couponSelectRoomsFirst");
        }

        return t("bookingEngine.couponInvalid");
    }

    function getCouponDraftCode() {
        const inst = $("#beCouponCode").dxTextBox("instance");
        return inst ? String(inst.option("value") || "").trim() : "";
    }

    function setCouponUiStatus(status, message) {
        state.couponUiStatus = status;
        const $wrap = $("#beCouponInputWrap");
        const $icon = $("#beCouponStatusIcon");
        const $msg = $("#beCouponMsg");
        const $hint = $("#beCouponHint");

        $wrap.removeClass("be-coupon-input-wrap--invalid");
        $icon.removeClass("be-coupon-status-icon--applied be-coupon-status-icon--invalid").attr("hidden", true);

        $msg.removeClass("be-coupon-msg--ok be-coupon-msg--warn be-coupon-msg--error").text("");
        $hint.text("");

        if (status === "applied") {
            $icon.addClass("be-coupon-status-icon--applied").text("✓").removeAttr("hidden");
            $msg.addClass("be-coupon-msg--ok").text(message || "");
        } else if (status === "invalid") {
            $wrap.addClass("be-coupon-input-wrap--invalid");
            $icon.addClass("be-coupon-status-icon--invalid").text("✕").removeAttr("hidden");
            $msg.addClass("be-coupon-msg--error").text(message || t("bookingEngine.couponInvalid"));
        } else if (status === "pending") {
            $hint.text(t("bookingEngine.couponPendingHint"));
        }
    }

    function refreshCheckoutSummaries() {
        const totals = getCartTotals();
        $(".be-checkout-summary-refresh").each(function () {
            const $host = $(this);
            const compact = $host.hasClass("be-summary-box--compact");
            $host.empty();
            buildSelectionSummaryHtml(totals, compact).appendTo($host);
        });
        renderCartBar();
    }

    async function validateCouponWithServer(code) {
        const totals = getCartTotals();
        const lines = totals.lines.map((l) => ({
            roomTypeId: l.offer.roomTypeId,
            quantity: l.quantity
        }));

        const raw = await svc.validateCoupon({
            hotelCode: state.hotelCode,
            couponCode: code,
            lines,
            fromDate: svc.formatLocalDateParam(state.checkIn),
            toDate: svc.formatLocalDateParam(state.checkOut),
            rentalType: state.rentalType
        });

        const data = raw && typeof raw === "object" ? raw : {};
        const valid = !!(data.valid ?? data.Valid);
        const serverMsg = data.message ?? data.Message ?? "";
        return {
            valid,
            message: valid ? serverMsg : localizeCouponMessage(serverMsg),
            promoCode: data.promoCode ?? data.PromoCode ?? code,
            discountAmount: data.discountAmount ?? data.DiscountAmount ?? 0
        };
    }

    async function applyCouponFromCheckout() {
        const code = getCouponDraftCode();
        if (!code) {
            state.appliedCoupon = null;
            setCouponUiStatus("none", "");
            refreshCheckoutSummaries();
            return true;
        }

        try {
            loadPanel.show();
            const result = await validateCouponWithServer(code);
            if (!result.valid) {
                state.appliedCoupon = null;
                setCouponUiStatus("invalid", result.message);
                refreshCheckoutSummaries();
                return false;
            }

            state.appliedCoupon = {
                promoCode: normalizeCouponCode(result.promoCode),
                discountAmount: result.discountAmount,
                valid: true
            };
            setCouponUiStatus("applied", t("bookingEngine.couponApplied", state.appliedCoupon.promoCode));
            DevExpress.ui.notify(t("bookingEngine.couponApplied", state.appliedCoupon.promoCode), "success", 2200);
            refreshCheckoutSummaries();
            return true;
        } catch (err) {
            state.appliedCoupon = null;
            setCouponUiStatus("invalid", localizeCouponMessage(err.message));
            refreshCheckoutSummaries();
            return false;
        } finally {
            loadPanel.hide();
        }
    }

    async function ensureCouponReadyBeforeBook() {
        if (!couponsEnabled()) {
            state.appliedCoupon = null;
            return { ok: true, code: null };
        }

        const draft = getCouponDraftCode();
        if (!draft) {
            state.appliedCoupon = null;
            setCouponUiStatus("none", "");
            return { ok: true, code: null };
        }

        const normDraft = normalizeCouponCode(draft);
        const applied = state.appliedCoupon;

        if (!applied || !applied.valid || normalizeCouponCode(applied.promoCode) !== normDraft) {
            setCouponUiStatus("pending", "");
            DevExpress.ui.notify(t("bookingEngine.couponApplyFirst"), "warning", 4000);
            return { ok: false, code: null };
        }

        try {
            const result = await validateCouponWithServer(draft);
            if (!result.valid) {
                state.appliedCoupon = null;
                setCouponUiStatus("invalid", result.message);
                DevExpress.ui.notify(result.message, "error", 4000);
                return { ok: false, code: null };
            }

            state.appliedCoupon = {
                promoCode: normalizeCouponCode(result.promoCode),
                discountAmount: result.discountAmount,
                valid: true
            };
            return { ok: true, code: state.appliedCoupon.promoCode };
        } catch (err) {
            const msg = localizeCouponMessage(err.message);
            state.appliedCoupon = null;
            setCouponUiStatus("invalid", msg);
            DevExpress.ui.notify(msg, "error", 4000);
            return { ok: false, code: null };
        }
    }

    function closeCheckoutDrawer() {
        clearTimeout(returningGuestLookupTimer);
        returningGuestLookupSeq += 1;
        state.appliedCoupon = null;
        state.couponUiStatus = "none";
        $("#beCheckoutHost").empty();
        $("body").removeClass("be-drawer-open");
    }

    function closeImageLightbox() {
        $("#beImageLightbox").remove();
        $(document).off("keydown.beLightbox");
    }

    function openImageLightbox(images, startIndex) {
        const list = (images || []).filter(Boolean);
        if (!list.length) {
            return;
        }

        closeImageLightbox();
        let idx = Math.max(0, Math.min(list.length - 1, startIndex || 0));
        const rtl = isArabic();

        const $lb = $("<div/>").attr("id", "beImageLightbox").addClass("be-lightbox").appendTo("body");
        const $dialog = $("<div/>").addClass("be-lightbox-dialog").appendTo($lb);
        const $head = $("<div/>").addClass("be-lightbox-head").appendTo($dialog);
        $("<span/>").addClass("be-lightbox-counter").appendTo($head);
        $("<button/>").attr("type", "button").addClass("be-lightbox-close").html("&times;").appendTo($head);

        const $stage = $("<div/>").addClass("be-lightbox-stage").appendTo($dialog);
        const $img = $("<img/>", { alt: "", class: "be-lightbox-img" }).appendTo($stage);

        const $prev = $("<button/>")
            .attr("type", "button")
            .addClass("be-lightbox-nav be-lightbox-prev")
            .attr("aria-label", "Previous")
            .html(rtl ? "&#8250;" : "&#8249;")
            .appendTo($stage);
        const $next = $("<button/>")
            .attr("type", "button")
            .addClass("be-lightbox-nav be-lightbox-next")
            .attr("aria-label", "Next")
            .html(rtl ? "&#8249;" : "&#8250;")
            .appendTo($stage);

        function paint() {
            $img.attr("src", list[idx]);
            $head
                .find(".be-lightbox-counter")
                .addClass("be-num-en")
                .text(`${formatWesternInteger(idx + 1)} / ${formatWesternInteger(list.length)}`);
            $prev.prop("disabled", list.length <= 1);
            $next.prop("disabled", list.length <= 1);
        }

        function step(delta) {
            if (list.length <= 1) {
                return;
            }
            idx = (idx + delta + list.length) % list.length;
            paint();
        }

        $prev.on("click", (e) => {
            e.stopPropagation();
            step(rtl ? 1 : -1);
        });
        $next.on("click", (e) => {
            e.stopPropagation();
            step(rtl ? -1 : 1);
        });
        $lb.find(".be-lightbox-close").on("click", closeImageLightbox);
        $lb.on("click", (e) => {
            if (e.target === $lb[0]) {
                closeImageLightbox();
            }
        });

        $(document).on("keydown.beLightbox", (e) => {
            if (e.key === "Escape") {
                closeImageLightbox();
            } else if (e.key === "ArrowLeft") {
                step(rtl ? 1 : -1);
            } else if (e.key === "ArrowRight") {
                step(rtl ? -1 : 1);
            }
        });

        paint();
    }

    function openCheckoutDrawer() {
        if (isSalesClosed()) {
            DevExpress.ui.notify(salesClosedMessage(), "warning", 4000);
            return;
        }
        const totals = getCartTotals();
        if (!totals.rooms) {
            DevExpress.ui.notify(t("bookingEngine.selectRooms"), "warning", 3000);
            return;
        }

        $("#beCheckoutHost").empty();
        $("body").addClass("be-drawer-open");

        state.appliedCoupon = null;
        state.couponUiStatus = "none";
        clearTimeout(returningGuestLookupTimer);
        returningGuestLookupSeq += 1;

        const isMobile = window.matchMedia("(max-width: 767px)").matches;
        const rtl = isArabic();
        const $overlay = $("<div/>")
            .addClass("be-checkout-overlay")
            .addClass(isMobile ? "be-checkout-overlay--modal" : "be-checkout-overlay--drawer")
            .toggleClass("be-checkout-overlay--rtl", rtl)
            .toggleClass("be-checkout-overlay--ltr", !rtl)
            .attr("dir", rtl ? "rtl" : "ltr")
            .appendTo("#beCheckoutHost");

        const $panel = $("<div/>")
            .addClass("be-checkout-panel")
            .addClass(isMobile ? "be-checkout-panel--modal" : "be-checkout-panel--drawer")
            .attr("dir", rtl ? "rtl" : "ltr")
            .appendTo($overlay);

        const $head = $("<div/>").addClass("be-drawer-head").appendTo($panel);
        $("<h2/>").text(t("bookingEngine.guestInfo")).appendTo($head);
        $("<button/>")
            .attr("type", "button")
            .addClass("be-drawer-close")
            .attr("aria-label", t("common.cancel"))
            .html("&times;")
            .on("click", closeCheckoutDrawer)
            .appendTo($head);

        const $layout = $("<div/>").addClass("be-drawer-layout").appendTo($panel);

        if (!isMobile) {
            const $summaryCol = $("<div/>").addClass("be-drawer-summary-col be-checkout-summary-refresh").appendTo($layout);
            buildSelectionSummaryHtml(totals, false).appendTo($summaryCol);
        }

        const $formCol = $("<div/>").addClass("be-drawer-form-col").appendTo($layout);

        if (isMobile) {
            buildSelectionSummaryHtml(totals, true)
                .addClass("be-checkout-summary-refresh")
                .prependTo($formCol);
        }
        const $grid = $("<div/>").addClass("be-checkout-grid").appendTo($formCol);
        $("<div/>").attr("id", "beWelcomeBanner").addClass("be-welcome-banner be-checkout-field--full").appendTo($grid);
        $("<div/>").attr("id", "beFirstName").appendTo($grid);
        $("<div/>").attr("id", "beLastName").appendTo($grid);
        $("<div/>").attr("id", "beEmail").addClass("be-checkout-field--full be-checkout-field--email").appendTo($grid);
        $("<div/>").attr("id", "bePhone").addClass("be-checkout-field--full be-checkout-field--phone").appendTo($grid);
        if (couponsEnabled()) {
            const $couponWrap = $("<div/>").addClass("be-coupon-wrap be-checkout-field--full").appendTo($grid);
            const $couponRow = $("<div/>").addClass("be-coupon-row").appendTo($couponWrap);
            const $inputWrap = $("<div/>").attr("id", "beCouponInputWrap").addClass("be-coupon-input-wrap").appendTo($couponRow);
            $("<div/>").attr("id", "beCouponCode").appendTo($inputWrap);
            $("<span/>").attr("id", "beCouponStatusIcon").addClass("be-coupon-status-icon").attr("hidden", true).appendTo($inputWrap);
            $("<div/>").attr("id", "beCouponApply").appendTo($couponRow);
            $("<div/>").attr("id", "beCouponMsg").addClass("be-coupon-msg").appendTo($couponWrap);
            $("<div/>").attr("id", "beCouponHint").addClass("be-coupon-hint").appendTo($couponWrap);
        }
        $("<div/>").attr("id", "beNotes").addClass("be-checkout-field--full").appendTo($grid);

        const showDeposit =
            state.profile &&
            state.profile.depositMode !== "none" &&
            (state.profile.onlineDepositEnabled || state.profile.depositMode === "optional");

        if (showDeposit) {
            $("<div/>").attr("id", "bePayDeposit").appendTo($formCol);
        }

        const $actions = $("<div/>").addClass("be-checkout-actions").appendTo($formCol);
        $("<div/>").appendTo($actions).dxButton({
            text: t("common.cancel"),
            stylingMode: "outlined",
            onClick: closeCheckoutDrawer
        });
        $("<div/>").appendTo($actions).dxButton({
            text: t("bookingEngine.bookNow"),
            type: "default",
            onClick: submitBooking
        });

        $("#beFirstName").dxTextBox({ label: t("bookingEngine.firstName") });
        $("#beLastName").dxTextBox({ label: t("bookingEngine.lastName") });
        $("#beEmail").dxTextBox({ label: t("bookingEngine.email"), mode: "email" });
        $("#bePhone").dxTextBox({
            label: t("bookingEngine.phone"),
            mode: "tel",
            onValueChanged(e) {
                scheduleReturningGuestLookup(e.value);
            }
        });
        if (couponsEnabled()) {
            $("#beCouponCode").dxTextBox({
                label: t("bookingEngine.couponCode"),
                onValueChanged(e) {
                    const next = String(e.value || "").trim();
                    const applied = state.appliedCoupon;
                    if (
                        applied &&
                        applied.valid &&
                        normalizeCouponCode(applied.promoCode) === normalizeCouponCode(next)
                    ) {
                        return;
                    }
                    state.appliedCoupon = null;
                    if (next) {
                        setCouponUiStatus("pending", "");
                    } else {
                        setCouponUiStatus("none", "");
                    }
                    refreshCheckoutSummaries();
                }
            });
            $("#beCouponApply").dxButton({
                text: t("bookingEngine.applyCoupon"),
                type: "default",
                stylingMode: "outlined",
                onClick: applyCouponFromCheckout
            });
            setCouponUiStatus("none", "");
        } else {
            state.appliedCoupon = null;
            state.couponUiStatus = "none";
        }

        $("#beNotes").dxTextArea({ label: t("bookingEngine.notes"), height: 72 });

        if (showDeposit) {
            $("#bePayDeposit").dxCheckBox({ text: t("bookingEngine.payDeposit"), value: false });
        }

        $overlay.on("click", (e) => {
            if (e.target === $overlay[0]) {
                closeCheckoutDrawer();
            }
        });
    }

    function showSuccessScreen(result) {
        closeCheckoutDrawer();
        state.cart = {};
        renderCartBar();

        $("#beShell").attr("hidden", true);
        $("#beCartBar").attr("hidden", true);
        $("body").removeClass("be-has-cart");

        const $host = $("#beSuccessHost").removeAttr("hidden").empty();
        const $wrap = $("<div/>").addClass("be-success-screen").appendTo($host);
        $("<h1/>").text(t("bookingEngine.confirmSuccess")).appendTo($wrap);
        $("<p/>")
            .addClass("be-success-no")
            .html(`${t("bookingEngine.confirmationNo")}: <strong>${result.reservationNo || ""}</strong>`)
            .appendTo($wrap);

        const codes = result.assignedRoomCodes || result.AssignedRoomCodes || [];
        if (codes.length) {
            $("<p/>")
                .html(`${t("bookingEngine.assignedRooms")}: <strong>${codes.join(", ")}</strong>`)
                .appendTo($wrap);
        } else if (result.assignedRoomCode) {
            $("<p/>")
                .html(`${t("bookingEngine.assignedRoom")}: <strong>${result.assignedRoomCode}</strong>`)
                .appendTo($wrap);
        }

        if (result.totalAmount) {
            $("<p/>")
                .html(
                    `${t("bookingEngine.total")}: <strong class="be-num-en">${formatMoney(result.totalAmount)} SAR</strong>`
                )
                .appendTo($wrap);
        }

        $("<p/>").text(result.message || "").appendTo($wrap);
        $("<button/>")
            .addClass("be-btn-primary")
            .text(t("bookingEngine.backToSearch"))
            .on("click", () => {
                $host.attr("hidden", true).empty();
                $("#beShell").removeAttr("hidden");
                runSearch();
            })
            .appendTo($wrap);
    }

    function renderOffers(offers) {
        clearCardCarousels();
        state.offers = (offers || []).map(normalizeOffer);
        pruneCart();
        const $host = $("#beOffers").empty();
        const $head = $("#beResultsHead").empty();

        if (!state.offers.length) {
            $head.attr("hidden", true);
            const $empty = $("<div/>").addClass("be-empty be-animate-in").appendTo($host);
            $("<p/>").text(t("bookingEngine.noResults")).appendTo($empty);
            $("<p/>").addClass("be-empty-hint").text(t("bookingEngine.noResultsHint")).appendTo($empty);
            renderCartBar();
            return;
        }

        $head.removeAttr("hidden").addClass("be-animate-in");
        $("<h2/>").addClass("be-results-title").text(t("bookingEngine.searchResults")).appendTo($head);
        $("<p/>").addClass("be-results-sub").text(t("bookingEngine.selectRoomsHint")).appendTo($head);

        const monthly = isMonthlyRental();
        const unitLabel = monthly ? t("bookingEngine.perMonth") : t("bookingEngine.perNight");

        state.offers.forEach((offer, index) => {
            const images =
                offer.images && offer.images.length ? offer.images.filter(Boolean) : [fallbackRoomImage()];
            const soldOut = (offer.availableUnits || 0) <= 0;
            let activeImg = images[0];
            const qty = getCartQty(offer.roomTypeId);
            const $card = $("<article/>")
                .addClass("be-offer-card be-offer-agoda be-card-animate")
                .css("animation-delay", `${Math.min(index, 8) * 0.06}s`)
                .attr("data-room-type", offer.roomTypeId)
                .toggleClass("be-offer-card--selected", qty > 0)
                .toggleClass("be-offer-card--sold-out", soldOut)
                .appendTo($host);

            const $galleryCol = $("<div/>").addClass("be-offer-gallery-col").appendTo($card);
            const $gallery = $("<button/>")
                .attr("type", "button")
                .addClass("be-gallery-beacon")
                .attr("aria-label", t("bookingEngine.viewPhotos"))
                .appendTo($galleryCol);

            if (images.length > 1) {
                const $slides = $("<div/>").addClass("be-gallery-slides").appendTo($gallery);
                images.forEach((src, i) => {
                    $("<img/>", {
                        class: "be-gallery-slide" + (i === 0 ? " is-active" : ""),
                        src,
                        alt: offerDisplayName(offer),
                        loading: i === 0 ? "lazy" : "lazy"
                    }).appendTo($slides);
                });
                let slideIdx = 0;
                const timer = setInterval(() => {
                    slideIdx = (slideIdx + 1) % images.length;
                    $slides.find(".be-gallery-slide").removeClass("is-active").eq(slideIdx).addClass("is-active");
                    activeImg = images[slideIdx];
                }, 4500);
                cardCarouselTimers.push(timer);
            } else {
                $("<img/>", {
                    class: "be-gallery-main",
                    src: images[0],
                    alt: offerDisplayName(offer),
                    loading: "lazy"
                }).appendTo($gallery);
            }

            $("<span/>").addClass("be-gallery-zoom").text(t("bookingEngine.viewPhotos")).appendTo($gallery);
            if (images.length > 1) {
                $("<span/>")
                    .addClass("be-gallery-count be-num-en")
                    .text(formatWesternInteger(images.length))
                    .appendTo($gallery);
            }

            if (soldOut) {
                $("<span/>").addClass("be-sold-out-badge").text(t("bookingEngine.soldOut")).appendTo($gallery);
            }

            $gallery.on("click", () => {
                const start = Math.max(0, images.indexOf(activeImg));
                openImageLightbox(images, start);
            });

            const $mainCol = $("<div/>").addClass("be-offer-main-col").appendTo($card);
            const $info = $("<div/>").addClass("be-offer-info").appendTo($mainCol);
            $("<h3/>").addClass("be-offer-title").text(offerDisplayName(offer)).appendTo($info);

            const $meta = $("<div/>").addClass("be-offer-meta").appendTo($info);
            if (offer.areaSqm) {
                const areaLabel = isArabic()
                    ? `${formatWesternInteger(offer.areaSqm)} م²`
                    : `${formatWesternInteger(offer.areaSqm)} m²`;
                $("<span/>").addClass("be-offer-meta-item").text(areaLabel).appendTo($meta);
            }
            if (!soldOut) {
                $("<span/>")
                    .addClass("be-offer-meta-item be-offer-meta-item--avail")
                    .text(t("bookingEngine.available", formatWesternInteger(offer.availableUnits)))
                    .appendTo($meta);
            } else {
                $("<span/>").addClass("be-offer-meta-item be-offer-meta-item--sold").text(t("bookingEngine.soldOut")).appendTo($meta);
            }

            appendOfferServiceIcons($info, offer.services);

            if (offer.description) {
                $("<p/>").addClass("be-offer-desc").text(offer.description).appendTo($info);
            }

            const facilities = offer.facilities && offer.facilities.length ? offer.facilities : [];
            if (facilities.length) {
                $("<div/>").addClass("be-offer-facilities-head").text(t("bookingEngine.facilities")).appendTo($info);
                const $facList = $("<ul/>").addClass("be-offer-facilities").appendTo($info);
                facilities.slice(0, 8).forEach((f) => {
                    const label = facilityDisplayName(f);
                    if (!label) {
                        return;
                    }
                    $("<li/>")
                        .addClass("be-offer-facility")
                        .append($("<i/>").addClass("be-offer-facility-icon dx-icon dx-icon-check"))
                        .append($("<span/>").text(label))
                        .appendTo($facList);
                });
            } else {
                const $highlights = $("<div/>").addClass("be-highlights").appendTo($info);
                (offer.highlights || []).slice(0, 4).forEach((h) => {
                    $("<span/>").addClass("be-highlight-pill").text(h).appendTo($highlights);
                });
            }

            const $bookCol = $("<div/>").addClass("be-offer-book-col").appendTo($card);
            $("<div/>")
                .addClass("be-offer-rate")
                .append($("<span/>").addClass("be-offer-rate-label").text(unitLabel))
                .append($("<strong/>").addClass("be-num-en").text(`${formatMoney(offer.pricePerNight)} SAR`))
                .appendTo($bookCol);

            if (!soldOut) {
                $("<div/>")
                    .addClass("be-offer-total-hint be-num-en")
                    .text(`${formatMoney(offer.grandTotal)} SAR ${t("bookingEngine.summary")}`)
                    .appendTo($bookCol);
            }

            const $qty = $("<div/>").addClass("be-qty-stepper").toggleClass("be-qty-stepper--disabled", soldOut).appendTo($bookCol);
            $("<button/>")
                .attr("type", "button")
                .addClass("be-qty-btn be-qty-minus")
                .text("−")
                .prop("disabled", soldOut || qty <= 0)
                .on("click", (e) => {
                    e.stopPropagation();
                    setCartQty(offer, getCartQty(offer.roomTypeId) - 1);
                })
                .appendTo($qty);
            $("<span/>")
                .addClass("be-qty-value be-num-en")
                .attr("data-room-type", offer.roomTypeId)
                .text(formatWesternInteger(qty))
                .appendTo($qty);
            $("<button/>")
                .attr("type", "button")
                .addClass("be-qty-btn be-qty-plus")
                .text("+")
                .prop("disabled", soldOut || qty >= offer.availableUnits)
                .on("click", (e) => {
                    e.stopPropagation();
                    setCartQty(offer, getCartQty(offer.roomTypeId) + 1);
                })
                .appendTo($qty);

            if (soldOut) {
                $("<p/>").addClass("be-offer-sold-hint").text(t("bookingEngine.soldOutHint")).appendTo($bookCol);
            }
        });

        renderCartBar();
    }

    async function submitBooking() {
        const totals = getCartTotals();
        if (!totals.rooms) {
            DevExpress.ui.notify(t("bookingEngine.selectRooms"), "warning", 3000);
            return;
        }

        const firstName = $("#beFirstName").dxTextBox("instance").option("value");
        const lastName = $("#beLastName").dxTextBox("instance").option("value");
        if (!firstName || !lastName) {
            DevExpress.ui.notify(t("bookingEngine.nameRequired"), "warning", 3000);
            return;
        }

        let payDeposit = false;
        const $dep = $("#bePayDeposit");
        if ($dep.length && $dep.data("dxCheckBox")) {
            payDeposit = !!$dep.dxCheckBox("instance").option("value");
        }

        const lines = totals.lines.map((l) => ({
            roomTypeId: l.offer.roomTypeId,
            quantity: l.quantity
        }));

        const hadDraftCoupon = couponsEnabled() && !!getCouponDraftCode();
        const couponCheck = await ensureCouponReadyBeforeBook();
        if (!couponCheck.ok) {
            return;
        }

        try {
            loadPanel.show();
            const result = await svc.confirm({
                hotelCode: state.hotelCode,
                lines,
                fromDate: svc.formatLocalDateParam(state.checkIn),
                toDate: svc.formatLocalDateParam(state.checkOut),
                rentalType: state.rentalType,
                firstName,
                lastName,
                email: $("#beEmail").dxTextBox("instance").option("value"),
                phone: $("#bePhone").dxTextBox("instance").option("value"),
                notes: $("#beNotes").dxTextArea("instance").option("value"),
                payDepositNow: payDeposit,
                couponCode: couponCheck.code
            });

            const discountApplied = Number(result.discountAmount ?? result.DiscountAmount ?? 0);
            const appliedCode = result.appliedCouponCode ?? result.AppliedCouponCode ?? null;
            if (hadDraftCoupon && (!appliedCode || discountApplied <= 0)) {
                DevExpress.ui.notify(t("bookingEngine.couponNotAppliedOnBook"), "warning", 4500);
            }

            showSuccessScreen(result);
        } catch (err) {
            DevExpress.ui.notify(err.message || "Booking failed", "error", 4000);
        } finally {
            loadPanel.hide();
        }
    }

    function normalizeSearchResult(raw) {
        let root = raw && typeof raw === "object" ? raw : {};
        if (root.data !== undefined && root.offers === undefined && root.Offers === undefined && root.hotel === undefined && root.Hotel === undefined) {
            root = root.data;
        }

        const hotelRaw = root.hotel ?? root.Hotel ?? null;
        const offersRaw = root.offers ?? root.Offers ?? [];
        return {
            hotel: hotelRaw ? normalizeProfile(hotelRaw) : null,
            offers: Array.isArray(offersRaw) ? offersRaw.map(normalizeOffer) : []
        };
    }

    async function runSearch() {
        if (!state.hotelCode) {
            return;
        }
        if (isSalesClosed()) {
            applySalesClosedUi();
            return;
        }
        try {
            loadPanel.show();
            const data = await svc.search({
                hotelCode: state.hotelCode,
                fromDate: svc.formatLocalDateParam(state.checkIn),
                toDate: svc.formatLocalDateParam(state.checkOut),
                rentalType: state.rentalType
            });
            const parsed = normalizeSearchResult(data);
            if (parsed.hotel) {
                state.profile = parsed.hotel;
                applyRentalTypeFromProfile();
                const rentalWasVisible = $("#beRentalType").length > 0;
                const rentalShouldVisible = showRentalTypeDropdown();
                if (rentalWasVisible !== rentalShouldVisible) {
                    disposeSearchForm();
                    buildSearchForm();
                }
                applyTheme(state.profile);
                renderBrand(state.profile);
                renderPromoBanner();
            }
            applySalesClosedUi();
            if (isSalesClosed()) {
                return;
            }
            renderOffers(parsed.offers);
        } catch (err) {
            DevExpress.ui.notify(err.message || "Search failed", "error", 4000);
        } finally {
            loadPanel.hide();
        }
    }

    async function refreshHotelContext() {
        try {
            loadPanel.show();
            const profile = await svc.loadProfile(state.hotelCode);
            state.profile = normalizeProfile(profile);
            applyRentalTypeFromProfile();
            applyTheme(state.profile);
            renderBrand(state.profile);
            renderFilterHtml(state.profile);
            applySalesClosedUi();
            const rentalWasVisible = $("#beRentalType").length > 0;
            const rentalShouldVisible = showRentalTypeDropdown();
            if (rentalWasVisible !== rentalShouldVisible) {
                disposeSearchForm();
                buildSearchForm();
            } else {
                syncSearchEditors();
            }
            if (!isSalesClosed()) {
                await runSearch();
            }
        } catch (err) {
            DevExpress.ui.notify(err.message || "Hotel not found", "error", 4000);
        } finally {
            loadPanel.hide();
        }
    }

    async function initPage() {
        state.hotelCode = getQueryHotel();
        if (!state.hotelCode) {
            DevExpress.ui.notify("Missing hotel in URL (?hotel=code)", "warning", 5000);
            return;
        }

        try {
            loadPanel.show();
            const hotelsRaw = await svc.loadHotels();
            state.hotelList = normalizeHotelList(hotelsRaw);
            if (!state.hotelList.some((h) => h.code.toLowerCase() === state.hotelCode.toLowerCase())) {
                state.hotelList.unshift({
                    code: state.hotelCode,
                    name: state.hotelCode,
                    nameEn: state.hotelCode
                });
            }

            const profile = await svc.loadProfile(state.hotelCode);
            state.profile = normalizeProfile(profile);
            applyRentalTypeFromProfile();
            applyTheme(state.profile);
            renderBrand(state.profile);
            buildSearchForm();
            renderFilterHtml(state.profile);
            renderPromoBanner();
            applySalesClosedUi();
            if (!isSalesClosed()) {
                await runSearch();
            }
        } catch (err) {
            DevExpress.ui.notify(err.message || "Hotel not found", "error", 4000);
        } finally {
            loadPanel.hide();
        }
    }

    $(function () {
        window.Zaaer.LocalizationService.init();
        loadPanel = $("#beLoadPanel")
            .dxLoadPanel({
                visible: false,
                showIndicator: true,
                shading: true,
                position: { of: window }
            })
            .dxLoadPanel("instance");

        $("#beLangToggle").on("click", () => {
            if (langSwitching) {
                return;
            }
            langSwitching = true;
            try {
                refreshAfterLanguageChange();
            } finally {
                langSwitching = false;
            }
        });

        $("#beCartClearBtn").on("click", clearCartSelections);

        initPage();
    });
})(window, jQuery, DevExpress);
