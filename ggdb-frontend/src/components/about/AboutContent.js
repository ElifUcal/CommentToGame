import React from 'react';
import { Card } from 'react-bootstrap';

const AboutContent = () => {
  return (
    <div className="about-content">
      <Card className="mb-4">
        <Card.Body>
          <h2>Our Story</h2>
          <p>
            GGDB Frontend was created as a modern React application to demonstrate best practices
            in frontend development. Our goal is to provide a clean, maintainable, and scalable
            codebase that can be used as a starting point for new projects.
          </p>
          <p>
            We focus on using the latest technologies and methodologies to create responsive,
            accessible, and user-friendly web applications.
          </p>
        </Card.Body>
      </Card>

      <Card className="mb-4">
        <Card.Body>
          <h2>Our Mission</h2>
          <p>
            Our mission is to simplify the process of creating modern web applications by providing
            a solid foundation that follows best practices and industry standards.
          </p>
          <p>
            We believe in clean code, component-based architecture, and separation of concerns to
            create maintainable and scalable applications.
          </p>
        </Card.Body>
      </Card>

      <Card className="mb-4">
        <Card.Body>
          <h2>Technologies We Use</h2>
          <ul>
            <li>React for building user interfaces</li>
            <li>React Router for navigation</li>
            <li>SASS for styling</li>
            <li>Bootstrap for responsive design</li>
            <li>Modern JavaScript (ES6+)</li>
          </ul>
        </Card.Body>
      </Card>
    </div>
  );
};

export default AboutContent;
