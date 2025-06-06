"""Configuration settings for the Python Web App."""

import os
import secrets
import logging.config
from typing import Dict, Any


class Config:
    """Base configuration class."""
    
    # Flask settings - Generate secure random key for development
    SECRET_KEY = os.environ.get('SECRET_KEY') or secrets.token_urlsafe(32)
    DEBUG = False
    TESTING = False
    
    # Server settings
    HOST = os.environ.get('HOST') or '0.0.0.0'
    PORT = int(os.environ.get('PORT') or 8000)
    
    # CORS settings
    CORS_ORIGINS = os.environ.get('CORS_ORIGINS', '*').split(',')
    
    # Security settings
    MAX_CONTENT_LENGTH = 16 * 1024 * 1024  # 16MB max request size
    
    # Rate limiting settings
    RATELIMIT_STORAGE_URL = os.environ.get('REDIS_URL', 'memory://')
    
    @classmethod
    def get_config(cls) -> Dict[str, Any]:
        """Get configuration as dictionary."""
        return {
            'SECRET_KEY': cls.SECRET_KEY,
            'DEBUG': cls.DEBUG,
            'TESTING': cls.TESTING,
            'HOST': cls.HOST,
            'PORT': cls.PORT,
            'CORS_ORIGINS': cls.CORS_ORIGINS,
            'MAX_CONTENT_LENGTH': cls.MAX_CONTENT_LENGTH,
            'RATELIMIT_STORAGE_URL': cls.RATELIMIT_STORAGE_URL,
        }
    
    @classmethod
    def setup_logging(cls) -> None:
        """Setup logging configuration."""
        logging_config = {
            'version': 1,
            'disable_existing_loggers': False,
            'formatters': {
                'detailed': {
                    'format': '%(asctime)s [%(levelname)s] %(name)s: %(message)s'
                },
                'json': {
                    'format': '{"timestamp": "%(asctime)s", "level": "%(levelname)s", "logger": "%(name)s", "message": "%(message)s"}'
                }
            },
            'handlers': {
                'console': {
                    'class': 'logging.StreamHandler',
                    'level': 'INFO',
                    'formatter': 'detailed',
                },
                'file': {
                    'class': 'logging.handlers.RotatingFileHandler',
                    'filename': 'logs/app.log',
                    'maxBytes': 10485760,  # 10MB
                    'backupCount': 5,
                    'formatter': 'json',
                    'level': 'INFO',
                }
            },
            'root': {
                'level': 'INFO',
                'handlers': ['console', 'file'] if not cls.TESTING else ['console']
            }
        }
        
        # Create logs directory if it doesn't exist
        os.makedirs('logs', exist_ok=True)
        logging.config.dictConfig(logging_config)


class DevelopmentConfig(Config):
    """Development configuration."""
    
    DEBUG = True


class ProductionConfig(Config):
    """Production configuration."""
    
    DEBUG = False
    SECRET_KEY = os.environ.get('SECRET_KEY')
    
    @classmethod
    def get_config(cls) -> Dict[str, Any]:
        """Get production configuration with validation."""
        config = super().get_config()
        
        if not cls.SECRET_KEY:
            raise ValueError("SECRET_KEY environment variable must be set in production")
        
        return config


class TestingConfig(Config):
    """Testing configuration."""
    
    TESTING = True
    DEBUG = True
    SECRET_KEY = 'test-secret-key'  # Fixed key for consistent testing


# Configuration mapping
config_map = {
    'development': DevelopmentConfig,
    'production': ProductionConfig,
    'testing': TestingConfig,
    'default': DevelopmentConfig,
}


def get_config(config_name: str = None) -> Config:
    """Get configuration class based on environment."""
    if config_name is None:
        config_name = os.environ.get('FLASK_ENV', 'default')
    
    return config_map.get(config_name, config_map['default'])