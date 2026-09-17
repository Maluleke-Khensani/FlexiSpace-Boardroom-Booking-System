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
