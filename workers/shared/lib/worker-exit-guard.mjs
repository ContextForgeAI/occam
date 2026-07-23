/**
 * Last-resort guard: a one-shot worker must NEVER exit without emitting a JSON line.
 *
 * The one-shot workers are `const result = await run…(); console.log(JSON.stringify(result))` — a
 * top-level await. If that promise never settles and nothing ref'd is left on the event loop, node
 * exits **13** ("Unfinished Top-Level Await") having printed nothing at all. The host then can only
 * report `workers_unavailable` + "run doctor", which blames the user's install for what is a worker
 * bug. That happened for real: an undici `Agent.close()` awaiting an unread response body
 * (MDN's 404 on Node 20) — silent exit 13 instead of a plain `http_404`.
 *
 * Node 20 does not warn about an unsettled top-level await (Node 24 does), so this class is invisible
 * on a newer dev runtime and only bites on the older supported floor. The guard closes it at the
 * worker, independent of node version: on exit without a verdict we emit a typed `timeout` failure —
 * the host's closest honest code for "the worker produced no result" — plus a stderr line carrying the
 * real diagnosis.
 *
 * Usage:
 *   const guard = installSilentExitGuard("node_readability_turndown");
 *   const result = await run(...);
 *   guard.emit(result);   // prints the JSON and disarms the guard
 */

/**
 * @param {string} backend value for the `backend` field of the fallback verdict
 * @returns {{ emit: (result: unknown) => void, disarm: () => void }}
 */
export function installSilentExitGuard(backend) {
  let emitted = false;

  process.on("exit", () => {
    if (emitted) {
      return;
    }

    // Only synchronous writes are allowed in an 'exit' handler.
    process.stderr.write(
      "[occam.worker] stalled: the extract promise never settled — emitting a typed timeout instead of exiting silently.\n",
    );
    process.stdout.write(
      JSON.stringify({
        ok: false,
        backend,
        failure: "timeout",
        message: "worker stalled before producing a result",
        latency_ms: 0,
      }) + "\n",
    );
  });

  return {
    emit(result) {
      emitted = true;
      console.log(JSON.stringify(result));
    },
    disarm() {
      emitted = true;
    },
  };
}
