import React, { useState } from "react";
import API from "../services/api.js";            // <-- kısa axios instance (baseURL:/api)
import { useNavigate } from "react-router-dom";
import "../styles/pages/_login.scss";

export default function Login() {
  const [showPwd, setShowPwd] = useState(false);
  const [emailOrUser, setEmailOrUser] = useState("");
  const [password, setPassword]       = useState("");
  const [loading, setLoading]         = useState(false);
  const [error, setError]             = useState(null);

  const navigate = useNavigate();

  const onSubmit = async (e) => {
    e.preventDefault();
    if (loading) return;
    setError(null);
    setLoading(true);

    try {
      // Backend UserName bekliyor; front'ta email alanı var.
      // Şimdilik inputu userName olarak yolluyoruz (email de yazılsa kabul).
      const res = await API.post("/auth/login", {
        userName: emailOrUser.trim(),
        password: password,
      });

      const { token, refreshToken } = res.data || {};
      if (!token) throw new Error("Token alınamadı.");

      // Basit hali: token'ı localStorage'a yaz (istersen sessionStorage kullanabilirsin)
      localStorage.setItem("token", token);
      if (refreshToken) localStorage.setItem("refresh_token", refreshToken);

      navigate("/chat"); // girişten sonra yönlenecek sayfan
    } catch (err) {
      const msg = err?.response?.data || err?.message || "Login failed.";
      setError(typeof msg === "string" ? msg : "Login failed.");
    } finally {
      setLoading(false);
    }
  };

  return (

    <div className="login-page">
        <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"></link>
      {/* Background */}
      <div
        className="login-bg"
        style={{
          backgroundImage:
            'url("https://images.unsplash.com/photo-1511512578047-dfb367046420?q=80&w=2071&auto=format&fit=crop")',
        }}
      >
        <div className="login-overlay" />
      </div>

      <div className="login-card">
        <h2 className="login-title">Welcome back!</h2>
        <p className="login-sub">
          Log in to access your game library and community!
        </p>

        <form className="login-form" onSubmit={onSubmit}>
          {error && (
            <div className="login-error" role="alert">
              {error}
            </div>
          )}

          <input
            type="text"
            placeholder="Username"
            className="login-input"
            value={emailOrUser}
            onChange={(e) => setEmailOrUser(e.target.value)}
            required
            autoComplete="username"
          />

          <div className="login-password">
            <input
              type={showPwd ? "text" : "password"}
              placeholder="Password"
              className="login-input"
              aria-label="Password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete="current-password"
            />
            <button
              type="button"
              className="login-eyeBtn"
              aria-label={showPwd ? "Hide password" : "Show password"}
              onClick={() => setShowPwd((p) => !p)}
              disabled={loading}
            >
              {showPwd ? (
                // eye-off svg
                <svg
                  width="20"
                  height="20"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M17.94 17.94A10.94 10.94 0 0 1 12 20C7 20 2.73 16.11 1 12c.62-1.44 1.52-2.76 2.64-3.88M9.88 9.88a3 3 0 0 0 4.24 4.24M1 1l22 22" />
                </svg>
              ) : (
                // eye svg
                <svg
                  width="20"
                  height="20"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8S1 12 1 12Z" />
                  <circle cx="12" cy="12" r="3" />
                </svg>
              )}
            </button>
          </div>

          <button type="submit" className="login-btn" disabled={loading}>
            {loading ? "Signing in..." : "Login"}
          </button>

          {/* Social login - placeholder */}
    
       <div className="login-social">
    <span tabIndex={0}>
      <button type="button" className="social-button google">
        <i className="social-icon fab fa-google"></i>
      </button>
    </span>

    <span tabIndex={0}>
      <button type="button" className="social-button steam">
        <i className="social-icon fab fa-steam"></i>
      </button>
    </span>

    <span tabIndex={0}>
      <button type="button" className="social-button discord">
        <i className="social-icon fab fa-discord"></i>
      </button>
    </span>

    <span tabIndex={0}>
      <button type="button" className="social-button epic">
        <i className="social-icon fas fa-gamepad"></i>
      </button>
    </span>

    <span tabIndex={0}>
      <button type="button" className="social-button ea">
        <i className="social-icon fas fa-headset"></i>
      </button>
    </span>

    <span tabIndex={0}>
      <button type="button" className="social-button microsoft">
        <i className="social-icon fab fa-microsoft"></i>
      </button>
    </span>
  </div>


          <div className="login-divider">OR</div>

          <div className="login-extra">
            <p>
              Don’t have an account? <a href="/register">Create one</a>
            </p>
            <a href="/forgot" className="forgot">
              Forgot your password?
            </a>
          </div>
        </form>
      </div>
    </div>
  );
}
