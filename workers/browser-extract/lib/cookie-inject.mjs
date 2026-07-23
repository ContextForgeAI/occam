/** Pre-goto cookie injection — opt-in only (privacy-reviewed per domain). */

export function isCookieInjectEnabled() {
  const v = (process.env.WT_COOKIE_INJECT ?? "").toLowerCase();
  return v === "1" || v === "true" || v === "yes";
}

export async function injectRecipeCookies(context, recipe) {
  if (!isCookieInjectEnabled() || !recipe?.cookies?.length) {
    return { injected: false, count: 0 };
  }
  try {
    await context.addCookies(recipe.cookies);
    return { injected: true, count: recipe.cookies.length };
  } catch {
    return { injected: false, count: 0 };
  }
}
