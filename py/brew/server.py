#!/usr/bin/env python

import web
import json
import signal
import sys
import brew

    
class on:
    def GET(self):
        try:
            pin = int(web.input().pin)
            piBrew.GPIO_on(pin)
            return json.dumps({'status':'on'})
        except Exception as e:
            print(f"ERROR: {e}")
            return json.dumps({'status': 'ERROR'})
        
class off:
    def GET(self):
        try:
            pin = int(web.input().pin)
            piBrew.GPIO_off(pin)
            return json.dumps({'status':'off'})
        except Exception as e:
            print(f"ERROR: {e}")
            return json.dumps({'status':'ERROR'})
        
class start:
    def GET(self):
        piBrew.start()   
        return json.dumps({'running': piBrew.isStarted})

class stop:
    def GET(self):
        piBrew.stop()
        return json.dumps({'running': not piBrew.isStarted})

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
        piBrew.setTarget(float(web.input().target))
    
class index:
    def GET(self):
        try:
            web.header('Content-type', 'text/html')
            with open('html/index.html', 'r', encoding='utf-8') as f:
                return f.read()
        except FileNotFoundError:
            return 'File not found'
        except Exception as e:
            return f'Error: {e}'

class static:
    def GET(self, media, fileReq):
        try:
            if fileReq is None:
                fileReq = 'index.html'
            if media == 'js':
                web.header('Content-type', 'text/javascript')
            else:
                web.header('Content-type', 'text/html')
            with open(f'{media}/{fileReq}', 'r', encoding='utf-8') as f:
                return f.read()
        except FileNotFoundError:
            return ''
        except Exception as e:
            print(f"Static file error: {e}")
            return ''
        
def teardown(signal, frame):
    print('Exit')
    piBrew.stop()
    #GPIO.cleanup() #wait for thread to exit
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
            '/(html)/(.*)', 'static',
            '/', 'index'
    )
    
    web.config.debug = False
    app = web.application(urls, globals())
    app.run()
    
            
        