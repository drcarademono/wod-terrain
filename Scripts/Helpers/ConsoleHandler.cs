using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game;

namespace Monobelisk
{
    public static class ConsoleHandler
    {
        public static bool screenshotsEnabled = false;
        private static bool isSpeedyMode = false;

        public static void RegisterConsoleCommands()
        {
            Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("clearnoon", "Forwards the time to 12 pm, clears the weather and kills enemies spawned as a result of the time shift. Intended for development purposes.", "clearnoon", (args) =>
            {
                InterestingTerrains.instance.StartCoroutine(InterestingTerrains.instance.ClearNoonRoutine());
                return "Time set to noon, weather cleared and enemies killed.";
            });

            Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("speedy", "Toggles \"Speedy\" mode, in which god mode is enabled and walk, run and jump speeds are significantly increased, on or off. Intended for development purposes. Takes an optional multiplier argument for the speed increases.", "speedy [multiplier]\nEg. > speedy 2.5", (args) =>
            {
                var mulString = args.Length == 0
                    ? "1"
                    : args[0];
                float mul;
                if (!float.TryParse(mulString, out mul))
                {
                    mul = 1.0f;
                }

                if (!isSpeedyMode || args.Length != 0)
                {
                    isSpeedyMode = true;
                    Wenzil.Console.Console.ExecuteCommand("set_walkspeed", ((int)(50 * mul)).ToString());
                    Wenzil.Console.Console.ExecuteCommand("set_runspeed", ((int)(150 * mul)).ToString());
                    Wenzil.Console.Console.ExecuteCommand("set_jump", "80");
                    GameManager.Instance.PlayerEntity.GodMode = true;

                    return "Speedy mode enabled with multiplier " + mul.ToString("0.00") + ". God mode enabled.";
                }

                isSpeedyMode = false;
                Wenzil.Console.Console.ExecuteCommand("set_walkspeed", "-1");
                Wenzil.Console.Console.ExecuteCommand("set_runspeed", "-1");
                Wenzil.Console.Console.ExecuteCommand("set_jump", "8");
                GameManager.Instance.PlayerEntity.GodMode = false;

                return "Speedy mode disabled. God mode disabled.";
            });
        }
    }
}