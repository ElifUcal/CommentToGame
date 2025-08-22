import React from 'react';
import { Card } from 'react-bootstrap';

const ContactInfo = () => {
  return (
    <div className="contact-info">
      <Card>
        <Card.Body>
          <h3 className="mb-4">Contact Information</h3>
          
          <div className="mb-4">
            <h5>Address</h5>
            <p>
              123 React Street<br />
              Frontend City, JS 12345<br />
              United States
            </p>
          </div>
          
          <div className="mb-4">
            <h5>Phone</h5>
            <p>
              +1 (555) 123-4567
            </p>
          </div>
          
          <div className="mb-4">
            <h5>Email</h5>
            <p>
              <a href="mailto:info@ggdbfrontend.com">info@ggdbfrontend.com</a>
            </p>
          </div>
          
          <div className="mb-4">
            <h5>Business Hours</h5>
            <p>
              Monday - Friday: 9:00 AM - 5:00 PM<br />
              Saturday & Sunday: Closed
            </p>
          </div>
        </Card.Body>
      </Card>
    </div>
  );
};

export default ContactInfo;
