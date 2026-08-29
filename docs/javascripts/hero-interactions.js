(() => {
  function initializeHeroInteractions() {
    const button = document.querySelector("[data-oc-copy-command]");
    const command = document.querySelector("[data-oc-install-command]");

    if (!button || !command || button.dataset.ocCopyReady === "true") {
      return;
    }

    let resetTimer;
    const reset = () => {
      button.textContent = "Copy";
      button.removeAttribute("data-copy-state");
    };

    const report = (label, state) => {
      window.clearTimeout(resetTimer);
      button.textContent = label;
      button.dataset.copyState = state;
      resetTimer = window.setTimeout(reset, 1800);
    };

    button.dataset.ocCopyReady = "true";
    button.addEventListener("click", async () => {
      const value = command.textContent.trim();
      try {
        if (!navigator.clipboard?.writeText) {
          throw new Error("Clipboard API unavailable");
        }
        await navigator.clipboard.writeText(value);
        report("Copied", "copied");
      } catch {
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(command);
        selection?.removeAllRanges();
        selection?.addRange(range);
        report("Selected", "selected");
      }
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeHeroInteractions, { once: true });
  } else {
    initializeHeroInteractions();
  }
})();
