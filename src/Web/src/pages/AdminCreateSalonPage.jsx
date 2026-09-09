import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api/client";
import { useAuth } from "../context/AuthContext";

const SERVICE_DURATION_MINUTES = 60;

const SERVICE_PRESETS = [
  { name: "Full Service", durationMinutes: SERVICE_DURATION_MINUTES, price: 40 },
];

export function AdminCreateSalonPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [name, setName] = useState("");
  const [address, setAddress] = useState("");
  const [error, setError] = useState(null);
  const [creating, setCreating] = useState(false);

  const [salon, setSalon] = useState(null);
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [uploadError, setUploadError] = useState(null);
  const [uploading, setUploading] = useState(false);

  const [services, setServices] = useState([]);
  const [serviceName, setServiceName] = useState("");
  const serviceDuration = SERVICE_DURATION_MINUTES;
  const [servicePrice, setServicePrice] = useState("");
  const [serviceError, setServiceError] = useState(null);
  const [addingService, setAddingService] = useState(false);

  const [editingServiceId, setEditingServiceId] = useState(null);
  const [editServiceName, setEditServiceName] = useState("");
  const [editServicePrice, setEditServicePrice] = useState("");
  const [savingServiceEdit, setSavingServiceEdit] = useState(false);

  async function handleCreate(e) {
    e.preventDefault();
    setError(null);
    setCreating(true);
    try {
      const created = await api.createSalon({ name, address, ownerId: user.id });
      setSalon(created);
      setServices(created.services ?? []);
    } catch (err) {
      setError(err.message);
    } finally {
      setCreating(false);
    }
  }

  function handleFileChange(e) {
    const file = e.target.files?.[0] ?? null;
    setImageFile(file);
    setImagePreview(file ? URL.createObjectURL(file) : null);
  }

  async function handleUpload(e) {
    e.preventDefault();
    if (!imageFile) return;
    setUploadError(null);
    setUploading(true);
    try {
      const updated = await api.uploadSalonImage(salon.id, imageFile);
      setSalon(updated);
    } catch (err) {
      setUploadError(err.message);
    } finally {
      setUploading(false);
    }
  }

  function applyPreset(preset) {
    setServiceName(preset.name);
    setServicePrice(preset.price);
  }

  async function handleAddService(e) {
    e.preventDefault();
    setServiceError(null);
    setAddingService(true);
    try {
      const created = await api.createService(salon.id, {
        name: serviceName,
        durationMinutes: Number(serviceDuration),
        price: Number(servicePrice),
      });
      setServices((prev) => [...prev, created]);
      setServiceName("");
      setServicePrice("");
    } catch (err) {
      setServiceError(err.message);
    } finally {
      setAddingService(false);
    }
  }

  async function handleDeleteService(serviceId) {
    try {
      await api.deleteService(salon.id, serviceId);
      setServices((prev) => prev.filter((s) => s.id !== serviceId));
    } catch (err) {
      setServiceError(err.message);
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
      const updated = await api.updateService(salon.id, serviceId, {
        name: editServiceName,
        price: Number(editServicePrice),
      });
      setServices((prev) => prev.map((s) => (s.id === serviceId ? updated : s)));
      setEditingServiceId(null);
    } catch (err) {
      setServiceError(err.message);
    } finally {
      setSavingServiceEdit(false);
    }
  }

  return (
    <div className="page">
      <div className="page-head">
        <span className="label">[ Admin ] New Salon</span>
        <h1>Create a Salon</h1>
      </div>

      <ol className="step-tracker">
        <li className={`step-tracker-item ${!salon ? "is-active" : "is-done"}`}>
          <span className="step-tracker-index dot-index">{salon ? "✓" : "01"}</span>
          Details
        </li>
        <li className={`step-tracker-item ${salon ? "is-active" : ""}`}>
          <span className="step-tracker-index dot-index">02</span>
          Photo
        </li>
        <li className={`step-tracker-item ${salon ? "is-active" : ""}`}>
          <span className="step-tracker-index dot-index">03</span>
          Services
        </li>
      </ol>

      {!salon ? (
        <form onSubmit={handleCreate} className="admin-form">
          <div className="field">
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="field">
            <label>Address</label>
            <input value={address} onChange={(e) => setAddress(e.target.value)} required />
          </div>
          {error && <p className="message message-error">{error}</p>}
          <button type="submit" className="btn" disabled={creating}>
            {creating ? "Creating…" : "Create salon"}
          </button>
          <p className="state-msg">You&rsquo;ll add a photo and services in the next steps, once the salon is created.</p>
        </form>
      ) : (
        <>
          <p className="message message-success">
            &ldquo;{salon.name}&rdquo; created. Add a photo and services below, or{" "}
            <button type="button" className="btn-link" onClick={() => navigate(`/salons/${salon.id}`)}>
              go to the salon
            </button>
            .
          </p>

          <section className="detail-section">
            <h2>Salon photo</h2>
            <form onSubmit={handleUpload} className="admin-form">
              {(imagePreview || salon.imageUrl) && (
                <img
                  src={imagePreview ?? salon.imageUrl}
                  alt={salon.name}
                  className="salon-image-preview"
                />
              )}
              <div className="field">
                <label>Image file</label>
                <input type="file" accept="image/*" onChange={handleFileChange} />
              </div>
              {uploadError && <p className="message message-error">{uploadError}</p>}
              <button type="submit" className="btn" disabled={!imageFile || uploading}>
                {uploading ? "Uploading…" : "Upload photo"}
              </button>
            </form>
          </section>

          <section className="detail-section">
            <h2>Services</h2>

            {services.length > 0 && (
              <ul className="service-list">
                {services.map((service) =>
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
                        onClick={() => handleDeleteService(service.id)}
                      >
                        Remove
                      </button>
                    </li>
                  )
                )}
              </ul>
            )}

            <div className="preset-row">
              {SERVICE_PRESETS.map((preset) => (
                <button
                  key={preset.name}
                  type="button"
                  className="btn btn-outline"
                  onClick={() => applyPreset(preset)}
                >
                  {preset.name}
                </button>
              ))}
            </div>

            <form onSubmit={handleAddService} className="admin-form service-form">
              <div className="field">
                <label>Name</label>
                <input value={serviceName} onChange={(e) => setServiceName(e.target.value)} required />
              </div>
              <div className="field">
                <label>Duration (minutes)</label>
                <input type="number" value={serviceDuration} readOnly disabled />
              </div>
              <div className="field">
                <label>Price</label>
                <input
                  type="number"
                  value={servicePrice}
                  onChange={(e) => setServicePrice(e.target.value)}
                  min="0"
                  step="0.01"
                  required
                />
              </div>
              {serviceError && <p className="message message-error">{serviceError}</p>}
              <button type="submit" className="btn" disabled={addingService}>
                {addingService ? "Adding…" : "Add service"}
              </button>
            </form>
          </section>
        </>
      )}
    </div>
  );
}
