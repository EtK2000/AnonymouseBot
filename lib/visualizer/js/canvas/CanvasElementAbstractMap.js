/**
 * @class Base class for maps
 * @extends CanvasElement
 * @constructor
 * @param {State}
 *        state the visualizer state for reference
 */
function CanvasElementAbstractMap(state) {
	this.upper();
	this.state = state;
	this.water = null;
    this.zone = null;
}
CanvasElementAbstractMap.extend(CanvasElement);

/**
 * Draws a red marker on the map. Used when coordinates are given in the replay URL.
 *
 * @param {Number}
 *        xs the x pixel position
 * @param {Number}
 *        ys the y pixel position
 */
CanvasElementAbstractMap.prototype.redFocusRectFun = function(xs, ys) {
	var x, y, w, i;
	for (i = 0; i < 5; i++) {
		this.ctx.strokeStyle = 'rgba(255,0,0,' + (i + 1) / 5 + ')';
		w = this.scale + 9 - 2 * i;
		x = xs + i;
		y = ys + i;
		this.ctx.strokeRect(x, y, w, w);
	}
};

/**
 * Draws the terrain map.
 */
CanvasElementAbstractMap.prototype.draw = function() {
	var row, col, xs, ys;
	var zones, playerZone, z, x1, y1;
	var rows = this.state.replay.rows;
	var cols = this.state.replay.cols;
	var rowOpt = this.state.options['row'];
	var colOpt = this.state.options['col'];

	// here we want to draw the zone over the water
	if (this.state.config['showZones']) {
		zones = this.state.replay.meta['replaydata']['zones'];
		for (var i = 0; i < zones.length; i++) {
			playerZone = zones[i];
			for (var j = 0; j < playerZone.length; j++) {
				z = playerZone[j];
				x1 = (z[1]) * this.scale;
				y1 = (z[0]) * this.scale;
				// TODO zones fill style
				this.ctx.globalAlpha = 0.3;
				this.ctx.fillStyle = this.state.options.playercolors[i];
				this.ctx.fillRect(x1, y1, this.scale, this.scale);
				this.ctx.globalAlpha = 1;
			}
		}
	}

	// show grid
	if (this.state.config['showGrid']) {
		this.ctx.beginPath();
		this.ctx.lineWidth = 0.2;

		for (i = 0; i <= rows; i++) {
			var offset = 0.7;
			this.ctx.moveTo(0, (i - offset) * this.scale);
			this.ctx.lineTo(cols * this.scale, (i - offset) * this.scale);
		}
		for (i = 0; i <= cols; i++) {
			this.ctx.moveTo(i * this.scale, 0);
			this.ctx.lineTo(i * this.scale, cols * this.scale);
		}
		this.ctx.strokeStyle = "black";
		this.ctx.stroke();
	}

	// marker
	if (!isNaN(rowOpt) && !isNaN(colOpt)) {
		xs = (colOpt % cols) * this.scale - 4.5;
		ys = (rowOpt % rows) * this.scale - 4.5;
		this.drawWrapped(xs, ys, this.scale + 9, this.scale + 9, this.w, this.h,
				this.redFocusRectFun, [ xs, ys ]);
	}
};
