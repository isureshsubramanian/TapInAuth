/*
 * TapInAuth theme toggle.
 * Served from RCL static web assets at /_content/TapInAuth.UI/tapinauth-theme.js.
 *
 * Three pieces:
 *   1. A tiny synchronous bootstrap (inlined by the layout in <head>) reads
 *      localStorage and sets data-theme on <html> BEFORE paint, avoiding a flash.
 *   2. window.TapInAuth.toggleTheme() cycles auto → light → dark → auto.
 *   3. Event delegation + MutationObserver so toggles work across Blazor's
 *      SPA-style navigation (which doesn't re-fire DOMContentLoaded) AND
 *      traditional MVC / Razor Pages full-page navigations.
 *
 * The chosen mode persists across reloads. If the developer forced a mode via
 * Program.cs (TapInAuthOptions.Theme.Mode), the user can still override it here.
 */
(function () {
    "use strict";

    var STORAGE_KEY = "tapinauth.theme";

    function setTheme(mode) {
        var root = document.documentElement;
        root.setAttribute("data-theme", mode);
        try { localStorage.setItem(STORAGE_KEY, mode); } catch (_) {}
        updateAllToggleLabels(mode);
    }

    function currentTheme() {
        return document.documentElement.getAttribute("data-theme") || "auto";
    }

    function decorateButton(btn, mode) {
        var icon = mode === "light" ? "☀️" : mode === "dark" ? "🌙" : "🌓";
        var label = mode === "light" ? "Light" : mode === "dark" ? "Dark" : "Auto";
        btn.textContent = icon + " " + label;
        btn.setAttribute("title", "Theme: " + label + " — click to change");
    }

    function updateAllToggleLabels(mode) {
        var btns = document.querySelectorAll(".tap-theme-toggle");
        for (var i = 0; i < btns.length; i++) {
            decorateButton(btns[i], mode);
        }
    }

    function toggleTheme() {
        var current = currentTheme();
        var next = current === "auto" ? "light" : current === "light" ? "dark" : "auto";
        setTheme(next);
    }

    window.TapInAuth = window.TapInAuth || {};
    window.TapInAuth.toggleTheme = toggleTheme;

    // Event delegation — works regardless of when the button was added to the DOM.
    // Survives Blazor SPA navigation and traditional full-page reloads alike.
    document.addEventListener("click", function (e) {
        var t = e.target;
        // closest() walks up the DOM tree from the clicked element looking for a matching ancestor.
        var btn = t && t.closest ? t.closest(".tap-theme-toggle") : null;
        if (!btn) { return; }
        e.preventDefault();
        toggleTheme();
    });

    // Decorate any toggle button currently in the DOM with the right icon + label.
    function decorateAll() { updateAllToggleLabels(currentTheme()); }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", decorateAll);
    } else {
        decorateAll();
    }

    // Watch for new toggle buttons added by client-side navigation (Blazor, Turbo, htmx, etc.)
    // and decorate them with the current theme's icon + label.
    if (typeof MutationObserver !== "undefined") {
        var observer = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var added = mutations[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    var node = added[j];
                    if (node.nodeType !== 1) { continue; }
                    if (node.matches && node.matches(".tap-theme-toggle")) {
                        decorateButton(node, currentTheme());
                    } else if (node.querySelectorAll) {
                        var inner = node.querySelectorAll(".tap-theme-toggle");
                        for (var k = 0; k < inner.length; k++) {
                            decorateButton(inner[k], currentTheme());
                        }
                    }
                }
            }
        });
        observer.observe(document.body || document.documentElement, { childList: true, subtree: true });
    }
})();
