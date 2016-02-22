'use strict';

angular.module('visualizerApp', []);

angular.module('visualizerApp').controller('VisualizerCtrl', ['$scope', function($scope) {
    var fog = undefined;
    function loadDebugMessages(debugMessages) {
        var res = [];
        var stop_turns = {};
        for (var playerIndex = 0; playerIndex < debugMessages.length; playerIndex++) {
            var byTurn = {};
            var turns = debugMessages[playerIndex];
            for (var turnIndex = 0; turnIndex < turns.length; turnIndex++) {
                var turn = turns[turnIndex];
                var turnNumber = turn[0] - 1;
                turnNumber = Math.max(0, turnNumber);
                if (!byTurn[turnNumber]) {
                    byTurn[turnNumber] = [];
                }
                var level = turn[1];
                if (level === 3) {
                    stop_turns[turnNumber] = true;
                }
                var messages = turn[2];
                for (var messageIndex = 0; messageIndex < messages.length; messageIndex++) {
                    var message = messages[messageIndex];
                    byTurn[turnNumber].push({
                        level: level,
                        message: message
                    });
                }
            }
            res.push(byTurn);
        }
        return [res, stop_turns];
    }

    function init() {
        var options = new Options();
        options.data_dir = window.pirates_data_dir || '../visualizer/data/';
        options.embedded = true;
        options.local_run = window.local_run;
        options.bigscreen = window.bigscreen;
        options.playercolors = window.playercolors;
        options.color1 = window.color1;
        options.color2 = window.color2;
        options.debug_mode = false;
        options.stop_turns = undefined;
        options.slowmo_enabled = false;
        if (options.local_run) {
            $scope.displayLog = false;
        } else {
            options.turn = 0;
        }
        options.updateExternalView = function() {
            setTimeout(function() {
                $scope.$apply(function() {
                    if (!$scope.debugMessages) {
                        var res = loadDebugMessages($scope.visualizer.state.replay.meta.debug_messages);
                        $scope.debugMessages = res[0];
                        options.stop_turns = res[1];
                    }
//                    $scope.turn = $scope.visualizer.state.time | 0;
                    $scope.turn = Math.floor($scope.visualizer.state.time)
                    $scope.playing = $scope.visualizer.director.playing();
                    $scope.slowmo = options.slowmo_enabled;
                    $scope.cutoff = $scope.visualizer.state.replay.meta.replaydata.cutoff;
                    $scope.maxpoints = $scope.visualizer.state.replay.meta.replaydata.maxpoints || 1000;
                    $scope.isLastTurn = $scope.turn >= $scope.visualizer.state.replay.duration;
                });
            }, 0);
        };
        setTimeout(function() {
            $scope.visualizer = new Visualizer(document.getElementById('game-canvas'), options);
            $scope.visualizer.loadReplayData(window.replayData);
        }, 0);
    }

    $scope.showFog = function(id) {
        if (fog !== id) {
            fog = id;
        } else {
            fog = undefined;
        }
        $scope.visualizer.showFog(fog);
    };

    $scope.play = function() {
        $scope.visualizer.director.play();
    };

    $scope.firstTurn = function() {
        $scope.visualizer.director.gotoTick(0);
    };

    $scope.lastTurn = function() {
        $scope.visualizer.director.gotoTick($scope.visualizer.director.duration);
    };

    $scope.prevTurn = function() {
//        var stop = Math.ceil($scope.visualizer.state.time * 2) - 1;
//        $scope.visualizer.director.slowmoTo(stop / 2);
        $scope.visualizer.director.gotoTick(Math.ceil($scope.visualizer.state.time) - 1)
    };

    $scope.nextTurn = function() {
//        var stop = Math.floor($scope.visualizer.state.time * 2) + 1;
//        $scope.visualizer.director.slowmoTo(stop / 2);
        $scope.visualizer.director.gotoTick(Math.floor($scope.visualizer.state.time) + 1)
    };

    $scope.togglePlay = function() {
        $scope.visualizer.director.playStop();
        $scope.visualizer.draw();
    };

    $scope.toggleSlowmo = function() {
        $scope.visualizer.state.options.slowmo_enabled = !$scope.visualizer.state.options.slowmo_enabled;
    };

    $scope.speed = function(amount) {
        $scope.visualizer.modifySpeed(amount);
    };

    $scope.gotoTurn = function(turn) {
        $scope.visualizer.director.gotoTick(turn - 1);
    };

    $scope.togglePiratesText = function() {
        var lbl = $scope.visualizer.state.config['label'];
        $scope.visualizer.setPirateLabels((lbl + 1) % 3);
        $scope.visualizer.director.draw();
    };

    $scope.toggleLog = function() {
        $scope.displayLog = !$scope.displayLog;
        //if log is shown -> disable stop points
        $scope.visualizer.state.options.debug_mode = $scope.displayLog;
        //if log is shown -> show squared zones
        $scope.visualizer.state.config['showZones'] = !$scope.visualizer.state.config['showZones'];
        setTimeout(function() {
            $scope.visualizer.resize(true);
        }, 0);
    };

    $scope.download = function() {
        var data = JSON.stringify(JSON.stringify($scope.visualizer.state.replay.meta.replaydata.ants));
        var text = 'turns = ' + data + '\n' + document.getElementById('replay-code').textContent;

        var pom = document.createElement('a');
        pom.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(text));
        pom.setAttribute('download', 'dummy.py');

        pom.style.display = 'none';
        document.body.appendChild(pom);

        pom.click();

        document.body.removeChild(pom);
    };

    $scope.debugPlayerIndex = 1;
    init();
}]);