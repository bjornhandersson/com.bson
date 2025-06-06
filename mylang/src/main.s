// main.s - Temperature sensor reader with 100ms refresh rate
.global _main

.data
error_msg: .ascii "ERR404"
temp_unit: .ascii "C"
decimal_point: .ascii "."

.text

_main:
    // Main loop - read temperature every 100ms
temperature_loop:
    // Try to open serial connection to Arduino
    bl open_serial
    cmp x0, -1
    beq sensor_error
    
    mov x19, x0             // Save file descriptor
    
    // Read temperature from Arduino
    mov x0, x19
    bl read_temperature_from_arduino
    cmp x0, -1
    beq read_error
    
    // Temperature reading successful
    mov x20, x0             // Save temperature (scaled by 100)
    
    // Close serial connection
    mov x0, x19
    bl close_serial
    
    // Display temperature
    bl display_temperature
    
    // Wait 100ms before next reading
    mov x0, #100
    mov x1, #1000
    mul x0, x0, x1          // x0 = 100 * 1000 = 100000 microseconds
    bl microsleep
    
    // Continue loop
    b temperature_loop

sensor_error:
    // No sensor connected - display ERR404
    bl display_error
    
    // Wait 100ms before retry
    mov x0, #100
    mov x1, #1000
    mul x0, x0, x1          // x0 = 100 * 1000 = 100000 microseconds
    bl microsleep
    
    // Continue loop
    b temperature_loop

read_error:
    // Close connection and display error
    mov x0, x19
    bl close_serial
    bl display_error
    
    // Wait 100ms before retry
    mov x0, #100
    mov x1, #1000
    mul x0, x0, x1          // x0 = 100 * 1000 = 100000 microseconds
    bl microsleep
    
    // Continue loop
    b temperature_loop

// Function: display_temperature
// Purpose: Display temperature value in format XX.XC
// Input: x20 = temperature * 100 (e.g., 2550 for 25.50°C)
// Output: none
display_temperature:
    stp x19, x20, [sp, #-16]!
    stp x21, x22, [sp, #-16]!
    
    // Extract whole degrees
    mov x1, 100
    udiv x0, x20, x1        // x0 = whole degrees
    msub x21, x0, x1, x20   // x21 = remainder (fractional part)
    
    // Display tens digit of whole degrees
    mov x1, 10
    udiv x19, x0, x1        // x19 = tens digit
    msub x22, x19, x1, x0   // x22 = units digit
    
    // Print tens digit (if non-zero)
    cmp x19, 0
    beq skip_tens
    mov x0, x19
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer

skip_tens:
    // Print units digit
    mov x0, x22
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    
    // Print decimal point
    mov x0, '.'
    bl store_in_buffer
    bl print_buffer
    
    // Print fractional part (tenths)
    mov x1, 10
    udiv x0, x21, x1        // Get tens digit of fractional part
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    
    // Print units of fractional part
    mov x1, 10
    udiv x19, x21, x1       // x19 = tens digit
    msub x0, x19, x1, x21   // x0 = units digit
    bl convert_to_ascii
    bl store_in_buffer
    bl print_buffer
    
    // Print temperature unit
    mov x0, 'C'
    bl store_in_buffer
    bl print_buffer
    
    // Print newline
    bl print_newline
    
    ldp x21, x22, [sp], #16
    ldp x19, x20, [sp], #16
    ret

// Function: display_error
// Purpose: Display ERR404 message
// Input: none
// Output: none
display_error:
    stp x19, x20, [sp, #-16]!
    
    // Print E
    mov x0, 'E'
    bl store_in_buffer
    bl print_buffer
    
    // Print R
    mov x0, 'R'
    bl store_in_buffer
    bl print_buffer
    
    // Print R
    mov x0, 'R'
    bl store_in_buffer
    bl print_buffer
    
    // Print 4
    mov x0, '4'
    bl store_in_buffer
    bl print_buffer
    
    // Print 0
    mov x0, '0'
    bl store_in_buffer
    bl print_buffer
    
    // Print 4
    mov x0, '4'
    bl store_in_buffer
    bl print_buffer
    
    // Print newline
    bl print_newline
    
    ldp x19, x20, [sp], #16
    ret