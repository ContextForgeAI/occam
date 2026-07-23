# HTTP Semantics

This introduction intentionally precedes the target with enough general HTTP material to make head-only truncation unable to retain the later answer. It discusses representation metadata, caches, intermediaries, connection handling, transfer coding, content negotiation, request methods, response status codes, validators, conditional requests, range requests, and routing without defining the requested status.

## Unauthorized deployment notes

WRONG_SECTION. Unauthorized is a deployment label for clients that appear in audit logs. Unauthorized retries and Unauthorized proxies are discussed here. Unauthorized traffic is the dominant topic, but this section does not define status 401.

## 15.5.2 401 Unauthorized {#section-15.5.2}

ANSWER_401. The 401 status code indicates that the request lacks valid authentication credentials for the target resource.

## 407 Proxy Authentication Required {#section-15.5.8}

The 407 status code concerns an authenticating proxy rather than the origin server.
