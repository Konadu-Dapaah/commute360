// Extra admin features. Loaded after the main script in admin-dashboard.html, so it can use
// its helpers ($, h, routes, readError) and the shared Auth helper.

// ---------- Driver assignment (shown under each route in "Routes & stops") ----------
let extraDrivers = null;

async function fetchDrivers() {
  if (extraDrivers) return extraDrivers;
  try {
    const res = await Auth.api("/api/admin/users");
    if (res.ok) extraDrivers = (await res.json()).filter((u) => u.role === "Driver");
  } catch (err) {
    if (err.message !== "Unauthorized") console.error(err);
  }
  return extraDrivers || [];
}

async function decorateRoutes() {
  const drivers = await fetchDrivers();
  const routesTab = $("tab-routes");
  if (!routesTab) return;

  const panels = Array.from(routesTab.children);

  routes.forEach((r, i) => {
    const panel = panels[i];
    if (!panel || panel.dataset.decorated) return;
    panel.dataset.decorated = "1";

    const select = h("select", { class: "input" },
      h("option", { value: "", text: "Any driver (unassigned)" }),
      ...drivers.map((d) => h("option", { value: String(d.id), text: d.name })));
    select.value = r.driverId ? String(r.driverId) : "";

    const note = h("span", { class: "muted" });

    select.addEventListener("change", async () => {
      note.textContent = "Saving…";
      try {
        const res = await Auth.api(`/api/admin/routes/${r.id}/driver`, {
          method: "PUT",
          body: JSON.stringify({ driverId: select.value ? Number(select.value) : null })
        });
        if (res.ok) {
          r.driverId = select.value ? Number(select.value) : null;
          note.textContent = "Saved.";
        } else {
          select.value = r.driverId ? String(r.driverId) : "";
          note.textContent = await readError(res);
        }
      } catch (err) {
        if (err.message !== "Unauthorized") note.textContent = "Could not reach the server.";
      }
    });

    panel.append(h("div", { class: "form-row", style: "margin-top:12px" },
      h("span", { text: "Assigned driver:" }), select, note));
  });
}

// ---------- Reports ----------
async function loadReport() {
  const typeEl = $("reportType");
  const daysEl = $("reportDays");
  const msg = $("reportMsg");
  if (!typeEl || !daysEl || !msg) return;

  const type = typeEl.value;
  const days = daysEl.value;
  msg.className = "msg";
  msg.textContent = "Loading…";

  try {
    const res = await Auth.api(`/api/reports/${type}?days=${days}`);
    if (!res.ok) { msg.className = "msg is-error"; msg.textContent = await readError(res); return; }
    const report = await res.json();

    $("reportTitle").textContent = report.title;
    $("reportHead").innerHTML = "";
    $("reportHead").append(h("tr", {}, ...report.header.map((t) => h("th", { text: t }))));

    $("reportBody").innerHTML = "";
    if (report.rows.length === 0) {
      $("reportBody").append(h("tr", {}, h("td", { colspan: String(report.header.length), class: "muted", text: "No data yet." })));
    }
    report.rows.forEach((row) => $("reportBody").append(h("tr", {}, ...row.map((c) => h("td", { text: c })))));
    msg.textContent = "";
  } catch (err) {
    if (err.message !== "Unauthorized") { msg.className = "msg is-error"; msg.textContent = "Could not load the report."; }
  }
}

async function downloadCsv() {
  const typeEl = $("reportType");
  const daysEl = $("reportDays");
  const msg = $("reportMsg");
  if (!typeEl || !daysEl) return;

  const type = typeEl.value;
  const days = daysEl.value;
  try {
    const res = await Auth.api(`/api/reports/${type}/csv?days=${days}`);
    if (!res.ok) { 
      if (msg) { msg.className = "msg is-error"; msg.textContent = await readError(res); }
      return; 
    }

    const url = URL.createObjectURL(await res.blob());
    const a = document.createElement("a");
    a.href = url;
    a.download = `commute360-${type}.csv`;
    document.body.append(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (err) {
    if (err.message !== "Unauthorized" && msg) { 
      msg.className = "msg is-error"; 
      msg.textContent = "Download failed."; 
    }
  }
}

if ($("reportLoad")) $("reportLoad").addEventListener("click", loadReport);
if ($("reportType")) $("reportType").addEventListener("change", loadReport);
if ($("reportDays")) $("reportDays").addEventListener("change", loadReport);
if ($("reportCsv")) $("reportCsv").addEventListener("click", downloadCsv);

// ---------- Announcements ----------
function fillAnnouncementRoutes() {
  const select = $("annRoute");
  if (!select) return;
  if (select.options.length === routes.length + 1) return;
  select.innerHTML = "";
  select.append(h("option", { value: "", text: "Everyone" }),
    ...routes.map((r) => h("option", { value: String(r.id), text: `Riders on ${r.origin} → ${r.destination}` })));
}

async function loadAnnouncements() {
  fillAnnouncementRoutes();
  const body = $("annBody");
  if (!body) return;
  body.innerHTML = "";

  try {
    const res = await Auth.api("/api/announcements/sent");
    if (!res.ok) return;
    const items = await res.json();

    if (items.length === 0) {
      body.append(h("tr", {}, h("td", { colspan: "4", class: "muted", text: "Nothing sent yet." })));
      return;
    }

    items.forEach((a) => body.append(h("tr", {},
      h("td", { text: a.route ?? "—" }),
      h("td", { text: a.message }),
      h("td", { text: new Date(a.createdAt).toLocaleString([], { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" }) }),
      h("td", {}, h("button", {
        class: "mini-btn", type: "button", text: "Delete",
        onclick: async () => { await Auth.api(`/api/announcements/${a.id}`, { method: "DELETE" }); loadAnnouncements(); }
      }))
    )));
  } catch (err) {
    if (err.message !== "Unauthorized") console.error(err);
  }
}

if ($("annSend")) {
  $("annSend").addEventListener("click", async () => {
    const msg = $("annMsg");
    const textEl = $("annText");
    const routeEl = $("annRoute");
    if (!textEl || !msg) return;

    const message = textEl.value.trim();
    const routeId = routeEl && routeEl.value ? Number(routeEl.value) : null;

    if (message.length < 3) { 
      msg.className = "msg is-error"; 
      msg.textContent = "Write a message (at least 3 characters)."; 
      return; 
    }

    $("annSend").disabled = true;
    try {
      const res = await Auth.api("/api/announcements", { method: "POST", body: JSON.stringify({ message, routeId }) });
      if (res.ok) {
        msg.className = "msg is-success";
        msg.textContent = "Announcement sent. Riders see it on their dashboard.";
        textEl.value = "";
        loadAnnouncements();
      } else {
        msg.className = "msg is-error";
        msg.textContent = await readError(res);
      }
    } catch (err) {
      if (err.message !== "Unauthorized") { msg.className = "msg is-error"; msg.textContent = "Could not reach the server."; }
    } finally {
      $("annSend").disabled = false;
    }
  });
}

// ---------- SOS banner (checked every 10 seconds, on every tab) ----------
async function pollSos() {
  const box = $("sosBanner");
  if (!box) return;

  try {
    const res = await Auth.api("/api/safety/sos/active");
    if (!res.ok) return;
    const items = await res.json();

    box.innerHTML = "";
    box.hidden = items.length === 0;

    items.forEach((s) => {
      const hasPlace = s.latitude != null && s.longitude != null;
      const when = new Date(s.createdAt).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });

      box.append(h("div", { class: "banner banner--danger" },
        h("span", {},
          h("span", { class: "banner__title", text: "🚨 EMERGENCY · " }),
          `${s.driver} on ${s.route} at ${when}. ${s.message}`),
        h("span", { class: "form-row" },
          hasPlace
            ? h("a", {
                class: "mini-btn", target: "_blank", rel: "noopener", text: "Open in Maps",
                href: `https://www.google.com/maps?q=${Number(s.latitude)},${Number(s.longitude)}`
              })
            : "",
          h("button", {
            class: "mini-btn", type: "button", text: "Mark resolved",
            onclick: async () => { await Auth.api(`/api/safety/sos/${s.id}/resolve`, { method: "POST" }); pollSos(); }
          }))
      ));
    });
  } catch (err) {
    if (err.message !== "Unauthorized") console.error(err);
  }
}

pollSos();
setInterval(() => { if (!document.hidden) pollSos(); }, 10000);