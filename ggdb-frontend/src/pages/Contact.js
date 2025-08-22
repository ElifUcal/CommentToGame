import React from 'react';
import { Container, Row, Col } from 'react-bootstrap';
import ContactForm from '../components/contact/ContactForm';
import ContactInfo from '../components/contact/ContactInfo';

const Contact = () => {
  return (
    <div className="contact-page">
      <Container>
        <h1 className="text-center mb-4">Contact Us</h1>
        <Row>
          <Col md={6} className="mb-4">
            <ContactInfo />
          </Col>
          <Col md={6} className="mb-4">
            <ContactForm />
          </Col>
        </Row>
      </Container>
    </div>
  );
};

export default Contact;
