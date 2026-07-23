# CORS guide

## Simple requests

Simple requests are requests that satisfy a constrained method and header profile.

Introductory context explains origins, preflight motivation, user agents, caching, credentials, and response headers. This material is relevant background but does not answer which methods are allowed. More context follows so that a constrained section window must choose between the early prose and the answer-bearing list.

Simple requests are discussed in relation to browser security models, origin comparison, redirects, caching, credentials, and response filtering. This background repeats the focus phrase but does not contain the requested answer.

Simple requests also interact with response tainting, exposed headers, preflight caches, network errors, service workers, and navigation. This second background block is deliberately answer-free.

ANSWER_BODY

- GET
- HEAD
- POST

The allowed request headers are Accept, Accept-Language, Content-Language, Content-Type, and Range under the documented restrictions.
