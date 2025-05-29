"""Tests for heater controller."""

import pytest
from unittest.mock import Mock
from src.pibrew.hardware.heater import Heater


class TestHeater:
    """Test cases for heater controller."""
    
    def test_initialization(self):
        """Test heater initialization."""
        heater = Heater(cycle_time=3.0, min_toggle_time=0.2)
        assert heater._cycle_time == 3.0
        assert heater._min_toggle_time == 0.2
        assert heater._power == 0.0
        assert heater._heater_on is False
        
    def test_set_power(self):
        """Test setting heater power."""
        heater = Heater()
        
        # Normal power setting
        heater.set_power(50.0)
        assert heater.get_power() == 50.0
        
        # Power above 100% should be limited
        heater.set_power(150.0)
        assert heater.get_power() == 100.0
        
        # Negative power should be limited to 0
        heater.set_power(-10.0)
        assert heater.get_power() == 0.0
        
    def test_get_status(self):
        """Test getting heater status."""
        heater = Heater(cycle_time=2.5, min_toggle_time=0.15)
        heater.set_power(75.0)
        
        status = heater.get_status()
        assert status['power'] == 75.0
        assert status['heater_on'] is False
        assert status['cycle_time'] == 2.5
        assert status['min_toggle_time'] == 0.15
        
    def test_run_cycle_zero_power(self):
        """Test running cycle with zero power."""
        heater = Heater(cycle_time=1.0, min_toggle_time=0.1)
        heater.set_power(0.0)
        
        callback = Mock()
        
        # With zero power, heater should stay off
        heater.run_cycle(callback)
        
        # Should only call callback with False (heater off)
        callback.assert_called_once_with(False)
        assert heater.is_heater_on() is False
        
    def test_run_cycle_full_power(self):
        """Test running cycle with full power."""
        heater = Heater(cycle_time=1.0, min_toggle_time=0.1)
        heater.set_power(100.0)
        
        callback = Mock()
        
        # With full power, heater should stay on for full cycle
        heater.run_cycle(callback)
        
        # Should only call callback with True (heater on)
        callback.assert_called_once_with(True)
        
    def test_run_cycle_no_callback(self):
        """Test running cycle without callback."""
        heater = Heater(cycle_time=0.1)  # Short cycle for testing
        heater.set_power(50.0)
        
        # Should not raise exception when no callback provided
        heater.run_cycle()
        
    def test_power_bounds(self):
        """Test power value boundaries."""
        heater = Heater()
        
        # Test various power values
        test_values = [-50, 0, 25, 50, 75, 100, 150]
        expected = [0, 0, 25, 50, 75, 100, 100]
        
        for test_val, expected_val in zip(test_values, expected):
            heater.set_power(test_val)
            assert heater.get_power() == expected_val