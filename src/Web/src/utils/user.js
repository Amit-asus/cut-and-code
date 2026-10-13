export function initialsFrom(email) {
  const local = (email ?? "").split("@")[0];
  const parts = local.split(/[._-]+/).filter(Boolean);
  const letters = parts.length > 1 ? parts[0][0] + parts[1][0] : local.slice(0, 2);
  return letters.toUpperCase() || "?";
}
