/**
 * @fileOverview Classes for loading the used images in the background.
 * @author <a href="mailto:marco.leise@gmx.de">Marco Leise</a>
 */

/**
 * @class Stores information about an image source and the result of loading it.
 * @constructor
 * @param {String}
 *        src The image source.
 */
function ImageInfo(src) {
	this.src = src;
	this.success = undefined;
}

/**
 * @class This class keeps a list of images and loads them in the background. It also offers a
 *        pattern slot for every image that is setup by certain methods to contain special modified
 *        versions of that image.
 * @constructor
 * @param {String}
 *        dataDir The base directory string that will be prepended to all image load requests.
 * @param {Delegate}
 *        callback A delegate that will be invoked when the loading of images has completed.
 */
function ImageManager(dataDir, callback) {
	this.dataDir = dataDir;
	this.callback = callback;
	this.info = {};
	this.images = {};
	this.patterns = {};
	this.error = '';
	this.pending = 0;
}

ImageManager.prototype.put = function(key, source) {
	this.info[key] = new ImageInfo(this.dataDir + source);
};

/**
 * Announces an image that must be loaded. Calling this method after startRequests() results in
 * unexpected behavior.
 * 
 * @param {String}
 *        source The image name relative to the data directory.
 * @see #startRequests
 */
ImageManager.prototype.add = function(source) {
};

/**
 * We clean up the state of all images that failed to download in hope that they will succeed next
 * time. This does not apply to the applet version which handles these cases internally.
 * 
 * @see Visualizer#cleanUp
 */
ImageManager.prototype.cleanUp = function() {
	for (var key in this.info) {
		if (this.info[key].success === false) {
			this.info[key].success = undefined;
			delete this.images[key];
			this.pending++;
		}
	}
	this.startRequests();
};

/**
 * Invoked once after all images have been added to start the download process.
 */
ImageManager.prototype.startRequests = function() {
	var img;
	this.error = '';
	for (var key in this.info) {
		if (this.info[key].success === undefined && !this.images[key]) {
			img = new Image();
			this.images[key] = img;
			var that = this;
			/** @ignore */
			img.key = key;
			img.onload = function() {
				that.imgHandler(this.key, true);
			};
			/** @ignore */
			img.onerror = function() {
				that.imgHandler(this.key, false);
			};
			img.onabort = img.onerror;
			img.src = this.info[key].src;
			this.pending++;
		}
	}
};

/**
 * Records the state of an image when the browser has finished loading it. If no more images are
 * pending, the visualizer is signaled.
 * 
 * @private
 * @param {HTMLImageElement}
 *        img The image that finished loading.
 * @param {Boolean}
 *        success If false, an error message for this image will be added.
 */
ImageManager.prototype.imgHandler = function(imageKey, success) {
	if (!success) {
		if (this.error) this.error += '\n';
		this.error += this.info[imageKey].src + ' did not load.';
	}
	this.info[imageKey].success = success;
	if (--this.pending == 0) {
		this.callback.invoke([ this.error ]);
	}
};

/**
 * Generates a CanvasPattern for an image, which can be used as fillStyle in drawing operations to
 * create a repeated tile texture. The new pattern overrides the current pattern slot for the image
 * and activates the pattern for drawing.
 * 
 * @param {Number}
 *        key The name of the image.
 * @param {CanvasRenderingContext2D}
 *        ctx The rendering context to create the pattern in.
 * @param {String}
 *        repeat the pattern repeat mode according to the HTML canvas createPattern() method.
 */
ImageManager.prototype.pattern = function(key, ctx, repeat) {
	if (!this.patterns[key]) {
		this.patterns[key] = ctx.createPattern(this.images[key], repeat);
	}
	ctx.fillStyle = this.patterns[key];
};



/**
 * Sets the pattern of an image to a set of colorized copies of itself. Only gray pixels will be
 * touched. The new pattern overrides the current pattern slot for the image.
 * 
 * @param {Number}
 *        key The name of the image.
 * @param {Array}
 *        colors An array of colors to use. Every array slot can be either an array of rgb values
 *        ([31, 124, 59]) or HTML color string ("#f90433").
 */
ImageManager.prototype.colorize = function(key, colors) {
	this.patterns[key] = colorize(this.images[key], colors);
};
