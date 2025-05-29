"""Tests for PID controller."""

import pytest
from src.pibrew.core.pid_controller import PIDController


class TestPIDController:
    """Test cases for PID controller."""
    
    def test_initialization(self):
        """Test PID controller initialization."""
        pid = PIDController(kp=1.0, ki=0.1, kd=0.01)
        assert pid.kp == 1.0
        assert pid.ki == 0.1
        assert pid.kd == 0.01
        
    def test_reset(self):
        """Test PID controller reset."""
        pid = PIDController()
        
        # Run some calculations to set internal state
        pid.compute(10.0)
        pid.compute(5.0)
        
        # Reset should clear internal state
        pid.reset()
        assert pid._error == 0.0
        assert pid._integral == 0.0
        assert pid._derivative == 0.0
        assert pid._last_time == 0.0
        
    def test_proportional_only(self):
        """Test proportional-only controller (P controller)."""
        pid = PIDController(kp=2.0, ki=0.0, kd=0.0)
        
        # For P-only controller, output should be proportional to error
        output = pid.compute(10.0, sample_time=1.0)
        assert output == 20.0  # 2.0 * 10.0
        
    def test_output_limits(self):
        """Test output limiting."""
        pid = PIDController(kp=10.0, output_max=50.0, output_min=-50.0)
        
        # Large error should be limited to max output
        output = pid.compute(100.0, sample_time=1.0)
        assert output == 50.0
        
        # Large negative error should be limited to min output
        output = pid.compute(-100.0, sample_time=1.0)
        assert output == -50.0
        
    def test_integral_limits(self):
        """Test integral windup protection."""
        pid = PIDController(
            kp=0.0, ki=1.0, kd=0.0,
            integral_max=10.0, integral_min=-10.0
        )
        
        # Apply large error for multiple cycles
        for _ in range(20):
            pid.compute(100.0, sample_time=1.0)
            
        # Integral should be limited
        assert pid._integral == 10.0
        
    def test_parameter_update(self):
        """Test updating PID parameters."""
        pid = PIDController(kp=1.0, ki=1.0, kd=1.0)
        
        pid.set_parameters(kp=2.0, ki=0.5)
        assert pid.kp == 2.0
        assert pid.ki == 0.5
        assert pid.kd == 1.0  # Should remain unchanged
        
    def test_get_parameters(self):
        """Test getting PID parameters."""
        pid = PIDController(kp=1.5, ki=0.2, kd=0.05)
        params = pid.get_parameters()
        
        assert params['kp'] == 1.5
        assert params['ki'] == 0.2
        assert params['kd'] == 0.05