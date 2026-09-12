const views = new Map();

// The app shell scrolls inside <main>, not the window, so the box has to be sized against whatever
// actually scrolls above it.
function scroller(el) {
    let node = el.parentElement;
    while (node && node !== document.body) {
        const overflow = getComputedStyle(node).overflowY;
        if (overflow === 'auto' || overflow === 'scroll') {
            return node;
        }
        node = node.parentElement;
    }
    return null;
}

// The outermost page element under the scroller: its bottom is where content actually ends, which
// scrollHeight won't say when the page is shorter than the window.
function pageOf(el, host) {
    let page = el;
    while (page.parentElement && page.parentElement !== (host ?? document.body)) {
        page = page.parentElement;
    }
    return page;
}

// Run the box from where it sits to the bottom of the scrolling viewport. Measured from zero height
// and in content coordinates, so repeat calls converge and the answer doesn't move as the page scrolls.
function fit(el, host, page) {
    el.style.height = '0px';

    const viewport = host ? host.clientHeight : window.innerHeight;
    const origin = host ? host.getBoundingClientRect().top - host.scrollTop : -window.scrollY;
    const box = el.getBoundingClientRect();
    const below = page.getBoundingClientRect().bottom - box.bottom;

    el.style.height = `${Math.round(Math.max(viewport - (box.top - origin) - below - 8, 240))}px`;
}

export function attach(el, dotNet) {
    const host = scroller(el);
    const page = pageOf(el, host);
    const state = { atBottom: true, frame: 0 };

    const report = () => {
        state.frame = 0;
        // Slack of a few pixels: fractional line heights mean scrollTop rarely lands exactly on the end.
        const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 4;
        if (atBottom !== state.atBottom) {
            state.atBottom = atBottom;
            dotNet.invokeMethodAsync('OnAtBottomChanged', atBottom);
        }
    };

    state.onScroll = () => {
        if (!state.frame) {
            state.frame = requestAnimationFrame(report);
        }
    };

    state.refit = () => {
        const wasAtBottom = state.atBottom;
        fit(el, host, page);
        if (wasAtBottom) {
            el.scrollTop = el.scrollHeight;
        }
    };

    el.addEventListener('scroll', state.onScroll, { passive: true });
    window.addEventListener('resize', state.refit);
    state.observer = new ResizeObserver(state.refit);
    state.observer.observe(page);

    state.refit();
    views.set(el, state);
}

export function scrollToBottom(el) {
    el.scrollTop = el.scrollHeight;
}

export function detach(el) {
    const state = views.get(el);
    if (!state) {
        return;
    }

    el.removeEventListener('scroll', state.onScroll);
    window.removeEventListener('resize', state.refit);
    state.observer.disconnect();
    if (state.frame) {
        cancelAnimationFrame(state.frame);
    }
    views.delete(el);
}
