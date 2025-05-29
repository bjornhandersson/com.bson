#!/usr/bin/env python3
"""
Basic usage example for PiBrew.

This example shows how to use the PiBrew library programmatically
to control brewing temperature.
"""

import time
from pibrew import BrewController


def main():
    """Basic brewing control example."""
    
    # Create brew controller with default settings
    with BrewController() as controller:
        print("PiBrew Basic Usage Example")
        print("=" * 30)
        
        # Set target temperature
        target_temp = 65.0  # Celsius
        controller.set_target_temperature(target_temp)
        print(f"Target temperature set to {target_temp}°C")
        
        # Start brewing process
        if controller.start():
            print("Brewing process started!")
        else:
            print("Failed to start brewing process")
            return
        
        try:
            # Monitor for 60 seconds
            for i in range(60):
                status = controller.get_status()
                
                print(f"\rTime: {i:2d}s | "
                      f"Temp: {status['temperature']:5.1f}°C | "
                      f"Target: {status['target_temperature']:5.1f}°C | "
                      f"Power: {status['heater_power']:5.1f}% | "
                      f"Heater: {'ON ' if status['heater_on'] else 'OFF'}", 
                      end='', flush=True)
                
                time.sleep(1)
                
        except KeyboardInterrupt:
            print("\n\nStopping brewing process...")
        
        # Stop brewing
        controller.stop()
        print("\nBrewing process stopped.")


if __name__ == '__main__':
    main()