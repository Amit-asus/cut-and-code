import { createContext, useContext, useState } from "react";
import { useAuth } from "./AuthContext";

// The user picks their own avatar on the Profile page — we never guess it from their name/email.
// Stored per user id in this browser only (localStorage), so it doesn't follow them to other devices.
export const AVATAR_OPTIONS = [
  { value: "girl", label: "Girl", src: "/avatars/girl.svg" },
  { value: "boy", label: "Boy", src: "/avatars/boy.svg" },
  { value: "initials", label: "Initials", src: null },
];

const AvatarContext = createContext(null);

function storageKey(userId) {
  return `avatar:${userId}`;
}

function readAvatar(userId) {
  if (!userId) return "initials";
  try {
    const saved = localStorage.getItem(storageKey(userId));
    return AVATAR_OPTIONS.some((o) => o.value === saved) ? saved : "initials";
  } catch {
    return "initials";
  }
}

export function AvatarProvider({ children }) {
  const { user } = useAuth();
  // Bumped on every change so consumers re-render; the value itself is read from storage during render.
  const [, setVersion] = useState(0);

  const avatar = readAvatar(user?.id);

  function setAvatar(value) {
    if (!user) return;
    try {
      localStorage.setItem(storageKey(user.id), value);
    } catch {
      // Storage blocked — nothing we can persist.
    }
    setVersion((v) => v + 1);
  }

  return <AvatarContext.Provider value={{ avatar, setAvatar }}>{children}</AvatarContext.Provider>;
}

export function useAvatar() {
  return useContext(AvatarContext);
}
