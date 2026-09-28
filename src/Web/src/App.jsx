import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import { ToastProvider } from "./context/ToastContext";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { Layout } from "./components/Layout";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { SalonsPage } from "./pages/SalonsPage";
import { SalonDetailPage } from "./pages/SalonDetailPage";
import { MyBookingsPage } from "./pages/MyBookingsPage";
import { AdminCreateSalonPage } from "./pages/AdminCreateSalonPage";
import { ProfilePage } from "./pages/ProfilePage";
import { ThemeProvider } from "./context/ThemeContext";
import { AvatarProvider } from "./context/AvatarContext";

export default function App() {
  return (
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <AvatarProvider>
            <ToastProvider>
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
                  <Route
                    path="/profile"
                    element={
                      <ProtectedRoute>
                        <ProfilePage />
                      </ProtectedRoute>
                    }
                  />
                  <Route path="/" element={<SalonsPage />} />
                </Route>
              </Routes>
            </ToastProvider>
          </AvatarProvider>
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}
