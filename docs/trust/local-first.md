# Local-first model

Occam is designed to run as a **local** MCP server on your machine.

## What that means

- Your AI host starts Occam as a local process (stdio by default).  
- HTML extraction and Markdown compilation happen locally.  
- There is no required cloud API key for core extract tools.  
- Session profiles, receipt keys, and saved playbooks are local files.  

## What leaves your machine

- **Egress to the URLs you ask Occam to fetch** (and optional search provider, if you configure one).  
- Optional managed extract providers, only if you explicitly enable them in configuration.  
- Optional time-anchor or remote transport features you turn on yourself.  

Fetched web content is **untrusted input**. Occam extracts text; it does not treat page JavaScript as trusted code beyond the browser sandbox used for rendering.

## What Occam does not do (default product)

- No telemetry endpoint  
- No automatic update phone-home as part of a normal extract  
- No cloud middleman for ordinary `occam_transcode` / `occam_digest` flows  

See also: [Trust & Safety](../trust-and-safety.md) · [Configuration](../configuration.md)
