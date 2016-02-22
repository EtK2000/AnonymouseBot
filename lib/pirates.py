#!/usr/bin/env python
from __future__ import print_function

from random import randrange, choice, shuffle, randint, seed, random
from math import sqrt
from collections import deque, defaultdict

import base64
from fractions import Fraction
import operator
import itertools
from game import Game
from copy import deepcopy

MAX_RAND = 2147483647

PIRATES = 0
DEAD = -1
LAND = -2
FOOD = -3
WATER = -4
UNSEEN = -5
TREASURE = -6
LIGHTHOUSE= -7

PLAYER_PIRATE = 'abcdefghij'
PLAYER_UNLOAD_AREA = 'ABCDEFGHIJ'
MAP_OBJECT = 'KL$?%*.!'
MAP_RENDER = PLAYER_PIRATE + MAP_OBJECT #todo: add PLAYER_UNLOAD_AREA?? how is MAP_RENDER affecting the replay?

HILL_POINTS = 2
RAZE_POINTS = -1
NEUTRAL_ATTACKER = None

# possible directions an pirate can move
AIM = {'n': (-1, 0),
       'e': (0, 1),
       's': (1, 0),
       'w': (0, -1),
       'a': (0, 0),
       'd': (0, 0)}

# precalculated sqrt
SQRT = [int(sqrt(r)) for r in range(101)]

class Pirates(Game):
    def __init__(self, options=None):
        # setup options
        map_text = options['map']
        map_data = self.parse_map(map_text)
        # override parameters with params we got from map
        for key, val in map_data['params'].items():
            # only get valid keys - keys that already exist
            if key in options:
                options[key] = val
        self.bot_names = options['bot_names']
        self.max_turns = int(options['turns'])
        self.loadtime = int(options['loadtime'])
        self.turntime = int(options['turntime'])
        self.recover_errors = int(options['recover_errors'])
        self.viewradius = int(options['viewradius2'])
        self.fogofwar = options.get('fogofwar')
        self.attack_radius = int(options['attackradius2'])
        self.engine_seed = options.get('engine_seed', randint(-MAX_RAND-1, MAX_RAND))
        seed(self.engine_seed)
        self.player_seed = options.get('player_seed', randint(-MAX_RAND-1, MAX_RAND))
        self.cyclic = options.get('cyclic', False)
        self.cutoff_percent = options.get('cutoff_percent', 0.85)
        self.cutoff_turn = options.get('cutoff_turn', 150)

        self.do_attack = {
            'focus':   self.do_attack_focus,
            'closest': self.do_attack_closest,
            'support': self.do_attack_support,
            'damage':  self.do_attack_damage,
            'active':  self.do_attack_active
        }.get(options.get('attack'))

        self.maxpoints = int(options.get('maxpoints'))
        self.actions_per_turn = int(options.get('actions_per_turn'))
        self.reload_turns = int(options.get('reload_turns'))
        self.defense_reload_turns = int(options.get('defense_reload_turns'))
        self.defense_expiration_turns = int(options.get('defense_expiration_turns'))
        self.spawn_turns = int(options.get('spawn_turns'))
        self.sober_turns = int(options.get('sober_turns'))
        self.ghostcooldownturns = int(options.get('ghostcooldown'))
        self.treasure_spawn_turns = int(options.get('treasure_spawn_turns'))

        self.scenario = options.get('scenario', False)

        self.turn = 0
        self.num_players = map_data['num_players']

        self.current_pirates = {} # pirates that are currently alive
        self.dead_pirates = []    # pirates that are currently dead
        self.drunk_pirates = []   # pirates that are currently drunk
        self.all_pirates = []     # all pirates that have been created

        self.all_food = []     # all food created
        self.current_food = {} # food currently in game
        self.pending_food = defaultdict(int)

        self.hills = {}        # all hills
        self.hive_history = [[0] for _ in range(self.num_players)]

        self.treasures = []
        self.unload_areas = []
        self.items = []
        self.zones = dict([(player, []) for player in range(self.num_players)])
        self.lighthouses = set(map_data['lighthouses'])
        self.enemy_zones = dict([(player, []) for player in range(self.num_players)])
        self.ghost_cooldowns = [0] * self.num_players
        self.ghost_ships = [None] * self.num_players

        # used to cutoff games early
        self.cutoff = None
        self.cutoff_bot = LAND # Can be pirate owner, FOOD or LAND
        self.cutoff_turns = 0
        # used to calculate the turn when the winner took the lead
        self.winning_bot = []
        self.winning_turn = 0
        # used to calculate when the player rank last changed
        self.ranking_bots = None
        self.ranking_turn = 0

        # initialize size
        self.height, self.width = map_data['size']
        self.land_area = self.height*self.width - len(map_data['water'])

        # initialize map
        # this matrix does not track hills, just pirates
        self.map = [[LAND]*self.width for _ in range(self.height)]

        # initialize water
        for row, col in map_data['water']:
            self.map[row][col] = WATER

        # cache used by neighbourhood_offsets() to determine nearby squares
        self.offsets_cache = {}

        for id, loc in enumerate(map_data['treasures']):
            treasure = Treasure(id, loc)
            self.treasures.append(treasure)

        # initialize items
        for id, item_data in enumerate(map_data['items']):
            if item_data[0] == 'a':
                AttackItem(id, (item_data[1], item_data[2]), item_data[3], item_data[4], item_data[5])
                self.items.append(AttackItem)
            elif item_data[0] == 'd':
                DefenseItem(id, (item_data[1], item_data[2]), item_data[3], item_data[4], item_data[5])
                self.items.append(DefenseItem)
            elif item_data[0] == 'm':
                ActionItem(id, (item_data[1], item_data[2]), item_data[3], item_data[4], item_data[5])
                self.items.append(ActionItem)


        # initialize pirates
        for player, player_pirates in map_data['pirates'].items():
            for id,pirate_loc in enumerate(player_pirates):
                self.add_initial_pirate(pirate_loc, player, id)

        # initialize unload areas
        for player, player_unload_areas in map_data['unload_areas'].items():
            for unload_area_loc in player_unload_areas:
                unload_area = UnloadArea(unload_area_loc, player)
                self.unload_areas.append(unload_area)

        # initialize zones and create enemy_zone lists
        self.zones[0] = []
        self.zones[1] = []
        for player, zone_data in enumerate(map_data['zones']):
            if zone_data[0].isdigit():
                player = int(zone_data[0])
                zone_data = zone_data[1:]
            self.zones[player] += self.get_zone_locations(zone_data[0], zone_data[1:])

        # this is for the visualizer to display moves which didnt work for various reasons
        self.rejected_moves = []

        for player in range(len(self.zones)):
            # select all zones apart from current player
            enemy_zones = [z for p,z in self.zones.items() if p != player]
            # flatten list
            self.enemy_zones[player] = [loc for zone in enemy_zones for loc in zone]

        # initialize scores
        self.score = [0]*self.num_players
        self.score_history = [[s] for s in self.score]

        # used to track dead players, pirates may still exist, but orders are not processed
        self.killed = [False for _ in range(self.num_players)]

        # used to give a different ordering of players to each player
        #   initialized to ensure that each player thinks they are player 0
        self.switch = [[None]*self.num_players + list(range(-5,0)) for i in range(self.num_players)]
        for i in range(self.num_players):
            self.switch[i][i] = 0

        # used to track water and land already reveal to player
        self.revealed = [[[False for col in range(self.width)]
                          for row in range(self.height)]
                         for _ in range(self.num_players)]

        # used to track what a player can see
        self.init_vision()

        # the engine may kill players before the game starts and this is needed to prevent errors
        self.orders = [[] for i in range(self.num_players)]


    def distance(self, a_loc, b_loc):
        """ Returns distance between x and y squared """
        d_row = abs(a_loc[0] - b_loc[0])
        d_col = abs(a_loc[1] - b_loc[1])
        if self.cyclic:
            d_row = min(d_row, self.height - d_row)
            d_col = min(d_col, self.width - d_col)
        return d_row**2 + d_col**2

    def parse_map(self, map_text):
        """ Parse the map_text into a more friendly data structure """
        pirate_list = None
        unload_area_player_list = None
        zone_data = []
        width = height = None
        water = []
        food = []
        pirates = defaultdict(list)
        unload_areas = defaultdict(list)
        treasures = []
        lighthouses = []
        row = 0
        score = None
        num_players = None
        params = {}
        items = []

        for line in map_text.split('\n'):
            line = line.strip()

            # ignore blank lines and comments
            if not line or line[0] == '#':
                continue

            key, value = line.split(' ', 1)
            key = key.lower()
            if key == 'cols':
                width = int(value)
            elif key == 'rows':
                height = int(value)
            elif key == 'players':
                num_players = int(value)
                if num_players < 2 or num_players > 10:
                    raise Exception("map",
                                    "player count must be between 2 and 10")
            elif key == 'score':
                score = list(map(int, value.split()))
            elif key == 'zone':
                zone_data.append(value.split())
            elif key == 'm':
                if pirate_list is None:
                    if num_players is None:
                        raise Exception("map",
                                        "players count expected before map lines")
                    # pirates of team 'a'/'b'/'c'...
                    pirate_list = [chr(97 + i) for i in range(num_players)]
                    unload_area_player_list = [p.upper() for p in pirate_list]
                if len(value) != width:
                    raise Exception("map",
                                    "Incorrect number of cols in row %s. "
                                    "Got %s, expected %s."
                                    %(row, len(value), width))
                for col, c in enumerate(value):
                    if c in pirate_list:
                        pirates[pirate_list.index(c)].append((row,col))
                    elif c in unload_area_player_list:
                        unload_areas[unload_area_player_list.index(c)].append((row,col))
                    elif c == MAP_OBJECT[TREASURE]:
                        treasures.append((row, col))
                    elif c == MAP_OBJECT[FOOD]:
                        food.append((row,col))
                    elif c == MAP_OBJECT[WATER]:
                        water.append((row,col))
                    elif c == MAP_OBJECT[LIGHTHOUSE]:
                        lighthouses.append((row, col))
                    elif c != MAP_OBJECT[LAND]:
                        raise Exception("map",
                                        "Invalid character in map: %s" % c)
                row += 1
            elif key == 'item':
                items.append(value.split())
            else:
                # default collect all other parameters
                params[key] = value

        if score and len(score) != num_players:
            raise Exception("map",
                            "Incorrect score count.  Expected %s, got %s"
                            % (num_players, len(score)))
        if height != row:
            raise Exception("map",
                            "Incorrect number of rows.  Expected %s, got %s"
                            % (height, row))

        return {
            'size':         (height, width),
            'num_players':  num_players,
            'treasures':    treasures,
            'items':        items,
            'lighthouses':  lighthouses,
            'pirates':      pirates,
            'unload_areas': unload_areas,
            'water':        water,
            'zones':        zone_data,
            'params':       params
        }

    def neighbourhood_offsets(self, max_dist):
        """ Return a list of squares within a given distance of loc

            Loc is not included in the list
            For all squares returned: 0 < distance(loc,square) <= max_dist

            Offsets are calculated so that:
              -height <= row+offset_row < height (and similarly for col)
              negative indicies on self.map wrap thanks to python
        """
        if max_dist not in self.offsets_cache:
            offsets = []
            mx = int(sqrt(max_dist))
            for d_row in range(-mx,mx+1):
                for d_col in range(-mx,mx+1):
                    d = d_row**2 + d_col**2
                    if 0 < d <= max_dist:
                        offsets.append((
                            d_row%self.height-self.height,
                            d_col%self.width-self.width
                        ))
            self.offsets_cache[max_dist] = offsets
        return self.offsets_cache[max_dist]

    def init_vision(self):
        """ Initialise the vision data """
        # calculate and cache vision offsets
        cache = {}
        # all offsets that an pirate can see
        locs = set(self.neighbourhood_offsets(self.viewradius))
        locs.add((0,0))
        cache['new'] = list(locs)
        cache['-'] = [list(locs)]

        for d in AIM:
            # determine the previous view
            p_r, p_c = -AIM[d][0], -AIM[d][1]
            p_locs = set(
                (((p_r+r)%self.height-self.height),
                 ((p_c+c)%self.width-self.width))
                for r,c in locs
            )
            cache[d] = [list(p_locs), list(locs-p_locs), list(p_locs-locs)]
        self.vision_offsets_cache = cache

        # create vision arrays
        self.vision = []
        for _ in range(self.num_players):
            self.vision.append([[0]*self.width for __ in range(self.height)])
        # initialise the data based on the initial pirates
        self.update_vision()
        self.update_revealed()

    def update_vision(self):
        """ Incrementally updates the vision data """
        for pirate in self.current_pirates.values():
            if not pirate.orders:
                # new pirate
                self.update_vision_pirate(pirate, self.vision_offsets_cache['new'], 1)
            else:
                order = pirate.orders[-1]
                if order in AIM:
                    # pirate moved
                    self.update_vision_pirate(pirate, self.vision_offsets_cache[order][1], 1)
                    self.update_vision_pirate(pirate, self.vision_offsets_cache[order][-1], -1)
                # else: pirate stayed where it was
        for pirate in self.killed_pirates():
            orders = pirate.orders[-1]
            #################################################################
            ####### ilya need to review!!!!!!!!!!!!!
            #################################################################
            for order in orders:
                if order in AIM:
                    self.update_vision_pirate(pirate, self.vision_offsets_cache[order][0], -1)

    def update_vision_pirate(self, pirate, offsets, delta):
        """ Update the vision data for a single pirate

            Increments all the given offsets by delta for the vision
              data for pirate.owner
        """
        a_row, a_col = pirate.loc
        vision = self.vision[pirate.owner]
        for v_row, v_col in offsets:
            # offsets are such that there is never an IndexError
            vision[a_row+v_row][a_col+v_col] += delta

    def update_revealed(self):
        """ Make updates to state based on what each player can see

            Update self.revealed to reflect the updated vision
            Update self.switch for any new enemies
            Update self.revealed_water
        """
        self.revealed_water = []
        for player in range(self.num_players):
            water = []
            revealed = self.revealed[player]
            switch = self.switch[player]

            for row, squares in enumerate(self.vision[player]):
                for col, visible in enumerate(squares):
                    if not visible:
                        continue

                    value = self.map[row][col]

                    # if this player encounters a new enemy then
                    #   assign the enemy the next index
                    if value >= PIRATES and switch[value] is None:
                        switch[value] = self.num_players - switch.count(None)

                    # mark square as revealed and determine if we see any
                    #   new water
                    if not revealed[row][col]:
                        revealed[row][col] = True
                        if value == WATER or (row, col) in self.enemy_zones[player]:
                            water.append((row,col))

            # update the water which was revealed this turn
            self.revealed_water.append(water)

    def get_perspective(self, player=None):
        """ Get the map from the perspective of the given player

            If player is None, the map is return unaltered.
            Squares that are outside of the player's vision are
               marked as UNSEEN.
            Enemy identifiers are changed to reflect the order in
               which the player first saw them.
        """
        if player is not None:
            v = self.vision[player]
        result = []
        for row, squares in enumerate(self.map):
            map_row = []
            for col, square in enumerate(squares):
                if player is None or v[row][col]:
                    if (row,col) in self.hills:
                        if (row,col) in self.current_pirates:
                            # assume pirate is hill owner
                            # numbers should be divisible by the length of PLAYER_PIRATE
                            map_row.append(square+10)
                        else:
                            map_row.append(square+20)
                    else:
                        map_row.append(square)
                else:
                    map_row.append(UNSEEN)
            result.append(map_row)
        return result

    def render_changes(self, player):
        """ Create a string which communicates the updates to the state

            Water which is seen for the first time is included.
            All visible transient objects (pirates, food) are included.
        """
        updates = self.get_state_changes()
        v = self.vision[player]
        visible_updates = []
        # first add unseen water
        for row, col in self.revealed_water[player]:
            visible_updates.append(['w', row, col])

        # next list all transient objects
        for update in updates:
            if update[0] == 't' or update[0] == 'u':
                visible_updates.append(update)
                continue
            
            ilk, id, row, col, owner = update[0:5]

            # only include updates to squares which are (visible) or (where a player ant just died) or (a fort)
            # if fog of war flag not set then we always display visible results
            if not self.fogofwar or v[row][col] or ((ilk == 'k') and update[4] == player) or (ilk == 'f'):
                visible_updates.append(update)

                # switch player perspective of player numbers
                if ilk in ['a', 'k', 'f']:
                    # if pirate is enemie's and cloaked - we need to send a wrong locatoin
                    if ilk is 'a' and not owner == player and update[7] == int(True):
                        update[2] = update[3] = -1
                    update[4] = self.switch[player][owner]

        visible_updates.append(['g','s'] + self.order_for_player(player, self.score))
        visible_updates.append(['g','c'] + self.order_for_player(player, self.ghost_cooldowns))
        visible_updates.append(['g','p'] + self.order_for_player(player, self.get_last_turn_points()))
        visible_updates.append([]) # newline
        return '\n'.join(' '.join(map(str,s)) for s in visible_updates)

    def get_state_changes(self):
        """ Return a list of all transient objects on the map.

            Food, living pirates, pirates killed this turn
            Changes are sorted so that the same state will result in the same output
        """

        changes = []
        changes.extend(sorted(
            ['t', treasure.id, treasure.initial_loc[0], treasure.initial_loc[1]]
            for treasure in self.treasures if treasure.is_available
        ))

        # current pirates
        changes.extend(sorted(
            ['a', pirate.id, pirate.loc[0], pirate.loc[1], pirate.owner, pirate.initial_loc[0], pirate.initial_loc[1], int(pirate.sober_turns), int(pirate.treasure is not None), pirate.reload_turns, pirate.defense_reload_turns, pirate.defense_expiration_turns_counter]
            for pirate in self.current_pirates.values()
        ))

        # killed pirates
        changes.extend(sorted(
            ['k', pirate.id, pirate.loc[0], pirate.loc[1], pirate.owner, pirate.initial_loc[0], pirate.initial_loc[1], self.turns_till_revive(pirate)]
            for pirate in self.dead_pirates
        ))

        # unload areas
        changes.extend(sorted(
            ['u', unload_area.loc[0], unload_area.loc[1], unload_area.owner]
            for unload_area in self.unload_areas
        ))

        return changes

    def get_zone_locations(self, mode, params):
        """ Returns a list of locations that are in a zone.
            Modes may be rect or radius to specify different types of zones.
            Zones do not change throughout the game. Each zone belongs to a player.
        """
        zone_locations = []
        if mode == 'rect':
            assert len(params) == 4, 'Requires 4 parameters for rect zone'
            # in this line the rows/cols get modulated by width/height appropriately so zone selection is easy
            fromrow, fromcol, torow, tocol = [int(param) % [self.height,self.width][i % 2] for i,param in enumerate(params)]
            for r in range(fromrow, torow+1):
                for c in range(fromcol, tocol+1):
                    zone_locations.append((r,c))
        if mode == 'radius':
            assert len(params) == 3, 'Requires 4 parameters for radius zone'
            row, col, rad = [int(i) for i in params]
            row = row % self.height
            col = col % self.width
            pirates = []
            zone_locations.append((row, col))
            for d_row, d_col in self.neighbourhood_offsets(rad):
                new_loc = ((row+d_row) % self.height, (col+d_col) % self.width)
                if self.cyclic or self.distance(new_loc,(row,col)) <= rad:
                    n_loc = self.destination((row, col), (d_row, d_col))
                    zone_locations.append(n_loc)
        return zone_locations

    def get_map_output(self, player=None, replay=False):
        """ Render the map from the perspective of the given player.

            If player is None, then no squares are hidden and player ids
              are not reordered.
            TODO: get this function working
        """
        result = []
        if replay and self.scenario:
            for row in self.original_map:
                result.append(''.join([MAP_RENDER[col] for col in row]))
        else:
            for row in self.get_perspective(player):
                result.append(''.join([MAP_RENDER[col] for col in row]))
        return result

    def nearby_pirates(self, loc, max_dist, exclude=None, exclude_drunk=True):
        """ Returns pirates where 0 < dist to loc <= sqrt(max_dist)

            If exclude is not None, pirates with owner == exclude
              will be ignored.
        """
        # TODO
        pirates = []
        row, col = loc
        for d_row, d_col in self.neighbourhood_offsets(max_dist):
            # this if will prevent finding enemies through the side of the map if the self.cyclic option is set to false. May make game slower if max_dist is very big and suggest thinking of a way to improve performance in some smarter way.
            # quick tip - the indices of (row+d_row) for example are sometimes negative and sometimes positive and use pythons negative indexing to work well.
            new_loc = ((row+d_row) % self.height, (col+d_col) % self.width)
            if self.cyclic or self.distance(new_loc,(row,col)) <= self.attack_radius:
                if PIRATES <= self.map[row+d_row][col+d_col] != exclude:
                    n_loc = self.destination(loc, (d_row, d_col))
                    if exclude_drunk and self.current_pirates[n_loc].sober_turns > 0:
                        continue
                    pirates.append(self.current_pirates[n_loc])
        return pirates

    def parse_orders(self, player, lines):
        """ Parse orders from the given player

            Orders must be of the form: o row col direction
            row, col must be integers
            direction must be in (n,s,e,w)
            Messages must be of the form: m message
        """
        orders = []
        valid = []
        ignored = []
        invalid = []
        action_counter = 0
        for line in lines:
            line = line.strip()
            # ignore blank lines and comments
            if not line or line[0] == '#':
                continue

            line = line.lower()
            data = line.split()

            # validate data format
            if data[0] == 'm':
                # was a debug message - printed in engine
                continue

            if data[0] == 's':
                #stop point
                #stop_messages.append(data[1])
                # like debug message - printed in engine
                continue

            if data[0] != 'o':
                invalid.append((line, 'unknown action'))
                continue
            if len(data) != 4:
                invalid.append((line, 'incorrectly formatted order'))
                continue

            row, col, directions = data[1:4]
            args = data[4:]
            # validate the data types
            try:
                loc = int(row), int(col)
            except ValueError:
                invalid.append((line,'invalid row or col'))
                continue

            # if not self.correct_attack_parse(player, directions):
            #     invalid.append((line,'invalid attack command'))
            #     continue

            if 'a' not in directions and 'd' not in directions and any(d not in AIM for d in directions):
                invalid.append((line,'invalid directions'))
                continue

            # check that attack order is the last order for a pirate
            only_direction_letters = [d for d in directions if d in AIM and d != 'a' and d != 'd']
            # attack_first_occurrence = ''.join(only_direction_letters).find('a')
            # if attack_first_occurrence != -1 and attack_first_occurrence != len(only_direction_letters)-1:
            #
            #     invalid.append((line,'invalid directions - attack not last order'))
            #     continue

            # check that defense order is the last order for a pirate
            # defend_first_occurrence = ''.join(only_direction_letters).find('d')
            # if defend_first_occurrence != -1 and defend_first_occurrence != len(only_direction_letters)-1:
            # # if directions.find('d') != -1 and len(directions) > 1:
            #
            #     invalid.append((line,'invalid directions - defend not last order'))
            #     continue

            # count total directions of player's pirates

            action_counter += len(only_direction_letters) # only movement!

            if action_counter > self.actions_per_turn:

                invalid.append((line,'total actions per turn (%s) exceeded allowed maximum (%s)' % (action_counter, self.actions_per_turn)))
                orders = []
                valid = []
                break

            #todo: do we ignore all moves or just the moves that exceeded the max

            # this order can be parsed
            orders.append((loc, directions, args))
            valid.append(line)

        return orders, valid, ignored, invalid

    def correct_attack_parse(self, player, directions):
        i = 1
        while i < len(directions):
            # check for attack target validity
            if directions[i-1] == 'a':
                p_id_str = ""
                while i < len(directions) and directions[i].isdigit():
                    p_id_str += directions[i]
                    i += 1
                if p_id_str == "" or int(p_id_str) not in [p.id for p in self.get_physical_pirates() if player != p.owner]:
                    return False
            i += 1
        return True

    def validate_orders(self, player, orders, lines, ignored, invalid):
        """ Validate orders from a given player

            Location (row, col) must be pirate belonging to the player
            direction must not be blocked
            may not enter other team's zone
            Can't multiple orders to one pirate
        """
        valid = []
        valid_orders = []
        seen_locations = set()

        # each line refers to a single pirate ship
        # each pirate ship may move to more than 1 direction, and may attack at the end
        for line, (loc, directions, order_args) in zip(lines, orders):
            only_direction_letters = ''.join([d for d in directions if d in AIM and d != 'a' and d != 'd'])
            # validate orders
            if loc in seen_locations:
                invalid.append((line, 'duplicate order'))
                continue
            try:
                if self.map[loc[0]][loc[1]] != player:
                    invalid.append((line, 'You tried to move a pirate but dont have one at this location'))
                    continue
            except IndexError:
                invalid.append((line, 'out of bounds'))
                continue
            if loc[0] < 0 or loc[1] < 0:
                invalid.append((line, 'out of bounds'))
                continue

            p = self.current_pirates[loc]

            # validate that attack or defense are standalone
            only_actions_letters = ''.join([d for d in directions if d in AIM])
            if 'a' in only_actions_letters and len(only_actions_letters) > 1:
                invalid.append((line, 'Attacking ship cannot defend, move or attack more than once in the same turn'))
                continue
            if 'd' in only_actions_letters and len(only_actions_letters) > 1:
                invalid.append((line, 'Defending ship cannot attack, move or defend more than once in the same turn'))
                continue

            #validate that ship cannot attack if it's reloading
            if 'a' in only_actions_letters and self.current_pirates[loc].reload_turns > 0:
                ignored.append((line, 'Attack ignored - pirate ship is reloading'))
                continue

            #validate that ship cannot defend if it's reloading
            if 'd' in only_actions_letters and self.current_pirates[loc].defense_reload_turns > 0:
                ignored.append((line, 'Defense ignored - pirate ship is reloading'))
                continue

            if p.treasure is not None and len(only_direction_letters) > 1:
                invalid.append((line, 'Cannot move more than 1 step if carrying a treasure'))
                continue

            if p.treasure is not None and 'a' in only_actions_letters:
                invalid.append((line, 'Cannot attack while carrying a treasure'))
                continue

            if p.sober_turns > 0: #--> sober_turns is updated only the first turn the pirate is drunk, so this doesn't hold value.
                invalid.append((line, 'The pirate is drunk - can\'t do anything'))
                continue

            if self.check_simulated_pirate_move(loc, only_direction_letters, player, ignored, line) is False:
                continue

            # this order is valid!
            valid_orders.append((loc, directions, order_args))
            valid.append(line)
            seen_locations.add(loc)

        return valid_orders, valid, ignored, invalid

    def check_simulated_pirate_move(self, loc, directions, player, ignored, line):
        # make sure current loc + all moves of current pirate (iteratively!) are valid.
        # e.g.: 'n,n,w,w' - n1 allowed, loc updates. n2 not allowed - entire order is ignored.
        current_loc = loc
        for direction in directions:
            future_loc = self.destination(current_loc, AIM[direction])
            if self.map[future_loc[0]][future_loc[1]] in (FOOD, WATER):
                ignored.append((line,'move blocked'))
                return False
            if self.distance(current_loc,future_loc) > 1 and not self.cyclic:
                ignored.append((line,'move blocked - cant move out of map'))
                self.rejected_moves.append([self.turn, current_loc[0], current_loc[1], direction])
                return False
            if future_loc in self.enemy_zones[player]:
                ignored.append((line,'move blocked - entering enemy zone'))
                self.rejected_moves.append([self.turn, current_loc[0], current_loc[1], direction])
                return False
            current_loc = future_loc
        return True

    def do_orders(self):
        """ Execute player orders and handle conflicts

            All pirates are moved to their new positions.
            Any pirates which occupy the same square are killed.
        """
        # set old pirate locations to land
        for pirate in self.current_pirates.values():
            row, col = pirate.loc
            self.map[row][col] = LAND

        # determine the direction that each pirate moves
        #  (holding any pirates that don't have orders)
        move_direction = {}
        for orders in self.orders:
            for loc, direction, args in orders:

                if direction == 'a':
                    target_id = int(direction[direction.find('a')+1:])
                    move_direction[self.current_pirates[loc]] = (('a',target_id), args)
                    break
                else:
                    move_direction[self.current_pirates[loc]] = (direction, args)


        for pirate in self.current_pirates.values():
            if pirate not in move_direction:
                move_direction[pirate] = ('-', [])

        # move all the pirates
        next_loc = defaultdict(list)
        for pirate, (direction, order_args) in move_direction.items():
            new_loc = pirate.loc
            for d in direction:

                if d == 'a':

                    # pirate is attacking this turn
                    pirate.attack_turns.extend((self.turn, int(direction[-1])))
                else:
                    new_loc = self.destination(new_loc, AIM.get(d, (0,0)))
                    if d == 'd':
                        # pirate is defending this turn
                        pirate.defense_expiration_turns_counter = pirate.defense_expiration_turns
                        # pirate.is_defensive = True
            pirate.loc = new_loc
            pirate.orders.append(direction)
            next_loc[pirate.loc].append(pirate)
            # defense aura is on
            if pirate.defense_expiration_turns_counter > 0:
                pirate.defense_turns.append(self.turn)


        # if pirate is sole occupy of a new square then it survives
        self.current_pirates = {}
        colliding_pirates = []
        for loc, pirates in next_loc.items():
            if len(pirates) == 1:
                self.current_pirates[loc] = pirates[0]
            else:
                for pirate in pirates:
                    self.kill_pirate(pirate, True)
                    colliding_pirates.append(pirate)

        # set new pirate locations
        for pirate in self.current_pirates.values():
            row, col = pirate.loc
            self.map[row][col] = pirate.owner

    def do_cloaks(self):
        ''' Lower cooldowns for all teams that don't have a ghost ship '''
        for player, ship in enumerate(self.ghost_ships):
            if ship is None and self.ghost_cooldowns[player] > 0:
                self.ghost_cooldowns[player] -= 1

    def do_defense(self):
        for p in self.get_physical_pirates():
            # if defense expiration is full and defense was activated this turn, start counting defense reload time
            if p.defense_expiration_turns_counter == p.defense_expiration_turns and p.defense_turns[-1] == self.turn:
                p.defense_reload_turns = self.defense_reload_turns
            else:
                if p.defense_reload_turns > 0:
                    p.defense_reload_turns -= 1
            # count defense expiration
            if p.defense_expiration_turns_counter > 0:
                p.defense_expiration_turns_counter -= 1


    def do_sober(self):
        # handles the drunk pirates
        pirates_to_sober = []
        for pirate in self.all_pirates:
            if pirate in self.drunk_pirates:
                pirate.drink_history.append(True)
                # if pirate.drink_turns[-1] == self.turn: #meaning that this is the first turn for being drunk
                #     pirate.sober_turns = self.sober_turns
                if pirate.sober_turns > 0:
                    pirate.sober_turns -= 1
                    # calculate if the turn has come to sober
                if pirate.sober_turns == 0:
                    pirates_to_sober.append(pirate)
            else:
                pirate.drink_history.append(False)

        for pirate in pirates_to_sober:
            self.drunk_pirates.remove(pirate)

    def do_spawn(self):
        # handles the reviving of dead pirates
        pirates_to_revive = []
        for pirate in self.dead_pirates:

            # calculate if the turn has come to revive
            if self.turn - pirate.die_turn >= self.spawn_turns:
                # verify no one standing in the pirate's location
                if pirate.initial_loc not in self.current_pirates:
                    pirates_to_revive.append(pirate)

        # remove pirate from dead list and make new one in the alive
        for pirate in pirates_to_revive:
            self.dead_pirates.remove(pirate)
            owner = pirate.owner
            loc = pirate.initial_loc
            new_pirate = Pirate(loc, owner, pirate.id, self.attack_radius, self.defense_expiration_turns, self.turn)
            row, col = loc
            self.map[row][col] = owner
            self.all_pirates.append(new_pirate)
            self.current_pirates[loc] = new_pirate

    def get_last_turn_points(self):
        """ Get points achieved on last turns """
        if len(self.score_history[0]) < 2:
            return self.score
        return [self.score_history[player][-1] - self.score_history[player][-2] for player in range(self.num_players)]

    def killed_pirates(self):
        """ Return pirates that were killed this turn """
        return [dead for dead in self.dead_pirates if dead.die_turn == self.turn]

    def turns_till_revive(self, pirate):
        return self.spawn_turns - (self.turn - pirate.die_turn)

    # def add_pirate(self, hill):
    #     """ Spawn an pirate on a hill
    #     """
    #     loc = hill.loc
    #     owner = hill.owner
    #     pirate = Pirate(loc, owner, self.turn)
    #     row, col = loc
    #     self.map[row][col] = owner
    #     self.all_pirates.append(pirate)
    #     self.current_pirates[loc] = pirate
    #     hill.last_touched = self.turn
    #     return pirate

    def add_initial_pirate(self, loc, owner, id):
        pirate = Pirate(loc, owner, id, self.attack_radius, self.defense_expiration_turns, self.turn)
        row, col = loc
        self.map[row][col] = owner
        self.all_pirates.append(pirate)
        self.current_pirates[loc] = pirate
        return pirate

    def drunk_pirate(self, pirate):
        """ Drunk the pirate at the given location
            Raises an error if no pirate is found at the location (if ignore error is set to False)
        """
        
        # if the drunk pirate holds treasure
        if pirate.treasure:
            #release it
            pirate.treasure.is_available = True
            pirate.treasure = None

        self.drunk_pirates.append(pirate)
        pirate.drink_turns.append(self.turn+1)
        pirate.sober_turns = self.sober_turns


    def kill_pirate(self, pirate, ignore_error=False):
        """ Kill the pirate at the given location
            Raises an error if no pirate is found at the location (if ignore error is set to False)
        """

        try:
            # if the killed pirate holds treasure
            if pirate.treasure:
                #release it
                pirate.treasure.is_available = True
                pirate.treasure = None

            loc = pirate.loc
            self.map[loc[0]][loc[1]] = LAND
            self.dead_pirates.append(pirate)
            pirate.die_turn = self.turn

            return self.current_pirates.pop(loc)
        except KeyError:
            if not ignore_error:
                raise Exception("Kill pirate error",
                                "Pirate not found at %s" %(loc,))

    def player_pirates(self, player):
        """ Return the current and dead pirates belonging to the given player """
        return [pirate for pirate in self.current_pirates.values() + self.dead_pirates if player == pirate.owner]

    def do_attack_damage(self):
        """ Kill pirates which take more than 1 damage in a turn

            Each pirate deals 1/#nearby_enemy damage to each nearby enemy.
              (nearby enemies are those within the attack_radius)
            Any pirate with at least 1 damage dies.
            Damage does not accumulate over turns
              (ie, pirates heal at the end of the battle).
        """
        damage = defaultdict(Fraction)
        nearby_enemies = {}

        # each pirate damages nearby enemies
        for pirate in self.current_pirates.values():
            enemies = self.nearby_pirates(pirate.loc, self.attack_radius, pirate.owner)
            if enemies:
                nearby_enemies[pirate] = enemies
                strenth = 10 # dot dot dot
                if pirate.orders[-1] == '-':
                    strenth = 10
                else:
                    strenth = 10
                damage_per_enemy = Fraction(strenth, len(enemies)*10)
                for enemy in enemies:
                    damage[enemy] += damage_per_enemy

        # kill pirates with at least 1 damage
        for pirate in damage:
            if damage[pirate] >= 1:
                self.kill_pirate(pirate)

    def in_attack_range(self, attacker, target):
        loc1 = attacker.loc
        loc2 = target.loc
        d_row, d_col = loc1[0]-loc2[0],loc1[1]-loc2[1]
        d = d_row**2 + d_col**2
        if 0 < d <= attacker.attack_radius:
            return True
        return False

    def do_attack_active(self):
        """ Kill pirates 
        """

        # map pirates (to be killed) to the enemies that kill it
        pirates_to_drunk = set()
        for pirate in self.get_physical_pirates():

            if pirate.attack_turns[-2] != self.turn: # [-2] is the last turn attack was made. [-1] is the attack target

                if pirate.reload_turns > 0:
                    pirate.reload_turns -= 1
                continue
            pirate.reload_turns = self.reload_turns

            # attack turn
            target_pirate = next((p for p in self.get_physical_pirates() if p.owner != pirate.owner and p.id == pirate.attack_turns[-1]), None)
            if target_pirate:
                if self.in_attack_range(pirate, target_pirate) and target_pirate.sober_turns == 0 and target_pirate.defense_turns[-1] != self.turn:  # target not drunk and did not defend and in attack range
                    pirates_to_drunk.add(target_pirate)

        for pirate in pirates_to_drunk:
            self.drunk_pirate(pirate)

    def do_attack_support(self):
        """ Kill pirates which have more enemies nearby than friendly pirates

            An pirate dies if the number of enemy pirates within the attack_radius
            is greater than the number of friendly pirates within the attack_radius.
            The current pirate is not counted in the friendly pirate count.

            1 point is distributed evenly among the enemies of the dead pirate.
        """
        # map pirates (to be killed) to the enemies that kill it
        pirates_to_kill = {}
        lighthouse_pirates_to_kill = set()

        for pirate in self.get_physical_pirates():
            enemies = []
            friends = []
            if pirate.loc in self.lighthouses:
                # using different mechanism for lighthouses
                nearby_enemy_pirates = filter(lambda p: p.owner != pirate.owner and not p.is_cloaked,
                    self.nearby_pirates(pirate.loc, self.attack_radius))
                if len(nearby_enemy_pirates) > 1:
                    lighthouse_pirates_to_kill.add(pirate)
                else:
                    for enemy_pirate in nearby_enemy_pirates:
                        lighthouse_pirates_to_kill.add(enemy_pirate)
                continue

            # sort nearby pirates into friend and enemy lists
            # TODO: this line was bugged. neatby_pirates got pirate.owner as third param and didnt work. why???
            for nearby_pirate in self.nearby_pirates(pirate.loc, self.attack_radius):
                # ignore pirates that are cloaked or on lighthouse
                if nearby_pirate.is_cloaked or nearby_pirate.loc in self.lighthouses:
                    continue
            # add the support an pirate has
            pirate.supporters.append(len(friends))
            # add pirate to kill list if it doesn't have enough support
            if len(friends) < len(enemies):
                pirates_to_kill[pirate] = enemies

        # actually do the killing and score distribution
        all_pirates_to_kill = lighthouse_pirates_to_kill.union(pirates_to_kill.keys())
        for pirate in all_pirates_to_kill:
            self.kill_pirate(pirate)

    def do_attack_focus(self):
        """ Kill pirates which are the most surrounded by enemies

            For a given pirate define: Focus = 1/NumOpponents
            An pirate's Opponents are enemy pirates which are within the attack_radius.
            Pirate alive if its Focus is greater than Focus of any of his Opponents.
            If an pirate dies 1 point is shared equally between its Opponents.
        """
        # maps pirates to nearby enemies
        nearby_enemies = {}
        for pirate in self.current_pirates.values():
            nearby_enemies[pirate] = self.nearby_pirates(pirate.loc, self.attack_radius, pirate.owner)

        # determine which pirates to kill
        pirates_to_kill = []
        for pirate in self.current_pirates.values():
            # determine this pirates weakness (1/focus)
            weakness = len(nearby_enemies[pirate])
            # an pirate with no enemies nearby can't be attacked
            if weakness == 0:
                continue
            # determine the most focused nearby enemy
            min_enemy_weakness = min(len(nearby_enemies[enemy]) for enemy in nearby_enemies[pirate])
            # pirate dies if it is weak as or weaker than an enemy weakness
            if min_enemy_weakness <= weakness:
                pirates_to_kill.append(pirate)

        # kill pirates and distribute score
        for pirate in pirates_to_kill:
            self.kill_pirate(pirate)

    def do_attack_closest(self):
        """ Iteratively kill neighboring groups of pirates """
        # maps pirates to nearby enemies by distance
        pirates_by_distance = {}
        for pirate in self.current_pirates.values():
            # pre-compute distance to each enemy in range
            dist_map = defaultdict(list)
            for enemy in self.nearby_pirates(pirate.loc, self.attack_radius, pirate.owner):
                dist_map[self.distance(pirate.loc, enemy.loc)].append(enemy)
            pirates_by_distance[pirate] = dist_map

        # create helper method to find pirate groups
        pirate_group = set()
        def find_enemy(pirate, distance):
            """ Recursively finds a group of pirates to eliminate each other """
            # we only need to check pirates at the given distance, because closer
            #   pirates would have been eliminated already
            for enemy in pirates_by_distance[pirate][distance]:
                if not enemy.killed and enemy not in pirate_group:
                    pirate_group.add(enemy)
                    find_enemy(enemy, distance)

        # setup done - start the killing
        for distance in range(1, self.attack_radius):
            for pirate in self.current_pirates.values():
                if not pirates_by_distance[pirate] or pirate.killed:
                    continue

                pirate_group = set([pirate])
                find_enemy(pirate, distance)

                # kill all pirates in groups with more than 1 pirate
                #  this way of killing is order-independent because the
                #  the pirate group is the same regardless of which pirate
                #  you start looking at
                if len(pirate_group) > 1:
                    for pirate in pirate_group:
                        self.kill_pirate(pirate)

    def do_treasures(self):
        """ Calculates Treasures logic
        """
        available_treasures = [t for t in self.treasures if t.is_available]

        # if pirate already has a treasure, update treasure history and ignore the rest
        # check if pirate location is an existing treasure location
        # if yes, pick it up and update treasure history
        # if not, update location history
        for p in self.current_pirates.values():
            if p.treasure:
                if p.loc != p.initial_loc:
                #if not p.loc in [ua.loc for ua in self.unload_areas if ua.owner == p.owner]:
                    p.treasure_history.append(True)
                else:
                    p.treasure_history.append(False)
                    # when ship unloads treasure, start counting spawn turns for the treasure
                    p.treasure.spawn_turns = self.treasure_spawn_turns
                    p.treasure = None
                    #update score
                    self.score[p.owner] += 1
            else:
                # if pirate doesnt hold a treasure AND is in an available treasure location, pick it up
                p.treasure = next((t for t in available_treasures
                                        if p.loc == t.initial_loc and p not in self.drunk_pirates),
                                  None)
                if p.treasure: # drunk pirates cant pick up treasures
                    p.treasure_history.append(True)
                    p.treasure.is_available = False
                else:
                    p.treasure_history.append(False)

        for t in self.treasures:
            t.is_available_history.append(t.is_available)
            if t.spawn_turns > 0:
                t.spawn_turns -= 1
            if t.spawn_turns == 0:
                t.is_available = True
                t.spawn_turns = -1


    def do_items(self):
        """ Calculates item logic
        """
        available_items = [i for i in self.items if self.turn >= i.start_turn and self.turn <= i.end_turn]

        for p in self.current_pirates.values():
            # if item already activated
            if p.attack_item_active_turns > 0:
                p.attack_item_active_turns -= 1
            else:
                p.attack_radius = self.attack_radius
            if p.defense_item_active_turns > 0:
                p.defense_item_active_turns -= 1
            else:
                p.defense_expiration_turns = self.defense_expiration_turns

            # check if pirate is standing on an item
            item = next((i for i in available_items if p.loc == i.loc), None)
            if item:
                item.end_turn = self.turn
            if isinstance(item, AttackItem):
                p.attack_radius = item.attack_radius
                p.attack_item_active_turns = item.active_turns
            if isinstance(item, DefenseItem):
                p.defense_expiration_turns = item.defense_expiration_turns
                p.defense_item_active_turns = item.active_turns
            if isinstance(item, ActionItem):
                pass #todo - item.actions_per_turn


    def get_physical_pirates(self):
        return filter(lambda pirate: not pirate.is_cloaked, self.current_pirates.values())

    def destination(self, loc, d):
        """ Returns the location produced by offsetting loc by d """
        return ((loc[0] + d[0]) % self.height, (loc[1] + d[1]) % self.width)

    def find_closest_land(self, coord):
        """ Find the closest square to coord which is a land square using BFS

            Return None if no square is found
        """
        if self.map[coord[0]][coord[1]] == LAND:
            return coord

        visited = set()
        square_queue = deque([coord])

        while square_queue:
            c_loc = square_queue.popleft()

            for d in AIM.values():
                n_loc = self.destination(c_loc, d)
                if n_loc in visited: continue

                if self.map[n_loc[0]][n_loc[1]] == LAND:
                    return n_loc

                visited.add(n_loc)
                square_queue.append(n_loc)

        return None

    def get_initial_vision_squares(self):
        """ Get initial squares in bots vision that are traversable

            flood fill from each starting hill up to the vision radius
        """
        vision_squares = {}
        for hill in self.hills.values():
            squares = deque()
            squares.append(hill.loc)
            while squares:
                c_loc = squares.popleft()
                vision_squares[c_loc] = True
                for d in AIM.values():
                    n_loc = self.destination(c_loc, d)
                    if (n_loc not in vision_squares
                            and self.map[n_loc[0]][n_loc[1]] != WATER and
                            self.distance(hill.loc, n_loc) <= self.viewradius):
                        squares.append(n_loc)
        return vision_squares

    def remaining_players(self):
        """ Return the players still alive """
        return [p for p in range(self.num_players) if self.is_alive(p)]

    # Common functions for all games
    def game_over(self):
        """ Determine if the game is over

            Used by the engine to determine when to finish the game.
            A game is over when there are no players remaining, or a single
            player remaining or a player reached the point maximum.
        """
        if len(self.remaining_players()) < 1:
            self.cutoff = 'No bots left'
            self.winning_bot = []
            return True
        if len(self.remaining_players()) == 1:
            self.winning_bot = self.remaining_players()
            #                                    The NON winning bot, it the crashed one
            self.cutoff = 'Bot crashed'
            return True
        if max(self.score) >= self.maxpoints:
            self.cutoff = 'Maximum points'
            return True
        return False

    def get_winner(self):
        """ Returns the winner of the game

            The winner is defined as the player with the most points.
            In case other bots crash the remaining bot will win automatically.
            If remaining bots crash on same turn - there will be no winner.
        """
        return self.winning_bot

    def kill_player(self, player):
        """ Used by engine to signal that a player is out of the game """
        self.killed[player] = True

    def start_game(self):
        """ Called by engine at the start of the game """
        pass

    def finish_game(self):
        """ Called by engine at the end of the game """
        if self.cutoff is None:
            self.cutoff = 'Turn limit reached'
            if self.get_winner() and len(self.get_winner()) == 1:
                self.cutoff += ', Bot [' + self.bot_names[self.winning_bot[0]] + '] won'
            else:
                self.cutoff += ', there is no winner'
            self.calc_significpirate_turns()

    def start_turn(self):
        """ Called by engine at the start of the turn """
        self.turn += 1
        #self.dead_pirates = []
        self.revealed_water = [[] for _ in range(self.num_players)]
        self.removed_food = [[] for _ in range(self.num_players)]
        self.orders = [[] for _ in range(self.num_players)]

    def finish_turn(self):
        """ Called by engine at the end of the turn """
        self.do_orders() #moves the pirates on the map
        self.do_sober() #handles drunk history and removes drunk pirates who are sober
        self.do_attack() #handles attacking pirates
        self.do_defense() #handles defending pirates
        self.do_treasures() #handles treasure - collecting and unloading
        self.do_items() #handles power ups
        self.do_spawn() #spawns new pirates


        # calculate the score for history
        for player in range(self.num_players):
            self.score_history[player].append(self.score[player])

        # now that all the pirates have moved (or sunk) we can update the vision
        self.update_vision()
        self.update_revealed()

        self.calc_significpirate_turns()

    def calc_significpirate_turns(self):
        ranking_bots = [sorted(self.score, reverse=True).index(x) for x in self.score]
        if self.ranking_bots != ranking_bots:
            self.ranking_turn = self.turn
        self.ranking_bots = ranking_bots

        winning_bot = [p for p in range(len(self.score)) if self.score[p] == max(self.score)]
        if self.winning_bot != winning_bot:
            self.winning_turn = self.turn
        self.winning_bot = winning_bot

    def get_state(self):
        """ Get all state changes

            Used by engine for streaming playback
        """
        updates = self.get_state_changes()
        updates.append([]) # newline

        return '\n'.join(' '.join(map(str,s)) for s in updates)

    def get_player_start(self, player=None):
        """ Get game parameters visible to players

            Used by engine to send bots startup info on turn 0
        """
        result = []
        result.append(['turn', 0])
        result.append(['loadtime', self.loadtime])
        result.append(['turntime', self.turntime])
        result.append(['recover_errors', self.recover_errors])
        result.append(['rows', self.height])
        result.append(['cols', self.width])
        result.append(['max_turns', self.max_turns])
        result.append(['viewradius2', self.viewradius])
        result.append(['attackradius2', self.attack_radius])
        result.append(['player_seed', self.player_seed])
        # send whether map is cyclic or not
        result.append(['cyclic', int(self.cyclic)])
        result.append(['ghost_cooldown', self.ghostcooldownturns])

        result.append(['numplayers', self.num_players])
        result.append(['spawn_turns', self.spawn_turns])
        result.append(['sober_turns', self.sober_turns])
        result.append(['maxpoints', self.maxpoints])
        result.append(['actions_per_turn', self.actions_per_turn])
        result.append(['reload_turns', self.reload_turns])
        result.append(['defense_reload_turns', self.defense_reload_turns])
        result.append(['defense_expiration_turns', self.defense_expiration_turns])
        result.append(['treasure_spawn_turns', self.treasure_spawn_turns])

        for lighthouse in self.lighthouses:
            result.append(['lighthouse', lighthouse[0], lighthouse[1]])

        # information hidden from players
        if player is None:
            for line in self.get_map_output():
                result.append(['m',line])
        else:
            bot_names = self.order_for_player(player, self.bot_names)
            result.append(['bot_names', len(bot_names)] + bot_names)

            
        result.append([]) # newline
        return '\n'.join(' '.join(map(str,s)) for s in result)

    def get_player_state(self, player):
        """ Get state changes visible to player

            Used by engine to send state to bots
        """
        return self.render_changes(player)

    def is_alive(self, player):
        """ Determine if player is still alive

            Used by engine to determine players still in the game
        """
        if self.killed[player]:
            return False
        else:
            return bool(self.player_pirates(player))

    def get_error(self, player):
        """ Returns the reason a player was killed

            Used by engine to report the error that kicked a player
              from the game
        """
        return ''

    def do_moves(self, player, moves):
        """ Called by engine to give latest player orders """
        orders, valid, ignored, invalid = self.parse_orders(player, moves)
        orders, valid, ignored, invalid = self.validate_orders(player, orders, valid, ignored, invalid)
        self.orders[player] = orders
        return valid, \
               ['The order: \'%s\' was ignored # %s' % ignore for ignore in ignored], \
               ['The order: \'%s\' is invalid # %s' % error for error in invalid]

    def get_scores(self, player=None):
        """ Gets the scores of all players

            Used by engine for ranking
        """
        if player is None:
            return self.score
        else:
            return self.order_for_player(player, self.score)

    def order_for_player(self, player, data):
        """ Orders a list of items for a players perspective of player #

            Used by engine for ending bot states
        """
        s = self.switch[player]
        return [None if i not in s else data[s.index(i)]
                for i in range(max(len(data),self.num_players))]

    def get_stats(self):
        """ Get current stats

            Used by engine to report stats
        """
        # in new version it is: <pirateCount> <treasureCount> <Ranking/leading> <scores>
        pirate_count = [0] * self.num_players
        for pirate in self.current_pirates.values():
            pirate_count[pirate.owner] += 1

        stats = {}
        stats['pirates'] = pirate_count
        stats['score'] = self.score
        return stats

    def get_replay(self):
        """ Return a summary of the entire game

            Used by the engine to create a replay file which may be used
            to replay the game.
        """
        replay = {}
        # required params
        replay['revision'] = 3
        replay['players'] = self.num_players

        # optional params
        replay['loadtime'] = self.loadtime
        replay['turntime'] = self.turntime
        replay['turns'] = self.max_turns
        replay['viewradius2'] = self.viewradius
        replay['attackradius2'] = self.attack_radius
        replay['maxpoints'] = self.maxpoints
        replay['actions_per_turn'] = self.actions_per_turn
        replay['reload_turns'] = self.reload_turns
        replay['defense_reload_turns'] = self.defense_reload_turns
        replay['defense_expiration_turns'] = self.defense_expiration_turns
        replay['treasure_spawn_turns'] = self.treasure_spawn_turns
        replay['engine_seed'] = self.engine_seed
        replay['player_seed'] = self.player_seed
        replay['lighthouses'] = list(self.lighthouses)

        # map
        replay['map'] = {}
        replay['map']['rows'] = self.height
        replay['map']['cols'] = self.width
        replay['map']['data'] = self.get_map_output(replay=True)

        # food - deprecated
        replay['food'] = []

        # pirates
        replay['ants'] = []
        for pirate in self.all_pirates:
            pirate_data = [pirate.initial_loc[0], pirate.initial_loc[1], pirate.spawn_turn] #3
            if not pirate.die_turn:
                pirate_data.append(self.turn + 1) #4
            else:
                pirate_data.append(pirate.die_turn) #4

            pirate_data.append(pirate.owner) #5
            pirate_data.append(pirate.orders) #6
            pirate_data.append(pirate.supporters) #7
            pirate_data.append(pirate.id) #8
            pirate_data.append(pirate.reason_of_death) #9
            pirate_data.append(''.join(['1' if i else '0' for i in pirate.treasure_history])) #10
            pirate_data.append(pirate.attack_turns) #11
            pirate_data.append(pirate.defense_turns) #12
            pirate_data.append(''.join(['1' if i else '0' for i in pirate.drink_history])) #13
            pirate_data.append(pirate.attack_radius)

            replay['ants'].append(pirate_data)

        replay['hills'] = []
        replay['forts'] = []
        replay['treasures'] = []

        for treasure in self.treasures:
            replay['treasures'].append([treasure.id, treasure.initial_loc, ''.join(['1' if i else '0' for i in treasure.is_available_history])])

        replay['zones'] = self.zones.values()
        replay['rejected'] = self.rejected_moves

        # scores
        replay['scores'] = self.score_history
        replay['bonus'] = [0]*self.num_players
        replay['hive_history'] = self.hive_history
        replay['winning_turn'] = self.winning_turn
        replay['ranking_turn'] = self.ranking_turn
        replay['cutoff'] = self.cutoff
        return replay


    def calc_game_excitement(self):
        ''' This function is called at the end of a game to calculate a numerical value
            describing how exciting a games was
        '''
        final_scores = self.score.values()
        # sort least to most
        final_scores.sort()
        least_diff = 0
        if max(final_scores) > 100:
            # get the difference between two leading scores
            least_diff = abs(final_scores[-1] - final_scores[-2])


    def get_game_statistics(self):
        ''' This will return interesting statistics and info about the game '''
        return

    def get_map_format(self):
        ''' Returns the map-file equivalent in order to allow pausing of games and continuing from same point '''
        return

    def print_zone(self):
        for i,row in enumerate(self.map):
            row = ''
            for j,col in enumerate(self.map[i]):
                if (i,j) in self.current_pirates:
                    row += '0'
                elif (i,j) in self.zones[1]:
                    row += '-'
                elif (i,j) in self.zones[0]:
                    row += '|'
                else:
                    row += 'x'
            print(row)


class Item:
    def __init__(self, id, loc, start_turn, end_turn, active_turns):
        self.id = id
        self.location = loc
        self.start_turn = start_turn
        self.end_turn = end_turn
        self.active_turns = active_turns

class AttackItem(Item):
    def __init__(self, id, loc, start_turn, end_turn, activate_turn, attack_radius):
        Item.__init__(self, id, loc, start_turn, end_turn, activate_turn)
        self.attack_radius = attack_radius

class DefenseItem(Item):
    def __init__(self, id, loc, start_turn, end_turn, activate_turn, defense_expiration_turns):
        Item.__init__(self, id, loc, start_turn, end_turn, activate_turn)
        self.defense_expiration_turns = defense_expiration_turns

class ActionItem(Item):
    def __init__(self, id, loc, start_turn, end_turn, activate_turn, actions_per_turn):
        Item.__init__(self, id, loc, start_turn, end_turn, activate_turn)
        self.actions_per_turn = actions_per_turn

class Treasure:
    # Treasure class
    def __init__(self, id, loc, pirate=None):
        self.id = id
        self.initial_loc = loc
        self.pirate = pirate
        self.loc_history = []
        self.loc_history.append(loc)
        self.done = False
        self.done_turn = MAX_RAND

        # true if doesnt belong to any pirate
        self.is_available = True
        self.is_available_history = []

        self.spawn_turns = -1

    def __str__(self):
        return '(%s, %s)' % (self.initial_loc)


class UnloadArea:
    def __init__(self, loc, owner):
        self.loc = loc
        self.owner = owner


class Pirate:
    def __init__(self, loc, owner, id, attack_radius, defense_expiration_turns, spawn_turn=None):
        self.loc = loc
        self.owner = owner
        self.id = id

        self.initial_loc = loc
        self.spawn_turn = spawn_turn

        self.attack_turns = [-1000, -1]
        self.die_turn = None
        self.drink_turns = [-1000]
        self.is_cloaked = False
        self.orders = []
        # this is for support mode and logs how much support an pirate had per turn
        self.supporters = []
        self.reason_of_death = ''
        # reload counter
        self.reload_turns = 0
        # defense reload counter
        self.defense_reload_turns = 0
        # sober counter
        self.sober_turns = 0
        # indicator for pirate carrying a treasure
        self.treasure = None
        # list of booleans, indicates if the pirate is carrying a treasure this turn
        self.treasure_history = []
        self.drink_history = []
        # is the pirate defending this turn

        # self.is_defensive = False
        self.defense_turns = [-1000]

        # defense item
        self.defense_expiration_turns = defense_expiration_turns
        self.defense_expiration_turns_counter = 0
        self.defense_item_active_turns = 0

        #attack item
        self.attack_radius = attack_radius
        self.attack_item_active_turns = 0


    def __str__(self):
        return '(%s, %s, %s, %s, %s, %s, %s)' % (self.loc, self.owner, self.id, self.spawn_turn, self.die_turn, self.sober_turns, ''.join(self.orders))

