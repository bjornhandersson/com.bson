# Makefile for ARM64 assembly program
TARGET = add
SOURCE = add.s
OBJECT = add.o

# Default target
all: $(TARGET)

# Build the executable
$(TARGET): $(OBJECT)
	ld -arch arm64 -o $(TARGET) $(OBJECT) -lSystem -syslibroot `xcrun --show-sdk-path` -e _main

# Assemble the source file
$(OBJECT): $(SOURCE)
	as -arch arm64 $(SOURCE) -o $(OBJECT)

# Run the program and show exit code
run: $(TARGET)
	./$(TARGET)
	@echo "Exit code: $$?"

# Clean up generated files
clean:
	rm -f $(OBJECT) $(TARGET)

# Rebuild everything
rebuild: clean all

.PHONY: all run clean rebuild