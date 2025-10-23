// Shared helpers for utility pages (no inline scripts in .razor files)
window.statusPages = (function () {

  function initBase() {
    if (window.feather) window.feather.replace();
    if (window.AOS && typeof window.AOS.refresh === "function") window.AOS.refresh();
  }

  function initNotFound() {
    initBase();
  }

  function initError() {
    initBase();
    // subtle nudge: shake the icon once
    const icon = document.querySelector('[data-feather="zap"]')?.closest('div');
    if (!icon) return;
    icon.animate(
      [{ transform: "translateX(0)" }, { transform: "translateX(-2px)" }, { transform: "translateX(2px)" }, { transform: "translateX(0)" }],
      { duration: 250, iterations: 1, easing: "ease-in-out" }
    );
  }

  function initMaintenance(isoEnd) {
    initBase();
    const end = new Date(isoEnd).getTime();
    const ids = { dd: "dd", hh: "hh", mm: "mm", ss: "ss" };
    function tick() {
      const now = Date.now();
      let diff = Math.max(0, end - now);
      const d = Math.floor(diff / (24 * 3600e3)); diff -= d * 24 * 3600e3;
      const h = Math.floor(diff / 3600e3);        diff -= h * 3600e3;
      const m = Math.floor(diff / 60e3);          diff -= m * 60e3;
      const s = Math.floor(diff / 1e3);

      document.getElementById(ids.dd).textContent = String(d).padStart(2, "0");
      document.getElementById(ids.hh).textContent = String(h).padStart(2, "0");
      document.getElementById(ids.mm).textContent = String(m).padStart(2, "0");
      document.getElementById(ids.ss).textContent = String(s).padStart(2, "0");
    }
    tick();
    setInterval(tick, 1000);
  }

  function initOffline() {
    initBase();
  }

  return { initNotFound, initError, initMaintenance, initOffline };
})();
