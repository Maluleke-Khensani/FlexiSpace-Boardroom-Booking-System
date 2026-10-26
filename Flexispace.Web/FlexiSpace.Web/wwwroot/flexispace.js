(() => {
    function initScrollProgress() {
        const bar = document.querySelector('.fs-scroll-progress__bar');
        if (!bar || bar.dataset.bound) return;
        bar.dataset.bound = '1';

        const update = () => {
            const scrollTop = window.scrollY || document.documentElement.scrollTop;
            const height = document.documentElement.scrollHeight - window.innerHeight;
            bar.style.width = (height > 0 ? Math.min(100, (scrollTop / height) * 100) : 0) + '%';
        };

        window.addEventListener('scroll', update, { passive: true });
        update();
    }

    function init() {
        initScrollProgress();
    }

    document.addEventListener('DOMContentLoaded', init);
    document.addEventListener('enhancedload', init);
})();

window.flexispacePrivacy = {
    hasAccepted: (key) => window.localStorage.getItem(key) === '1',
    accept: (key) => window.localStorage.setItem(key, '1')
};

window.flexispaceAlerts = {
    getUnread: (key) => {
        try {
            const raw = window.localStorage.getItem(key);
            const parsed = raw ? JSON.parse(raw) : [];
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    },
    setUnread: (key, ids) => window.localStorage.setItem(key, JSON.stringify(ids || [])),
    getFeed: (key) => window.localStorage.getItem(key) || "",
    setFeed: (key, json) => window.localStorage.setItem(key, json || "[]")
};

let lastErrorKey = '';

window.flexispaceUi = {
    scrollTo: (id) => {
        if (!id) return;
        const el = document.getElementById(id);
        if (!el) return;
        const header = document.querySelector('.fs-page-header');
        const pad = (header ? header.getBoundingClientRect().height : 0) + 16;
        const top = Math.max(0, window.scrollY + el.getBoundingClientRect().top - pad);
        window.scrollTo({ top, behavior: 'smooth' });
        if (typeof el.focus === 'function' && (el.tagName === 'INPUT' || el.tagName === 'SELECT' || el.tagName === 'TEXTAREA')) {
            try { el.focus({ preventScroll: true }); } catch { el.focus(); }
        }
    },
    scrollToFirstError: () => {
        const banner = document.querySelector('#fs-error-banner');
        const bannerText = banner && (banner.textContent || '').trim();
        const invalid = document.querySelector('.is-invalid');
        const others = Array.from(document.querySelectorAll('.fs-error'))
            .filter((el) => (el.textContent || '').trim().length > 0);
        const target = bannerText ? banner : invalid || others[0] || null;
        if (!target) {
            lastErrorKey = '';
            return;
        }
        const header = document.querySelector('.fs-page-header');
        const pad = (header ? header.getBoundingClientRect().height : 0) + 16;
        const rect = target.getBoundingClientRect();
        const visible = rect.top >= pad - 8 && rect.bottom <= window.innerHeight;
        const key = (target.id || target.className || '') + '|' + (target.textContent || '').trim();
        if (key === lastErrorKey && visible) return;
        lastErrorKey = key;
        const top = target.id === 'fs-error-banner'
            ? 0
            : Math.max(0, window.scrollY + rect.top - pad);
        window.scrollTo({ top, behavior: 'smooth' });
        document.documentElement.scrollTop = top;
        document.body.scrollTop = top;
    },
    goBack: (fallback) => {
        if (window.history.length > 1) window.history.back();
        else window.location.href = fallback || '/home';
    }
};

window.flexispaceGuest = {
    is: () => {
        try { return window.sessionStorage.getItem('flexispace.guest') === '1'; } catch { return false; }
    },
    set: (on) => {
        try {
            if (on) window.sessionStorage.setItem('flexispace.guest', '1');
            else window.sessionStorage.removeItem('flexispace.guest');
        } catch { }
    }
};

(() => {
    let timer = 0;
    const watch = () => {
        if (timer) window.clearTimeout(timer);
        timer = window.setTimeout(() => window.flexispaceUi.scrollToFirstError(), 80);
    };
    const start = () => {
        if (document.body && !document.body.dataset.errorWatch) {
            document.body.dataset.errorWatch = '1';
            new MutationObserver(watch).observe(document.body, {
                childList: true,
                subtree: true,
                characterData: true,
                attributes: true,
                attributeFilter: ['class']
            });
        }
    };
    document.addEventListener('DOMContentLoaded', start);
    document.addEventListener('enhancedload', start);
})();
