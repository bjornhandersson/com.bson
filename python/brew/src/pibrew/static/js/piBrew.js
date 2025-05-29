piBrew = function() {

}

piBrew.prototype.run = function() {
	$.getJSON('/service/call', function(data) {
		// todo: model should not relay on view
  		$('#response').html(data.temp + ' ' + data.unit);
	});
}

piBrew.prototype.toggle = function(on, pin, callback) {
	if(!on) {
		$.getJSON('/service/off?pin=' + pin, function(data) {
				callback(data);
		});
			
	} else {
		$.getJSON('/service/on?pin=' + pin, function(data) {
			callback(data);
		});
	}
}

piBrew.prototype.startStop = function(start) {
	if(start) {
		return $.getJSON('/service/start');
	}
	else {
		return $.getJSON('/service/stop');
	}
}

piBrew.prototype.setPID = function(KP, KI, KD) {
	return $.getJSON('/service/setPID?KP=' + KP + '&KI=' + KI + '&KD=' + KD);
}

piBrew.prototype.getPID = function() {
	return $.getJSON('/service/getPID');
}

piBrew.prototype.getStatus = function() {
	return $.getJSON('/service/getStatus');
}

piBrew.prototype.setTarget = function(target) {
	return $.getJSON('/service/setTarget?target=' + target);
}
