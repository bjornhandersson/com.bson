# ARM64 Assembly Addition Program

A simple ARM64 assembly program that adds two numbers (5 + 3) and exits with the result.

## What it does

The program:

- Loads 5 into register `x0`
- Loads 3 into register `x1`
- Adds them together (result: 8)
- Exits with the sum as the exit code

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
```

### Manual Execution

```bash
# Run the program
./add

# Check the exit code (should be 8)
echo $?
```

## Files

- `add.s` - ARM64 assembly source code
- `Makefile` - Build configuration
- `add` - Compiled executable (generated)
- `add.o` - Object file (generated)

## Requirements

- macOS with Xcode command line tools
- ARM64 processor (Apple Silicon)

## Assembly Explanation

```assembly
mov x0, xzr         # Clear x0 register
add x0, x0, 5       # x0 = x0 + 5 (equivalent to x0 += 5)
mov x1, xzr         # Clear x1 register
add x1, x1, 3       # x1 = x1 + 3 (equivalent to x1 += 3)
add x0, x0, x1      # x0 = x0 + x1 (final result: 8)
```

The program demonstrates basic ARM64 assembly arithmetic and system calls.
