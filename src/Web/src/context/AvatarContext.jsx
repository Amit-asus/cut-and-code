import { createContext, useContext, useState } from "react";
import { useAuth } from "./AuthContext";

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
  const [, setVersion] = useState(0);

  const avatar = readAvatar(user?.id);

  function setAvatar(value) {
    if (!user) return;
    try {
      localStorage.setItem(storageKey(user.id), value);
    } catch {
    }
    setVersion((v) => v + 1);
  }

  return <AvatarContext.Provider value={{ avatar, setAvatar }}>{children}</AvatarContext.Provider>;
}

export function useAvatar() {
  return useContext(AvatarContext);
}
