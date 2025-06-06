"""PID Controller implementation for temperature regulation."""

import time
from typing import Optional


class PIDController:
    """
    Discrete implementation of a PID controller.
    
    The PID controller calculates an output value based on the error between
    a desired setpoint and a measured process variable. It uses proportional,
    integral, and derivative terms to minimize the error over time.
    """

    def __init__(
        self,
        kp: float = 2.0,
        ki: float = 10.0,
        kd: float = 0.001,
        integral_max: float = 1000,
        integral_min: float = -1000,
        output_max: float = 100,
        output_min: float = 0,
    ):
        """
        Initialize the PID controller parameters.
        
        Args:
            kp: Proportional gain
            ki: Integral gain  
            kd: Derivative gain
            integral_max: Maximum integral value
            integral_min: Minimum integral value
            output_max: Maximum output signal
            output_min: Minimum output signal
        """
        self.kp = kp
        self.ki = ki
        self.kd = kd
        
        self.integral_max = integral_max
        self.integral_min = integral_min
        
        self.output_max = output_max
        self.output_min = output_min
        
        self._error = 0.0
        self._integral = 0.0
        self._derivative = 0.0
        self._last_time = 0.0
        
    def reset(self) -> None:
        """Reset the PID controller state."""
        self._error = 0.0
        self._integral = 0.0
        self._derivative = 0.0
        self._last_time = 0.0
        
    def compute(self, error: float, sample_time: Optional[float] = None) -> float:
        """
        Compute the PID output based on the current error.
        
        Args:
            error: The error between setpoint and measured value
            sample_time: Optional fixed sampling time in seconds
            
        Returns:
            The control signal output
        """
        current_time = time.time()
        
        # Calculate time delta
        if not self._last_time:
            self._last_time = current_time
            
        if sample_time is None:
            dt = current_time - self._last_time
        else:
            dt = sample_time
            
        self._last_time = current_time
        
        # Avoid division by zero
        if dt <= 0:
            dt = 0.001
            
        # Calculate integral term
        self._integral += error * dt
        
        # Apply integral limits (anti-windup)
        if self._integral > self.integral_max:
            self._integral = self.integral_max
        elif self._integral < self.integral_min:
            self._integral = self.integral_min
            
        # Calculate derivative term
        self._derivative = (error - self._error) / dt
        
        # Store current error for next iteration
        self._error = error
        
        # Calculate PID output
        output = (
            self.kp * error +
            self.ki * self._integral +
            self.kd * self._derivative
        )
        
        # Apply output limits
        if output > self.output_max:
            output = self.output_max
        elif output < self.output_min:
            output = self.output_min
            
        return output
    
    def get_parameters(self) -> dict:
        """Get current PID parameters."""
        return {
            'kp': self.kp,
            'ki': self.ki,
            'kd': self.kd,
            'integral_max': self.integral_max,
            'integral_min': self.integral_min,
            'output_max': self.output_max,
            'output_min': self.output_min,
        }
    
    def set_parameters(
        self,
        kp: Optional[float] = None,
        ki: Optional[float] = None,
        kd: Optional[float] = None,
    ) -> None:
        """Update PID parameters."""
        if kp is not None:
            self.kp = kp
        if ki is not None:
            self.ki = ki
        if kd is not None:
            self.kd = kd