// The Pirate Bay g4 Go Tennenbaum Team! :D
using System.Collections.Generic;
using Pirates;
using System.Linq;

namespace ExampleBot
{
    public class MyBot : Pirates.IPirateBot
    {
        System.Random rand = new System.Random();
        IPirateGame game;
        PirateSetting[] setting = new PirateSetting[4];

        public int i = 0;
        public int turns;

        public void DoTurn(IPirateGame game)
        {
            this.game = game;
            turns = game.GetActionsPerTurn();
            PlayZero(turns);
            PlayZero(turns, 1);
            PlayZero(turns, 2);
            PlayZero(turns, 3);
        }

        public void PlayZero(int moves, int id = 0)
        {
            Pirate p0 = game.GetMyPirate(id);
            if (KillIfPossible(p0) || moves == 0 || p0.TurnsToSober > 0)
                return;

            if (p0.HasTreasure)
            {
                game.SetSail(p0, tempDes(p0, p0.InitialLocation, 1));
                turns--;
            }
            else
            {
                Treasure target = closestTreasure(FreeTreasures().ToList(), p0);
                if (target != null)
                {
                    game.SetSail(p0, tempDes(p0, target.Location, moves));
                    turns -= moves;
                }
            }
        }

        bool KillIfPossible(Pirate p)
        {
            if (p.HasTreasure) return false;
            foreach (var enemy in game.EnemySoberPirates())
                if (game.InRange(p, enemy))
                {
                    game.Attack(p, enemy);
                    return true;
                }
            return false;
        }

        Treasure closestTreasure(List<Treasure> treasures, Pirate p)
        {
            if (treasures.Count == 0)
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
                if (pirates.All(p => game.Distance(p.Location, t.Location) > 4))
                    yield return t;
        }

        public Location tempDes(Pirate ship, Location des, int numOfSteps)
        {
            var arr = game.GetSailOptions(ship, des, numOfSteps);
            return arr[rand.Next(0, arr.Count)];
        }

        void SetTargets()
        {
            var enemies = FightingEnemyPirates().GetEnumerator();
            foreach (Pirate p in game.AllMyPirates().Where(pi => pi.TurnsToSober == 0 && setting[pi.Id].IsDeployed == false && pi.ReloadTurns == 0))
                if (enemies.MoveNext())
                    setting[p.Id].Target = enemies.Current.Id;
        }

        IEnumerable<Pirate> FightingEnemyPirates()
        {
            return (from i in game.EnemySoberPirates()
                    where i.Location != i.InitialLocation
                    select i);
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