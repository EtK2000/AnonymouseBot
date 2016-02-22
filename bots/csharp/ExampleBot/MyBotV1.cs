using System.Collections.Generic;
using Pirates;

namespace ExampleBot
{
    public class MyBotV1 : IPirateBot
    {
        public void DoTurn(IPirateGame game)
        {
            int reamaining = 6;

            Pirate[] ps = new Pirate[4];
            for (int i = 0; i < 4; i++)
                ps[i] = game.GetMyPirate(i);

            List<Pirate> ltp = game.MyPiratesWithTreasures();
            reamaining -= ltp.Count;
            foreach (Pirate p in ltp)
                game.SetSail(p, game.GetSailOptions(p, p.InitialLocation, 1)[0]);

            for (int i = 0; i < 4; i++)
            {
                if (!ps[i].HasTreasure)
                {

                    Treasure cull = null;
                    int minD = int.MaxValue;

                    foreach (Treasure t in game.Treasures())
                    {
                        if (game.Distance(ps[i], t) < minD)
                        {
                            minD = game.Distance(ps[i], t);
                            cull = t;
                        }
                    }

                    game.SetSail(ps[i], game.GetSailOptions(ps[i], cull, reamaining)[0]);
                    reamaining = 0;
                    break;
                }
            }
        }
    }
}