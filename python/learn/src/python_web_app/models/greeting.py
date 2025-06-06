"""Greeting model for handling greeting data."""

import os
import psutil
from dataclasses import dataclass, field
from typing import Optional, Dict, Any
from datetime import datetime, timezone


@dataclass
class GreetingModel:
    """Model for greeting data."""
    
    message: str
    status: str = "success"
    timestamp: Optional[datetime] = None
    details: Optional[Dict[str, Any]] = field(default_factory=dict)
    
    def __post_init__(self) -> None:
        """Set timestamp if not provided."""
        if self.timestamp is None:
            self.timestamp = datetime.now(timezone.utc)
    
    def to_dict(self) -> dict:
        """Convert model to dictionary for JSON serialization."""
        result = {
            "message": self.message,
            "status": self.status,
            "timestamp": self.timestamp.isoformat() if self.timestamp else None,
        }
        
        if self.details:
            result["details"] = self.details
            
        return result
    
    @classmethod
    def create_simple_greeting(cls) -> "GreetingModel":
        """Create a simple greeting."""
        return cls(message="Hello from Flask API!")
    
    @classmethod
    def create_personalized_greeting(cls, name: str) -> "GreetingModel":
        """Create a personalized greeting."""
        return cls(message=f"Hello, {name}!")
    
    @classmethod
    def create_health_check(cls) -> "GreetingModel":
        """Create a comprehensive health check response."""
        checks = cls._perform_health_checks()
        all_healthy = all(check["status"] == "healthy" for check in checks.values())
        
        status = "healthy" if all_healthy else "unhealthy"
        message = f"Application status: {status}"
        
        return cls(
            message=message,
            status=status,
            details={"checks": checks}
        )
    
    @classmethod
    def _perform_health_checks(cls) -> Dict[str, Dict[str, Any]]:
        """Perform various health checks."""
        checks = {}
        
        # Memory check
        try:
            memory = psutil.virtual_memory()
            memory_usage = memory.percent
            checks["memory"] = {
                "status": "healthy" if memory_usage < 90 else "unhealthy",
                "usage_percent": memory_usage,
                "available_gb": round(memory.available / (1024**3), 2)
            }
        except Exception as e:
            checks["memory"] = {
                "status": "error",
                "error": str(e)
            }
        
        # Disk space check
        try:
            disk = psutil.disk_usage('/')
            disk_usage = disk.percent
            checks["disk"] = {
                "status": "healthy" if disk_usage < 90 else "unhealthy",
                "usage_percent": disk_usage,
                "free_gb": round(disk.free / (1024**3), 2)
            }
        except Exception as e:
            checks["disk"] = {
                "status": "error",
                "error": str(e)
            }
        
        # Application status
        checks["application"] = {
            "status": "healthy",
            "uptime": "running",
            "version": "0.1.0"
        }
        
        return checks