import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useAvatar } from "../context/AvatarContext";
import { Avatar } from "./Avatar";

function SunIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

export function Layout() {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const { avatar } = useAvatar();
  const navigate = useNavigate();
  const location = useLocation();

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <>
      <header className="site-header">
        <div className="container site-header-inner">
          <Link to="/salons" className="wordmark">
            Aurelia<span className="wordmark-thin">Salon</span>
          </Link>

          <nav className="site-nav">
            <Link to="/salons" className="site-nav-link">
              Salons
            </Link>
            {user && user.role !== "Admin" && (
              <Link to="/bookings" className="site-nav-link">
                My Bookings
              </Link>
            )}
            {(user?.role === "Staff" || user?.role === "Admin") && (
              <Link to="/admin/salons/new" className="site-nav-link">
                New Salon
              </Link>
            )}
          </nav>

          <div className="site-account">
            <button
              type="button"
              className="theme-toggle"
              onClick={toggleTheme}
              aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
              title={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
            >
              {theme === "dark" ? <SunIcon /> : <MoonIcon />}
            </button>
            {user ? (
              <>
                <Link
                  to="/profile"
                  className={`profile-chip ${location.pathname === "/profile" ? "is-current" : ""}`}
                  aria-label="View profile"
                >
                  <span className="profile-chip-avatar">
                    <span className="profile-chip-ring" aria-hidden="true" />
                    <span className="profile-chip-initials">
                      <Avatar email={user.email} choice={avatar} />
                    </span>
                    <span className="profile-chip-dot" aria-hidden="true" />
                  </span>
                  <span className="profile-chip-text">
                    <span className="profile-chip-email">{user.email}</span>
                    <span className="profile-chip-role">{user.role} &middot; View profile</span>
                  </span>
                  <span className="profile-chip-arrow" aria-hidden="true">&rarr;</span>
                </Link>
                <button onClick={handleLogout} className="btn btn-ghost">
                  Logout
                </button>
              </>
            ) : (
              <Link to="/login" className="btn btn-outline">
                Login
              </Link>
            )}
          </div>
        </div>
      </header>

      <main className="site-main">
        <div className="container">
          <Outlet />
        </div>
      </main>

      <footer className="site-footer">
        <div className="container site-footer-inner">
          <span className="label">Aurelia Salon &copy; {new Date().getFullYear()}</span>
          <span className="label">Crafted for your finest moments</span>
        </div>
      </footer>
    </>
  );
}
