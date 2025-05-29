"""Web server for PiBrew control interface."""

import json
import signal
import sys
from typing import Optional

import web

from ..core.brew_controller import BrewController


class BrewWebServer:
    """Web server for controlling the brewing process."""
    
    def __init__(self, brew_controller: BrewController):
        """Initialize with a brew controller instance."""
        self.brew_controller = brew_controller
        
    def create_app(self):
        """Create and configure the web application."""
        # Store controller reference globally for handlers
        globals()['_brew_controller'] = self.brew_controller
        
        urls = (
            '/api/start', 'StartHandler',
            '/api/stop', 'StopHandler',
            '/api/status', 'StatusHandler',
            '/api/target', 'TargetHandler',
            '/api/pid', 'PIDHandler',
            '/api/gpio/on', 'GPIOOnHandler',
            '/api/gpio/off', 'GPIOOffHandler',
            '/(js|css|images|static)/(.*)', 'StaticHandler',
            '/', 'IndexHandler'
        )
        
        app = web.application(urls, {
            'StartHandler': StartHandler,
            'StopHandler': StopHandler,
            'StatusHandler': StatusHandler,
            'TargetHandler': TargetHandler,
            'PIDHandler': PIDHandler,
            'GPIOOnHandler': GPIOOnHandler,
            'GPIOOffHandler': GPIOOffHandler,
            'StaticHandler': StaticHandler,
            'IndexHandler': IndexHandler,
        })
        
        return app


class BaseHandler:
    """Base handler with common functionality."""
    
    @property
    def brew_controller(self) -> BrewController:
        """Get the brew controller from global reference."""
        return globals()['_brew_controller']
    
    def json_response(self, data: dict) -> str:
        """Return JSON response with proper headers."""
        web.header('Content-Type', 'application/json')
        web.header('Access-Control-Allow-Origin', '*')
        return json.dumps(data)
    
    def error_response(self, message: str, status_code: int = 400) -> str:
        """Return error response."""
        web.ctx.status = f'{status_code} {message}'
        return self.json_response({'error': message})


class StartHandler(BaseHandler):
    """Handle brewing start requests."""
    
    def GET(self):
        """Start the brewing process."""
        try:
            if self.brew_controller.start():
                return self.json_response({'status': 'started'})
            else:
                return self.json_response({'status': 'already_running'})
        except Exception as e:
            return self.error_response(f'Failed to start: {str(e)}', 500)


class StopHandler(BaseHandler):
    """Handle brewing stop requests."""
    
    def GET(self):
        """Stop the brewing process."""
        try:
            if self.brew_controller.stop():
                return self.json_response({'status': 'stopped'})
            else:
                return self.json_response({'status': 'not_running'})
        except Exception as e:
            return self.error_response(f'Failed to stop: {str(e)}', 500)


class StatusHandler(BaseHandler):
    """Handle status requests."""
    
    def GET(self):
        """Get current brewing status."""
        try:
            status = self.brew_controller.get_status()
            return self.json_response(status)
        except Exception as e:
            return self.error_response(f'Failed to get status: {str(e)}', 500)


class TargetHandler(BaseHandler):
    """Handle target temperature requests."""
    
    def GET(self):
        """Set target temperature."""
        try:
            args = web.input()
            if not hasattr(args, 'target'):
                return self.error_response('Missing target parameter')
            
            target = float(args.target)
            self.brew_controller.set_target_temperature(target)
            return self.json_response({'target_temperature': target})
        except ValueError:
            return self.error_response('Invalid target temperature value')
        except Exception as e:
            return self.error_response(f'Failed to set target: {str(e)}', 500)


class PIDHandler(BaseHandler):
    """Handle PID parameter requests."""
    
    def GET(self):
        """Get or set PID parameters."""
        try:
            args = web.input()
            
            # If parameters provided, set them
            if hasattr(args, 'kp') and hasattr(args, 'ki') and hasattr(args, 'kd'):
                kp = float(args.kp)
                ki = float(args.ki) 
                kd = float(args.kd)
                self.brew_controller.set_pid_parameters(kp, ki, kd)
            
            # Return current parameters
            params = self.brew_controller.get_pid_parameters()
            return self.json_response(params)
        except ValueError:
            return self.error_response('Invalid PID parameter values')
        except Exception as e:
            return self.error_response(f'Failed to handle PID: {str(e)}', 500)


class GPIOOnHandler(BaseHandler):
    """Handle GPIO on requests."""
    
    def GET(self):
        """Turn GPIO pin on."""
        try:
            args = web.input()
            if not hasattr(args, 'pin'):
                return self.error_response('Missing pin parameter')
            
            pin = int(args.pin)
            # Direct GPIO control - use with caution
            try:
                import RPi.GPIO as GPIO
            except ImportError:
                from ..hardware import gpio_mock as GPIO
            GPIO.output(pin, True)
            return self.json_response({'status': 'on', 'pin': pin})
        except Exception as e:
            return self.error_response(f'GPIO error: {str(e)}', 500)


class GPIOOffHandler(BaseHandler):
    """Handle GPIO off requests."""
    
    def GET(self):
        """Turn GPIO pin off."""
        try:
            args = web.input()
            if not hasattr(args, 'pin'):
                return self.error_response('Missing pin parameter')
            
            pin = int(args.pin)
            # Direct GPIO control - use with caution
            try:
                import RPi.GPIO as GPIO
            except ImportError:
                from ..hardware import gpio_mock as GPIO
            GPIO.output(pin, False)
            return self.json_response({'status': 'off', 'pin': pin})
        except Exception as e:
            return self.error_response(f'GPIO error: {str(e)}', 500)


class StaticHandler:
    """Handle static file requests."""
    
    def GET(self, media=None, file_req=None):
        """Serve static files."""
        try:
            if media is None:
                media = 'static'
            if file_req is None:
                file_req = 'index.html'
                
            # Set appropriate content type
            if media == 'js':
                web.header('Content-Type', 'text/javascript')
            elif media == 'css':
                web.header('Content-Type', 'text/css')
            else:
                web.header('Content-Type', 'text/html')
            
            # Try to read file from static directory
            import os
            static_path = os.path.join(os.path.dirname(__file__), '..', 'static', media, file_req)
            
            if os.path.exists(static_path):
                with open(static_path, 'r') as f:
                    return f.read()
            else:
                # Fallback to original location for backward compatibility
                fallback_path = os.path.join(media, file_req)
                if os.path.exists(fallback_path):
                    with open(fallback_path, 'r') as f:
                        return f.read()
                        
            web.ctx.status = '404 Not Found'
            return 'File not found'
        except Exception as e:
            web.ctx.status = '500 Internal Server Error'
            return f'Error serving file: {str(e)}'


class IndexHandler:
    """Handle index page requests."""
    
    def GET(self):
        """Serve the main index page."""
        return StaticHandler().GET('html', 'index.html')


def create_app(brew_controller: BrewController):
    """Create web application with brew controller."""
    server = BrewWebServer(brew_controller)
    return server.create_app()


def run_server(brew_controller: BrewController, host: str = '0.0.0.0', port: int = 8080):
    """Run the web server."""
    def signal_handler(signum, frame):
        """Handle shutdown signals."""
        print('\nShutting down server...')
        brew_controller.cleanup()
        sys.exit(0)
    
    # Register signal handlers
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)
    
    # Create and run app
    app = create_app(brew_controller)
    web.config.debug = False
    
    print(f"Starting PiBrew server on {host}:{port}")
    print(f"Web interface: http://{host}:{port}")
    
    # Override web.py's default host/port
    sys.argv = ['server.py', f'{host}:{port}']
    app.run()