// main.s - Temperature sensor reader with 100ms refresh rate
.global _main

.data
error_msg: .ascii "ERR_NO_SNR"
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
    
    // Raw sensor data reading successful
    mov x20, x0             // Save raw hex data
    
    // Close serial connection
    mov x0, x19
    bl close_serial
    
    // Display raw hex data
    bl display_hex_data
    
    // Wait 1 second before next reading
    mov x0, #1000
    mov x1, #1000
    mul x0, x0, x1          // x0 = 1000 * 1000 = 1000000 microseconds (1 second)
    bl microsleep
    
    // Continue loop
    b temperature_loop

sensor_error:
    // No sensor connected - display ERR404
    bl display_error
    
    // Wait 1 second before retry
    mov x0, #1000
    mov x1, #1000
    mul x0, x0, x1          // x0 = 1000 * 1000 = 1000000 microseconds (1 second)
    bl microsleep
    
    // Continue loop
    b temperature_loop

read_error:
    // Close connection and display error
    mov x0, x19
    bl close_serial
    bl display_error
    
    // Wait 1 second before retry
    mov x0, #1000
    mov x1, #1000
    mul x0, x0, x1          // x0 = 1000 * 1000 = 1000000 microseconds (1 second)
    bl microsleep
    
    // Continue loop
    b temperature_loop

// Function: display_hex_data
// Purpose: Display raw sensor data in hexadecimal format
// Input: x20 = raw 32-bit sensor data
// Output: none
display_hex_data:
    stp x19, x20, [sp, #-16]!
    stp x21, x22, [sp, #-16]!
    
    // Print "0x" prefix
    mov x0, '0'
    bl store_in_buffer
    bl print_buffer
    mov x0, 'x'
    bl store_in_buffer
    bl print_buffer
    
    // Print 8 hex digits (32 bits)
    mov x21, #8             // Counter for 8 hex digits
    mov x19, #28            // Start with bit position 28 (highest nibble)
    
hex_loop:
    // Extract 4 bits (one hex digit)
    lsr x22, x20, x19       // Shift right to get the nibble
    and x22, x22, #0xF      // Mask to 4 bits
    
    // Convert to hex character
    cmp x22, #10
    blt hex_digit
    
    // A-F (10-15)
    sub x0, x22, #10
    add x0, x0, 'A'
    b print_hex_char
    
hex_digit:
    // 0-9
    add x0, x22, '0'
    
print_hex_char:
    bl store_in_buffer
    bl print_buffer
    
    // Move to next nibble
    sub x19, x19, #4        // Move to next 4 bits
    sub x21, x21, #1        // Decrement counter
    cmp x21, #0
    bne hex_loop
    
    // Print newline
    bl print_newline
    
    ldp x21, x22, [sp], #16
    ldp x19, x20, [sp], #16
    ret

// Function: display_error
// Purpose: Display ERR_NO_SNR message
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
    
    // Print _
    mov x0, '_'
    bl store_in_buffer
    bl print_buffer
    
    // Print N
    mov x0, 'N'
    bl store_in_buffer
    bl print_buffer
    
    // Print O
    mov x0, 'O'
    bl store_in_buffer
    bl print_buffer
    
    // Print _
    mov x0, '_'
    bl store_in_buffer
    bl print_buffer
    
    // Print S
    mov x0, 'S'
    bl store_in_buffer
    bl print_buffer
    
    // Print N
    mov x0, 'N'
    bl store_in_buffer
    bl print_buffer
    
    // Print R
    mov x0, 'R'
    bl store_in_buffer
    bl print_buffer
    
    // Print newline
    bl print_newline
    
    ldp x19, x20, [sp], #16
    ret