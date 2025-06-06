# Python Web App

A production-ready full-stack web application with React frontend and Flask backend API, featuring comprehensive security, monitoring, and error handling.

## 🚀 Features

### Backend (Flask)

- **Security-first design** with input validation, rate limiting, and security headers
- **Comprehensive error handling** with custom exceptions and correlation IDs
- **Structured logging** with request tracking and log rotation
- **Health monitoring** with system resource checks
- **Rate limiting** to prevent abuse
- **CORS support** with configurable origins
- **Environment-based configuration** for different deployment stages
- **Type hints** throughout for better code quality

### Frontend (React)

- Modern React application with hooks
- Responsive design
- API integration with error handling

### Development & Testing

- Comprehensive test suite with pytest
- Code quality tools (Black, Flake8, MyPy)
- Development and production configurations
- Docker support (coming soon)

## 📁 Project Structure

```
python-web-app/
├── frontend/                 # React frontend application
│   ├── src/
│   ├── public/
│   └── package.json
├── src/                     # Python backend source code
│   └── python_web_app/
│       ├── models/          # Data models with validation
│       ├── services/        # Business logic with error handling
│       ├── utils/           # Utility functions and exceptions
│       ├── config.py        # Secure configuration management
│       └── main.py          # Main application with middleware
├── tests/                   # Comprehensive test suite
│   ├── unit/               # Unit tests
│   ├── integration/        # Integration tests
│   └── fixtures/           # Test data
├── logs/                   # Application logs (auto-created)
├── pyproject.toml          # Python project configuration
└── start-dev.sh           # Development startup script
```

## 🛠️ Quick Start

### Prerequisites

- **Python 3.8+** (3.9+ recommended)
- **Node.js 16+**
- **npm or yarn**

### Installation

1. **Clone and setup:**

```bash
git clone <repository-url>
cd python-web-app
```

2. **Install Python dependencies:**

```bash
# Install in development mode
pip install -e ".[dev]"

# Or install from requirements
pip install -r requirements-dev.txt
```

3. **Install frontend dependencies:**

```bash
cd frontend
npm install
cd ..
```

4. **Create environment file:**

```bash
cp .env.example .env
# Edit .env with your configuration
```

### 🏃‍♂️ Running the Application

#### Development Mode (Recommended)

```bash
# Use the startup script (starts both backend and frontend)
chmod +x start-dev.sh
./start-dev.sh
```

Or run components separately:

```bash
# Terminal 1: Backend (with auto-reload)
export FLASK_ENV=development
python -m src.python_web_app.main

# Terminal 2: Frontend (with hot reload)
cd frontend && npm start
```

#### Production Mode

```bash
# Set required environment variables
export FLASK_ENV=production
export SECRET_KEY="your-super-secure-secret-key-here"
export CORS_ORIGINS="https://yourdomain.com"

# Run the application
python -m src.python_web_app.main
```

## 🔌 API Endpoints

### Health & Monitoring

- `GET /api/health` - Comprehensive health check with system metrics
  - Rate limit: 30 requests/minute
  - Returns: Application status, memory usage, disk space

### Greetings API

- `GET /api/hello` - Simple greeting message
  - Rate limit: 60 requests/minute
- `GET /api/hello/<name>` - Personalized greeting
  - Rate limit: 30 requests/minute
  - Validates name format and length
  - Sanitizes input for security

### Response Format

All endpoints return standardized JSON responses:

```json
{
  "success": true,
  "timestamp": "2024-01-01T12:00:00.000Z",
  "correlation_id": "uuid-for-tracking",
  "data": { ... }
}
```

Error responses:

```json
{
  "success": false,
  "timestamp": "2024-01-01T12:00:00.000Z",
  "correlation_id": "uuid-for-tracking",
  "error": {
    "message": "Error description",
    "type": "ValidationError",
    "code": "VALIDATION_ERROR"
  }
}
```

## ⚙️ Configuration

### Environment Variables

| Variable       | Description                                  | Default        | Required         |
| -------------- | -------------------------------------------- | -------------- | ---------------- |
| `FLASK_ENV`    | Environment (development/production/testing) | development    | No               |
| `SECRET_KEY`   | Flask secret key                             | Auto-generated | Yes (production) |
| `HOST`         | Server host                                  | 0.0.0.0        | No               |
| `PORT`         | Server port                                  | 8000           | No               |
| `CORS_ORIGINS` | Allowed CORS origins (comma-separated)       | \*             | No               |
| `REDIS_URL`    | Redis URL for rate limiting                  | memory://      | No               |

### Security Features

- **Input validation** with regex patterns and length limits
- **Rate limiting** per endpoint with configurable storage
- **Security headers** (HSTS, CSP, X-Frame-Options, etc.)
- **Request size limits** to prevent DoS attacks
- **Correlation IDs** for request tracking
- **Structured logging** with security event monitoring

## 🧪 Testing

### Run Tests

```bash
# Run all tests
pytest

# Run with coverage report
pytest --cov=src/python_web_app --cov-report=html

# Run specific test categories
pytest tests/unit/          # Unit tests only
pytest tests/integration/   # Integration tests only

# Run with verbose output
pytest -v
```

### Test Coverage

The project maintains high test coverage with:

- Unit tests for all business logic
- Integration tests for API endpoints
- Error scenario testing
- Input validation testing

## 🔧 Development

### Code Quality Tools

```bash
# Format code
black src/ tests/

# Lint code
flake8 src/ tests/

# Type checking
mypy src/

# Run all quality checks
black src/ tests/ && flake8 src/ tests/ && mypy src/
```

### Development Guidelines

1. **Security First**: Always validate inputs, use proper error handling
2. **Type Everything**: Use type hints for all functions and methods
3. **Test Thoroughly**: Write tests for happy path and edge cases
4. **Log Appropriately**: Use structured logging with correlation IDs
5. **Handle Errors Gracefully**: Use custom exceptions and proper HTTP status codes

### Adding New Features

1. **Create models** in `src/python_web_app/models/`
2. **Implement business logic** in `src/python_web_app/services/`
3. **Add API endpoints** in `src/python_web_app/main.py`
4. **Write comprehensive tests** in `tests/`
5. **Update documentation**

## 📊 Monitoring & Logging

### Logging

- **Structured JSON logging** in production
- **Correlation IDs** for request tracking
- **Log rotation** to manage disk space
- **Security event logging**

### Health Monitoring

The `/api/health` endpoint provides:

- Application status
- Memory usage and availability
- Disk space usage
- System resource checks

### Error Tracking

- All errors include correlation IDs
- Structured error responses
- Comprehensive error logging with stack traces

## 🚀 Deployment

### Production Checklist

- [ ] Set `FLASK_ENV=production`
- [ ] Configure secure `SECRET_KEY`
- [ ] Set appropriate `CORS_ORIGINS`
- [ ] Configure Redis for rate limiting (optional)
- [ ] Set up log aggregation
- [ ] Configure monitoring and alerting
- [ ] Set up SSL/TLS termination
- [ ] Configure reverse proxy (nginx/Apache)

### Docker Deployment (Coming Soon)

```bash
# Build and run with Docker
docker build -t python-web-app .
docker run -p 8000:8000 python-web-app
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Run quality checks
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Troubleshooting

### Common Issues

**Import Errors**: Make sure you've installed the package in development mode:

```bash
pip install -e .
```

**Rate Limiting Issues**: Check Redis connection or use memory storage:

```bash
export REDIS_URL="memory://"
```

**CORS Issues**: Configure appropriate origins:

```bash
export CORS_ORIGINS="http://localhost:3000,https://yourdomain.com"
```

### Getting Help

- Check the logs in the `logs/` directory
- Look for correlation IDs in error responses
- Run tests to verify functionality
- Check configuration with health endpoint

## 🎓 Educational Notes

### Why Flask Needs a SECRET_KEY

The SECRET_KEY is required by Flask for:

1. **Session signing** - Cryptographically signing session cookies
2. **CSRF protection** - Generating and validating CSRF tokens
3. **Secure cookies** - Encrypting sensitive cookie data
4. **Flash messages** - Securing temporary messages between requests
5. **Extension security** - Many Flask extensions require it for cryptographic operations

Even for simple APIs, Flask's security infrastructure requires this key. In development, we auto-generate a secure random key. In production, you must provide your own secure key.

### Security Improvements Made

1. **Input Validation**: Comprehensive regex-based validation with length limits
2. **Rate Limiting**: Per-endpoint limits to prevent abuse
3. **Error Handling**: Structured error responses with correlation tracking
4. **Security Headers**: HSTS, CSP, X-Frame-Options, etc.
5. **Request Size Limits**: Prevent DoS attacks via large payloads
6. **Structured Logging**: JSON logging with correlation IDs for tracking
7. **Health Monitoring**: Real system resource checks, not just "OK" responses

### Code Quality Improvements

1. **Type Hints**: Complete type annotations for better IDE support and error catching
2. **Custom Exceptions**: Proper exception hierarchy for different error types
3. **Dependency Injection Ready**: Service layer designed for easy testing
4. **Correlation IDs**: Request tracking for debugging and monitoring
5. **Comprehensive Testing**: Edge cases, error scenarios, and input validation tests
