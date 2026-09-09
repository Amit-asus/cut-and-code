const API_URL = import.meta.env.VITE_API_URL;

function getToken() {
  return localStorage.getItem("token");
}

async function request(path, options = {}) {
  const token = getToken();
  const headers = {
    ...(options.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  };

  const response = await fetch(`${API_URL}${path}`, { ...options, headers });

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const message = typeof data === "string" ? data : data?.title || response.statusText;
    throw new Error(message || `Request failed with ${response.status}`);
  }

  return data;
}

export const api = {
  register: (email, password) =>
    request("/identity/register", { method: "POST", body: JSON.stringify({ email, password }) }),

  login: (email, password) =>
    request("/identity/login", { method: "POST", body: JSON.stringify({ email, password }) }),

  me: () => request("/identity/me"),

  listSalons: () => request("/salons"),

  getSalon: (id) => request(`/salons/${id}`),

  createSalon: (salon) => request("/salons", { method: "POST", body: JSON.stringify(salon) }),

  updateSalon: (id, salon) => request(`/salons/${id}`, { method: "PUT", body: JSON.stringify(salon) }),

  deleteSalon: (id) => request(`/salons/${id}`, { method: "DELETE" }),

  uploadSalonImage: (id, file) => {
    const formData = new FormData();
    formData.append("file", file);
    return request(`/salons/${id}/image`, { method: "POST", body: formData });
  },

  createService: (salonId, service) =>
    request(`/salons/${salonId}/services`, { method: "POST", body: JSON.stringify(service) }),

  updateService: (salonId, serviceId, service) =>
    request(`/salons/${salonId}/services/${serviceId}`, { method: "PUT", body: JSON.stringify(service) }),

  deleteService: (salonId, serviceId) =>
    request(`/salons/${salonId}/services/${serviceId}`, { method: "DELETE" }),

  listBookings: () => request("/bookings"),

  getAvailability: (serviceId, date) =>
    request(`/bookings/availability?serviceId=${serviceId}&date=${date}`),

  createBooking: (booking) => request("/bookings", { method: "POST", body: JSON.stringify(booking) }),

  cancelBooking: (id) => request(`/bookings/${id}`, { method: "DELETE" }),
};
