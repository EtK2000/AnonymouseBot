using System.Collections.Generic;
using Pirates;

namespace ExampleBotV2
{
	public class MyBotV2 : IPirateBot
	{
		public void DoTurn(IPirateGame game)
		{
			int remaining = 6;

			Pirate[] ps = new Pirate[4];
			Location[] l = new Location[4];
			int[] ds = new int[4];
			List<int> dss = new List<int>();// should always be size 4
			for (int i = 0; i < 4; i++)
			{
				ps[i] = game.GetMyPirate(i);
				ds[i] = int.MaxValue;
				foreach (Treasure t in game.Treasures())
				{
					if (game.Distance(ps[i], t) < ds[i])
					{
						ds[i] = game.Distance(ps[i], t);
						l[i] = t.Location;
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
				game.SetSail(p, game.GetSailOptions(p, p.InitialLocation, 1)[0]);

			if (game.Treasures().Count == 0)
				return;
			for (int j = 0; j < 4; j++)
			{
				int i = dss[j];
				if (!ps[i].HasTreasure)
				{
					bool attacked = false;
					if (ps[i].ReloadTurns == 0)
					{
						foreach (Pirate e in game.EnemySoberPirates())
						{
							if (game.InRange(ps[i], e))
							{
								game.Attack(ps[i], e);
								attacked = true;
								break;
							}
						}
					}

					if (!attacked && ps[i].TurnsToSober == 0 && ps[i].TurnsToRevive == 0)
					{
						game.SetSail(ps[i], game.GetSailOptions(ps[i], /*cull*/l[i], remaining)[0]);
						remaining = 0;
					}
				}
			}
		}
	}
}