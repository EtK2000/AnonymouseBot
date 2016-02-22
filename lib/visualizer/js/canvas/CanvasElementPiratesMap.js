/**
 * @class The main map including pirates and indicators
 * @extends CanvasElement
 * @constructor
 * @param {State}
 *        state the visualizer state for reference
 * @param {CanvasElementMap}
 *        map the background map
 * @param {CanvasElementFog}
 *        fog the fog overlay
 */
function CanvasElementPiratesMap(state, map, fog) {
	this.upper();
	this.state = state;
	this.map = map;
	this.fog = fog;
	this.dependsOn(map);
	this.dependsOn(fog);
	this.time = 0;
	this.pirates = [];
	this.drawStates = new Object();
	this.pairing = [];
	this.scale = 1;
	this.circledPirates = [];
	this.mouseOverVis = false;
	this.mouseCol = 0;
	this.mouseRow = 0;
}
CanvasElementPiratesMap.extend(CanvasElement);

// set this to determine the offset of the directional-ship images
var orientation={'w':0, 'n':1, 'e':2, 's':3};

// add slow motion for the next 3 turns if an attack is about to happen
CanvasElementPiratesMap.prototype.attackAboutToHappen = function() {
    //todo: check if not out of bounds of drunk history (instead of condition in director)
    for (i = this.pirates.length - 1; i >= 0; i--) {
        for (j = 2; j >= 0; j--) {
            if (this.pirates[i].drinkHistory.charAt(j + this.turn - Math.round(this.pirates[i].time) - 1) === '0' && this.pirates[i].drinkHistory.charAt(j + this.turn - Math.round(this.pirates[i].time)) === '1') {
                return true;
            }
        }
    }
    return false;
}


/**
 * Causes a comparison of the relevant values that make up the visible content of this canvas
 * between the visualizer and cached values. If the cached values are out of date the canvas is
 * marked as invalid.
 *
 * @returns {Boolean} true, if the internal state has changed
 */
CanvasElementPiratesMap.prototype.checkState = function() {
	var i, k, kf, p_i, p_k, dx, dy, rows, cols, ar, owner;
	var hash = undefined;
	var timeChanged = this.time !== this.state.time;
	if (timeChanged || this.scale !== this.state.scale || this.label !== this.state.config['label']) {
		this.invalid = true;
		this.time = this.state.time;
		this.scale = this.state.scale;
		this.label = this.state.config['label'];

		// per turn calculations
		if (this.turn !== (this.time | 0)) {
			cols = this.state.replay.cols;
			rows = this.state.replay.rows;
			this.turn = this.time | 0;
			this.pirates = this.state.replay.getTurn(this.turn);
			this.pairing = new Array(this.pirates.length);
			for (i = this.pirates.length - 1; i >= 0; i--) {
				if ((kf = this.pirates[i].interpolate(this.turn-1))) {
					owner = kf['owner'];
					kf = this.pirates[i].interpolate(this.turn);
					this.pairing[this.pirates[i].id] = {
						kf : kf,
						owner : owner,
						x : Math.wrapAround(kf['x'], cols),
						y : Math.wrapAround(kf['y'], rows),
						targets : []
					};
				}
			}
			//if ((ar = this.state.replay.meta['replaydata']['attackradius2'])) {
            for (i = this.pirates.length - 1; i >= 0; i--) {
                if (this.turn == 105 && this.pirates[i].gameId == 3 && this.pirates[i].owner == 0) { //the attacked pirate
                    var za = 1;
                }
                if (this.turn == 45 && this.pirates[i].gameId == 3 && this.pirates[i].owner == 1) { //the attacked pirate
                    var zb = 1;
                }
                if (this.pirates[i].drinkHistory[this.turn - Math.round(this.pirates[i].time) - 1] === '0' && this.pirates[i].drinkHistory[this.turn - Math.round(this.pirates[i].time)] === '1') { //pirate that died this turn
                    p_i = this.pairing[this.pirates[i].id];
                    if (p_i !== undefined && p_i.owner !== undefined) { // pirate is defined and has an owner
                        for (k = this.pirates.length - 1; k >= 0; k--) { //k is the attacker
                        	var ar = this.pirates[k].attackRadius
                            if (this.pirates[k].owner !== p_i.owner) { // attacker is of different owner
                                attackIndex = this.pirates[k].attackHistory.indexOf(this.turn)
                                if (attackIndex != -1 && this.pirates[k].attackHistory[attackIndex + 1] === this.pirates[i].gameId) {
                                    p_k = this.pairing[this.pirates[k].id];
                                    // distance between pirates' end-points
                                    dx = p_k.x - p_i.x;
                                    //if (2 * dx > cols) dx -= cols;
                                    dy = p_k.y - p_i.y;
                                    //if (2 * dy > rows) dy -= rows;
                                    if (dx * dx + dy * dy <= ar) {
                                        // these two pirates will be in attack
                                        // range
                                        //p_i.targets.push(p_k.kf);
                                        p_k.targets.push(p_i.kf);
                                    }
                                }
                            }
                        }
                    }
                }
            }
		}

		// interpolate pirates for this point in time
		this.drawStates = new Object();
		for (i = this.pirates.length - 1; i >= 0; i--) {
			if ((kf = this.pirates[i].interpolate(this.time))) {
				hash = '#';
				hash += INT_TO_HEX[kf['r']];
				hash += INT_TO_HEX[kf['g']];
				hash += INT_TO_HEX[kf['b']];
				kf.calcMapCoords(this.scale, this.w, this.h);
				if (!this.drawStates[hash]) this.drawStates[hash] = [];
				this.drawStates[hash].push(kf);
			}
		}
	}

	// find pirates in range of mouse cursor
	if (this.mouseOverVis !== this.state.mouseOverVis
			|| this.mouseOverVis
			&& (timeChanged || this.mouseCol !== this.state.mouseCol || this.mouseRow !== this.state.mouseRow)) {
		this.mouseOverVis = this.state.mouseOverVis;
		this.mouseCol = this.state.mouseCol;
		this.mouseRow = this.state.mouseRow;
		if (this.collectPiratesAroundCursor()) this.invalid = true;
	}
};

/**
 * Builds the internal list of pirates and food that need a circle drawn around them because the mouse
 * cursor is within their radius of effect (either attack or spawn).
 *
 * @returns {Boolean} true, if the internal list has changed since the last call of this method
 */
CanvasElementPiratesMap.prototype.collectPiratesAroundCursor = function() {
	var col, row, ar, sr, colPixels, rowPixels, drawList, i, k, pirate, d, owned;
	var found;
	var circledPirates = [];
	var hash = undefined;
	var same = true;
	if (this.mouseOverVis) {
		col = this.scale * this.mouseCol;
		row = this.scale * this.mouseRow;
		ar = this.state.replay.meta['replaydata']['attackradius2'];
		ar *= this.scale * this.scale;
		sr = this.state.replay.meta['replaydata']['spawnradius2'];
		sr *= this.scale * this.scale;
		colPixels = this.scale * this.state.replay.cols;
		rowPixels = this.scale * this.state.replay.rows;
		for (hash in this.drawStates) {
			drawList = this.drawStates[hash];
			for (i = drawList.length - 1; i >= 0; i--) {
				pirate = drawList[i];
				d = Math.dist_2(col, row, pirate.mapX, pirate.mapY, colPixels, rowPixels);
				owned = pirate['owner'] !== undefined;
//				ar = this.pirates[pirate.pirateId].attackRadius;
//		        ar *= this.scale * this.scale;
				if (!owned && (d <= sr) || owned && (d <= ar)) {
					if (same) {
						found = false;
						for (k = 0; k < this.circledPirates.length; k++) {
							if (this.circledPirates[k] === pirate) {
								found = true;
								break;
							}
						}
						same &= found;
					}
					circledPirates.push(pirate);
				}
			}
		}
	}
	same &= circledPirates.length === this.circledPirates.length;
	if (same) return false;
	this.circledPirates = circledPirates;
	return true;
};

CanvasElementPiratesMap.prototype.drawLighthouses = function() {
	var lighthouses = this.state.replay.meta['replaydata']['lighthouses'];
	if (lighthouses) {
		for (var i = 0; i < lighthouses.length; i++) {
			var lighthouse = lighthouses[i];
			var x1 = (lighthouse[1] - 0.75 ) * this.scale;
			var y1 = (lighthouse[0] - 1.5) * this.scale;
			var w = 2.5 * this.scale;
			this.ctx.drawImage(this.map.lighthouse, x1, y1, w, w);
		}
	}
};

CanvasElementPiratesMap.prototype.drawInScale = function(image, sX, sY, sWidth, sHeight, x, y, width, height, scaleSize) {
	var halfScale = 0.5 * this.scale;
	width = width * scaleSize;
	height = height * scaleSize;
	x = x + halfScale - (width / 2);
	y = y + halfScale - (height / 2);
    try {
        this.ctx.drawImage(image, sX, sY, sWidth, sHeight, x, y, width, height);
    }
    catch(err) {
        console.log(err.message);
    }
};

CanvasElementPiratesMap.prototype.drawPirates = function() {
	var attackRadius = this.state.replay.meta['replaydata']['attackradius2'];
	attackRadius = this.scale * Math.sqrt(attackRadius);
	for (var hash in this.drawStates) {
		this.ctx.fillStyle = hash;
		var drawList = this.drawStates[hash];
        drawList.sort(function(a, b) {return a.mapY < b.mapY}); //pirates. draws from top to bottom
		for (n = drawList.length - 1; n >= 0; n--) {
			var kf = drawList[n]; //key frame of nth pirate. Pirate object defined in Pirate.js
			var treasureHistoryIndex = this.turn - Math.round(kf.pirate.time) - 1;
			var hasTreasure = kf.pirate.treasureHistory[treasureHistoryIndex]; // this should give us the treasure history
			var isDrunk = kf.pirate.drinkHistory[this.turn - Math.round(kf.pirate.time) - 1]; // the drunk state history
			var isDefending = (kf.pirate.defenseHistory.indexOf(this.turn + 1) !== -1);
			if (kf['owner'] !== undefined) {
				this.ctx.globalAlpha = kf['cloaked'];
				// while the size == 1 the pirate is alive and well. on it's death turn the size is faded gradually to 0. we use this to know when the pirate is dying
				if (kf['size'] == 1 || kf.reasonOfDeath === undefined || kf.reasonOfDeath == '') {

					// the regular drawing of a pirate
					var or = orientation[kf['orientation']];
					var scaleSize = 1.8;
					var shipImage;
					if (kf['owner'] == 0) {
						shipImage = this.map.ship1;
					}
					else {
						shipImage = this.map.ship2;
					}
					if (isDrunk == '1') {
					    or = orientation[['e', 'n', 'w', 's'][this.turn % 4]]; //rotate the drunk ship
                    	this.ctx.globalAlpha = 0.9;
					    scaleSize = 1.7;
					}
                    /*
					if (isDefending) {
					    shipImage = this.map.shipDefend;
					}*/
					this.drawWrapped(kf.mapX, kf.mapY, this.scale, this.scale, this.w, this.h,
                        function (x, y, width) {
                            this.drawInScale(
                                shipImage,
                                // orientation - row of image (different stages)
                                0, or * 200, 200, 200, x-2.5, y-3, width, width, scaleSize);
                        },
                        [kf.mapX, kf.mapY, this.scale * kf['size']]);
					if (isDrunk == '1') {
                        this.drawWrapped(kf.mapX, kf.mapY, this.scale, this.scale, this.w, this.h,
                            function (x, y, width) {
                                this.drawInScale(
                                    this.map.drunk,
                                    // orientation - row of image (different stages)
                                    0, or * 200, 200, 200, x-2.5, y-4, width, width, scaleSize);
                            },
                            [kf.mapX, kf.mapY, this.scale * kf['size']]);
                    } else if (hasTreasure == '1') {
                        this.drawWrapped(kf.mapX, kf.mapY, this.scale, this.scale, this.w, this.h,
                            function (x, y, width) {
                                this.drawInScale(
                                    this.map.treasureOnShip,
                                    // orientation - row of image (different stages)
                                    0, or * 200, 200, 200, x-2.5, y-4, width, width, scaleSize);
                            },
                            [kf.mapX, kf.mapY, this.scale * kf['size']]);
                    }
				} else {
					console.log('unknown reason of death!');
				}

				if (this.state.config['showRange'] && kf['cloaked'] === 1) {
					this.ctx.globalAlpha = 0.1;
					this.ctx.beginPath();
					this.ctx.strokeStyle = this.state.options.playercolors[kf['owner']];
					this.ctx.arc(kf.mapX + this.scale / 2, kf.mapY + this.scale / 2, attackRadius, 0, 2 * Math.PI, false);
					this.ctx.fill();
				}
			} else {
				var w = this.scale;
				var dx = kf.mapX;
				var dy = kf.mapY;
				if (kf['size'] !== 1) {
					var d = 0.5 * (1.0 - kf['size']) * this.scale;
					dx += d;
					dy += d;
					w *= kf['size'];
				}
				this.ctx.fillRect(dx, dy, w, w);
			}
		}
	}
	this.ctx.globalAlpha = 1;
};

CanvasElementPiratesMap.prototype.drawBlockedMoves = function() {
	var rejected = this.state.replay.meta['replaydata']['rejected'];
	for (var i = 0; i < rejected.length; i++) {
		var rej = rejected[i];
		if (rej[0] > this.turn + 1) {
			// no need to check this anymore - only relevant in future
			break;
		}
		if ((rej[0] > this.time) && (rej[0] < (this.time + 1))) {
			var centerx = (rej[2] + 0.5) * this.scale;
			var centery = (rej[1] + 0.5) * this.scale;
			var dir = undefined;
			var interpol = 1 - rej[0] + this.time;
			dir = Direction.fromChar(rej[3]);
			this.ctx.lineWidth = 4;
			this.ctx.strokeStyle = "#FF0000";
			this.ctx.beginPath();
			// make this line a little bigger
			this.ctx.arc(centerx, centery, this.scale * 0.5,
				dir.angle - Math.PI / 2 + Math.PI / 4,
				dir.angle - Math.PI / 2 - Math.PI / 4, true);
			this.ctx.stroke();
		}
	}
};

CanvasElementPiratesMap.prototype.drawBattleIndicators = function() {
	var halfScale = 0.5 * this.scale;
	var rows = this.state.replay.rows;
	var rowPixels = rows * this.scale;
	var cols = this.state.replay.cols;
	var colPixels = cols * this.scale;
	this.ctx.lineWidth = Math.pow(this.scale, 0.3);
	for (var hash in this.drawStates) {
		var drawList = this.drawStates[hash];
		this.ctx.strokeStyle = hash;
		this.ctx.beginPath();
		for (var n = drawList.length - 1; n >= 0; n--) {
			var kf = drawList[n];
			if (this.pairing[kf.pirateId] !== undefined) {
				for (d = this.pairing[kf.pirateId].targets.length - 1; d >= 0; d--) {
				    var scale = 1.0;
					var target = this.pairing[kf.pirateId].targets[d];
					//check if target did not shoot the attacker haha!
					if (this.pairing[target.pirateId].targets[0] === kf) {
					    scale = 0.5;
					}
					var x1 = kf.mapX + halfScale;
					var y1 = kf.mapY + halfScale;
					var dx = Math.wrapAround(target.mapX - kf.mapX, colPixels);
					var ar = this.state.replay.meta['replaydata']['attackradius2'];
					if (2 * dx > colPixels) dx -= colPixels;
					var x2 = x1 + scale * dx;
					var dy = Math.wrapAround(target.mapY - kf.mapY, rowPixels);
					if (2 * dy > rowPixels) dy -= rowPixels;
					var y2 = y1 + scale * dy;

                    var m = -1;
                    if (this.time >= this.turn && this.time < this.turn + 0.25) {
                        m = 0;
                    }
                    else if (this.time >= this.turn + 0.25 && this.time < this.turn + 0.5) {
                        m = 1;
                    }
                    else if (this.time >= this.turn + 0.5 && this.time < this.turn + 0.75) {
                        m = 2;
                    }
                    else if (this.time >= this.turn + 0.75 && this.time < this.turn + 1) {
                        m = 3;
                    }
					var barrel_w = 43;
					var barrel_h = 34;
					var size = 1.2;
					this.drawWrapped(Math.min(x1, x2) - 1, Math.min(y1, y2) - 1,
						Math.abs(x2 - x1) + 2, Math.abs(y2 - y1) + 2, colPixels, rowPixels,
						function (fx1, fy1, fx2, fy2) {
						    this.ctx.setLineDash([2, 4]);
							this.ctx.moveTo(fx1, fy1);
							this.ctx.lineTo(fx2, fy2);
							this.drawInScale(this.map.barrel, barrel_w*m, 0, barrel_w, barrel_h,
                                fx1 + (fx2-fx1)/(4-m) - barrel_w*size*0.25,
                                fy1 + (fy2-fy1)/(4-m) - barrel_h*size*0.25,
							this.scale , this.scale, size);
						}, [x1, y1, x2, y2]);

				}
			}
		}
		this.ctx.stroke();
		this.ctx.setLineDash([]);
	}
};

CanvasElementPiratesMap.prototype.drawAttackRadiuses = function() {
	var halfScale = 0.5 * this.scale;
	if (this.mouseOverVis) {
		var ar = this.state.replay.meta['replaydata']['attackradius2'];
		ar = this.scale * Math.sqrt(ar);
		this.ctx.globalAlpha = 0.5;
		for (var n = this.circledPirates.length - 1; n >= 0; --n) {
//		    var ar = this.pirates[this.circledPirates[n].pirateId].attackRadius;
//		    ar = this.scale * Math.sqrt(ar);
			var kf = this.circledPirates[n];
			var hash = '#';
			hash += INT_TO_HEX[kf['r']];
			hash += INT_TO_HEX[kf['g']];
			hash += INT_TO_HEX[kf['b']];
			this.ctx.strokeStyle = hash;
			this.ctx.beginPath();
			var dx = kf.mapX + halfScale;
			var dy = kf.mapY + halfScale;
			var x1 = dx - ar;
			var y1 = dy - ar;
			this.ctx.moveTo(dx + ar, dy);
			this.ctx.arc(dx, dy, ar, 0, 2 * Math.PI, false);
			this.ctx.stroke();
		}
		this.ctx.globalAlpha = 1;
	}
};

CanvasElementPiratesMap.prototype.drawLabels = function() {
	var label = this.state.config['label'];
	if (label) {
		var fontSize = Math.ceil(Math.max(this.scale, 10) / label);
		this.ctx.save();
		this.ctx.translate(halfScale, halfScale);
		this.ctx.textBaseline = 'middle';
		this.ctx.textAlign = 'center';
		this.ctx.font = 'bold ' + fontSize + 'px Arial';
		this.ctx.fillStyle = '#000';
		this.ctx.strokeStyle = '#fff';
		this.ctx.lineWidth = 0.2 * fontSize;
		var order = new Array(this.state.order.length);
		for (n = 0; n < order.length; n++) {
			order[this.state.order[n]] = n;
		}
		for (var hash in this.drawStates) {
			var drawList = this.drawStates[hash];
			for (var n = drawList.length - 1; n >= 0; n--) {
				var kf = drawList[n];
				if (label === 1) {
					if (kf['owner'] === undefined) continue;
					var caption = String.fromCharCode(0x3b1 + order[kf['owner']]);
				} else {
					caption = kf.pirateId;
				}
				this.ctx.strokeText(caption, kf.mapX, kf.mapY);
				this.ctx.fillText(caption, kf.mapX, kf.mapY);
				if (kf.mapX < 0) {
					this.ctx.strokeText(caption, kf.mapX + this.map.w, kf.mapY);
					this.ctx.fillText(caption, kf.mapX + this.map.w, kf.mapY);
					if (kf.mapY < 0) {
						this.ctx.strokeText(caption, kf.mapX + this.map.w, kf.mapY + this.map.h);
						this.ctx.fillText(caption, kf.mapX + this.map.w, kf.mapY + this.map.h);
					}
				}
				if (kf.mapY < 0) {
					this.ctx.strokeText(caption, kf.mapX, kf.mapY + this.map.h);
					this.ctx.fillText(caption, kf.mapX, kf.mapY + this.map.h);
				}
			}
		}
		this.ctx.restore();
	}
};

CanvasElementPiratesMap.prototype.drawFog = function() {
	var rows = this.state.replay.rows;
	var rowPixels = rows * this.scale;
	var cols = this.state.replay.cols;
	var colPixels = cols * this.scale;
	if (this.state.fogPlayer !== undefined) {
		var dx = (this.fog.w < colPixels) ? ((colPixels - this.fog.w + 1) >> 1) - this.fog.shiftX : 0;
		var dy = (this.fog.h < rowPixels) ? ((rowPixels - this.fog.h + 1) >> 1) - this.fog.shiftY : 0;
		this.drawWrapped(dx, dy, this.fog.w, this.fog.h, this.w, this.h, function (ctx, img, x, y) {
			ctx.drawImage(img, x, y);
		}, [this.ctx, this.fog.canvas, dx, dy]);
	}
};

CanvasElementPiratesMap.prototype.drawZones = function() {

	var zones = this.state.replay.meta['replaydata']['zones'];

    //var x = 1.5
    //var y = 1.5
    //var w = 1
    var right_player_y = this.map.h - 2.5*this.scale;
	var right_player_x = this.map.w - 2.5*this.scale;
	var left_player_y = right_player_y;
	var left_player_x = 1.5* this.scale;
    var w = this.scale;
    var size = 4;

    //this.drawInScale(this.treasureImageClosed, 0, 0, 50, 50, x, y, w, w, size);
//	for (var i = 0; i < zones.length; i++) {
//    	this.drawInScale(this.map.zone, 0, 0, 200, 200, right_player_x, right_player_y, w, w, size);
//    }
    this.drawInScale(this.map.bottom_right_zone, 200, (this.turn % 3) * 200, 200, 200, right_player_x, right_player_y, w, w, size);
    this.drawInScale(this.map.bottom_left_zone, 400, (this.turn % 3) * 200, 200, 200, left_player_x, left_player_y, w, w, size);
}

CanvasElementPiratesMap.prototype.drawTreasures = function() {
	var treasures = this.state.replay.meta['replaydata']['treasures'];
	var w = this.scale;
	var size = 2;
	for (var i = 0; i < treasures.length; i++) {
		var treasure = treasures[i];
		var initial_location = treasure[1];
        var is_available_history = treasure[2];
		//dont draw treasure if it is currently being carried
		if (is_available_history[this.turn - 1] == '0') {
			continue;
		}
		var x = (initial_location[1]+0.4) * this.scale;
		var y = (initial_location[0]+0.4) * this.scale;
		this.drawInScale(this.treasureImageClosed, 0, 0, 50, 50, x, y, w, w, size);
	}
};

CanvasElementPiratesMap.prototype.fade = function fade(currentTime, valuea, valueb, timea, timeb) {
    mix = (currentTime - timea) / (timeb - timea);
    var value = (1 - mix) * valuea + mix * valueb;
    return value;
};

CanvasElementPiratesMap.prototype.drawImageRotated = function(image, x, y, h, w, angle) {
	var ctx = this.ctx;
	var tx = x + w / 2;
	var ty = y + h / 2;
	// I read here "http://stackoverflow.com/questions/3793397/html5-canvas-drawimage-with-at-an-angle" that code is more efficient without save/restore
	//ctx.save();
	ctx.translate(tx, ty);
	ctx.rotate(angle);
	ctx.drawImage(image, - w / 2, - h / 2, w, w);
	ctx.rotate(-angle);
	ctx.translate(-tx, -ty);
	//ctx.restore();
};


/**
 * Draws pirates onto the map image. This includes overlay letters / ids, attack lines, effect circles
 * and finally the fog of war.
 */
CanvasElementPiratesMap.prototype.draw = function () {
    /* Do not draw board on the site */
    var board;
    board = this.map.boardA;
    /*if ((this.turn % 3) == 0 ) {
        board = this.map.boardA;
    } else if ((this.turn % 3) == 1 ) {
        board = this.map.boardA; //B
    } else {
        board = this.map.boardA; //C
    }
	*/
	this.ctx.drawImage(board, -130, -130);
	this.drawTreasures();
	//this.drawLighthouses();
	this.drawPirates();
	//this.drawBlockedMoves();
	this.drawBattleIndicators();
	this.drawAttackRadiuses();
	this.drawLabels();
	//this.drawFog();
};


/**
* Sets the pirate treasure image to use when drawing the map.
 *
 * @param {HTMLCanvasElement}
 *        @treasureImageClosed a colorized closed treasure graphic.
 *        @treasureImageOpened a colorized opened treasure graphic.
 */
CanvasElementPiratesMap.prototype.setTreasureImage = function(treasureImageClosed, treasureImageOpened ) {
	this.treasureImageClosed = treasureImageClosed;
	this.treasureImageOpened = treasureImageOpened;
};
