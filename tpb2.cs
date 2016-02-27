using System.Collections.Generic;
using Pirates;
using System.Linq;
using System;


namespace ExampleBot
{
    public class MyBot : Pirates.IPirateBot
    {
        Random rand;
        IPirateGame game;
        PirateSetting[] setting = new PirateSetting[4]
            {
             new PirateSetting(),
             new PirateSetting(),
             new PirateSetting(),
             new PirateSetting()
            };

        public int i = 0;
        public int turns;

        public void DoTurn(IPirateGame game)
        {
			if (game.GetTurn() == 1)
			{
				int s = DateTime.Now.Millisecond;
				rand = new Random(s);
				game.Debug("seed: " + s);
			}
            try
            {
                this.game = game;
                turns = game.GetActionsPerTurn();
                CheckBase();
                StopEnemies();
                ManagePlayZero();
            }
            catch (Exception e)
            {
                game.Debug(e.ToString());

            }
        }
        public void CheckBase()
        {
            var arr = game.AllEnemyPirates().Where(item => game.Distance(item.Location, game.AllMyPirates()[2].InitialLocation) < 5).ToList();
            if (arr.Any())
            {
                foreach (var item in game.MyPirates().Where(item2 => !item2.HasTreasure && game.Distance(item2.Location, arr[0].Location) <= 6 && game.Distance(item2.Location, game.AllMyPirates()[2].InitialLocation) < 5))
                {

                    game.SetSail(item, arr[0].Location);
                    if (arr.Count() > 0)
                        turns -= game.Distance(item.Location, arr[0].Location);
                    return;
                }
            }

        }
        void StopEnemies()
        {
            foreach (var ship in game.AllMyPirates())
                if (ship.IsLost || ship.TurnsToSober > 0)
                    if (setting[ship.Id].Target > -1 && game.GetEnemyPirate(setting[ship.Id].Target).InitialLocation != ship.Location)
                        setting[ship.Id].Target = -1;
            foreach (var ship in game.AllMyPirates())
                if (setting[ship.Id].Target > -1)
                    if (!game.AllEnemyPirates().First(item => item.Id == setting[ship.Id].Target).HasTreasure)
                        setting[ship.Id].Target = -1;
            var targets = from item in game.EnemyPirates()
                          where item.HasTreasure && !setting.Any(set => set.Target == item.Id)
                          orderby EnemyTurnsToInitial(item)
                          select item;
            foreach (var enemy in targets)
            {
                var closest = (from ship in game.MyPirates()
                               where !ship.HasTreasure
                               orderby MyTurnsToIntercept(ship, enemy)
                               select ship).FirstOrDefault();
                if (closest != null && ShouldMoveToIntercept(closest, enemy))
                    setting[closest.Id].Target = enemy.Id;
            }

            foreach (var ship in game.MyPirates())
                if (setting[ship.Id].Target > -1)
                    PlayToKill(ship, turns);
            if (game.AllMyPirates().All(item => setting[item.Id].Target == -1))
            {
                var avPirates = from pirate in game.MyPirates()
                                where !pirate.HasTreasure
                                orderby game.Distance(pirate.Location, game.AllEnemyPirates()[2].InitialLocation)
                                select pirate;

                if (!(avPirates == null || avPirates.Count() == 0))
                    setting[avPirates.First().Id].Target = -2;
            }
        }

        public void PlayToKill(Pirate p, int moves)
        {
            if (KillIfPossible(p) || moves == 0 || p.TurnsToSober > 0)
                return;
            Location targetLoc = game.GetEnemyPirate(setting[p.Id].Target).InitialLocation;
            moves = Math.Min(moves, game.Distance(p.Location, targetLoc));
            game.SetSail(p, tempDes(p, targetLoc, moves));
            turns -= moves;
        }

        public void PlayZero(int moves, int id = 0)
        {
            Pirate p0 = game.GetMyPirate(id);
            if (KillIfPossible(p0) || moves == 0 || p0.TurnsToSober > 0)
                return;
            TreasureHunt(moves, p0);
        }

        public void TreasureHunt(int moves, Pirate p0)
        {
            if (p0.HasTreasure)
            {
                game.SetSail(p0, tempDes(p0, p0.InitialLocation, 1));
                turns--;
            }
            else
            {
                if (p0.TurnsToSober > 0)
                    return;
                Treasure target = closestTreasure(FreeTreasures().ToList(), p0);
                if (target != null)
                {
                    game.SetSail(p0, tempDes(p0, target.Location, moves));
                    turns -= moves;
                }
            }
        }

        void ManagePlayZero()
        {
            var has_treasure = from worker in game.MyPirates()
                               where setting[worker.Id].Target == -1 && worker.HasTreasure
                               orderby game.Distance(worker.Location, worker.InitialLocation)
                               select worker;

            var free = from worker in game.MyPirates()
                       where setting[worker.Id].Target == -1 && !worker.HasTreasure
                       let closest = closestTreasure(game.Treasures(), worker)
                       orderby game.Distance((closest == null ? worker.Location : closest.Location), worker.Location)
                       select worker;

            foreach (var worker in has_treasure.Concat(free))
                PlayZero(turns, worker.Id);
        }

        bool KillIfPossible(Pirate p)
        {
            if (p.HasTreasure || p.ReloadTurns > 0)
                return false;
            foreach (var enemy in game.EnemySoberPirates())
            {
                if (game.InRange(p, enemy) && enemy.HasTreasure)
                {
                    game.Attack(p, enemy);
                    return true;
                }
                else if (game.InRange(p, enemy) && game.Distance(game.AllMyPirates()[2].InitialLocation, enemy.Location) < 6)
                {
                    game.Attack(p, enemy);
                    return true;
                }
            }
            return false;
        }

        Treasure closestTreasure(List<Treasure> treasures, Pirate p)
        {
            if (treasures == null || treasures.Count == 0)
                return null;
            Treasure res = treasures[0];
            foreach (Treasure t in treasures)
                if (game.Distance(t.Location, p.Location) < game.Distance(res.Location, p.Location))
                    res = t;
            return res;
        }

        IEnumerable<Treasure> FreeTreasures()
        {
            //all priates that can attack
            var pirates = game.EnemyPiratesWithoutTreasures().Where(p => p.TurnsToSober == 0 && p.ReloadTurns == 0);
            foreach (Treasure t in game.Treasures())
                if (pirates.All(p => !game.InRange(p.Location, t.Location)))
                    yield return t;
        }

        public Location tempDes(Pirate ship, Location des, int numOfSteps)
        {
            if (numOfSteps <= 0)
                return ship.Location;
            var arr = game.GetSailOptions(ship, des, numOfSteps).Where(l => game.GetPirateOn(l) == null).ToList();
            if (arr.Count == 0)
                arr = game.GetSailOptions(ship, des, numOfSteps);
            return arr[rand.Next(0, arr.Count)];
        }
        public Location tempDesWithoutTreasure(Pirate ship, Location des, int numOfSteps)
        {
            var arr = game.GetSailOptions(ship, des, numOfSteps).Where(
                l => game.GetPirateOn(l) == null
                && game.Treasures().All(t => t.Location.Row != l.Row || t.Location.Col != l.Col)).ToList();
            return arr[rand.Next(0, arr.Count)];
        }

        public int EnemyTurnsToInitial(Pirate p)
        {
            if (!p.HasTreasure)
                return -1;
            return game.Distance(p.Location, p.InitialLocation);
        }

        public int MyTurnsToIntercept(Pirate MyPirate, Pirate EnemyPirate, int MyMoves = 6)
        {
            if (MyPirate.HasTreasure)
                return -1;
            return (int)((game.Distance(MyPirate.Location, EnemyPirate.InitialLocation) / (double)MyMoves) + 0.999);
        }

        public bool ShouldMoveToIntercept(Pirate myPirate, Pirate enemy)
        {
            if (MyTurnsToIntercept(myPirate, enemy) + 1 == EnemyTurnsToInitial(enemy))
                return true;
            return false;
        }

        internal class PirateSetting
        {
            public PirateSetting()
            {
                IsDeployed = false;
                Target = -1;
            }
            public bool IsDeployed;
            public int Target;
        }
    }

}