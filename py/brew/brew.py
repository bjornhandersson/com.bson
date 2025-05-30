from __future__ import division
import pid
import heater
import max31855 as Thermocouple
import _thread
import threading
import time

try :
    import RPi.GPIO as GPIO
except ImportError :
    import GPIOMock as GPIO

class Brew:
    def __init__(self):
        self.GPIO_HEATER_LED_PIN = 15
        self.GPIO_HEATER_RELAY_PIN = 13
        self.CYCLE_LENGTH = 2
        
        ''' Cleaning up and setup GPIO '''
        GPIO.cleanup();
        GPIO.setmode(GPIO.BOARD)
        GPIO.setup(self.GPIO_HEATER_LED_PIN, GPIO.OUT)
        GPIO.output(self.GPIO_HEATER_LED_PIN, False)
        GPIO.setup(self.GPIO_HEATER_RELAY_PIN, GPIO.OUT)
        GPIO.output(self.GPIO_HEATER_RELAY_PIN, False)
        
        ''' is the brewing process started '''
        self.isStarted = False

        #self.kettle = kettle.Kettle()
        ''' heater managing the heating cycle. 
        The heater transforms the analog output from the PID algorithm to on/off cycles '''
        self.heater = heater.Heater(self.CYCLE_LENGTH)
        
        ''' Thermometer '''
        self.thermocouple = Thermocouple.MAX31855(dataInPin=21, clkPin=23, csPin=26)

        ''' The PID algorithm '''
        self.pid = pid.PID(25.0, 0.4, 1.0, 100, -100)
        
        ''' Target temperature '''
        self.targetTemp = 0
        
        ''' Current temperature. The temperature is redefined one time per cycle '''
        self.temp = self.thermocouple.readTempC();
        
        self._status = {
            'time': time.time(),
            'temp': self.getTemp(),
            'target': self.targetTemp,
            'power': self.heater.getPower(),
            'started':self.isStarted
        }
    
    def start(self):
        print('Start')
        if(self.isStarted == False):
            self.isStarted = True
            _thread.start_new_thread(self._worker, (self,))
    
    def stop(self):
        print('Stop')
        if(self.isStarted == True):
            self._evStop = threading.Event()
            self.isStarted = False
            self._evStop.wait(None)
            
    def _worker(self, arg):
        try:
            print('Thread started')
            self._evStop = None
            ''' run the PID/Heater/temp cycle '''
            while(self.isStarted == True):
                ''' get temperature '''
                self.temp = self.thermocouple.readTempC()
                
                ''' feed PID with the error. error = target - current temp '''
                ''' PID outputs the power, the power is a value between 0 - 100% '''
                effect = self.pid.run(self.targetTemp - self.getTemp())
                print('acc_I:{} acc_D:{}'.format(self.pid._integral, self.pid._derivative))
                
                ''' Set power to heater '''
                self.heater.setPower(effect)
                
                ''' Set status '''
                self._status = {
                    'time': time.time(),
                    'temp': self.temp,
                    'target': self.targetTemp,
                    'power': effect,
                    'started': self.isStarted
                }
                
                ''' run the cycle. This methods blocks for the cycle time '''
                ''' runCycle takes a callback method which is called when the heater is turned on or off'''
                self.heater.runCycle(self.heatKettle) #blocking for cycle time
            
            self._status['time'] = time.time()
            self._status['started'] = False
            self.heatKettle(False)
        finally:
            #self.pid.reset()
            self.isStarted = False
            self._evStop.set()
            print('Stopped')
    
    ''' returns the temperature used in the last cycle '''
    def getTemp(self):
        return self.temp
    
    ''' callback method from heater '''
    ''' this methods turns the heater relay and LED on / off '''
    def heatKettle(self, on):
        if(on == True):
            self.GPIO_on(self.GPIO_HEATER_LED_PIN)
            self.GPIO_on(self.GPIO_HEATER_RELAY_PIN)
        else:
            self.GPIO_off(self.GPIO_HEATER_LED_PIN)
            self.GPIO_off(self.GPIO_HEATER_RELAY_PIN)
    
    def getStatus(self):
        return self._status
    
    def setTarget(self, target):
        self.targetTemp = target
        self._status['target'] = target;
              
    def GPIO_on(self, pin):
        GPIO.output(pin, True)
    
    def GPIO_off(self, pin):
        GPIO.output(pin, False)
        
        
    