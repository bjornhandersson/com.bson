import { render, screen, waitFor, fireEvent, act } from '@testing-library/react';
import App from './App';

// Mock fetch
global.fetch = jest.fn();

describe('App Component', () => {
  beforeEach(() => {
    fetch.mockClear();
  });

  test('renders main heading', () => {
    // Mock API calls
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'healthy', message: 'Application is running' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello from Flask API!', status: 'success' })
    });

    render(<App />);
    const heading = screen.getByRole('heading', { name: /Python Web App/i });
    expect(heading).toBeInTheDocument();
  });

  test('displays health status', async () => {
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'healthy', message: 'Application is running' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello from Flask API!', status: 'success' })
    });

    render(<App />);
    
    await waitFor(() => {
      expect(screen.getByText('Connected')).toBeInTheDocument();
    });
  });

  test('displays simple greeting', async () => {
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'healthy', message: 'Application is running' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello from Flask API!', status: 'success' })
    });

    render(<App />);
    
    await waitFor(() => {
      expect(screen.getByText('Hello from Flask API!')).toBeInTheDocument();
    });
  });

  test('handles personalized greeting form', async () => {
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'healthy', message: 'Application is running' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello from Flask API!', status: 'success' })
    });

    render(<App />);
    
    const nameInput = screen.getByPlaceholderText('Enter your name');
    const submitButton = screen.getByText('Get Greeting');
    
    expect(nameInput).toBeInTheDocument();
    expect(submitButton).toBeInTheDocument();
    expect(submitButton).toBeDisabled();
    
    // Type in the input
    fireEvent.change(nameInput, { target: { value: 'John' } });
    expect(submitButton).not.toBeDisabled();
  });

  test('submits personalized greeting form', async () => {
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'healthy', message: 'Application is running' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello from Flask API!', status: 'success' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello, John!', status: 'success' })
    });

    render(<App />);
    
    const nameInput = screen.getByPlaceholderText('Enter your name');
    const submitButton = screen.getByText('Get Greeting');
    
    fireEvent.change(nameInput, { target: { value: 'John' } });
    fireEvent.click(submitButton);
    
    await waitFor(() => {
      expect(screen.getByText('Hello, John!')).toBeInTheDocument();
    });
  });

  test('displays API endpoints documentation', () => {
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'healthy', message: 'Application is running' })
    });
    fetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ message: 'Hello from Flask API!', status: 'success' })
    });

    render(<App />);
    
    expect(screen.getByText('Available API Endpoints')).toBeInTheDocument();
    expect(screen.getByText('GET /api/health')).toBeInTheDocument();
    expect(screen.getByText('GET /api/hello')).toBeInTheDocument();
  });
});