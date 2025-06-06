from __future__ import division
import time


class Heater:
    def __init__(self, cycleTime=2) :
        self._power = 0
        self._clk = cycleTime
        self._minToggleTime = 0.1
        self._heaterOn = False
        
    def setPower(self, power):
        self._power = power
        
    def getPower(self):
        return self._power
    
    def isHeaterOn(self):
        return self._heaterOn
    
    def runCycle(self, callback=None):
        ''' set NOP callback if none was passed '''
        if(callback is None):
            callback = self._nopCallback
        
        ''' calculates the time for ON '''
        duty = self._power / 100 * self._clk
        
        if(duty > self._minToggleTime):
            self._heaterOn = True
            callback(True)
            time.sleep(duty)

        if(duty < self._clk - self._minToggleTime):
            self._heaterOn = False
            callback(False)
            time.sleep(self._clk - duty)
    
    def _nopCallback(self, on):
        pass
