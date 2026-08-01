export class FixedWindowRateLimiter {
  constructor({ limit, windowMs, now = () => Date.now(), maxKeys = 10_000 }) {
    if (!Number.isInteger(limit) || limit < 1) throw new Error("limit must be a positive integer");
    if (!Number.isInteger(windowMs) || windowMs < 1) throw new Error("windowMs must be a positive integer");
    this.limit = limit;
    this.windowMs = windowMs;
    this.now = now;
    this.maxKeys = maxKeys;
    this.entries = new Map();
  }

  take(key) {
    const now = this.now();
    let entry = this.entries.get(key);
    if (!entry || entry.resetAt <= now) {
      if (!entry && this.entries.size >= this.maxKeys) {
        const oldest = this.entries.keys().next().value;
        if (oldest !== undefined) this.entries.delete(oldest);
      }
      entry = { count: 0, resetAt: now + this.windowMs };
      this.entries.set(key, entry);
    }
    entry.count += 1;
    const allowed = entry.count <= this.limit;
    return {
      allowed,
      remaining: Math.max(0, this.limit - entry.count),
      retryAfterSeconds: Math.max(1, Math.ceil((entry.resetAt - now) / 1000)),
    };
  }
}

export function createDemoRateLimiters({ perMinute, perDay, now }) {
  return [
    new FixedWindowRateLimiter({ limit: perMinute, windowMs: 60_000, now }),
    new FixedWindowRateLimiter({ limit: perDay, windowMs: 86_400_000, now }),
  ];
}

export function takeAll(limiters, key) {
  const results = limiters.map((limiter) => limiter.take(key));
  const blocked = results.find((result) => !result.allowed);
  return blocked ?? {
    allowed: true,
    remaining: Math.min(...results.map((result) => result.remaining)),
    retryAfterSeconds: 0,
  };
}
