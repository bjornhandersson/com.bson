import time

class PID:
    def __init__(self, KP=2.0, KI=10.0, KD=0.001, O_max=100, O_min=0):
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
        
        self.O_max = O_max
        self.O_min = O_min
        
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
            self._dt = self._gettime();
        
        if(Ts is None):
            dt = self._gettime() - self._dt
        else:
            dt = Ts
        # Storing the time in seconds
        self._dt = self._gettime()
        
        self._integral = self._integral + error * dt;
        self._derivative  = (error - self._error) / dt;
        pid = self.Kp * error + self.Ki * self._integral + self.Kd * self._derivative;
        if(pid > self.O_max):
            pid = self.O_max
        if(pid < self.O_min):
            pid = self.O_min;
            
        self._error = error;
        return pid
    
    def _gettime(self):
        default_timer = time.time
        return default_timer()

        
        
        
    
