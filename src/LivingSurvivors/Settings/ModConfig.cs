using BepInEx.Configuration;
using LivingSurvivors.Util;

namespace LivingSurvivors.Settings
{
    /// <summary>
    /// Настройки мода (ТЗ раздел 20). Файл: BepInEx/config/com.livingsurvivors.valheim.cfg.
    /// На Этапе 1 присутствует только то, что реально используется; остальные настройки
    /// из ТЗ добавляются вместе с соответствующими системами.
    /// </summary>
    internal static class ModConfig
    {
        // General
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> LogLevel;

        // Npc
        internal static ConfigEntry<string> BasePrefab;
        internal static ConfigEntry<float> BaseHealth;
        internal static ConfigEntry<int> DefaultTier;
        internal static ConfigEntry<int> MaxNpcCount;
        internal static ConfigEntry<bool> DropInventoryOnDeath;
        internal static ConfigEntry<bool> AllowGiveItems;
        internal static ConfigEntry<float> PassiveRegenPercentPer10s;
        internal static ConfigEntry<bool> ShowProfileInHover;

        // Companion
        internal static ConfigEntry<bool> AutoFollowOnSpawn;
        internal static ConfigEntry<bool> EnableCatchUpTeleport;
        internal static ConfigEntry<float> CatchUpTeleportDistance;
        internal static ConfigEntry<float> StuckTeleportSeconds;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Master switch of the whole mod. / Главный выключатель мода.");

            LogLevel = config.Bind("General", "LogLevel", 1,
                new ConfigDescription(
                    "0 = errors only, 1 = info, 2 = debug. / Подробность журнала.",
                    new AcceptableValueRange<int>(0, 2)));
            Log.Level = LogLevel.Value;
            LogLevel.SettingChanged += (sender, args) => Log.Level = LogLevel.Value;

            BasePrefab = config.Bind("Npc", "BasePrefab", "Dverger",
                "Vanilla humanoid prefab used as the NPC body (restart required; try ls_trybody in game). "
                + "/ Ванильный гуманоид, из которого делается тело NPC (нужен перезапуск; в игре можно проверить через ls_trybody).");

            BaseHealth = config.Bind("Npc", "BaseHealth", 100f,
                new ConfigDescription(
                    "Max health of a survivor. / Максимальное здоровье выжившего.",
                    new AcceptableValueRange<float>(10f, 2000f)));

            DefaultTier = config.Bind("Npc", "DefaultTier", 0,
                new ConfigDescription(
                    "Starting gear tier: 0 Meadows, 1 Black Forest, 2 Swamp, 3 Mountains. / Ярус стартового снаряжения.",
                    new AcceptableValueRange<int>(0, 3)));

            MaxNpcCount = config.Bind("Npc", "MaxNpcCount", 12,
                new ConfigDescription(
                    "Hard cap of simultaneously loaded survivors. / Максимум одновременно загруженных NPC.",
                    new AcceptableValueRange<int>(1, 100)));

            DropInventoryOnDeath = config.Bind("Npc", "DropInventoryOnDeath", true,
                "Drop all carried items where the survivor dies. / Выбрасывать вещи NPC при гибели.");

            AllowGiveItems = config.Bind("Npc", "AllowGiveItems", true,
                "Let the player hand gear to a survivor by using an item on them. / Разрешить выдавать экипировку, используя предмет на NPC.");

            PassiveRegenPercentPer10s = config.Bind("Npc", "PassiveRegenPercentPer10s", 2f,
                new ConfigDescription(
                    "Out-of-combat regeneration, percent of max health per 10 seconds (0 = off). / Регенерация вне боя, % здоровья за 10 секунд.",
                    new AcceptableValueRange<float>(0f, 20f)));

            ShowProfileInHover = config.Bind("Npc", "ShowProfileInHover", true,
                "Show traits and weapon style in the hover text. / Показывать характер и стиль боя в подсказке.");

            AutoFollowOnSpawn = config.Bind("Companion", "AutoFollowOnSpawn", true,
                "Spawned survivors start following the player. / Созданные командой NPC сразу следуют за игроком.");

            EnableCatchUpTeleport = config.Bind("Companion", "EnableCatchUpTeleport", true,
                "Rarely teleport a lagging or stuck follower next to the player. / Редко подтягивать отставшего или застрявшего NPC к игроку.");

            CatchUpTeleportDistance = config.Bind("Companion", "CatchUpTeleportDistance", 45f,
                new ConfigDescription(
                    "Distance to the player above which a follower is brought closer. / Дистанция до игрока, после которой NPC подтягивается.",
                    new AcceptableValueRange<float>(15f, 200f)));

            StuckTeleportSeconds = config.Bind("Companion", "StuckTeleportSeconds", 12f,
                new ConfigDescription(
                    "Seconds without progress before a stuck follower is brought closer. / Секунд без движения до коррекции позиции.",
                    new AcceptableValueRange<float>(5f, 60f)));
        }
    }
}
