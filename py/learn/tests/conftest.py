"""Pytest configuration and fixtures."""

import pytest
from src.python_web_app.main import create_app


@pytest.fixture
def app():
    """Create application for testing."""
    app = create_app('testing')
    return app


@pytest.fixture
def client(app):
    """Create a test client for the Flask application."""
    return app.test_client()


@pytest.fixture
def runner(app):
    """Create a test runner for the Flask application's Click commands."""
    return app.test_cli_runner()


@pytest.fixture
def sample_names():
    """Sample names for testing."""
    return ['Alice', 'Bob', 'Charlie', 'Diana']


@pytest.fixture
def invalid_names():
    """Invalid names for testing."""
    return ['', '   ', 'A' * 100, None]