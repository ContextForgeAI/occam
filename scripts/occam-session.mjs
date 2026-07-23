#!/usr/bin/env node
/**
 * Occam session profile helper — init, list, import cookies.txt, export Playwright storageState
 */
import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import {
  ensureSessionsLayout,
  listSessionProfiles,
  parseNetscapeCookies,
  resolveSessionsRoot,
  suggestId,
  templateImportsPath,
  writeSessionProfile,
} from "./lib/occam-sessions-lib.mjs";

const COOKIE_HEADER_WARN_BYTES = 8192;

function usage() {
  console.log(`Occam session profiles — local header maps for session_profile

Commands:
  init                          Create ~/.occam/sessions + README + _imports/ + states/
  list                          List profile ids and keys (no secret values)
  import --from <file>          Netscape cookies.txt → JSON profile
           --host <domain>      Filter cookies for one site
           --all                Import every non-expired cookie (multi-site dump)
           --id <profile-id>    Output id (default: <host>.imported or browser.export)
           [--user-agent <ua>]  Optional User-Agent header
           [--label <text>]     _occam.label metadata
           [--force]            Overwrite existing profile
           [--no-keep-import]   Do not copy source into _imports/

  export-state --url <url>      Headed Playwright — log in, press Enter, save storageState
           [--id <profile-id>]  Profile + states file name (default: hostname)
           [--state-file <name>] states/<name> (default: <id>.json)
           [--user-agent <ua>]  Browser + profile UA
           [--no-write-profile] Only write states/*.json, skip profile JSON
           [--force]            Overwrite profile
           [--timeout-ms <n>]   Initial navigation timeout (default 60000)

Env: OCCAM_SESSIONS_ROOT overrides default ~/.occam/sessions/
Run from repo root; export-state needs Playwright (occam-doctor).
`);
}

/** @param {string[]} argv */
function parseArgs(argv) {
  const out = { _: [] };
  for (let i = 0; i < argv.length; i += 1) {
    const a = argv[i];
    if (a.startsWith("--")) {
      const key = a.slice(2);
      const next = argv[i + 1];
      if (next && !next.startsWith("--")) {
        out[key] = next;
        i += 1;
      } else {
        out[key] = true;
      }
    } else {
      out._.push(a);
    }
  }
  return out;
}

function cmdInit() {
  const root = resolveSessionsRoot();
  ensureSessionsLayout(root);
  console.log(JSON.stringify({ ok: true, sessionsRoot: root, message: "sessions layout ready" }, null, 2));
}

function cmdList() {
  const root = resolveSessionsRoot();
  const profiles = listSessionProfiles(root);
  console.log(
    JSON.stringify(
      {
        ok: true,
        sessionsRoot: root,
        count: profiles.length,
        profiles: profiles.map((p) => ({
          id: p.id,
          headers: p.headerKeys,
          label: p.meta?.label ?? null,
          hosts: p.meta?.hosts ?? null,
        })),
      },
      null,
      2,
    ),
  );
}

/** @param {ReturnType<typeof parseArgs>} args */
function cmdImport(args) {
  const from = args.from;
  const importAll = Boolean(args.all);
  const host = args.host;
  if (!from) {
    console.error("import requires --from <cookies.txt>");
    process.exit(1);
  }
  if (!importAll && !host) {
    console.error("import requires --host <domain> or --all");
    process.exit(1);
  }
  if (importAll && host) {
    console.error("use either --host or --all, not both");
    process.exit(1);
  }

  const sourcePath = resolve(from);
  if (!existsSync(sourcePath)) {
    console.error(`file not found: ${sourcePath}`);
    process.exit(1);
  }

  const root = resolveSessionsRoot();
  ensureSessionsLayout(root);

  const keepImport = args["keep-import"] !== false && args["no-keep-import"] !== true;
  if (keepImport) {
    const dest = templateImportsPath(root, sourcePath);
    mkdirSync(resolve(dest, ".."), { recursive: true });
    copyFileSync(sourcePath, dest);
  }

  const parsed = parseNetscapeCookies(sourcePath, importAll ? null : host);
  if (parsed.count === 0) {
    const scope = importAll ? "file" : `host "${host}"`;
    console.error(`no cookies matched ${scope} (expired skipped: ${parsed.skippedExpired})`);
    process.exit(1);
  }

  const id = args.id ?? (importAll ? "browser.export" : suggestId(host, "imported"));
  /** @type {Record<string, string>} */
  const headers = { Cookie: parsed.cookie };
  if (args["user-agent"]) {
    headers["User-Agent"] = String(args["user-agent"]);
  }

  const warnings = [];
  if (parsed.cookieBytes > COOKIE_HEADER_WARN_BYTES) {
    warnings.push(
      `Cookie header is ${parsed.cookieBytes} bytes — may exceed server limits; prefer export-state for CF sites`,
    );
  }
  if (importAll) {
    warnings.push(
      "Multi-site Cookie on every HTTP request — browsers filter by domain; Occam workers do not. Prefer export-state + browser backend.",
    );
  }
  if (parsed.cookie.includes("cf_clearance=")) {
    warnings.push("cf_clearance present — may still get http_403 on HTTP worker; use export-state + browser");
  }

  const outPath = writeSessionProfile({
    sessionsRoot: root,
    id,
    headers,
    meta: {
      label: args.label ?? (importAll ? `All cookies from ${basenameOnly(sourcePath)}` : `Import from ${sourcePath}`),
      hosts: importAll ? parsed.hosts : [String(host).replace(/^\./, "")],
      updated: new Date().toISOString().slice(0, 10),
      source: keepImport ? `_imports/${basenameOnly(sourcePath)}` : basenameOnly(sourcePath),
      cookieCount: parsed.count,
      importMode: importAll ? "all" : "host",
      notes: "See docs/19-occam-sessions.md",
    },
    force: Boolean(args.force),
  });

  console.log(
    JSON.stringify(
      {
        ok: true,
        id,
        path: outPath,
        importMode: importAll ? "all" : "host",
        cookiesImported: parsed.count,
        hosts: parsed.hosts,
        skippedExpired: parsed.skippedExpired,
        cookieBytes: parsed.cookieBytes,
        hasCfClearance: parsed.cookie.includes("cf_clearance="),
        session_profile: id,
        warnings: warnings.length > 0 ? warnings : undefined,
      },
      null,
      2,
    ),
  );
}

/** @param {ReturnType<typeof parseArgs>} args */
async function cmdExportState(args) {
  const { exportPlaywrightStorageState } = await import("./lib/occam-session-export-state.mjs");
  const result = await exportPlaywrightStorageState({
    url: args.url,
    id: args.id,
    stateFile: args["state-file"],
    writeProfile: args["no-write-profile"] !== true,
    userAgent: args["user-agent"],
    force: Boolean(args.force),
    timeoutMs: args["timeout-ms"] ? Number(args["timeout-ms"]) : undefined,
  });
  console.log(JSON.stringify(result, null, 2));
}

function basenameOnly(p) {
  return p.split(/[/\\]/).pop() ?? p;
}

const args = parseArgs(process.argv.slice(2));
const cmd = args._[0];

if (!cmd || cmd === "help" || args.help) {
  usage();
  process.exit(cmd ? 0 : 1);
}

try {
  if (cmd === "init") {
    cmdInit();
  } else if (cmd === "list") {
    cmdList();
  } else if (cmd === "import") {
    cmdImport(args);
  } else if (cmd === "export-state") {
    await cmdExportState(args);
  } else {
    usage();
    process.exit(1);
  }
} catch (err) {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
}
