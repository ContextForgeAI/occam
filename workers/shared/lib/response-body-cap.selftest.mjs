import assert from "node:assert/strict";
import {
  readResponseBodyForExtract,
  readResponseBodyWithCap,
  rejectIfContentLengthExceeds,
  resolveHttpOversizeMode,
  resolveMaxResponseBytes,
  ResponseTooLargeError,
  snapHtmlToSafeBoundary,
} from "./response-body-cap.mjs";

const prev = process.env.OCCAM_MAX_RESPONSE_BYTES;
delete process.env.OCCAM_MAX_RESPONSE_BYTES;
// Q-012: default raised 1 MiB -> 8 MiB so common heavy-HTML pages extract instead of
// hard-failing response_too_large before extraction is attempted.
assert.equal(resolveMaxResponseBytes(), 8 * 1024 * 1024);

// Regression: a 2 MiB body must now read fully under the DEFAULT cap (it would have thrown
// at the old 1 MiB default). Uses resolveMaxResponseBytes() so it tracks the real default.
const twoMiB = await readResponseBodyWithCap(makeResponse(2 * 1024 * 1024), resolveMaxResponseBytes());
assert.equal(Buffer.byteLength(twoMiB, "utf8"), 2 * 1024 * 1024);

process.env.OCCAM_MAX_RESPONSE_BYTES = "131072";
assert.equal(resolveMaxResponseBytes(), 131072);

process.env.OCCAM_MAX_RESPONSE_BYTES = "4096";
assert.equal(resolveMaxResponseBytes(), MIN_CLAMP());

process.env.OCCAM_MAX_RESPONSE_BYTES = "99999999";
assert.equal(resolveMaxResponseBytes(), 16 * 1024 * 1024);

assert.throws(() => {
  process.env.OCCAM_MAX_RESPONSE_BYTES = "0";
  resolveMaxResponseBytes();
}, /invalid OCCAM_MAX_RESPONSE_BYTES/);

function MIN_CLAMP() {
  return 64 * 1024;
}

function makeResponse(byteLength) {
  const payload = Buffer.alloc(byteLength, 0x61);
  const stream = new ReadableStream({
    start(controller) {
      controller.enqueue(payload);
      controller.close();
    },
  });

  return new Response(stream, {
    status: 200,
    headers: { "content-type": "text/html; charset=utf-8" },
  });
}

const small = await readResponseBodyWithCap(makeResponse(50), 100);
assert.equal(small.length, 50);

await assert.rejects(
  () => readResponseBodyWithCap(makeResponse(200), 100),
  (error) => {
    assert.ok(error instanceof ResponseTooLargeError);
    assert.equal(error.code, "response_too_large");
    assert.equal(error.bytesRead, 200);
    assert.equal(error.maxBytes, 100);
    return true;
  },
);

const headerOnly = new Response(null, {
  status: 200,
  headers: { "content-length": "5000" },
});
assert.throws(
  () => rejectIfContentLengthExceeds(headerOnly, 100),
  (error) => {
    assert.ok(error instanceof ResponseTooLargeError);
    assert.equal(error.bytesRead, 5000);
    return true;
  },
);

process.env.OCCAM_HTTP_OVERSIZE_MODE = "partial";
const partial = await readResponseBodyForExtract(makeResponse(200), 100);
assert.equal(partial.truncated, true);
assert.ok(partial.html.length > 0);
assert.equal(resolveHttpOversizeMode(), "partial");

const snapped = snapHtmlToSafeBoundary(Buffer.from("<div>hello</div><div>tail"), 20);
assert.ok(snapped.endsWith("</div>"));

delete process.env.OCCAM_HTTP_OVERSIZE_MODE;

if (prev === undefined) {
  delete process.env.OCCAM_MAX_RESPONSE_BYTES;
} else {
  process.env.OCCAM_MAX_RESPONSE_BYTES = prev;
}

console.log("response-body-cap.selftest: OK");
