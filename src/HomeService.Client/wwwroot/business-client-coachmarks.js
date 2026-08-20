(() => {
    const seenPrefix = "wele:business-client:coachmark:seen:";
    const postponedPrefix = "wele:business-client:coachmark:postponed:";
    let active = null;
    let scanTimer = 0;

    const safeGet = (storage, key) => {
        try { return storage.getItem(key); } catch { return null; }
    };

    const safeSet = (storage, key) => {
        try { storage.setItem(key, "1"); } catch { }
    };

    const isVisible = (element) => {
        if (!(element instanceof HTMLElement)) return false;
        const style = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.display !== "none"
            && style.visibility !== "hidden"
            && Number(style.opacity) !== 0
            && rect.width > 8
            && rect.height > 8;
    };

    const isAvailable = (element) => !element.matches(":disabled, [aria-disabled='true']");

    const queueScan = () => {
        window.clearTimeout(scanTimer);
        scanTimer = window.setTimeout(scan, 180);
    };

    const getCandidates = () => Array.from(document.querySelectorAll("[data-coachmark-key]"))
        .filter(isVisible)
        .filter(isAvailable)
        .filter((target) => {
            const key = target.dataset.coachmarkKey;
            return key
                && safeGet(window.localStorage, seenPrefix + key) !== "1"
                && safeGet(window.sessionStorage, postponedPrefix + key) !== "1";
        })
        .sort((left, right) => Number(left.dataset.coachmarkOrder || 999) - Number(right.dataset.coachmarkOrder || 999));

    const scan = () => {
        if (active || document.hidden) return;
        const target = getCandidates()[0];
        if (target) show(target);
    };

    const positionPanel = () => {
        if (!active) return;
        const rect = active.target.getBoundingClientRect();
        const panel = active.panel;
        const margin = 18;
        panel.style.top = "auto";
        panel.style.bottom = "auto";
        panel.style.left = `${Math.max(16, Math.min(window.innerWidth - panel.offsetWidth - 16, rect.left + (rect.width - panel.offsetWidth) / 2))}px`;

        if (rect.top + rect.height / 2 < window.innerHeight / 2) {
            panel.style.bottom = `${margin}px`;
        } else {
            panel.style.top = `${margin}px`;
        }
    };

    const close = ({ seen = false, postponed = false } = {}) => {
        if (!active) return;
        const current = active;
        active = null;

        if (seen) safeSet(window.localStorage, seenPrefix + current.key);
        if (postponed) safeSet(window.sessionStorage, postponedPrefix + current.key);

        current.target.removeEventListener("click", current.onTargetClick);
        window.removeEventListener("resize", current.onViewportChange);
        window.removeEventListener("scroll", current.onViewportChange, true);
        document.removeEventListener("keydown", current.onKeyDown);
        current.target.classList.remove("wele-coachmark-target");
        current.target.style.position = current.previous.position;
        current.target.style.zIndex = current.previous.zIndex;
        current.backdrop.remove();
        current.panel.remove();
        queueScan();
    };

    const createButton = (label, className, onClick) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.textContent = label;
        button.addEventListener("click", onClick);
        return button;
    };

    const show = (target) => {
        const key = target.dataset.coachmarkKey;
        if (!key) return;

        const previous = {
            position: target.style.position,
            zIndex: target.style.zIndex
        };

        if (window.getComputedStyle(target).position === "static") {
            target.style.position = "relative";
        }
        target.style.zIndex = "10002";
        target.classList.add("wele-coachmark-target");

        const backdrop = document.createElement("div");
        backdrop.className = "wele-coachmark-backdrop";
        backdrop.setAttribute("aria-hidden", "true");

        const panel = document.createElement("section");
        panel.className = "wele-coachmark-panel";
        panel.setAttribute("role", "dialog");
        panel.setAttribute("aria-live", "polite");

        const step = document.createElement("span");
        step.className = "wele-coachmark-step";
        step.textContent = "Première visite";

        const title = document.createElement("strong");
        title.textContent = target.dataset.coachmarkTitle || "À vous de jouer";

        const body = document.createElement("p");
        body.textContent = target.dataset.coachmarkBody || "Découvrez cette fonctionnalité.";

        const hint = document.createElement("small");
        hint.textContent = target.matches("button, a, input, select, textarea, label")
            ? "Cliquez sur l’élément entouré pour continuer."
            : "La zone concernée est entourée. Explorez-la ou cliquez sur Compris.";

        const actions = document.createElement("div");
        actions.className = "wele-coachmark-actions";
        actions.append(
            createButton("Plus tard", "wele-coachmark-later", () => close({ postponed: true })),
            createButton("Compris", "wele-coachmark-done", () => close({ seen: true }))
        );

        panel.append(step, title, body, hint, actions);
        document.body.append(backdrop, panel);

        const onTargetClick = () => close({ seen: true });
        const onViewportChange = () => window.requestAnimationFrame(positionPanel);
        const onKeyDown = (event) => {
            if (event.key === "Escape") close({ postponed: true });
        };

        active = { key, target, backdrop, panel, previous, onTargetClick, onViewportChange, onKeyDown };
        target.addEventListener("click", onTargetClick, { once: true });
        window.addEventListener("resize", onViewportChange);
        window.addEventListener("scroll", onViewportChange, true);
        document.addEventListener("keydown", onKeyDown);
        positionPanel();
    };

    const observer = new MutationObserver(queueScan);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    document.addEventListener("DOMContentLoaded", queueScan);
    document.addEventListener("enhancedload", queueScan);
    window.addEventListener("popstate", queueScan);
    queueScan();
})();
