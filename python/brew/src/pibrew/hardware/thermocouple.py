import time

try:
    import RPi.GPIO as GPIO
except ImportError:
    from . import gpio_mock as GPIO

LITTLE_END = 0
BIG_END = 1

'''
Read temperature out of MAX31855 thermocuple amplifier type K.
Data sheet: http://www.maximintegrated.com/datasheet/index.mvp/id/7273
Format: data out (feed through MAX31855 pin marked as do)
            returns 32bit 
                0 - 13 temperature signed 14-bit, 
                14 reserved always 0
                15 error bit (1 error 0 ok), 
                16 - 28 cold junction temperature signed 12 bit
                29, 30, 31 error type

'''
class MAX31855:
    def __init__(self, dataInPin=21, clkPin=23, csPin=24):
        ''' defaults data: MISO(21), clock:clk(23) cs:ce0(24) '''
        self._data = dataInPin
        self._clk = clkPin
        self._cs = csPin
        self.error = None
        
        GPIO.setup(self._data, GPIO.IN)
        GPIO.setup(self._cs, GPIO.OUT)
        GPIO.setup(self._clk, GPIO.OUT)

        ''' set channel select off '''
        GPIO.output(self._cs, True)

    def readTempC(self):
        ''' returns temperature in Celsius or MAX31855Exceptino '''
        value = self._read()
        if(value == None): 
            return None
        
        temp = (value >> 18) & 0x3fff # get first 14bit 
        if (temp & (1 << 14)): # + or -
            temp = -(~temp + 1 & 0x1fff)
        return temp * 0.25; #to Celsius

    def readTempInternal(self):
        ''' returns reference junction temperature in Celsius or MAX31855Exceptino'''
        value = self._read()
        if(value == None):
            return None
        
        temp = (value >> 4) & 0xfff
        if (temp & (1 << 12)): # + or -
            temp = -(~temp + 1 & 0x7ff)
        return temp * 0.0625

    def _read(self):
        # reset error
        self.error = None
        
        # set select channel
        GPIO.output(self._cs, False)
        
        # read 32-bit
        value = self.shiftIn(self._data, self._clk, BIG_END, 32)
        
        # unselect channel
        GPIO.output(self._cs, True)
        
        # we have 15th bit high => error
        if (value & (1 << 16)):
            if (value & (1 << 2)):
                raise MAX31855Exception("Short to VCC")
            elif (value & (1 << 1)):
                raise MAX31855Exception("Short to GND")
            else:
                raise MAX31855Exception("No Connection")
            raise MAX31855Exception("Unknown error")
        
        # todo: raise error instead of None
        return value
     
    def shiftIn(self, dataPin, clkPin,  order, count=32) :
        ''' 
        dataPin:   input pin, 
        clkPin:    clock pin, 
        order:     BIGENDIAN or LITTLEENDIAN, 
        count:     number of bytes to read
        '''
        
        value = 0
        if (order == BIG_END):
            for i in range(count -1, -1, -1):
                #clock high
                GPIO.output(clkPin, True)
                # read out value
                value |= GPIO.input(dataPin) << i
                #clock low
                GPIO.output(clkPin, False)
        else:
            for i in range(0, count):
                GPIO.output(clkPin, True)
                ival = GPIO.input(dataPin)
                value |= ival << i
                GPIO.output(clkPin, False)
     
        return value
    
class MAX31855Exception(Exception):
    def __init__(self, value):
        self.value = value
    def __str__(self):
        return repr(self.value)
    
if __name__ == "__main__":
    MISO = 21
    CLK = 23
    CH = 26
    
    GPIO.cleanup()
    GPIO.setmode(GPIO.BOARD)
    
    thermocouple = MAX31855(MISO, CLK, CH)
    while(True):
        try:
            print("tc: {} and rj: {} err: {}".format(
                thermocouple.readTempC(),
                thermocouple.readTempInternal(),
                thermocouple.error
            ))
            time.sleep(.5)
        except KeyboardInterrupt:
            GPIO.cleanup()
            break
