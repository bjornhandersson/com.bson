# ARM64 Assembly Addition Program (Modular Structure)

A modular ARM64 assembly program that demonstrates separation of concerns by splitting IO operations from business logic. The program adds two numbers (5 + 3) and prints the result.

## What it does

The program:

- Loads 5 into register `x0`
- Loads 3 into register `x1`
- Adds them together (result: 8)
- Converts the result to ASCII
- Prints the result and a newline
- Exits with success code

## Project Structure

The code is organized into logical modules:

```
├── math.s           # Mathematical operations (add_numbers, convert_to_ascii)
├── src/
│   ├── main.s       # Main program entry point
│   ├── io/
│   │   └── output.s # I/O operations (print_buffer, print_newline, store_in_buffer)
│   └── system/
│       └── exit.s   # System operations (exit_program)
```

### Module Responsibilities

- **`math.s`**: Pure mathematical functions without side effects
- **`src/main.s`**: Program entry point and orchestration
- **`src/io/output.s`**: All I/O operations and buffer management
- **`src/system/exit.s`**: System calls and program termination

## Building and Running

### Build Commands

```bash
# Build the program
make

# Build and run with exit code display
make run

# Clean generated files
make clean

# Rebuild from scratch
make rebuild

# Show project structure info
make info
```

### Manual Execution

```bash
# Run the program
./add

# Check the exit code (should be 0 for success)
echo $?
```

## Files

### Source Files

- `math.s` - Mathematical operations (root level)
- `src/main.s` - Main program entry point
- `src/io/output.s` - I/O operations and buffer management
- `src/system/exit.s` - System calls

### Build Files

- `Makefile` - Modular build configuration
- `add` - Compiled executable (generated)
- `*.o` and `src/**/*.o` - Object files (generated)

### Legacy

- `add.s` - Original monolithic version (kept for reference)

## Requirements

- macOS with Xcode command line tools
- ARM64 processor (Apple Silicon)

## Architecture Benefits

This modular structure provides:

1. **Separation of Concerns**: Logic, I/O, and system operations are isolated
2. **Reusability**: Mathematical functions can be reused without I/O dependencies
3. **Testability**: Pure functions are easier to test in isolation
4. **Maintainability**: Changes to I/O don't affect mathematical logic
5. **Clarity**: Each module has a single, well-defined responsibility

## Assembly Functions

### Mathematical Logic (`math.s`)

- `add_numbers`: Adds two numbers together
- `convert_to_ascii`: Converts single digit to ASCII character

### I/O Operations (`src/io/output.s`)

- `store_in_buffer`: Stores character in output buffer
- `print_buffer`: Prints buffer content to stdout
- `print_newline`: Prints newline character

### System Operations (`src/system/exit.s`)

- `exit_program`: Terminates program with success code

The program demonstrates clean architecture principles applied to low-level assembly programming.
