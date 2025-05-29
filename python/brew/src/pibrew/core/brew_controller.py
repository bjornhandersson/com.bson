"""Main brewing controller that orchestrates temperature control."""

import threading
import time
from typing import Optional, Dict, Any

from ..hardware.heater import Heater
from ..hardware.thermocouple import MAX31855
from .pid_controller import PIDController

try:
    import RPi.GPIO as GPIO
except ImportError:
    from ..hardware import gpio_mock as GPIO


class BrewController:
    """
    Main brewing controller that manages temperature control using PID algorithm.
    
    This class coordinates the heater, thermocouple, and PID controller to maintain
    a target temperature during the brewing process.
    """

    def __init__(
        self,
        heater_led_pin: int = 15,
        heater_relay_pin: int = 13,
        thermo_data_pin: int = 21,
        thermo_clk_pin: int = 23,
        thermo_cs_pin: int = 26,
        cycle_length: float = 2.0,
        pid_kp: float = 5.0,
        pid_ki: float = 0.007,
        pid_kd: float = 1.0,
    ):
        """
        Initialize the brewing controller.
        
        Args:
            heater_led_pin: GPIO pin for heater LED indicator
            heater_relay_pin: GPIO pin for heater relay control
            thermo_data_pin: GPIO pin for thermocouple data
            thermo_clk_pin: GPIO pin for thermocouple clock
            thermo_cs_pin: GPIO pin for thermocouple chip select
            cycle_length: Heater cycle time in seconds
            pid_kp: PID proportional gain
            pid_ki: PID integral gain
            pid_kd: PID derivative gain
        """
        # GPIO pin assignments
        self.heater_led_pin = heater_led_pin
        self.heater_relay_pin = heater_relay_pin
        
        # Initialize GPIO
        self._setup_gpio()
        
        # Initialize hardware components
        self.heater = Heater(cycle_time=cycle_length)
        self.thermocouple = MAX31855(
            dataInPin=thermo_data_pin,
            clkPin=thermo_clk_pin,
            csPin=thermo_cs_pin
        )
        self.pid = PIDController(kp=pid_kp, ki=pid_ki, kd=pid_kd)
        
        # Control state
        self.target_temperature = 0.0
        self.current_temperature = 0.0
        self.is_running = False
        self._worker_thread: Optional[threading.Thread] = None
        self._stop_event = threading.Event()
        
        # Read initial temperature
        try:
            self.current_temperature = self.thermocouple.readTempC()
        except Exception as e:
            print(f"Warning: Could not read initial temperature: {e}")
            self.current_temperature = 20.0  # Default room temperature
    
    def _setup_gpio(self) -> None:
        """Initialize GPIO pins for heater control."""
        GPIO.cleanup()
        GPIO.setmode(GPIO.BOARD)
        GPIO.setup(self.heater_led_pin, GPIO.OUT)
        GPIO.output(self.heater_led_pin, False)
        GPIO.setup(self.heater_relay_pin, GPIO.OUT)
        GPIO.output(self.heater_relay_pin, False)
    
    def start(self) -> bool:
        """
        Start the brewing process.
        
        Returns:
            True if started successfully, False if already running
        """
        if self.is_running:
            return False
            
        print("Starting brewing process...")
        self.is_running = True
        self._stop_event.clear()
        self._worker_thread = threading.Thread(target=self._control_loop, daemon=True)
        self._worker_thread.start()
        return True
    
    def stop(self) -> bool:
        """
        Stop the brewing process.
        
        Returns:
            True if stopped successfully, False if not running
        """
        if not self.is_running:
            return False
            
        print("Stopping brewing process...")
        self.is_running = False
        self._stop_event.set()
        
        if self._worker_thread and self._worker_thread.is_alive():
            self._worker_thread.join(timeout=5.0)
            
        # Ensure heater is off
        self._set_heater_state(False)
        self.pid.reset()
        print("Brewing process stopped")
        return True
    
    def set_target_temperature(self, temperature: float) -> None:
        """Set the target temperature for brewing."""
        self.target_temperature = temperature
        print(f"Target temperature set to {temperature}°C")
    
    def get_temperature(self) -> float:
        """Get the current temperature reading."""
        return self.current_temperature
    
    def get_status(self) -> Dict[str, Any]:
        """
        Get the current system status.
        
        Returns:
            Dictionary containing temperature, target, power, and running status
        """
        return {
            'temperature': self.current_temperature,
            'target_temperature': self.target_temperature,
            'heater_power': self.heater.get_power(),
            'is_running': self.is_running,
            'heater_on': self.heater.is_heater_on(),
        }
    
    def get_pid_parameters(self) -> Dict[str, float]:
        """Get current PID parameters."""
        return {
            'kp': self.pid.kp,
            'ki': self.pid.ki,
            'kd': self.pid.kd,
        }
    
    def set_pid_parameters(self, kp: float, ki: float, kd: float) -> None:
        """Update PID parameters."""
        self.pid.set_parameters(kp=kp, ki=ki, kd=kd)
        print(f"PID parameters updated: Kp={kp}, Ki={ki}, Kd={kd}")
    
    def _control_loop(self) -> None:
        """Main control loop that runs in a separate thread."""
        try:
            while self.is_running and not self._stop_event.is_set():
                # Read current temperature
                try:
                    self.current_temperature = self.thermocouple.readTempC()
                except Exception as e:
                    print(f"Error reading temperature: {e}")
                    continue
                
                # Calculate error and PID output
                error = self.target_temperature - self.current_temperature
                power_output = self.pid.compute(error)
                
                # Set heater power
                self.heater.set_power(power_output)
                
                # Run heater cycle (this blocks for the cycle time)
                self.heater.run_cycle(self._set_heater_state)
                
        except Exception as e:
            print(f"Error in control loop: {e}")
        finally:
            self._set_heater_state(False)
            self.pid.reset()
    
    def _set_heater_state(self, on: bool) -> None:
        """
        Control the heater relay and LED.
        
        Args:
            on: True to turn heater on, False to turn off
        """
        try:
            GPIO.output(self.heater_led_pin, on)
            GPIO.output(self.heater_relay_pin, on)
        except Exception as e:
            print(f"Error controlling heater GPIO: {e}")
    
    def cleanup(self) -> None:
        """Clean up resources and GPIO."""
        self.stop()
        try:
            GPIO.cleanup()
        except Exception as e:
            print(f"Error during GPIO cleanup: {e}")
    
    def __enter__(self):
        """Context manager entry."""
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit with cleanup."""
        self.cleanup()