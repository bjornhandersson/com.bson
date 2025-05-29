# PiBrew - Raspberry Pi Brewing Control System

A modern Python application for controlling brewing temperature using a Raspberry Pi, featuring PID temperature control, web interface, and hardware integration.

## Features

- **PID Temperature Control**: Precise temperature regulation using configurable PID parameters
- **Web Interface**: Real-time monitoring and control via web browser
- **Hardware Integration**: Support for thermocouples, relays, and GPIO control
- **Real-time Monitoring**: Live temperature readings and system status
- **Safety Features**: Automatic shutdown and error handling

## Hardware Requirements

- Raspberry Pi (any model with GPIO)
- MAX31855 thermocouple amplifier
- K-type thermocouple
- Solid state relay for heater control
- LED indicators
- Heating element

## Installation

### From Source

```bash
git clone https://github.com/pibrew/pibrew.git
cd pibrew
pip install -e .
```

### Development Installation

```bash
git clone https://github.com/pibrew/pibrew.git
cd pibrew
pip install -e ".[dev]"
```

## Quick Start

1. **Configure Hardware**: Connect your thermocouple, relay, and LEDs according to the wiring diagram
2. **Start the Server**:
   ```bash
   pibrew server
   ```
3. **Access Web Interface**: Open http://localhost:8080 in your browser
4. **Set Target Temperature**: Use the web interface to set your desired temperature
5. **Start Brewing**: Click the start button to begin temperature control

## Configuration

### GPIO Pin Configuration

Default GPIO pins (BOARD numbering):

- Heater LED: Pin 15
- Heater Relay: Pin 13
- Thermocouple Data: Pin 21
- Thermocouple Clock: Pin 23
- Thermocouple CS: Pin 26

### PID Parameters

Default PID settings:

- Kp (Proportional): 5.0
- Ki (Integral): 0.007
- Kd (Derivative): 1.0

Adjust these parameters via the web interface for optimal temperature control.

## API Endpoints

- `GET /service/getStatus` - Get current system status
- `GET /service/start` - Start brewing process
- `GET /service/stop` - Stop brewing process
- `GET /service/setTarget?target=65.0` - Set target temperature
- `GET /service/getPID` - Get current PID parameters
- `GET /service/setPID?KP=5.0&KI=0.007&KD=1.0` - Set PID parameters

## Development

### Running Tests

```bash
pytest
```

### Code Formatting

```bash
black src/
```

### Type Checking

```bash
mypy src/
```

## Project Structure

```
pibrew/
├── src/pibrew/           # Main package
│   ├── core/             # Core brewing logic
│   ├── hardware/         # Hardware interfaces
│   ├── web/              # Web server and API
│   └── static/           # Web assets
├── tests/                # Test suite
├── docs/                 # Documentation
└── examples/             # Example configurations
```

## Safety Notes

⚠️ **Important Safety Information**

- Always use appropriate electrical safety measures when working with heating elements
- Ensure proper grounding and use GFCI protection
- Never leave the system unattended during operation
- Test all safety shutoffs before use
- Follow local electrical codes and regulations

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

- Documentation: [https://pibrew.readthedocs.io](https://pibrew.readthedocs.io)
- Issues: [https://github.com/pibrew/pibrew/issues](https://github.com/pibrew/pibrew/issues)
- Discussions: [https://github.com/pibrew/pibrew/discussions](https://github.com/pibrew/pibrew/discussions)
