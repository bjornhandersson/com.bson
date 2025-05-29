# PiBrew Architecture

This document describes the architecture and design of the PiBrew brewing control system.

## Overview

PiBrew is a modern Python application designed to control brewing temperature using a Raspberry Pi. The system uses a PID controller to maintain precise temperature control through hardware interfaces.

## System Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Web Interface │    │   CLI Interface │    │  Python API     │
│   (Browser)     │    │   (Terminal)    │    │  (Direct Use)   │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          └──────────────────────┼──────────────────────┘
                                 │
                    ┌─────────────▼───────────────┐
                    │      Web Server             │
                    │   (web.py framework)        │
                    └─────────────┬───────────────┘
                                  │
                    ┌─────────────▼───────────────┐
                    │    Brew Controller          │
                    │  (Main orchestration)       │
                    └─────────────┬───────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
┌─────────▼───────┐    ┌─────────▼───────┐    ┌─────────▼───────┐
│ PID Controller  │    │     Heater      │    │  Thermocouple   │
│   (Algorithm)   │    │   (PWM Control) │    │   (Temperature) │
└─────────────────┘    └─────────┬───────┘    └─────────────────┘
                                 │
                       ┌─────────▼───────┐
                       │  GPIO Hardware  │
                       │ (Relays, LEDs)  │
                       └─────────────────┘
```

## Core Components

### 1. Brew Controller (`src/pibrew/core/brew_controller.py`)

The main orchestration component that coordinates all other subsystems:

- **Responsibilities:**

  - Initialize and manage hardware components
  - Run the main control loop in a separate thread
  - Coordinate PID controller, heater, and temperature sensor
  - Provide status and control interface

- **Key Features:**
  - Thread-safe operation
  - Context manager support for proper cleanup
  - Configurable GPIO pins and timing parameters
  - Error handling and recovery

### 2. PID Controller (`src/pibrew/core/pid_controller.py`)

Implements a discrete PID (Proportional-Integral-Derivative) controller:

- **Algorithm:** `output = Kp*error + Ki*integral + Kd*derivative`
- **Features:**
  - Configurable gains (Kp, Ki, Kd)
  - Integral windup protection
  - Output limiting
  - Reset functionality
  - Tunable parameters during runtime

### 3. Heater Controller (`src/pibrew/hardware/heater.py`)

Converts analog power commands to digital on/off cycles:

- **PWM Simulation:** Uses time-based duty cycles to simulate analog control
- **Safety Features:**
  - Minimum toggle time to prevent relay damage
  - Power limiting (0-100%)
  - Callback system for state changes

### 4. Hardware Interfaces (`src/pibrew/hardware/`)

#### Thermocouple (`thermocouple.py`)

- MAX31855 thermocouple amplifier interface
- SPI communication
- Temperature reading in Celsius/Fahrenheit
- Error detection and handling

#### GPIO Mock (`gpio_mock.py`)

- Development/testing interface
- Simulates RPi.GPIO for non-Raspberry Pi environments
- Enables development on any platform

### 5. Web Interface (`src/pibrew/web/`)

RESTful API and web server:

- **Framework:** web.py (lightweight Python web framework)
- **API Endpoints:**
  - `/api/start` - Start brewing process
  - `/api/stop` - Stop brewing process
  - `/api/status` - Get system status
  - `/api/target` - Set target temperature
  - `/api/pid` - Get/set PID parameters
- **Static Files:** HTML, CSS, JavaScript for web interface

### 6. Command Line Interface (`src/pibrew/cli.py`)

Provides command-line access to all functionality:

- **Commands:**
  - `pibrew server` - Start web server
  - `pibrew status` - Get system status
  - `pibrew test` - Test hardware connections
- **Configuration:** Command-line arguments for all parameters

## Data Flow

### 1. Temperature Control Loop

```
1. Read temperature from thermocouple
2. Calculate error (target - current)
3. Feed error to PID controller
4. Get power output from PID
5. Set heater power level
6. Run heater cycle (blocking)
7. Repeat
```

### 2. Web API Request Flow

```
1. HTTP request received by web server
2. Route to appropriate handler
3. Handler calls brew controller method
4. Brew controller performs action
5. Return JSON response
```

## Configuration

### Hardware Configuration

- **GPIO Pins:** Configurable for different hardware setups
- **Timing:** Adjustable cycle times and minimum toggle periods
- **Safety Limits:** Temperature and power limits

### PID Tuning

- **Kp (Proportional):** Immediate response to current error
- **Ki (Integral):** Correction for accumulated error over time
- **Kd (Derivative):** Prediction based on rate of error change

### Typical Values:

- Kp: 5.0 (aggressive response)
- Ki: 0.007 (slow integral correction)
- Kd: 1.0 (moderate derivative action)

## Safety Features

### 1. Hardware Safety

- GPIO cleanup on shutdown
- Heater auto-shutoff on errors
- Minimum toggle times to protect relays

### 2. Software Safety

- Thread-safe operations
- Exception handling throughout
- Graceful shutdown procedures
- Signal handling for clean exits

### 3. Operational Safety

- Temperature monitoring
- Power limiting
- Timeout protections
- Error logging

## Extension Points

### 1. New Hardware

- Implement hardware interface classes
- Follow existing patterns for GPIO and SPI
- Add to hardware module

### 2. Additional Sensors

- Temperature sensors (DS18B20, etc.)
- Pressure sensors
- Flow meters

### 3. Control Algorithms

- Alternative to PID (fuzzy logic, neural networks)
- Multi-zone control
- Adaptive tuning

### 4. User Interfaces

- Mobile app
- Desktop GUI
- Voice control
- IoT integration

## Testing Strategy

### 1. Unit Tests

- Individual component testing
- Mock hardware interfaces
- Algorithm validation

### 2. Integration Tests

- Component interaction testing
- Hardware simulation
- End-to-end workflows

### 3. Hardware Tests

- Real hardware validation
- Calibration procedures
- Safety system testing

## Deployment

### 1. Development

- Use GPIO mock for development
- Local web server testing
- Unit test execution

### 2. Production

- Raspberry Pi deployment
- Real hardware interfaces
- System service configuration

### 3. Monitoring

- Log file analysis
- Performance metrics
- Error tracking
