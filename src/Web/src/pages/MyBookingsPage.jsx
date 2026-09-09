import { useEffect, useState } from "react";
import { api } from "../api/client";

const STATUS_LABELS = { 0: "Confirmed", 1: "Cancelled" };

export function MyBookingsPage() {
  const [bookings, setBookings] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  function load() {
    setLoading(true);
    api
      .listBookings()
      .then(setBookings)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  async function handleCancel(id) {
    try {
      await api.cancelBooking(id);
      load();
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <div className="page">
      <div className="page-head">
        <span className="label">[ 02 ] Account</span>
        <h1>My Bookings</h1>
      </div>

      {error && <p className="message message-error">{error}</p>}
      {loading && <p className="state-msg">Loading&hellip;</p>}

      {!loading && bookings.length === 0 && <p className="state-msg">No bookings yet.</p>}

      {!loading && bookings.length > 0 && (
        <ul className="booking-list">
          {bookings.map((booking) => {
            const isConfirmed = booking.status === 0;
            return (
              <li key={booking.id} className="booking-row">
                <span
                  className={`booking-status label ${isConfirmed ? "booking-status-confirmed" : "booking-status-cancelled"}`}
                >
                  {STATUS_LABELS[booking.status] ?? booking.status}
                </span>
                <span className="booking-time">
                  {new Date(booking.startTime).toLocaleString()} &ndash;{" "}
                  {new Date(booking.endTime).toLocaleString()}
                </span>
                {isConfirmed && (
                  <button onClick={() => handleCancel(booking.id)} className="btn btn-danger booking-cancel">
                    Cancel
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
