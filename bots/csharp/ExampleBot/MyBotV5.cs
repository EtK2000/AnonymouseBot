﻿using System;
using System.Collections.Generic;
using System.Linq;
using Pirates;

namespace ExampleBotV5
{
	// this class manages the queued action for the given Pirate
	internal class PirateContainer
	{
		public enum State
		{
			attacked, defended, moved, treasure, none
		}

		// TODO: make these read-only?
		public static List<PirateContainer> free;// list of all Pirates that didn't do anything
		public static List<PirateContainer> kamikazes;
		public static List<PirateContainer> withTreasure;// list of all Pirates that have Treasure

		private static int remainingMoves;// number of remaining allowed moves

		public static void init(IPirateGame game)
		{
			free = new List<PirateContainer>();
			kamikazes = new List<PirateContainer>();
			withTreasure = new List<PirateContainer>();
			remainingMoves = game.GetActionsPerTurn();
		}

		public static int GetRemainingMoves()
		{
			return remainingMoves;
		}

		// instance variables
		private Pirate p;
		private State s = State.none;
		private bool k;
		private bool a;

		public Pirate P
		{
			get { return p; }
		}

		public State S
		{
			get { return s; }
		}

		public bool K
		{
			get { return k; }
		}

		public bool AVALIBLE
		{
			get { return a; }
		}

		public PirateContainer(Pirate p, bool kamikaze)
		{
			this.p = p;
			a = (p.TurnsToSober == 0 && p.TurnsToRevive == 0);
			if (k = kamikaze)
				kamikazes.Add(this);
			free.Add(this);
			if (p.HasTreasure)
			{
				withTreasure.Add(this);
				s = State.treasure;
			}
		}

		public bool attack(Pirate p, IPirateGame game)
		{
			if (s != State.none)
			{
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to attacked!");
				return false;
			}
			if (P.ReloadTurns > 0)
			{
				game.Debug("Pirate " + P.Id + " cannot attack, no ammo!");
				return false;
			}

			free.Remove(this);
			s = State.attacked;
			game.Attack(P, p);
			return true;
		}

		public bool defend(IPirateGame game)
		{
			if (s != State.none && s != State.treasure)
			{
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to defended!");
				return false;
			}
			if (P.ReloadTurns > 0)
			{
				game.Debug("Pirate " + P.Id + " cannot defend, no ammo!");
				return false;
			}

			game.Debug("def");
			free.Remove(this);
			s = State.defended;
			game.Defend(P);
			return true;
		}

		public bool move(Location l, IPirateGame game)
		{
			if (s != State.none && s != State.treasure)
			{
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to moved!");
				return false;
			}
			int d = game.Distance(P, l);
			if (d > remainingMoves || (P.HasTreasure && d > 1))
			{
				game.Debug("Pirate " + P.Id + " cannot move, not enough moves!");
				return false;
			}

			free.Remove(this);
			s = State.moved;
			game.SetSail(P, l);
			new QueuedMotion(P, l);
			return true;
		}
		public bool move1(Location l, IPirateGame game)
		{
			if (s != State.none && s != State.treasure && s != State.moved)
			{
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to moved!");
				return false;
			}
			int d = game.Distance(P, l);
			if (d > remainingMoves || (P.HasTreasure && d > 1))
			{
				game.Debug("Pirate " + P.Id + " cannot move, not enough moves!");
				return false;
			}

			//  free.Remove(this);
			s = State.moved;
			/*game.SetSail(P, l);
			new QueuedMotion(P, l);*/
			return true;
		}
	}

	// this class allows for assumption of this turns attacks
	// and allows for attacks based off of them
	internal class QueuedAttack
	{
		private static Dictionary<Pirate, List<PirateContainer>> targetMap;
		private static List<QueuedAttack> queued;
		private static List<Pirate> shot;

		public static void init()
		{
			targetMap = new Dictionary<Pirate, List<PirateContainer>>();
			queued = new List<QueuedAttack>();
			shot = null;
		}

		public static bool containsEnemy(Pirate e)
		{
			foreach (QueuedAttack qa in queued)
			{
				if (qa.E.Contains(e))
					return true;
			}
			return false;
		}

		public static bool contains(Pirate p)
		{
			foreach (QueuedAttack qa in queued)
			{
				if (qa.P.Equals(p))
					return true;
			}
			return false;
		}

		public static void doAttacks(IPirateGame game, bool deadMap)
		{
			if (shot != null)
			{
				game.Debug("can only call doAttacks(IPirateGame) once per turn!");
				return;
			}
			shot = new List<Pirate>();

			foreach (Pirate p in targetMap.Keys)
			{
				foreach (PirateContainer c in targetMap[p])
					game.Debug(p.Id + " --> " + c.P.Id);
			}

			for (int i = 1; i <= game.AllMyPirates().Count && targetMap.Keys.Count > 0; i++)
			{
				List<Pirate> removed = new List<Pirate>();
				foreach (Pirate p in targetMap.Keys)
				{
					if (targetMap[p].Count == i && p.DefenseExpirationTurns == 0 && (!deadMap || p.HasTreasure))
					{
						PirateContainer pc = targetMap[p][0];
						pc.attack(p, game);
						removed.Add(p);
						foreach (Pirate k in targetMap.Keys)
							targetMap[k].Remove(pc);
					}
				}
				foreach (Pirate p in removed)
					targetMap.Remove(p);
			}

			/*for (int i = 0; i < queued.Count; i++)
			{
				if (queued[i].E.Count == 1 && !shot.Contains(queued[i].E[0]))
				{
					queued[i].P.attack(queued[i].E[0], game);// TODO: if kamikaze, and my target, ignore it
					shot.Add(queued[i].E[0]);
				}
			}*/
		}

		private PirateContainer P;
		private List<Pirate> E;

		public QueuedAttack(PirateContainer p, List<Pirate> e)
		{
			P = p;
			E = e;
			queued.Add(this);

			foreach (Pirate t in e)
			{
				if (!targetMap.ContainsKey(t))
					targetMap.Add(t, new List<PirateContainer>());
				targetMap[t].Add(p);
			}
		}
	}

	// this class allows for assumption of this turns moves
	// and allows for motion based off of them
	internal class QueuedMotion
	{
		private static List<QueuedMotion> queued;

		public static void init()
		{
			queued = new List<QueuedMotion>();
		}

		public static bool contains(Location l)
		{
			foreach (QueuedMotion qm in queued)
			{
				if (qm.L.Equals(l))
					return true;
			}
			return false;
		}

		public static bool contains(Pirate p)
		{
			foreach (QueuedMotion qm in queued)
			{
				if (qm.P.Equals(p))
					return true;
			}
			return false;
		}

		public static bool isOccupied(Location l, IPirateGame game, bool dontHitEnemies)
		{
			if (contains(l))
				return true;
			else
			{
				foreach (Pirate p in game.AllMyPirates())
				{
					if (!contains(p) && p.TurnsToRevive == 0 && l.Equals(p.Location))
						return true;
				}
				if (dontHitEnemies)// ASSUMING THEY DONT MOVE!!!
				{
					foreach (Pirate p in game.AllEnemyPirates())
					{
						if (p.TurnsToRevive == 0 && l.Equals(p.Location))
							return true;
					}
				}
			}
			return false;
		}

		private Pirate P;
		private Location L;

		public QueuedMotion(Pirate p, Location l)
		{
			P = p;
			L = l;
			queued.Add(this);
		}
	}

	// this is the actual AI
	public class MyBotV5 : IPirateBot
	{
		private static bool panic, nowhere;// TODO: make nowhere mode do something...

		private static bool deadMap = false;

		private static Random rand;

		// this is the actual turn
		public void DoTurn(IPirateGame game)
		{
			if (game.GetTurn() == 1)
			{
				rand = new Random(42934837); //,,79409223
				if (game.AllEnemyPirates().Count > game.AllMyPirates().Count)
					rand = new Random(31400000);
				Location l = new Location(1, 1);
				if (game.Treasures().Count == 1 && game.AllMyPirates().Count == game.AllEnemyPirates().Count)
					deadMap = true;

				game.Debug((deadMap ? "DEADMAP!!!" : "NIE!"));
				if (deadMap)
					return;// this stops some fighting over treasures
			}

			nowhere = (game.GetEnemyScore() == game.GetMyScore() && game.GetTurn() == (game.GetMaxTurns() / 3));
			if (nowhere)
				game.Debug("ACTIVATING NOWHERE MODE!!!");

			panic = (((game.Treasures().Count == 0 || game.GetEnemyScore() >= (game.GetMaxPoints() - 2))) && game.EnemyPiratesWithTreasures().Count > 0);
			if (panic)
				game.Debug("ACTIVATING PANIC MODE!!!");

			PirateContainer.init(game);
			QueuedAttack.init();
			QueuedMotion.init();
			int remaining = game.GetActionsPerTurn();
			int ships = game.AllMyPirates().Count;

			try
			{
				// calculate the closest treasure to ps[i]
				PirateContainer[] ps = new PirateContainer[ships];
				int[] ds = new int[ships];
				Treasure[] ts = new Treasure[ships];
				for (int i = 0; i < ships; i++)
				{
					ps[i] = new PirateContainer(game.GetMyPirate(i), (i % 2) == 1);
					ds[i] = int.MaxValue;
					foreach (Treasure t in game.Treasures())
					{
						if (game.Distance(ps[i].P, t) < ds[i])
						{
							ds[i] = game.Distance(ps[i].P, t);
							ts[i] = t;
						}
					}
				}

				// control the kamikazes
				calcKamikazes(ps, ref ts, ref ds, game);

				// move Pirates that have treasures towards the base
				{
					List<PirateContainer> ltp = PirateContainer.withTreasure;
					foreach (PirateContainer p in ltp)
					{
						List<Pirate> es = findEnemiesFor(p.P, game);
						if (es.Count > 0 && p.P.ReloadTurns == 0)
							p.defend(game);
						else
							remaining -= move(p, p.P.InitialLocation, 1, game, true);
					}
				}

				// search and destroy, TODO: prioritise this!!!
				Pirate k = null, tar = null;
				if (panic)
				{
					int mx = (game.GetRows() + game.GetCols() - game.GetAttackRadius()) / game.GetActionsPerTurn();// turns it takes to get from a corner to its opposing corner
					int d = int.MaxValue;
					tar = game.EnemyPiratesWithTreasures()[0];// TODO: focus on closest to enemy base
					// find closest Pirate
					foreach (PirateContainer p in PirateContainer.free)// notice all pirates with Treasure already moved, see: ltp
					{
						game.Debug("panic->k testing for " + p.P.Id + " | " + d);
						game.Debug(p.AVALIBLE + " && " + (p.P.ReloadTurns < mx) + " && " + (d > game.Distance(p.P, tar)));
						if (p.AVALIBLE && p.P.ReloadTurns < mx && d > game.Distance(p.P, tar))
						{
							d = game.Distance(p.P, tar);
							k = p.P;
							game.Debug("panic->k = " + p.P.Id + " | " + d);
						}
					}
					if (k == null)// no Pirate with ammo, so choose the closest to the InitialLocation then move to there
					{
						foreach (PirateContainer p in PirateContainer.free)// notice all pirates with Treasure already moved, see: ltp
						{
							if (p.AVALIBLE && d > game.Distance(p.P, tar.InitialLocation))// TODO: make the "6" generic to board size
							{
								d = game.Distance(p.P, tar.InitialLocation);
								k = p.P;
							}
						}
					}
				}

				List<int> dss = sortInto(ds);// sort the ds into the dss

				// AAAAATTTTTTTTTTTTAAAAAAAAAAACCCCCCCCKKKKKKKKKKKKKKK!!!!!!!!!!!!!!!! (or defend...)
				for (int i = PirateContainer.free.Count; i > 0; )
				{
					PirateContainer p = PirateContainer.free[--i];
					if (p.P.ReloadTurns == 0 && !p.P.HasTreasure && p.AVALIBLE)
					{
						List<Pirate> es = findTargetsFor(p.P, game);
						if (es.Count > 0)
							new QueuedAttack(p, es);
					}
				}
				QueuedAttack.doAttacks(game, deadMap);

				// move
				for (int j = 0; j < ships; j++)
				{
					int i = dss[j];
					if (ps[i].S == PirateContainer.State.none && ps[i].AVALIBLE && !ps[i].P.HasTreasure)
					{
						if (game.Treasures().Count > 0)// use typical motion
						{
							int mv = move(ps[i], ts[i].Location, remaining, game);
							if (mv > 0)
							{
								remaining -= mv;
								continue;
							}
						}
						if (game.EnemyPiratesWithTreasures().Count > 0 && ps[i].P == k)// activate search and destroy
							remaining -= move(ps[i], tar.Location, remaining, game);
					}
				}
			}
			catch (Exception e)
			{
				game.Debug("Crashed!");
				game.Debug(e.Message);
				game.Debug(e.StackTrace);
			}
			game.Debug("turn " + game.GetTurn() + ": ran " + (game.GetActionsPerTurn() - remaining) + " motions");
		}

		// calculations
		private void calcKamikazes(PirateContainer[] ps, ref Treasure[] ts, ref int[] ds, IPirateGame game)
		{
			// find the enemies pirate the closest to their base
			List<Pirate> es = new List<Pirate>();
			int[] eds = new int[game.EnemyPiratesWithTreasures().Count];
			foreach (Pirate e in game.EnemyPiratesWithTreasures())
			{
				eds[es.Count] = game.Distance(e, e.InitialLocation);
				es.Add(e);
			}
			List<int> edss = sortInto(eds);

			// map the kamikazes to the ps[]
			List<PirateContainer> unusedKamikazes = new List<PirateContainer>(PirateContainer.kamikazes);
			List<int> kamikazeDefinitions = new List<int>();
			for (int i = unusedKamikazes.Count; i > 0; )
			{
				if (unusedKamikazes[--i].P.HasTreasure)
					unusedKamikazes.RemoveAt(i);// if a kamikaze has treasure, deactivate it
				else
				{
					for (int j = 0; j < ps.Length; j++)
					{
						if (unusedKamikazes[i].Equals(ps[j]))
						{
							kamikazeDefinitions.Add(j);
							break;
						}
					}
				}
			}

			for (int i = 0; i < es.Count && unusedKamikazes.Count > 0; i++)
			{
				// find closest kamikaze to the closest enemy to its initial position
				List<int> kds = new List<int>();
				foreach (PirateContainer p in unusedKamikazes)
					kds.Add(game.Distance(p.P, es[edss[i]]));

				int min = -1;
				for (int j = 0; j < kds.Count; j++)
				{
					if (min == -1 || kds[j] <= kds[min])
						min = j;
				}

				// set the target
				int index = kamikazeDefinitions[min];
				ts[index] = new Treasure(100 + i, es[i].InitialLocation, 0);
				ds[index] = (int)Math.Ceiling(game.Distance(unusedKamikazes[min].P, es[i].InitialLocation) / 2f);
				if (ds[index] == 0)
					ds[index] = int.MaxValue;// speed up some things if the Pirate shouldn't move
				unusedKamikazes.RemoveAt(min);
				kamikazeDefinitions.RemoveAt(min);
			}
		}

		// returns an array of the indencies in ascending order
		private List<int> sortInto(int[] os)
		{
			List<int> res = new List<int>();
			bool add;
			do
			{
				add = false;
				int min = -1;
				for (int i = 0; i < os.Length; i++)
				{
					if (!res.Contains(i))
					{
						if (min == -1 || os[i] <= os[min])
						{
							min = i;
							add = true;
						}
					}
				}
				if (add)
					res.Add(min);
			} while (add);
			return res;
		}

		// returns a prioritised queue of targets for the given Pirate
		private List<Pirate> findTargetsFor(Pirate p, IPirateGame game)
		{
			// TODO: if kamikaze and in enemy InitialLocation, don't shoot the Pirate with the id for the location
			List<Pirate> res = new List<Pirate>();
			foreach (Pirate e in game.EnemySoberPirates())
			{
				if (game.InRange(p, e) && e.DefenseExpirationTurns == 0)
				{
					if (!panic && e.ReloadTurns == 0)// if we aren't in panic, and we have no target, shoot the one that has ammo; TODO: alter ReloadTurns value
						res.Add(e);
					else if (e.HasTreasure)// always prioritise Pirates with Treasures
						res.Insert(0, e);
				}
			}
			return res;
		}

		// returns a list of all the enemies who can shoot the given Pirate
		private List<Pirate> findEnemiesFor(Pirate p, IPirateGame game)
		{
			List<Pirate> res = new List<Pirate>();
			foreach (Pirate e in game.EnemySoberPirates())
			{
				if (e.ReloadTurns == 0 && game.InRange(e, p))
					res.Add(e);
			}
			return res;
		}

		// can we move the given Pirate to the given Location according to the number of moves?
		// if so --> move it!
		private static int move(PirateContainer p, Location t, int moves, IPirateGame game, bool dontHitEnemies = false)
		{
			if (moves == 0 || !p.AVALIBLE || p.P.Location.Equals(t))
				return 0;

			// calculate the best route
			/*foreach (Location l in game.GetSailOptions(p.P, t, moves))
			{
				if (!QueuedMotion.isOccupied(l, game, dontHitEnemies) && p.move(l, game))
					return game.Distance(p.P, l);
			}*/
			var X = from l in game.GetSailOptions(p.P, t, moves)
					where (!QueuedMotion.isOccupied(l, game, dontHitEnemies) && p.move1(l, game))
					select l;

			if (X.Count() > 0)
			{
				PirateContainer.free.Remove(p);

				Location loc = X.ElementAt(rand.Next(X.Count()));

				game.SetSail(p.P, loc);
				new QueuedMotion(p.P, loc);
				return game.Distance(p.P, loc);
			}

			else if (X.Count() == 0)
			{
				if (PirateContainer.GetRemainingMoves() > 2)
				{
					Location loc = p.P.Location;
					p.move(loc, game);
				}
			}

			game.Debug("Failed to find a move for " + p.P.Id + " to " + t);
			return 0;
		}
	}
}