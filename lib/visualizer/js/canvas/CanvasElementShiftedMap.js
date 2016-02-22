/**
 * @class The main map with pirates, dragged with the mouse and extended by borders if required
 * @extends CanvasElement
 * @constructor
 * @param {State}
 *        state the visualizer state for reference
 * @param {CanvasElementPiratesMap}
 *        piratesMap the prepared map with pirates
 */
function CanvasElementShiftedMap(state, piratesMap) {
	this.upper();
	this.state = state;
	this.piratesMap = piratesMap;
	this.dependsOn(piratesMap);
	this.shiftX = 0;
	this.shiftY = 0;
	this.fade = undefined;
	this.padding = 0;
}
CanvasElementShiftedMap.extend(CanvasElement);

/**
 * Causes a comparison of the relevant values that make up the visible content of this canvas
 * between the visualizer and cached values. If the cached values are out of date the canvas is
 * marked as invalid.
 *
 * @returns {Boolean} true, if the internal state has changed
 */
CanvasElementShiftedMap.prototype.checkState = function() {
	if (this.state.shiftX !== this.shiftX || this.state.shiftY !== this.shiftY
			|| this.state.fade !== this.fade || this.state.time !== this.time) {
		this.invalid = true;
		this.shiftX = this.state.shiftX;
		this.shiftY = this.state.shiftY;
		this.fade = this.state.fade;
		this.time = this.state.time;
	}
};

/**
 * Draws the visible portion of the map with pirates. If the map is smaller than the view area it is
 * repeated in a darker shade on both sides.
 */
CanvasElementShiftedMap.prototype.draw = function() {
	var dx, dy, cutoff;
	var mx = ((this.w - this.piratesMap.w) >> 1);
	var my = this.padding;

	dx = mx + this.shiftX;
	dy = my + this.shiftY;

	var bw = this.piratesMap.canvas.width + (2 * this.padding);
	var bh = this.piratesMap.canvas.height + (2 * this.padding);
	//this.ctx.drawImage(this.piratesMap.map.boardA, dx - this.padding, dy - this.padding, bw, bh);
	this.ctx.drawImage(this.piratesMap.canvas, dx, dy);

	if (this.fade) {
		this.ctx.fillStyle = this.fade;
		this.ctx.fillRect(0, 0, this.w, this.h);
	}

	// game cut-off reason
	cutoff = this.state.replay.meta['replaydata']['cutoff'];
	if (this.time > this.state.replay.duration - 1 && cutoff) {
		cutoff = '"' + cutoff + '"';
		this.ctx.font = FONT;
		dx = 0.5 * (this.w - this.ctx.measureText(cutoff).width);
		dy = this.h - 45;
		this.ctx.lineWidth = 4;
		this.ctx.strokeStyle = '#000';
		this.ctx.strokeText(cutoff, dx, dy);
		this.ctx.fillStyle = '#fff';
		this.ctx.fillText(cutoff, dx, dy);
	}
};