class Session:
    def __init__(self):
        self.recipie = None
        self.name = None
        self.volume = None
        self.comments = None
        
class Recipie:
    def __init__(self):
        self.name = None
        self.ingridients = dict({
            'barley': [''' type:amount '''],
            'hops': [''' type:amount ''']
        })
        self.mashSchema = None
        
class MashSchema:
    def __init__(self):
        self.something = 0
    
    def add(self, length, temp):
        pass
    
    def get(self, time):
        # get the step for the time stamp, return nothing at end.
        pass
    
    def remove(self, index):
        pass
    
    def length(self):
        return 0
    
    def repr(self):
        pass