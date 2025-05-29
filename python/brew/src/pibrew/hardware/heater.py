"""Heater control module for managing heating cycles."""

import time
from typing import Callable, Optional


class Heater:
    """
    Controls heating cycles using PWM-like duty cycle control.
    
    The heater converts analog power values (0-100%) into on/off cycles
    with appropriate timing to achieve the desired average power output.
    """

    def __init__(self, cycle_time: float = 2.0, min_toggle_time: float = 0.1):
        """
        Initialize the heater controller.
        
        Args:
            cycle_time: Duration of each heating cycle in seconds
            min_toggle_time: Minimum time for on/off states in seconds
        """
        self._power = 0.0
        self._cycle_time = cycle_time
        self._min_toggle_time = min_toggle_time
        self._heater_on = False
        
    def set_power(self, power: float) -> None:
        """
        Set the heater power as a percentage.
        
        Args:
            power: Power level from 0.0 to 100.0 percent
        """
        self._power = max(0.0, min(100.0, power))
        
    def get_power(self) -> float:
        """Get the current power setting."""
        return self._power
    
    def is_heater_on(self) -> bool:
        """Check if the heater is currently on."""
        return self._heater_on
    
    def run_cycle(self, callback: Optional[Callable[[bool], None]] = None) -> None:
        """
        Execute one heating cycle based on the current power setting.
        
        This method blocks for the duration of the cycle time and calls
        the callback function when the heater state changes.
        
        Args:
            callback: Function called with True/False when heater turns on/off
        """
        if callback is None:
            callback = self._nop_callback
        
        # Calculate duty cycle time (time heater should be on)
        duty_time = (self._power / 100.0) * self._cycle_time
        
        # Turn heater on if duty time is significant
        if duty_time > self._min_toggle_time:
            self._heater_on = True
            callback(True)
            time.sleep(duty_time)

        # Turn heater off for remaining cycle time
        off_time = self._cycle_time - duty_time
        if off_time > self._min_toggle_time:
            self._heater_on = False
            callback(False)
            time.sleep(off_time)
    
    def _nop_callback(self, on: bool) -> None:
        """No-operation callback for when none is provided."""
        pass
    
    def get_status(self) -> dict:
        """Get current heater status."""
        return {
            'power': self._power,
            'heater_on': self._heater_on,
            'cycle_time': self._cycle_time,
            'min_toggle_time': self._min_toggle_time,
        }