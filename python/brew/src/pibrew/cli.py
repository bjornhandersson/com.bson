"""Command-line interface for PiBrew."""

import argparse
import sys
from typing import Optional

from .core.brew_controller import BrewController
from .web.server import run_server


def create_parser() -> argparse.ArgumentParser:
    """Create command-line argument parser."""
    parser = argparse.ArgumentParser(
        description="PiBrew - Raspberry Pi Brewing Control System",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  pibrew server                    # Start web server on default port 8080
  pibrew server --port 9000        # Start web server on port 9000
  pibrew server --host 192.168.1.100 --port 8080
        """
    )
    
    subparsers = parser.add_subparsers(dest='command', help='Available commands')
    
    # Server command
    server_parser = subparsers.add_parser('server', help='Start the web server')
    server_parser.add_argument(
        '--host', 
        default='0.0.0.0',
        help='Host to bind to (default: 0.0.0.0)'
    )
    server_parser.add_argument(
        '--port', 
        type=int, 
        default=8080,
        help='Port to bind to (default: 8080)'
    )
    server_parser.add_argument(
        '--heater-led-pin',
        type=int,
        default=15,
        help='GPIO pin for heater LED (default: 15)'
    )
    server_parser.add_argument(
        '--heater-relay-pin',
        type=int,
        default=13,
        help='GPIO pin for heater relay (default: 13)'
    )
    server_parser.add_argument(
        '--thermo-data-pin',
        type=int,
        default=21,
        help='GPIO pin for thermocouple data (default: 21)'
    )
    server_parser.add_argument(
        '--thermo-clk-pin',
        type=int,
        default=23,
        help='GPIO pin for thermocouple clock (default: 23)'
    )
    server_parser.add_argument(
        '--thermo-cs-pin',
        type=int,
        default=26,
        help='GPIO pin for thermocouple chip select (default: 26)'
    )
    server_parser.add_argument(
        '--cycle-time',
        type=float,
        default=2.0,
        help='Heater cycle time in seconds (default: 2.0)'
    )
    server_parser.add_argument(
        '--pid-kp',
        type=float,
        default=5.0,
        help='PID proportional gain (default: 5.0)'
    )
    server_parser.add_argument(
        '--pid-ki',
        type=float,
        default=0.007,
        help='PID integral gain (default: 0.007)'
    )
    server_parser.add_argument(
        '--pid-kd',
        type=float,
        default=1.0,
        help='PID derivative gain (default: 1.0)'
    )
    
    # Status command
    status_parser = subparsers.add_parser('status', help='Get system status')
    status_parser.add_argument(
        '--json',
        action='store_true',
        help='Output status as JSON'
    )
    
    # Test command
    test_parser = subparsers.add_parser('test', help='Test hardware connections')
    test_parser.add_argument(
        '--component',
        choices=['gpio', 'thermocouple', 'heater', 'all'],
        default='all',
        help='Component to test (default: all)'
    )
    
    return parser


def cmd_server(args) -> int:
    """Run the web server command."""
    try:
        # Create brew controller with specified parameters
        controller = BrewController(
            heater_led_pin=args.heater_led_pin,
            heater_relay_pin=args.heater_relay_pin,
            thermo_data_pin=args.thermo_data_pin,
            thermo_clk_pin=args.thermo_clk_pin,
            thermo_cs_pin=args.thermo_cs_pin,
            cycle_length=args.cycle_time,
            pid_kp=args.pid_kp,
            pid_ki=args.pid_ki,
            pid_kd=args.pid_kd,
        )
        
        # Run server
        run_server(controller, host=args.host, port=args.port)
        return 0
        
    except KeyboardInterrupt:
        print("\nServer stopped by user")
        return 0
    except Exception as e:
        print(f"Error starting server: {e}")
        return 1


def cmd_status(args) -> int:
    """Get system status command."""
    try:
        controller = BrewController()
        status = controller.get_status()
        
        if args.json:
            import json
            print(json.dumps(status, indent=2))
        else:
            print("PiBrew System Status:")
            print(f"  Temperature: {status['temperature']:.1f}°C")
            print(f"  Target: {status['target_temperature']:.1f}°C")
            print(f"  Heater Power: {status['heater_power']:.1f}%")
            print(f"  Running: {'Yes' if status['is_running'] else 'No'}")
            print(f"  Heater On: {'Yes' if status['heater_on'] else 'No'}")
        
        controller.cleanup()
        return 0
        
    except Exception as e:
        print(f"Error getting status: {e}")
        return 1


def cmd_test(args) -> int:
    """Test hardware connections command."""
    try:
        print("Testing PiBrew hardware connections...")
        
        if args.component in ['gpio', 'all']:
            print("\n1. Testing GPIO...")
            try:
                import RPi.GPIO as GPIO
                GPIO.setmode(GPIO.BOARD)
                print("   ✓ GPIO library available")
                GPIO.cleanup()
            except ImportError:
                print("   ⚠ RPi.GPIO not available (using mock)")
            except Exception as e:
                print(f"   ✗ GPIO error: {e}")
        
        if args.component in ['thermocouple', 'all']:
            print("\n2. Testing thermocouple...")
            try:
                from .hardware.thermocouple import MAX31855
                thermo = MAX31855(dataInPin=21, clkPin=23, csPin=26)
                temp = thermo.readTempC()
                print(f"   ✓ Temperature reading: {temp:.1f}°C")
            except Exception as e:
                print(f"   ✗ Thermocouple error: {e}")
        
        if args.component in ['heater', 'all']:
            print("\n3. Testing heater...")
            try:
                from .hardware.heater import Heater
                heater = Heater(cycle_time=1.0)
                heater.set_power(50.0)
                print(f"   ✓ Heater power set to: {heater.get_power()}%")
            except Exception as e:
                print(f"   ✗ Heater error: {e}")
        
        print("\nHardware test completed.")
        return 0
        
    except Exception as e:
        print(f"Error during testing: {e}")
        return 1


def main(argv: Optional[list] = None) -> int:
    """Main CLI entry point."""
    parser = create_parser()
    args = parser.parse_args(argv)
    
    if not args.command:
        parser.print_help()
        return 1
    
    # Route to appropriate command handler
    if args.command == 'server':
        return cmd_server(args)
    elif args.command == 'status':
        return cmd_status(args)
    elif args.command == 'test':
        return cmd_test(args)
    else:
        print(f"Unknown command: {args.command}")
        return 1


if __name__ == '__main__':
    sys.exit(main())