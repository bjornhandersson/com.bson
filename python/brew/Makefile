.PHONY: help install install-dev test lint format clean build upload docs

# Default target
help:
	@echo "PiBrew Development Commands"
	@echo "=========================="
	@echo "install      Install package in current environment"
	@echo "install-dev  Install package with development dependencies"
	@echo "test         Run test suite"
	@echo "lint         Run code linting"
	@echo "format       Format code with black"
	@echo "type-check   Run type checking with mypy"
	@echo "clean        Clean build artifacts"
	@echo "build        Build distribution packages"
	@echo "docs         Build documentation"
	@echo "run-server   Run development server"
	@echo "run-tests    Run tests with coverage"

# Installation
install:
	pip install -e .

install-dev:
	pip install -e ".[dev]"

# Testing
test:
	pytest

run-tests:
	pytest --cov=src/pibrew --cov-report=html --cov-report=term

# Code quality
lint:
	flake8 src/ tests/

format:
	black src/ tests/ examples/

type-check:
	mypy src/

# Development server
run-server:
	python -m pibrew.cli server

# Build and distribution
clean:
	rm -rf build/
	rm -rf dist/
	rm -rf *.egg-info/
	find . -type d -name __pycache__ -exec rm -rf {} +
	find . -type f -name "*.pyc" -delete

build: clean
	python -m build

upload: build
	python -m twine upload dist/*

# Documentation
docs:
	@echo "Documentation build not yet implemented"
	@echo "Consider adding Sphinx or MkDocs"

# Quick development setup
dev-setup: install-dev
	@echo "Development environment ready!"
	@echo "Run 'make run-server' to start the development server"

# Hardware test
test-hardware:
	python -m pibrew.cli test

# Show project status
status:
	python -m pibrew.cli status --json