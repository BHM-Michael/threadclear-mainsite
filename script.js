// ThreadClear Marketing Site JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Mobile Navigation Toggle
    const mobileToggle = document.querySelector('.nav-mobile-toggle');
    const navLinks = document.querySelector('.nav-links');
    const navActions = document.querySelector('.nav-actions');
    
    if (mobileToggle) {
        mobileToggle.addEventListener('click', function() {
            this.classList.toggle('active');
            
            // Create mobile menu if it doesn't exist
            let mobileMenu = document.querySelector('.nav-mobile-menu');
            
            if (!mobileMenu) {
                mobileMenu = document.createElement('div');
                mobileMenu.className = 'nav-mobile-menu';
                mobileMenu.innerHTML = `
                    <div class="mobile-links">
                        <a href="#features">Features</a>
                        <a href="#how-it-works">How It Works</a>
                        <a href="#industries">Industries</a>
                        <a href="#pricing">Pricing</a>
                    </div>
                    <div class="mobile-actions">
                        <a href="https://app.threadclear.com/login" class="btn-secondary btn-full">Sign In</a>
                        <a href="#contact" class="btn-primary btn-full">Get Early Access</a>
                    </div>
                `;
                
                // Add styles for mobile menu
                const style = document.createElement('style');
                style.textContent = `
                    .nav-mobile-menu {
                        position: fixed;
                        top: 72px;
                        left: 0;
                        right: 0;
                        background: white;
                        padding: 24px;
                        border-bottom: 1px solid #e2e8f0;
                        box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1);
                        display: none;
                        flex-direction: column;
                        gap: 24px;
                        z-index: 999;
                    }
                    .nav-mobile-menu.active {
                        display: flex;
                    }
                    .mobile-links {
                        display: flex;
                        flex-direction: column;
                        gap: 8px;
                    }
                    .mobile-links a {
                        padding: 12px 16px;
                        font-size: 16px;
                        font-weight: 500;
                        color: #334155;
                        border-radius: 8px;
                        transition: background 0.2s;
                    }
                    .mobile-links a:hover {
                        background: #f1f5f9;
                    }
                    .mobile-actions {
                        display: flex;
                        flex-direction: column;
                        gap: 12px;
                    }
                    .nav-mobile-toggle.active span:nth-child(1) {
                        transform: rotate(45deg) translate(5px, 5px);
                    }
                    .nav-mobile-toggle.active span:nth-child(2) {
                        opacity: 0;
                    }
                    .nav-mobile-toggle.active span:nth-child(3) {
                        transform: rotate(-45deg) translate(5px, -5px);
                    }
                `;
                document.head.appendChild(style);
                document.querySelector('.nav').appendChild(mobileMenu);
            }
            
            mobileMenu.classList.toggle('active');
        });
    }
    
    // Close mobile menu when clicking a link
    document.addEventListener('click', function(e) {
        if (e.target.closest('.nav-mobile-menu a')) {
            const mobileMenu = document.querySelector('.nav-mobile-menu');
            const mobileToggle = document.querySelector('.nav-mobile-toggle');
            if (mobileMenu) mobileMenu.classList.remove('active');
            if (mobileToggle) mobileToggle.classList.remove('active');
        }
    });
    
    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            const href = this.getAttribute('href');
            if (href === '#') return;
            
            const target = document.querySelector(href);
            if (target) {
                e.preventDefault();
                const navHeight = document.querySelector('.nav').offsetHeight;
                const targetPosition = target.getBoundingClientRect().top + window.pageYOffset - navHeight;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
    
    // Navbar background on scroll
    const nav = document.querySelector('.nav');
    let lastScroll = 0;
    
    window.addEventListener('scroll', function() {
        const currentScroll = window.pageYOffset;
        
        if (currentScroll > 50) {
            nav.style.background = 'rgba(255, 255, 255, 0.98)';
            nav.style.boxShadow = '0 1px 3px rgba(0, 0, 0, 0.1)';
        } else {
            nav.style.background = 'rgba(255, 255, 255, 0.9)';
            nav.style.boxShadow = 'none';
        }
        
        lastScroll = currentScroll;
    });
    
    // Animate elements on scroll
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };
    
    const observer = new IntersectionObserver(function(entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-in');
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);
    
    // Add animation styles
    const animationStyles = document.createElement('style');
    animationStyles.textContent = `
        .problem-card,
        .feature-card,
        .industry-card,
        .pricing-card,
        .step {
            opacity: 0;
            transform: translateY(20px);
            transition: opacity 0.5s ease, transform 0.5s ease;
        }
        .problem-card.animate-in,
        .feature-card.animate-in,
        .industry-card.animate-in,
        .pricing-card.animate-in,
        .step.animate-in {
            opacity: 1;
            transform: translateY(0);
        }
        .problem-card:nth-child(2),
        .feature-card:nth-child(2),
        .industry-card:nth-child(2),
        .pricing-card:nth-child(2),
        .step:nth-child(3) {
            transition-delay: 0.1s;
        }
        .problem-card:nth-child(3),
        .feature-card:nth-child(3),
        .industry-card:nth-child(3),
        .pricing-card:nth-child(3),
        .step:nth-child(5) {
            transition-delay: 0.2s;
        }
    `;
    document.head.appendChild(animationStyles);
    
    // Observe elements for animation
    document.querySelectorAll('.problem-card, .feature-card, .industry-card, .pricing-card, .step').forEach(el => {
        observer.observe(el);
    });
    
    // Form submission handling
    const ctaForm = document.querySelector('.cta-form');
    if (ctaForm) {
        ctaForm.addEventListener('submit', function(e) {
            // If using Formspree or similar, let it handle the submission
            // Otherwise, add custom handling here
            
            const button = this.querySelector('button[type="submit"]');
            const originalText = button.innerHTML;
            
            button.innerHTML = `
                <svg class="spinner" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10" stroke-opacity="0.25"/>
                    <path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round">
                        <animateTransform attributeName="transform" type="rotate" from="0 12 12" to="360 12 12" dur="1s" repeatCount="indefinite"/>
                    </path>
                </svg>
                Submitting...
            `;
            button.disabled = true;
            
            // Re-enable after submission (Formspree will redirect)
            setTimeout(() => {
                button.innerHTML = originalText;
                button.disabled = false;
            }, 5000);
        });
    }
});

// Console Easter Egg
console.log('%cThreadClear', 'font-size: 24px; font-weight: bold; color: #0ea5e9;');
console.log('%cConversation Intelligence for Regulated Industries', 'font-size: 12px; color: #64748b;');
console.log('%cInterested in joining the team? Contact us at careers@threadclear.com', 'font-size: 11px; color: #94a3b8;');
