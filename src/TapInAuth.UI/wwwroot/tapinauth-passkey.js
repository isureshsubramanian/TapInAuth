/*
 * TapInAuth passkey JS interop.
 * Served from RCL static web assets at /_content/TapInAuth.UI/tapinauth-passkey.js.
 *
 * Public API on window.TapInAuth:
 *   registerPasskey({ deviceName? }) → Promise<{ ok, error?, credential? }>
 *   signInWithPasskey({ redirectTo? }) → Promise<{ ok, error?, redirect? }>
 *
 * Wraps navigator.credentials.create() / .get() with the base64url<->ArrayBuffer
 * conversions WebAuthn requires.
 */
(function () {
    "use strict";

    function b64uToBuf(b64u) {
        const pad = (s) => s + "=".repeat((4 - (s.length % 4)) % 4);
        const b64 = pad(b64u.replace(/-/g, "+").replace(/_/g, "/"));
        const bin = atob(b64);
        const buf = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
        return buf.buffer;
    }
    function bufToB64u(buf) {
        const bytes = new Uint8Array(buf);
        let bin = "";
        for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
        return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function prepareCreate(opts) {
        opts.challenge = b64uToBuf(opts.challenge);
        opts.user.id = b64uToBuf(opts.user.id);
        if (opts.excludeCredentials) {
            for (const c of opts.excludeCredentials) c.id = b64uToBuf(c.id);
        }
        return opts;
    }
    function prepareGet(opts) {
        opts.challenge = b64uToBuf(opts.challenge);
        if (opts.allowCredentials) {
            for (const c of opts.allowCredentials) c.id = b64uToBuf(c.id);
        }
        return opts;
    }
    function serializeAttestation(cred) {
        return {
            id: cred.id,
            rawId: bufToB64u(cred.rawId),
            type: cred.type,
            extensions: cred.getClientExtensionResults ? cred.getClientExtensionResults() : {},
            response: {
                attestationObject: bufToB64u(cred.response.attestationObject),
                clientDataJSON: bufToB64u(cred.response.clientDataJSON),
                transports: cred.response.getTransports ? cred.response.getTransports() : []
            }
        };
    }
    function serializeAssertion(cred) {
        return {
            id: cred.id,
            rawId: bufToB64u(cred.rawId),
            type: cred.type,
            extensions: cred.getClientExtensionResults ? cred.getClientExtensionResults() : {},
            response: {
                authenticatorData: bufToB64u(cred.response.authenticatorData),
                clientDataJSON: bufToB64u(cred.response.clientDataJSON),
                signature: bufToB64u(cred.response.signature),
                userHandle: cred.response.userHandle ? bufToB64u(cred.response.userHandle) : null
            }
        };
    }

    async function postJson(url, body) {
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        const res = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: headers,
            body: body == null ? "" : JSON.stringify(body)
        });
        if (!res.ok) {
            let err;
            try { err = await res.json(); } catch (_) { err = { error: "http_" + res.status }; }
            throw new Error(err && err.error ? err.error : "http_" + res.status);
        }
        const ct = res.headers.get("content-type") || "";
        return ct.indexOf("application/json") >= 0 ? res.json() : res.text();
    }

    async function registerPasskey(input) {
        if (!window.PublicKeyCredential) {
            return { ok: false, error: "webauthn_unsupported" };
        }
        const deviceName = (input && input.deviceName) || "";
        try {
            const optionsJson = await postJson("/auth/passkey/register/options", {});
            const options = prepareCreate(optionsJson);
            const cred = await navigator.credentials.create({ publicKey: options });
            const body = serializeAttestation(cred);
            const qs = deviceName ? ("?deviceName=" + encodeURIComponent(deviceName)) : "";
            const out = await postJson("/auth/passkey/register" + qs, body);
            return { ok: true, credential: out };
        } catch (e) {
            return { ok: false, error: (e && e.message) || "registration_failed" };
        }
    }

    async function signInWithPasskey(input) {
        if (!window.PublicKeyCredential) {
            return { ok: false, error: "webauthn_unsupported" };
        }
        try {
            const optionsJson = await postJson("/auth/passkey/assert/options", {});
            const options = prepareGet(optionsJson);
            const cred = await navigator.credentials.get({ publicKey: options });
            const body = serializeAssertion(cred);
            const out = await postJson("/auth/passkey/assert", body);
            const redirect = (input && input.redirectTo) || (out && out.redirect) || "/";
            window.location.assign(redirect);
            return { ok: true, redirect: redirect };
        } catch (e) {
            return { ok: false, error: (e && e.message) || "assertion_failed" };
        }
    }

    window.TapInAuth = window.TapInAuth || {};
    window.TapInAuth.registerPasskey = registerPasskey;
    window.TapInAuth.signInWithPasskey = signInWithPasskey;

    // ───── Global click delegation ─────
    // Buttons opt in via data-tap-passkey="sign-in" or "register". Works regardless of
    // when the button was added to the DOM — survives Blazor interactive Server SignalR
    // diffs (which DON'T execute inline <script> tags) AND traditional full-page loads.
    //
    //   <button data-tap-passkey="sign-in"
    //           data-error-target="tap-passkey-err"
    //           data-busy-text="Waiting for your authenticator…">…</button>
    //
    //   <button data-tap-passkey="register"
    //           data-device-name="Alice's laptop"
    //           data-message-target="tap-passkey-msg">…</button>
    document.addEventListener("click", async function (e) {
        var t = e.target;
        var btn = t && t.closest ? t.closest("[data-tap-passkey]") : null;
        if (!btn) { return; }
        e.preventDefault();
        var action   = btn.getAttribute("data-tap-passkey");
        var errEl    = btn.getAttribute("data-error-target")   ? document.getElementById(btn.getAttribute("data-error-target"))   : null;
        var msgEl    = btn.getAttribute("data-message-target") ? document.getElementById(btn.getAttribute("data-message-target")) : null;
        var busy     = btn.getAttribute("data-busy-text") || "Waiting for your authenticator…";
        var original = btn.textContent;
        if (errEl) { errEl.style.display = "none"; }
        btn.disabled = true;
        btn.textContent = busy;
        try {
            if (action === "sign-in") {
                var r = await signInWithPasskey({});
                if (!r.ok) {
                    if (errEl) { errEl.textContent = "Couldn't sign in with that passkey (" + r.error + ")."; errEl.style.display = ""; }
                    btn.disabled = false;
                    btn.textContent = original;
                }
                // success path navigates the window — no need to restore button state.
            } else if (action === "register") {
                var dev = btn.getAttribute("data-device-name") || (navigator.userAgent.split(")")[0].split("(")[1] || "This device");
                var rr = await registerPasskey({ deviceName: dev });
                btn.disabled = false;
                btn.textContent = original;
                if (msgEl) {
                    msgEl.style.display = "";
                    msgEl.className = rr.ok ? "tap-success-msg" : "tap-error";
                    msgEl.textContent = rr.ok ? "Passkey added." : ("Could not add passkey: " + rr.error);
                }
            }
        } catch (err) {
            btn.disabled = false;
            btn.textContent = original;
            if (errEl) { errEl.textContent = err.message || String(err); errEl.style.display = ""; }
        }
    });
})();
