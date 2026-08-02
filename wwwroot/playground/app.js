(() => {
    "use strict";

    const DEMO = Object.freeze({
        admin: {
            id: "0198f3a0-0000-7000-8001-000000000001",
            email: "admin@localhost.dev",
            password: "Dev_Admin_Password_1!",
        },
        user: {
            id: "0198f3a0-0000-7000-8001-000000000002",
            email: "user@localhost.dev",
            password: "Dev_User_Password_1!",
        },
        adminRoleId: "0198f3a0-0000-7000-8000-000000000001",
        userRoleId: "0198f3a0-0000-7000-8000-000000000002",
        sessionId: "0198f3a0-0000-7000-8001-000000000101",
        apiKeyId: "0198f3a0-0000-7000-8001-000000000301",
        accountId: "0198f3a0-0000-7000-8001-000000000401",
        apiKey: "ak_demoAdmin01_Dev_Demo_Api_Key_Only_Local_2026",
    });

    const groups = Object.freeze([
        ["all", "Tümü"],
        ["operations", "Operasyon"],
        ["authentication", "Kimlik"],
        ["account", "Hesap"],
        ["sessions", "Oturumlar"],
        ["security", "Güvenlik"],
        ["admin", "Yönetim"],
    ]);

    const endpoints = [
        endpoint("health-live", "GET", "/health/live", "Liveness kontrolü", "İşlem ayakta mı? Veritabanından bağımsız, hafif probe.", "operations", "public"),
        endpoint("health-ready", "GET", "/health/ready", "Readiness kontrolü", "API ve PostgreSQL trafiğe hazır mı?", "operations", "public"),
        endpoint("jwks", "GET", "/.well-known/jwks.json", "Public signing keys", "Aktif ve retiring ES256 anahtarlarını JWKS biçiminde döndürür.", "operations", "public"),

        endpoint("auth-register", "POST", "/api/v1/auth/register", "Yeni hesap kaydı", "Hesabı oluşturur ve Mailpit’e doğrulama e-postası yollar.", "authentication", "public", {
            body: {
                email: "new-user@example.test",
                password: "V4lid!River-Stone-Cobalt-47",
                displayName: "New User",
            },
        }),
        endpoint("auth-login", "POST", "/api/v1/auth/login", "E-posta ve parola ile giriş", "Bearer/body ya da cookie taşımasıyla yeni oturum açar; MFA etkinse ticket döndürür.", "authentication", "public", {
            body: {
                email: DEMO.user.email,
                password: DEMO.user.password,
            },
        }),
        endpoint("auth-login-mfa", "POST", "/api/v1/auth/login/mfa", "MFA girişini tamamla", "Parola adımından gelen tek kullanımlık ticket’ı TOTP veya recovery code ile tamamlar.", "authentication", "public", {
            body: {
                mfaTicket: "{{mfaTicket}}",
                code: "{{totpCode}}",
            },
        }),
        endpoint("auth-refresh", "POST", "/api/v1/auth/refresh", "Token çiftini yenile", "Refresh token’ı atomik olarak döndürür; eski token’ın tekrar kullanımı oturumu iptal eder.", "authentication", "public", {
            body: {
                refreshToken: "{{refreshToken}}",
            },
        }),
        endpoint("auth-logout", "POST", "/api/v1/auth/logout", "Aktif oturumu kapat", "Geçerli oturumu iptal eder ve varsa auth cookie’lerini temizler.", "authentication", "auth", {
            destructive: true,
        }),
        endpoint("auth-csrf", "GET", "/api/v1/auth/csrf", "CSRF token üret", "Cookie taşımasındaki mutasyonlar için oturuma bağlı double-submit token üretir.", "authentication", "auth"),
        endpoint("social-authorize", "GET", "/api/v1/auth/social/{provider}/authorize", "Sosyal girişi başlat", "Development’ta yerel fixture URL’si; diğer ortamlarda gerçek sağlayıcı authorize URL’si döner.", "authentication", "public", {
            pathParams: [{ name: "provider", value: "google" }],
            special: "social",
        }),
        endpoint("social-callback", "GET", "/api/v1/auth/social/{provider}/callback", "Sosyal giriş callback", "İmzalı state’i tüketir, sağlayıcı kimliğini yerel hesaba dönüştürür ve oturum açar.", "authentication", "public", {
            pathParams: [{ name: "provider", value: "google" }],
            query: [
                { name: "code", value: "{{socialCode}}" },
                { name: "state", value: "{{socialState}}" },
                { name: "error", value: "" },
            ],
        }),
        endpoint("email-send", "POST", "/api/v1/email-verification/send", "Doğrulama e-postasını yeniden gönder", "Aktif kullanıcının doğrulama bağlantısını yerel Mailpit inbox’ına yollar.", "authentication", "auth"),
        endpoint("email-confirm", "POST", "/api/v1/email-verification/confirm", "E-posta adresini doğrula", "Mailpit mesajındaki tek kullanımlık doğrulama token’ını tüketir.", "authentication", "public", {
            body: { token: "{{verificationToken}}" },
        }),
        endpoint("password-request", "POST", "/api/v1/password-reset/request", "Parola sıfırlama iste", "Hesap varlığını açığa çıkarmadan Mailpit’e sıfırlama bağlantısı yollar.", "authentication", "public", {
            body: { email: DEMO.user.email },
        }),
        endpoint("password-confirm", "POST", "/api/v1/password-reset/confirm", "Yeni parolayı kaydet", "Tek kullanımlık token’ı tüketir, security stamp’i döndürür ve tüm oturumları iptal eder.", "authentication", "public", {
            body: {
                token: "{{passwordResetToken}}",
                newPassword: "N3w!River-Stone-Cobalt-82",
            },
            destructive: true,
        }),

        endpoint("users-me", "GET", "/api/v1/users/me", "Profilimi getir", "Kimliği token’daki subject’ten çözer; roller ve güvenlik duruşunu döndürür.", "account", "auth"),
        endpoint("users-update", "PATCH", "/api/v1/users/me", "Profilimi güncelle", "Yalnızca display name alanını günceller.", "account", "auth", {
            body: { displayName: "Workbench User" },
        }),
        endpoint("users-delete", "DELETE", "/api/v1/users/me", "Hesabımı sil", "Hesabı ve tüm kimlik bilgilerini geri alınamaz biçimde siler; yakın zamanda giriş gerekir.", "account", "recent", {
            destructive: true,
        }),
        endpoint("users-password", "PUT", "/api/v1/users/me/password", "Parolamı değiştir", "Mevcut parolayı doğrular, kardeş oturumları iptal eder.", "account", "auth", {
            body: {
                currentPassword: DEMO.user.password,
                newPassword: "N3w!River-Stone-Cobalt-82",
            },
            destructive: true,
        }),
        endpoint("users-accounts", "GET", "/api/v1/users/me/accounts", "Bağlı hesapları listele", "Google/GitHub bağlantılarının yerel id ve provider bilgisini döndürür.", "account", "auth"),
        endpoint("users-unlink", "DELETE", "/api/v1/users/me/accounts/{accountId}", "Sosyal hesabı ayır", "Son giriş yöntemini kaldırmaya izin vermez.", "account", "auth", {
            pathParams: [{ name: "accountId", value: DEMO.accountId }],
            destructive: true,
        }),

        endpoint("sessions-list", "GET", "/api/v1/sessions", "Oturumlarımı listele", "Hazır iki fixture oturumu ve girişle oluşan güncel oturumu gösterir.", "sessions", "auth"),
        endpoint("sessions-revoke", "DELETE", "/api/v1/sessions/{sessionId}", "Bir oturumu iptal et", "Yalnızca çağıranın oturum uzayında arar; diğer kullanıcı id’leri 404 döner.", "sessions", "auth", {
            pathParams: [{ name: "sessionId", value: DEMO.sessionId }],
            destructive: true,
        }),
        endpoint("sessions-revoke-others", "DELETE", "/api/v1/sessions", "Diğer tüm oturumları iptal et", "İsteği yapan oturumu koruyup diğer aktif oturumları kapatır.", "sessions", "auth", {
            destructive: true,
        }),

        endpoint("mfa-enroll", "POST", "/api/v1/mfa/totp/enroll", "TOTP kaydını başlat", "Yeni Base32 secret ve otpauth URI üretir; workbench canlı kodu otomatik hesaplar.", "security", "auth", {
            special: "totp",
        }),
        endpoint("mfa-confirm", "POST", "/api/v1/mfa/totp/confirm", "TOTP kaydını doğrula", "Canlı kodu doğrular, MFA’yı etkinleştirir ve 10 tek kullanımlık recovery code döndürür.", "security", "auth", {
            body: { code: "{{totpCode}}" },
            special: "totp",
        }),
        endpoint("mfa-disable", "DELETE", "/api/v1/mfa/totp", "TOTP’yi kapat", "Yakın zamanda giriş şartıyla MFA credential ve recovery code’ları siler.", "security", "recent", {
            destructive: true,
        }),
        endpoint("mfa-recovery", "POST", "/api/v1/mfa/recovery-codes/regenerate", "Recovery code’ları yenile", "Önceki batch’i geçersiz kılar ve yalnızca bir kez gösterilen yeni kodlar döndürür.", "security", "recent", {
            destructive: true,
        }),

        endpoint("passkey-registration-options", "POST", "/api/v1/passkeys/registration/options", "Passkey kayıt seçenekleri", "Sunucuda tek kullanımlık challenge saklar ve WebAuthn creation options döndürür.", "security", "auth", {
            body: { label: "Workbench passkey" },
            special: "passkey-register",
        }),
        endpoint("passkey-registration-complete", "POST", "/api/v1/passkeys/registration/complete", "Passkey kaydını tamamla", "navigator.credentials.create() çıktısını doğrular ve public credential’ı kaydeder.", "security", "auth", {
            body: {
                attestationResponse: {},
                label: "Workbench passkey",
            },
        }),
        endpoint("passkey-auth-options", "POST", "/api/v1/passkeys/authentication/options", "Passkey giriş seçenekleri", "Anonymous WebAuthn assertion challenge üretir.", "security", "public", {
            body: {},
            special: "passkey-auth",
        }),
        endpoint("passkey-auth-complete", "POST", "/api/v1/passkeys/authentication/complete", "Passkey ile giriş yap", "Assertion imzasını ve sayacını doğrular, yeni oturum açar.", "security", "public", {
            body: { assertionResponse: {} },
        }),
        endpoint("passkeys-list", "GET", "/api/v1/passkeys", "Passkey’leri listele", "Credential id, etiket ve son kullanım bilgisini döndürür; anahtar materyali taşımaz.", "security", "auth"),
        endpoint("passkey-delete", "DELETE", "/api/v1/passkeys/{credentialId}", "Passkey’i kaldır", "Base64url credential id’yi çağıran kullanıcı kapsamında siler.", "security", "auth", {
            pathParams: [{ name: "credentialId", value: "{{credentialId}}" }],
            destructive: true,
        }),

        endpoint("api-key-create", "POST", "/api/v1/api-keys", "API key oluştur", "Secret’ı yalnızca bu yanıtta gösterir; kapsamlar kullanıcının izinlerini aşamaz.", "security", "auth", {
            body: {
                name: "Local automation",
                scopes: ["users:read:any", "audit:read"],
                expiresAt: "2027-12-31T00:00:00Z",
            },
        }),
        endpoint("api-keys-list", "GET", "/api/v1/api-keys", "API key’leri listele", "Secret olmadan prefix, kapsam, kullanım ve iptal bilgilerini döndürür.", "security", "auth"),
        endpoint("api-key-revoke", "DELETE", "/api/v1/api-keys/{keyId}", "API key’i iptal et", "Credential’ı geri döndürülemez biçimde iptal eder; audit bağlantısı için satırı korur.", "security", "auth", {
            pathParams: [{ name: "keyId", value: DEMO.apiKeyId }],
            destructive: true,
        }),

        endpoint("admin-users-list", "GET", "/api/v1/admin/users", "Kullanıcıları listele", "Sayfalı, aranabilir, filtrelenebilir yönetim görünümü.", "admin", "admin", {
            query: [
                { name: "page", value: "1" },
                { name: "pageSize", value: "20" },
                { name: "sort", value: "createdAt:desc" },
                { name: "search", value: "" },
                { name: "role", value: "" },
                { name: "emailVerified", value: "" },
                { name: "locked", value: "" },
            ],
        }),
        endpoint("admin-user-get", "GET", "/api/v1/admin/users/{userId}", "Kullanıcı detayını getir", "Roller, bağlı provider’lar, lockout ve aktif oturumlarla tam görünüm.", "admin", "admin", {
            pathParams: [{ name: "userId", value: DEMO.user.id }],
        }),
        endpoint("admin-user-update", "PATCH", "/api/v1/admin/users/{userId}", "Kullanıcıyı güncelle", "Display name, doğrulama durumu veya lockout temizleme alanlarını patch eder.", "admin", "admin", {
            pathParams: [{ name: "userId", value: DEMO.user.id }],
            body: {
                displayName: "Reviewed User",
                emailVerified: true,
                unlock: true,
            },
        }),
        endpoint("admin-user-delete", "DELETE", "/api/v1/admin/users/{userId}", "Kullanıcıyı sil", "Kullanıcıyı ve credential’larını siler; audit trail yaşamaya devam eder.", "admin", "admin", {
            pathParams: [{ name: "userId", value: DEMO.user.id }],
            destructive: true,
        }),
        endpoint("admin-role-grant", "POST", "/api/v1/admin/users/{userId}/roles", "Rol ver", "Sabit rol id’sini kullanıcıya atar; yeni token issuance sonrası etkili olur.", "admin", "admin", {
            pathParams: [{ name: "userId", value: DEMO.user.id }],
            body: { roleId: DEMO.adminRoleId },
            destructive: true,
        }),
        endpoint("admin-role-revoke", "DELETE", "/api/v1/admin/users/{userId}/roles/{roleId}", "Rolü geri al", "Rol grant’ını kaldırır; yeni token issuance sonrası etkili olur.", "admin", "admin", {
            pathParams: [
                { name: "userId", value: DEMO.user.id },
                { name: "roleId", value: DEMO.adminRoleId },
            ],
            destructive: true,
        }),
        endpoint("admin-sessions-revoke", "DELETE", "/api/v1/admin/users/{userId}/sessions", "Kullanıcının oturumlarını kapat", "Incident response için hedef kullanıcının tüm aktif oturumlarını iptal eder.", "admin", "admin", {
            pathParams: [{ name: "userId", value: DEMO.user.id }],
            destructive: true,
        }),
        endpoint("admin-audit", "GET", "/api/v1/admin/audit-logs", "Audit trail sorgula", "Hazır fixture kayıtlarını ve çalışma sırasında oluşan güvenlik olaylarını sayfalı döndürür.", "admin", "admin", {
            query: [
                { name: "page", value: "1" },
                { name: "pageSize", value: "20" },
                { name: "sort", value: "occurredAt:desc" },
                { name: "userId", value: "" },
                { name: "eventType", value: "" },
                { name: "from", value: "" },
                { name: "to", value: "" },
                { name: "correlationId", value: "" },
            ],
        }),
    ];

    const methodColors = Object.freeze({
        GET: "#63dcc3",
        POST: "#c8ff62",
        PUT: "#77a7ff",
        PATCH: "#ffc45b",
        DELETE: "#ff765f",
    });

    const authLabels = Object.freeze({
        public: "PUBLIC",
        auth: "AUTH",
        recent: "RECENT AUTH",
        admin: "ADMIN",
    });

    const sessionIssuingEndpoints = new Set([
        "auth-login",
        "auth-login-mfa",
        "social-callback",
        "passkey-auth-complete",
    ]);

    const storageKeys = Object.freeze({
        accessToken: "startpack.workbench.accessToken",
        refreshToken: "startpack.workbench.refreshToken",
        apiKey: "startpack.workbench.apiKey",
        csrfToken: "startpack.workbench.csrfToken",
        authMode: "startpack.workbench.authMode",
        totpSecret: "startpack.workbench.totpSecret",
        mfaTicket: "startpack.workbench.mfaTicket",
        credentialId: "startpack.workbench.credentialId",
        socialCode: "startpack.workbench.socialCode",
        socialState: "startpack.workbench.socialState",
    });

    const state = {
        activeGroup: "all",
        search: "",
        selectedId: null,
        responseText: "",
        responseData: null,
        authMode: sessionStorage.getItem(storageKeys.authMode) || "bearer",
        requestAbort: null,
        variables: {
            accessToken: sessionStorage.getItem(storageKeys.accessToken) || "",
            refreshToken: sessionStorage.getItem(storageKeys.refreshToken) || "",
            apiKey: sessionStorage.getItem(storageKeys.apiKey) || "",
            csrfToken: sessionStorage.getItem(storageKeys.csrfToken) || "",
            totpSecret: sessionStorage.getItem(storageKeys.totpSecret) || "",
            totpCode: "",
            mfaTicket: sessionStorage.getItem(storageKeys.mfaTicket) || "",
            credentialId: sessionStorage.getItem(storageKeys.credentialId) || "",
            socialCode: sessionStorage.getItem(storageKeys.socialCode) || "",
            socialState: sessionStorage.getItem(storageKeys.socialState) || "",
        },
    };

    const dom = {
        apiBase: byId("api-base"),
        categoryTabs: byId("category-tabs"),
        endpointList: byId("endpoint-list"),
        endpointSearch: byId("endpoint-search"),
        emptyState: byId("empty-state"),
        visibleCount: byId("visible-count"),
        catalogTitle: byId("catalog-title"),
        requestPanel: byId("request-panel"),
        panelBackdrop: byId("panel-backdrop"),
        panelEmpty: byId("panel-empty"),
        panelContent: byId("panel-content"),
        panelMethod: byId("panel-method"),
        panelAuth: byId("panel-auth"),
        panelTitle: byId("panel-title"),
        panelDescription: byId("panel-description"),
        requestOrigin: byId("request-origin"),
        requestPath: byId("request-path"),
        parameterFields: byId("parameter-fields"),
        bodyField: byId("body-field"),
        requestBody: byId("request-body"),
        requestHeaders: byId("request-headers"),
        destructiveNote: byId("destructive-note"),
        specialActions: byId("special-actions"),
        sendRequest: byId("send-request"),
        responseTabStatus: byId("response-tab-status"),
        responseCode: byId("response-code"),
        responseLabel: byId("response-label"),
        responseMeta: byId("response-meta"),
        responseViewer: byId("response-viewer"),
        responseHeaders: byId("response-headers"),
        responseNotice: byId("response-notice"),
        accessToken: byId("access-token"),
        refreshToken: byId("refresh-token"),
        apiKey: byId("api-key"),
        csrfToken: byId("csrf-token"),
        vaultOrb: byId("vault-orb"),
        vaultTitle: byId("vault-title"),
        vaultSubtitle: byId("vault-subtitle"),
        leftRail: byId("left-rail"),
        toastRegion: byId("toast-region"),
        topbar: document.querySelector(".topbar"),
        mainContent: document.querySelector(".main-content"),
    };

    // The widths at which each aside stops being a column and becomes an off-canvas drawer.
    // These must stay in step with the two media queries in styles.css.
    const drawerQueries = Object.freeze({
        rail: window.matchMedia("(max-width: 980px)"),
        panel: window.matchMedia("(max-width: 1320px)"),
    });

    let drawerReturnFocus = null;

    initialize();

    function endpoint(id, method, path, title, description, group, auth, options = {}) {
        return {
            id,
            method,
            path,
            title,
            description,
            group,
            auth,
            body: null,
            pathParams: [],
            query: [],
            destructive: false,
            special: null,
            ...options,
        };
    }

    function byId(id) {
        return document.getElementById(id);
    }

    /// References a symbol from the sprite in index.html. Decorative by definition — every
    /// caller either sits next to its own label or is on a button with an aria-label.
    function icon(name) {
        const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("class", "icon");
        svg.setAttribute("aria-hidden", "true");
        const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
        use.setAttribute("href", `#${name}`);
        svg.append(use);
        return svg;
    }

    function initialize() {
        const defaultOrigin = window.location.origin;
        dom.apiBase.value = defaultOrigin;
        dom.requestOrigin.textContent = defaultOrigin;

        const mailpit = new URL(window.location.href);
        mailpit.protocol = "http:";
        mailpit.port = "8025";
        mailpit.pathname = "/";
        byId("mailpit-link").href = mailpit.toString();

        hydrateVaultFields();
        bindEvents();
        renderCategories();
        renderEndpoints();
        updateVaultStatus();
        updateAuthMode();
        syncDrawerState();
        updateTotpContinuously();
        checkServices();
    }

    function bindEvents() {
        dom.endpointSearch.addEventListener("input", (event) => {
            state.search = event.target.value.trim().toLocaleLowerCase("tr-TR");
            renderEndpoints();
        });

        dom.apiBase.addEventListener("change", () => {
            dom.apiBase.value = normalizeBase(dom.apiBase.value);
            dom.requestOrigin.textContent = dom.apiBase.value;
            updateRequestPreview();
            checkServices();
        });

        dom.categoryTabs.addEventListener("click", (event) => {
            const button = event.target.closest("button[data-group]");
            if (!button) return;
            state.activeGroup = button.dataset.group;
            renderCategories();
            renderEndpoints();
        });

        dom.endpointList.addEventListener("click", (event) => {
            const card = event.target.closest("button[data-endpoint-id]");
            if (card) selectEndpoint(card.dataset.endpointId);
        });

        document.querySelectorAll("[data-view]").forEach((button) => {
            button.addEventListener("click", () => {
                document.querySelectorAll("[data-view]").forEach((candidate) => {
                    const active = candidate === button;
                    candidate.classList.toggle("active", active);
                    candidate.setAttribute("aria-pressed", String(active));
                });
                dom.endpointList.classList.toggle("compact", button.dataset.view === "compact");
            });
        });

        document.querySelectorAll("#auth-mode button").forEach((button) => {
            button.addEventListener("click", () => setAuthMode(button.dataset.mode));
        });

        byId("auth-mode").addEventListener("keydown", (event) => {
            const modes = ["bearer", "cookie", "apiKey"];
            const next = rovingTarget(event, modes.indexOf(state.authMode), modes.length);
            if (next === null) return;
            event.preventDefault();
            setAuthMode(modes[next]);
            document.querySelector(`#auth-mode button[data-mode="${modes[next]}"]`)?.focus();
        });

        document.querySelector(".request-tabs").addEventListener("keydown", (event) => {
            const names = ["request", "response"];
            const current = names.findIndex(
                (name) => byId(`tab-${name}`).getAttribute("aria-selected") === "true");
            const next = rovingTarget(event, current, names.length);
            if (next === null) return;
            event.preventDefault();
            switchPanelTab(names[next]);
            byId(`tab-${names[next]}`).focus();
        });

        // A drawer that is open when the viewport crosses back into a column layout must
        // stop being modal, or the rest of the shell stays inert at desktop width.
        Object.values(drawerQueries).forEach((query) => {
            query.addEventListener("change", () => closeDrawers(false));
        });

        [
            [dom.accessToken, "accessToken"],
            [dom.refreshToken, "refreshToken"],
            [dom.apiKey, "apiKey"],
            [dom.csrfToken, "csrfToken"],
        ].forEach(([field, name]) => {
            field.addEventListener("input", () => {
                setVariable(name, field.value.trim());
                updateVaultStatus();
                updateRequestPreview();
            });
        });

        byId("clear-vault").addEventListener("click", clearVault);
        byId("close-panel").addEventListener("click", closePanels);
        dom.panelBackdrop.addEventListener("click", closePanels);
        byId("open-vault").addEventListener("click", () => openDrawer(dom.leftRail));
        byId("close-vault").addEventListener("click", closePanels);

        document.querySelectorAll("[data-demo-login]").forEach((button) => {
            button.addEventListener("click", () => demoLogin(button.dataset.demoLogin));
        });
        byId("prepare-sessions").addEventListener("click", prepareSessionScenario);
        byId("use-demo-api-key").addEventListener("click", useDemoApiKey);
        byId("demo-google").addEventListener("click", () => runDemoSocial("google"));
        byId("demo-github").addEventListener("click", () => runDemoSocial("github"));

        document.querySelectorAll("[data-copy]").forEach((button) => {
            button.addEventListener("click", () => copyText(button.dataset.copy, "Fixture kopyalandı"));
        });

        document.querySelectorAll("[data-health-path]").forEach((button) => {
            button.addEventListener("click", () => {
                const match = endpoints.find((item) => item.path === button.dataset.healthPath);
                if (match) {
                    selectEndpoint(match.id);
                } else {
                    window.open("/openapi/v1.json", "_blank", "noopener");
                }
            });
        });

        dom.parameterFields.addEventListener("input", updateRequestPreview);
        dom.requestBody.addEventListener("input", updateRequestPreview);
        dom.requestPath.addEventListener("input", updateHeaderPreview);
        byId("format-json").addEventListener("click", formatRequestJson);
        dom.sendRequest.addEventListener("click", () => sendSelectedRequest());
        byId("copy-curl").addEventListener("click", copyCurl);
        byId("copy-response").addEventListener("click", () => copyText(state.responseText, "Yanıt kopyalandı"));

        document.querySelector(".request-tabs").addEventListener("click", (event) => {
            const tab = event.target.closest("button[data-panel-tab]");
            if (tab) switchPanelTab(tab.dataset.panelTab);
        });

        document.addEventListener("keydown", (event) => {
            const editable = ["INPUT", "TEXTAREA"].includes(document.activeElement?.tagName);
            if (event.key === "/" && !editable) {
                event.preventDefault();
                dom.endpointSearch.focus();
            }
            if ((event.metaKey || event.ctrlKey) && event.key === "Enter" && state.selectedId) {
                event.preventDefault();
                sendSelectedRequest();
            }
            if (event.key === "Escape") closePanels();
        });
    }

    /// Shared arrow-key arithmetic for the two composite widgets (the transport radio group
    /// and the request/response tablist). Returns the index to move to, or null when the key
    /// is not one this pattern handles.
    function rovingTarget(event, current, length) {
        if (["ArrowRight", "ArrowDown"].includes(event.key)) return (current + 1) % length;
        if (["ArrowLeft", "ArrowUp"].includes(event.key)) return (current - 1 + length) % length;
        if (event.key === "Home") return 0;
        if (event.key === "End") return length - 1;
        return null;
    }

    function renderCategories() {
        dom.categoryTabs.replaceChildren();
        groups.forEach(([id, label]) => {
            const count = id === "all" ? endpoints.length : endpoints.filter((item) => item.group === id).length;
            const button = document.createElement("button");
            button.type = "button";
            button.dataset.group = id;
            button.classList.toggle("active", state.activeGroup === id);
            button.setAttribute("aria-pressed", String(state.activeGroup === id));
            button.append(document.createTextNode(label));
            const badge = document.createElement("span");
            badge.textContent = String(count);
            button.append(badge);
            dom.categoryTabs.append(button);
        });
    }

    function renderEndpoints() {
        const visible = endpoints.filter((item) => {
            const groupMatches = state.activeGroup === "all" || item.group === state.activeGroup;
            const haystack = `${item.method} ${item.path} ${item.title} ${item.description}`.toLocaleLowerCase("tr-TR");
            return groupMatches && (!state.search || haystack.includes(state.search));
        });

        dom.endpointList.replaceChildren();
        visible.forEach((item) => dom.endpointList.append(endpointCard(item)));
        dom.emptyState.hidden = visible.length > 0;
        dom.visibleCount.textContent = `${visible.length} endpoint`;
        const groupLabel = groups.find(([id]) => id === state.activeGroup)?.[1] || "Tümü";
        dom.catalogTitle.textContent = state.activeGroup === "all" ? "Tüm işlemler" : groupLabel;
    }

    function endpointCard(item) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "endpoint-card";
        button.dataset.endpointId = item.id;
        button.classList.toggle("selected", state.selectedId === item.id);
        button.style.setProperty("--method-color", methodColors[item.method]);

        const method = document.createElement("span");
        method.className = "method";
        method.textContent = item.method;

        const route = document.createElement("code");
        route.className = "route";
        route.textContent = item.path;

        const summary = document.createElement("span");
        summary.className = "summary";
        const title = document.createElement("strong");
        title.textContent = item.title;
        const description = document.createElement("small");
        description.textContent = item.description;
        summary.append(title, description);

        const security = document.createElement("span");
        security.className = `security ${item.auth}`;
        security.textContent = authLabels[item.auth];

        const arrow = icon("i-arrow-right");
        arrow.classList.add("arrow");

        button.append(method, route, summary, security, arrow);
        return button;
    }

    function selectEndpoint(id) {
        const item = endpoints.find((candidate) => candidate.id === id);
        if (!item) return;

        state.selectedId = id;
        renderEndpoints();
        dom.panelEmpty.hidden = true;
        dom.panelContent.hidden = false;
        dom.panelMethod.textContent = item.method;
        dom.panelMethod.style.setProperty("--method-color", methodColors[item.method]);
        dom.panelAuth.textContent = authLabels[item.auth];
        dom.panelTitle.textContent = item.title;
        dom.panelDescription.textContent = item.description;
        dom.requestBody.value = item.body === null ? "" : JSON.stringify(item.body, null, 2);
        dom.bodyField.hidden = item.body === null;
        dom.destructiveNote.hidden = !item.destructive;
        renderParameters(item);
        renderSpecialActions(item);
        updateRequestPreview();
        resetResponse();
        switchPanelTab("request");

        if (drawerQueries.panel.matches) {
            openDrawer(dom.requestPanel);
        }
    }

    function renderParameters(item) {
        dom.parameterFields.replaceChildren();
        if (item.pathParams.length) {
            dom.parameterFields.append(parameterFieldset("PATH PARAMETERS", item.pathParams, "path"));
        }
        if (item.query.length) {
            dom.parameterFields.append(parameterFieldset("QUERY PARAMETERS", item.query, "query"));
        }
    }

    function parameterFieldset(title, parameters, kind) {
        const fieldset = document.createElement("fieldset");
        fieldset.className = "dynamic-fieldset";
        const legend = document.createElement("legend");
        legend.textContent = title;
        const fields = document.createElement("div");
        fields.className = "dynamic-fields";
        parameters.forEach((parameter) => {
            const label = document.createElement("label");
            const name = document.createElement("span");
            name.textContent = parameter.name;
            const input = document.createElement("input");
            input.type = "text";
            input.dataset.paramKind = kind;
            input.dataset.paramName = parameter.name;
            input.value = resolveTemplateString(parameter.value);
            input.placeholder = parameter.name;
            input.autocomplete = "off";
            input.spellcheck = false;
            label.append(name, input);
            fields.append(label);
        });
        fieldset.append(legend, fields);
        return fieldset;
    }

    function renderSpecialActions(item) {
        dom.specialActions.replaceChildren();
        if (item.special === "passkey-register") {
            dom.specialActions.append(specialButton(
                "i-shield",
                "WebAuthn kayıt törenini çalıştır",
                "Tarayıcı authenticator penceresini açar",
                runPasskeyRegistration));
        }
        if (item.special === "passkey-auth") {
            dom.specialActions.append(specialButton(
                "i-key",
                "Passkey ile giriş törenini çalıştır",
                "Challenge → authenticator → assertion",
                runPasskeyAuthentication));
        }
        if (item.special === "totp" || (item.id === "auth-login-mfa" && state.variables.totpSecret)) {
            const detail = state.variables.totpSecret
                ? `Canlı kod: ${state.variables.totpCode || "hesaplanıyor"}`
                : "Önce enrollment endpoint’ini gönderin";
            dom.specialActions.append(specialButton(
                "i-clock",
                "Canlı TOTP kodunu gövdeye yaz",
                detail,
                fillTotpCode));
        }
        if (item.special === "social") {
            dom.specialActions.append(specialButton(
                "i-external",
                "Yerel OAuth akışını tamamla",
                "Authorize ve callback’i art arda çalıştırır",
                () => {
                    const provider = document.querySelector('[data-param-kind="path"][data-param-name="provider"]')?.value || "google";
                    runDemoSocial(provider);
                }));
        }
    }

    function specialButton(iconName, title, subtitle, handler) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "special-action";
        const iconNode = document.createElement("span");
        iconNode.append(icon(iconName));
        const copy = document.createElement("span");
        const strong = document.createElement("strong");
        strong.textContent = title;
        const small = document.createElement("small");
        small.textContent = subtitle;
        copy.append(strong, small);
        button.append(iconNode, copy, icon("i-arrow-right"));
        button.addEventListener("click", handler);
        return button;
    }

    function updateRequestPreview() {
        const item = selectedEndpoint();
        if (!item) return;
        dom.requestOrigin.textContent = normalizeBase(dom.apiBase.value);
        dom.requestPath.value = buildPath(item);
        updateHeaderPreview();
    }

    function buildPath(item) {
        let path = item.path;
        document.querySelectorAll('[data-param-kind="path"]').forEach((input) => {
            const value = input.value.trim();
            path = path.replace(`{${input.dataset.paramName}}`, encodeURIComponent(value));
        });
        const query = new URLSearchParams();
        document.querySelectorAll('[data-param-kind="query"]').forEach((input) => {
            const value = resolveTemplateString(input.value.trim());
            if (value && !value.startsWith("{{")) query.set(input.dataset.paramName, value);
        });
        const queryString = query.toString();
        return queryString ? `${path}?${queryString}` : path;
    }

    function updateHeaderPreview() {
        const item = selectedEndpoint();
        if (!item) return;
        const headers = buildHeaders(item);
        dom.requestHeaders.textContent = Object.entries(headers)
            .map(([name, value]) => `${name}: ${redactHeader(name, value)}`)
            .join("\n") || "(özel başlık yok)";
    }

    function buildHeaders(item) {
        const headers = { Accept: "application/json" };
        if (item.body !== null) headers["Content-Type"] = "application/json";

        if (item.auth !== "public") {
            if (state.authMode === "bearer" && state.variables.accessToken) {
                headers.Authorization = `Bearer ${state.variables.accessToken}`;
            }
            if (state.authMode === "apiKey" && state.variables.apiKey) {
                headers.Authorization = `ApiKey ${state.variables.apiKey}`;
            }
        }

        if (state.authMode === "cookie" && isUnsafe(item.method)) {
            const csrf = readCookie("__Host-auth.csrf") || state.variables.csrfToken;
            if (csrf) headers["X-CSRF-Token"] = csrf;
        }

        if (sessionIssuingEndpoints.has(item.id) && state.authMode === "cookie") {
            headers["X-Auth-Transport"] = "cookie";
        }

        return headers;
    }

    function redactHeader(name, value) {
        if (name.toLowerCase() !== "authorization") return value;
        const [scheme, token = ""] = value.split(" ", 2);
        return `${scheme} ${token.slice(0, 12)}…${token.slice(-5)}`;
    }

    async function sendSelectedRequest() {
        const item = selectedEndpoint();
        if (!item) return;
        if (item.destructive && !window.confirm(`“${item.title}” demo verisini değiştirecek. Devam edilsin mi?`)) {
            return;
        }
        await executeEndpoint(item);
    }

    async function executeEndpoint(item, overrides = {}) {
        let body;
        try {
            body = overrides.body !== undefined
                ? overrides.body
                : item.body === null
                    ? undefined
                    : materialize(JSON.parse(dom.requestBody.value || "{}"));
        } catch {
            toast("JSON gövdesi geçersiz", "Sözdizimini düzeltip tekrar deneyin.", true);
            dom.requestBody.focus();
            return null;
        }

        const requestPath = overrides.path || (item.id === state.selectedId ? dom.requestPath.value : buildPathFromDefinition(item));
        const headers = { ...buildHeaders(item), ...(overrides.headers || {}) };
        return performRequest(item, requestPath, body, headers, overrides.display !== false);
    }

    async function performRequest(item, requestPath, body, headers, display = true) {
        if (state.requestAbort) state.requestAbort.abort();
        const controller = new AbortController();
        state.requestAbort = controller;
        const started = performance.now();

        if (display) {
            dom.sendRequest.disabled = true;
            dom.sendRequest.querySelector("span").textContent = "Gönderiliyor…";
        }

        try {
            const response = await fetch(`${normalizeBase(dom.apiBase.value)}${requestPath}`, {
                method: item.method,
                headers,
                body: body === undefined ? undefined : JSON.stringify(body),
                credentials: state.authMode === "cookie" ? "include" : "omit",
                signal: controller.signal,
            });
            const elapsed = Math.round(performance.now() - started);
            const text = await response.text();
            const data = parseResponse(text);
            captureResponseData(item, data, response);
            if (display) displayResponse(response, text, data, elapsed);
            return { response, text, data, elapsed };
        } catch (error) {
            if (error.name === "AbortError") return null;
            const elapsed = Math.round(performance.now() - started);
            const message = error instanceof Error ? error.message : String(error);
            if (display) displayNetworkError(message, elapsed);
            toast("İstek gönderilemedi", message, true);
            return null;
        } finally {
            if (state.requestAbort === controller) state.requestAbort = null;
            if (display) {
                dom.sendRequest.disabled = false;
                dom.sendRequest.querySelector("span").textContent = "İsteği gönder";
            }
        }
    }

    function captureResponseData(item, data, response) {
        if (!data || typeof data !== "object") return;

        if (typeof data.accessToken === "string") setVariable("accessToken", data.accessToken);
        if (typeof data.refreshToken === "string") setVariable("refreshToken", data.refreshToken);
        if (typeof data.key === "string") setVariable("apiKey", data.key);
        if (typeof data.mfaTicket === "string") setVariable("mfaTicket", data.mfaTicket);
        if (typeof data.secret === "string") {
            setVariable("totpSecret", data.secret);
            updateTotp();
        }
        if (item.id === "auth-csrf" && typeof data.token === "string") setVariable("csrfToken", data.token);

        if (item.id === "social-authorize" && typeof data.authorizationUrl === "string") {
            const url = new URL(data.authorizationUrl, normalizeBase(dom.apiBase.value));
            setVariable("socialCode", url.searchParams.get("code") || "");
            setVariable("socialState", url.searchParams.get("state") || "");
        }

        if (item.id === "passkeys-list" && Array.isArray(data) && data[0]?.credentialId) {
            setVariable("credentialId", data[0].credentialId);
        }

        if (item.id === "auth-login" && response.status === 202) {
            showResponseNotice("Parola doğrulandı; MFA gerekiyor. Ticket kasaya alındı. “MFA girişini tamamla” endpoint’i canlı TOTP veya recovery code ile hazır.");
        } else if (sessionIssuingEndpoints.has(item.id) && response.ok && state.authMode === "cookie") {
            const csrf = readCookie("__Host-auth.csrf");
            if (csrf) setVariable("csrfToken", csrf);
            showResponseNotice("Cookie modu aktif: tokenlar güvenlik için yanıt gövdesinde gösterilmez; HttpOnly cookie’lere kaydedildi. Korumalı endpoint’leri doğrudan çalıştırabilirsiniz.");
        } else if (data.accessToken) {
            showResponseNotice("Access ve refresh token bu sekmedeki kimlik kasasına otomatik alındı.");
        } else if (data.key) {
            showResponseNotice("Yeni API key kasaya alındı. Secret sunucu tarafından bir daha gösterilmeyecek.");
        } else if (data.secret) {
            showResponseNotice("TOTP secret yalnızca bu yanıtta gösterilir. Workbench canlı altı haneli kodu üretmeye başladı.");
        } else if (Array.isArray(data.codes)) {
            showResponseNotice("Recovery code’lar yalnızca bu yanıtta görünür. Yanıtı şimdi kopyalayın.");
        } else {
            hideResponseNotice();
        }

        hydrateVaultFields();
        updateVaultStatus();
        updateRequestPreview();
    }

    function displayResponse(response, text, data, elapsed) {
        state.responseText = text || "(boş yanıt)";
        state.responseData = data;
        const ok = response.ok;
        dom.responseCode.textContent = String(response.status);
        dom.responseCode.className = `response-code ${ok ? "success" : "error"}`;
        dom.responseLabel.textContent = `${response.status} ${response.statusText || (ok ? "Başarılı" : "Hata")}`;
        const correlation = response.headers.get("x-correlation-id");
        dom.responseMeta.textContent = `${elapsed} ms${correlation ? ` · ${correlation}` : ""}`;
        dom.responseViewer.textContent = formatResponse(text, data);
        dom.responseTabStatus.className = ok ? "success" : "error";
        renderResponseHeaders(response.headers);
        switchPanelTab("response");
    }

    function displayNetworkError(message, elapsed) {
        state.responseText = message;
        state.responseData = null;
        dom.responseCode.textContent = "ERR";
        dom.responseCode.className = "response-code error";
        dom.responseLabel.textContent = "Ağ hatası";
        dom.responseMeta.textContent = `${elapsed} ms`;
        dom.responseViewer.textContent = message;
        dom.responseTabStatus.className = "error";
        dom.responseHeaders.replaceChildren();
        switchPanelTab("response");
    }

    function renderResponseHeaders(headers) {
        dom.responseHeaders.replaceChildren();
        ["content-type", "x-correlation-id", "retry-after", "api-supported-versions"].forEach((name) => {
            const value = headers.get(name);
            if (!value) return;
            const row = document.createElement("div");
            const term = document.createElement("dt");
            term.textContent = name;
            const description = document.createElement("dd");
            description.textContent = value;
            row.append(term, description);
            dom.responseHeaders.append(row);
        });
    }

    function parseResponse(text) {
        if (!text) return null;
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    function formatResponse(text, data) {
        if (!text) return "(204 · response body yok)";
        return typeof data === "object" && data !== null
            ? JSON.stringify(data, null, 2)
            : text;
    }

    function resetResponse() {
        state.responseText = "";
        state.responseData = null;
        dom.responseCode.textContent = "—";
        dom.responseCode.className = "response-code idle";
        dom.responseLabel.textContent = "Henüz yanıt yok";
        dom.responseMeta.textContent = "İsteği gönderdiğinizde burada görünür.";
        dom.responseViewer.textContent = '{\n  "message": "Bir istek gönderin."\n}';
        dom.responseHeaders.replaceChildren();
        dom.responseTabStatus.className = "";
        hideResponseNotice();
    }

    function switchPanelTab(name) {
        document.querySelectorAll("[data-panel-tab]").forEach((button) => {
            const selected = button.dataset.panelTab === name;
            button.setAttribute("aria-selected", String(selected));
            button.tabIndex = selected ? 0 : -1;
        });
        byId("request-tab").hidden = name !== "request";
        byId("response-tab").hidden = name !== "response";
    }

    function showResponseNotice(text) {
        dom.responseNotice.hidden = false;
        dom.responseNotice.textContent = text;
    }

    function hideResponseNotice() {
        dom.responseNotice.hidden = true;
        dom.responseNotice.textContent = "";
    }

    async function demoLogin(kind) {
        const account = DEMO[kind];
        if (!account) return;
        setAuthMode(state.authMode === "apiKey" ? "bearer" : state.authMode);
        const item = endpoints.find((candidate) => candidate.id === "auth-login");
        selectEndpoint(item.id);
        dom.requestBody.value = JSON.stringify({ email: account.email, password: account.password }, null, 2);
        const result = await executeEndpoint(item);
        if (!result) return;
        if (result.response.ok && result.response.status !== 202) {
            toast(`${kind === "admin" ? "Admin" : "Kullanıcı"} oturumu hazır`, account.email);
            if (state.authMode === "cookie") await ensureCsrf();
        } else if (result.response.status === 202) {
            toast("MFA adımı gerekiyor", "Ticket kasaya alındı; canlı kodla tamamlayın.");
        }
    }

    async function prepareSessionScenario() {
        setAuthMode("bearer");
        const item = endpoints.find((candidate) => candidate.id === "auth-login");
        const body = { email: DEMO.user.email, password: DEMO.user.password };
        const first = await executeEndpoint(item, { body, path: item.path, display: false });
        const second = await executeEndpoint(item, { body, path: item.path, display: false });
        if (first?.response.ok && second?.response.ok) {
            captureResponseData(item, second.data, second.response);
            toast("Oturum senaryosu hazır", "İki yeni oturum açıldı; son oturum aktif kasaya alındı.");
            selectEndpoint("sessions-list");
        } else {
            toast("Senaryo hazırlanamadı", "Hesap MFA istiyor olabilir veya API erişilemiyor.", true);
        }
    }

    function useDemoApiKey() {
        setVariable("apiKey", DEMO.apiKey);
        hydrateVaultFields();
        setAuthMode("apiKey");
        updateVaultStatus();
        toast("Demo API key aktif", "Admin rolüyle kesişen yedi kapsam hazır.");
    }

    async function runDemoSocial(provider) {
        if (!["google", "github"].includes(provider)) {
            toast("Desteklenmeyen provider", "Demo fixture yalnızca google ve github kabul eder.", true);
            return;
        }

        setAuthMode("bearer");
        const authorize = endpoints.find((item) => item.id === "social-authorize");
        const callback = endpoints.find((item) => item.id === "social-callback");
        toast("OAuth fixture başlatıldı", `${provider} için yerel state üretiliyor.`);

        const first = await performRequest(
            authorize,
            `/api/v1/auth/social/${provider}/authorize`,
            undefined,
            buildHeaders(authorize),
            false);
        if (!first?.response.ok || !first.data?.authorizationUrl) {
            toast("OAuth authorize başarısız", "Development demo modu açık mı kontrol edin.", true);
            return;
        }

        const callbackUrl = new URL(first.data.authorizationUrl, normalizeBase(dom.apiBase.value));
        setVariable("socialCode", callbackUrl.searchParams.get("code") || "");
        setVariable("socialState", callbackUrl.searchParams.get("state") || "");
        selectEndpoint(callback.id);
        const path = `${callbackUrl.pathname}${callbackUrl.search}`;
        dom.requestPath.value = path;
        const second = await performRequest(callback, path, undefined, buildHeaders(callback), true);
        if (second?.response.ok) {
            toast(`${provider} demo girişi hazır`, "Gerçek sağlayıcıya ağ isteği yapılmadı.");
        }
    }

    async function ensureCsrf() {
        const item = endpoints.find((candidate) => candidate.id === "auth-csrf");
        const result = await performRequest(item, item.path, undefined, buildHeaders(item), false);
        if (result?.response.ok && result.data?.token) {
            setVariable("csrfToken", result.data.token);
            hydrateVaultFields();
        }
    }

    async function runPasskeyRegistration() {
        if (!window.PublicKeyCredential || !navigator.credentials) {
            toast("WebAuthn desteklenmiyor", "Güncel bir tarayıcı ve güvenli localhost context’i gerekir.", true);
            return;
        }
        const optionsEndpoint = endpoints.find((item) => item.id === "passkey-registration-options");
        const completeEndpoint = endpoints.find((item) => item.id === "passkey-registration-complete");
        try {
            const optionsResult = await performRequest(
                optionsEndpoint,
                optionsEndpoint.path,
                { label: "Workbench passkey" },
                buildHeaders(optionsEndpoint),
                false);
            if (!optionsResult?.response.ok) throw new Error(responseMessage(optionsResult));

            const publicKey = decodeCreationOptions(optionsResult.data.options);
            const credential = await navigator.credentials.create({ publicKey });
            if (!credential) throw new Error("Authenticator credential üretmedi.");

            selectEndpoint(completeEndpoint.id);
            const body = {
                attestationResponse: credentialToJson(credential),
                label: "Workbench passkey",
            };
            dom.requestBody.value = JSON.stringify(body, null, 2);
            const completed = await executeEndpoint(completeEndpoint, { body });
            if (completed?.response.ok) toast("Passkey kaydedildi", "Credential listesi endpoint’inden görülebilir.");
        } catch (error) {
            toast("Passkey kaydı tamamlanamadı", error.message || String(error), true);
        }
    }

    async function runPasskeyAuthentication() {
        if (!window.PublicKeyCredential || !navigator.credentials) {
            toast("WebAuthn desteklenmiyor", "Güncel bir tarayıcı ve güvenli localhost context’i gerekir.", true);
            return;
        }
        const optionsEndpoint = endpoints.find((item) => item.id === "passkey-auth-options");
        const completeEndpoint = endpoints.find((item) => item.id === "passkey-auth-complete");
        try {
            const optionsResult = await performRequest(
                optionsEndpoint,
                optionsEndpoint.path,
                {},
                buildHeaders(optionsEndpoint),
                false);
            if (!optionsResult?.response.ok) throw new Error(responseMessage(optionsResult));

            const publicKey = decodeRequestOptions(optionsResult.data.options);
            const credential = await navigator.credentials.get({ publicKey });
            if (!credential) throw new Error("Authenticator assertion üretmedi.");

            selectEndpoint(completeEndpoint.id);
            const body = { assertionResponse: credentialToJson(credential) };
            dom.requestBody.value = JSON.stringify(body, null, 2);
            const completed = await executeEndpoint(completeEndpoint, { body });
            if (completed?.response.ok) toast("Passkey oturumu hazır", "Token’lar kimlik kasasına alındı.");
        } catch (error) {
            toast("Passkey girişi tamamlanamadı", error.message || String(error), true);
        }
    }

    function decodeCreationOptions(options) {
        const copy = structuredClone(options);
        copy.challenge = base64UrlToBytes(copy.challenge);
        copy.user.id = base64UrlToBytes(copy.user.id);
        if (Array.isArray(copy.excludeCredentials)) {
            copy.excludeCredentials = copy.excludeCredentials.map((credential) => ({
                ...credential,
                id: base64UrlToBytes(credential.id),
            }));
        }
        return copy;
    }

    function decodeRequestOptions(options) {
        const copy = structuredClone(options);
        copy.challenge = base64UrlToBytes(copy.challenge);
        if (Array.isArray(copy.allowCredentials)) {
            copy.allowCredentials = copy.allowCredentials.map((credential) => ({
                ...credential,
                id: base64UrlToBytes(credential.id),
            }));
        }
        return copy;
    }

    function credentialToJson(credential) {
        if (typeof credential.toJSON === "function") return credential.toJSON();
        const response = credential.response;
        const json = {
            id: credential.id,
            rawId: bytesToBase64Url(credential.rawId),
            type: credential.type,
            clientExtensionResults: credential.getClientExtensionResults(),
            authenticatorAttachment: credential.authenticatorAttachment,
            response: {
                clientDataJSON: bytesToBase64Url(response.clientDataJSON),
            },
        };
        if ("attestationObject" in response) {
            json.response.attestationObject = bytesToBase64Url(response.attestationObject);
            json.response.transports = typeof response.getTransports === "function" ? response.getTransports() : [];
        } else {
            json.response.authenticatorData = bytesToBase64Url(response.authenticatorData);
            json.response.signature = bytesToBase64Url(response.signature);
            json.response.userHandle = response.userHandle ? bytesToBase64Url(response.userHandle) : null;
        }
        return json;
    }

    function base64UrlToBytes(value) {
        const base64 = value.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(value.length / 4) * 4, "=");
        return Uint8Array.from(atob(base64), (character) => character.charCodeAt(0));
    }

    function bytesToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = "";
        bytes.forEach((byte) => { binary += String.fromCharCode(byte); });
        return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");
    }

    function responseMessage(result) {
        if (result?.data?.detail) return result.data.detail;
        if (result?.data?.title) return result.data.title;
        return `HTTP ${result?.response?.status || "error"}`;
    }

    function updateTotpContinuously() {
        updateTotp();
        window.setInterval(updateTotp, 1000);
    }

    async function updateTotp() {
        if (!state.variables.totpSecret || !window.crypto?.subtle) return;
        try {
            const code = await computeTotp(state.variables.totpSecret);
            if (code === state.variables.totpCode) return;
            state.variables.totpCode = code;
            if (selectedEndpoint()?.special === "totp" || state.selectedId === "auth-login-mfa") {
                renderSpecialActions(selectedEndpoint());
            }
        } catch {
            state.variables.totpCode = "";
        }
    }

    async function computeTotp(secret) {
        const keyBytes = decodeBase32(secret);
        const counter = Math.floor(Date.now() / 1000 / 30);
        const counterBytes = new Uint8Array(8);
        let remaining = counter;
        for (let index = 7; index >= 0; index -= 1) {
            counterBytes[index] = remaining & 0xff;
            remaining = Math.floor(remaining / 256);
        }
        const key = await crypto.subtle.importKey(
            "raw",
            keyBytes,
            { name: "HMAC", hash: "SHA-1" },
            false,
            ["sign"]);
        const digest = new Uint8Array(await crypto.subtle.sign("HMAC", key, counterBytes));
        const offset = digest[digest.length - 1] & 0x0f;
        const binary = ((digest[offset] & 0x7f) << 24)
            | (digest[offset + 1] << 16)
            | (digest[offset + 2] << 8)
            | digest[offset + 3];
        return String(binary % 1_000_000).padStart(6, "0");
    }

    function decodeBase32(value) {
        const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        const clean = value.toUpperCase().replace(/=+$/g, "").replace(/\s+/g, "");
        let bits = "";
        for (const character of clean) {
            const index = alphabet.indexOf(character);
            if (index < 0) throw new Error("Invalid Base32");
            bits += index.toString(2).padStart(5, "0");
        }
        const bytes = [];
        for (let index = 0; index + 8 <= bits.length; index += 8) {
            bytes.push(Number.parseInt(bits.slice(index, index + 8), 2));
        }
        return new Uint8Array(bytes);
    }

    function fillTotpCode() {
        if (!state.variables.totpCode) {
            toast("TOTP secret bulunamadı", "Önce enrollment endpoint’ini başarıyla çalıştırın.", true);
            return;
        }
        try {
            const body = JSON.parse(dom.requestBody.value || "{}");
            if ("code" in body) body.code = state.variables.totpCode;
            dom.requestBody.value = JSON.stringify(body, null, 2);
            toast("Canlı kod yazıldı", state.variables.totpCode);
        } catch {
            toast("JSON gövdesi geçersiz", "Kod gövdeye yazılamadı.", true);
        }
    }

    async function checkServices() {
        const buttons = [...document.querySelectorAll("[data-health-path]")];
        await Promise.all(buttons.map(async (button) => {
            const path = button.dataset.healthPath;
            const dot = button.querySelector(".health-dot");
            const label = button.querySelector("strong");
            dot.className = "health-dot checking";
            label.textContent = "Kontrol ediliyor";
            try {
                const response = await fetch(`${normalizeBase(dom.apiBase.value)}${path}`, {
                    headers: { Accept: path.includes("openapi") ? "application/json" : "text/plain" },
                    credentials: "include",
                });
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                dot.className = "health-dot healthy";
                if (path.includes("openapi")) {
                    const document = await response.json();
                    const count = Object.values(document.paths || {}).reduce(
                        (sum, pathItem) => sum + Object.keys(pathItem).filter((method) =>
                            ["get", "post", "put", "patch", "delete", "head", "options"].includes(method)).length,
                        0);
                    label.textContent = `${count} işlem hazır`;
                    byId("coverage-count").textContent = `${count}/43`;
                } else {
                    label.textContent = "Healthy";
                }
            } catch {
                dot.className = "health-dot unhealthy";
                label.textContent = "Erişilemiyor";
            }
        }));
    }

    function setAuthMode(mode) {
        state.authMode = ["bearer", "cookie", "apiKey"].includes(mode) ? mode : "bearer";
        sessionStorage.setItem(storageKeys.authMode, state.authMode);
        updateAuthMode();
        updateVaultStatus();
        updateRequestPreview();
    }

    function updateAuthMode() {
        document.querySelectorAll("#auth-mode button").forEach((button) => {
            const active = button.dataset.mode === state.authMode;
            button.classList.toggle("active", active);
            // aria-checked, not aria-pressed: these are radios in a radiogroup, and only
            // the selected one stays in the tab order (roving tabindex).
            button.setAttribute("aria-checked", String(active));
            button.tabIndex = active ? 0 : -1;
        });
    }

    function hydrateVaultFields() {
        dom.accessToken.value = state.variables.accessToken;
        dom.refreshToken.value = state.variables.refreshToken;
        dom.apiKey.value = state.variables.apiKey;
        dom.csrfToken.value = state.variables.csrfToken;
    }

    function updateVaultStatus() {
        const mode = state.authMode;
        const ready = mode === "cookie"
            ? document.cookie.includes("auth.access") || Boolean(state.variables.csrfToken)
            : mode === "apiKey"
                ? Boolean(state.variables.apiKey)
                : Boolean(state.variables.accessToken);

        dom.vaultOrb.className = `status-orb ${ready ? "ready" : "idle"}`;
        if (ready) {
            dom.vaultTitle.textContent = mode === "apiKey"
                ? "API key aktif"
                : mode === "cookie"
                    ? "Cookie oturumu aktif"
                    : "Bearer token aktif";
            dom.vaultSubtitle.textContent = mode === "cookie"
                ? "Mutasyonlarda CSRF otomatik eklenir."
                : "Yalnızca bu tarayıcı sekmesinde saklanır.";
        } else {
            dom.vaultTitle.textContent = "Oturum bekleniyor";
            // The idle state is not an error state: lead with the action in every mode.
            // The __Host- cookie prefix does need HTTPS, but that is a footnote here, not
            // the first thing a user sees after picking cookie transport.
            dom.vaultSubtitle.textContent = mode === "cookie"
                ? "Demo hesaplardan biriyle giriş yapın (https profili gerekir)."
                : "Demo hesaplardan biriyle giriş yapın.";
        }
    }

    function clearVault() {
        Object.keys(storageKeys).forEach((name) => {
            if (name !== "authMode") sessionStorage.removeItem(storageKeys[name]);
        });
        Object.keys(state.variables).forEach((name) => { state.variables[name] = ""; });
        hydrateVaultFields();
        updateVaultStatus();
        updateRequestPreview();
        toast("Kimlik kasası temizlendi", "Sunucudaki oturumlar ayrıca logout/revoke edilmelidir.");
    }

    function setVariable(name, value) {
        state.variables[name] = value || "";
        if (storageKeys[name]) {
            if (value) sessionStorage.setItem(storageKeys[name], value);
            else sessionStorage.removeItem(storageKeys[name]);
        }
    }

    function materialize(value) {
        if (Array.isArray(value)) return value.map(materialize);
        if (value && typeof value === "object") {
            return Object.fromEntries(Object.entries(value).map(([key, child]) => [key, materialize(child)]));
        }
        return typeof value === "string" ? resolveTemplateString(value) : value;
    }

    function resolveTemplateString(value) {
        return value.replace(/\{\{([a-zA-Z0-9]+)\}\}/g, (match, name) => state.variables[name] || match);
    }

    function buildPathFromDefinition(item) {
        let path = item.path;
        item.pathParams.forEach((parameter) => {
            path = path.replace(`{${parameter.name}}`, encodeURIComponent(resolveTemplateString(parameter.value)));
        });
        const query = new URLSearchParams();
        item.query.forEach((parameter) => {
            const value = resolveTemplateString(parameter.value);
            if (value && !value.startsWith("{{")) query.set(parameter.name, value);
        });
        const encoded = query.toString();
        return encoded ? `${path}?${encoded}` : path;
    }

    function selectedEndpoint() {
        return endpoints.find((item) => item.id === state.selectedId) || null;
    }

    function normalizeBase(value) {
        const trimmed = (value || window.location.origin).trim().replace(/\/+$/, "");
        return trimmed || window.location.origin;
    }

    function isUnsafe(method) {
        return !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);
    }

    function readCookie(name) {
        const prefix = `${encodeURIComponent(name)}=`;
        const pair = document.cookie.split("; ").find((candidate) => candidate.startsWith(prefix));
        return pair ? decodeURIComponent(pair.slice(prefix.length)) : "";
    }

    function formatRequestJson() {
        try {
            dom.requestBody.value = JSON.stringify(JSON.parse(dom.requestBody.value), null, 2);
        } catch {
            toast("JSON biçimlendirilemedi", "Gövde geçerli JSON değil.", true);
        }
    }

    async function copyCurl() {
        const item = selectedEndpoint();
        if (!item) return;
        let body;
        try {
            body = item.body === null ? undefined : materialize(JSON.parse(dom.requestBody.value || "{}"));
        } catch {
            toast("cURL oluşturulamadı", "Önce JSON gövdesini düzeltin.", true);
            return;
        }
        const url = `${normalizeBase(dom.apiBase.value)}${dom.requestPath.value}`;
        const headerArguments = Object.entries(buildHeaders(item))
            .map(([name, value]) => `-H '${shellEscape(`${name}: ${value}`)}'`)
            .join(" \\\n  ");
        const bodyArgument = body === undefined
            ? ""
            : ` \\\n  --data '${shellEscape(JSON.stringify(body))}'`;
        const command = `curl -i -X ${item.method} '${shellEscape(url)}'${headerArguments ? ` \\\n  ${headerArguments}` : ""}${bodyArgument}`;
        await copyText(command, "cURL komutu kopyalandı");
    }

    function shellEscape(value) {
        return value.replace(/'/g, "'\"'\"'");
    }

    async function copyText(value, successTitle) {
        if (!value) return;
        try {
            await navigator.clipboard.writeText(value);
            toast(successTitle, "Panoya yazıldı.");
        } catch {
            toast("Pano erişimi reddedildi", "Tarayıcı izinlerini kontrol edin.", true);
        }
    }

    function toast(title, message, error = false) {
        const node = document.createElement("div");
        node.className = `toast${error ? " error" : ""}`;
        const copy = document.createElement("div");
        const strong = document.createElement("strong");
        strong.textContent = title;
        const detail = document.createElement("span");
        detail.textContent = message;
        copy.append(strong, detail);

        const dismiss = document.createElement("button");
        dismiss.type = "button";
        dismiss.className = "toast-dismiss";
        dismiss.setAttribute("aria-label", "Bildirimi kapat");
        dismiss.append(icon("i-close"));
        dismiss.addEventListener("click", () => node.remove());

        node.append(copy, dismiss);
        dom.toastRegion.append(node);

        // A failed passkey ceremony, TOTP fill or clipboard write has no other surface —
        // the toast body is the only copy of the reason. Errors wait to be dismissed.
        if (!error) window.setTimeout(() => node.remove(), 4200);
    }

    function openDrawer(element) {
        const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        closeDrawers(false);
        element.classList.add("open");
        drawerReturnFocus = trigger;
        syncDrawerState();
        element.focus();
    }

    function closePanels() {
        closeDrawers(true);
    }

    function closeDrawers(restoreFocus) {
        const wasOpen = dom.requestPanel.classList.contains("open")
            || dom.leftRail.classList.contains("open");
        dom.requestPanel.classList.remove("open");
        dom.leftRail.classList.remove("open");
        syncDrawerState();
        if (!restoreFocus) return;
        if (wasOpen && drawerReturnFocus?.isConnected) drawerReturnFocus.focus();
        drawerReturnFocus = null;
    }

    /// A drawer parked off-canvas with translateX() is still in the tab order, so keyboard
    /// users would tab through ~15 invisible controls. `inert` is the only thing that takes
    /// it out without also breaking the slide transition. The same call makes an open drawer
    /// genuinely modal by inerting every sibling region behind the backdrop.
    function syncDrawerState() {
        const railIsDrawer = drawerQueries.rail.matches;
        const panelIsDrawer = drawerQueries.panel.matches;
        const railOpen = railIsDrawer && dom.leftRail.classList.contains("open");
        const panelOpen = panelIsDrawer && dom.requestPanel.classList.contains("open");
        const openDrawerElement = railOpen ? dom.leftRail : panelOpen ? dom.requestPanel : null;

        [dom.topbar, dom.leftRail, dom.mainContent, dom.requestPanel].forEach((region) => {
            region.inert = openDrawerElement
                ? region !== openDrawerElement
                : (region === dom.leftRail && railIsDrawer)
                    || (region === dom.requestPanel && panelIsDrawer);
        });

        [[dom.leftRail, railOpen], [dom.requestPanel, panelOpen]].forEach(([element, isModal]) => {
            if (isModal) {
                element.setAttribute("role", "dialog");
                element.setAttribute("aria-modal", "true");
            } else {
                element.removeAttribute("role");
                element.removeAttribute("aria-modal");
            }
        });

        dom.panelBackdrop.hidden = !openDrawerElement;
    }
})();
