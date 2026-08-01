const form = document.querySelector("#demo-form");
const input = document.querySelector("#url");
const button = form.querySelector("button");
const result = document.querySelector("#result");

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  button.disabled = true;
  result.setAttribute("aria-busy", "true");
  result.replaceChildren(Object.assign(document.createElement("p"), {
    className: "state",
    textContent: "Reading the page…",
  }));
  try {
    const response = await fetch("/v1/transcode", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ url: input.value }),
    });
    const payload = await response.json();
    if (!payload.ok) {
      const message = document.createElement("p");
      message.className = "failure";
      message.textContent = `${payload.failure?.code ?? "failed"}: ${payload.failure?.message ?? "The page could not be read."}`;
      result.replaceChildren(message);
      return;
    }
    const state = document.createElement("p");
    state.className = "state";
    state.textContent = payload.truncated ? "Readable result · demo output capped" : "Readable result";
    const source = document.createElement("p");
    source.textContent = `Source: ${payload.url?.finalUrl ?? payload.url?.url ?? input.value}`;
    const output = document.createElement("pre");
    output.textContent = payload.markdown;
    result.replaceChildren(state, source, output);
  } catch {
    const message = document.createElement("p");
    message.className = "failure";
    message.textContent = "The local demo is unavailable.";
    result.replaceChildren(message);
  } finally {
    button.disabled = false;
    result.setAttribute("aria-busy", "false");
  }
});
