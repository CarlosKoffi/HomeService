(function () {
    "use strict";

    var consentKey = "wele_analytics_consent_v1";

    if (window.__weleAnalyticsInitialized) {
        return;
    }

    var measurementId = document
        .querySelector('meta[name="wele-google-analytics-id"]')
        ?.getAttribute("content")
        ?.trim();

    if (!measurementId) {
        return;
    }

    window.__weleAnalyticsInitialized = true;
    window.dataLayer = window.dataLayer || [];
    window.gtag = window.gtag || function () {
        window.dataLayer.push(arguments);
    };

    window.gtag("consent", "default", {
        ad_storage: "denied",
        ad_user_data: "denied",
        ad_personalization: "denied",
        analytics_storage: "denied",
        wait_for_update: 500
    });

    var lastTrackedPage = "";
    var analyticsEnabled = false;
    var googleTagLoaded = false;

    function getConsentChoice() {
        try {
            return window.localStorage.getItem(consentKey);
        } catch (_) {
            return null;
        }
    }

    function saveConsentChoice(value) {
        try {
            window.localStorage.setItem(consentKey, value);
        } catch (_) {
            // Le choix reste valable pour la page courante si le stockage est bloqué.
        }
    }

    function loadGoogleTag() {
        if (googleTagLoaded) {
            return;
        }

        googleTagLoaded = true;
        var script = document.createElement("script");
        script.async = true;
        script.src = "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(measurementId);
        document.head.appendChild(script);

        window.gtag("js", new Date());
        window.gtag("config", measurementId, {
            send_page_view: false
        });
    }

    function enableAnalytics() {
        analyticsEnabled = true;
        window.gtag("consent", "update", {
            analytics_storage: "granted"
        });
        loadGoogleTag();
        trackPage();
    }

    function trackPage() {
        if (!analyticsEnabled) {
            return;
        }

        var pagePath = window.location.pathname + window.location.search;
        if (pagePath === lastTrackedPage) {
            return;
        }

        lastTrackedPage = pagePath;
        window.gtag("event", "page_view", {
            page_title: document.title,
            page_location: window.location.href,
            page_path: pagePath
        });

        var serviceMatch = window.location.pathname.match(/^\/services\/([^/]+)\/?$/i);
        if (serviceMatch) {
            window.gtag("event", "service_page_view", {
                service_slug: decodeURIComponent(serviceMatch[1]),
                page_location: window.location.href
            });
        }
    }

    function trackApplicationClick(link) {
        if (!analyticsEnabled) {
            return;
        }

        var path = link.pathname.toLowerCase();
        if (path !== "/telecharger/android" && path !== "/telecharger/ios") {
            return;
        }

        window.gtag("event", "app_download_click", {
            app_platform: path.endsWith("/android") ? "android" : "ios",
            link_url: link.href,
            page_location: window.location.href
        });
    }

    function dismissConsentBanner(banner, accepted) {
        saveConsentChoice(accepted ? "accepted" : "refused");
        banner.remove();

        if (accepted) {
            enableAnalytics();
            return;
        }

        window.gtag("consent", "update", {
            analytics_storage: "denied"
        });
    }

    function showConsentBanner() {
        var banner = document.createElement("aside");
        banner.className = "wele-consent";
        banner.setAttribute("role", "dialog");
        banner.setAttribute("aria-label", "Choix des cookies de mesure d'audience");
        banner.innerHTML = [
            '<div class="wele-consent__copy">',
            '<strong>Votre expérience, toujours plus simple.</strong>',
            '<span>Avec votre accord, Wélé utilise Google Analytics pour comprendre les pages consultées et améliorer le site.</span>',
            '</div>',
            '<div class="wele-consent__actions">',
            '<button type="button" class="wele-consent__refuse">Refuser</button>',
            '<button type="button" class="wele-consent__accept">Accepter</button>',
            '</div>'
        ].join("");

        banner.querySelector(".wele-consent__refuse").addEventListener("click", function () {
            dismissConsentBanner(banner, false);
        });
        banner.querySelector(".wele-consent__accept").addEventListener("click", function () {
            dismissConsentBanner(banner, true);
        });

        document.body.appendChild(banner);
    }

    function initializeConsent() {
        var choice = getConsentChoice();
        if (choice === "accepted") {
            enableAnalytics();
            return;
        }

        if (choice !== "refused") {
            showConsentBanner();
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeConsent, { once: true });
    } else {
        initializeConsent();
    }

    document.addEventListener("blazor:enhancedload", function () {
        window.setTimeout(trackPage, 0);
    });

    document.addEventListener("click", function (event) {
        if (!(event.target instanceof Element)) {
            return;
        }

        var link = event.target.closest("a[href]");
        if (link instanceof HTMLAnchorElement) {
            trackApplicationClick(link);
        }
    });
})();
