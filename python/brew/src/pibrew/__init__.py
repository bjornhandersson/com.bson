"""
PiBrew - Raspberry Pi Brewing Control System

A modern Python application for controlling brewing temperature using a Raspberry Pi,
featuring PID temperature control, web interface, and hardware integration.
"""

__version__ = "1.0.0"
__author__ = "PiBrew Team"
__email__ = "contact@pibrew.com"

from .core.brew_controller import BrewController
from .core.pid_controller import PIDController
from .hardware.heater import Heater
from .hardware.thermocouple import MAX31855

__all__ = [
    "BrewController",
    "PIDController", 
    "Heater",
    "MAX31855",
]