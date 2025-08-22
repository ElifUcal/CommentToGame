import React from "react";
import "../../styles/components/_header.scss";

const Header = () => {
  return (
    <header className="ggdb-header">
      <div className="ggdb-header__inner">
        {/* Left: Logo */}
        <a href="/" className="ggdb-header__logo" aria-label="GGDB Home">
          GGDB
        </a>

        {/* Left: Menu */}
        <button className="ggdb-header__menu" aria-label="Open menu">
          <span className="ggdb-header__menuIcon" aria-hidden="true">
            {/* hamburger */}
            <svg width="18" height="14" viewBox="0 0 18 14" fill="none">
              <rect x="0" y="0" width="18" height="2" rx="1" />
              <rect x="0" y="6" width="18" height="2" rx="1" />
              <rect x="0" y="12" width="18" height="2" rx="1" />
            </svg>
          </span>
          <span className="ggdb-header__menuText">MENU</span>
        </button>

        <div className="ggdb-header__spacer" />

        {/* Right: Search / Auth */}
        <nav className="ggdb-header__right" aria-label="Primary">
          <button className="ggdb-header__search" aria-label="Search">
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <circle cx="11" cy="11" r="7" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
          </button>

          <a href="/login" className="ggdb-header__link">
            Sign In
          </a>

          <a href="/register" className="ggdb-header__cta">
            Sign Up
          </a>
        </nav>
      </div>
    </header>
  );
};

export default Header;
