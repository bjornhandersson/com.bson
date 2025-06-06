"""Greeting service containing business logic for greetings."""

import re
import logging
from typing import Dict, Any
from ..models.greeting import GreetingModel
from ..utils.exceptions import ValidationError, ServiceError

logger = logging.getLogger(__name__)


class GreetingService:
    """Service class for handling greeting business logic."""
    
    # Name validation pattern: letters, spaces, hyphens, apostrophes only
    NAME_PATTERN = re.compile(r"^[a-zA-Z\s\-']+$")
    MIN_NAME_LENGTH = 1
    MAX_NAME_LENGTH = 50
    
    @staticmethod
    def get_health_status() -> Dict[str, Any]:
        """Get application health status."""
        try:
            logger.info("Performing health status check")
            health_check = GreetingModel.create_health_check()
            return health_check.to_dict()
        except Exception as e:
            logger.error(f"Health check failed: {e}", exc_info=True)
            raise ServiceError(f"Health check failed: {str(e)}") from e
    
    @staticmethod
    def get_simple_greeting() -> Dict[str, Any]:
        """Get a simple greeting message."""
        try:
            logger.info("Generating simple greeting")
            greeting = GreetingModel.create_simple_greeting()
            return greeting.to_dict()
        except Exception as e:
            logger.error(f"Failed to generate simple greeting: {e}", exc_info=True)
            raise ServiceError(f"Failed to generate greeting: {str(e)}") from e
    
    @staticmethod
    def get_personalized_greeting(name: str) -> Dict[str, Any]:
        """Get a personalized greeting message."""
        try:
            logger.info(f"Generating personalized greeting for name length: {len(name) if name else 0}")
            
            # Validate the name
            if not GreetingService.validate_name(name):
                raise ValidationError("Invalid name provided")
            
            # Sanitize the name
            clean_name = GreetingService._sanitize_name(name)
            
            greeting = GreetingModel.create_personalized_greeting(clean_name)
            return greeting.to_dict()
            
        except ValidationError:
            raise  # Re-raise validation errors as-is
        except Exception as e:
            logger.error(f"Failed to generate personalized greeting: {e}", exc_info=True)
            raise ServiceError(f"Failed to generate personalized greeting: {str(e)}") from e
    
    @staticmethod
    def validate_name(name: str) -> bool:
        """Validate if a name is acceptable."""
        if not name:
            logger.warning("Name validation failed: name is None")
            return False
        
        if not isinstance(name, str):
            logger.warning(f"Name validation failed: name is not a string, got {type(name)}")
            return False
        
        clean_name = name.strip()
        
        # Check if empty after stripping
        if not clean_name:
            logger.warning("Name validation failed: name is empty after stripping")
            return False
        
        # Check length constraints
        if not (GreetingService.MIN_NAME_LENGTH <= len(clean_name) <= GreetingService.MAX_NAME_LENGTH):
            logger.warning(f"Name validation failed: length {len(clean_name)} not in range {GreetingService.MIN_NAME_LENGTH}-{GreetingService.MAX_NAME_LENGTH}")
            return False
        
        # Check character pattern
        if not GreetingService.NAME_PATTERN.match(clean_name):
            logger.warning("Name validation failed: contains invalid characters")
            return False
        
        # Check for suspicious patterns
        if GreetingService._contains_suspicious_patterns(clean_name):
            logger.warning("Name validation failed: contains suspicious patterns")
            return False
        
        return True
    
    @staticmethod
    def _sanitize_name(name: str) -> str:
        """Sanitize and clean the name input."""
        if not name:
            return ""
        
        # Strip whitespace and normalize
        clean_name = name.strip()
        
        # Remove multiple consecutive spaces
        clean_name = re.sub(r'\s+', ' ', clean_name)
        
        # Capitalize properly (first letter of each word)
        clean_name = clean_name.title()
        
        return clean_name
    
    @staticmethod
    def _contains_suspicious_patterns(name: str) -> bool:
        """Check for suspicious patterns that might indicate malicious input."""
        suspicious_patterns = [
            r'<[^>]*>',  # HTML tags
            r'javascript:',  # JavaScript protocol
            r'data:',  # Data URLs
            r'vbscript:',  # VBScript
            r'on\w+\s*=',  # Event handlers
            r'expression\s*\(',  # CSS expressions
            r'@import',  # CSS imports
            r'\\x[0-9a-fA-F]{2}',  # Hex encoded characters
            r'%[0-9a-fA-F]{2}',  # URL encoded characters
        ]
        
        name_lower = name.lower()
        for pattern in suspicious_patterns:
            if re.search(pattern, name_lower):
                return True
        
        return False