import React from 'react';
import { Row, Col, Card, Button } from 'react-bootstrap';

const FeaturedCards = () => {
  const cardData = [
    {
      id: 1,
      title: 'React Router',
      text: 'Navigate between pages seamlessly with React Router, the standard routing library for React.',
      image: 'https://picsum.photos/id/101/300/200',
    },
    {
      id: 2,
      title: 'SASS Styling',
      text: 'Leverage the power of SASS for more maintainable and organized CSS with variables and nesting.',
      image: 'https://picsum.photos/id/102/300/200',
    },
    {
      id: 3,
      title: 'Bootstrap Integration',
      text: 'Utilize Bootstrap components and grid system for responsive and consistent UI design.',
      image: 'https://picsum.photos/id/103/300/200',
    },
  ];

  return (
    <Row>
      {cardData.map((card) => (
        <Col md={4} key={card.id} className="mb-4">
          <Card>
            <Card.Img variant="top" src={card.image} />
            <Card.Body>
              <Card.Title>{card.title}</Card.Title>
              <Card.Text>{card.text}</Card.Text>
              <Button variant="outline-primary">Learn More</Button>
            </Card.Body>
          </Card>
        </Col>
      ))}
    </Row>
  );
};

export default FeaturedCards;
