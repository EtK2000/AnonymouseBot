using System;
using System.Collections.Generic;
using Pirates;

namespace ExampleBot
{
    public class MyBotV3 : Pirates.IPirateBot
    {
        public void DoTurn(IPirateGame game)
        {
            List<Pirate> full_ships = game.MyPiratesWithTreasures();
            List<Pirate> empty_ships = game.MyPiratesWithoutTreasures();
            List<Treasure> current_treasures = game.Treasures();
            int min = game.Distance(empty_ships[0], current_treasures[0]);
            Pirate S = empty_ships[0];
            Treasure T = current_treasures[0];
            int steps = 6;
            bool valid = false;
            if (full_ships != null)
            {
                foreach (Pirate s in full_ships)
                {
                    game.SetSail(s, game.GetSailOptions(s, s.InitialLocation, 1)[0]);
                    steps--;
                }
            }

            if (current_treasures != null && empty_ships != null)
            {
                while (steps != 0)
                {
                    foreach (Pirate s in empty_ships)
                    {
                        if (s.ReloadTurns != 0)
                        {
                            empty_ships.Remove(S);
                            continue;
                        }
                        valid = true;
                        foreach (Treasure t in current_treasures)
                        {
                            if (game.Distance(s, t) < min && game.Distance(s, t) != 0)
                            {
                                min = game.Distance(s, t);
                                S = s;
                                T = t;

                            }
                        }
                    }
                    if (valid)
                    {
                        game.SetSail(S, game.GetSailOptions(S, T, (steps < min) ? steps : min)[0]);
                        steps -= min;
                        empty_ships.Remove(S);
                    }
                }
            }
        }
    }

}
	}
}