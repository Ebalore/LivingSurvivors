using System;
using System.Collections.Generic;
using System.Text;
using LivingSurvivors.Core;
using LivingSurvivors.Npc;
using LivingSurvivors.Settings;
using LivingSurvivors.Util;
using UnityEngine;

namespace LivingSurvivors.DevTools
{
    /// <summary>
    /// Отладочные консольные команды (ТЗ 24.6). Консоль — F5; в одиночной игре сначала введите
    /// devcommands. Все команды — читы (isCheat), поэтому без devcommands они недоступны.
    /// Команды для подбора «тела» NPC (ls_prefabs, ls_bodyinfo, ls_trybody) нужны потому, что
    /// набор анимаций у разных ванильных существ разный и проверять его надо в игре.
    /// </summary>
    internal static class DebugCommands
    {
        private static bool _registered;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;

            Add("ls_help", "List all Living Survivors commands", CmdHelp);
            Add("ls_spawn", "ls_spawn [count 1-10] [tier 0-3] - spawn survivors in front of you", CmdSpawn);
            Add("ls_list", "List loaded survivors", CmdList);
            Add("ls_info", "Details of the nearest survivor (profile, skills, items, history)", CmdInfo);
            Add("ls_give", "ls_give <itemPrefab> [amount] - give an item to the nearest survivor and equip it", CmdGive);
            Add("ls_kit", "ls_kit [tier 0-3] - replace the nearest survivor's gear with a fresh kit", CmdKit);
            Add("ls_follow", "Nearest survivor follows you", CmdFollow);
            Add("ls_wait", "Nearest survivor stays here", CmdWait);
            Add("ls_tp", "Bring the nearest survivor next to you", CmdTeleport);
            Add("ls_kill", "Kill the nearest survivor (test of death and item drop)", CmdKill);
            Add("ls_remove", "ls_remove [all] - delete the nearest (or all) survivors", CmdRemove);
            Add("ls_prefabs", "ls_prefabs [filter] - humanoid prefabs usable as Npc.BasePrefab", CmdPrefabs);
            Add("ls_bodyinfo", "ls_bodyinfo [prefab] - components, animator triggers and default gear of a body", CmdBodyInfo);
            Add("ls_trybody", "ls_trybody <prefab> - rebuild the survivor prefab from another body (new spawns only)", CmdTryBody);
            Add("ls_checkitems", "Check which item prefab names used by the loadout planner exist in this game version", CmdCheckItems);
            Add("ls_loglevel", "ls_loglevel <0|1|2> - change the log verbosity", CmdLogLevel);

            Log.Info("Console commands registered (type ls_help; run devcommands first).");
        }

        private static void Add(string name, string description, Terminal.ConsoleEvent action)
        {
            try
            {
                new Terminal.ConsoleCommand(name, description, action, isCheat: true);
            }
            catch (Exception e)
            {
                Log.Error("Cannot register console command " + name + ": " + e.Message);
            }
        }

        // ------------------------------------------------------------------ вспомогательное

        private static void Say(Terminal.ConsoleEventArgs args, string message)
        {
            if (args != null && args.Context != null) args.Context.AddString(message);
            Log.Info(message);
        }

        private static int ArgInt(Terminal.ConsoleEventArgs args, int index, int fallback, int min, int max)
        {
            int value;
            if (args.Args.Length > index && int.TryParse(args.Args[index], out value))
            {
                return Math.Max(min, Math.Min(max, value));
            }
            return fallback;
        }

        private static Player RequirePlayer(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null) Say(args, "No local player: load a world first.");
            return player;
        }

        private static SurvivorBrain RequireNearest(Terminal.ConsoleEventArgs args, out Player player)
        {
            player = RequirePlayer(args);
            if (player == null) return null;

            SurvivorBrain brain = SurvivorRegistry.Nearest(player.transform.position, 60f);
            if (brain == null) Say(args, "No survivor within 60 m. Use ls_spawn first.");
            return brain;
        }

        // ------------------------------------------------------------------ команды

        private static void CmdHelp(Terminal.ConsoleEventArgs args)
        {
            Say(args, "Living Survivors: ls_spawn, ls_list, ls_info, ls_give, ls_kit, ls_follow, ls_wait, ls_tp, ls_kill, ls_remove,");
            Say(args, "body tests: ls_prefabs, ls_bodyinfo, ls_trybody; checks: ls_checkitems, ls_loglevel.");
            Say(args, "Give gear by hand: look at a survivor, hold Shift and press the hotbar key of the item.");
        }

        private static void CmdSpawn(Terminal.ConsoleEventArgs args)
        {
            Player player = RequirePlayer(args);
            if (player == null) return;

            int count = ArgInt(args, 1, 1, 1, 10);
            int tier = ArgInt(args, 2, ModConfig.DefaultTier.Value, 0, LoadoutPlanner.MaxTier);

            // Только что созданные NPC попадут в реестр чуть позже (в Start), поэтому лимит считаем заранее.
            int room = Math.Max(0, ModConfig.MaxNpcCount.Value - SurvivorRegistry.Count);
            if (count > room)
            {
                Say(args, "Limit Npc.MaxNpcCount = " + ModConfig.MaxNpcCount.Value + ": only " + room + " more can be spawned.");
                count = room;
            }

            int created = 0;
            for (int i = 0; i < count; i++)
            {
                if (SurvivorSpawner.Spawn(player, i, tier) == null) break;
                created++;
            }

            // Личность создаётся в Start мозга (чуть позже в этом кадре), поэтому имена — в ls_list / журнале.
            Say(args, "Spawned " + created + " of " + count + " survivor(s), gear tier " + tier + ". Names: ls_list.");
        }

        private static void CmdList(Terminal.ConsoleEventArgs args)
        {
            List<SurvivorBrain> all = SurvivorRegistry.Snapshot();
            if (all.Count == 0)
            {
                Say(args, "No survivors are loaded.");
                return;
            }

            Player player = Player.m_localPlayer;
            Vector3 here = player != null ? player.transform.position : Vector3.zero;
            foreach (SurvivorBrain brain in all)
            {
                float hp = brain.CharacterRef != null ? brain.CharacterRef.GetHealth() : 0f;
                Say(args, string.Format("{0} | {1:0} m | HP {2:0} | {3}",
                    brain.Describe(), Vector3.Distance(brain.transform.position, here), hp,
                    brain.IsFollowing ? "following" : "waiting"));
            }
        }

        private static void CmdInfo(Terminal.ConsoleEventArgs args)
        {
            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null) return;

            foreach (string line in brain.DescribeFull()) Say(args, line);
        }

        private static void CmdGive(Terminal.ConsoleEventArgs args)
        {
            if (args.Args.Length < 2)
            {
                Say(args, "Usage: ls_give <itemPrefab> [amount], e.g. ls_give SwordBronze");
                return;
            }

            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null) return;

            string error;
            int amount = ArgInt(args, 2, 1, 1, 999);
            if (brain.GiveItem(args.Args[1], amount, true, out error))
            {
                Say(args, brain.DisplayName + " received " + args.Args[1] + (amount > 1 ? " x" + amount : ""));
            }
            else
            {
                Say(args, "Cannot give: " + error);
            }
        }

        private static void CmdKit(Terminal.ConsoleEventArgs args)
        {
            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null) return;

            int tier = ArgInt(args, 1, ModConfig.DefaultTier.Value, 0, LoadoutPlanner.MaxTier);
            Say(args, brain.DisplayName + " (tier " + tier + "): " + brain.ResetKit(tier));
        }

        private static void CmdFollow(Terminal.ConsoleEventArgs args)
        {
            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null) return;

            brain.SetFollow(player, true);
            Say(args, brain.DisplayName + ": following = " + brain.IsFollowing);
        }

        private static void CmdWait(Terminal.ConsoleEventArgs args)
        {
            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null) return;

            brain.SetFollow(player, false);
            Say(args, brain.DisplayName + ": following = " + brain.IsFollowing);
        }

        private static void CmdTeleport(Terminal.ConsoleEventArgs args)
        {
            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null) return;

            bool ok = brain.TryTeleportNear(player.transform.position);
            Say(args, ok ? brain.DisplayName + " moved next to you." : "No safe spot found next to you.");
        }

        private static void CmdKill(Terminal.ConsoleEventArgs args)
        {
            Player player;
            SurvivorBrain brain = RequireNearest(args, out player);
            if (brain == null || brain.CharacterRef == null) return;

            HitData hit = new HitData();
            hit.m_damage.m_damage = 100000f;
            hit.m_point = brain.CharacterRef.GetCenterPoint();
            brain.CharacterRef.Damage(hit);
            Say(args, brain.DisplayName + " was hit with lethal damage.");
        }

        private static void CmdRemove(Terminal.ConsoleEventArgs args)
        {
            Player player = RequirePlayer(args);
            if (player == null) return;

            bool all = args.Args.Length > 1 && args.Args[1] == "all";
            List<SurvivorBrain> targets = new List<SurvivorBrain>();
            if (all)
            {
                targets.AddRange(SurvivorRegistry.Snapshot());
            }
            else
            {
                SurvivorBrain nearest = SurvivorRegistry.Nearest(player.transform.position, 60f);
                if (nearest != null) targets.Add(nearest);
            }

            int removed = 0;
            foreach (SurvivorBrain brain in targets)
            {
                ZNetView nview = brain.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;
                nview.Destroy();
                removed++;
            }
            Say(args, "Removed " + removed + " survivor(s).");
        }

        private static void CmdPrefabs(Terminal.ConsoleEventArgs args)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
            {
                Say(args, "World is not loaded.");
                return;
            }

            string filter = args.Args.Length > 1 ? args.Args[1].ToLowerInvariant() : "";
            List<string> names = new List<string>();
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab == null || prefab.GetComponent<Humanoid>() == null) continue;
                if (filter.Length > 0 && prefab.name.ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0) continue;
                names.Add(prefab.name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            Say(args, names.Count + " humanoid prefab(s)" + (filter.Length > 0 ? " matching '" + filter + "'" : "") + ":");
            Say(args, string.Join(", ", names.ToArray()));
        }

        private static void CmdBodyInfo(Terminal.ConsoleEventArgs args)
        {
            GameObject go;
            string label;
            if (args.Args.Length > 1)
            {
                go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(args.Args[1]) : null;
                label = args.Args[1];
            }
            else
            {
                go = SurvivorPrefab.Current;
                label = SurvivorPrefab.PrefabName + " (body: " + SurvivorPrefab.BaseName + ")";
            }

            if (go == null)
            {
                Say(args, "Prefab not found. Use ls_prefabs to list humanoids.");
                return;
            }

            Say(args, "== " + label + " ==");

            StringBuilder components = new StringBuilder();
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                if (components.Length > 0) components.Append(", ");
                components.Append(component.GetType().Name);
            }
            Say(args, "Components: " + components);

            Character character = go.GetComponent<Character>();
            if (character != null) Say(args, "Faction: " + character.m_faction + ", health: " + character.m_health);

            Humanoid humanoid = go.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                Say(args, "Default items: " + JoinOrNone(Reflect.NamesOf(Reflect.GetFieldValue(humanoid, "m_defaultItems") as Array)));
                Say(args, "Random weapons: " + JoinOrNone(Reflect.NamesOf(Reflect.GetFieldValue(humanoid, "m_randomWeapon") as Array)));
            }

            Say(args, "VisEquipment (visible gear): " + (go.GetComponentInChildren<VisEquipment>(true) != null ? "yes" : "NO"));

            foreach (Animator animator in go.GetComponentsInChildren<Animator>(true))
            {
                string controller = animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "(none)";
                Say(args, "Animator '" + animator.gameObject.name + "', controller: " + controller);

                try
                {
                    List<string> triggers = new List<string>();
                    List<string> others = new List<string>();
                    foreach (AnimatorControllerParameter parameter in animator.parameters)
                    {
                        if (parameter.type == AnimatorControllerParameterType.Trigger) triggers.Add(parameter.name);
                        else others.Add(parameter.name);
                    }
                    Say(args, "  triggers (attack animations live here): " + JoinOrNone(triggers));
                    Say(args, "  other parameters: " + JoinOrNone(others));
                }
                catch (Exception e)
                {
                    Say(args, "  parameters unavailable: " + e.Message);
                }
            }
        }

        private static void CmdTryBody(Terminal.ConsoleEventArgs args)
        {
            if (args.Args.Length < 2)
            {
                Say(args, "Usage: ls_trybody <prefab>, e.g. ls_trybody Draugr (see ls_prefabs)");
                return;
            }

            bool ok = SurvivorPrefab.Register(ZNetScene.instance, args.Args[1]);
            Say(args, ok
                ? "Body switched to " + args.Args[1] + ". Survivors spawned from now on use it (already spawned ones keep their body). "
                  + "To make it permanent set Npc.BasePrefab in the config."
                : "Could not switch the body; see the log.");
        }

        private static void CmdCheckItems(Terminal.ConsoleEventArgs args)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                Say(args, "ObjectDB is not ready: load a world first.");
                return;
            }

            List<string> names = LoadoutPlanner.AllItemNames();
            List<string> missing = new List<string>();
            foreach (string name in names)
            {
                if (db.GetItemPrefab(name) == null) missing.Add(name);
            }

            Say(args, names.Count + " item names checked, " + missing.Count + " missing"
                + (missing.Count > 0 ? ": " + string.Join(", ", missing.ToArray()) : "."));
            if (missing.Count > 0)
            {
                Say(args, "Missing names are skipped when gear is issued; fix them in Core/LoadoutPlanner.cs.");
            }
        }

        private static void CmdLogLevel(Terminal.ConsoleEventArgs args)
        {
            int level = ArgInt(args, 1, Log.Level, 0, 2);
            Log.Level = level;
            ModConfig.LogLevel.Value = level;
            Say(args, "Log level = " + level);
        }

        private static string JoinOrNone(List<string> items)
        {
            return items.Count > 0 ? string.Join(", ", items.ToArray()) : "(none)";
        }
    }
}
