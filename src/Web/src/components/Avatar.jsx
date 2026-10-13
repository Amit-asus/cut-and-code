import { AVATAR_OPTIONS } from "../context/AvatarContext";
import { initialsFrom } from "../utils/user";

export function Avatar({ email, choice }) {
  const option = AVATAR_OPTIONS.find((o) => o.value === choice);
  if (option?.src) {
    return <img src={option.src} alt="" className="avatar-img" draggable="false" />;
  }
  return initialsFrom(email);
}
