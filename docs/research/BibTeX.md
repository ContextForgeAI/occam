# Citation

## Citing this software

Use the software entry until a paper exists. There is no paper yet; a `@misc` entry pointing at an
arXiv identifier that does not exist would be a fabricated citation.

```bibtex
@software{occam_mcp,
  title        = {Occam: a proof-of-read layer for MCP agents},
  author       = {{Ebony Swan}},
  year         = {2026},
  version      = {1.1.1},
  url          = {https://github.com/ContextForgeAI/occam},
  note         = {Local MCP server for verifiable web-content extraction.
                  Proof-of-read canary protocol: PROBE_PROTOCOL.md}
}
```

Citing the protocol specifically:

```bibtex
@techreport{occam_probe_protocol_v1,
  title       = {Occam Probe Protocol v1: Proof-of-Read Canaries},
  author      = {{Ebony Swan}},
  year        = {2026},
  institution = {Occam project},
  type        = {Protocol specification},
  number      = {occam-canary-v1},
  url         = {https://github.com/ContextForgeAI/occam/blob/main/PROBE_PROTOCOL.md},
  note        = {Status: Experimental. Test vectors and cross-platform
                 verification included in the repository.}
}
```

## Normative references used by the protocol

Verified primary sources, cited in `PROBE_PROTOCOL.md §12`.

```bibtex
@techreport{rfc2104,
  title       = {{HMAC}: Keyed-Hashing for Message Authentication},
  author      = {Krawczyk, Hugo and Bellare, Mihir and Canetti, Ran},
  year        = {1997},
  month       = feb,
  institution = {Internet Engineering Task Force},
  type        = {Request for Comments},
  number      = {2104},
  doi         = {10.17487/RFC2104},
  url         = {https://www.rfc-editor.org/info/rfc2104}
}

@techreport{rfc5869,
  title       = {{HMAC}-based Extract-and-Expand Key Derivation Function ({HKDF})},
  author      = {Krawczyk, Hugo and Eronen, Pasi},
  year        = {2010},
  month       = may,
  institution = {Internet Engineering Task Force},
  type        = {Request for Comments},
  number      = {5869},
  doi         = {10.17487/RFC5869},
  url         = {https://www.rfc-editor.org/info/rfc5869}
}

@techreport{rfc4648,
  title       = {The Base16, Base32, and Base64 Data Encodings},
  author      = {Josefsson, Simon},
  year        = {2006},
  month       = oct,
  institution = {Internet Engineering Task Force},
  type        = {Request for Comments},
  number      = {4648},
  doi         = {10.17487/RFC4648},
  url         = {https://www.rfc-editor.org/info/rfc4648}
}

@techreport{rfc2119,
  title       = {Key words for use in {RFCs} to Indicate Requirement Levels},
  author      = {Bradner, Scott},
  year        = {1997},
  month       = mar,
  institution = {Internet Engineering Task Force},
  type        = {Request for Comments},
  number      = {2119},
  doi         = {10.17487/RFC2119},
  url         = {https://www.rfc-editor.org/info/rfc2119}
}

@techreport{rfc6238,
  title       = {{TOTP}: Time-Based One-Time Password Algorithm},
  author      = {M'Raihi, David and Machani, Salah and Pei, Mingliang and Rydell, Johan},
  year        = {2011},
  month       = may,
  institution = {Internet Engineering Task Force},
  type        = {Request for Comments},
  number      = {6238},
  doi         = {10.17487/RFC6238},
  url         = {https://www.rfc-editor.org/info/rfc6238},
  note        = {Cited as prior art for the time-bucketed HMAC construction}
}
```

## Deliberately absent

No entries exist here for:

- **a paper about this work** — none has been written or submitted;
- **"Tool Receipts", "Progressive Disclosure", "Tool Attention", "NabaOS"** — these appeared in the
  project's early notes as possible prior art and have **not** been confirmed against any primary
  source. See [`related_work.md`](related_work.md) §4 and §7. They will be added once identified, or
  dropped;
- **MCP specification** — cited by URL rather than BibTeX, as it is a living document without a
  stable citable version.
