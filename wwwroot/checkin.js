// Rider check-in: "I've boarded" and "I've got off". Feeds the headcount shown to drivers and admins.
// Usage: <div id="checkinBox"></div>  then  Checkin.mount("checkinBox")
// The box stays hidden unless the person has a booking that runs today.
(function () {
  const base = () => (window.APP_CONFIG && window.APP_CONFIG.API_BASE) || "";
  const token = () => localStorage.getItem("token") || localStorage.getItem("authToken");

  async function call(path, method) {
    return fetch(base() + path, { method: method || "GET", headers: { Authorization: "Bearer " + token() } });
  }

  function addStyle() {
    if (document.getElementById("checkin-style")) return;
    const s = document.createElement("style");
    s.id = "checkin-style";
    s.textContent =
      ".ck{background:rgba(52,211,153,.10);border:1px solid rgba(52,211,153,.45);border-radius:14px;padding:14px 16px;margin-bottom:14px;color:#e8eefc}" +
      ".ck h3{margin:0 0 6px;font-size:13px;letter-spacing:.08em;text-transform:uppercase;opacity:.75}" +
      ".ck__status{font-size:16px;font-weight:700;margin-bottom:10px}" +
      ".ck__row{display:flex;flex-wrap:wrap;gap:10px;align-items:center}" +
      ".ck__btn{padding:12px 20px;border-radius:12px;border:0;font:700 15px inherit;font-family:inherit;cursor:pointer;background:#34d399;color:#04210f}" +
      ".ck__btn--off{background:#fbbf24;color:#2b1a00}" +
      ".ck__btn--ghost{background:transparent;color:#e8eefc;border:1px solid rgba(255,255,255,.35);font-weight:600}" +
      ".ck__btn:disabled{opacity:.5;cursor:not-allowed}" +
      ".ck__msg{font-size:13px;margin-top:8px;color:#fca5a5;min-height:16px}";
    document.head.appendChild(s);
  }

  const timeOf = (v) => new Date(v).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });

  function mount(id) {
    const box = document.getElementById(id);
    if (!box) return;
    addStyle();

    async function act(path, method, msgEl) {
      msgEl.textContent = "";
      try {
        const res = await call(path, method);
        if (res.ok) { render(await res.json()); return; }
        msgEl.textContent = (await res.text()) || "Something went wrong.";
      } catch { msgEl.textContent = "Could not reach the server."; }
    }

    function render(s) {
      // let the page know (the track page stops live tracking once the trip is complete)
      document.dispatchEvent(new CustomEvent("checkin:state", { detail: s }));
      box.innerHTML = "";
      if (!s || !s.hasBooking || !s.ridingToday) { box.hidden = true; return; }
      box.hidden = false;

      const wrap = document.createElement("div");
      wrap.className = "ck";
      const title = document.createElement("h3");
      title.textContent = "Today's ride";
      const status = document.createElement("div");
      status.className = "ck__status";
      const row = document.createElement("div");
      row.className = "ck__row";
      const msg = document.createElement("div");
      msg.className = "ck__msg";

      const button = (text, cls, path, method, confirmText) => {
        const b = document.createElement("button");
        b.type = "button";
        b.className = "ck__btn " + (cls || "");
        b.textContent = text;
        b.addEventListener("click", async () => {
          if (confirmText && !confirm(confirmText)) return;
          b.disabled = true;
          await act(path, method, msg);
          b.disabled = false;
        });
        row.append(b);
      };

      if (s.skipped) {
        status.textContent = "You marked today as skipped.";
      } else if (!s.boarded) {
        status.textContent = "You haven't boarded yet.";
        button("I've boarded", "", "/api/rides/board", "POST");
      } else if (!s.alighted) {
        status.textContent = "On the bus since " + timeOf(s.boardedAt);
        button("I've got off", "ck__btn--off", "/api/rides/alight", "POST",
          "Confirm you've got off the bus?\n\nThis ends today's trip and it can't be undone.");
        button("Undo boarding", "ck__btn--ghost", "/api/rides/board", "DELETE");
      } else {
        status.textContent = "Trip complete. You got off at " + timeOf(s.alightedAt) + ", and your trip has been saved.";
      }

      wrap.append(title, status, row, msg);
      box.append(wrap);
    }

    async function load() {
      try {
        const res = await call("/api/rides/today");
        if (res.ok) render(await res.json());
      } catch { /* try again on the next refresh */ }
    }

    box.hidden = true;
    load();
    setInterval(() => { if (!document.hidden) load(); }, 30000);
  }

  window.Checkin = { mount };
})();