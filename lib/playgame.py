#!/usr/bin/env python
from __future__ import print_function
import traceback
import sys
import os
import time
from optparse import OptionParser, OptionGroup
import ConfigParser
import random
import shutil
import zipfile
import cProfile
import tempfile
import visualizer.visualize_locally
import json
try:
    from StringIO import StringIO
except ImportError:
    from io import StringIO

from pirates import Pirates

# verify we are running in python 2.7
if not (sys.version_info[0] == 2 and sys.version_info[1] == 7):
    print("You are running from python %d.%d. Run from Python 2.7 instead!" % sys.version_info[0:2])
    sys.exit(-1)
try:
    from engine import run_game
except ImportError:
    # this can happen if we're launched with cwd outside our own dir
    # get our full path, then work relative from that
    cmd_folder = os.path.dirname(os.path.abspath(__file__))
    if cmd_folder not in sys.path:
        sys.path.insert(0, cmd_folder)
    # try again
    from engine import run_game

# make stderr red text
try:
    import colorama
    colorama.init()
    colorize = True
    color_default = (colorama.Fore.RED)
    color_reset = (colorama.Style.RESET_ALL)
except:
    colorize = False
    color_default = None
    color_reset = None

class Colorize(object):
    def __init__(self, file, color=color_default):
        self.file = file
        self.color = color
        self.reset = color_reset
    def write(self, data):
        if self.color:
            self.file.write(''.join(self.color))
        self.file.write(data)
        if self.reset:
            self.file.write(''.join(self.reset))
    def flush(self):
        self.file.flush()
    def close(self):
        self.file.close()

if colorize:
    stderr = Colorize(sys.stderr)
else:
    stderr = sys.stderr

class Comment(object):
    def __init__(self, file):
        self.file = file
        self.last_char = '\n'
    def write(self, data):
        for char in data:
            if self.last_char == '\n':
                self.file.write('# ')
            self.file.write(char)
            self.last_char = char
    def flush(self):
        self.file.flush()
    def close(self):
        self.file.close()

class Tee(object):
    ''' Write to multiple files at once '''
    def __init__(self, *files):
        self.files = files
    def write(self, data):
        for file in self.files:
            file.write(data)
    def flush(self):
        for file in self.files:
            file.flush()
    def close(self):
        for file in self.files:
            file.close()
            
class ZipEncapsulator(object):
    '''  List of temporary folders used to be deleted '''
    def __init__(self):
        self.tempdirs = []
    def unzip(self, zipfilename):
        # here we assume that the 
        self.tempdirs.append(tempfile.mkdtemp(dir=os.path.dirname(zipfilename)))
        with zipfile.ZipFile(zipfilename, 'r') as zippy:
            zippy.extractall(self.tempdirs[-1])
            try:
                new_target = self.tempdirs[-1]
            except:
                print('Empty zipfile found!')
                traceback.print_exc()
                return -1
        return new_target
    def close(self):
        [shutil.rmtree(td) for td in self.tempdirs]
            
def main(argv):
    usage ="Usage: %prog [options] map bot1 bot2\n\nYou must specify a map file."
    parser = OptionParser(usage=usage)

    config = ConfigParser.ConfigParser()
    config.read("lib//game_cfg.txt")
    main_cfg_section = config.sections()[0]

    options = config.options(main_cfg_section)
    def_opts = {}
    for option in options:
        try:
            def_opts[option] = config.get(main_cfg_section, option)
        except:
            print("exception on %s!" % option)
            def_opts[option] = None

    # map to be played
    # number of players is determined by the map file
    parser.add_option("-m", "--map_file", dest="map",
                      default=None,
                      help="Name of the map file")

    # maximum number of turns that the game will be played
    parser.add_option("-t", "--turns", dest="turns",
                      default=def_opts['turns'], type="int",
                      help="Maximum number of turns in the game")

    parser.add_option("--serial", dest="serial",
                      action="store_true",
                      help="Run bots in serial, instead of parallel.")

    parser.add_option("--recover-errors", dest="recover_errors",
                      default=False, action="store_true",
                      help="Instruct runners to recover errors in do_turn")

    parser.add_option("--abort-errors", dest="recover_errors",
                      action="store_false",
                      help="Instruct runners to not recover errors in do_turn")

    parser.add_option("--turntime", dest="turntime",
                      default=100, type="int",
                      help="Amount of time to give each bot, in milliseconds")
    parser.add_option("--loadtime", dest="loadtime",
                      default=5000, type="int",
                      help="Amount of time to give for load, in milliseconds")

    parser.add_option("--extratime", dest="extratime",
                      default=1000, type="int",
                      help="Amount of extra total time to give each bot (in serial mode), in milliseconds")

    parser.add_option("-r", "--rounds", dest="rounds",
                      default=1, type="int",
                      help="Number of rounds to play")
    parser.add_option("--player_seed", dest="player_seed",
                      default=None, type="int",
                      help="Player seed for the random number generator")
    parser.add_option("--engine_seed", dest="engine_seed",
                      default=None, type="int",
                      help="Engine seed for the random number generator")
    
    parser.add_option('--strict', dest='strict',
                      action='store_true', default=False,
                      help='Strict mode enforces valid moves for bots')
    parser.add_option('--capture_errors', dest='capture_errors',
                      action='store_true', default=False,
                      help='Capture errors and stderr in game result')
    parser.add_option('--end_wait', dest='end_wait',
                      default=0.25, type="float",
                      help='Seconds to wait at end for bots to process end')
    parser.add_option('--secure_jail', dest='secure_jail',
                      action='store_true', default=False,
                      help='Use the secure jail for each bot (*nix only)')
    parser.add_option('--fill', dest='fill',
                      action='store_true', default=False,
                      help='Fill up extra player starts with last bot specified')
    parser.add_option('-p', '--position', dest='position',
                      default=0, type='int',
                      help='Player position for first bot specified')

    # pirates specific game options
    game_group = OptionGroup(parser, "Game Options", "Options that affect the game mechanics for pirates")
    game_group.add_option("--attack", dest="attack",
                          default="active",
                          help="Attack method to use for engine. (closest, focus, support, damage, active)")
    game_group.add_option("--viewradius2", dest="viewradius2",
                          default=20000, type="int",
                          help="Vision radius of pirate ships squared")
    game_group.add_option("--attackradius2", dest="attackradius2",
                          default=def_opts['attackradius2'], type="int",
                          help="Attack radius of pirate ships squared")
    game_group.add_option("--maxpoints", dest="maxpoints",
                          default=def_opts['maxpoints'], type="int",
                          help="Points to reach to end game")
    game_group.add_option("--actions_per_turn", dest="actions_per_turn",
                          default=def_opts['actions_per_turn'], type="int",
                          help="How many actions can be performed by player in one turn")
    game_group.add_option("--reload_turns", dest="reload_turns",
                          default=def_opts['reload_turns'], type="int",
                          help="How many turns ship can not move after attacking")
    game_group.add_option("--defense_reload_turns", dest="defense_reload_turns",
                          default=def_opts['defense_reload_turns'], type="int",
                          help="How many turns ship can not move after defending")
    game_group.add_option("--defense_expiration_turns", dest="defense_expiration_turns",
                          default=def_opts['defense_expiration_turns'], type="int",
                          help="How many turns till pirate ship defense expires")
    game_group.add_option("--treasure_spawn_turns", dest="treasure_spawn_turns",
                          default=def_opts['treasure_spawn_turns'], type="int",
                          help="How many turns for a treasure to respawn after successfully unloaded")
    game_group.add_option("--spawn_turns", dest="spawn_turns",
                          default=def_opts['spawn_turns'], type="int",
                          help="Turns for unit to respawn")
    game_group.add_option("--sober_turns", dest="sober_turns",
                          default=def_opts['sober_turns'], type="int",
                          help="Turns for unit to sober up")
    game_group.add_option("--ghostcooldown", dest="ghostcooldown",
                          default=50, type="int",
                          help="Number of turns to change ownership of island")
    game_group.add_option("--fogofwar", dest="fogofwar", 
    					  default=False, action="store_true",
    					  help="Activate option of fog of war and radius vision for ships")
    game_group.add_option("--cutoff_turn", dest="cutoff_turn", type="int", default=150,
                          help="Number of turns cutoff percentage is maintained to end game early")
    game_group.add_option("--cutoff_percent", dest="cutoff_percent", type="float", default=0.85,
                          help="Number of turns cutoff percentage is maintained to end game early")
    game_group.add_option("--scenario", dest="scenario",
                          action='store_true', default=False)
    parser.add_option_group(game_group)

    # the log directory must be specified for any logging to occur, except:
    #    bot errors to stderr
    #    verbose levels 1 & 2 to stdout and stderr
    #    profiling to stderr
    # the log directory will contain
    #    the replay or stream file used by the visualizer, if requested
    #    the bot input/output/error logs, if requested    
    log_group = OptionGroup(parser, "Logging Options", "Options that control the logging")
    log_group.add_option("-g", "--game", dest="game_id", default=0, type='string',
                         help="game id to start at when numbering log files")
    log_group.add_option("-l", "--log_dir", dest="log_dir", default=None,
                         help="Directory to dump replay files to.")
    log_group.add_option("--debug_in_replay", dest="debug_in_replay", 
                         action='store_true',default=False,
                         help="Specify if should insert debug/warning/error prints in replay file")
    game_group.add_option("--debug_max_count", dest="debug_max_count",
                          default=10000, type="int",
                          help="Maximum number of debug message to be stored in replay data")
    game_group.add_option("--debug_max_length", dest="debug_max_length",
                          default=200000, type="int",
                          help="Maximum total length of debug message to be stored in replay data")
    log_group.add_option('-R', '--log_replay', dest='log_replay',
                         action='store_true', default=False),
    log_group.add_option('-S', '--log_stream', dest='log_stream',
                         action='store_true', default=False),
    log_group.add_option("-I", "--log_input", dest="log_input",
                         action="store_true", default=False,
                         help="Log input streams sent to bots")
    log_group.add_option("-O", "--log_output", dest="log_output",
                         action="store_true", default=False,
                         help="Log output streams from bots")
    log_group.add_option("-E", "--log_error", dest="log_error",
                         action="store_true", default=False,
                         help="log error streams from bots")
    log_group.add_option('-e', '--log_stderr', dest='log_stderr',
                         action='store_true', default=False,
                         help='additionally log bot errors to stderr')
    log_group.add_option('-o', '--log_stdout', dest='log_stdout',
                         action='store_true', default=False,
                         help='additionally log replay/stream to stdout')
    # verbose will not print bot input/output/errors
    # only info+debug will print bot error output
    log_group.add_option("-v", "--verbose", dest="verbose",
                         action='store_true', default=False,
                         help="Print out status as game goes.")
    log_group.add_option("-d", "--debug", dest="debug",
                         action='store_true', default=False,
                         help="Print debug messages from bots.")
    log_group.add_option("--profile", dest="profile",
                         action="store_true", default=False,
                         help="Run under the python profiler")
    parser.add_option("--nolaunch", dest="nolaunch",
                      action='store_true', default=False,
                      help="Prevent visualizer from launching")
    log_group.add_option("--html", dest="html_file",
                         default=None,
                         help="Output file name for an html replay")
    parser.add_option_group(log_group)

    (opts, args) = parser.parse_args(argv)
    if opts.map is None or not os.path.exists(opts.map):
        parser.print_help()
        return -1
    try:
        if opts.profile:
            # put profile file into output dir if we can
            prof_file = "pirates.profile"
            if opts.log_dir:
                prof_file = os.path.join(opts.log_dir, prof_file)
            # cProfile needs to be explitly told about out local and global context
            print("Running profile and outputting to {0}".format(prof_file,), file=stderr)
            cProfile.runctx("run_rounds(opts,args)", globals(), locals(), prof_file)
        else:
            # only use psyco if we are not profiling
            # (psyco messes with profiling)
            try:
                import psyco
                psyco.full()
            except ImportError:
                pass
            run_rounds(opts,args)
        return 0
    except Exception:
        traceback.print_exc()
        return -1

def run_rounds(opts,args):
    def get_bot_paths(cmd, zip_encapsulator):
        ''' Receives a single file string from the command line and 
            Returns a 3 list of <working_dir> <full_file_path> <file_name>
        '''
        filepath = os.path.realpath(cmd)
        if filepath.endswith('.zip'):
            # if we get zip file - override original filepath for abstraction
            filepath = zip_encapsulator.unzip(filepath)
        working_dir = os.path.dirname(filepath)
        bot_name = os.path.basename(cmd).split('.')[0]
        return working_dir, filepath, bot_name
    def get_cmd_wd(cmd, exec_rel_cwd=False):
        ''' get the proper working directory from a command line '''
        new_cmd = []
        wd = None
        for i, part in reversed(list(enumerate(cmd.split()))):
            if wd == None and os.path.exists(part):
                wd = os.path.dirname(os.path.realpath(part))
                basename = os.path.basename(part)
                if i == 0:
                    if exec_rel_cwd:
                        new_cmd.insert(0, os.path.join(".", basename))
                    else:
                        new_cmd.insert(0, part)
                else:
                    new_cmd.insert(0, basename)
            else:
                new_cmd.insert(0, part)
        return wd, ' '.join(new_cmd)
    def get_cmd_name(cmd):
        ''' get the name of a bot from the command line '''
        for i, part in enumerate(reversed(cmd.split())):
            # if extra whatever-"-character-called in the line - remove them    
            part = part.replace('"','')
            if os.path.exists(part):
                return os.path.basename(part)
# this split of options is not needed, but left for documentation
    game_options = {
        "map": opts.map,
        "attack": opts.attack,
        "viewradius2": opts.viewradius2,
        "attackradius2": opts.attackradius2,
        "loadtime": opts.loadtime,
        "turntime": opts.turntime,
        "recover_errors": opts.recover_errors,
        "turns": opts.turns,
        "cutoff_turn": opts.cutoff_turn,
        "cutoff_percent": opts.cutoff_percent,
        "scenario": opts.scenario,
        "maxpoints" : opts.maxpoints,
        "actions_per_turn" : opts.actions_per_turn,
        "reload_turns" : opts.reload_turns,
        "defense_reload_turns" : opts.defense_reload_turns,
        "defense_expiration_turns" : opts.defense_expiration_turns,
        "treasure_spawn_turns" : opts.treasure_spawn_turns,
        "spawn_turns" : opts.spawn_turns,
        "sober_turns" : opts.sober_turns,
        "ghostcooldown" : opts.ghostcooldown,
        "fogofwar" : opts.fogofwar }
        
    if opts.player_seed != None:
        game_options['player_seed'] = opts.player_seed
    if opts.engine_seed != None:
        game_options['engine_seed'] = opts.engine_seed
    engine_options = {
        "loadtime": opts.loadtime,
        "turntime": opts.turntime,
        "extratime": opts.extratime,
        "map_file": opts.map,
        "turns": opts.turns,
        "debug_in_replay": opts.debug_in_replay,
        "debug_max_length": opts.debug_max_length,
        "debug_max_count": opts.debug_max_count,
        "log_replay": opts.log_replay,
        "log_stream": opts.log_stream,
        "log_input": opts.log_input,
        "log_output": opts.log_output,
        "log_error": opts.log_error,
        "serial": opts.serial,
        "strict": opts.strict,
        "capture_errors": opts.capture_errors,
        "secure_jail": opts.secure_jail,
        "end_wait": opts.end_wait }
    for round in range(opts.rounds):
        # initialize bots
        zip_encapsulator = ZipEncapsulator()
        bots = [get_bot_paths(arg, zip_encapsulator) for arg in args]
        bot_count = len(bots)

        # initialize game
        game_id = "{0}.{1}".format(opts.game_id, round) if opts.rounds > 1 else opts.game_id
        with open(opts.map, 'r') as map_file:
            game_options['map'] = map_file.read()
        if opts.engine_seed:
            game_options['engine_seed'] = opts.engine_seed + round
        game_options['bot_names'] = map(lambda b: b[2], bots)
        game = Pirates(game_options)
        # insure correct number of bots, or fill in remaining positions
        if game.num_players != len(bots):
            print("Incorrect number of bots for map.  Need {0}, got {1}"
                  .format(game.num_players, len(bots)), file=stderr)

        # initialize file descriptors
        if opts.log_dir and not os.path.exists(opts.log_dir):
            os.mkdir(opts.log_dir)
        if not opts.log_replay and not opts.log_stream and (opts.log_dir or opts.log_stdout):
            opts.log_replay = True
        replay_path = None # used for visualizer launch
        
        if opts.log_replay:
            if opts.log_dir:
                replay_path = os.path.join(opts.log_dir, '{0}.replay'.format(game_id))
                engine_options['replay_log'] = open(replay_path, 'w')
            if opts.log_stdout:
                if 'replay_log' in engine_options and engine_options['replay_log']:
                    engine_options['replay_log'] = Tee(sys.stdout, engine_options['replay_log'])
                else:
                    engine_options['replay_log'] = sys.stdout
        else:
            engine_options['replay_log'] = None

        if opts.log_stream:
            if opts.log_dir:
                engine_options['stream_log'] = open(os.path.join(opts.log_dir, '{0}.stream'.format(game_id)), 'w')
            if opts.log_stdout:
                if engine_options['stream_log']:
                    engine_options['stream_log'] = Tee(sys.stdout, engine_options['stream_log'])
                else:
                    engine_options['stream_log'] = sys.stdout
        else:
            engine_options['stream_log'] = None
        
        if opts.log_input and opts.log_dir:
            engine_options['input_logs'] = [open(os.path.join(opts.log_dir, '{0}.bot{1}.input'.format(game_id, i)), 'w')
                             for i in range(bot_count)]
        else:
            engine_options['input_logs'] = None
        if opts.log_output and opts.log_dir:
            engine_options['output_logs'] = [open(os.path.join(opts.log_dir, '{0}.bot{1}.output'.format(game_id, i)), 'w')
                              for i in range(bot_count)]
        else:
            engine_options['output_logs'] = None
        if opts.log_error and opts.log_dir:
            if opts.log_stderr:
                if opts.log_stdout:
                    engine_options['error_logs'] = [Tee(Comment(stderr), open(os.path.join(opts.log_dir, '{0}.bot{1}.error'.format(game_id, i)), 'w'))
                                      for i in range(bot_count)]
                else:
                    engine_options['error_logs'] = [Tee(stderr, open(os.path.join(opts.log_dir, '{0}.bot{1}.error'.format(game_id, i)), 'w'))
                                      for i in range(bot_count)]
            else:
                engine_options['error_logs'] = [open(os.path.join(opts.log_dir, '{0}.bot{1}.error'.format(game_id, i)), 'w')
                                  for i in range(bot_count)]
        elif opts.log_stderr:
            if opts.log_stdout:
                engine_options['error_logs'] = [Comment(stderr)] * bot_count
            else:
                engine_options['error_logs'] = [stderr] * bot_count
        else:
            engine_options['error_logs'] = None
        
        if opts.verbose:
            if opts.log_stdout:
                engine_options['verbose_log'] = Comment(sys.stdout)
            else:
                engine_options['verbose_log'] = sys.stdout

        if opts.debug:
            engine_options['debug_log'] = sys.stdout
                
        engine_options['game_id'] = game_id 
        if opts.rounds > 1:
            print('# playgame round {0}, game id {1}'.format(round, game_id))

        # intercept replay log so we can add player names
        if opts.log_replay:
            intcpt_replay_io = StringIO()
            real_replay_io = engine_options['replay_log']
            engine_options['replay_log'] = intcpt_replay_io

        result = run_game(game, bots, engine_options)
        
        # destroy temporary directories
        zip_encapsulator.close()
        
        # add player names, write to proper io, reset back to normal
        if opts.log_replay:
            replay_json = json.loads(intcpt_replay_io.getvalue())
            replay_json['playernames'] = [b[2] for b in bots]
            real_replay_io.write(json.dumps(replay_json))
            intcpt_replay_io.close()
            engine_options['replay_log'] = real_replay_io
        
        # close file descriptors
        if engine_options['stream_log']:
            engine_options['stream_log'].close()
        if engine_options['replay_log']:
            engine_options['replay_log'].close()
        if engine_options['input_logs']:
            for input_log in engine_options['input_logs']:
                input_log.close()
        if engine_options['output_logs']:
            for output_log in engine_options['output_logs']:
                output_log.close()
        if engine_options['error_logs']:
            for error_log in engine_options['error_logs']:
                error_log.close()
        if replay_path:
            if opts.nolaunch:
                if opts.html_file:
                    visualizer.visualize_locally.launch(replay_path, True, opts.html_file)
            else:
                if 'game_length' in result and result['game_length'] > 1: # ANCHOR - this is kool right?
                    if opts.html_file == None:
                        visualizer.visualize_locally.launch(replay_path,
                                generated_path="replay.{0}.html".format(game_id))
                    else:
                        visualizer.visualize_locally.launch(replay_path,
                                generated_path=opts.html_file)
if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
