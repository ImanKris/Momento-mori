/**
 * RoboStore Scroll Effects
 * Subtle scroll-driven visual effects for backgrounds and elements
 */
(function () {
    'use strict';

    const SCROLL_BG_CLASS = 'scroll-bg';
    let ticking = false;
    let lastScrollY = 0;

    // Create scroll background layers
    function createScrollBackground() {
        // Remove existing if any
        const existing = document.querySelector('.' + SCROLL_BG_CLASS);
        if (existing) existing.remove();

        const bg = document.createElement('div');
        bg.className = SCROLL_BG_CLASS;
        bg.innerHTML = `
            <div class="scroll-bg-layer scroll-bg-gradient"></div>
        `;
        document.body.insertBefore(bg, document.body.firstChild);
    }

    // Handle scroll events
    function handleScroll() {
        if (!ticking) {
            window.requestAnimationFrame(() => {
                updateScrollEffects();
                ticking = false;
            });
            ticking = true;
        }
    }

    // Update visual effects based on scroll position
    function updateScrollEffects() {
        const scrollY = window.scrollY;
        const docHeight = document.documentElement.scrollHeight;
        const winHeight = window.innerHeight;
        const scrollPercent = Math.min(scrollY / (docHeight - winHeight), 1);

        const bg = document.querySelector('.' + SCROLL_BG_CLASS);
        if (!bg) return;

        const layers = bg.querySelectorAll('.scroll-bg-layer');

        layers.forEach((layer, index) => {
            const speed = (index + 1) * 0.05;
            const offset = scrollY * speed;
            const opacity = 1 - (scrollPercent * 0.3);

            layer.style.transform = `translateY(${offset}px)`;
            layer.style.opacity = opacity;
        });

        // Add/remove scrolled class for navbar
        const navbar = document.querySelector('.navbar');
        if (navbar) {
            if (scrollY > 10) {
                navbar.classList.add('navbar-scrolled');
            } else {
                navbar.classList.remove('navbar-scrolled');
            }
        }

        lastScrollY = scrollY;
    }

    // Reveal elements on scroll
    function initScrollReveal() {
        const elements = document.querySelectorAll('[data-reveal]');

        if (!elements.length) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const delay = entry.target.dataset.revealDelay || 0;
                    setTimeout(() => {
                        entry.target.classList.add('revealed');
                    }, delay);
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        });

        elements.forEach(el => {
            el.classList.add('reveal-ready');
            observer.observe(el);
        });
    }

    // Smooth scroll to elements
    function initSmoothScroll() {
        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', function (e) {
                const href = this.getAttribute('href');
                if (href === '#') return;

                const target = document.querySelector(href);
                if (target) {
                    e.preventDefault();
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            });
        });
    }

    // Parallax effect for hero sections
    function initParallax() {
        const parallaxElements = document.querySelectorAll('[data-parallax]');

        if (!parallaxElements.length) return;

        window.addEventListener('scroll', () => {
            const scrollY = window.scrollY;

            parallaxElements.forEach(el => {
                const speed = parseFloat(el.dataset.parallax) || 0.5;
                const offset = scrollY * speed;
                el.style.transform = `translateY(${offset}px)`;
            });
        }, { passive: true });
    }

    // Initialize on DOM ready
    function init() {
        createScrollBackground();
        initScrollReveal();
        initSmoothScroll();

        // Scroll event listener (passive for performance)
        window.addEventListener('scroll', handleScroll, { passive: true });

        // Initial update
        updateScrollEffects();

        // Handle resize
        window.addEventListener('resize', () => {
            updateScrollEffects();
        });
    }

    // Export API
    window.ScrollEffects = {
        init: init,
        refresh: updateScrollEffects
    };

    // Auto-init when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
