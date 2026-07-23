// Build a content-focused corpus from Tranco, filtering out infra/CDN/API/DNS domains.
// Source stays Tranco (popularity-ranked); we just drop non-content endpoints that have no page to extract.
//   node scripts/bench/build-corpus.mjs --listid=XNW4N --want=1000 --pull=6000
//   # second, non-overlapping batch (skip ranks already used by the 1k run, write elsewhere):
//   node scripts/bench/build-corpus.mjs --want=2000 --skip=1313 --out=corpora/bench-2k.jsonl
import fs from "node:fs";
import { join } from "node:path";

const root = process.cwd();
const args = Object.fromEntries(process.argv.slice(2).map((a) => { const m = a.match(/^--([^=]+)=(.*)$/); return m ? [m[1], m[2]] : [a.replace(/^--/, ""), true]; }));
const WANT = parseInt(args.want ?? "1000", 10);
const PULL = parseInt(args.pull ?? "6000", 10);
const SKIP = parseInt(args.skip ?? "0", 10);            // skip Tranco ranks <= SKIP (non-overlapping batches)
const OUT = args.out || "corpora/bench-1k.jsonl";

// Infra / CDN / API / DNS / tracking endpoints — no user-facing article to extract.
const INFRA = [
  "servers.net", "akamai", "akamaized", "edgekey", "edgesuite", "cloudfront", "amazonaws", "azureedge",
  "windows.net", "googleapis", "gstatic", "googlevideo", "googleusercontent", "googletagmanager",
  "googlesyndication", "google-analytics", "googleadservices", "gvt1.com", "gvt2.com", "gvt3.com",
  "ggpht", "ytimg", "fbcdn", "cdninstagram", "fastly", "llnwd", "doubleclick", "app-measurement",
  "crashlytics", "demdex", "adnxs", "licdn", "twimg", "pinimg", "mzstatic", "aaplimg", "apple-dns",
  "recaptcha", "cloudflareinsights", "cloudflare-dns", "onetrust", "branch.io", "segment.io",
  "adservice", "2mdn", "scorecardresearch", "omtrdc", "rlcdn", "casalemedia", "rubiconproject",
  "trafficmanager", "azure-dns", "awsdns", "root-servers", "nsone", "ntp.org", "in-addr",
  // --- Expanded from the bench-1k-2026-06-26 run: 211 dns_error rows were ~all infra. ---
  // Microsoft / Azure / Edge
  "msedge", "azurefd", "azurewebsites", "azure-devices", "usgovcloudapi", "msftncsi", "msftconnecttest",
  "msftauth", "msidentity", "microsoftonline", "office.net", "windowsupdate", "static.microsoft",
  "sfx.ms", "svc.ms", "live.net",
  // Akamai / DNS providers / telemetry endpoints
  "akadns", "akam.net", "akahost", "name-services", "orderbox-dns", "squarespacedns", "herokudns",
  "vercel-dns", "ui-dns", "nstld", "afilias-nst", "gandi-ns", "nominetdns", "share-dns", "mynetname",
  "impervadns", "footprintdns", "namebrightdns", "dns-parking", "dnsowl", "jomodns", "alibabadns",
  "zdnscloud", "yahoodns", "nelreports", "nel.goog", "nr-data", "go-mpulse", "yellowblue", "cloudsink",
  "googlezip", "bunnyinfra", "hichina", "3gppnetwork", "resolver.arpa", "ci-servers", "mozilla.net",
  // CDN families (incl. ByteDance/TikTok, gaming, vendor CDNs)
  "cdn77", "spotifycdn", "steamstatic", "scdn.co", "githubusercontent", "byteglb", "bytefcdn",
  "byteoversea", "bytedns", "douyincdn", "tiktokcdn", "tiktokv", "tiktokw", "tiktokpangle", "pangle",
  "ttdns", "ttvnw", "ibyteimg", "yximgs", "i18n-pglstatp", "bdydns", "volcfcdndvs", "volcgslb",
  "qlivecdn", "ksyuncdn", "cdnhwc", "cdnbuild", "cdn20", "yccdn", "avcdn", "ovscdns", "enacdn", "edgcdn",
  "wswebcdn", "vedcdnlb", "vecdnlb", "vedsalb", "wsdvs", "kwcdn", "rtbcdn", "cmediahub", "shopifysvc",
  // Ad-tech / tracking / RTB
  "criteo", "bidswitch", "smaato", "adgrx", "liadm", "nmrodam", "online-metrix", "imrworldwide",
  "contentsquare", "adobedtm", "adobedc", "supertms", "dotaplabs", "fbpigeon", "mtgglobals", "bidr.io",
  "1rx.io", "4dex", "agkn", "turn.com", "privacy-mgmt", "vk-analytics", "googletagservices",
  "facebook.net", "afafb",
  // AWS / cloud vendor infra
  "awswaf", "awsglobalaccelerator", "aws.dev", "amazon.dev", "on.aws", "ssl-images-amazon",
  "media-amazon", "a2z", "aiv-delivery", "bamgrid", "playfabapi", "goskope", "myqcloud", "tencent-cloud",
  "dbankcloud", "myhuaweicloud", "hicloud", "whecloud", "samsungcloudsolution", "samsungqbe",
  "samsungosp", "samsungacr", "heytapmobile", "heytapdl", "allawnos", "yandexcloud", "kwai", "kwaipros",
  // Device / ISP / IoT / streaming infra
  "nextlgsdp", "lgtvcommon", "vidaahub", "xcal.tv", "live-video", "playstation.net", "nintendo.net",
  "pvp.net", "steamserver", "e2ro", "tplinknbu", "keenetic.io", "immedia-semi", "ezviz7", "hicloudcam",
  "capcutapi", "kslawin", "dnse0", "wbbasket", "geobasket", "gwfb.net", "dyingbirds", "zenecn",
  "sc-gw", "exp-tas", "ioref", "libp2p", "nflxso", "virginm.net", "rzone.de", "safebrowsing.apple",
  "adguard-vpn", "grammarly.io", "easebar",
];
// Family heuristics: any *cdn*/*dns*/api./ns<N>/*-msedge/azure/aliyun/huawei/metrics host is infra.
const isInfra = (d) =>
  INFRA.some((p) => d.includes(p)) ||
  /(^|[.-])cdn|cdn([.-]|\d|$)|(^|[.-])api\.|(^|[.-])ns\d|dns|[.-]msedge\.|azure|aliyun|huaweicloud|telemetry|metrix/.test(d);

const csv = fs.readFileSync(join(root, "artifacts", "bench-scratch", `tranco-top${PULL}.csv`), "utf8").trim().split(/\r?\n/);
const out = [];
let dropped = 0;
let skipped = 0;
for (const line of csv) {
  const [rank, domain] = line.split(",");
  if (+rank <= SKIP) { skipped++; continue; }          // reserve the low ranks for the first batch
  if (isInfra(domain)) { dropped++; continue; }
  out.push({ id: `tranco-${rank}`, rank: +rank, url: `https://${domain}`, domain, category: "tranco-content", source: `tranco-${args.listid || "list"}` });
  if (out.length >= WANT) break;
}
fs.writeFileSync(join(root, OUT), out.map((o) => JSON.stringify(o)).join("\n") + "\n");
console.log(`scanned=${csv.length} skipped_rank<=${SKIP}=${skipped} dropped_infra=${dropped} kept=${out.length} (rank ${out[0]?.rank}..${out[out.length - 1]?.rank}) -> ${OUT}`);
