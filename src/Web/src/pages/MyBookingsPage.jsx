import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import { ConfirmModal } from "../components/ConfirmModal";

const STATUS_LABELS = { 0: "Confirmed", 1: "Cancelled" };
const PAGE_SIZE = 5;

export function MyBookingsPage() {
  const [bookings, setBookings] = useState([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [salonsById, setSalonsById] = useState({});
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [bookingToCancel, setBookingToCancel] = useState(null);
  const [cancelling, setCancelling] = useState(false);

  const load = useCallback(async (pageToLoad) => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.listBookings(pageToLoad, PAGE_SIZE);

      if (result.items.length === 0 && pageToLoad > 1) {
        setPage(pageToLoad - 1);
        return;
      }

      setBookings(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);

      const salonIds = [...new Set(result.items.map((b) => b.salonId))];
      const salons = await Promise.all(salonIds.map((id) => api.getSalon(id).catch(() => null)));
      setSalonsById((prev) => {
        const next = { ...prev };
        salonIds.forEach((id, i) => {
          if (salons[i]) next[id] = salons[i];
        });
        return next;
      });
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load(page);
  }, [page, load]);

  async function handleConfirmCancel() {
    setCancelling(true);
    try {
      await api.cancelBooking(bookingToCancel.id);
      setBookingToCancel(null);
      load(page);
    } catch (err) {
      setBookingToCancel(null);
      setError(err.message);
    } finally {
      setCancelling(false);
    }
  }

  function describeBooking(booking) {
    if (!booking) return "";
    const salon = salonsById[booking.salonId];
    const service = salon?.services?.find((s) => s.id === booking.serviceId);
    const what = service && salon ? `${service.name} at ${salon.name}` : "this booking";
    return `${what} on ${new Date(booking.startTime).toLocaleString()}`;
  }

  return (
    <div className="page">
      <div className="page-head">
        <span className="label">Account</span>
        <h1>My Bookings</h1>
      </div>

      {error && <p className="message message-error">{error}</p>}
      {loading && <p className="state-msg">Loading&hellip;</p>}

      {!loading && totalCount === 0 && <p className="state-msg">No bookings yet.</p>}

      {!loading && bookings.length > 0 && (
        <>
          <ul className="booking-list">
            {bookings.map((booking) => {
              const isConfirmed = booking.status === 0;
              const salon = salonsById[booking.salonId];
              const service = salon?.services?.find((s) => s.id === booking.serviceId);
              return (
                <li key={booking.id} className={`booking-row ${isConfirmed ? "" : "is-cancelled"}`}>
                  {salon ? (
                    <Link to={`/salons/${salon.id}`} className="booking-thumb" aria-label={`View ${salon.name}`}>
                      {salon.imageUrl ? (
                        <img src={salon.imageUrl} alt="" loading="lazy" />
                      ) : (
                        <span className="booking-thumb-fallback">{salon.name[0]?.toUpperCase()}</span>
                      )}
                    </Link>
                  ) : (
                    <span className="booking-thumb">
                      <span className="booking-thumb-fallback">?</span>
                    </span>
                  )}
                  <div className="booking-info">
                    <div className="booking-info-head">
                      <span className="booking-service">{service?.name ?? "Unknown service"}</span>
                      <span
                        className={`booking-status label ${isConfirmed ? "booking-status-confirmed" : "booking-status-cancelled"}`}
                      >
                        {STATUS_LABELS[booking.status] ?? booking.status}
                      </span>
                    </div>
                    <span className="booking-salon">
                      {salon ? <Link to={`/salons/${salon.id}`}>{salon.name}</Link> : "Unknown salon"}
                      {service && <> &middot; ₹{service.price}</>}
                    </span>
                    <span className="booking-time">
                      {new Date(booking.startTime).toLocaleString()} &ndash;{" "}
                      {new Date(booking.endTime).toLocaleTimeString()}
                    </span>
                    <span className="booking-created">
                      Booked on {new Date(booking.createdAt).toLocaleString()}
                    </span>
                  </div>
                  {isConfirmed && (
                    <button onClick={() => setBookingToCancel(booking)}className="btn btn-danger booking-cancel">
                      Cancel
                    </button>
                  )}
                </li>
              );
            })}
          </ul>

          {totalPages > 1 && (
            <nav className="pagination">
              <button
                type="button"
                className="btn btn-outline"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                &larr; Previous
              </button>
              <span className="pagination-info label">
                Page {page} of {totalPages} &middot; {totalCount} bookings
              </span>
              <button
                type="button"
                className="btn btn-outline"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next &rarr;
              </button>
            </nav>
          )}
        </>
      )}

      <ConfirmModal
        open={bookingToCancel !== null}
        title="Cancel booking?"
        message={`Are you sure you want to cancel ${describeBooking(bookingToCancel)}? This can't be undone.`}
        confirmLabel="Yes, cancel booking"
        cancelLabel="Keep booking"
        busy={cancelling}
        onConfirm={handleConfirmCancel}
        onCancel={() => setBookingToCancel(null)}
      />
    </div>
  );
}
