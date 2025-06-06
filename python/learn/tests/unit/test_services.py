"""Unit tests for services."""

import pytest
from src.python_web_app.services.greeting_service import GreetingService
from src.python_web_app.utils.exceptions import ValidationError, ServiceError


class TestGreetingService:
    """Test cases for GreetingService."""
    
    def test_get_health_status(self):
        """Test health status retrieval."""
        result = GreetingService.get_health_status()
        
        assert isinstance(result, dict)
        assert result["status"] in ["healthy", "unhealthy"]
        assert "message" in result
        assert "timestamp" in result
        assert "details" in result
        assert "checks" in result["details"]
    
    def test_get_simple_greeting(self):
        """Test simple greeting retrieval."""
        result = GreetingService.get_simple_greeting()
        
        assert isinstance(result, dict)
        assert result["message"] == "Hello from Flask API!"
        assert result["status"] == "success"
        assert "timestamp" in result
    
    def test_get_personalized_greeting_valid_name(self, sample_names):
        """Test personalized greeting with valid names."""
        for name in sample_names:
            result = GreetingService.get_personalized_greeting(name)
            
            assert isinstance(result, dict)
            # Name should be title-cased
            expected_name = name.strip().title()
            assert result["message"] == f"Hello, {expected_name}!"
            assert result["status"] == "success"
            assert "timestamp" in result
    
    def test_get_personalized_greeting_empty_name(self):
        """Test personalized greeting with empty name."""
        with pytest.raises(ValidationError, match="Invalid name provided"):
            GreetingService.get_personalized_greeting("")
    
    def test_get_personalized_greeting_whitespace_name(self):
        """Test personalized greeting with whitespace-only name."""
        with pytest.raises(ValidationError, match="Invalid name provided"):
            GreetingService.get_personalized_greeting("   ")
    
    def test_get_personalized_greeting_long_name(self):
        """Test personalized greeting with very long name."""
        long_name = "A" * 100
        with pytest.raises(ValidationError, match="Invalid name provided"):
            GreetingService.get_personalized_greeting(long_name)
    
    def test_get_personalized_greeting_invalid_characters(self):
        """Test personalized greeting with invalid characters."""
        invalid_names = [
            "John123",
            "Jane@Doe",
            "Bob<script>",
            "Alice%20",
            "Charlie\x00",
        ]
        
        for name in invalid_names:
            with pytest.raises(ValidationError, match="Invalid name provided"):
                GreetingService.get_personalized_greeting(name)
    
    def test_validate_name_valid(self, sample_names):
        """Test name validation with valid names."""
        for name in sample_names:
            assert GreetingService.validate_name(name) is True
    
    def test_validate_name_empty(self):
        """Test name validation with empty string."""
        assert GreetingService.validate_name("") is False
    
    def test_validate_name_whitespace(self):
        """Test name validation with whitespace-only string."""
        assert GreetingService.validate_name("   ") is False
    
    def test_validate_name_too_long(self):
        """Test name validation with too long string."""
        long_name = "A" * 51
        assert GreetingService.validate_name(long_name) is False
    
    def test_validate_name_none(self):
        """Test name validation with None."""
        assert GreetingService.validate_name(None) is False
    
    def test_validate_name_invalid_type(self):
        """Test name validation with non-string types."""
        invalid_inputs = [123, [], {}, True, 3.14]
        for invalid_input in invalid_inputs:
            assert GreetingService.validate_name(invalid_input) is False
    
    def test_validate_name_special_characters(self):
        """Test name validation with various special characters."""
        valid_names = [
            "Mary-Jane",
            "O'Connor",
            "Jean-Luc",
            "D'Angelo",
            "Anne Marie",
        ]
        
        for name in valid_names:
            assert GreetingService.validate_name(name) is True
        
        invalid_names = [
            "John123",
            "Jane@example.com",
            "Bob<script>",
            "Alice#Hash",
            "Charlie$Money",
        ]
        
        for name in invalid_names:
            assert GreetingService.validate_name(name) is False
    
    def test_sanitize_name(self):
        """Test name sanitization."""
        test_cases = [
            ("  john  ", "John"),
            ("mary jane", "Mary Jane"),
            ("o'connor", "O'Connor"),
            ("jean-luc", "Jean-Luc"),
            ("  multiple   spaces  ", "Multiple Spaces"),
        ]
        
        for input_name, expected in test_cases:
            result = GreetingService._sanitize_name(input_name)
            assert result == expected