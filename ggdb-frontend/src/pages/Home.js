import React from 'react';
import { Container, Row, Col, Card, Button } from 'react-bootstrap';
import HomeHero from '../components/home/HomeHero';
import FeaturedCards from '../components/home/FeaturedCards';

const Home = () => {
  return (
    <div className="home-page">
      <HomeHero />
      <Container>
        <FeaturedCards />
      </Container>
    </div>
  );
};

export default Home;
