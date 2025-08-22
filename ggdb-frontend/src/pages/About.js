import React from 'react';
import { Container, Row, Col } from 'react-bootstrap';
import AboutContent from '../components/about/AboutContent';

const About = () => {
  return (
    <div className="about-page">
      <Container>
        <Row className="justify-content-center">
          <Col md={10}>
            <h1 className="text-center mb-4">About Us</h1>
            <AboutContent />
          </Col>
        </Row>
      </Container>
    </div>
  );
};

export default About;
