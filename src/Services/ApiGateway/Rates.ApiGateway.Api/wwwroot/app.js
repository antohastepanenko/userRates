(() => {
  "use strict";

  const API_BASE = "/api/v1";

  const STORAGE = {
    ACCESS: "rates.accessToken",
    REFRESH: "rates.refreshToken",
    NAME: "rates.userName",
  };

  const views = {
    auth: document.getElementById("view-auth"),
    account: document.getElementById("view-account"),
  };

  const els = {
    authForm: document.getElementById("auth-form"),
    authLogin: document.getElementById("auth-login"),
    authRegister: document.getElementById("auth-register"),
    authName: document.getElementById("auth-name"),
    authPassword: document.getElementById("auth-password"),
    authError: document.getElementById("auth-error"),

    accountGreeting: document.getElementById("account-greeting"),
    accountMeta: document.getElementById("account-meta"),
    accountLogout: document.getElementById("account-logout"),

    favoritesList: document.getElementById("favorites-list"),
    favoritesEmpty: document.getElementById("favorites-empty"),
    favoritesError: document.getElementById("favorites-error"),
    addFavoriteForm: document.getElementById("add-favorite-form"),
    addFavoriteCode: document.getElementById("add-favorite-code"),

    apiBase: document.getElementById("api-base"),
  };

  els.apiBase.textContent = API_BASE;

  // ---------- helpers ----------

  function show(view) {
    for (const [name, el] of Object.entries(views)) {
      el.hidden = name !== view;
    }
  }

  function showError(target, message) {
    target.textContent = message ?? "";
    target.hidden = !message;
  }

  function getTokens() {
    return {
      accessToken: localStorage.getItem(STORAGE.ACCESS),
      refreshToken: localStorage.getItem(STORAGE.REFRESH),
    };
  }

  function setTokens({ accessToken, refreshToken }) {
    if (accessToken) localStorage.setItem(STORAGE.ACCESS, accessToken);
    if (refreshToken) localStorage.setItem(STORAGE.REFRESH, refreshToken);
  }

  function clearSession() {
    localStorage.removeItem(STORAGE.ACCESS);
    localStorage.removeItem(STORAGE.REFRESH);
    localStorage.removeItem(STORAGE.NAME);
  }

  function authHeaders() {
    const { accessToken } = getTokens();
    return accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
  }

  async function parseProblem(response) {
    let detail = null;
    try {
      const data = await response.json();
      detail = data.detail || data.title || data.message || data.error;
      if (Array.isArray(data.errors) && data.errors.length) {
        const first = data.errors[0];
        if (first?.description) detail = first.description;
      }
    } catch {
      // ignore — body is not JSON
    }
    return detail ?? `Запрос завершился с ошибкой ${response.status}`;
  }

  async function request(path, { method = "GET", body, auth = true } = {}) {
    const headers = { "Content-Type": "application/json" };
    if (auth) Object.assign(headers, authHeaders());

    const response = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });

    if (response.status === 401) {
      clearSession();
      enterAuth();
      throw new Error("Сессия истекла или недействительна. Войдите снова.");
    }

    if (!response.ok) {
      throw new Error(await parseProblem(response));
    }

    if (response.status === 204) return null;
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("application/json")) return response.json();
    return null;
  }

  // ---------- views: transitions ----------

  function enterAuth() {
    show("auth");
    showError(els.authError, null);
    els.authName.focus();
  }

  function enterAccount() {
    show("account");
    els.accountGreeting.textContent = `Привет, ${localStorage.getItem(STORAGE.NAME) ?? "пользователь"}!`;
    Promise.all([loadProfile(), loadFavorites(), loadAvailableCurrencies()]).catch(() => {
      /* отдельные ошибки уже показаны */
    });
  }

  // ---------- loaders ----------

  async function loadProfile() {
    const data = await request("/users/me");
    if (data?.name) {
      localStorage.setItem(STORAGE.NAME, data.name);
      els.accountGreeting.textContent = `Привет, ${data.name}!`;
    }
    if (data?.createdAt) {
      els.accountMeta.textContent = `Создан: ${new Date(data.createdAt).toLocaleString()}`;
    }
  }

  async function loadFavorites() {
    const data = await request("/users/me/favorites");
    const items = data?.items ?? [];
    renderFavorites(items);
  }

  async function loadAvailableCurrencies() {
    const select = els.addFavoriteCode;
    select.innerHTML = '<option value="">Загрузка…</option>';
    try {
      const data = await request("/finance/currencies/me");
      const items = data?.items ?? [];
      populateAvailableCurrencies(items);
    } catch (err) {
      select.innerHTML = '<option value="">—</option>';
      showError(els.favoritesError, `Не удалось загрузить список валют: ${err.message}`);
    }
  }

  function populateAvailableCurrencies(items) {
    const select = els.addFavoriteCode;
    select.innerHTML = "";
    if (!items.length) {
      const opt = document.createElement("option");
      opt.value = "";
      opt.textContent = "Справочник валют пуст";
      select.appendChild(opt);
      select.disabled = true;
      return;
    }
    select.disabled = false;
    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "— выберите валюту —";
    select.appendChild(placeholder);
    for (const item of items) {
      const opt = document.createElement("option");
      opt.value = item.code;
      opt.textContent = item.name ? `${item.code} — ${item.name}` : item.code;
      select.appendChild(opt);
    }
  }

  function renderFavorites(items) {
    els.favoritesList.innerHTML = "";
    if (!items.length) {
      els.favoritesEmpty.hidden = false;
      return;
    }
    els.favoritesEmpty.hidden = true;
    for (const item of items) {
      const li = document.createElement("li");

      const left = document.createElement("div");
      const code = document.createElement("span");
      code.className = "favorite-code";
      code.textContent = item.code;
      const meta = document.createElement("span");
      meta.className = "favorite-meta";
      meta.textContent = item.addedAt ? ` · добавлена ${new Date(item.addedAt).toLocaleDateString()}` : "";
      left.append(code, meta);

      const remove = document.createElement("button");
      remove.type = "button";
      remove.className = "danger";
      remove.textContent = "Удалить";
      remove.addEventListener("click", () => removeFavorite(item.code));

      li.append(left, remove);
      els.favoritesList.appendChild(li);
    }
  }

  // ---------- actions ----------

  async function register(name, password) {
    const data = await request("/auth/register", {
      method: "POST",
      body: { name, password },
      auth: false,
    });
    if (data?.accessToken && data?.refreshToken) {
      setTokens(data);
      enterAccount();
    } else {
      enterAuth();
    }
  }

  async function login(name, password) {
    const data = await request("/auth/login", {
      method: "POST",
      body: { name, password },
      auth: false,
    });
    if (!data?.accessToken) {
      throw new Error("Сервер не вернул токен");
    }
    setTokens(data);
    enterAccount();
  }

  async function logout() {
    const { refreshToken, accessToken } = getTokens();
    const wasAuthenticated = Boolean(accessToken);
    if (wasAuthenticated && refreshToken) {
      try {
        await request("/auth/logout", {
          method: "POST",
          body: { refreshToken },
        });
      } catch (err) {
        // даже если отзыв не удался — клиентское состояние всё равно чистим
        console.warn("Logout request failed:", err);
      }
    }
    clearSession();
    enterAuth();
  }

  async function addFavorite(code) {
    showError(els.favoritesError, null);
    await request(`/users/me/favorites/${encodeURIComponent(code)}`, {
      method: "PUT",
    });
    await loadFavorites();
  }

  async function removeFavorite(code) {
    showError(els.favoritesError, null);
    await request(`/users/me/favorites/${encodeURIComponent(code)}`, {
      method: "DELETE",
    });
    await loadFavorites();
  }

  // ---------- event wiring ----------

  els.authForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    showError(els.authError, null);
    const name = els.authName.value.trim();
    const password = els.authPassword.value;
    if (!name || !password) return;
    els.authLogin.disabled = true;
    els.authRegister.disabled = true;
    try {
      await login(name, password);
    } catch (err) {
      showError(els.authError, err.message);
    } finally {
      els.authLogin.disabled = false;
      els.authRegister.disabled = false;
    }
  });

  els.authRegister.addEventListener("click", async () => {
    showError(els.authError, null);
    const name = els.authName.value.trim();
    const password = els.authPassword.value;
    if (!name || !password) return;
    els.authLogin.disabled = true;
    els.authRegister.disabled = true;
    try {
      await register(name, password);
    } catch (err) {
      showError(els.authError, err.message);
    } finally {
      els.authLogin.disabled = false;
      els.authRegister.disabled = false;
    }
  });

  els.accountLogout.addEventListener("click", async () => {
    els.accountLogout.disabled = true;
    try {
      await logout();
    } finally {
      els.accountLogout.disabled = false;
    }
  });

  els.addFavoriteForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    const code = els.addFavoriteCode.value;
    if (!code) return;
    try {
      await addFavorite(code);
      els.addFavoriteCode.value = "";
    } catch (err) {
      showError(els.favoritesError, err.message);
    }
  });

  // ---------- bootstrap ----------

  const { accessToken } = getTokens();
  if (accessToken) {
    enterAccount();
  } else {
    enterAuth();
  }
})();
