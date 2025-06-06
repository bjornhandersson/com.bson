// math.s - Mathematical operations and calculations
.text

// Function: add_numbers
// Purpose: Add two numbers together
// Input: x0 = first number, x1 = second number
// Output: x0 = result
.global add_numbers
add_numbers:
    add x0, x0, x1      // Add x1 to x0
    ret                 // Return to caller

// Function: convert_to_ascii
// Purpose: Convert single digit number to ASCII character
// Input: x0 = number (0-9)
// Output: x0 = ASCII character
.global convert_to_ascii
convert_to_ascii:
    add x0, x0, '0'     // Convert to ASCII ('0' = 48)
    ret                 // Return to caller