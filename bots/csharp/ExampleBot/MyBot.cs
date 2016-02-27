//g1 - Anonymous --> V3b
using System;
using System.Collections.Generic;
using Pirates;

namespace ExampleBot
{
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

		public static bool isOccupied(Location l, IPirateGame game)
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
			}
			return false;
		}

		private Pirate p;
		private Location l;

		public Pirate P
		{
			get { return p; }
		}

		public Location L
		{
			get { return l; }
		}

		public QueuedMotion(Pirate p, Location l)
		{
			this.p = p;
			this.l = l;
			queued.Add(this);
		}
	}

	// this is the actual AI
	public class MyBot : IPirateBot
	{
		// should we kamikaze?
		private static bool kamikaze = true;
		// index the targets for each of our Pirates
		private static Treasure[] ts = new Treasure[4];

		// this is the actual turn
		public void DoTurn(IPirateGame game)
		{
			QueuedMotion.init();
			int remaining = game.GetActionsPerTurn();
			try
			{

				Pirate[] ps = new Pirate[4];
				int[] ds = new int[4];
				List<int> dss = new List<int>();// should always be size 4
				for (int i = 0; i < 4; i++)
				{
					ps[i] = game.GetMyPirate(i);
					ds[i] = int.MaxValue;
					if (game.Treasures().Contains(ts[i]))
					{
						ds[i] = game.Distance(ps[i], ts[i]);
						continue;
					}
					foreach (Treasure t in game.Treasures())
					{
						if (game.Distance(ps[i], t) < ds[i])
						{
							ds[i] = game.Distance(ps[i], t);
							ts[i] = t;
						}
					}
				}

				if (kamikaze || game.Treasures().Count == 0)
				{
					if (ps[0].InitialLocation.Equals(new Location(23, 1)))
					{
						if (!ps[0].HasTreasure)
						{
							ts[0] = new Treasure(19, new Location(24, 30));
							//ds[0] = 0;
							foreach (Pirate t in game.EnemyPirates())
							{
								if (t.Id == 2)
								{
									if (!t.IsLost)
										ts[0] = new Treasure(19, new Location(t.Location));
									break;
								}
							}
							ds[0] = game.Distance(ps[0], ts[0]) * 2;
						}
						if (!ps[1].HasTreasure)
						{
							ts[1] = new Treasure(20, new Location(25, 29));
							ds[1] = 0;
						}
						if (game.Treasures().Count == 0)
						{
							if (!ps[2].HasTreasure)
							{
								ts[2] = new Treasure(21, new Location(23, 31));
								ds[2] = 0;
							}
							if (!ps[3].HasTreasure)
							{
								ts[3] = new Treasure(22, new Location(26, 28));
								ds[3] = 0;
							}
						}
					}
					else
					{
						if (!ps[0].HasTreasure)
						{
							ts[0] = new Treasure(19, new Location(24, 2));
							//ds[0] = 0;
							foreach (Pirate t in game.EnemyPirates())
							{
								if (t.Id == 2)
								{
									if (!t.IsLost)
										ts[0] = new Treasure(19, new Location(t.Location));
									break;
								}
							}
							ds[0] = game.Distance(ps[0], ts[0]) * 2;
						}
						if (!ps[1].HasTreasure)
						{
							ts[1] = new Treasure(20, new Location(25, 3));
							ds[1] = 0;
						}
						if (game.Treasures().Count == 0)
						{
							if (!ps[2].HasTreasure)
							{
								ts[2] = new Treasure(21, new Location(23, 1));
								ds[2] = 0;
							}
							if (!ps[3].HasTreasure)
							{
								ts[3] = new Treasure(22, new Location(26, 4));
								ds[3] = 0;
							}
						}
					}
				}

				// sort the ds into the dss
				{
					bool add;
					do
					{
						add = false;
						int min = -1;
						for (int i = 0; i < ds.Length; i++)
						{
							if (!dss.Contains(i))
							{
								if (min == -1 || ds[i] <= ds[min])
								{
									min = i;
									add = true;
								}
							}
						}
						if (add)
							dss.Add(min);
					} while (add);
				}

				List<Pirate> ltp = game.MyPiratesWithTreasures();
				remaining -= ltp.Count;
				foreach (Pirate p in ltp)
					move(p, p.InitialLocation, 1, game);

				Pirate k = null, tar = null;
				if (game.Treasures().Count == 0 && game.EnemyPiratesWithTreasures().Count > 0)
				{
					int d = int.MaxValue;
					tar = game.EnemyPiratesWithTreasures()[0];
					foreach (Pirate p in game.MyPiratesWithoutTreasures())
					{
						if (p.TurnsToSober == 0 && p.ReloadTurns < 6 && d > game.Distance(p, tar))
						{
							d = game.Distance(p, tar);
							k = p;
						}
					}
				}


				for (int j = 0; j < 4; j++)
				{
					int i = dss[j];
					if (ps[i].TurnsToSober == 0 && ps[i].TurnsToRevive == 0 && !ps[i].HasTreasure)
					{
						bool attacked = false;
						{
							Pirate t = null;
							foreach (Pirate e in game.EnemySoberPirates())
							{
								if (game.InRange(ps[i], e))
								{
									if (e.ReloadTurns == 0 && (!kamikaze || t == null))
									{
										t = e;
										break;
									}
									else if (e.HasTreasure)
									{
										if (t == null || t.HasTreasure || t.ReloadTurns > 0)
											t = e;
									}
								}
							}
							if (t != null)
							{
								if (!t.HasTreasure && ps[i].DefenseExpirationTurns == 0)
									game.Defend(ps[i]);
								else if (ps[i].ReloadTurns == 0)
									game.Attack(ps[i], t);
								attacked = true;
							}
						}
						if (!attacked)
						{
							if ((game.Treasures().Count > 0 && move(ps[i], ts[i].Location, remaining, game) || (game.EnemyPiratesWithTreasures().Count > 0 && ps[i] == k && move(ps[i], tar.Location, remaining, game))))
								remaining = 0;
						}
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

		// can we move the given Pirate to the given Location according to the number of moves?
		// if so --> move it!
		private static bool move(Pirate p, Location t, int moves, IPirateGame game)
		{
			if (moves == 0)
				return true;
			foreach (Location l in game.GetSailOptions(p, t, moves))
			{
				if (!QueuedMotion.isOccupied(l, game) || (game.IsOccupied(l) && game.GetPirateOn(l).Owner != p.Owner))
				{
					game.SetSail(p, l);
					new QueuedMotion(p, l);
					return true;
				}
			}
			game.Debug("Failed to find a move for " + p.Id + " to " + t);
			return false;
		}
	}
}