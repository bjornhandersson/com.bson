"""Helper utility functions."""

import uuid
import logging
from typing import Dict, Any, Tuple, Optional
from datetime import datetime, timezone
from flask import jsonify, Response, g
from .exceptions import ValidationError, AppException

logger = logging.getLogger(__name__)


def get_correlation_id() -> str:
    """Get or create a correlation ID for request tracking."""
    if not hasattr(g, 'correlation_id'):
        g.correlation_id = str(uuid.uuid4())
    return g.correlation_id


def create_api_response(
    data: Optional[Dict[str, Any]] = None,
    error: Optional[Exception] = None,
    status_code: int = 200,
    message: Optional[str] = None
) -> Tuple[Response, int]:
    """Create a standardized API response."""
    correlation_id = get_correlation_id()
    
    response = {
        "success": error is None,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "correlation_id": correlation_id,
    }
    
    if error:
        response["error"] = {
            "message": str(error),
            "type": type(error).__name__,
        }
        
        # Add error code if it's an AppException
        if isinstance(error, AppException) and hasattr(error, 'error_code'):
            response["error"]["code"] = error.error_code
            
    else:
        response["data"] = data or {}
        if message:
            response["message"] = message
    
    return jsonify(response), status_code


def format_response(data: Dict[str, Any], status_code: int = 200) -> Tuple[Response, int]:
    """Format a standardized JSON response (legacy compatibility)."""
    return create_api_response(data=data, status_code=status_code)


def handle_error(error: Exception, status_code: int = 500) -> Tuple[Response, int]:
    """Handle and format error responses."""
    correlation_id = get_correlation_id()
    
    # Log error with correlation ID for tracking
    logger.error(
        f"Error occurred [correlation_id: {correlation_id}]: {str(error)}",
        exc_info=True,
        extra={'correlation_id': correlation_id}
    )
    
    # Determine appropriate status code based on exception type
    if isinstance(error, ValidationError):
        status_code = 400
    elif isinstance(error, AppException):
        # Keep the provided status code for other app exceptions
        pass
    else:
        # For unexpected errors, always use 500
        status_code = 500
    
    return create_api_response(error=error, status_code=status_code)


def validate_json_input(data: Dict[str, Any], required_fields: list) -> Tuple[bool, Optional[str]]:
    """Validate that required fields are present in JSON input."""
    if not data:
        return False, "Request body is empty or invalid JSON"
    
    missing_fields = []
    for field in required_fields:
        if field not in data:
            missing_fields.append(field)
        elif data[field] is None:
            missing_fields.append(f"{field} (cannot be null)")
    
    if missing_fields:
        return False, f"Missing required fields: {', '.join(missing_fields)}"
    
    return True, None


def sanitize_string(input_string: str, max_length: int = 100) -> str:
    """Sanitize string input by trimming and limiting length."""
    if not input_string:
        return ""
    
    # Strip whitespace and limit length
    sanitized = input_string.strip()[:max_length]
    
    # Remove any potentially harmful characters
    sanitized = ''.join(char for char in sanitized if char.isprintable())
    
    # Remove control characters except newlines and tabs
    sanitized = ''.join(char for char in sanitized if ord(char) >= 32 or char in '\n\t')
    
    return sanitized


def log_request(endpoint: str, method: str, user_agent: str = None) -> None:
    """Log incoming requests for monitoring."""
    correlation_id = get_correlation_id()
    
    log_data = {
        'correlation_id': correlation_id,
        'endpoint': endpoint,
        'method': method,
        'user_agent': user_agent
    }
    
    log_message = f"{method} {endpoint} [correlation_id: {correlation_id}]"
    if user_agent:
        log_message += f" - User-Agent: {user_agent}"
    
    logger.info(log_message, extra=log_data)


def validate_request_size(content_length: Optional[int], max_size: int = 16 * 1024 * 1024) -> bool:
    """Validate request content length."""
    if content_length is None:
        return True  # No content length header
    
    return content_length <= max_size