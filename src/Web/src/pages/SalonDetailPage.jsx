import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "../api/client";
import { useAuth } from "../context/AuthContext";

export function SalonDetailPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [salon, setSalon] = useState(null);
  const [error, setError] = useState(null);
  const [selectedServiceId, setSelectedServiceId] = useState("");
  const [bookingDate, setBookingDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [slots, setSlots] = useState([]);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [slotsError, setSlotsError] = useState(null);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [bookingMessage, setBookingMessage] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [deleteError, setDeleteError] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editName, setEditName] = useState("");
  const [editAddress, setEditAddress] = useState("");
  const [editError, setEditError] = useState(null);
  const [savingEdit, setSavingEdit] = useState(false);
  const [editingServiceId, setEditingServiceId] = useState(null);
  const [editServiceName, setEditServiceName] = useState("");
  const [editServicePrice, setEditServicePrice] = useState("");
  const [serviceError, setServiceError] = useState(null);
  const [savingServiceEdit, setSavingServiceEdit] = useState(false);
  const [deletingServiceId, setDeletingServiceId] = useState(null);

  const canManage = user?.role === "Admin" || user?.role === "Staff";

  useEffect(() => {
    api
      .getSalon(id)
      .then(setSalon)
      .catch((err) => setError(err.message));
  }, [id]);

  useEffect(() => {
    if (!selectedServiceId || !bookingDate) {
      setSlots([]);
      return;
    }
    setSelectedSlot(null);
    setSlotsError(null);
    setLoadingSlots(true);
    api
      .getAvailability(selectedServiceId, bookingDate)
      .then(setSlots)
      .catch((err) => setSlotsError(err.message))
      .finally(() => setLoadingSlots(false));
  }, [selectedServiceId, bookingDate]);

  function startEditing() {
    setEditName(salon.name);
    setEditAddress(salon.address);
    setEditError(null);
    setEditing(true);
  }

  async function handleSaveEdit(e) {
    e.preventDefault();
    setEditError(null);
    setSavingEdit(true);
    try {
      const updated = await api.updateSalon(id, { name: editName, address: editAddress });
      setSalon(updated);
      setEditing(false);
    } catch (err) {
      setEditError(err.message);
    } finally {
      setSavingEdit(false);
    }
  }

  async function handleDelete() {
    if (!window.confirm(`Delete "${salon.name}"? This cannot be undone.`)) {
      return;
    }
    setDeleteError(null);
    setDeleting(true);
    try {
      await api.deleteSalon(id);
      navigate("/salons");
    } catch (err) {
      setDeleteError(err.message);
      setDeleting(false);
    }
  }

  function startEditingService(service) {
    setEditingServiceId(service.id);
    setEditServiceName(service.name);
    setEditServicePrice(service.price);
    setServiceError(null);
  }

  async function handleSaveServiceEdit(serviceId) {
    setServiceError(null);
    setSavingServiceEdit(true);
    try {
      const updated = await api.updateService(id, serviceId, {
        name: editServiceName,
        price: Number(editServicePrice),
      });
      setSalon((prev) => ({
        ...prev,
        services: prev.services.map((s) => (s.id === serviceId ? { ...s, ...updated } : s)),
      }));
      setEditingServiceId(null);
    } catch (err) {
      setServiceError(err.message);
    } finally {
      setSavingServiceEdit(false);
    }
  }

  async function handleDeleteService(serviceId) {
    setServiceError(null);
    setDeletingServiceId(serviceId);
    try {
      await api.deleteService(id, serviceId);
      setSalon((prev) => ({
        ...prev,
        services: prev.services.filter((s) => s.id !== serviceId),
      }));
    } catch (err) {
      setServiceError(err.message);
    } finally {
      setDeletingServiceId(null);
    }
  }

  async function handleBook(e) {
    e.preventDefault();
    if (!selectedSlot) return;
    setBookingMessage(null);
    setSubmitting(true);

    try {
      await api.createBooking({
        salonId: id,
        serviceId: selectedServiceId,
        startTime: selectedSlot.startTime,
        endTime: selectedSlot.endTime,
      });
      setBookingMessage({ type: "success", text: "Booked!" });
      setSelectedSlot(null);
      const updated = await api.getAvailability(selectedServiceId, bookingDate);
      setSlots(updated);
    } catch (err) {
      setBookingMessage({ type: "error", text: err.message });
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <p className="message message-error">{error}</p>;
  if (!salon) return <p className="state-msg">Loading&hellip;</p>;

  return (
    <div className="page">
      <div className="page-head">
        {salon.imageUrl && (
          <img src={salon.imageUrl} alt={salon.name} className="salon-detail-image" />
        )}
        <span className="label">[ Salon ]</span>
        {editing ? (
          <form onSubmit={handleSaveEdit} className="admin-form">
            <div className="field">
              <label>Name</label>
              <input value={editName} onChange={(e) => setEditName(e.target.value)} required />
            </div>
            <div className="field">
              <label>Address</label>
              <input value={editAddress} onChange={(e) => setEditAddress(e.target.value)} required />
            </div>
            {editError && <p className="message message-error">{editError}</p>}
            <button type="submit" className="btn" disabled={savingEdit}>
              {savingEdit ? "Saving…" : "Save changes"}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setEditing(false)}>
              Cancel
            </button>
          </form>
        ) : (
          <>
            <h1>{salon.name}</h1>
            <p className="salon-detail-address">{salon.address}</p>
          </>
        )}
        {canManage && !editing && (
          <>
            <button type="button" className="btn btn-outline" onClick={startEditing}>
              Edit salon
            </button>
            <button
              type="button"
              className="btn btn-danger"
              onClick={handleDelete}
              disabled={deleting}
            >
              {deleting ? "Deleting…" : "Delete salon"}
            </button>
            {deleteError && <p className="message message-error">{deleteError}</p>}
          </>
        )}
      </div>

      <section className="detail-section">
        <h2>Services</h2>
        {salon.services.length === 0 && <p className="state-msg">No services listed.</p>}
        {serviceError && <p className="message message-error">{serviceError}</p>}
        {salon.services.length > 0 && (
          <ul className="service-list">
            {salon.services.map((service) =>
              editingServiceId === service.id ? (
                <li key={service.id} className="service-row">
                  <input
                    value={editServiceName}
                    onChange={(e) => setEditServiceName(e.target.value)}
                  />
                  <input
                    type="number"
                    value={editServicePrice}
                    onChange={(e) => setEditServicePrice(e.target.value)}
                    min="0"
                    step="0.01"
                  />
                  <button
                    type="button"
                    className="btn"
                    disabled={savingServiceEdit}
                    onClick={() => handleSaveServiceEdit(service.id)}
                  >
                    {savingServiceEdit ? "Saving…" : "Save"}
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost"
                    onClick={() => setEditingServiceId(null)}
                  >
                    Cancel
                  </button>
                </li>
              ) : (
                <li key={service.id} className="service-row">
                  <span className="service-name">{service.name}</span>
                  <span className="service-meta label">{service.durationMinutes} min</span>
                  <span className="service-price dot-index">₹{service.price}</span>
                  {canManage && (
                    <>
                      <button
                        type="button"
                        className="btn btn-outline"
                        onClick={() => startEditingService(service)}
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        className="btn btn-danger service-remove"
                        disabled={deletingServiceId === service.id}
                        onClick={() => handleDeleteService(service.id)}
                      >
                        {deletingServiceId === service.id ? "Removing…" : "Remove"}
                      </button>
                    </>
                  )}
                </li>
              )
            )}
          </ul>
        )}
      </section>

      {user?.role !== "Admin" && (
      <section className="detail-section">
        <h2>Book an appointment</h2>
        {user ? (
          <form onSubmit={handleBook} className="booking-form">
            <div className="field">
              <label>Service</label>
              <select
                value={selectedServiceId}
                onChange={(e) => setSelectedServiceId(e.target.value)}
                required
              >
                <option value="">-- choose --</option>
                {salon.services.map((service) => (
                  <option key={service.id} value={service.id}>
                    {service.name} (60 min)
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Date</label>
              <input
                type="date"
                value={bookingDate}
                min={new Date().toISOString().slice(0, 10)}
                onChange={(e) => setBookingDate(e.target.value)}
                required
              />
            </div>

            {selectedServiceId && (
              <div className="field">
                <label>Time slot</label>
                {loadingSlots && <p className="state-msg">Loading slots&hellip;</p>}
                {slotsError && <p className="message message-error">{slotsError}</p>}
                {!loadingSlots && !slotsError && (
                  <div className="slot-grid">
                    {slots.map((slot) => {
                      const isSelected = selectedSlot?.startTime === slot.startTime;
                      return (
                        <button
                          key={slot.startTime}
                          type="button"
                          className={`btn ${isSelected ? "" : "btn-outline"}`}
                          disabled={slot.booked}
                          onClick={() => setSelectedSlot(slot)}
                        >
                          {new Date(slot.startTime).toLocaleTimeString([], {
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                          {slot.booked ? " — Booked" : ""}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

            {bookingMessage && (
              <p className={`message ${bookingMessage.type === "error" ? "message-error" : "message-success"}`}>
                {bookingMessage.text}
              </p>
            )}
            <button type="submit" className="btn" disabled={!selectedSlot || submitting}>
              {submitting ? "Booking…" : "Book"}
            </button>
          </form>
        ) : (
          <p className="state-msg">Log in to book an appointment.</p>
        )}
      </section>
      )}
    </div>
  );
}
