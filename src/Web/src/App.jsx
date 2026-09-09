import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { Layout } from "./components/Layout";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { SalonsPage } from "./pages/SalonsPage";
import { SalonDetailPage } from "./pages/SalonDetailPage";
import { MyBookingsPage } from "./pages/MyBookingsPage";
import { AdminCreateSalonPage } from "./pages/AdminCreateSalonPage";

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/salons" element={<SalonsPage />} />
            <Route path="/salons/:id" element={<SalonDetailPage />} />
            <Route
              path="/bookings"
              element={
                <ProtectedRoute roles={["Customer", "Staff"]}>
                  <MyBookingsPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/admin/salons/new"
              element={
                <ProtectedRoute roles={["Staff", "Admin"]}>
                  <AdminCreateSalonPage />
                </ProtectedRoute>
              }
            />
            <Route path="/" element={<SalonsPage />} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
