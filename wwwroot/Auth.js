// Shared helpers for every page: token, API calls and role-based redirects.
(function () {
  const getApiBase = () => {
    return (window.APP_CONFIG && window.APP_CONFIG.API_BASE) || "";
  };

  // Role destination map
  const HOME = {
    Admin: "/admin-dashboard.html",
    Driver: "/driver.html",
    Staff: "/staff-dashboard.html"
  };

  function token() {
    return localStorage.getItem("token") || localStorage.getItem("authToken") || localStorage.getItem("jwt");
  }

  function logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("authToken");
    localStorage.removeItem("jwt");
    localStorage.removeItem("userRole");
    localStorage.removeItem("role");
    sessionStorage.clear();
    window.location.href = "/login.html";
  }

  async function api(path, options = {}) {
    const res = await fetch(getApiBase() + path, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token(),
        ...(options.headers || {})
      }
    });

    if (res.status === 401) {
      logout();
      throw new Error("Unauthorized");
    }
    return res;
  }

  async function me() {
    const res = await api("/api/me");
    if (!res.ok) throw new Error("Could not load your profile.");
    return res.json();
  }

  function homeFor(role) {
    return HOME[role] || HOME.Staff;
  }

  // Call this right after a successful login or registration.
  async function goHome() {
    const user = await me();
    window.location.href = homeFor(user.role);
  }

  // Enforces authorization per page
  async function requireRole(roles) {
    if (!token()) {
      window.location.href = "/login.html";
      throw new Error("Unauthorized");
    }
    const user = await me();
    if (!roles.includes(user.role)) {
      window.location.href = homeFor(user.role);
      throw new Error("Wrong role");
    }
    return user;
  }

  window.Auth = { apiBase: getApiBase(), token, api, me, homeFor, goHome, requireRole, logout };
})();