/**
 * @fileOverview This file contains the stack of off-screen images that are rendered on top and into
 *               each other to create the final display.
 * @author <a href="mailto:marco.leise@gmx.de">Marco Leise</a>
 */

/**
 * @class A canvas that serves as an off-screen buffer for some graphics to be displayed possibly in
 *        tandem with other canvas elements or graphics.
 * @constructor
 */
function CanvasElement() {
	this.canvas = document.createElement('canvas');
	this.ctx = this.canvas.getContext('2d');
	this.invalid = true;
	this.resized = false;
	this.x = 0;
	this.y = 0;
	this.w = this.canvas.width;
	this.h = this.canvas.height;
	this.dependencies = [];
	this.invalidates = [];
}

/**
 * Sets the size of this canvas and invalidates it, if an actual change is detected.
 * 
 * @param {Number}
 *        width the new width
 * @param {Number}
 *        height the new height
 */
CanvasElement.prototype.setSize = function(width, height) {
	if (this.w !== width || this.h !== height) {
		this.w = width;
		this.h = height;
		if (width > 0 && height > 0) {
			this.canvas.width = width;
			this.canvas.height = height;
		}
		this.invalid = true;
		this.resized = true;
	}
};

/**
 * Checks if a coordinate pair is within the canvas area. The canvas' x and y properties are used as
 * it's offset.
 * 
 * @param {Number}
 *        x the x coordinate in question
 * @param {Number}
 *        y the y coordinate in question
 * @returns {Boolean} true, if the coordinates are contained within the canvas area
 */
CanvasElement.prototype.contains = function(x, y) {
	return (x >= this.x && x < this.x + this.w && y >= this.y && y < this.y + this.h);
};

/**
 * Ensures that the contents of the canvas are up to date. A redraw is triggered if necessary.
 * 
 * @returns {Boolean} true, if the canvas had to be redrawn
 */
CanvasElement.prototype.validate = function() {
	var i;
	for (i = 0; i < this.dependencies.length; i++) {
		if (this.dependencies[i].validate()) this.invalid = true;
	}
	this.checkState();
	if (this.invalid) {
		this.draw(this.resized);
		this.invalid = false;
		this.resized = false;
		return true;
	}
	return false;
};

/**
 * Causes a comparison of the relevant values that make up the visible content of this canvas
 * between the visualizer and cached values. If the cached values are out of date the canvas is
 * marked as invalid.
 * 
 * @returns {Boolean} true, if the internal state has changed
 */
CanvasElement.prototype.checkState = function() {
// default implementation doesn't invalidate
};

/**
 * Makes another canvas a dependency of this one. This will cause this canvas to be invalidated if
 * the dependency becomes invalid and will cause this canvas to validate the dependency before
 * attempting to validate itself. Do not create cyclic dependencies!
 * 
 * @param {CanvasElement}
 *        element the dependency
 */
CanvasElement.prototype.dependsOn = function(element) {
	this.dependencies.push(element);
	element.invalidates.push(this);
};

/**
 * For cases where a drawn object would cross the border of the canvas and it is desirable to have
 * it wrap around and come in again on the other side, this method can be called with a given
 * function that contains the drawing commands. The wrapping will be simulated by repeatedly calling
 * the function and using matrix translations on the drawing context in between.
 * 
 * @param {Number}
 *        x the left coordinate
 * @param {Number}
 *        y the top coordinate
 * @param {Number}
 *        w the drawing width
 * @param {Number}
 *        h the drawing height
 * @param {Number}
 *        wField the width of the whole field on which wrapping should occur
 * @param {Number}
 *        hField the height of the field
 * @param {Function}
 *        func the drawing routine
 * @param {Array}
 *        args parameters for the drawing routine
 */
CanvasElement.prototype.drawWrapped = function(x, y, w, h, wField, hField, func, args) {
    var delta_x, delta_y, tx, ty, sum;
	if (x < 0 || y < 0 || x + w > wField || y + h > hField) {
		this.ctx.save();
		delta_x = -Math.floor((x + w) / wField) * wField;
		delta_y = -Math.floor((y + h) / hField) * hField;
		this.ctx.translate(delta_x, delta_y);
		for (ty = y + delta_y; ty < hField; ty += hField) {
			sum = 0;
			for (tx = x + delta_x; tx < wField; tx += wField) {
				func.apply(this, args);
				this.ctx.translate(wField, 0);
				sum -= wField;
			}
			this.ctx.translate(sum, hField);
		}
		this.ctx.restore();
	} else {
		func.apply(this, args);
	}
};

/**
 * @class A helper class to transfer statistical values inside {@link CanvasElement} descendants.
 * @constructor
 * @param values
 *        {Array} Statistical values for every player and turn.
 * @param bonus
 *        {Array} The bonus that will be added to each player's values at the end of the replay. Can
 *        be undefined and is used for the 'scores' statistical item.
 * @property values {Array}
 * @property bonus {Array}
 */
function Stats(values, bonus) {
	this.values = values;
	this.bonus = bonus;
}
