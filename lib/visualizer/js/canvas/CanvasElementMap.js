/**
 * @class A canvas element for the main map.
 * @extends CanvasElementAbstractMap
 * @constructor
 * @param {State}
 *        state the visualizer state for reference
 */
function CanvasElementMap(state) {
	this.upper(state);
}
CanvasElementMap.extend(CanvasElementAbstractMap);

/**
 * Causes a comparison of the relevant values that make up the visible content of this canvas
 * between the visualizer and cached values. If the cached values are out of date the canvas is
 * marked as invalid.
 *
 * @returns {Boolean} true, if the internal state has changed
 */
CanvasElementMap.prototype.checkState = function() {
	if (this.scale !== this.state.scale) {
		this.invalid = true;
		this.scale = this.state.scale;
	}
};