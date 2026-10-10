function renderNav() {
    const token = localStorage.getItem("token");
    const role = localStorage.getItem("role");

    let links = `<a href="/login.html">Login</a> | <a href="/register.html">Register</a>`;

    if (token) {
        links = `<a href="/dashboard.html">Dashboard</a> | <a href="/select-route.html">Book a Route</a>`;
        if (role === "Admin") links += ` | <a href="/admin.html">Admin</a>`;
        if (role === "Driver") links += ` | <a href="/driver.html">Driver</a>`;
        links += ` | <a href="#" onclick="logout()">Logout</a>`;
    }

    document.write(`<nav style="padding:10px; background:#eee; margin-bottom:16px;">${links}</nav>`);
}

function logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("role");
    window.location.href = "/login.html";
}

renderNav();