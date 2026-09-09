import { Link, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <>
      <header className="site-header">
        <div className="container site-header-inner">
          <Link to="/salons" className="wordmark">
            <span className="wordmark-dot" />
            SALON<span className="wordmark-thin">BOOKING</span>
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
            {user ? (
              <>
                <span className="account-badge">
                  <span className="label">{user.role}</span>
                  {user.email}
                </span>
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
          <span className="label">SalonBooking &copy; {new Date().getFullYear()}</span>
          <span className="label">Book local. Book fast.</span>
        </div>
      </footer>
    </>
  );
}
