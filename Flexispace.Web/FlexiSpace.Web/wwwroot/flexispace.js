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

// Saves bytes from .NET (a byte[] arrives here as a Uint8Array) as a file
// download, e.g. the bookings and audit CSV exports on /reports.
window.flexispaceDownload = (fileName, contentType, bytes) => {
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};

window.flexispacePrivacy = {
    hasAccepted: (key) => window.localStorage.getItem(key) === '1',
    accept: (key) => window.localStorage.setItem(key, '1')
};
