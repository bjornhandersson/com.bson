import time
import _thread

class Kettle:
    def __init__(self):
        self.temp = 0
        self._isHeating = False
        self._isRunning = False       
        self._heatPerSec = 0.06
        self._coolPerSec = -0.03
        self._clk = 0.1
        self._slowFactor = 0.0005
        self._loopCnt = 0
        
    def heat(self):
        #self._loopCnt = 0
        self._isHeating = True
        if(self._isRunning == False):
            self._isRunning = True
            _thread.start_new_thread(self._worker, (self,))
            
    def cool(self):
        #self._loopCnt = 0
        self._isHeating = False
        if(self._isRunning == False):
            self._isRunning = True
            _thread.start_new_thread(self._worker, (self,))
                  
    def _worker(self, a):
        try:
            while(self._isRunning):
                
                if(self._isHeating == True):
                    if(self._loopCnt < 60):
                        self._loopCnt +=1
                    if(self.temp < 100):
                        self.temp += (self._clk * self._heatPerSec) + (self._slowFactor * self._loopCnt)
                else:
                    if(self._loopCnt > -6):
                        self._loopCnt -=1;
                    if(self.temp > 0):
                        self.temp += (self._clk * self._coolPerSec) + (self._slowFactor * self._loopCnt)
                time.sleep(self._clk)
        except:
            print('Kettle error')
            return