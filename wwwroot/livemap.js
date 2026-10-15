// Shared live map: road-following route lines, numbered stops and moving buses.
// Used by track.html (riders), driver.html (drivers) and admin-dashboard.html (admins).
//
// Road lines: the route is drawn along real roads using a routing service.
//   APP_CONFIG.ROAD_PROVIDER = "osrm"   (default) free OpenStreetMap routing, no setup
//   APP_CONFIG.ROAD_PROVIDER = "google" Google Routes API (needs "Routes API" enabled and billing; falls back to OSRM)
//   APP_CONFIG.OSRM_URL                 optional: your own OSRM server instead of the public demo server
// If routing is unavailable, the line simply joins the stops.
(function () {
  const cfg = () => window.APP_CONFIG || {};
  const COLORS = ["#38bdf8", "#a78bfa", "#34d399", "#fbbf24", "#f472b6", "#fb923c"];
  let mapsPromise = null;
  const memory = new Map();

  // marker styles, added once so pages do not need their own
  (function addStyle() {
    if (document.getElementById("livemap-style")) return;
    const s = document.createElement("style");
    s.id = "livemap-style";
    s.textContent =
      ".stop-dot{width:24px;height:24px;border-radius:50%;background:#475569;color:#fff;border:2px solid #fff;display:grid;place-items:center;font:700 12px sans-serif;box-shadow:0 2px 6px rgba(0,0,0,.4)}" +
      ".bus-dot{width:42px;height:42px;border-radius:50%;background:#fbbf24;border:3px solid #0a1628;display:grid;place-items:center;font-size:22px;box-shadow:0 0 0 6px rgba(251,191,36,.3),0 4px 10px rgba(0,0,0,.5)}";
    document.head.appendChild(s);
  })();

  function loadGoogleMaps() {
    if (window.google && window.google.maps && window.google.maps.marker) return Promise.resolve();
    if (mapsPromise) return mapsPromise;
    mapsPromise = new Promise((resolve, reject) => {
      const key = cfg().GOOGLE_MAPS_API_KEY;
      if (!key || String(key).startsWith("PASTE")) { reject(new Error("Add your Google Maps API key to config.js")); return; }
      window.__liveMapReady = resolve;
      const s = document.createElement("script");
      s.src = "https://maps.googleapis.com/maps/api/js?key=" + encodeURIComponent(key) +
              "&callback=__liveMapReady&loading=async&libraries=marker&v=weekly";
      s.async = true;
      s.onerror = () => { mapsPromise = null; reject(new Error("Google Maps failed to load")); };
      document.head.appendChild(s);
    });
    return mapsPromise;
  }

  const validStop = (s) =>
    Number.isFinite(s.latitude) && Number.isFinite(s.longitude) && !(s.latitude === 0 && s.longitude === 0);
  const toLL = (s) => ({ lat: s.latitude, lng: s.longitude });
  const sortedStops = (route) =>
    (route.stops || []).filter(validStop).sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

  // ---- encoded polyline (the compact text format routing services return) ----
  function decodePolyline(str) {
    let index = 0, lat = 0, lng = 0;
    const out = [];
    while (index < str.length) {
      let b, shift = 0, result = 0;
      do { b = str.charCodeAt(index++) - 63; result |= (b & 0x1f) << shift; shift += 5; } while (b >= 0x20);
      lat += (result & 1) ? ~(result >> 1) : (result >> 1);
      shift = 0; result = 0;
      do { b = str.charCodeAt(index++) - 63; result |= (b & 0x1f) << shift; shift += 5; } while (b >= 0x20);
      lng += (result & 1) ? ~(result >> 1) : (result >> 1);
      out.push({ lat: lat / 1e5, lng: lng / 1e5 });
    }
    return out;
  }

  async function fetchOsrm(stops) {
    const base = (cfg().OSRM_URL || "https://router.project-osrm.org").replace(/\/$/, "");
    const coords = stops.map((s) => s.longitude + "," + s.latitude).join(";");
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), 8000);
    try {
      const res = await fetch(base + "/route/v1/driving/" + coords + "?overview=full&geometries=polyline", { signal: ctrl.signal });
      if (!res.ok) throw new Error("routing service answered " + res.status);
      const data = await res.json();
      if (data.code !== "Ok" || !data.routes || !data.routes[0] || !data.routes[0].geometry) throw new Error("no route found");
      return data.routes[0].geometry;
    } finally { clearTimeout(timer); }
  }

  async function fetchGoogle(stops) {
    await loadGoogleMaps();
    const { Route } = await google.maps.importLibrary("routes");
    const request = {
      origin: toLL(stops[0]),
      destination: toLL(stops[stops.length - 1]),
      intermediates: stops.slice(1, -1).map(toLL),
      travelMode: "DRIVING",
      fields: ["path"]
    };
    const { routes } = await Route.computeRoutes(request);
    if (!routes || !routes[0] || !routes[0].path) throw new Error("no path from Google");
    return routes[0].path.map((p) => ({ lat: p.lat, lng: p.lng }));
  }

  // Path along the roads through the stops. Cached, so each route is only requested once per browser.
  async function roadPath(stops) {
    if (stops.length < 2) return { path: stops.map(toLL), road: true };

    const key = "roadpath:v1:" + stops.map((s) => s.latitude.toFixed(5) + "," + s.longitude.toFixed(5)).join(";");
    if (memory.has(key)) return memory.get(key);

    try {
      const saved = localStorage.getItem(key);
      if (saved) { const r = { path: decodePolyline(saved), road: true }; memory.set(key, r); return r; }
    } catch { /* storage unavailable */ }

    let result = null;

    if ((cfg().ROAD_PROVIDER || "osrm").toLowerCase() === "google") {
      try { result = { path: await fetchGoogle(stops), road: true }; }
      catch (e) { console.warn("Google routing failed, trying OSRM", e); }
    }

    if (!result) {
      try {
        const encoded = await fetchOsrm(stops);
        result = { path: decodePolyline(encoded), road: true };
        try { localStorage.setItem(key, encoded); } catch { /* full or blocked */ }
      } catch (e) {
        console.warn("Road routing unavailable, drawing straight lines", e);
      }
    }

    if (!result) return { path: stops.map(toLL), road: false };   // not cached, so it is tried again next time
    memory.set(key, result);
    return result;
  }

  // ---- the map ----
  async function create(elementId) {
    await loadGoogleMaps();
    const { AdvancedMarkerElement } = google.maps.marker;
    const el = document.getElementById(elementId);
    if (!el) throw new Error("Map container not found: " + elementId);

    const map = new google.maps.Map(el, {
      center: cfg().DEFAULT_CENTER || { lat: 5.6037, lng: -0.187 },
      zoom: 13,
      mapId: cfg().MAP_ID,
      streetViewControl: false,
      mapTypeControl: false
    });

    let layers = [];
    let drawToken = 0;
    const buses = new Map();

    function clearRoutes() {
      layers.forEach((l) => { if (typeof l.setMap === "function") l.setMap(null); else l.map = null; });
      layers = [];
    }

    // Draw one or many routes. opts.highlight = { boardingStopId, dropOffStopId } colours the rider's own stops.
    async function showRoutes(list, opts) {
      opts = opts || {};
      const token = ++drawToken;
      clearRoutes();

      const bounds = new google.maps.LatLngBounds();
      const prepared = list.map((route, i) => ({ stops: sortedStops(route), color: COLORS[i % COLORS.length] }));

      prepared.forEach((p) => {
        p.stops.forEach((s, j) => {
          const kind = opts.highlight && s.id === opts.highlight.boardingStopId ? "board"
                     : opts.highlight && s.id === opts.highlight.dropOffStopId ? "drop" : "";
          const dot = document.createElement("div");
          dot.className = "stop-dot";
          dot.textContent = String(j + 1);
          dot.style.background = kind === "board" ? "#16a34a" : kind === "drop" ? "#dc2626" : p.color;
          layers.push(new AdvancedMarkerElement({ map, position: toLL(s), title: s.name, content: dot }));
          bounds.extend(toLL(s));
        });
      });

      if (opts.fit !== false && !bounds.isEmpty()) map.fitBounds(bounds, 60);

      const results = await Promise.all(prepared.map((p) => roadPath(p.stops)));
      if (token !== drawToken) return { road: true };       // the user picked another route meanwhile

      let allRoad = true;
      results.forEach((r, i) => {
        if (!r.path.length) return;
        if (!r.road) allRoad = false;
        layers.push(new google.maps.Polyline({
          map, path: r.path, strokeColor: prepared[i].color, strokeOpacity: 0.9, strokeWeight: 5
        }));
      });
      return { road: allRoad };
    }

    function animate(marker, target) {
      const from = { ...marker._pos };
      const start = performance.now();
      const duration = 1500;
      function step(now) {
        const t = Math.min((now - start) / duration, 1);
        marker._pos = { lat: from.lat + (target.lat - from.lat) * t, lng: from.lng + (target.lng - from.lng) * t };
        marker.position = marker._pos;
        if (t < 1) requestAnimationFrame(step);
      }
      requestAnimationFrame(step);
    }

    // Create the bus marker, or slide it to its new position
    function setBus(key, pos, o) {
      o = o || {};
      let m = buses.get(key);
      if (!m) {
        const content = document.createElement("div");
        content.className = "bus-dot";
        content.textContent = "\u{1F68C}";
        m = new AdvancedMarkerElement({ map, position: pos, content, title: o.title || "Bus", zIndex: 999 });
        m._pos = pos;
        buses.set(key, m);
      } else {
        animate(m, pos);
      }
      m.content.style.opacity = o.stale ? "0.45" : "1";
      if (o.follow) map.panTo(pos);
    }

    function removeBus(key) {
      const m = buses.get(key);
      if (m) { m.map = null; buses.delete(key); }
    }

    function clearBuses() { Array.from(buses.keys()).forEach(removeBus); }
    function busKeys() { return Array.from(buses.keys()); }

    return { map, showRoutes, setBus, removeBus, clearBuses, busKeys };
  }

  window.LiveMap = { create, load: loadGoogleMaps, roadPath };
})();