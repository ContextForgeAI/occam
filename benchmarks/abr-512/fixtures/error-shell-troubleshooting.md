# Browser troubleshooting

When a tab shows **This page couldn’t load**, the usual advice is to reload, check the network, or try again after a few minutes.

This article explains how operators diagnose client render failures. It quotes the error chrome on purpose: “Reload to try again, or go back.” Those phrases appear inside a long, useful document and must not be treated as an error-shell extract.

Additional guidance covers cache busting, extension conflicts, TLS interception, and captive portals. The page is intentionally longer than a short_quality shell so length and quoting can be distinguished from a genuine render failure.

If the extract of this file is classified as `render_error`, the lexicon is too broad.

Operators also compare HAR traces, disable extensions one at a time, and confirm that DNS and TLS handshakes complete before blaming the document. A troubleshooting article that merely quotes browser chrome remains a healthy page.
