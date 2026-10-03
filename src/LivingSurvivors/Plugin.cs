using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using LivingSurvivors.Patches;
using LivingSurvivors.Settings;
using LivingSurvivors.Util;

namespace LivingSurvivors
{
    /// <summary>
    /// Living Survivors — автономные NPC-выжившие для Valheim (Этап 1: прототип NPC).
    /// Зависит только от BepInEx и Harmony. Jötunn на этом этапе не используется:
    /// в Valheim 1.0 он потребовал обновления из-за изменения сигнатуры Terminal.ConsoleCommand,
    /// а всё нужное Этапу 1 делается штатным API игры. Подключим его на Этапе 8 (сеть и синхронизация).
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.livingsurvivors.valheim";
        public const string PluginName = "Living Survivors";
        public const string PluginVersion = "0.1.0";

        private Harmony _harmony;

        private void Awake()
        {
            Log.Init(Logger);
            ModConfig.Bind(Config);

            if (!ModConfig.Enabled.Value)
            {
                Log.Info(PluginName + " is disabled in the config (General.Enabled = false).");
                return;
            }

            _harmony = new Harmony(PluginGuid);
            PatchRunner.ApplyAll(_harmony);

            Log.Info(PluginName + " " + PluginVersion + " loaded. Game version: " + GameVersion()
                + ". Open the console (F5), type devcommands, then ls_help.");
        }

        /// <summary>Версия игры для журнала — первое, что нужно знать при разборе проблем после обновления.</summary>
        private static string GameVersion()
        {
            try
            {
                Type type = Type.GetType("Version, assembly_valheim");
                MethodInfo method = type != null
                    ? type.GetMethod("GetVersionString", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)
                    : null;
                object value = method != null ? method.Invoke(null, null) : null;
                return value != null ? value.ToString() : "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}
