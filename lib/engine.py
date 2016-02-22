#!/usr/bin/env python
from __future__ import print_function
import time
import traceback
import os
import base64
import random
import sys
import json
import subprocess
from os import walk
from os.path import splitext, join
import io

if sys.version_info >= (3,):
    def unicode(s):
        return s

from sandbox import get_sandbox

class HeadTail(object):
    'Capture first part of file write and discard remainder'
    def __init__(self, file, max_capture=510):
        self.file = file
        self.max_capture = max_capture
        self.capture_head_len = 0
        self.capture_head = unicode('')
        self.capture_tail = unicode('')
    def write(self, data):
        if self.file:
            self.file.write(data)
        capture_head_left = self.max_capture - self.capture_head_len
        if capture_head_left > 0:
            data_len = len(data)
            if data_len <= capture_head_left:
                self.capture_head += data
                self.capture_head_len += data_len
            else:
                self.capture_head += data[:capture_head_left]
                self.capture_head_len = self.max_capture
                self.capture_tail += data[capture_head_left:]
                self.capture_tail = self.capture_tail[-self.max_capture:]
        else:
            self.capture_tail += data
            self.capture_tail = self.capture_tail[-self.max_capture:]
    def flush(self):
        if self.file:
            self.file.flush()
    def close(self):
        if self.file:
            self.file.close()
    def head(self):
        return self.capture_head
    def tail(self):
        return self.capture_tail
    def headtail(self):
        if self.capture_head != '' and self.capture_tail != '':
            sep = unicode('\n..\n')
        else:
            sep = unicode('')
        return self.capture_head + sep + self.capture_tail


def run_game(game, botcmds, options):
    # file descriptors for replay and streaming formats
    replay_log = options.get('replay_log', None)
    stream_log = options.get('stream_log', None)
    verbose_log = options.get('verbose_log', None)
    debug_log = options.get('debug_log', None)
    debug_in_replay = options.get('debug_in_replay', None)
    debug_max_length = options.get('debug_max_length', None)
    debug_max_count = options.get('debug_max_count', None)
    # file descriptors for bots, should be list matching # of bots
    input_logs = options.get('input_logs', [None]*len(botcmds))
    output_logs = options.get('output_logs', [None]*len(botcmds))
    error_logs = options.get('error_logs', [None]*len(botcmds))

    capture_errors = options.get('capture_errors', False)
    capture_errors_max = options.get('capture_errors_max', 510)

    turns = int(options['turns'])
    loadtime = float(options['loadtime']) / 1000
    turntime = float(options['turntime']) / 1000
    extratime = float(options['extratime']) / 1000
    strict = options.get('strict', False)
    end_wait = options.get('end_wait', 0.0)

    location = options.get('location', 'localhost')
    game_id = options.get('game_id', 0)

    error = ''

    bots = []
    bot_status = []
    bot_turns = []
    bot_extra_times = [extratime for _ in range(len(botcmds))]

    debug_msgs = [[] for _ in range(len(botcmds))]
    debug_msgs_length = [0 for _ in range(len(botcmds))]
    debug_msgs_count = [0 for _ in range(len(botcmds))]
    debug_msgs_exceeded = [False for _ in range(len(botcmds))]

    #helper function to add messages for replay data
    def add_debug_messages(bot_index, turn, level, messages):
        if (not debug_in_replay) or len(messages) == 0:
            return

        # In order to calculate this only if we not already exceeded
        if not debug_msgs_exceeded[bot_index]:
            messages_size = sum(map(lambda m: len(m), messages))
            debug_msgs_length[bot_index] += messages_size
            debug_msgs_count[bot_index] += len(messages)

            if (debug_msgs_count[bot_index] > debug_max_count) or (
                debug_msgs_length[bot_index] > debug_max_length):
                # update the calculated exceeded
                debug_msgs_exceeded[bot_index] = True

        if debug_msgs_exceeded[bot_index] and level != 2:
            debug_msgs[bot_index].append([turn+1, 2, ["Exceeded debug messages limit."]])
            if error_logs and error_logs[bot_index]:
                error_logs[bot_index].write("Exceeded debug messages limit.\n")
        else:
            debug_msgs[bot_index].append([turn+1, level, messages])

    if capture_errors:
        error_logs = [HeadTail(log, capture_errors_max) for log in error_logs]
    try:
        # TODO: where did this come from?? do we need it??
        for b, bot in enumerate(botcmds):
            # this struct is given to us from the playgame.py file
            bot_cwd, bot_path, bot_name = bot
            # generate the appropriate command from file extension
            bot_cmd = generate_cmd(bot_path)

            # generate the sandbox from the bot working directory
            sandbox = get_sandbox(bot_cwd, protected_files=[bot_path], secure=options.get('secure_jail', None))

            if bot_cmd:
                sandbox.start(bot_cmd)

            bots.append(sandbox)
            bot_status.append('alive')
            bot_turns.append(0)

            # ensure it started
            if not sandbox.is_alive:
                bot_status[-1] = 'crashed 0'
                bot_turns[-1] = 0
                if verbose_log:
                    verbose_log.write('bot %s did not start\n' % bot_name)
                game.kill_player(b)
                sandbox.pause()

            if not bot_cmd:
                # couldnt generate bot command - couldnt recognize the language of the code
                add_debug_messages(b, 0, 2, ["Couldnt recognize code language. Are you sure code files are correct?"])

        if stream_log:
            # stream the start info - including non-player info
            stream_log.write(game.get_player_start())
            stream_log.flush()

        if verbose_log:
            verbose_log.write('running for %s turns\n' % turns)
        for turn in range(turns+1):
            if turn == 0:
                game.start_game()

            # send game state to each player
            for b, bot in enumerate(bots):
                if game.is_alive(b):
                    if turn == 0:
                        start = game.get_player_start(b) + 'ready\n'
                        bot.write(start)
                        if input_logs and input_logs[b]:
                            input_logs[b].write(start)
                            input_logs[b].flush()
                    else:
                        state = 'turn ' + str(turn) + '\n' + game.get_player_state(b) + 'go\n'
                        bot.write(state)
                        if input_logs and input_logs[b]:
                            input_logs[b].write(state)
                            input_logs[b].flush()
                        bot_turns[b] = turn

            if turn > 0:
                if stream_log:
                    stream_log.write('turn %s\n' % turn)
                    stream_log.write('score %s\n' % ' '.join([str(s) for s in game.get_scores()]))
                    stream_log.write(game.get_state())
                    stream_log.flush()
                game.start_turn()

            is_serial = options.get('serial', False)

            # get moves from each player
            if turn == 0:
                time_limit = loadtime
            elif turn == 1:
            	time_limit = max([turntime * 10, 1.500])
            else:
                time_limit = turntime

            if is_serial:
                simul_num = 1
            else:
                simul_num = len(bots)

            bot_moves = [[] for b in bots]
            error_lines = [[] for b in bots]
            statuses = [None for b in bots]
            bot_list = [(b, bot) for b, bot in enumerate(bots)
                        if game.is_alive(b)]
            #random.shuffle(bot_list)
            for group_num in range(0, len(bot_list), simul_num):
                pnums, pbots = zip(*bot_list[group_num:group_num + simul_num])
                if is_serial:
                    turn_time_limit = time_limit + bot_extra_times[pnums[0]]
                else:
                    turn_time_limit = time_limit

                # get the moves from each bot
                moves, errors, status, moves_time = get_moves(game, pbots, pnums,
                        turn_time_limit, turn)

                # if running in serial, deduct the exceeded time from the bot time quota
                if is_serial and moves_time > time_limit:
                    bot_extra_times[pnums[0]] -= moves_time - time_limit

                for p, b in enumerate(pnums):
                    bot_moves[b] = moves[p]
                    error_lines[b] = errors[p]
                    statuses[b] = status[p]

            # print debug messages from bots
            if debug_log:
                for b, moves in enumerate(bot_moves):
                    bot_name = botcmds[b][2]
                    messages = []
                    for move in [m for m in moves if m.startswith('m')]:
                        # if not move.startswith('m'):
                        #     # break since messages come only before orders
                        #     break
                        try:
                            messages.append(base64.b64decode(move.split(' ')[1]))
                        except:
                            messages.append("Invalid debug message")
                    if messages:
                        debug_log.write('turn %4d bot %s Debug prints:\n' % (turn, bot_name))
                        debug_log.write('Debug>> ' + '\nDebug>> '.join(messages)+'\n')
                        add_debug_messages(b, turn, 0, messages)
                    stop_messages = []
                    for move in [m for m in moves if m.startswith('s')]:
                        stop_messages.append(base64.b64decode(move.split(' ')[1]))
                    if stop_messages:
                        add_debug_messages(b, turn, 3, stop_messages) #todo: separate debug from stop messages?
                
            # handle any logs that get_moves produced
            for b, errors in enumerate(error_lines):
                if errors:
                    if error_logs and error_logs[b]:
                        error_logs[b].write(unicode('\n').join(errors)+unicode('\n'))
                    add_debug_messages(b, turn, 2, [unicode('\n').join(errors)+unicode('\n')])

                    #    error_logs[b].write(unicode('\n').join(unicode(errors))+unicode('\n'))
                    #add_debug_messages(b, turn, 2, [unicode('\n').join(unicode(errors))+unicode('\n')])
                        
            # set status for timeouts and crashes
            for b, status in enumerate(statuses):
                if status != None:
                    bot_status[b] = status
                    bot_turns[b] = turn

                    
            # process all moves
            bot_alive = [game.is_alive(b) for b in range(len(bots))]
            if turn > 0 and not game.game_over():
                for b, moves in enumerate(bot_moves):
                    valid, ignored, invalid = game.do_moves(b, moves)
                    bot_name = botcmds[b][2]
                    if output_logs and output_logs[b]:
                        output_logs[b].write('# turn %s\n' % turn)
                        if valid:
                            if output_logs and output_logs[b]:
                                output_logs[b].write('\n'.join(valid)+'\n')
                                output_logs[b].flush() 
                    if ignored:
                        if error_logs and error_logs[b]:
                            error_logs[b].write('turn %4d bot %s ignored actions:\n' % (turn, bot_name))
                            error_logs[b].write('\n'.join(ignored)+'\n')
                            error_logs[b].flush()
                        if output_logs and output_logs[b]:
                            output_logs[b].write('\n'.join(ignored)+'\n')
                            output_logs[b].flush()
                        add_debug_messages(b, turn, 1, ignored)
                            
                    if invalid:
                        if strict:
                            game.kill_player(b)
                            bot_status[b] = 'invalid'
                            bot_turns[b] = turn
                        if error_logs and error_logs[b]:
                            error_logs[b].write('turn %4d bot [%s] invalid actions:\n' % (turn, bot_name))
                            error_logs[b].write('\n'.join(invalid)+'\n')
                            error_logs[b].flush()
                        if output_logs and output_logs[b]:
                            output_logs[b].write('\n'.join(invalid)+'\n')
                            output_logs[b].flush()
                        add_debug_messages(b, turn, 1, invalid)

            if turn > 0:
                game.finish_turn()

            # send ending info to eliminated bots
            bots_eliminated = []
            for b, alive in enumerate(bot_alive):
                if alive and not game.is_alive(b):
                    bots_eliminated.append(b)
            for b in bots_eliminated:
                if verbose_log:
                    verbose_log.write('turn %4d bot %s defeated\n' % (turn, bot_name))
                if bot_status[b] == 'alive': # could be invalid move
                    bot_status[b] = 'defeated'
                    bot_turns[b] = turn
                score_line ='score %s\n' % ' '.join([str(s) for s in game.get_scores(b)])
                status_line = 'status %s\n' % ' '.join(map(str, game.order_for_player(b, bot_status)))
                status_line += 'playerturns %s\n' % ' '.join(map(str, game.order_for_player(b, bot_turns)))
                end_line = 'end\nplayers %s\n' % len(bots) + score_line + status_line
                state = end_line + game.get_player_state(b) + 'go\n'
                bots[b].write(state)
                if input_logs and input_logs[b]:
                    input_logs[b].write(state)
                    input_logs[b].flush()
                if end_wait:
                    bots[b].resume()
            if bots_eliminated and end_wait:
                if verbose_log:
                    verbose_log.write('waiting {0} seconds for bots to process end turn\n'.format(end_wait))
                time.sleep(end_wait)
            for b in bots_eliminated:
                bots[b].kill()

            # with verbose log we want to display the following <pirateCount> <treasureCount> <Ranking/leading> <scores>
            if verbose_log:
                stats = game.get_stats()
                stat_keys = sorted(stats.keys())
                s = 'turn %4d stats: ' % turn
                if turn % 50 == 0:
                    verbose_log.write(' '*len(s))
                    for key in stat_keys:
                        values = stats[key]
                        verbose_log.write(' {0:^{1}}'.format(key, max(len(key), len(str(values)))))
                    verbose_log.write('\n')
                verbose_log.write(s)
                for key in stat_keys:
                    values = stats[key]
                    if type(values) == list:
                        values = '[' + ','.join(map(str,values)) + ']'
                    verbose_log.write(' {0:^{1}}'.format(values, max(len(key), len(str(values)))))
                verbose_log.write('\n')
            else:
                # no verbose log - print progress every 20 turns
                if turn % 20 == 0:
                    turn_prompt = "turn #%d of max %d\n" % (turn,turns)
                    sys.stdout.write(turn_prompt)

            #alive = [game.is_alive(b) for b in range(len(bots))]
            #if sum(alive) <= 1:
            if game.game_over():
                break

        # send bots final state and score, output to replay file
        game.finish_game()
        score_line ='score %s\n' % ' '.join(map(str, game.get_scores()))
        status_line = ''
        if game.get_winner() and len(game.get_winner()) == 1:
            winner = game.get_winner()[0]
            winner_line = 'player %s [%s] is the Winner!\n' % (winner + 1, botcmds[winner][2])
        else:
            winner_line = 'Game finished at a tie - there is no winner'
        status_line += winner_line
        end_line = 'end\nplayers %s\n' % len(bots) + score_line + status_line
        if stream_log:
            stream_log.write(end_line)
            stream_log.write(game.get_state())
            stream_log.flush()
        if verbose_log:
            verbose_log.write(score_line)
            verbose_log.write(status_line)
            verbose_log.flush()
        else:
            sys.stdout.write(score_line)
            sys.stdout.write(status_line)
        for b, bot in enumerate(bots):
            if game.is_alive(b):
                score_line ='score %s\n' % ' '.join([str(s) for s in game.get_scores(b)])
                status_line = 'status %s\n' % ' '.join(map(str, game.order_for_player(b, bot_status)))
                status_line += 'playerturns %s\n' % ' '.join(map(str, game.order_for_player(b, bot_turns)))
                end_line = 'end\nplayers %s\n' % len(bots) + score_line + status_line
                state = end_line + game.get_player_state(b) + 'go\n'
                bot.write(state)
                if input_logs and input_logs[b]:
                    input_logs[b].write(state)
                    input_logs[b].flush()

    except Exception as e:
        # TODO: sanitize error output, tracebacks shouldn't be sent to workers
        error = traceback.format_exc()
        sys.stderr.write('Error Occurred\n')
        sys.stderr.write(str(e) + '\n')
        if verbose_log:
            verbose_log.write(error)
        # error = str(e)
    finally:
        if end_wait:
            for bot in bots:
                bot.resume()
            if verbose_log and end_wait > 1:
                verbose_log.write('waiting {0} seconds for bots to process end turn\n'.format(end_wait))
            time.sleep(end_wait)
        for bot in bots:
            if bot.is_alive:
                bot.kill()
            bot.release()

    if error:
        game_result = { 'error': error }
    else:
        scores = game.get_scores()
        game_result = {
            'challenge': game.__class__.__name__.lower(), 
            'location': location,
            'game_id': game_id,
            'status': bot_status,
            'playerturns': bot_turns,
            'score': scores,
            'winner_names': [botcmds[win][2] for win in game.get_winner()],
            'rank': [sorted(scores, reverse=True).index(x) for x in scores],
            'replayformat': 'json',
            'replaydata': game.get_replay(),
            'game_length': turn,
            'debug_messages': debug_msgs,
        }
        if capture_errors:
            game_result['errors'] = [head.headtail() for head in error_logs]

    if replay_log:
        json.dump(game_result, replay_log, sort_keys=True)

    return game_result

def get_moves(game, bots, bot_nums, time_limit, turn):
    bot_finished = [not game.is_alive(bot_nums[b]) for b in range(len(bots))]
    bot_moves = [[] for b in bots]
    error_lines = [[] for b in bots]
    statuses = [None for b in bots]

    # resume all bots
    for bot in bots:
        if bot.is_alive:
            bot.resume()

    # don't start timing until the bots are started
    start_time = time.time()

    # loop until received all bots send moves or are dead
    #   or when time is up
    while (sum(bot_finished) < len(bot_finished) and
            time.time() - start_time < time_limit):
        time.sleep(0.003)
        for b, bot in enumerate(bots):
            if bot_finished[b]:
                continue # already got bot moves
            if not bot.is_alive:
                error_lines[b].append(unicode('turn %4d bot %s crashed') % (turn, bot_nums[b]))
                statuses[b] = 'crashed'
                line = bot.read_error()
                while line != None:
                    error_lines[b].append(line)
                    line = bot.read_error()
                bot_finished[b] = True
                game.kill_player(bot_nums[b])
                continue # bot is dead

            # read a maximum of 100 lines per iteration
            for x in range(100):
                line = bot.read_line()
                if line is None:
                    # stil waiting for more data
                    break
                line = line.strip()
                if line.lower() == 'go':
                    bot_finished[b] = True
                    # bot finished sending data for this turn
                    break
                bot_moves[b].append(line)

            for x in range(100):
                line = bot.read_error()
                if line is None:
                    break
                error_lines[b].append(line)

    moves_time = time.time() - start_time

    # pause all bots again
    for bot in bots:
        if bot.is_alive:
            bot.pause()

    # check for any final output from bots
    for b, bot in enumerate(bots):
        if bot_finished[b]:
            continue # already got bot moves
        if not bot.is_alive:
            error_lines[b].append(unicode('turn %4d bot %s crashed') % (turn, bot_nums[b]))
            statuses[b] = 'crashed'
            line = bot.read_error()
            while line != None:
                error_lines[b].append(line)
                line = bot.read_error()
            bot_finished[b] = True
            game.kill_player(bot_nums[b])
            continue # bot is dead

        line = bot.read_line()
        while line is not None and len(bot_moves[b]) < 40000:
            line = line.strip()
            if line.lower() == 'go':
                bot_finished[b] = True
                # bot finished sending data for this turn
                break
            bot_moves[b].append(line)
            line = bot.read_line()

        line = bot.read_error()
        while line is not None and len(error_lines[b]) < 1000:
            error_lines[b].append(line)
            line = bot.read_error()

    # kill timed out bots
    for b, finished in enumerate(bot_finished):
        if not finished:
            error_lines[b].append(unicode('turn %4d bot %s timed out') % (turn, bot_nums[b]))
            statuses[b] = 'timeout'
            bot = bots[b]
            for x in range(100):
                line = bot.read_error()
                if line is None:
                    break
                error_lines[b].append(line)
            game.kill_player(bot_nums[b])
            bots[b].kill()
            

    return bot_moves, error_lines, statuses, moves_time
    
def get_java_path():
    if (os.name != "nt"):
        return 'java'
    # TODO: search path as well!
    # TODO: actually run os.system('java -version') to see version
    javas = []
    if os.path.exists("C:\\Program Files\\java"):
        javas += [os.path.join("C:\\Program Files\\java",i) for i in os.listdir("C:\\Program Files\\java")]
    if os.path.exists("C:\\Program Files (x86)\\java"):
        javas += [os.path.join("C:\\Program Files (x86)\\java",i) for i in os.listdir("C:\\Program Files (x86)\\java")]
    javas.reverse() # this will make us pick the higher version
    for java in javas:
        if 'jdk' in java.lower() and any([ver in java for ver in ['1.6','1.7','1.8']]):
            return os.path.join(java,"bin","java.exe")
    print("Cannot find path of Java JDK version 1.6 or over!")
    # we should really quit but since we dont yet search path - first try default
    return 'java'

def get_dot_net_version():
    pass

def select_files(root, files, suffix):
    """
    simple logic here to filter out interesting files
    """

    selected_files = []

    for file in files:
        #do concatenation here to get full path
        full_path = join(root, file)
        ext = splitext(file)[1]

        if ext == suffix:
            selected_files.append(full_path)

    return selected_files

def build_recursive_dir_tree(path, suffix):
    """
    path    -    where to begin folder scan
    """
    selected_files = []

    for root, dirs, files in walk(path):
        selected_files += select_files(root, files, suffix)

    return selected_files


def recognize_language(bot_path):
    '''Decide between java, python or csh'''

    '''First do single file case'''
    if not os.path.isdir(bot_path):
        if bot_path.endswith('.py') or bot_path.endswith('.pyc'):
            return 'python'
        elif bot_path.endswith('.cs'):
            return 'csh'
        elif bot_path.endswith('.java'):
            return 'java'
        else:
            return

    ''' Now handle directory case '''
    java_files = build_recursive_dir_tree(bot_path, '.java')
    csh_files = build_recursive_dir_tree(bot_path, '.cs')
    python_files = build_recursive_dir_tree(bot_path, '.py')

    max_files = max(len(java_files), len(csh_files), len(python_files))

    if max_files == 0:
        return

    if len(java_files) == max_files:
        return 'java'
    elif len(csh_files) == max_files:
        return 'csh'
    elif len(python_files) == max_files:
        return 'python'

    return

def generate_cmd(bot_path):
    ''' Generates the command to run and returns other information from the filename given '''
    csh_runner_path = os.path.join(os.path.dirname(__file__), "cshRunner.exe")
    java_runner_path = os.path.join(os.path.dirname(__file__), "javaRunner.jar")
    python_runner_path = os.path.join(os.path.dirname( __file__), "pythonRunner.py")        
    
    command = ''
    
    lang = recognize_language(bot_path)

        
    if lang == 'python':
        command = 'python "%s" "%s"' % (python_runner_path, bot_path)
        
    elif lang == 'csh':
        # Run with Mono if Unix. But in the future just receive source code (.cs) and compile on the fly
        if (os.name == "nt"):
            command = '"%s" "%s"' % (csh_runner_path, bot_path)
        else:
            command = 'mono --debug %s %s' % (csh_runner_path, bot_path)
    elif lang == 'java':
        command = '"%s" -jar "%s" "%s"' % (get_java_path(), java_runner_path, bot_path)
    else:
        if os.path.isdir(bot_path):
            sys.stdout.write('Couldnt find code in folder! %s\n' % (bot_path))
        else:
            sys.stdout.write('Unknown file format! %s\nPlease give file that ends with .cs , .java or .py\n' % (bot_path))
        #sys.exit(-1)
        
    #print(command)
    return command
