using System;
using System.Collections.Generic;
using System.Linq;
using Pirates;

namespace ExampleBot
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

		public static void init(IPirateGame game)
		{
			free = new List<PirateContainer>();
			kamikazes = new List<PirateContainer>();
			withTreasure = new List<PirateContainer>();
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
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to attacked");
				return false;
			}
			if (P.ReloadTurns > 0)
			{
				game.Debug("Pirate " + P.Id + " cannot attack, no ammo");
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
			if (P.DefenseReloadTurns > 0)
			{
				game.Debug("Pirate " + P.Id + " cannot defend, no ammo!");
				return false;
			}

			game.Debug("Pirate " + P.Id + " defended");
			free.Remove(this);
			s = State.defended;
			game.Defend(P);
			return true;
		}

		public bool move(Location l, IPirateGame game, int remaining)
		{
			if (s != State.none && s != State.treasure)
			{
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to moved!");
				return false;
			}
			int d = game.Distance(P, l);
			if (d > remaining || (P.HasTreasure && d > P.CarryTreasureSpeed))
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

		public bool move1(Location l, IPirateGame game, int remaining)
		{
			if (s != State.none && s != State.treasure && s != State.moved)
			{
				game.Debug("State on Pirate " + P.Id + " cannot shift from " + s.ToString() + " to moved!");
				return false;
			}
			int d = game.Distance(P, l);
			if (d > remaining || (P.HasTreasure && d > P.CarryTreasureSpeed))
			{
				game.Debug("Pirate " + P.Id + " cannot move, not enough moves!");
				return false;
			}

			s = State.moved;
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
					game.Debug("Pirate " + p.Id + " attacked enemy pirate " + c.P.Id);
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

	// string b64 to char[] --> Convert.FromBase64String(s);

	// this is the actual AI
	public class MyBot : IPirateBot
	{
		static int WastedTurnsCounter = 0;
		private static bool panic, nowhere;// TODO: make nowhere mode do something...
		private static bool deadMap = false;
		private static Random rand;

		// this is the actual turn
		public void DoTurn(IPirateGame game)
		{
			int remaining = game.GetActionsPerTurn();
			if (game.GetTurn() == 1)
				game.Debug("moves per turn: " + remaining);

			try
			{
				#region init
				if (game.GetTurn() == 1)
				{
					chooseMap(game);
				}
				nowhere = inNowhereMode(game);
				if (nowhere)
					game.Debug("ACTIVATING NOWHERE MODE");
				panic = inPanicMode(game);
				if (panic)
					game.Debug("ACTIVATING PANIC MODE");

				PirateContainer.init(game);
				QueuedAttack.init();
				QueuedMotion.init();
				int ships = game.AllMyPirates().Count;
				PirateContainer[] ps = new PirateContainer[ships];
				int[] ds = new int[ships];
				Treasure[] ts = new Treasure[ships];
				#endregion

				calcBestTreasure(game, ships, ref ps, ref ds, ref ts); // calculate the closest treasure to ps[i]
				BringBackTreasure(game, ref remaining); // move Pirates that have treasures towards the base
				calcKamikazes(ps, ref ts, ref ds, game); // control the kamikazes

				//BringBackTreasure(game, ref remaining); // move Pirates that have treasures towards the base
				//Powerup pt = new SpeedPowerup(-1, new Location(0, 0), 0, 0, 0);

				#region power up calculations
				Powerup[] pu = (from l in game.Powerups()
								where l.Type == "Speed"
								select l).ToArray();

				if (pu.Count() > 0)
				{
					Powerup[] puu = new Powerup[ships];
					for (int i = pu.Count() - 1; i >= 0; i--)
						puu[i] = pu[i];
					game.Debug("A speed powerup was found");
					int[] dis = new int[ships];



					locatePowerups(game, ships, ref ps, ref dis, ref puu);

					int chosen = -1;
					for (int i = 0, min = int.MaxValue; i < ships; ++i)
					{
						if (dis[i] < min && ps[i].AVALIBLE && dis[i] != 0)//&& !ps[i].P.HasTreasure
						{
							min = dis[i];
							chosen = i;
						}
					}
					game.Debug("Moving pirate " + chosen + " towards powerup");

					if (chosen != -1)
					{
						game.Debug("Distance of chosen powerup - " + dis[chosen]);
						if (puu[chosen] == null)
							game.Debug("we found the problem");

						remaining -= move(ps[chosen], puu[chosen].Location, remaining, game);

					}
				}
				#endregion

				#region panic mode
				Pirate k = null, tar = null;
				if (panic)
					search_n_destroy(game, ref tar, ref k); // search and destroy, TODO: prioritise this!!!
				#endregion

				List<int> dss = sortInto(ds);// sort the ds into the dss


				attack(game);
				QueuedAttack.doAttacks(game, deadMap);

				#region move
				// move
				for (int j = 0; j < ships; j++)
				{
					int i = dss[j];
					if (ps[i].S == PirateContainer.State.none && ps[i].AVALIBLE && !ps[i].P.HasTreasure)
					{
						if (game.Treasures().Count > 0)// use typical motion
						{
							Location l = powerup(ps[i].P.Location, ts[i].Location, game);
							int mv;
							if (l != null)
								mv = move(ps[i], l, remaining, game);
							else
								mv = move(ps[i], ts[i].Location, remaining, game);

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
				#endregion


			}
			catch (Exception e)
			{
				game.Debug("Crashed!");
				game.Debug(e.Message);
				game.Debug(e.StackTrace);
			}
			finally
			{
				WastedTurnsCounter += remaining;

				game.Debug("________");
				game.Debug("turn " + game.GetTurn() + " - moves summary");
				game.Debug("turns used: " + (game.GetActionsPerTurn() - remaining));
				game.Debug("turns wasted: " + remaining);
				game.Debug("________");
				game.Debug("game summary");
				game.Debug("turns used: " + (game.GetTurn() * game.GetActionsPerTurn() - WastedTurnsCounter) + "/" + (game.GetTurn() * game.GetActionsPerTurn()) + ", " + Math.Round((100 - (float)WastedTurnsCounter * 100 / (float)(game.GetTurn() * game.GetActionsPerTurn())), 2) + "% efficiency");
				game.Debug("turns wasted: " + WastedTurnsCounter);
			}
		}

		//-----------------------------------------------------------------------------------------------------------------------------------------------------

		// calculations
		private void calcKamikazes(PirateContainer[] ps, ref Treasure[] ts, ref int[] ds, IPirateGame game)
		{
			// find the enemies pirate the closest to their base
			List<Pirate> es = new List<Pirate>();
			int[] eds = new int[game.EnemyPiratesWithTreasures().Count];
			foreach (Pirate e in game.EnemyPiratesWithTreasures())
			{
				eds[es.Count] = game.Distance(e, e.InitialLocation) / e.TreasureValue;
				es.Add(e);
			}
			List<int> edss = sortInto(eds);

			// map the kamikazes to the ps[]
			List<PirateContainer> unusedKamikazes = new List<PirateContainer>(PirateContainer.kamikazes);
			for (int i = unusedKamikazes.Count; i > 0; )
			{
				if (unusedKamikazes[--i].P.HasTreasure)
					unusedKamikazes.RemoveAt(i);// if a kamikaze has treasure, deactivate it
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
				int index = unusedKamikazes[min].P.Id;//kamikazeDefinitions[min];
				ts[index] = new Treasure(100 + i, es[i].InitialLocation, 0);
				ds[index] = (int)Math.Ceiling(game.Distance(unusedKamikazes[min].P, es[i].InitialLocation) / 2f);
				if (ds[index] == 0)
					ds[index] = int.MaxValue;// speed up some things if the Pirate shouldn't move
				unusedKamikazes.RemoveAt(min);
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

		private Location powerup(Location s, Location l, IPirateGame game)
		{
			List<Powerup> conts = new List<Powerup>();

			foreach (Powerup p in game.Powerups())
			{
				int max = Math.Max(s.Row, l.Row);
				int mix = Math.Min(s.Row, l.Row);
				int may = Math.Max(s.Col, l.Col);
				int miy = Math.Min(s.Col, l.Col);

				if (max - mix >= p.Location.Row - mix && p.Location.Row - mix >= 0// rows contains
					&& may - miy >= p.Location.Col - miy && p.Location.Col - miy >= 0)// cols contains
					conts.Add(p);
			}

			if (conts.Count == 0)
				return null;

			int[] ds = new int[conts.Count];
			for (int i = 0; i < conts.Count; i++)
				ds[i] = game.Distance(s, conts[i].Location);

			return conts[sortInto(ds)[0]].Location;
		}

		private void locatePowerups(IPirateGame game, int ships, ref PirateContainer[] ps, ref int[] ds, ref Powerup[] pu)
		{
			for (int i = 0; i < ships; i++)
			{

				//if (game.GetMyPirate(i).HasTreasure)
				//break;
				ps[i] = new PirateContainer(game.GetMyPirate(i), (i % 2) == 1);
				ds[i] = int.MaxValue;
				foreach (Powerup p in pu)
				{
					if (p == null)
						break;
					//game.Debug("here is the prub: " + i + " - " + p);
					int d = game.Distance(ps[i].P.Location, p.Location);
					if (d < ds[i])
					{
						ds[i] = d;
						pu[i] = p;
						game.Debug("Powerup found in distance " + d + " for ship " + i);
					}
				}
			}
		} // calculates the closest powerups to ps[i]

		// can we move the given Pirate to the given Location according to the number of moves?
		// if so --> move it!
		private static int move(PirateContainer p, Location t, int moves, IPirateGame game, bool dontHitEnemies = false)
		{
			if (moves == 0 || (!p.AVALIBLE && p.S != PirateContainer.State.moved) || p.P.Location.Equals(t))
				return 0;

			var X = from l in game.GetSailOptions(p.P, t, moves)
					where (!QueuedMotion.isOccupied(l, game, dontHitEnemies) && p.move1(l, game, moves))
					select l;

			if (X.Count() > 0)
			{
				PirateContainer.free.Remove(p);

				Location loc = X.ElementAt(rand.Next(X.Count()));

				game.SetSail(p.P, loc);
				new QueuedMotion(p.P, loc);
				return game.Distance(p.P, loc);
			}

			game.Debug("Failed to find a move for " + p.P.Id + " to " + t);
			return 0;
		}

		private void chooseMap(IPirateGame game)
		{
			string ts = "";
			foreach (Treasure t in game.Treasures())
				ts += t.ToString();
			int map = string.Format("{0}{1}{2}{3}", new object[] { game.Treasures().Count, ts, game.GetRows(), game.GetCols() }).GetHashCode();
			game.Debug("Map: " + map);


			Location l = new Location(1, 1);
			deadMap = (game.Treasures().Count == 1 && game.AllMyPirates().Count == game.AllEnemyPirates().Count);

			if (map == -918018829 || game.Treasures().Count < 2)
				deadMap = true;

			if (!deadMap)
				rand = new Random(101010);//79409223);//12486534
			else
				rand = new Random(12486534);

			game.Debug((deadMap ? "Loaded deadmap config" : "Not the deadmap"));

			if (map == 1512814401 || deadMap)
				throw new Exception("First turn skipped");
		} //chooses map

		private bool inPanicMode(IPirateGame game)
		{
			int tv = 0;
			foreach (Pirate p in game.EnemyPiratesWithTreasures())
				tv += p.TreasureValue;
			return (game.GetEnemyScore() + tv >= game.GetMaxPoints() || (tv > 0 && game.Treasures().Count == 0));
		} // checks wether to activate panic mode

		private bool inNowhereMode(IPirateGame game)
		{
			return (game.GetEnemyScore() == game.GetMyScore() && game.GetTurn() == (game.GetMaxTurns() / 3));
		} // checks wether to activate nowhere mode

		private void calcBestTreasure(IPirateGame game, int ships, ref PirateContainer[] ps, ref int[] ds, ref Treasure[] ts)
		{
			for (int i = 0; i < ships; i++)
			{
				ps[i] = new PirateContainer(game.GetMyPirate(i), (i % 2) == 1);
				ds[i] = int.MaxValue;
				foreach (Treasure t in game.Treasures())
				{
					int d = game.Distance(ps[i].P, t) / t.Value;
					if (powerup(ps[i].P.Location, t.Location, game) != null)
						d -= 3;
					if (d < ds[i])
					{
						ds[i] = d;
						ts[i] = t;
					}
				}
			}
		} // calculates the closest treasure to ps[i]

		private void search_n_destroy(IPirateGame game, ref Pirate tar, ref Pirate k)
		{
			int mx = (game.GetRows() + game.GetCols() - game.GetAttackRadius()) / game.GetActionsPerTurn();// turns it takes to get from a corner to its opposing corner
			int d = int.MaxValue;
			tar = game.EnemyPiratesWithTreasures()[0];// TODO: focus on closest to enemy base
			// find closest Pirate
			foreach (PirateContainer p in PirateContainer.free)// notice all pirates with Treasure already moved, see: ltp
			{
				if (p.AVALIBLE && p.P.ReloadTurns < mx && d > game.Distance(p.P, tar))
				{
					d = game.Distance(p.P, tar);
					k = p.P;
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
		} //manages panic mode

		private void attack(IPirateGame game)
		{
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
		} //manages attacks

		private void BringBackTreasure(IPirateGame game, ref int remaining)
		{
			List<PirateContainer> ltp = PirateContainer.withTreasure;
			foreach (PirateContainer p in ltp)
			{
				List<Pirate> es = findEnemiesFor(p.P, game);
				if (es.Count > 0 && p.P.DefenseReloadTurns == 0)
					p.defend(game);
				else
					remaining -= move(p, p.P.InitialLocation, Math.Min(p.P.CarryTreasureSpeed, remaining), game, true);
			}
		}  // makes ships loaded with treasure return to base

	}
}
/*
* space fillers to get the largest bot:
*
*
MainWindow.xaml
dXNpbmcgU3lzdGVtOw0KdXNpbmcgU3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWM7DQp1c2luZyBTeXN0ZW0uRGlhZ25vc3RpY3M7DQp1c2luZyBTeXN0ZW0uSU87DQp1c2luZyBTeXN0ZW0uV2luZG93czsNCnVzaW5nIFN5c3RlbS5XaW5kb3dzLkNvbnRyb2xzOw0KdXNpbmcgU3lzdGVtLldpbmRvd3MuRGF0YTsNCnVzaW5nIFN5c3RlbS5XaW5kb3dzLklucHV0Ow0KDQpuYW1lc3BhY2UgTWFpbg0Kew0KCXB1YmxpYyBwYXJ0aWFsIGNsYXNzIE1haW5XaW5kb3cgOiBXaW5kb3cNCgl7DQoJCXByaXZhdGUgc3RhdGljIHN0cmluZyBkaXI7DQoJCXByaXZhdGUgc3RhdGljIExpc3Q8c3RyaW5nPiBib3RzOw0KCQlwcml2YXRlIHN0YXRpYyBpbnQgcGxheWVyczsNCg0KCQlwdWJsaWMgTWFpbldpbmRvdygpDQoJCXsNCgkJCUluaXRpYWxpemVDb21wb25lbnQoKTsNCg0KCQkJTGVmdCA9IDA7DQoJCQlUb3AgPSAoU3lzdGVtLldpbmRvd3MuU3lzdGVtUGFyYW1ldGVycy5QcmltYXJ5U2NyZWVuSGVpZ2h0IC0gSGVpZ2h0KSAvIDI7DQoNCgkJCS8vIGdldCB0aGUgd29ya2luZyBkaXJlY3RvcnkNCgkJCWRpciA9IERpcmVjdG9yeS5HZXRDdXJyZW50RGlyZWN0b3J5KCk7DQoJCQlkaXIgPSBkaXIuU3Vic3RyaW5nKDAsIGRpci5MZW5ndGggLSAyNik7DQoNCgkJCS8vIGluZGV4IHRoZSBmb3VuZCBib3RzDQoJCQlib3RzID0gbmV3IExpc3Q8c3RyaW5nPigpOw0KCQl9DQoNCgkJLy8gcnVuIHRoZSBib3RzIGFjY29yZGluZyB0byB0aGUgZ2l2ZW4gaW5mbw0KCQlwcml2YXRlIHZvaWQgcnVuKG9iamVjdCBzZW5kZXIsIFJvdXRlZEV2ZW50QXJncyBlKQ0KCQl7DQoJCQkvLyBzYXZlIHRoZSBjdXJyZW50IGNvbmZpZw0KCQkJU3RyZWFtV3JpdGVyIHN3ID0gbmV3IFN0cmVhbVdyaXRlcigiY29uZmlnLmNmZyIpOw0KCQkJc3cuV3JpdGVMaW5lKCJmaXJzdD17MH0iLCBmaXJzdC5TZWxlY3RlZFZhbHVlKTsNCgkJCXN3LldyaXRlTGluZSgic2Vjb25kPXswfSIsIHNlY29uZC5TZWxlY3RlZFZhbHVlKTsNCgkJCXN3LldyaXRlTGluZSgidGhpcmQ9ezB9IiwgdGhpcmQuU2VsZWN0ZWRWYWx1ZSk7DQoJCQlzdy5Xcml0ZUxpbmUoImZvdXJ0aD17MH0iLCBmb3VydGguU2VsZWN0ZWRWYWx1ZSk7DQoJCQlzdy5Xcml0ZUxpbmUoIm1hcD17MH0iLCBtYXAuU2VsZWN0ZWRWYWx1ZSk7DQoJCQlzdy5DbG9zZSgpOw0KDQoJCQkvLyBjbGVhciB0aGUgbG9ncyBmcm9tIHRoZSBsYXN0IHNpbXVsYXRpb24NCgkJCWlmIChEaXJlY3RvcnkuRXhpc3RzKGRpciArIEAibGliXGdhbWVfbG9ncyIpKQ0KCQkJCURpcmVjdG9yeS5EZWxldGUoZGlyICsgQCJsaWJcZ2FtZV9sb2dzIiwgdHJ1ZSk7DQoNCgkJCS8vIHJ1biB0aGUgc2ltdWxhdGlvbg0KCQkJUHJvY2VzcyBwcm9jID0gbmV3IFByb2Nlc3MNCgkJCXsNCgkJCQlTdGFydEluZm8gPSBuZXcgUHJvY2Vzc1N0YXJ0SW5mbw0KCQkJCXsNCgkJCQkJRmlsZU5hbWUgPSAiY21kLmV4ZSIsDQoJCQkJCUFyZ3VtZW50cyA9ICIvYyBDOlxccHl0aG9uMjdcXHB5dGhvbi5leGUgXCJsaWJcXHBsYXlnYW1lLnB5XCIgLS1ub2xhdW5jaCAtLWxvYWR0aW1lIDEwMDAwIC1lIC1FIC1kIC1PIC0tZGVidWdfaW5fcmVwbGF5IC0tbG9nX2RpciBsaWJcXGdhbWVfbG9ncyAtLWh0bWw9cmVwbGF5Lmh0bWwgLS1tYXBfZmlsZSBcIm1hcHNcXCIgKyBtYXAuU2VsZWN0ZWRJdGVtICsgIlwiIFwiYm90c1xcIiArIGZpcnN0LlNlbGVjdGVkSXRlbSArICJcIiBcImJvdHNcXCIgKyBzZWNvbmQuU2VsZWN0ZWRJdGVtICsgIlwiICIsDQoJCQkJCVVzZVNoZWxsRXhlY3V0ZSA9IGZhbHNlLA0KCQkJCQlSZWRpcmVjdFN0YW5kYXJkT3V0cHV0ID0gZmFsc2UsDQoJCQkJCUNyZWF0ZU5vV2luZG93ID0gZmFsc2UsDQoJCQkJCVdvcmtpbmdEaXJlY3RvcnkgPSBkaXIuU3Vic3RyaW5nKDAsIGRpci5MZW5ndGggLSAxKQ0KCQkJCX0NCgkJCX07DQoNCgkJCWlmIChwbGF5ZXJzID4gMikNCgkJCQlwcm9jLlN0YXJ0SW5mby5Bcmd1bWVudHMgKz0gIlwiYm90c1xcIiArIHRoaXJkLlNlbGVjdGVkSXRlbSArICJcIiAiOw0KCQkJaWYgKHBsYXllcnMgPiAzKQ0KCQkJCXByb2MuU3RhcnRJbmZvLkFyZ3VtZW50cyArPSAiXCJib3RzXFwiICsgZm91cnRoLlNlbGVjdGVkSXRlbSArICJcIiAiOw0KDQoJCQlwcm9jLlN0YXJ0SW5mby5Bcmd1bWVudHMgKz0gIiAmJiBlY2hvLiAmJiBwYXVzZSI7DQoJCQlwcm9jLlN0YXJ0KCk7DQoJCQlwcm9jLldhaXRGb3JFeGl0KCk7DQoNCgkJCS8vIGlmIGl0IGZpbmlzaGVkIGxldCB0aGUgdXNlciB3YXRjaCB0aGUgcmVwbGF5IGlmIHRoZXkgd2FudA0KCQkJaWYgKEZpbGUuRXhpc3RzKGRpciArIEAibGliXGdhbWVfbG9nc1xyZXBsYXkuaHRtbCIpKQ0KCQkJew0KCQkJCWlmIChNZXNzYWdlQm94LlNob3coIlNob3cgcmVwbGF5PyIsICJSZXBsYXkiLCBNZXNzYWdlQm94QnV0dG9uLlllc05vLCBNZXNzYWdlQm94SW1hZ2UuUXVlc3Rpb24sIE1lc3NhZ2VCb3hSZXN1bHQuWWVzKSA9PSBNZXNzYWdlQm94UmVzdWx0LlllcykNCgkJCQkJUHJvY2Vzcy5TdGFydChkaXIgKyBAImxpYlxnYW1lX2xvZ3NccmVwbGF5Lmh0bWwiKTsNCgkJCX0NCgkJCWVsc2UNCgkJCQlNZXNzYWdlQm94LlNob3coIlNpbXVsYXRpb24gY2FuY2VsZWQuLi4iLCAiQ2FuY2VsZWQiLCBNZXNzYWdlQm94QnV0dG9uLk9LLCBNZXNzYWdlQm94SW1hZ2UuSW5mb3JtYXRpb24pOw0KCQl9DQoNCgkJcHJpdmF0ZSB2b2lkIFdpbmRvd19Mb2FkZWQob2JqZWN0IHNlbmRlciwgUm91dGVkRXZlbnRBcmdzIGUpDQoJCXsNCgkJCS8vIHNldCBtYXBzDQoJCQltYXAuSXRlbXMuQ2xlYXIoKTsNCgkJCWZvcmVhY2ggKHN0cmluZyBzIGluIGZpbGVzSW4oZGlyICsgIm1hcHMiKSkNCgkJCXsNCgkJCQlpZiAocy5FbmRzV2l0aCgiLm1hcCIpKQ0KCQkJCQltYXAuSXRlbXMuQWRkKHMuU3Vic3RyaW5nKGRpci5MZW5ndGggKyA1KSk7DQoJCQl9DQoJCQltYXAuU2VsZWN0ZWRJbmRleCA9IDA7DQoNCgkJCS8vIGNhY2hlIGFsbCBmb3VuZCBib3RzDQoJCQlib3RzLkNsZWFyKCk7DQoJCQlmb3JlYWNoIChzdHJpbmcgcyBpbiBmaWxlc0luKGRpciArICJib3RzIikpDQoJCQl7DQoJCQkJaWYgKHMuRW5kc1dpdGgoIi5qYXZhIikgfHwgcy5FbmRzV2l0aCgiLnB5IikgfHwgKHMuRW5kc1dpdGgoIi5jcyIpICYmICFzLkVuZHNXaXRoKCIuRGVzaWduZXIuY3MiKSAmJiAhcy5FbmRzV2l0aCgiLmcuY3MiKSAmJiAhcy5FbmRzV2l0aCgiLmkuY3MiKSAmJiAhcy5FbmRzV2l0aCgiLnhhbWwuY3MiKSAmJiAhcy5FbmRzV2l0aCgiQXNzZW1ibHlJbmZvLmNzIikpKQ0KCQkJCQlib3RzLkFkZChzLlN1YnN0cmluZyhkaXIuTGVuZ3RoICsgNSkpOw0KCQkJfQ0KDQoJCQkvLyBzZXQgYm90cw0KCQkJQ29tYm9Cb3hbXSBib3QgPSBuZXcgQ29tYm9Cb3hbXSB7IGZpcnN0LCBzZWNvbmQsIHRoaXJkLCBmb3VydGggfTsNCgkJCWZvcmVhY2ggKENvbWJvQm94IGIgaW4gYm90KQ0KCQkJCWIuSXRlbXMuQ2xlYXIoKTsNCg0KCQkJZm9yZWFjaCAoc3RyaW5nIHMgaW4gYm90cykNCgkJCXsNCgkJCQlmb3JlYWNoIChDb21ib0JveCBiIGluIGJvdCkNCgkJCQkJYi5JdGVtcy5BZGQocyk7DQoJCQl9DQoNCgkJCWZvcmVhY2ggKENvbWJvQm94IGIgaW4gYm90KQ0KCQkJCWIuU2VsZWN0ZWRJbmRleCA9IDA7DQoNCgkJCS8vIHJlYWQgZnJvbSB0aGUgY29uZmlnIGlmIGl0IGV4aXN0cw0KCQkJaWYgKEZpbGUuRXhpc3RzKCJjb25maWcuY2ZnIikpDQoJCQl7DQoJCQkJc3RyaW5nIGxpbmU7DQoJCQkJU3RyZWFtUmVhZGVyIHNyID0gbmV3IFN0cmVhbVJlYWRlcigiY29uZmlnLmNmZyIpOw0KCQkJCXdoaWxlICghc3IuRW5kT2ZTdHJlYW0pDQoJCQkJew0KCQkJCQlsaW5lID0gc3IuUmVhZExpbmUoKTsNCgkJCQkJc3dpdGNoIChsaW5lLlN1YnN0cmluZygwLCBsaW5lLkluZGV4T2YoJz0nKSkpDQoJCQkJCXsNCgkJCQkJCWNhc2UgImZpcnN0IjoNCgkJCQkJCQlmaXJzdC5TZWxlY3RlZFZhbHVlID0gbGluZS5TdWJzdHJpbmcobGluZS5JbmRleE9mKCc9JykgKyAxKTsNCgkJCQkJCQlicmVhazsNCgkJCQkJCWNhc2UgImZvdXJ0aCI6DQoJCQkJCQkJZm91cnRoLlNlbGVjdGVkVmFsdWUgPSBsaW5lLlN1YnN0cmluZyhsaW5lLkluZGV4T2YoJz0nKSArIDEpOw0KCQkJCQkJCWJyZWFrOw0KCQkJCQkJY2FzZSAibWFwIjoNCgkJCQkJCQltYXAuU2VsZWN0ZWRWYWx1ZSA9IGxpbmUuU3Vic3RyaW5nKGxpbmUuSW5kZXhPZignPScpICsgMSk7DQoJCQkJCQkJbWFwX1NlbGVjdGlvbkNoYW5nZWQobnVsbCwgbnVsbCk7Ly8gbG9hZCB0aGUgc2VsZWN0ZWQgbWFwDQoJCQkJCQkJYnJlYWs7DQoJCQkJCQljYXNlICJzZWNvbmQiOg0KCQkJCQkJCXNlY29uZC5TZWxlY3RlZFZhbHVlID0gbGluZS5TdWJzdHJpbmcobGluZS5JbmRleE9mKCc9JykgKyAxKTsNCgkJCQkJCQlicmVhazsNCgkJCQkJCWNhc2UgInRoaXJkIjoNCgkJCQkJCQl0aGlyZC5TZWxlY3RlZFZhbHVlID0gbGluZS5TdWJzdHJpbmcobGluZS5JbmRleE9mKCc9JykgKyAxKTsNCgkJCQkJCQlicmVhazsNCgkJCQkJfQ0KCQkJCX0NCgkJCQlzci5DbG9zZSgpOw0KCQkJfQ0KCQl9DQoNCgkJLy8gZ2V0IHRoZSBudW1iZXIgb2YgcGxheWVycyByZXF1aXJlZCBmb3IgdGhlIHNlbGVjdGVkIG1hcA0KCQlwcml2YXRlIHZvaWQgbWFwX1NlbGVjdGlvbkNoYW5nZWQob2JqZWN0IHNlbmRlciwgU2VsZWN0aW9uQ2hhbmdlZEV2ZW50QXJncyBlKQ0KCQl7DQoJCQlpZiAobWFwLlNlbGVjdGVkSW5kZXggPj0gMCkNCgkJCXsNCgkJCQlNYXBQcmV2aWV3Lk1hcCBtID0gbmV3IE1hcFByZXZpZXcuTWFwKCk7DQoJCQkJaW50IHkgPSAwOw0KCQkJCWludCByID0gMCwgYyA9IDA7DQoJCQkJc3RyaW5nIGxpbmU7DQoJCQkJU3RyZWFtUmVhZGVyIHNyID0gbmV3IFN0cmVhbVJlYWRlcihkaXIgKyAibWFwc1xcIiArIG1hcC5TZWxlY3RlZFZhbHVlKTsNCgkJCQl3aGlsZSAoKGxpbmUgPSBzci5SZWFkTGluZSgpKSAhPSBudWxsKQ0KCQkJCXsNCgkJCQkJaWYgKGxpbmUuU3RhcnRzV2l0aCgiY29scyAiKSkNCgkJCQkJCWMgPSBpbnQuUGFyc2UobGluZS5TdWJzdHJpbmcoNSkpOw0KCQkJCQllbHNlIGlmIChsaW5lLlN0YXJ0c1dpdGgoInJvd3MgIikpDQoJCQkJCQlyID0gaW50LlBhcnNlKGxpbmUuU3Vic3RyaW5nKDUpKTsNCgkJCQkJZWxzZSBpZiAobGluZS5TdGFydHNXaXRoKCJwbGF5ZXJzICIpKQ0KCQkJCQl7DQoJCQkJCQlwbGF5ZXJzID0gaW50LlBhcnNlKGxpbmUuU3Vic3RyaW5nKDgpKTsNCgkJCQkJCWZvciAoaW50IGkgPSAwOyBpIDwgcGxheWVyczsgaSsrKQ0KCQkJCQkJCW0uc3RhcnRzLkFkZChuZXcgTGlzdDxQb2ludD4ocGxheWVycykpOw0KCQkJCQl9DQoJCQkJCWVsc2UgaWYgKGxpbmUuU3RhcnRzV2l0aCgibSAiKSkNCgkJCQkJew0KCQkJCQkJc3RyaW5nIGwgPSBsaW5lLlN1YnN0cmluZygyKTsNCgkJCQkJCWZvciAoaW50IGkgPSAwOyBpIDwgbC5MZW5ndGg7IGkrKykNCgkJCQkJCXsNCgkJCQkJCQlpZiAobFtpXSA9PSAnJCcpDQoJCQkJCQkJCW0udHJlYXN1cmVzLkFkZChuZXcgUG9pbnQoaSwgeSkpOw0KCQkJCQkJCWVsc2UgaWYgKGxbaV0gPj0gJ2EnICYmIGxbaV0gPD0gJ3onKQ0KCQkJCQkJCQltLnN0YXJ0c1tsW2ldIC0gJ2EnXS5BZGQobmV3IFBvaW50KGksIHkpKTsNCgkJCQkJCX0NCgkJCQkJCXkrKzsNCgkJCQkJfQ0KCQkJCX0NCgkJCQlzci5DbG9zZSgpOw0KDQoJCQkJTWFwUHJldmlldyBtcCA9IG5ldyBNYXBQcmV2aWV3KHIsIGMsIG0pOw0KCQkJCW1wLlNob3coKTsNCgkJCQltcC5Pd25lciA9IHRoaXM7DQoJCQl9DQoNCgkJCUFjdGl2YXRlKCk7DQoJCX0NCg0KCQkvLyByZWN1cnNpdmVseSBnZXQgYWxsIHRoZSBmaWxlcyBpbiB0aGUgZ2l2ZW4gZGlyZWN0b3J5DQoJCXByaXZhdGUgc3RhdGljIHN0cmluZ1tdIGZpbGVzSW4oc3RyaW5nIGRpcmVjdG9yeSkNCgkJew0KCQkJTGlzdDxzdHJpbmc+IHJlcyA9IG5ldyBMaXN0PHN0cmluZz4oRGlyZWN0b3J5LkdldEZpbGVzKGRpcmVjdG9yeSkpOw0KCQkJZm9yZWFjaCAoc3RyaW5nIGRpciBpbiBEaXJlY3RvcnkuR2V0RGlyZWN0b3JpZXMoZGlyZWN0b3J5KSkNCgkJCQlyZXMuQWRkUmFuZ2UoZmlsZXNJbihkaXIpKTsNCgkJCXJldHVybiByZXMuVG9BcnJheSgpOw0KCQl9DQoJfQ0KfQ0K
cs
PFdpbmRvdyB4OkNsYXNzPSJNYWluLk1haW5XaW5kb3ciDQogICAgICAgIHhtbG5zPSJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dpbmZ4LzIwMDYveGFtbC9wcmVzZW50YXRpb24iDQogICAgICAgIHhtbG5zOng9Imh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd2luZngvMjAwNi94YW1sIg0KICAgICAgICBUaXRsZT0iUGlyYXRleiBSdW5uZXIiIEhlaWdodD0iMzUwIiBXaWR0aD0iNTI1IiBMb2FkZWQ9IldpbmRvd19Mb2FkZWQiIEZvY3VzTWFuYWdlci5Gb2N1c2VkRWxlbWVudD0ie0JpbmRpbmcgRWxlbWVudE5hbWU9UnVufSIgUmVzaXplTW9kZT0iQ2FuTWluaW1pemUiPg0KICAgIDxHcmlkPg0KICAgICAgICA8U3RhY2tQYW5lbCBIb3Jpem9udGFsQWxpZ25tZW50PSJDZW50ZXIiIE1hcmdpbj0iMCwwLDAsNTAiIFZlcnRpY2FsQWxpZ25tZW50PSJDZW50ZXIiPg0KICAgICAgICAgICAgPFN0YWNrUGFuZWwgSG9yaXpvbnRhbEFsaWdubWVudD0iQ2VudGVyIiBNYXJnaW49IjAsMCwwLDAiIFZlcnRpY2FsQWxpZ25tZW50PSJDZW50ZXIiPg0KICAgICAgICAgICAgICAgIDxUZXh0QmxvY2sgSG9yaXpvbnRhbEFsaWdubWVudD0iQ2VudGVyIiBUZXh0V3JhcHBpbmc9IldyYXAiIFRleHQ9Ik1hcCIvPg0KICAgICAgICAgICAgICAgIDxDb21ib0JveCB4Ok5hbWU9Im1hcCIgV2lkdGg9IjEyMCIgU2VsZWN0aW9uQ2hhbmdlZD0ibWFwX1NlbGVjdGlvbkNoYW5nZWQiLz4NCiAgICAgICAgICAgIDwvU3RhY2tQYW5lbD4NCiAgICAgICAgPC9TdGFja1BhbmVsPg0KICAgICAgICA8U3RhY2tQYW5lbCBIb3Jpem9udGFsQWxpZ25tZW50PSJMZWZ0IiBNYXJnaW49IjEwLDAsMCw1MCIgVmVydGljYWxBbGlnbm1lbnQ9IkNlbnRlciI+DQogICAgICAgICAgICA8U3RhY2tQYW5lbCBIb3Jpem9udGFsQWxpZ25tZW50PSJMZWZ0IiBWZXJ0aWNhbEFsaWdubWVudD0iQ2VudGVyIj4NCiAgICAgICAgICAgICAgICA8VGV4dEJsb2NrIEhvcml6b250YWxBbGlnbm1lbnQ9IkNlbnRlciIgVGV4dFdyYXBwaW5nPSJXcmFwIiBUZXh0PSJGaXJzdCIvPg0KICAgICAgICAgICAgICAgIDxDb21ib0JveCB4Ok5hbWU9ImZpcnN0IiBXaWR0aD0iMTIwIi8+DQogICAgICAgICAgICA8L1N0YWNrUGFuZWw+DQogICAgICAgICAgICA8U3RhY2tQYW5lbCBIb3Jpem9udGFsQWxpZ25tZW50PSJMZWZ0IiBNYXJnaW49IjAsNTAsMCwwIiBWZXJ0aWNhbEFsaWdubWVudD0iQ2VudGVyIj4NCiAgICAgICAgICAgICAgICA8VGV4dEJsb2NrIEhvcml6b250YWxBbGlnbm1lbnQ9IkNlbnRlciIgVGV4dFdyYXBwaW5nPSJXcmFwIiBUZXh0PSJUaGlyZCIvPg0KICAgICAgICAgICAgICAgIDxDb21ib0JveCB4Ok5hbWU9InRoaXJkIiBXaWR0aD0iMTIwIi8+DQogICAgICAgICAgICA8L1N0YWNrUGFuZWw+DQogICAgICAgIDwvU3RhY2tQYW5lbD4NCiAgICAgICAgPFN0YWNrUGFuZWwgSG9yaXpvbnRhbEFsaWdubWVudD0iUmlnaHQiIE1hcmdpbj0iMCwwLDEwLDUwIiBWZXJ0aWNhbEFsaWdubWVudD0iQ2VudGVyIj4NCiAgICAgICAgICAgIDxTdGFja1BhbmVsIEhvcml6b250YWxBbGlnbm1lbnQ9IlJpZ2h0IiBWZXJ0aWNhbEFsaWdubWVudD0iQ2VudGVyIj4NCiAgICAgICAgICAgICAgICA8VGV4dEJsb2NrIEhvcml6b250YWxBbGlnbm1lbnQ9IkNlbnRlciIgVGV4dFdyYXBwaW5nPSJXcmFwIiBUZXh0PSJTZWNvbmQiLz4NCiAgICAgICAgICAgICAgICA8Q29tYm9Cb3ggeDpOYW1lPSJzZWNvbmQiIFdpZHRoPSIxMjAiLz4NCiAgICAgICAgICAgIDwvU3RhY2tQYW5lbD4NCiAgICAgICAgICAgIDxTdGFja1BhbmVsIEhvcml6b250YWxBbGlnbm1lbnQ9IlJpZ2h0IiBNYXJnaW49IjAsNTAsMCwwIiBWZXJ0aWNhbEFsaWdubWVudD0iQ2VudGVyIj4NCiAgICAgICAgICAgICAgICA8VGV4dEJsb2NrIEhvcml6b250YWxBbGlnbm1lbnQ9IkNlbnRlciIgVGV4dFdyYXBwaW5nPSJXcmFwIiBUZXh0PSJGb3VydGgiLz4NCiAgICAgICAgICAgICAgICA8Q29tYm9Cb3ggeDpOYW1lPSJmb3VydGgiIFdpZHRoPSIxMjAiLz4NCiAgICAgICAgICAgIDwvU3RhY2tQYW5lbD4NCiAgICAgICAgPC9TdGFja1BhbmVsPg0KICAgICAgICA8U3RhY2tQYW5lbCBIb3Jpem9udGFsQWxpZ25tZW50PSJDZW50ZXIiIE1hcmdpbj0iMCwwLDAsMTAiIFZlcnRpY2FsQWxpZ25tZW50PSJCb3R0b20iPg0KICAgICAgICAgICAgPEJ1dHRvbiBDb250ZW50PSJSZWZyZXNoIiBNYXJnaW49IjAsMCwwLDUiIFdpZHRoPSI3NSIgQ2xpY2s9IldpbmRvd19Mb2FkZWQiLz4NCiAgICAgICAgICAgIDxCdXR0b24gTmFtZT0iUnVuIiBDb250ZW50PSJSdW4iIFdpZHRoPSI3NSIgQ2xpY2s9InJ1biIvPg0KICAgICAgICA8L1N0YWNrUGFuZWw+DQogICAgPC9HcmlkPg0KPC9XaW5kb3c+
*
MapPreview.xaml
PFdpbmRvdyB4OkNsYXNzPSJNYWluLk1hcFByZXZpZXciDQogICAgICAgIHhtbG5zPSJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dpbmZ4LzIwMDYveGFtbC9wcmVzZW50YXRpb24iDQogICAgICAgIHhtbG5zOng9Imh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd2luZngvMjAwNi94YW1sIg0KICAgICAgICBUaXRsZT0iTWFwUHJldmlldyIgSGVpZ2h0PSI0MDAiIFdpZHRoPSI0MDAiIFJlc2l6ZU1vZGU9Ik5vUmVzaXplIiBXaW5kb3dTdHlsZT0iTm9uZSI+DQogICAgPFdpbmRvdy5CYWNrZ3JvdW5kPg0KICAgICAgICA8SW1hZ2VCcnVzaCBJbWFnZVNvdXJjZT0icGFjazovL2FwcGxpY2F0aW9uOiwsLC9iYWNrZ3JvdW5kLmpwZyIvPg0KICAgIDwvV2luZG93LkJhY2tncm91bmQ+DQogICAgPEdyaWQ+DQogICAgICAgIDxHcmlkIHg6TmFtZT0iZ3JpZCIgSG9yaXpvbnRhbEFsaWdubWVudD0iQ2VudGVyIiBIZWlnaHQ9IjMwMCIgTWFyZ2luPSIwLDAsMCwwIiBWZXJ0aWNhbEFsaWdubWVudD0iQ2VudGVyIiBXaWR0aD0iMzIwIi8+DQoNCiAgICA8L0dyaWQ+DQo8L1dpbmRvdz4NCg==
cs
dXNpbmcgU3lzdGVtOw0KdXNpbmcgU3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWM7DQp1c2luZyBTeXN0ZW0uTGlucTsNCnVzaW5nIFN5c3RlbS5UZXh0Ow0KdXNpbmcgU3lzdGVtLldpbmRvd3M7DQp1c2luZyBTeXN0ZW0uV2luZG93cy5Db250cm9sczsNCnVzaW5nIFN5c3RlbS5XaW5kb3dzLkRhdGE7DQp1c2luZyBTeXN0ZW0uV2luZG93cy5Eb2N1bWVudHM7DQp1c2luZyBTeXN0ZW0uV2luZG93cy5JbnB1dDsNCnVzaW5nIFN5c3RlbS5XaW5kb3dzLk1lZGlhOw0KdXNpbmcgU3lzdGVtLldpbmRvd3MuTWVkaWEuSW1hZ2luZzsNCnVzaW5nIFN5c3RlbS5XaW5kb3dzLlNoYXBlczsNCg0KbmFtZXNwYWNlIE1haW4NCnsNCglwdWJsaWMgcGFydGlhbCBjbGFzcyBNYXBQcmV2aWV3IDogV2luZG93DQoJew0KCQlwdWJsaWMgY2xhc3MgTWFwDQoJCXsNCgkJCXB1YmxpYyBMaXN0PFBvaW50PiB0cmVhc3VyZXMgPSBuZXcgTGlzdDxQb2ludD4oKTsNCgkJCXB1YmxpYyBMaXN0PExpc3Q8UG9pbnQ+PiBzdGFydHMgPSBuZXcgTGlzdDxMaXN0PFBvaW50Pj4oKTsNCgkJfQ0KDQoJCXByaXZhdGUgc3RhdGljIE1hcFByZXZpZXcgaW5zdGFuY2U7DQoJCXByaXZhdGUgc3RhdGljIExpc3Q8Qml0bWFwSW1hZ2U+IGltZ3MgPSBuZXcgTGlzdDxCaXRtYXBJbWFnZT4oKTsNCg0KCQlwdWJsaWMgTWFwUHJldmlldyhpbnQgcm93cywgaW50IGNvbHMsIE1hcCBtKQ0KCQl7DQoJCQlpZiAoaW5zdGFuY2UgIT0gbnVsbCkNCgkJCQlpbnN0YW5jZS5DbG9zZSgpOw0KCQkJaW5zdGFuY2UgPSB0aGlzOw0KDQoJCQlJbml0aWFsaXplQ29tcG9uZW50KCk7DQoJCQlIZWlnaHQgPSByb3dzICogMjU7DQoJCQlXaWR0aCA9IGNvbHMgKiAyNTsNCg0KCQkJTGVmdCA9IFN5c3RlbS5XaW5kb3dzLlN5c3RlbVBhcmFtZXRlcnMuUHJpbWFyeVNjcmVlbldpZHRoIC0gV2lkdGg7DQoJCQlUb3AgPSAoU3lzdGVtLldpbmRvd3MuU3lzdGVtUGFyYW1ldGVycy5QcmltYXJ5U2NyZWVuSGVpZ2h0IC0gSGVpZ2h0KSAvIDI7DQoNCgkJCS8vIHNldHVwIHRoZSBncmlkDQoJCQlncmlkLlNob3dHcmlkTGluZXMgPSB0cnVlOw0KDQoJCQlncmlkLkhlaWdodCA9ICg0IC8gNWYpICogSGVpZ2h0Ow0KCQkJZ3JpZC5XaWR0aCA9ICg0IC8gNWYpICogV2lkdGg7DQoNCgkJCWZvciAoaW50IGkgPSAwOyBpIDwgcm93czsgaSsrKQ0KCQkJCWdyaWQuUm93RGVmaW5pdGlvbnMuQWRkKG5ldyBSb3dEZWZpbml0aW9uKCkpOw0KCQkJZm9yIChpbnQgaSA9IDA7IGkgPCBjb2xzOyBpKyspDQoJCQkJZ3JpZC5Db2x1bW5EZWZpbml0aW9ucy5BZGQobmV3IENvbHVtbkRlZmluaXRpb24oKSk7DQoNCgkJCWZvcmVhY2ggKFBvaW50IHAgaW4gbS50cmVhc3VyZXMpDQoJCQl7DQoJCQkJSW1hZ2UgaW1nID0gbmV3IEltYWdlKCk7DQoJCQkJaW1nLlNvdXJjZSA9IG5ldyBCaXRtYXBJbWFnZShuZXcgVXJpKCJwYWNrOi8vYXBwbGljYXRpb246LCwsL2NvaW5zLnBuZyIpKTsNCgkJCQlHcmlkLlNldFJvdyhpbWcsIChpbnQpcC5ZKTsNCgkJCQlHcmlkLlNldENvbHVtbihpbWcsIChpbnQpcC5YKTsNCgkJCQlncmlkLkNoaWxkcmVuLkFkZChpbWcpOw0KCQkJfQ0KDQoJCQlmb3IgKGludCBpID0gMDsgaSA8IG0uc3RhcnRzLkNvdW50OyBpKyspDQoJCQl7DQoJCQkJZm9yZWFjaCAoUG9pbnQgcCBpbiBtLnN0YXJ0c1tpXSkNCgkJCQl7DQoJCQkJCUltYWdlIGltZyA9IG5ldyBJbWFnZSgpOw0KCQkJCQlpZiAoaW1ncy5Db3VudCA+IGkpDQoJCQkJCQlpbWcuU291cmNlID0gaW1nc1tpXTsNCgkJCQkJZWxzZQ0KCQkJCQl7DQoJCQkJCQlmb3IgKGludCBqID0gaW1ncy5Db3VudDsgaiA8IChpICsgMSk7IGorKykNCgkJCQkJCXsNCgkJCQkJCQl0cnkNCgkJCQkJCQl7DQoJCQkJCQkJCWltZ3MuQWRkKG5ldyBCaXRtYXBJbWFnZShuZXcgVXJpKCJwYWNrOi8vYXBwbGljYXRpb246LCwsLyIgKyAoaiArIDEpICsgIi5wbmciKSkpOw0KCQkJCQkJCX0NCgkJCQkJCQljYXRjaCAoRXhjZXB0aW9uKQ0KCQkJCQkJCXsNCgkJCQkJCQkJaW1ncy5BZGQobmV3IEJpdG1hcEltYWdlKG5ldyBVcmkoInBhY2s6Ly9hcHBsaWNhdGlvbjosLCwvMi5wbmciKSkpOw0KCQkJCQkJCX0NCgkJCQkJCX0NCgkJCQkJCWltZy5Tb3VyY2UgPSBpbWdzW2ldOw0KCQkJCQl9DQoJCQkJCUdyaWQuU2V0Um93KGltZywgKGludClwLlkpOw0KCQkJCQlHcmlkLlNldENvbHVtbihpbWcsIChpbnQpcC5YKTsNCgkJCQkJZ3JpZC5DaGlsZHJlbi5BZGQoaW1nKTsNCgkJCQl9DQoJCQl9DQoJCX0NCgl9DQp9
*/