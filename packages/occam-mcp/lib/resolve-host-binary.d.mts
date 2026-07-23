export function resolveRid(platform?: NodeJS.Platform, arch?: string): string;
export function hostBinaryBaseNames(rid?: string): string[];
export function listHostBinaryCandidates(root: string, rid?: string): string[];
export function resolveHostBinary(root: string, rid?: string): string | null;
