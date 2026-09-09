(() => {
    const cookieName = document.documentElement.dataset.timeZoneCookie;
    const canReload =
        document.documentElement.dataset.timeZoneReload === "true";

    if (!cookieName ||
        typeof Intl === "undefined" ||
        typeof Intl.DateTimeFormat !== "function") {
        return;
    }

    let timeZone;

    try {
        timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    } catch {
        return;
    }

    if (!timeZone || timeZone.length > 100) {
        return;
    }

    const readCookie = () => {
        const prefix = `${cookieName}=`;
        const value = document.cookie
            .split("; ")
            .find(cookie => cookie.startsWith(prefix))
            ?.slice(prefix.length);

        if (!value) {
            return "";
        }

        try {
            return decodeURIComponent(value);
        } catch {
            return "";
        }
    };

    if (readCookie() === timeZone) {
        return;
    }

    const secure = window.location.protocol === "https:" ? "; Secure" : "";
    document.cookie =
        `${cookieName}=${encodeURIComponent(timeZone)}` +
        `; Path=/; Max-Age=31536000; SameSite=Lax${secure}`;

    // Reload only GET documents and only when the browser accepted the cookie.
    // This avoids loops and never resubmits a POST response.
    if (canReload && readCookie() === timeZone) {
        window.location.reload();
    }
})();
