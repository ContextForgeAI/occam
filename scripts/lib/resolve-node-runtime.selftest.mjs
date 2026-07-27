import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  resolveNodeExecutable,
  stampNodeRuntimeEnv,
  writeInstallNodeBin,
  readInstallNodeBin,
  formatMissingNodeMessage,
  validateNodeExecutable,
} from "./resolve-node-runtime.mjs";

const sep = process.platform === "win32" ? ";" : ":";
const tmp = mkdtempSync(join(tmpdir(), "occam-node-runtime-"));

try {
  // 1) explicit override wins
  {
    const fake = join(tmp, "override-node");
    writeFileSync(fake, "");
    const got = resolveNodeExecutable({
      env: { OCCAM_NODE_BIN: fake },
      occamHome: tmp,
      execPath: process.execPath,
    });
    assert.equal(got, fake);
  }

  // 2) install-recorded path
  {
    const home = join(tmp, "home-a");
    mkdirSync(home);
    writeInstallNodeBin(home, process.execPath);
    assert.equal(readInstallNodeBin(home), process.execPath);
    const got = resolveNodeExecutable({
      env: { PATH: `/usr/bin${sep}/bin` },
      occamHome: home,
      execPath: "",
    });
    assert.equal(got, process.execPath);
  }

  // 3) stamp under starved PATH without pre-set OCCAM_NODE_BIN
  {
    const env = { PATH: `/usr/bin${sep}/bin`, OCCAM_HOME: join(tmp, "home-a") };
    stampNodeRuntimeEnv(env, { execPath: process.execPath, occamHome: env.OCCAM_HOME });
    assert.equal(env.OCCAM_NODE_BIN, process.execPath);
    const nodeDir = dirnameCompat(process.execPath);
    assert.ok(env.PATH.replace(/\\/g, "/").toLowerCase().includes(nodeDir.toLowerCase()));
  }

  // 4) stamp does not override explicit OCCAM_NODE_BIN
  {
    const env = { PATH: `/usr/bin${sep}/bin`, OCCAM_NODE_BIN: "/custom/node" };
    stampNodeRuntimeEnv(env, { execPath: process.execPath });
    assert.equal(env.OCCAM_NODE_BIN, "/custom/node");
  }

  // 5) missing-node message is human
  {
    const msg = formatMissingNodeMessage("/gone/node");
    assert.match(msg, /no longer available/);
    assert.match(msg, /OCCAM_NODE_BIN/);
  }

  // 6) validate current process.execPath
  {
    const v = validateNodeExecutable(process.execPath);
    assert.equal(v.ok, true);
    assert.ok(v.version);
  }

  // 7) validate missing path
  {
    const v = validateNodeExecutable(join(tmp, "missing-node-bin"));
    assert.equal(v.ok, false);
    assert.match(v.error, /no longer available/);
  }

  console.log("resolve-node-runtime.selftest: ok");
} finally {
  rmSync(tmp, { recursive: true, force: true });
}

function dirnameCompat(filePath) {
  const normalized = filePath.replace(/\\/g, "/");
  const idx = normalized.lastIndexOf("/");
  return idx <= 0 ? "" : normalized.slice(0, idx);
}
