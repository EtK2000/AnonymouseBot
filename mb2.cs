using System;
using System.Collections.Generic;
using Pirates;

namespace ExampleBot
{
	public class MyBot : IPirateBot
	{
		private static bool kamikaze = false;
		private static Treasure[] ts = new Treasure[4];

		public void DoTurn(IPirateGame game)
		{
			try
			{
				int remaining = 6;

				Pirate[] ps = new Pirate[4];
				int[] ds = new int[4];
				List<int> dss = new List<int>();// should always be size 4
				for (int i = 0; i < 4; i++)
				{
					ps[i] = game.GetMyPirate(i);
					ds[i] = int.MaxValue;
					if (game.Treasures().Contains(ts[i]))
						continue;
					foreach (Treasure t in game.Treasures())
					{
						if (game.Distance(ps[i], t) < ds[i])
						{
							ds[i] = game.Distance(ps[i], t);
							ts[i] = t;
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


				if (kamikaze)
				{
					if (ps[0].InitialLocation.Equals(new Location(23, 1)))
					{
						ts[0] = new Treasure(19, new Location(24, 30));
						ts[1] = new Treasure(20, new Location(25, 29));
					}
					else
					{
						ts[0] = new Treasure(19, new Location(24, 2));
						ts[1] = new Treasure(20, new Location(25, 3));
					}
					ds[0] = 0;
					ds[1] = 0;
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
					if (!ps[i].HasTreasure)
					{
						bool attacked = false;
						if (ps[i].ReloadTurns == 0)
						{
							Pirate t = null;
							foreach (Pirate e in game.EnemySoberPirates())
							{
								if (game.InRange(ps[i], e))
								{
									if (e.ReloadTurns == 0)
									{
										t = e;
										break;
									}
									else if (e.HasTreasure)
									{
										if (t == null || t.HasTreasure || t.ReloadTurns > 0)
											t = e;
									}
									//else if (e.ReloadTurns > 0 && !e.HasTreasure)
										//continue;
								}
							}
							if (t != null)
							{
								game.Attack(ps[i], t);
								attacked = true;
							}
						}
						if (!attacked && ps[i].TurnsToSober == 0 && ps[i].TurnsToRevive == 0)
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
		}

		private static bool move(Pirate p, Location t, int moves, IPirateGame game)
		{
			foreach (Location l in game.GetSailOptions(p, t, moves))
			{
				if (!game.IsOccupied(l))
				{
					game.SetSail(p, l);
					return true;
				}
			}
			game.Debug("Failed to find a move for " + p.Id);
			return false;
		}
	}
}