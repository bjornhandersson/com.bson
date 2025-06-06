"""Custom exception classes for the application."""


class AppException(Exception):
    """Base exception class for application-specific errors."""
    
    def __init__(self, message: str, error_code: str = None):
        super().__init__(message)
        self.message = message
        self.error_code = error_code or self.__class__.__name__


class ValidationError(AppException):
    """Raised when input validation fails."""
    pass


class ServiceError(AppException):
    """Raised when a service operation fails."""
    pass


class ConfigurationError(AppException):
    """Raised when there's a configuration issue."""
    pass


class ExternalServiceError(AppException):
    """Raised when an external service call fails."""
    pass