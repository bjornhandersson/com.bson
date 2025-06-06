import React, { useState, useEffect } from 'react';
import './App.css';

function App() {
  const [healthStatus, setHealthStatus] = useState(null);
  const [greeting, setGreeting] = useState('');
  const [personName, setPersonName] = useState('');
  const [personalizedGreeting, setPersonalizedGreeting] = useState('');
  const [loading, setLoading] = useState(false);

  // Check health status on component mount
  useEffect(() => {
    checkHealth();
    getSimpleGreeting();
  }, []);

  const checkHealth = async () => {
    try {
      const response = await fetch('/api/health');
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      const result = await response.json();
      // Handle new API response format
      if (result.success && result.data) {
        setHealthStatus(result.data);
      } else {
        setHealthStatus({ status: 'error', message: result.error?.message || 'Unknown error' });
      }
    } catch (error) {
      console.error('Health check failed:', error);
      setHealthStatus({ status: 'error', message: 'Failed to connect to backend' });
    }
  };

  const getSimpleGreeting = async () => {
    try {
      const response = await fetch('/api/hello');
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      const result = await response.json();
      // Handle new API response format
      if (result.success && result.data) {
        setGreeting(result.data.message);
      } else {
        setGreeting('Failed to get greeting');
      }
    } catch (error) {
      console.error('Failed to get greeting:', error);
      setGreeting('Failed to get greeting');
    }
  };

  const getPersonalizedGreeting = async () => {
    if (!personName.trim()) return;
    
    setLoading(true);
    try {
      const response = await fetch(`/api/hello/${encodeURIComponent(personName)}`);
      const result = await response.json();
      
      if (response.ok && result.success && result.data) {
        setPersonalizedGreeting(result.data.message);
      } else {
        // Handle error response
        const errorMessage = result.error?.message || 'Failed to get greeting';
        setPersonalizedGreeting(`Error: ${errorMessage}`);
      }
    } catch (error) {
      console.error('Failed to get personalized greeting:', error);
      setPersonalizedGreeting('Failed to connect to server');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    getPersonalizedGreeting();
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>Python Web App</h1>
        <p>React Frontend with Flask Backend</p>
      </header>

      <main className="App-main">
        {/* Health Status Section */}
        <section className="status-section">
          <h2>Backend Status</h2>
          {healthStatus ? (
            <div className={`status-indicator ${healthStatus.status}`}>
              <span className="status-dot"></span>
              <span>{healthStatus.status === 'healthy' ? 'Connected' : 'Disconnected'}</span>
              <p>{healthStatus.message}</p>
            </div>
          ) : (
            <div className="loading">Checking connection...</div>
          )}
        </section>

        {/* Simple Greeting Section */}
        <section className="greeting-section">
          <h2>API Response</h2>
          {greeting ? (
            <div className="greeting-display">
              <p>{greeting}</p>
            </div>
          ) : (
            <div className="loading">Loading greeting...</div>
          )}
        </section>

        {/* Personalized Greeting Section */}
        <section className="personalized-section">
          <h2>Personalized Greeting</h2>
          <form onSubmit={handleSubmit} className="greeting-form">
            <div className="input-group">
              <input
                type="text"
                value={personName}
                onChange={(e) => setPersonName(e.target.value)}
                placeholder="Enter your name"
                className="name-input"
              />
              <button 
                type="submit" 
                disabled={loading || !personName.trim()}
                className="submit-button"
              >
                {loading ? 'Getting Greeting...' : 'Get Greeting'}
              </button>
            </div>
          </form>
          
          {personalizedGreeting && (
            <div className="personalized-greeting">
              <p>{personalizedGreeting}</p>
            </div>
          )}
        </section>

        {/* API Endpoints Documentation */}
        <section className="api-docs">
          <h2>Available API Endpoints</h2>
          <div className="endpoint-list">
            <div className="endpoint">
              <code>GET /api/health</code>
              <span>Health check endpoint</span>
            </div>
            <div className="endpoint">
              <code>GET /api/hello</code>
              <span>Simple greeting API</span>
            </div>
            <div className="endpoint">
              <code>GET /api/hello/&lt;name&gt;</code>
              <span>Personalized greeting API</span>
            </div>
          </div>
        </section>
      </main>

      <footer className="App-footer">
        <p>&copy; 2025 Python Web App - React + Flask</p>
      </footer>
    </div>
  );
}

export default App;