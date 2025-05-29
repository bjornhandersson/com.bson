piBrew = function() {

}

piBrew.prototype.run = function() {
	$.getJSON('/api/status', function(data) {
		// todo: model should not relay on view
  		$('#response').html(data.temperature + ' °C');
	});
}

piBrew.prototype.toggle = function(on, pin, callback) {
	if(!on) {
		$.getJSON('/api/gpio/off?pin=' + pin, function(data) {
				callback(data);
		});
			
	} else {
		$.getJSON('/api/gpio/on?pin=' + pin, function(data) {
			callback(data);
		});
	}
}

piBrew.prototype.startStop = function(start) {
	if(start) {
		return $.getJSON('/api/start');
	}
	else {
		return $.getJSON('/api/stop');
	}
}

piBrew.prototype.setPID = function(KP, KI, KD) {
	return $.getJSON('/api/pid?kp=' + KP + '&ki=' + KI + '&kd=' + KD);
}

piBrew.prototype.getPID = function() {
	return $.getJSON('/api/pid');
}

piBrew.prototype.getStatus = function() {
	return $.getJSON('/api/status');
}

piBrew.prototype.setTarget = function(target) {
	return $.getJSON('/api/target?target=' + target);
}
