/**
 * @class A tiny canvas to contain a cached fog pattern for the selected player.
 * @extends CanvasElement
 * @constructor
 * @param {State}
 *        state the visualizer state for reference
 */
function CanvasElementFogPattern(state) {
	this.upper();
	this.state = state;
	this.player = undefined;
	this.setSize(2, 2);
}
CanvasElementFogPattern.extend(CanvasElement);

/**
 * Causes a comparison of the relevant values that make up the visible content of this canvas
 * between the visualizer and cached values. If the cached values are out of date the canvas is
 * marked as invalid.
 *
 * @returns {Boolean} true, if the internal state has changed
 */
CanvasElementFogPattern.prototype.checkState = function() {
	if (this.player !== this.state.fogPlayer) {
		this.invalid = true;
		this.player = this.state.fogPlayer;
	}
};

/**
 * Draws the 2x2 pixel pattern.
 */
CanvasElementFogPattern.prototype.draw = function() {
	if (this.player !== undefined) {
		this.ctx.fillStyle = this.state.options.playercolors[this.player];
		this.ctx.fillRect(0, 0, 1, 1);
		this.ctx.fillRect(1, 1, 1, 1);
	}
};