// exit.s - System operations and program termination
.text

// Function: exit_program
// Purpose: Exit the program with success code
// Input: none
// Output: none (program terminates)
.global exit_program
exit_program:
    mov x16, 1          // sys_exit system call
    mov x0, 0           // exit code (0 = success)
    svc 0x80            // make system call
    // No ret needed - program terminates