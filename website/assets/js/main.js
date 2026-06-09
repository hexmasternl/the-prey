/* The Prey — interaction layer: scroll reveals + count-up.
   Progressive enhancement only; the site is fully usable without JS. */
(function () {
  "use strict";

  /* ---- mobile nav toggle ---- */
  (function () {
    var toggle = document.querySelector(".nav-toggle");
    var menu = document.getElementById("nav-menu");
    if (!toggle || !menu) { return; }

    function setOpen(open) {
      menu.classList.toggle("open", open);
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
    }

    toggle.addEventListener("click", function () {
      setOpen(toggle.getAttribute("aria-expanded") !== "true");
    });

    // Close after tapping a link inside the menu.
    menu.addEventListener("click", function (e) {
      if (e.target.closest("a")) { setOpen(false); }
    });

    // Close on Escape.
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape") { setOpen(false); }
    });

    // Reset state when crossing back to the desktop layout.
    var desktop = window.matchMedia("(min-width: 881px)");
    (desktop.addEventListener ? desktop.addEventListener.bind(desktop, "change") : desktop.addListener.bind(desktop))(function (e) {
      if (e.matches) { setOpen(false); }
    });
  })();

  var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function parseCount(raw) {
    var m = String(raw).match(/(\D*)(\d+)(\D*)/);
    return m ? { pre: m[1], target: +m[2], post: m[3] } : null;
  }

  function setFinal(el) {
    var c = parseCount(el.dataset.count);
    if (c) { el.textContent = c.pre + c.target + c.post; }
  }

  function animateCount(el) {
    if (el.dataset.counted) { return; }
    el.dataset.counted = "1";
    var c = parseCount(el.dataset.count);
    if (!c) { return; }
    var dur = 1100, start = null;
    function step(ts) {
      if (start === null) { start = ts; }
      var t = Math.min(1, (ts - start) / dur);
      var eased = 1 - Math.pow(1 - t, 3);
      el.textContent = c.pre + Math.round(eased * c.target) + c.post;
      if (t < 1) { requestAnimationFrame(step); }
    }
    requestAnimationFrame(step);
  }

  var targets = document.querySelectorAll(".reveal, .reveal-stagger");
  var counters = document.querySelectorAll("[data-count]");

  if (reduce || !("IntersectionObserver" in window)) {
    targets.forEach(function (el) { el.classList.add("in"); });
    counters.forEach(setFinal);
    return;
  }

  var io = new IntersectionObserver(function (entries) {
    entries.forEach(function (entry) {
      if (!entry.isIntersecting) { return; }
      var el = entry.target;
      el.classList.add("in");
      if (el.matches("[data-count]")) { animateCount(el); }
      el.querySelectorAll("[data-count]").forEach(animateCount);
      io.unobserve(el);
    });
  }, { threshold: 0.18, rootMargin: "0px 0px -8% 0px" });

  targets.forEach(function (el) { io.observe(el); });
  counters.forEach(function (el) { if (!el.closest(".reveal, .reveal-stagger")) { io.observe(el); } });
})();
