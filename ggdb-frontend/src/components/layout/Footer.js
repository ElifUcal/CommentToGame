import React from "react";
import "../../styles/components/_footer.scss";

const Footer = () => {
  return (
    <footer className="ggdb-footer">
      <div className="ggdb-footer__top">
        {/* Social box */}
        <div className="ggdb-footer__card">
          <div className="ggdb-footer__cardTitle">Follow GGDB on social</div>
          <div className="ggdb-footer__icons">
            {/* TikTok */}
            <a href="#" aria-label="TikTok">
              <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                <path d="M20 8.1a6.8 6.8 0 0 1-4.2-1.5V16a6 6 0 1 1-6-6c.3 0 .6 0 .9.1v3A3 3 0 1 0 12 16V2h3a6.8 6.8 0 0 0 5 2.3z"/>
              </svg>
            </a>
            {/* Instagram */}
            <a href="#" aria-label="Instagram">
              <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2">
                <rect x="3" y="3" width="18" height="18" rx="5"/><circle cx="12" cy="12" r="3.5"/><circle cx="17.5" cy="6.5" r="1.2" fill="currentColor" stroke="none"/>
              </svg>
            </a>
            {/* X */}
            <a href="#" aria-label="X">
              <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                <path d="M3 3h3.3l5.2 7.1L15.8 3H21l-7.2 9.8L21 21h-3.2l-6-8.1L8 21H3l7.6-10.3z"/>
              </svg>
            </a>
            {/* YouTube */}
            <a href="#" aria-label="YouTube">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor">
                <path d="M23 12s0-3.6-.5-5.2c-.3-1-1-1.7-2-2-1.8-.5-8.5-.5-8.5-.5s-6.7 0-8.5.5c-1 .3-1.7 1-2 2C1 8.4 1 12 1 12s0 3.6.5 5.2c.3 1 1 1.7 2 2 1.8.5 8.5.5 8.5.5s6.7 0 8.5-.5c1-.3 1.7-1 2-2 .5-1.6.5-5.2.5-5.2zM10 15.5v-7l6 3.5-6 3.5z"/>
              </svg>
            </a>
            {/* Facebook */}
            <a href="#" aria-label="Facebook">
              <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                <path d="M13 22v-8h3l1-4h-4V7.5c0-1.1.3-1.8 1.9-1.8H17V2.2C16.4 2.1 15.3 2 14 2c-3 0-5 1.8-5 5.2V10H6v4h3v8h4z"/>
              </svg>
            </a>
          </div>
        </div>

        {/* App box */}
        <div className="ggdb-footer__card">
          <div className="ggdb-footer__cardTitle">Get the GGDB app</div>
          <div className="ggdb-footer__sub">For Android and iOS</div>
        </div>
      </div>

      {/* Links row */}
      <nav className="ggdb-footer__links" aria-label="Footer">
        {[
          "Help","Site Index","GGDB Pro","API","Press Room","Advertising","Jobs",
          "Conditions of Use","Privacy Policy","Your Ads Privacy Choices"
        ].map((t,i)=>(
          <a key={i} href="#" className="ggdb-footer__link">{t}</a>
        ))}
      </nav>

      {/* Brand bottom */}
      <div className="ggdb-footer__brand">
        <div className="ggdb-footer__logo">GGDB</div>
        <div className="ggdb-footer__tag">The Good game database</div>
      </div>
    </footer>
  );
};

export default Footer;
