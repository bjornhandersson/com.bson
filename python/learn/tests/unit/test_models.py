"""Unit tests for models."""

import pytest
from datetime import datetime
from src.python_web_app.models.greeting import GreetingModel


class TestGreetingModel:
    """Test cases for GreetingModel."""
    
    def test_greeting_model_creation(self):
        """Test basic greeting model creation."""
        message = "Hello, World!"
        greeting = GreetingModel(message=message)
        
        assert greeting.message == message
        assert greeting.status == "success"
        assert isinstance(greeting.timestamp, datetime)
    
    def test_greeting_model_with_custom_status(self):
        """Test greeting model with custom status."""
        message = "Test message"
        status = "custom"
        greeting = GreetingModel(message=message, status=status)
        
        assert greeting.message == message
        assert greeting.status == status
    
    def test_greeting_model_with_timestamp(self):
        """Test greeting model with provided timestamp."""
        message = "Test message"
        timestamp = datetime(2023, 1, 1, 12, 0, 0)
        greeting = GreetingModel(message=message, timestamp=timestamp)
        
        assert greeting.timestamp == timestamp
    
    def test_to_dict(self):
        """Test conversion to dictionary."""
        message = "Hello, World!"
        greeting = GreetingModel(message=message)
        result = greeting.to_dict()
        
        assert isinstance(result, dict)
        assert result["message"] == message
        assert result["status"] == "success"
        assert "timestamp" in result
        assert isinstance(result["timestamp"], str)
    
    def test_create_simple_greeting(self):
        """Test factory method for simple greeting."""
        greeting = GreetingModel.create_simple_greeting()
        
        assert greeting.message == "Hello from Flask API!"
        assert greeting.status == "success"
    
    def test_create_personalized_greeting(self):
        """Test factory method for personalized greeting."""
        name = "Alice"
        greeting = GreetingModel.create_personalized_greeting(name)
        
        assert greeting.message == f"Hello, {name}!"
        assert greeting.status == "success"
    
    def test_create_health_check(self):
        """Test factory method for health check."""
        greeting = GreetingModel.create_health_check()
        
        assert greeting.message == "Application is running"
        assert greeting.status == "healthy"