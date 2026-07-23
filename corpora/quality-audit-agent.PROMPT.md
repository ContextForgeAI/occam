# FF-Occam MCP — quality audit (agent role, not smoke test)



**Product surface:** **v0.8.10-media-refs** (eight tools + `mediaRefs[]` on transcode/digest + PB2 community + PB3 heal/save + PB4a genome/auto-policy + PB4b extract + PB4c publish CLI + digest parallelism + HTTP daemon) · **Repo:** `c:\PROJECTS\FFOccamMCP`  

**Audit baseline:** **2026-06-17** (post-L0 consolidation; L0 core CLOSED) · **2026-06-16 tier-3 r2** ([`quality-audit-reports/2026-06-16-tier3-reaudit-v0.8.4-r2.md`](quality-audit-reports/2026-06-16-tier3-reaudit-v0.8.4-r2.md) — confirmatory Merge PASS) · prior reports  

**MCP server:** `project-0-FFOccamMCP-ff-occam`  

**Tools (8):** `occam_probe`, `occam_transcode`, `occam_digest`, `occam_playbook_resolve`, `occam_map`, **`occam_playbook_heal`**, **`occam_playbook_save`**, **`occam_extract_knowledge`** (always live extract, no file cache; **no** `occam_playbook_publish`)



**Related:** gate smoke = `corpora/l0-smoke.jsonl` · visual golden = `corpora/visual-matrix.jsonl` · digest gate = `corpora/l2-digest.jsonl` · map gate = `corpora/l2-map.jsonl` · session gate = `corpora/l2-session.jsonl` · transport gate = `corpora/l2-transport.jsonl` · heal gate = `corpora/l3-heal-learn.jsonl` · genome gate = `corpora/l4-genome.jsonl` · **rotation URLs** = `corpora/quality-audit-rotation.jsonl` · **audit reports** = `corpora/quality-audit-reports/`



**Prerequisites (before any audit):** `occam-doctor` + **Reload MCP** (`ff-occam`). Workspace **FFOccamMCP** open in Cursor — иначе CallMcpTool недоступен.



---



## Твоя роль



Ты **исследовательский агент**, который реально работает с MCP-инструментами для чтения документации в интернете.



**Запрещено:**



- считать `ok: true` достаточным доказательством качества;

- проверять только наличие полей JSON;

- отвечать из training data вместо tool output;

- галлюцинировать содержимое страниц;

- подменять MCP вызовы gate / worker CLI / `dotnet run` / `desk-recipe-*.mjs` / `run-wide-cursor-desk.ps1`;

- ожидать definitions с MDN **index** при `focus_query` — для этого B2 **leaf** URL.



**Эталон (ground truth):** сырой HTML каждой страницы.  

Сначала HTML (WebFetch/curl), потом MCP. Сравни **смысл и полноту**, не строковое совпадение.



**Workspace:** папка **FFOccamMCP** открыта в Cursor — иначе MCP tools недоступны.



---



## Двухслойная матрица URL (регрессия vs обобщение)



| Слой | Назначение | Менять URL? | Регрессия |

|------|------------|-------------|-----------|

| **FROZEN (golden)** | Фиксированные якоря из § Матрица A/B/C | **Нет** без bump baseline в этом файле | Падение ≥2 баллов на Fidelity/Honesty = **fail** |

| **ROTATION** | Альтернативные URL того же tier/класса | **Да** — из `quality-audit-rotation.jsonl` | Не blocker; ловит **overfitting** под MDN/nginx/Nuxt |



**Правило прогона:** всегда **сначала FROZEN полностью**, затем **ROTATION** (минимум 3 rotation-case из jsonl).  

Если rotation провалился, а frozen OK → **сайт-специфичный** дефект или новый corpus gap, не обязательно регрессия golden.



---



## Известные границы продукта (v0.7.1)



| Ожидание | Реальность |

|----------|------------|

| `focus_query` на TOC/index | Навигация, не definitions с leaf |

| Digest `combined` | Не удаляет URL; смотри `items[].focusMatched` + `agentHints` |

| `focusMatched` | Strict multi-term (≥2 термина или полная фраза) |

| `per_url_max_tokens` ≤ 256 | Stress only; research ≥ 512 |

| nginx `/en/docs/` index | Module spam **должен** быть pruned (post ea230e0) |

| B1 hub + definition Q | Workflow fail — не баг |

| Docker `get-started` digest | ~~`focusMatched:false` + JS shell~~ — M1 closed; verify fm + no Marlin toolbar before cite |

| openai concepts Structure | Struct **≥4** — `openAiDocHeadings` seed (Path A ✅ 2026-06-15) |



---



## Верифицируемые изменения продукта (delta checklist)



При каждом re-run заполни § **Delta analysis** — что из списка **подтверждено live MCP**:



| Milestone | Что проверить в audit | Ожидание после ship |

|-----------|----------------------|------------------------|

| ea230e0 | nginx full transcode | Noise ≥4, нет `ngx_*` spam на index |

| ea230e0 | openai probe | Probe honesty ≥4, нет false `rate_limit` |

| ea230e0 | digest API | `items[].focusMatched` присутствует |

| ea230e0 | B1 hub TOC | bullet «Function scopes and closures» @512+focus |

| post-QA | digest strict match | MDN `focusMatched:false` при «configuration syntax» |

| post-QA | digest agentHints | `agentHints.warnings` при weak match; read before `combined` |

| post-QA | B2 closure @512 | ✅ Path A: Fid **≥4** — canonical phrase @512 via `PreserveDefinitionalAnchor` (C#) |
| post-QA | nuxt HTTP | Noise **3** baseline → **F3 target ≥4** via `nuxt.com.seed` `nuxtFooterPromos` (не generic) |
| post-QA | openai structure | ✅ Path A: Struct **≥4** — `## Embeddings`/`## Tokens` via `openAiDocHeadings` seed |
| PB0 | nginx index TOC | generic prune **не** режет doc TOC; module spam via seed |
| PB0 | worker architecture | per-host mjs → `playbook-seed.mjs` + seeds; default = full markdown |
| PB0 | digest-two-docs | nginx `focusMatched:true` + `min_succeeded:2` после TOC fix |
| 558864f | nuxt footer/shiki | `nuxtFooterPromos` + `nuxtShikiCssStrip` in seed only; generic host-agnostic |
| PB1.1 | playbook resolve | `occam_playbook_resolve` → seed/community path + agent_notes (8 tools incl. heal/save/extract) |
| P1-3 map | occam_map sitemap | `ok:true`, `linkCount` ≥ 10 nginx; `focus_query` match in **any** `links[]` path, not only `#1` |
| Path A | openai-concepts Struct | `## Embeddings`, `## Tokens` visible; Struct **≥4** vs HTML |
| Path A | B2 closure @512 | «A closure is any piece of source code…» survives @512; Fid **≥4** |



---



## Методология (на каждый URL)



1. **HTML baseline** — 5–10 якорных фактов для **заданного вопроса**.

2. **occam_probe** — pageClass, backend, risk flags.

3. **Probe vs transcode** — ложный challenge при успешном transcode = дефект.

4. **occam_transcode** — markdown vs HTML.

5. **occam_digest** (C) — `focusMatched`, `agentHints`, `combined` honesty.



---



## Rubric (1–5 + цитаты HTML vs MD, ≤2 строки)



Fidelity · Noise · Structure · Encoding · Honesty · Probe use · Probe honest · Token econ · Digest coh



---



## Матрица FROZEN — Layer 1



### A. Recipe A (probe → transcode)



| id | URL | backend | Вопрос | Доп. |

|----|-----|---------|--------|------|

| mdn-js-guide | `https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide` | http | Guide и разделы TOC? | Functions, Closures bullets |

| nginx-doc | `https://nginx.org/en/docs/` | http | Основные разделы? | **Noise:** без `ngx_*` spam |

| nuxt-spa | `https://nuxt.com/docs/getting-started/introduction` | http **и** browser отдельно | Что такое Nuxt? | Sidebar/emoji/Carbon |

| openai-concepts | `https://platform.openai.com/docs/concepts` | http_then_browser | tokenization? | probe vs transcode; `## Embeddings` |

| not-found | `https://developer.mozilla.org/en-US/docs/OccamGateMissingPage404` | http | 404 без body | `http_404`, stop |



### B. Token knobs



**B1 hub:** mdn-js-guide · `fit_markdown=true`, `focus_query="functions closures"`, `max_tokens=512`  

- TOC Q: «где closures?» · Definition Q: workflow fail (не регрессия)



**B2 leaf:** `/Guide/Functions` · те же knobs · «что такое closure?»  

- **Baseline 2026-06-15b:** Fid **3**, closure mechanics есть, canonical phrase **отсутствует** @512 — **known gap**, не regression blocker  

- Regression: падение Fid **≥2** (→1) или потеря closure section целиком



### C. Digest



| id | urls | focus / params |

|----|------|----------------|

| digest-two-docs | MDN Guide + nginx `/en/docs/` | `focus_query="configuration syntax"`, http |

| digest-partial | nginx + MDN 404 | partial honesty |

| digest-token-cap | MDN + nginx | `per_url_max_tokens=512` |



**Digest поля:** `items[].focusMatched`, `agentHints`, `stats`, `combined`.



---



## Матрица ROTATION — Layer 2



Источник: [`corpora/quality-audit-rotation.jsonl`](quality-audit-rotation.jsonl)



| rotation id | Заменяет frozen | Зачем |

|-------------|-----------------|-------|

| mdn-array-ref | mdn-js-guide | tier-A docs не только JS Guide |

| nginx-http-core-leaf | nginx-doc | leaf module page, не index |

| nuxt-install | nuxt-spa | другая Nuxt docs page |

| openai-rate-limits | openai-concepts | probe на странице про rate limits |

| mdn-404-alt | not-found | другой 404 URL |

| b2-closures-leaf | b2-functions | альтернативный leaf |

| digest-postgres-docker | digest-two-docs | digest на других доменах |
| map-nginx-sitemap-focus | *(chain)* | map → digest 0f; `focus_query` rank — scan `links[]`, not only first row; **mandatory** on full eight-tool audit |



Прогони **минимум 3** rotation-case из jsonl (после FROZEN). На full eight-tool audit **`map-nginx-sitemap-focus`** обязателен среди rotation picks.



---



## Матрица WILDCARD — Layer 3 (popular / agent-real URLs)



**Стратегически важно** — не менее golden-hosts для product direction. Golden (7 hosts) = merge gates; wildcard = куда агенты ходят в реальности (Reddit, SO, GitHub, news).

Источники:

- [`corpora/quality-audit-wildcard.jsonl`](quality-audit-wildcard.jsonl) — прогон URLs + `priority: critical|high`
- [`corpora/popular-hosts-priority.json`](popular-hosts-priority.json) — статус, permalink vs listing, audit notes

| Правило | Деталь |
|---------|--------|
| **Не regression blocker** | FAIL_HONEST на reddit/SO в env с 403 — OK для Merge PASS |
| **Permalink > listing** | Reddit/SO: тестируй **прямую ссылку на пост/вопрос**, не только subreddit/feed |
| **Reddit audit 2026-06-15** | `old.reddit` **permalink** и `www` listing — оба `http_403` в той среде; www permalink — отдельный row `reddit-www-permalink` |
| **Следующий product slice** | **P2-7** — [`quality-sprint-p2-7-popular-hosts.PROMPT.md`](quality-sprint-p2-7-popular-hosts.PROMPT.md) (seeds reddit/SO/x + probe tune) |
| **Social Run #2** | SO/IG/X browser WORKS; Reddit captcha + bad corpus slug — fix in P2-7 |

Прогони wildcard после ROTATION; в отчёте — § Wildcard findings + явный verdict **WORKS / PARTIAL / FAIL_HONEST** per host.



---



## Порядок прогона



1. **FROZEN A:** mdn-js-guide → nginx-doc → nuxt-spa (HTTP, потом browser) → openai-concepts → not-found  

2. **FROZEN B:** B1 hub → B2 leaf (definitions **только** leaf, не index)  

3. **FROZEN C:** C1 digest-two-docs → C2 digest-partial → C3 digest-token-cap  

4. **ROTATION:** минимум **3** case из `quality-audit-rotation.jsonl` (включая `map-nginx-sitemap-focus` на full audit)  

5. **Regression:** baseline **2026-06-17**; blockers mdn / nginx / 404 / digest-partial — падение **≥2** на Fidelity/Honesty = **FAIL**

6. **Eight-tool surface** (§ ниже) — **8/8** live MCP; Merge FAIL если любой tool не вызван

7. **Recipe R (K9)** — один сквозной chain (§ Часть 3)

8. **Delta analysis** + сводные таблицы



**MCP:** только **CallMcpTool** (`project-0-FFOccamMCP-ff-occam`). **HTML:** WebFetch/curl **до** каждого MCP.



**Начни с:** `mdn-js-guide` и `nginx-doc`.



---



## Часть 2 — Eight-tool surface (обязательно 8/8)



В отчёте § **Tool surface matrix** — таблица: tool × вызван? × URL × ok/failure.code × evidence (1 строка).  

**Merge FAIL** если любой из 8 tools не вызван live MCP. **Нет** `occam_playbook_publish` (девятый tool не существует).



### Шаг 0 — Transport

- Подтверди **8** tool descriptors в MCP (`tools/list` или Inspector) — нет `occam_playbook_publish`.



### Шаг 1 — `occam_playbook_resolve` (PB2 + PB4a)

| URL | Expect |
|-----|--------|
| `https://docs.docker.com/get-started/` | `provenance: community` |
| `https://kubernetes.io/docs/concepts/overview/` | `pageClass`, `knowledgeSchema`, `genome` fields |
| `https://nginx.org/en/docs/` | seed provenance + auto-policy hints |



### Шаг 2 — `occam_map` (Recipe 0f / P1-3)

- `nginx.org/en/docs/` + `source=sitemap` → `ok:true`, `linkCount` ≥ 10  
- `focus_query` rank: match в **любом** `links[]`, не только `#1`  
- Rotation: `map-nginx-sitemap-focus`



### Шаг 3 — `occam_digest` (Часть 1 + chain)

- map → pick ≤8 URLs → digest с `focus_query` (Recipe **0f** chain)  
- `items[].focusMatched`, `agentHints`; `mediaRefs[]` на ok rows (v0.8.10)



### Шаг 4 — `occam_probe` + `occam_transcode`

- openai: probe vs transcode consistency  
- transcode `playbook_policy=auto` на nginx после resolve  
- `mediaRefs[]` на ok transcode (v0.8.10)



### Шаг 5 — `occam_playbook_heal` (PB3)

| Case | Expect |
|------|--------|
| **POS** nginx leaf + `content_selectors=#sidebar` → `content_selectors_miss` → `suggestedNext: occam_playbook_heal` → вызов heal → `ok` + `domSkeleton` | `corpora/l3-heal-learn.jsonl` pilots |
| **NEG** not-found / captcha URL (reddit `js_challenge` если доступен) | **НЕТ** heal hint |



### Шаг 6 — `occam_playbook_save` (PB3 desk-min)

- После heal pilot: `occam_playbook_save` с `verify=true` и минимальным draft JSON (host drafts)  
- Зафиксируй `ok` или `failure.code` (`playbook_verify_failed`, schema)  
- Полный retry loop **не** blocker; tool **ДОЛЖЕН** быть вызван live MCP



### Шаг 7 — `occam_extract_knowledge` (PB4b Recipe D)

| Case | Expect |
|------|--------|
| **POS** после k8s resolve → extract на concepts URL | `ok:true`, `facts[]` non-empty, `meta.koId` |
| **NEG** extract на nginx без schema | `knowledge_schema_missing` |



### Шаг 8 — Session spot

- `occam_transcode` `http://127.0.0.1/` → `private_url_blocked`  
- public nginx OK без session



---



## Часть 3 — Recipe R (K9 orchestration)



Один сквозной chain (primary: **kubernetes.io**, fallback: **nginx.org**):



```text
occam_playbook_resolve(entry_url)
  → occam_map(entry_url, max_links≤32)
  → host picks ≤8 URLs from map.links[]
  → occam_digest(urls, focus_query=…)
  → occam_transcode(leaf, playbook_policy=auto)
  → occam_extract_knowledge(concepts_url)   # PB4b
```



Проверь: honest `failure.code` на bad items; `mediaRefs[]` на transcode/digest ok rows; resolve **до** map/digest на k8s. Normative: [GENOME_EXCHANGE_TEST_PLAN.md §9.2](../docs/GENOME_EXCHANGE_TEST_PLAN.md).



---



---



## Формат отчёта



1. **Executive summary** — доверие к tools; golden pass/fail; rotation findings; **eight-tool 8/8**.

2. **Delta analysis (обязательно)** — таблица milestone × verified live? × evidence × Δ vs baseline **2026-06-17** (см. § Expected outcomes).

3. **Regression verdict** — FROZEN only: blocker да/нет (≥2 балла падение).

4. **Generalization verdict** — ROTATION: overfitting риск да/нет.

5. **Per-URL deep dives** (FROZEN + ROTATION) — HTML → probe → transcode → scores.

6. **Token knobs** — B1 + B2 отдельно.

7. **Digest** — focusMatched, agentHints, partial, combined, `mediaRefs[]`.

8. **Regression risks** — заполненная таблица.

9. **Actionable fixes** — symptom → layer.

10. **Сводная таблица** — id × rubric × Agent-ready? (колонки **layer**: frozen/rotation).

11. **§ Tool surface matrix (обязательно)** — 8 rows × tool × called? × URL × ok/failure.code × evidence → **Eight-tool PASS/FAIL**.

12. **§ Recipe coverage (обязательно)** — A / B / C / D(map) / E(heal) / R(K9) — каждый **WORKS | PARTIAL | FAIL**.



**Сохрани:** `corpora/quality-audit-reports/YYYY-MM-DD-full-eight-tool-audit.md` (или дата прогона). **Не коммить** без «коммить».



### Итоговые вердикты (обязательно в конце отчёта)



| Вердикт | Когда |

|---------|-------|

| **Merge PASS** | FROZEN: нет падения **≥2** на Fidelity/Honesty vs baseline **2026-06-17** |

| **Eight-tool PASS** | Все **8** `occam_*` tools вызваны live MCP с evidence |

| **Generalization OK** | Rotation не хуже frozen на том же tier (SPA noise — оговорка, не blocker) |

| **Needs work** | FROZEN regression blockers **или** Eight-tool FAIL **или** новое падение ≥2 на golden |



### Delta analysis — шаблон таблицы



| Change (milestone) | Frozen case | Baseline score | This run | Δ | Live evidence (1 line) |

|--------------------|-------------|--------------|----------|---|------------------------|

| nginx prune | nginx-doc Noise | 4 | | | |

| strict focusMatched | digest-two-docs MDN | false | | | |

| agentHints | digest-two-docs | present + warnings | | | |

| openai structure | openai-concepts Struct | 3 | **4** | +1 | `## Embeddings`, `## Tokens` in live MCP |
| B2 closure @512 | B2 Functions Fid | 3 | **4** | +1 | canonical phrase @512 live MCP |

| nuxt HTTP noise | nuxt-spa Noise | **4** | | | |

| … | | | | | |



### Итоговые вердикты (detail)



| Вердикт | Когда |

|---------|-------|

| **Known gaps** | ~~openai Struct 3~~, ~~B2 Fid 3 @512~~ — closed Path A 2026-06-15 |

| **Do not use** | hub+definition Q; Docker digest без `focusMatched`; per_url_max_tokens≤256 only |



---



## Expected outcomes — FROZEN baseline 2026-06-17 (post-L0 consolidation, L0 core CLOSED)

**Верифицировано:** live MCP **2026-06-15** (consolidation run) vs compare baseline **2026-06-16** · report [`quality-audit-reports/2026-06-17-post-l0-consolidation.md`](quality-audit-reports/2026-06-17-post-l0-consolidation.md) · gates `L0_GATE_OK` + `L2_DIGEST_OK` + `L2_MAP_OK` + `L2_SESSION_OK` + `L2_TRANSPORT_OK`.

**Slice spots (required on full eight-tool audit):** 0f map→digest chain · session `127.0.0.1` blocked + public nginx OK · transport **8** tools stdio · PB2 community resolve · PB3 heal hint + **save desk-min call** · PB4 resolve + Recipe D extract · **mediaRefs** on ok transcode/digest (v0.8.10).

### FROZEN golden (regression = падение ≥2 на Fidelity или Honesty)

| Scenario | Baseline score | Re-run floor | Blocker if |
|----------|----------------|--------------|------------|
| mdn-js-guide | Fid 5, Noise 4, Struct 5 | Fid/Honesty ≥3 | Fid or Honesty ≤3 |
| nginx-doc | Fid 5, Noise 4 | Noise ≥2, Fid ≥3; **нет** `ngx_*` spam; TOC не обрезан | Fid or Honesty ≤3; ngx spam вернулся |
| nuxt-spa HTTP | Fid 5, Noise **4**, Struct 4 | Fid ≥3, Noise ≥3 | Fid or Honesty ≤3; footer promos вернулись |
| nuxt-spa browser | Fid 5, Noise **4** (same as HTTP) | same | Fid or Honesty ≤3; footer promos вернулись |
| openai-concepts | Probe 5, Fid 4, Struct **4** | Probe ≥3, Fid ≥2, Struct ≥3 | Fid or Honesty ≤2; Probe ≤3; Struct ≤2 |
| not-found | Honesty 5, ProbeH 5 | Honesty ≥3 | Honesty ≤3 |
| B1 hub TOC | Token 5, Fid 5 | Token ≥4 | Token ≤2 |
| B2 Functions @512 | Fid **4**, Token 4 | Fid ≥3 (canonical phrase @512) | Fid ≤2 or section lost |
| digest-two-docs | MDN `focusMatched:false`, nginx `true`, `agentHints` present | same fields | MDN fm:true or hints absent; nginx fm:false |
| digest-partial | Honesty 5 | Honesty ≥3 | Honesty ≤3 |
| digest-token-cap | Token econ 5 (~496/449 @512) | caps hold ±32 tok | per-URL cap broken |

**Prior baselines (история):**
- **2026-06-15 post-v0.7.9** — P2-6 browser + M1–M3 rotation re-verify Merge PASS; report [`quality-audit-reports/2026-06-15-post-v0.7.9-browser.md`](quality-audit-reports/2026-06-15-post-v0.7.9-browser.md); tier-3 golden floors unchanged (**2026-06-17**)
- **2026-06-17** — post-L0 consolidation Merge PASS (map + session + transport slice spots)
- **2026-06-16** — F3–F5 mini MCP re-audit PASS (`558864f` seeds verified live)
- **2026-06-15 Path A** — openai Struct 4, B2 Fid 4 @512 live MCP PASS
- **2026-06-15c** — live MCP tier-3 audit Merge PASS (post PB0 core slim)
- **2026-06-15b** — post-QA live MCP Merge PASS
- **2026-06-15a** — pre-publish MCP (ea230e0): digest fm MDN true, agentHints absent → **Needs work**

---

## Expected outcomes — prior baseline 2026-06-16 (post F3–F5 verify, gates green)

<details>
<summary>2026-06-16 tables (superseded by 2026-06-17 above)</summary>

**Верифицировано:** live MCP `project-0-FFOccamMCP-ff-occam` **2026-06-16** — F3/F4/F5 spot verify PASS · gates `L0_GATE_OK` + `L2_DIGEST_OK` · prior full audit **2026-06-15c** Merge PASS.

### FROZEN golden (regression = падение ≥2 на Fidelity или Honesty)

| Scenario | Baseline score | Re-run floor | Blocker if |
|----------|----------------|--------------|------------|
| mdn-js-guide | Fid 5, Noise 4, Struct 5 | Fid/Honesty ≥3 | Fid or Honesty ≤3 |
| nginx-doc | Fid 5, Noise 4 | Noise ≥2, Fid ≥3; **нет** `ngx_*` spam; TOC не обрезан | Fid or Honesty ≤3; ngx spam вернулся |
| nuxt-spa HTTP | Fid 5, Noise **4**, Struct 4 | Fid ≥3, Noise ≥3 | Fid or Honesty ≤3; footer promos вернулись |
| nuxt-spa browser | Fid 5, Noise **4** (same as HTTP) | same | Fid or Honesty ≤3; footer promos вернулись |
| openai-concepts | Probe 5, Fid 4, Struct **4** | Probe ≥3, Fid ≥2, Struct ≥3 | Fid or Honesty ≤2; Probe ≤3; Struct ≤2 |
| not-found | Honesty 5, ProbeH 5 | Honesty ≥3 | Honesty ≤3 |
| B1 hub TOC | Token 5, Fid 5 | Token ≥4 | Token ≤2 |
| B2 Functions @512 | Fid **4**, Token 4 | Fid ≥3 (canonical phrase @512) | Fid ≤2 or section lost |
| digest-two-docs | MDN `focusMatched:false`, nginx `true`, `agentHints` present | same fields | MDN fm:true or hints absent; nginx fm:false |
| digest-partial | Honesty 5 | Honesty ≥3 | Honesty ≤3 |
| digest-token-cap | Token econ 5 (~501/449 @512) | caps hold ±32 tok | per-URL cap broken |

</details>

---

## Expected outcomes — prior baseline 2026-06-15b (reference only)

<details>
<summary>2026-06-15b tables (superseded by 2026-06-15c above)</summary>

### FROZEN golden (regression = падение ≥2 на Fidelity или Honesty)

| Scenario | Baseline score | Re-run floor | Blocker if |
|----------|----------------|--------------|------------|
| mdn-js-guide | Fid 5, Noise 4, Struct 5 | Fid/Honesty ≥3 | Fid or Honesty ≤3 |
| nginx-doc | Fid 5, Noise 4 | Noise ≥2, Fid ≥3 | Fid or Honesty ≤3 |
| nuxt-spa HTTP | Fid 5, Noise **3**, Struct 4 | Fid ≥3, Noise ≥1 | Fid or Honesty ≤3 |
| nuxt-spa browser | Fid 5, Noise **3** (same as HTTP) | same | Fid or Honesty ≤3 |
| openai-concepts | Probe 5, Fid 4, Struct **3** | Probe ≥3, Fid ≥2 | Fid or Honesty ≤2; Probe ≤3 |
| not-found | Honesty 5, ProbeH 5 | Honesty ≥3 | Honesty ≤3 |
| B1 hub TOC | Token 5, Fid 5 | Token ≥4 | Token ≤2 |
| B2 Functions @512 | Fid **3**, Token 4 | Fid ≥1 (closure section present) | Fid ≤1 or section lost |
| digest-two-docs | MDN `focusMatched:false`, nginx `true`, `agentHints` present | same fields | MDN fm:true or hints absent |
| digest-partial | Honesty 5 | Honesty ≥3 | Honesty ≤3 |
| digest-token-cap | Token econ 5 (~501/449 @512) | caps hold ±32 tok | per-URL cap broken |

</details>



### Known gaps (track via playbook seeds + compile; **не** regression blockers)



| Gap | Baseline | Track in |
|-----|----------|----------|
| ~~openai-concepts Structure~~ | ✅ Struct **4** verified Path A 2026-06-15 | `developers.openai.com.seed.json` → `openAiDocHeadings` |
| ~~B2 definition @512~~ | ✅ Fid **4** verified Path A 2026-06-15 | C# `TokenBudget.PreserveDefinitionalAnchor` |
| ~~nuxt-spa footer~~ | ✅ Noise **4** verified 2026-06-16 | `nuxt.com.seed.json` → `postMarkdown.nuxtFooterPromos` |
| ~~nuxt-install shiki~~ | ✅ Noise **4** verified M2 2026-06-18 | `nuxt.com.seed.json` → `postMarkdown.nuxtShikiCssStrip` |
| ~~Docker digest JS shell~~ | ✅ closed M1 2026-06-18 — rotation `digest-postgres-docker` | `docs.docker.com` seed + `dockerMarlinToolbarStrip` + hub fallback |
| ~~map sitemap CHANGES* pages~~ | ✅ closed M3 2026-06-18 | `MapLinkFilter` when `filter_nonsense` |

**Worker evaluation rule:** judge default **full transcode** (`fit_markdown=false`) on **golden hosts only** (`corpora/golden-hosts.json`).

**Fix location rule:** FROZEN URLs (nuxt-spa, openai-concepts, nginx-doc) are **audit anchors** — improvements go to **`profiles/playbooks/seeds/`** or C# compile, **not** `generic-markdown-prune.mjs` and **not** new per-host worker mjs.

### ROTATION baseline 2026-06-17 (generalization, не golden)

| rotation id | Baseline | Overfitting signal |
|-------------|----------|-------------------|
| map-nginx-sitemap-focus | Map 5 | focus rank not only homepage; filter links[] for digest |
| digest-postgres-docker | PG fm:true; Docker fm:true, no JS shell / thin_extract | docker.com seed M1 |
| nuxt-install | Fid 5, Noise **4** | shiki/tab bar noise via seed M2 |
| openai-rate-limits | Fid 5, Probe 5 | probe honest |
| mdn-array-ref | Fid 5 | frozen tier-A OK |
| nginx-http-core-leaf | Fid 5 | leaf not index spam |
| mdn-404-alt | Honesty 5 | same as not-found |
| b2-closures-leaf | Fid 3–4 | advanced closures, not canonical def |

### ROTATION baseline 2026-06-16 (prior)



| rotation id | Baseline | Overfitting signal |

|-------------|----------|-------------------|

| mdn-array-ref | Fid 5 | frozen tier-A OK |

| nginx-http-core-leaf | Fid 5 | leaf not index spam |

| nuxt-install | Fid 5, Noise **3** | shiki strip via seed (`558864f` verified) |

| openai-rate-limits | Fid 5, Probe 5 | probe honest |

| mdn-404-alt | Honesty 5 | same as not-found |

| b2-closures-leaf | Fid 3–4 | advanced closures, not canonical def |

| digest-postgres-docker | PG fm:true; Docker fm:false + JS shell | SPA extract gap on docker.com |

---



## Правила честности



- Tool fail → покажи `failure.code`; не заполняй из памяти.

- Разделяй **баг продукта** vs **workflow** vs **site drift** (rotation-only).



Начни с `mdn-js-guide` и `nginx-doc`.



---



## Copy-paste — full eight-tool audit (v0.8.10-media-refs) — **normative**



```text
@corpora/quality-audit-agent.PROMPT.md @corpora/quality-sprint-post-v0.8.7-pb4c-audit.PROMPT.md @corpora/quality-sprint-wide-cursor-desk.PROMPT.md @AGENTS.md @docs/15-occam-digest.md @docs/GENOME_EXCHANGE_TEST_PLAN.md @docs/HEAL_LEARN_TEST_PLAN.md @corpora/quality-audit-rotation.jsonl @corpora/l3-heal-learn.jsonl @corpora/l4-genome.jsonl

Репозиторий: c:\PROJECTS\FFOccamMCP (workspace обязателен).
Перед стартом: occam-doctor + Reload MCP (ff-occam).
MCP: project-0-FFOccamMCP-ff-occam — ТОЛЬКО CallMcpTool.

ЗАПРЕЩЕНО: gate/worker CLI/dotnet run/desk scripts как замена MCP; ok:true без rubric; training data; definitions с MDN index (только B2 leaf).

═══════════════════════════════════════
ЧАСТЬ 1 — QUALITY AUDIT (FROZEN + ROTATION)
═══════════════════════════════════════
Метод: HTML baseline (WebFetch) ДО каждого MCP. Rubric 1–5 + цитаты HTML vs MD.

Порядок (строго):
1. FROZEN A: mdn-js-guide → nginx-doc → nuxt-spa (HTTP, затем browser) → openai-concepts → not-found
2. FROZEN B: B1 hub → B2 leaf (definitions только leaf)
3. FROZEN C: C1 digest-two-docs → C2 digest-partial → C3 digest-token-cap
4. ROTATION: минимум 3 case из quality-audit-rotation.jsonl (вкл. map-nginx-sitemap-focus)
5. Regression: baseline 2026-06-17; blockers mdn/nginx/404/digest-partial (падение ≥2 = FAIL)

Начни с mdn-js-guide и nginx-doc.

═══════════════════════════════════════
ЧАСТЬ 2 — EIGHT-TOOL SURFACE (ОБЯЗАТЕЛЬНО 8/8)
═══════════════════════════════════════
В отчёте § «Tool surface» — таблица: tool × вызван? × URL × ok/failure.code × evidence (1 строка).
Merge FAIL если любой из 8 tools не вызван live MCP.

Шаг 0–8: см. PROMPT § «Часть 2 — Eight-tool surface».

═══════════════════════════════════════
ЧАСТЬ 3 — RECIPE R (K9) ORCHESTRATION
═══════════════════════════════════════
Один сквозной chain (kubernetes.io, fallback nginx):
resolve → map → digest(focus) → transcode(auto) → extract_knowledge
Проверь: honest failure.code на bad items; mediaRefs на transcode/digest ok rows.

═══════════════════════════════════════
ФОРМАТ ОТЧЁТА
═══════════════════════════════════════
PROMPT § «Формат отчёта» (12 пунктов) ПЛЮС:
§ Tool surface matrix — 8/8 MUST PASS
§ Recipe coverage — A / B / C / D(map) / E(heal) / R(K9) — WORKS|PARTIAL|FAIL
§ Итоговые вердикты: Merge PASS | Eight-tool PASS | Generalization OK | Needs work

Сохрани: corpora/quality-audit-reports/YYYY-MM-DD-full-eight-tool-audit.md
Не коммить без «коммить».
```



---

## Copy-paste — on-demand tier-3 audit (focused, без full 8/8) — superseded by block above

> **Latest full audit template:** copy-paste block above · **Focused re-spot:** [`2026-06-16-post-v0.8.7-pb4c-exchange.md`](quality-audit-reports/2026-06-16-post-v0.8.7-pb4c-exchange.md) Merge PASS · OpenAI ENV_BLOCKED — egress retry when unblocked

```text
@corpora/quality-audit-agent.PROMPT.md @corpora/quality-audit-reports/2026-06-16-post-v0.8.4-pb3-heal-learn.md @AGENTS.md @docs/00-agent-handbook.md @docs/MASTER-PLAN.md @docs/GENOME_EXCHANGE_TEST_PLAN.md

Репозиторий: c:\PROJECTS\FFOccamMCP (workspace обязателен).
MCP: project-0-FFOccamMCP-ff-occam — только CallMcpTool.
Tools (8): occam_probe · occam_transcode · occam_digest · occam_playbook_resolve · occam_map · occam_playbook_heal · occam_playbook_save · occam_extract_knowledge
Запрещено: gate, worker CLI, dotnet run, desk scripts как замена MCP; occam_playbook_publish (ninth tool — не существует).

Gates: L0_GATE_OK + L2_DIGEST_OK + L2_MAP_OK + L2_SESSION_OK + L2_TRANSPORT_OK + L3_HEAL_LEARN_OK + L4_GENOME_OK + L2_MEDIA_REFS_OK.
Tier-3 baseline: 2026-06-17 (L0 core CLOSED). Delta vs post-v0.8.4 report.

Порядок: FROZEN A → B1/B2 → C1–C3 → ROTATION (min 3) → slice spots.

Обязательно: HTML до MCP; rubric 1–5; regression = FROZEN only (≥2 drop = fail).
Формат: PROMPT § «Формат отчёта».
Начни с mdn-js-guide и nginx-doc.
```

---

## Copy-paste — OpenAI egress retry only (when ENV unblocks)

> **Known gap:** `openai-concepts` **ENV_BLOCKED** since tier-3 §11 — WebFetch OK, MCP `http_403`. Run only after egress/VPN change.

```text
@corpora/quality-audit-reports/2026-06-16-tier3-reaudit-v0.8.4-delta.md @profiles/playbooks/seeds/developers.openai.com.seed.json @AGENTS.md

Репозиторий: c:\PROJECTS\FFOccamMCP · MCP Reload · CallMcpTool only · VPN OFF.

Задача: re-spot ТОЛЬКО openai-concepts — append §11b to tier-3 report.

1. WebFetch platform.openai.com/docs/concepts — ## Embeddings + ## Tokens.
2. occam_probe → documentation, not requires_login.
3. occam_transcode http_then_browser → ok:true, Struct ≥4 vs HTML.

Вердикт: WORKS / still ENV_BLOCKED. Baseline 2026-06-17 unchanged unless full tier-3 requested.
Коммить только по «коммить».
```

---

## Copy-paste — post-L0 consolidation audit (v0.7.7-install)

```text
@corpora/quality-audit-agent.PROMPT.md @corpora/quality-audit-reports/2026-06-17-post-l0-consolidation.md @AGENTS.md @docs/15-occam-digest.md @docs/MASTER-PLAN.md

Репозиторий: c:\PROJECTS\FFOccamMCP (workspace обязателен).
MCP: project-0-FFOccamMCP-ff-occam — только CallMcpTool.
Tools (5): occam_probe · occam_transcode · occam_digest · occam_playbook_resolve · occam_map
Запрещено: gate, worker CLI, dotnet run как замена MCP.

Gates: L0_GATE_OK + L2_DIGEST_OK + L2_MAP_OK + L2_SESSION_OK + L2_TRANSPORT_OK.
Tier-3 baseline: 2026-06-17 (L0 core CLOSED). Delta vs 2026-06-16.
Не в scope: heal/save (P3 CUT), bundle/adaptive (P2-3 CUT), MOB1 mobile app.

Порядок: FROZEN A → B1/B2 → C1–C3 → ROTATION (4 required + map-nginx-sitemap-focus) → slice spots (0f, session, transport).

Обязательно: HTML до MCP; rubric 1–5; regression = FROZEN only (≥2 drop = fail).
Формат: PROMPT § «Формат отчёта» (10 пунктов).
Начни с mdn-js-guide и nginx-doc.
```

---

## Copy-paste — стартовый промпт для Agent chat (post PB1.1, v0.7.2) — superseded

```text
@corpora/quality-audit-agent.PROMPT.md @AGENTS.md @docs/15-occam-digest.md @docs/MASTER-PLAN.md

Репозиторий: c:\PROJECTS\FFOccamMCP (workspace = эта папка — обязательно, иначе MCP недоступен).
MCP: project-0-FFOccamMCP-ff-occam — только CallMcpTool.
Tools (4): occam_probe · occam_transcode · occam_digest · occam_playbook_resolve
Запрещено: gate, worker CLI, dotnet run как замена MCP. Не ok:true без rubric; не training data.

Контекст: PB1.1 shipped — resolve read-only из profiles/playbooks/seeds/*.seed.json.
Gates: L0_GATE_OK + L2_DIGEST_OK. Tier-3 baseline: 2026-06-15c Merge PASS.
Не в scope: heal/save, playbook_policy auto на transcode, map, port 36 playbooks.

---

Часть 1 — Quality audit (если MCP stale или нужен bump baseline)

Порядок: FROZEN A → B1/B2 → C1–C3 → ROTATION (§ Порядок прогона).

Обязательно:
- HTML baseline (WebFetch) до каждого MCP;
- default transcode = full markdown (fit_markdown=false) для Fidelity/Noise на golden;
- B1 hub + B2 leaf (definitions — только B2 leaf, не index);
- digest C1–C3: items[].focusMatched + agentHints при focus_query;
- probe vs transcode consistency на openai-concepts;
- nuxt-spa: HTTP и отдельно browser — Noise sidebar/Carbon/Community;
- resolve spot: nginx.org, platform.openai.com → seed, unknown host → playbook_not_found.

Сверка: baseline 2026-06-15c vs ea230e0 milestones (nginx Noise≥4, focusMatched в API, probe honest, B1/B2).
Regression blockers: mdn index, 404, digest-partial — падение ≥2 на Fidelity/Honesty = fail.
Known gaps (не blockers): openai Struct 3, B2 Fid 3 @512 — seeds/compile. F3/F4 nuxt: verify `558864f` seed flags (не generic).

Формат: PROMPT § «Формат отчёта» (10 пунктов + итоговые вердикты).
Начни с mdn-js-guide и nginx-doc.

---

Часть 2 — Решение «что дальше»

| Path | Срез | Когда |
|------|------|-------|
| **F3–F5 verify** | ✅ PASS 2026-06-16 | `corpora/quality-sprint-audit-followup.PROMPT.md` |
| **A** | openai Struct seed + B2 @512 compile | ✅ PASS 2026-06-15 (live MCP re-audit) |
| B | Agent recipe: probe → resolve → transcode(selectors) | Docs, параллельно |
| C | P1-3 occam_map | Только по решению продукта |
| — | heal/save, auto playbook_policy, list_images | НЕ сейчас |

Выход: F3–F5 verify verdict + один path forward.
```

---

## Copy-paste — F3–F5 verify only (короткий)

См. полный блок в [`quality-sprint-audit-followup.PROMPT.md`](quality-sprint-audit-followup.PROMPT.md) § Copy-paste.

