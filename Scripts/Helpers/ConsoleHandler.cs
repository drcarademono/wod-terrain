using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Utility;

namespace Monobelisk
{
    public static class ConsoleHandler
    {
        public static bool screenshotsEnabled = false;
        private static bool isSpeedyMode = false;

        static ContactTracker tracker;

        public static void RegisterConsoleCommands()
        {
            // 1) Ensure we have a ContactTracker on the PlayerObject
            var player = GameManager.Instance.PlayerObject;
            if (player != null && tracker == null)
            {
                tracker = player.AddComponent<ContactTracker>();
            }

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
                    Wenzil.Console.Console.ExecuteCommand("set_walkspeed", ((int)(5 * mul)).ToString());
                    Wenzil.Console.Console.ExecuteCommand("set_runspeed", ((int)(20 * mul)).ToString());
                    Wenzil.Console.Console.ExecuteCommand("set_jump", "100");
                    GameManager.Instance.PlayerEntity.GodMode = true;
                    GameManager.Instance.AcrobatMotor.airControl = true;

                    return "Speedy mode enabled with multiplier " + mul.ToString("0.00") + ". God mode enabled.";
                }

                isSpeedyMode = false;

                // Access PlayerSpeedChanger through GameManager.Instance.SpeedChanger
                PlayerSpeedChanger playerSpeedChanger = GameManager.Instance.SpeedChanger;
                if (playerSpeedChanger == null)
                {
                    Debug.LogError("PlayerSpeedChanger is not found.");
                    return "Error: PlayerSpeedChanger is not found.";
                }

                playerSpeedChanger.ResetSpeed(true, true);
                Wenzil.Console.Console.ExecuteCommand("set_jump", "8");
                GameManager.Instance.PlayerEntity.GodMode = false;
                GameManager.Instance.AcrobatMotor.airControl = false;

                return "Speedy mode disabled. God mode disabled.";
            });

            // 2) Override "groundme" to use our tracker.lastContact
            Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand(
                "groundme",
                "Snap you back to the last X/Z you stood on, but raycast from high above to guarantee you land on the actual ground.",
                "groundme",
                (args) =>
                {
                    var cc    = GameManager.Instance.PlayerController;
                    var motor = GameManager.Instance.PlayerMotor;
                    if (cc == null || motor == null || tracker == null)
                        return "Error: missing PlayerController, PlayerMotor or tracker.";

                    // If we’ve never recorded a contact, bail
                    Vector3 last = tracker.lastContact;
                    if (last == Vector3.zero)
                        return "No ground contact recorded yet. Stand on something first.";

                    // Clear any pending fall‐damage
                    GameManager.Instance.AcrobatMotor.ClearFallingDamage();

                    // Raycast **straight down** from high above that X,Z
                    RaycastHit hit;
                    var origin = new Vector3(last.x, last.y + 50000f, last.z);
                    if (Physics.Raycast(origin, Vector3.down, out hit, 100000f))
                    {
                        // Snap you to the real ground
                        var snap = hit.point + Vector3.up * (cc.height / 2f + 0.01f);
                        player.transform.position = snap;
                        motor.FixStanding(cc.height / 2f);
                        return $"Snapped to ground at {hit.point:0.00},{hit.point.y:0.00},{hit.point.z:0.00}";
                    }

                    // Last resort: respawn you at world coords of your current map-pixel
                    var pee = GameManager.Instance.PlayerEnterExit;
                    var gps = GameManager.Instance.PlayerGPS;
                    if (pee != null && gps != null)
                    {
                        pee.RespawnPlayer(gps.WorldX, gps.WorldZ, /*insideDungeon=*/false);
                        return $"Respawned at world coords ({gps.WorldX},{gps.WorldZ}).";
                    }

                    return "Critical failure: couldn't find ground or respawn.";
                }
            );
        }

        /// <summary>
        /// Tiny helper MonoBehaviour that lives on the PlayerObject
        /// and constantly updates lastContact whenever you hit or stand on anything.
        /// </summary>
        class ContactTracker : MonoBehaviour
        {
            public Vector3 lastContact = Vector3.zero;
            CharacterController cc;

            void Awake()
            {
                cc = GetComponent<CharacterController>();
                // seed with your initial foot position
                if (cc != null)
                    lastContact = transform.position - Vector3.up * (cc.height / 2f);
            }

            void FixedUpdate()
            {
                if (cc != null && cc.isGrounded)
                {
                    // a quick downward ray from center to foot
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, Vector3.down, out hit, cc.height / 2f + cc.radius))
                        lastContact = hit.point;
                }
            }

            void OnControllerColliderHit(ControllerColliderHit hit)
            {
                // any controller-collision also updates contact
                lastContact = hit.point;
            }
        }
    }
}
