"""Integration tests for API endpoints."""

import json
import pytest


class TestAPIEndpoints:
    """Test cases for API endpoints."""
    
    def test_health_check_endpoint(self, client):
        """Test the health check endpoint."""
        response = client.get('/api/health')
        assert response.status_code == 200
        
        data = json.loads(response.data)
        assert data['status'] == 'healthy'
        assert 'message' in data
        assert 'timestamp' in data
    
    def test_hello_api_endpoint(self, client):
        """Test the hello API endpoint."""
        response = client.get('/api/hello')
        assert response.status_code == 200
        
        data = json.loads(response.data)
        assert data['message'] == 'Hello from Flask API!'
        assert data['status'] == 'success'
        assert 'timestamp' in data
    
    def test_hello_name_api_endpoint(self, client, sample_names):
        """Test the personalized hello API endpoint."""
        for name in sample_names:
            response = client.get(f'/api/hello/{name}')
            assert response.status_code == 200
            
            data = json.loads(response.data)
            assert data['message'] == f'Hello, {name}!'
            assert data['status'] == 'success'
            assert 'timestamp' in data
    
    def test_hello_name_api_with_special_chars(self, client):
        """Test the personalized hello API with special characters."""
        name = 'John-Doe'
        response = client.get(f'/api/hello/{name}')
        assert response.status_code == 200
        
        data = json.loads(response.data)
        assert data['message'] == f'Hello, {name}!'
        assert data['status'] == 'success'
    
    def test_hello_name_api_empty_name(self, client):
        """Test the personalized hello API with empty name."""
        # URL encoding for empty string
        response = client.get('/api/hello/')
        # This should return 404 as the route doesn't match
        assert response.status_code == 404
    
    def test_hello_name_api_whitespace_name(self, client):
        """Test the personalized hello API with whitespace name."""
        response = client.get('/api/hello/%20%20%20')  # URL encoded spaces
        assert response.status_code == 400
        
        data = json.loads(response.data)
        assert data['error'] is True
        assert 'Invalid name' in data['message']
    
    def test_hello_name_api_very_long_name(self, client):
        """Test the personalized hello API with very long name."""
        long_name = 'A' * 100
        response = client.get(f'/api/hello/{long_name}')
        assert response.status_code == 400
        
        data = json.loads(response.data)
        assert data['error'] is True
    
    def test_nonexistent_route(self, client):
        """Test that nonexistent routes return 404."""
        response = client.get('/nonexistent')
        assert response.status_code == 404
        
        data = json.loads(response.data)
        assert data['error'] is True
        assert 'not found' in data['message'].lower()
    
    def test_cors_headers(self, client):
        """Test that CORS headers are present."""
        response = client.get('/api/health')
        assert response.status_code == 200
        
        # Check for CORS headers
        assert 'Access-Control-Allow-Origin' in response.headers
    
    def test_api_response_format(self, client):
        """Test that all API responses follow the expected format."""
        endpoints = ['/api/health', '/api/hello', '/api/hello/TestUser']
        
        for endpoint in endpoints:
            response = client.get(endpoint)
            assert response.status_code == 200
            
            data = json.loads(response.data)
            
            # All successful responses should have these fields
            assert 'status' in data
            assert 'message' in data
            assert 'timestamp' in data
            
            # Content-Type should be JSON
            assert response.content_type == 'application/json'