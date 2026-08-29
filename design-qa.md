# Design QA — Occam documentation hero

## Evidence

- Source visual truth: `C:\PROJECTS\FFOccamMCP\docs\assets\occam-signal-corridor-rc4-fixture.png`
- Implementation URL: `http://127.0.0.1:8765/`
- Desktop implementation screenshot: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-docs-rc4-truth-desktop-1440x1024.png`
- Mobile implementation screenshot: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-docs-rc4-truth-mobile-430x932.png`
- Combined full-view and focused comparison: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-design-qa-comparison-truth.png`
- Motion rest screenshot: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-docs-rc4-motion-desktop-rest-1440x1024.png`
- Motion pointer-response screenshot: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-docs-rc4-motion-desktop-interactive-1440x1024.png`
- Motion mobile screenshot: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-docs-rc4-motion-mobile-430x932.png`
- Motion source/rest/interaction comparison: `C:\Users\Developer\.codex\visualizations\2026\08\28\01a04943-a59c-7841-8186-c06f47011d53\occam-docs-rc4-motion-comparison.png`
- State: documentation homepage, dark/system theme, no authentication, initial scroll position; resting and fine-pointer interaction states.
- Desktop viewport: 1440 × 1024 CSS px at DPR 1. Browser capture is 1425 × 1013 px after scrollbar/chrome exclusion.
- Mobile viewport: 430 × 932 CSS px at DPR 1. Browser capture is 415 × 899 px after scrollbar/chrome exclusion.
- Source pixels: 1487 × 1058. The implementation preserves the source aspect ratio and uses the same bitmap asset without recomposition.
- Density normalization: the combined comparison renders source and implementation at the same 581 px comparison width (413.95 px and 413.59 px high respectively). The focused row enlarges the package-command region at matched scale.
- Motion comparison normalization: source, resting implementation, and pointer-response implementation are rendered at the same 477 px card width. The two implementation captures have identical 1425 × 1013 px dimensions.

## Findings

- No actionable P0, P1, or P2 mismatch remains.
- The motion pass introduces no new visual mismatch. The bitmap content remains unchanged at rest, and the pointer response stays below one degree per axis in the captured state.
- [P3] The selected visual includes its own illustrative repository navigation inside the real MkDocs shell.
  Location: homepage hero image.
  Evidence: the combined comparison shows the selected source intact inside the production documentation frame.
  Impact: the framing is clear and preserves the user's selected variant, but a future headerless export could reduce visual chrome.
  Fix: optional only; create a separately art-directed headerless source asset rather than cropping or rebuilding the current image with CSS.

## Required Fidelity Surfaces

- Fonts and typography: the raster source is preserved exactly. Live command, caption, metadata, and CTA typography use the existing Occam font tokens and retain clear hierarchy at desktop and mobile widths.
- Spacing and layout rhythm: the source keeps its aspect ratio, the live launch strip stacks cleanly at 430 px, and neither tested viewport has horizontal overflow. Desktop and mobile screenshots show no collision or clipping of persistent controls.
- Colors and visual tokens: the asset retains the selected near-black, warm-white, and teal palette. The surrounding MkDocs shell and live controls use the existing project tokens with sufficient contrast.
- Image quality and asset fidelity: the final 1487 × 1058 project asset is the corrected source visual, not a CSS drawing, placeholder, inline SVG, or screenshot approximation. It loads at both tested breakpoints with no missing-image errors.
- Copy and content: the package command is exactly `npm install -g ff-occam@1.0.0-rc.4`; the byte comparison is explicitly qualified as one controlled fixture, bytes rather than tokens, and not universal.
- Accessibility: the page retains a semantic H1, descriptive image alt text, visible text equivalents for the command and CTAs, and responsive tap targets. The one-time entrance is disabled by `prefers-reduced-motion`; pointer depth is gated to fine-pointer/hover devices and resets on pointer exit or media-query changes.

## Interaction and Runtime Checks

- `Get your first result` navigates to `/quick-start/` and renders `Choose how you use AI`.
- `Inspect proof` targets `#measured-before-and-after` successfully.
- Browser console: no warnings or errors.
- Broken images: 0.
- Desktop overflow: false.
- Mobile overflow: false.
- Motion script loaded and set `data-oc-motion-ready="true"`.
- Pointer response measured at `rotateX(0.48deg)`, `rotateY(0.63deg)`, and `translateY(0.51px)` for the captured point.
- Pointer exit cleared all inline motion variables and returned the image to its identity transform.
- Reduced-motion CSS removes both the entrance animation and transformed image state; the script also resets motion when that preference changes.

## Comparison History

1. Initial implementation found two P2 issues: the desktop `.md-content` flex item collapsed to zero width, and nested Markdown mangled the signal-corridor structure. It also represented the selected graphic with HTML/CSS shapes instead of the real visual asset.
2. Fixes: added an explicit desktop content width, removed the nested graphic markup, corrected the selected bitmap's package text with a source-preserving image edit, embedded the real asset, and retained live copyable command/CTA controls below it.
3. Honesty pass: replaced the illustrative external URL label with the actual controlled fixture name, `representative-input.html`, while leaving the measured values and selected composition unchanged.
4. Post-fix evidence: the final desktop and mobile screenshots show the corrected layout; the combined comparison confirms source fidelity, the truthful fixture label, and exact `ff-occam` command text. No P0/P1/P2 issue remains.
5. Motion pass: added one-time entrance motion plus a bounded fine-pointer perspective response. The source/rest/interaction comparison confirms the real bitmap remains intact; desktop, mobile, reset, reduced-motion, navigation, console, broken-image, and overflow checks pass.

## Implementation Checklist

- [x] Use the selected source visual as a real project asset.
- [x] Correct the npm package name to `ff-occam`.
- [x] Preserve an accessible, copyable command outside the image.
- [x] Verify primary CTA navigation and proof anchor.
- [x] Verify desktop and mobile layout, image loading, console, and overflow.
- [x] Verify bounded pointer response and pointer-exit reset.
- [x] Disable motion for users who request reduced motion.

## Follow-up Polish

- Optional P3: commission a headerless derivative if the illustrative repository navigation ever feels redundant inside the live docs shell.

final result: passed
