#!/usr/bin/env python

from __future__ import division
import web
import json
import signal
import sys
import brew

    
class on:
    def GET(self):
        try:
            pin = int(web.input().pin)
            print("On")
            piBrew.GPIO_on(pin)
            return json.dumps({'status':'on'})
        except:
            print("ERROR")
            return json.dumps({'status': 'ERROR'})
        
class off:
    def GET(self):
        try:
            pin = int(web.input().pin)
            print("Off")
            piBrew.GPIO_off(pin)
            return json.dumps({'status':'off'})
        except:
            print("ERROR")
            return json.dumps({'status':'ERROR'})
        
class start:
    def GET(self):
        if(piBrew.isStarted == False):
            piBrew.start()   
        return json.dumps({'started': True})

class stop:
    def GET(self):
        if(piBrew.isStarted == True):
            piBrew.stop()   
        return json.dumps({'stopped': True})

class setPID:
    def GET(self):
        args = web.input() 
        piBrew.pid.Kp = float(args.KP)
        piBrew.pid.Ki = float(args.KI)
        piBrew.pid.Kd = float(args.KD)

class getPID:
    def GET(self):
        return json.dumps({
            'KP': float(piBrew.pid.Kp),
            'KI': float(piBrew.pid.Ki),
            'KD': float(piBrew.pid.Kd)
        })

class getStatus:
    def GET(self):
        return json.dumps(piBrew.getStatus())

class setTarget:
    def GET(self):
        piBrew.targetTemp = float(web.input().target)
    
class static:
    def GET(self, media=None, fileReq=None):
        try:
            if media is None:
                media = 'html'
            if(fileReq is None):
                fileReq = 'index.html'
            if media == 'js':
                web.header('Content-type', 'text/javascript')
            else:
                web.header('Content-type', 'text/html')
            f = open(media + '/'+ fileReq, 'r')
            return f.read()
        except:
            return ''
        
def teardown(signal, frame):
    print('Exit')
    piBrew.stop()
    sys.exit(0)
    
if __name__ == "__main__":
    signal.signal(signal.SIGINT, teardown)

    piBrew = brew.Brew()
    
    urls = (
            '/service/on', 'on',
            '/service/off', 'off',
            '/service/start', 'start',
            '/service/stop', 'stop',
            '/service/getPID', 'getPID',
            '/service/setPID', 'setPID',
            '/service/setTarget', 'setTarget',
            '/service/getStatus', 'getStatus',
            '/(js|js/float|css|images|html)/(.*)', 'static',
            '/', 'static'
    )
    
    web.config.debug = False
    app = web.application(urls, globals())
    app.run()
    
            
        