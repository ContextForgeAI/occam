/** Lazy recipe registry — golden-hosts allowlist only (see corpora/golden-hosts.json). */



/** @type {{ hosts: string[], file: string }[]} */

const RECIPE_INDEX = [

  { hosts: ["developer.mozilla.org"], file: "./developer.mozilla.org.mjs" },

  { hosts: ["kubernetes.io"], file: "./kubernetes.io.mjs" },

  { hosts: ["nginx.org"], file: "./nginx.org.mjs" },

  { hosts: ["nuxt.com"], file: "./nuxt.com.mjs" },

  { hosts: ["docs.docker.com"], file: "./docs.docker.com.mjs" },

  { hosts: ["postgresql.org"], file: "./postgresql.org.mjs" },

];



/** @type {Map<string, object>} */

const recipeCache = new Map();



export function hostFromUrl(url) {

  try {

    return new URL(url).hostname.toLowerCase().replace(/^www\./, "");

  } catch {

    return "";

  }

}



function findRecipeEntry(host) {

  if (!host) {

    return null;

  }



  return (

    RECIPE_INDEX.find((entry) =>

      entry.hosts.some((h) => host === h || host.endsWith(`.${h}`)),

    ) ?? null

  );

}



/** Sync host lookup — no module import (for lean-assets routing). */

export function hasRecipe(url) {

  return findRecipeEntry(hostFromUrl(url)) != null;

}



/** @param {string} url */

export async function getRecipe(url) {

  const entry = findRecipeEntry(hostFromUrl(url));

  if (!entry) {

    return null;

  }



  if (!recipeCache.has(entry.file)) {

    const mod = await import(entry.file);

    recipeCache.set(entry.file, mod.default);

  }



  return recipeCache.get(entry.file) ?? null;

}



/** @returns {string[]} */

export function listRegisteredHosts() {

  return RECIPE_INDEX.flatMap((entry) => entry.hosts);

}


