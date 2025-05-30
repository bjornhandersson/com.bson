import time

class PID:
    def __init__(self, KP=2.0, KI=10.0, KD=0.001, I_max=1000, I_min= -1000, U_max=100, U_min= 0):
        """
        @summary: Initializing the PID controller parameters
        Discrete implementation of the PID controller. If you want a P or PI controller 
        just set the I,D to 0
        
        @param KP: Proportional gain
        @param KI: Integral gain
        @param KD: Derivative gain
        @param U_max: The maximum output signal 
        @param U_min: The minimum output signal
        @param I_max: The maximum integral value 
        @param I_min: The minimal integral value
        
        @return: The control signal 
        """
        self.Kp = KP
        self.Ki = KI
        self.Kd = KD
        
        self.I_max = I_max
        self.I_min = I_min
        
        self.U_max = U_max
        self.U_min = U_min
        
        self._error = 0.0
        
        self._integral = 0
        self._derivative = 0
        
        self._dt = 0
    
    def reset(self):
        self._error = 0.0
        self._integral = 0
        self._derivative = 0
        self._dt = 0
        
    def run(self, error, Ts=None):
        """
        @summary: Updating the PID controller parameters
        
        @param error: The error between the predefined value and the measured value
        @param Ts: Sampling time.
        
        @return: The control signal  
        """    
        # The sampling time
        if not self._dt:
            self._dt = time.time()
        
        if Ts is None:
            dt = time.time() - self._dt
        else:
            dt = Ts
        # Storing the time in seconds
        self._dt = time.time()
        
        # Calculate the integral
        self._integral = self._integral + (error * dt)
        # Upper limit
        if self._integral > self.I_max:
            self._integral = self.I_max
        # Down limit
        if self._integral < self.I_min:
            self._integral = self.I_min
            
        # Calculate the derivate
        if dt != 0:
            self._derivative = self.Kd * (error - self._error) / dt
        
        # Storing the error
        self._error = error
        
        # Sum
        PID = self.Kp * error + self.Ki * self._integral + self._derivative
        
        # Signal limitation
        if PID > self.U_max:
            PID = self.U_max
        if PID < self.U_min:
            PID = self.U_min
            
        return PID