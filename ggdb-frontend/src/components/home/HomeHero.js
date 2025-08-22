import React from 'react';
import { Container, Row, Col, Button } from 'react-bootstrap';

const HomeHero = () => {
  return (
    <div className="hero-section">
      <Container>
        <Row className="justify-content-center">
          <Col md={8} className="text-center">
            <h1>Welcome to GGDB Frontend</h1>
            <p className="lead">
              A modern React application with routing, SASS styling, and Bootstrap integration.
              This project demonstrates best practices for React development.
            </p>
            <Button variant="primary" size="lg">
              Get Started
            </Button>
          </Col>
        </Row>
      </Container>
    </div>
  );
};

export default HomeHero;
