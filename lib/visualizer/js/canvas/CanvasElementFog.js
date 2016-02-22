/**
 * @class A canvas element for the fog overlay.
 * @extends CanvasElement
 * @constructor
 * @param {State}
 *        state the visualizer state for reference
 * @param {CanvasElementFogPattern}
 *        pattern the fog pattern to use
 */
function CanvasElementFog(state, pattern) {
	this.upper();
	this.state = state;
	this.turn = 0;
	this.shiftX = 0;
	this.shiftY = 0;
	this.scale = 1;
	this.fogMap = null;
	this.pattern = pattern;
	this.dependsOn(pattern);
	this.ptrn = null;
}
CanvasElementFog.extend(CanvasElement);

/**
 * Causes a comparison of the relevant values that make up the visible content of this canvas
 * between the visualizer and cached values. If the cached values are out of date the canvas is
 * marked as invalid.
 *
 * @returns {Boolean} true, if the internal state has changed
 */
CanvasElementFog.prototype.checkState = function() {
	if (this.player !== this.state.fogPlayer
			|| (this.player !== undefined && ((this.state.shiftX !== this.shiftX && this.w < this.scale
					* this.state.replay.cols)
					|| (this.state.shiftY !== this.shiftY && this.h < this.scale
							* this.state.replay.rows) || this.turn !== (this.state.time | 0) || this.scale !== this.state.scale))) {
		this.invalid = true;
		this.shiftX = this.state.shiftX;
		this.shiftY = this.state.shiftY;
		this.scale = this.state.scale;
		this.turn = this.state.time | 0;
		if (this.player !== this.state.fogPlayer) {
			this.player = this.state.fogPlayer;
			if (this.player !== undefined) {
				this.ptrn = this.ctx.createPattern(this.pattern.canvas, 'repeat');
				this.ctx.clearRect(0, 0, this.w, this.h);
			}
		}
		if (this.player === undefined) {
			this.fogMap = null;
		} else {
			this.fogMap = this.state.getFogMap();
		}
	}
};

/**
 * Draws the minimal fog image required to cover the currently visible area of the map.
 */
CanvasElementFog.prototype.draw = function() {
	var x, y, rowPixels, colPixels, x_idx, y_idx, rows, cols;
	var x_i, y_i, x_f, y_f, fogRow;
	var start = null;
	if (this.fogMap) {
		this.ctx.fillStyle = this.ptrn;
		this.ctx.fillRect(0, 0, this.w, this.h);
		cols = this.fogMap[0].length;
		colPixels = this.scale * cols;
		x = (this.w < colPixels) ? ((this.w - colPixels) >> 1) + this.shiftX : 0;
		rows = this.fogMap.length;
		rowPixels = this.scale * rows;
		y = (this.h < rowPixels) ? ((this.h - rowPixels) >> 1) + this.shiftY : 0;

		x_idx = Math.floor(-x / this.scale);
		y_idx = Math.floor(-y / this.scale);

		y_i = Math.wrapAround(y_idx, rows);
		for (y_f = y + y_idx * this.scale; y_f < this.h; y_f += this.scale) {
			fogRow = this.fogMap[y_i];
			x_i = Math.wrapAround(x_idx, cols);
			for (x_f = x + x_idx * this.scale; x_f < this.w; x_f += this.scale) {
				if (fogRow[x_i] === false) {
					if (start === null) {
						start = x_f;
					}
				} else if (start !== null) {
					this.ctx.clearRect(start, y_f, x_f - start, this.scale);
					start = null;
				}
				x_i = (x_i + 1) % cols;
			}
			if (start !== null) {
				this.ctx.clearRect(start, y_f, x_f - start, this.scale);
				start = null;
			}
			y_i = (y_i + 1) % rows;
		}
	} else {
		this.ctx.clearRect(0, 0, this.w, this.h);
	}
};