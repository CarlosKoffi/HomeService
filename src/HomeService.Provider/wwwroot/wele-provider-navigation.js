window.weleProviderNavigation = (() => {
    let observer;
    let trackedLinks = [];
    let headerScrollBound = false;
    let mobileMenuBound = false;

    const closeMobileMenu = () => {
        const toggle = document.getElementById("menu-toggle");
        if (toggle instanceof HTMLInputElement) {
            toggle.checked = false;
        }
    };

    const bindMobileMenu = () => {
        if (mobileMenuBound) {
            return;
        }

        document.addEventListener("click", (event) => {
            if (event.target instanceof Element
                && event.target.closest(".site-nav a, .brand-logo")) {
                closeMobileMenu();
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                closeMobileMenu();
            }
        });

        window.addEventListener("resize", () => {
            if (window.innerWidth > 980) {
                closeMobileMenu();
            }
        }, { passive: true });

        mobileMenuBound = true;
    };

    const updateHeaderState = () => {
        const header = document.querySelector(".site-header.theme-premium");
        if (!header) {
            return;
        }

        const isLanding = Boolean(document.querySelector(".premium-b2b"));
        const isScrolled = !isLanding || window.scrollY > 18;
        header.classList.toggle("is-transparent", isLanding && !isScrolled);
        header.classList.toggle("is-scrolled", isScrolled);
    };

    const setActive = (sectionId) => {
        trackedLinks.forEach((link) => {
            const isActive = link.dataset.sectionNav === sectionId;
            link.classList.toggle("section-active", isActive);
            if (isActive) {
                link.setAttribute("aria-current", "true");
            } else {
                link.removeAttribute("aria-current");
            }
        });
    };

    const getCurrentSection = (sections) => {
        const anchorLine = 112;
        let current = sections[0]?.id ?? "home";

        sections.forEach((section) => {
            if (section.getBoundingClientRect().top <= anchorLine) {
                current = section.id;
            }
        });

        return current;
    };

    const init = () => {
        updateHeaderState();
        bindMobileMenu();

        if (!headerScrollBound) {
            window.addEventListener("scroll", updateHeaderState, { passive: true });
            window.addEventListener("resize", updateHeaderState, { passive: true });
            headerScrollBound = true;
        }

        trackedLinks = Array.from(document.querySelectorAll("[data-section-nav]"));

        if (observer) {
            observer.disconnect();
        }

        if (trackedLinks.length === 0) {
            return;
        }

        const sectionIds = trackedLinks.map((link) => link.dataset.sectionNav);
        const sections = sectionIds
            .map((id) => document.getElementById(id))
            .filter(Boolean);

        if (sections.length === 0) {
            return;
        }

        const update = () => setActive(getCurrentSection(sections));

        trackedLinks.forEach((link) => {
            link.addEventListener("click", () => {
                const sectionId = link.dataset.sectionNav;
                if (sectionId) {
                    setActive(sectionId);
                }
            });
        });

        observer = new IntersectionObserver(update, {
            rootMargin: "-92px 0px -58% 0px",
            threshold: [0, 0.2, 0.45, 0.7]
        });

        sections.forEach((section) => observer.observe(section));
        update();
    };

    return { init, closeMobileMenu };
})();
