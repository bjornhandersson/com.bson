"""Main application module for the Python Web App."""

import logging
from flask import Flask, request, g
from flask_cors import CORS
from flask_limiter import Limiter
from flask_limiter.util import get_remote_address

from .config import get_config
from .services import GreetingService
from .utils import (
    create_api_response,
    handle_error,
    log_request,
    get_correlation_id,
    validate_request_size
)
from .utils.exceptions import ValidationError, ServiceError

logger = logging.getLogger(__name__)


def create_app(config_name: str = None) -> Flask:
    """Create and configure the Flask application."""
    app = Flask(__name__)
    
    # Load configuration
    config_class = get_config(config_name)
    config = config_class.get_config()
    
    app.config.update(config)
    
    # Setup logging
    config_class.setup_logging()
    
    # Setup CORS with more restrictive settings
    CORS(app,
         origins=config['CORS_ORIGINS'],
         methods=['GET', 'POST', 'PUT', 'DELETE'],
         allow_headers=['Content-Type', 'Authorization'],
         max_age=86400)  # Cache preflight for 24 hours
    
    # Setup rate limiting
    limiter = Limiter(
        key_func=get_remote_address,
        default_limits=["200 per day", "50 per hour"],
        storage_uri=config.get('RATELIMIT_STORAGE_URL', 'memory://')
    )
    limiter.init_app(app)
    
    # Register middleware
    register_middleware(app)
    
    # Register routes
    register_routes(app, limiter)
    
    # Register error handlers
    register_error_handlers(app)
    
    logger.info("Flask application created successfully")
    return app


def register_middleware(app: Flask) -> None:
    """Register middleware functions."""
    
    @app.before_request
    def before_request():
        """Execute before each request."""
        # Generate correlation ID for request tracking
        g.correlation_id = get_correlation_id()
        
        # Validate request size
        content_length = request.content_length
        max_size = app.config.get('MAX_CONTENT_LENGTH', 16 * 1024 * 1024)
        
        if not validate_request_size(content_length, max_size):
            logger.warning(f"Request too large: {content_length} bytes [correlation_id: {g.correlation_id}]")
            return create_api_response(
                error=ValidationError("Request payload too large"),
                status_code=413
            )
    
    @app.after_request
    def after_request(response):
        """Execute after each request."""
        # Add security headers
        response.headers['X-Content-Type-Options'] = 'nosniff'
        response.headers['X-Frame-Options'] = 'DENY'
        response.headers['X-XSS-Protection'] = '1; mode=block'
        response.headers['Strict-Transport-Security'] = 'max-age=31536000; includeSubDomains'
        response.headers['Content-Security-Policy'] = "default-src 'self'"
        
        # Add correlation ID to response headers for debugging
        if hasattr(g, 'correlation_id'):
            response.headers['X-Correlation-ID'] = g.correlation_id
        
        return response


def register_routes(app: Flask, limiter: Limiter) -> None:
    """Register application routes."""
    
    @app.route('/api/health', methods=['GET'])
    @limiter.limit("30 per minute")
    def health_check():
        """Health check endpoint."""
        log_request('/api/health', 'GET', request.headers.get('User-Agent'))
        
        try:
            health_data = GreetingService.get_health_status()
            return create_api_response(data=health_data)
        except ServiceError as e:
            logger.error(f"Service error in health check: {e}")
            return handle_error(e, 503)
        except Exception as e:
            logger.error(f"Unexpected error in health check: {e}", exc_info=True)
            return handle_error(e, 500)
    
    @app.route('/api/hello', methods=['GET'])
    @limiter.limit("60 per minute")
    def hello_api():
        """Simple API endpoint."""
        log_request('/api/hello', 'GET', request.headers.get('User-Agent'))
        
        try:
            greeting_data = GreetingService.get_simple_greeting()
            return create_api_response(data=greeting_data)
        except ServiceError as e:
            logger.error(f"Service error in hello API: {e}")
            return handle_error(e, 503)
        except Exception as e:
            logger.error(f"Unexpected error in hello API: {e}", exc_info=True)
            return handle_error(e, 500)
    
    @app.route('/api/hello/<name>', methods=['GET'])
    @limiter.limit("30 per minute")
    def hello_name(name: str):
        """Personalized greeting API endpoint."""
        log_request(f'/api/hello/{name}', 'GET', request.headers.get('User-Agent'))
        
        try:
            greeting_data = GreetingService.get_personalized_greeting(name)
            return create_api_response(data=greeting_data)
        except ValidationError as e:
            logger.warning(f"Validation error in personalized greeting: {e}")
            return handle_error(e, 400)
        except ServiceError as e:
            logger.error(f"Service error in personalized greeting: {e}")
            return handle_error(e, 503)
        except Exception as e:
            logger.error(f"Unexpected error in personalized greeting: {e}", exc_info=True)
            return handle_error(e, 500)


def register_error_handlers(app: Flask) -> None:
    """Register error handlers."""
    
    @app.errorhandler(404)
    def not_found(error):
        """Handle 404 errors."""
        logger.warning(f"404 error: {request.url}")
        return create_api_response(
            error=ValidationError("Endpoint not found"),
            status_code=404
        )
    
    @app.errorhandler(405)
    def method_not_allowed(error):
        """Handle 405 errors."""
        logger.warning(f"405 error: {request.method} {request.url}")
        return create_api_response(
            error=ValidationError("Method not allowed"),
            status_code=405
        )
    
    @app.errorhandler(413)
    def payload_too_large(error):
        """Handle 413 errors."""
        logger.warning(f"413 error: Request payload too large")
        return create_api_response(
            error=ValidationError("Request payload too large"),
            status_code=413
        )
    
    @app.errorhandler(429)
    def ratelimit_handler(error):
        """Handle rate limit errors."""
        logger.warning(f"Rate limit exceeded: {request.remote_addr}")
        return create_api_response(
            error=ValidationError("Rate limit exceeded. Please try again later."),
            status_code=429
        )
    
    @app.errorhandler(500)
    def internal_error(error):
        """Handle 500 errors."""
        logger.error(f"500 error: {error}", exc_info=True)
        return create_api_response(
            error=Exception("Internal server error"),
            status_code=500
        )


def main() -> None:
    """Main entry point for running the application."""
    try:
        app = create_app()
        config = app.config
        
        logger.info(f"Starting application on {config['HOST']}:{config['PORT']}")
        
        app.run(
            host=config['HOST'],
            port=config['PORT'],
            debug=config['DEBUG']
        )
    except Exception as e:
        logger.error(f"Failed to start application: {e}", exc_info=True)
        raise


if __name__ == '__main__':
    main()