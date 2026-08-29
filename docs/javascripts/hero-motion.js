(() => {
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");

  function initializeSignalMotion() {
    const figure = document.querySelector('[data-oc-motion="signal"]');
    const image = figure?.querySelector("img");

    if (!figure || !image || figure.dataset.ocMotionReady === "true") {
      return;
    }

    const reset = () => {
      figure.style.removeProperty("--oc-signal-tilt-x");
      figure.style.removeProperty("--oc-signal-tilt-y");
      figure.style.removeProperty("--oc-signal-lift");
    };

    const update = (event) => {
      if (reducedMotion.matches || !finePointer.matches) {
        reset();
        return;
      }

      const bounds = image.getBoundingClientRect();
      if (bounds.width === 0 || bounds.height === 0) {
        return;
      }

      const normalizedX = Math.min(1, Math.max(0, (event.clientX - bounds.left) / bounds.width)) - 0.5;
      const normalizedY = Math.min(1, Math.max(0, (event.clientY - bounds.top) / bounds.height)) - 0.5;

      figure.style.setProperty("--oc-signal-tilt-x", `${(-normalizedY * 1.4).toFixed(2)}deg`);
      figure.style.setProperty("--oc-signal-tilt-y", `${(normalizedX * 1.7).toFixed(2)}deg`);
      figure.style.setProperty("--oc-signal-lift", `${(-normalizedY * 1.5).toFixed(2)}px`);
    };

    figure.dataset.ocMotionReady = "true";
    image.addEventListener("pointermove", update, { passive: true });
    image.addEventListener("pointerleave", reset, { passive: true });
    reducedMotion.addEventListener?.("change", reset);
    finePointer.addEventListener?.("change", reset);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeSignalMotion, { once: true });
  } else {
    initializeSignalMotion();
  }
})();
