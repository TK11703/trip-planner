const storagePrefix = "trip-chat-session:";
const maximumMessages = 20;
const maximumMessageLength = 4000;
const maximumCitations = 12;
const maximumStateLength = 128000;

function emptyState() {
    return { messages: [], isOpen: false, scrollTop: 0 };
}

function storageKey(epoch) {
    return `${storagePrefix}${epoch}`;
}

function clearOtherEpochs(currentKey) {
    for (let index = sessionStorage.length - 1; index >= 0; index--) {
        const key = sessionStorage.key(index);
        if (key?.startsWith(storagePrefix) && key !== currentKey) {
            sessionStorage.removeItem(key);
        }
    }
}

export function load(epoch) {
    try {
        const key = storageKey(epoch);
        clearOtherEpochs(key);
        const stored = sessionStorage.getItem(key);
        if (!stored || stored.length > maximumStateLength) {
            sessionStorage.removeItem(key);
            return emptyState();
        }
        const parsed = JSON.parse(stored);
        if (!Array.isArray(parsed.messages)) {
            sessionStorage.removeItem(key);
            return emptyState();
        }
        const messages = parsed.messages
            .filter(message => (message?.role === "user" || message?.role === "assistant") && typeof message.text === "string")
            .slice(-maximumMessages)
            .map(message => ({
                role: message.role,
                text: message.text.slice(0, maximumMessageLength),
                citations: Array.isArray(message.citations) ? message.citations.slice(0, maximumCitations) : []
            }));
        return {
            messages,
            isOpen: parsed.isOpen === true,
            scrollTop: Number.isFinite(parsed.scrollTop) && parsed.scrollTop >= 0 ? parsed.scrollTop : 0
        };
    } catch {
        return emptyState();
    }
}

export function save(epoch, snapshot) {
    try {
        const messages = Array.isArray(snapshot.messages)
            ? snapshot.messages.slice(-maximumMessages).map(message => ({
                role: message.role === "user" ? "user" : "assistant",
                text: String(message.text ?? "").slice(0, maximumMessageLength),
                citations: Array.isArray(message.citations) ? message.citations.slice(0, maximumCitations) : []
            }))
            : [];
        const serialized = JSON.stringify({
            messages,
            isOpen: snapshot.isOpen === true,
            scrollTop: Number.isFinite(snapshot.scrollTop) && snapshot.scrollTop >= 0 ? snapshot.scrollTop : 0
        });
        if (serialized.length > maximumStateLength) {
            sessionStorage.removeItem(storageKey(epoch));
            return;
        }
        sessionStorage.setItem(storageKey(epoch), serialized);
    } catch {
    }
}

export function clear(epoch) {
    try {
        sessionStorage.removeItem(storageKey(epoch));
    } catch {
    }
}

export function focus(element) {
    element?.focus();
}

export function syncPane(pane, isOpen) {
    const narrow = window.matchMedia("(max-width: 640.98px)").matches;
    if (pane) {
        if (narrow && isOpen) {
            pane.setAttribute("aria-modal", "true");
        } else {
            pane.removeAttribute("aria-modal");
        }
    }
    const page = document.querySelector(".page");
    if (page) {
        page.inert = narrow && isOpen;
    }
}

export function restoreScroll(element, scrollTop) {
    if (element && Number.isFinite(scrollTop)) {
        element.scrollTop = scrollTop;
    }
}

export function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

export function readScroll(element) {
    return element?.scrollTop ?? 0;
}

function clearChatState() {
    try {
        for (let index = sessionStorage.length - 1; index >= 0; index--) {
            const key = sessionStorage.key(index);
            if (key?.startsWith(storagePrefix)) {
                sessionStorage.removeItem(key);
            }
        }
    } catch {
    }
}

document.addEventListener("click", event => {
    const target = event.target;
    if (target instanceof Element && target.closest('a[href*="MicrosoftIdentity/Account/SignOut"]')) {
        clearChatState();
    }
});

// Handled here because a Blazor Server handler runs after the browser has already inserted the newline.
document.addEventListener("keydown", event => {
    const target = event.target;
    if (event.key !== "Enter" || event.shiftKey || event.isComposing
        || !(target instanceof HTMLTextAreaElement) || target.id !== "trip-chat-message") {
        return;
    }
    event.preventDefault();
    if (target.value.trim().length > 0) {
        target.form?.requestSubmit();
    }
});