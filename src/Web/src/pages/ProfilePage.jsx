import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api/client";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useToast } from "../context/ToastContext";
import { AVATAR_OPTIONS, useAvatar } from "../context/AvatarContext";
import { Avatar } from "../components/Avatar";

function daysSince(dateString) {
  const ms = Date.now() - new Date(dateString).getTime();
  return Math.max(0, Math.floor(ms / 86_400_000));
}

export function ProfilePage() {
  const { user, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const { showToast } = useToast();
  const { avatar, setAvatar } = useAvatar();
  const navigate = useNavigate();

  const [profile, setProfile] = useState(null);
  const [bookingStats, setBookingStats] = useState(null);
  const [error, setError] = useState(null);

  const canBook = user?.role !== "Admin";

  useEffect(() => {
    api
      .profile()
      .then(setProfile)
      .catch((err) => setError(err.message));

    if (canBook) {
      api
        .listBookings(1, 1)
        .then((result) => setBookingStats({ total: result.totalCount, latest: result.items[0] ?? null }))
        .catch(() => setBookingStats(null));
    }
  }, [canBook]);

  async function copyId() {
    try {
      await navigator.clipboard.writeText(profile.id);
      showToast("User ID copied to clipboard.");
    } catch {
      showToast("Couldn't copy — your browser blocked clipboard access.", { type: "error" });
    }
  }

  function handleLogout() {
    logout();
    navigate("/login");
  }

  if (error) {
    return <p className="message message-error">{error}</p>;
  }

  if (!profile) {
    return <p className="state-msg">Loading&hellip;</p>;
  }

  const memberSince = new Date(profile.createdAt);
  const days = daysSince(profile.createdAt);

  return (
    <div className="page profile-page">
      <section className="profile-hero">
        <div className="profile-hero-banner" aria-hidden="true" />
        <div className="profile-hero-body">
          <div className={`profile-avatar ${avatar !== "initials" ? "has-image" : ""}`}>
            <Avatar email={profile.email} choice={avatar} />
          </div>
          <div className="profile-identity">
            <h1 className="profile-email">{profile.email}</h1>
            <div className="profile-tags">
              <span className={`profile-role profile-role-${profile.role.toLowerCase()}`}>{profile.role}</span>
              <span className="profile-since">
                Member since {memberSince.toLocaleDateString(undefined, { month: "long", year: "numeric" })}
              </span>
            </div>
          </div>
          <button type="button" className="btn btn-outline profile-logout" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </section>

      <div className="profile-grid">
        <section className="profile-card">
          <h2 className="profile-card-title">Account details</h2>
          <dl className="profile-fields">
            <div className="profile-field">
              <dt>Email</dt>
              <dd>{profile.email}</dd>
            </div>
            <div className="profile-field">
              <dt>Role</dt>
              <dd>{profile.role}</dd>
            </div>
            <div className="profile-field">
              <dt>Joined</dt>
              <dd>
                {memberSince.toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" })}
                <span className="profile-muted"> &middot; {days === 0 ? "today" : `${days} day${days === 1 ? "" : "s"} ago`}</span>
              </dd>
            </div>
            <div className="profile-field">
              <dt>User ID</dt>
              <dd className="profile-id">
                <code>{profile.id}</code>
                <button type="button" className="btn-link" onClick={copyId}>
                  Copy
                </button>
              </dd>
            </div>
          </dl>
        </section>

        <div className="profile-side">
          {canBook && (
            <section className="profile-card">
              <h2 className="profile-card-title">Activity</h2>
              <div className="profile-stats">
                <div className="profile-stat">
                  <span className="profile-stat-value">{bookingStats ? bookingStats.total : "—"}</span>
                  <span className="profile-stat-label">Total bookings</span>
                </div>
                <div className="profile-stat">
                  <span className="profile-stat-value profile-stat-value-sm">
                    {bookingStats?.latest
                      ? new Date(bookingStats.latest.createdAt).toLocaleDateString(undefined, {
                          day: "numeric",
                          month: "short",
                        })
                      : "—"}
                  </span>
                  <span className="profile-stat-label">Last booked</span>
                </div>
              </div>
              <Link to="/bookings" className="btn btn-outline">
                View my bookings &rarr;
              </Link>
            </section>
          )}

          <section className="profile-card">
            <h2 className="profile-card-title">Your avatar</h2>
            <div className="avatar-picker" role="radiogroup" aria-label="Avatar">
              {AVATAR_OPTIONS.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  role="radio"
                  aria-checked={avatar === option.value}
                  className={`avatar-option ${avatar === option.value ? "is-selected" : ""}`}
                  onClick={() => setAvatar(option.value)}
                >
                  <span className="avatar-option-img">
                    <Avatar email={profile.email} choice={option.value} />
                  </span>
                  <span className="avatar-option-label">{option.label}</span>
                </button>
              ))}
            </div>
          </section>

          <section className="profile-card">
            <h2 className="profile-card-title">Appearance</h2>
            <div className="theme-segment" role="radiogroup" aria-label="Theme">
              {["light", "dark"].map((option) => (
                <button
                  key={option}
                  type="button"
                  role="radio"
                  aria-checked={theme === option}
                  className={`theme-segment-option ${theme === option ? "is-selected" : ""}`}
                  onClick={() => setTheme(option)}
                >
                  {option === "light" ? "☀ Light" : "☾ Dark"}
                </button>
              ))}
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
