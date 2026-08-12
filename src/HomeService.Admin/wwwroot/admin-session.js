window.weleAdminSession = {
    setCookie: function (name, value, maxAgeSeconds) {
        const secure = window.location.protocol === "https:" ? "; secure" : "";
        document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; path=/; max-age=${maxAgeSeconds}; samesite=strict${secure}`;
    },
    clearCookie: function (name) {
        const secure = window.location.protocol === "https:" ? "; secure" : "";
        document.cookie = `${encodeURIComponent(name)}=; path=/; max-age=0; samesite=strict${secure}`;
    }
};
