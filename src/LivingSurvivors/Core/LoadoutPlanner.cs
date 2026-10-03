using System;
using System.Collections.Generic;

namespace LivingSurvivors.Core
{
    /// <summary>
    /// Результат планирования: имена префабов предметов (не сами предметы).
    /// Превращение имён в реальные предметы игры делает слой Npc/LoadoutService —
    /// он же пропускает несуществующие имена и оружие, анимации которого тело NPC не умеет.
    /// </summary>
    public sealed class LoadoutPlan
    {
        /// <summary>Кандидаты в оружие по убыванию предпочтения.</summary>
        public readonly List<string> WeaponCandidates = new List<string>();

        /// <summary>Щит (null — не нужен, например для лучника).</summary>
        public string Shield;

        public readonly List<string> Armor = new List<string>();

        /// <summary>Боеприпасы для лука (null — не нужны).</summary>
        public string Ammo;

        public int AmmoCount;
    }

    /// <summary>
    /// Подбор снаряжения по стилю боя и ярусу прогрессии (ТЗ 8: «выбирать экипировку
    /// под биом и тип угрозы»). Используются только реальные предметы Valheim.
    /// Имена префабов — по памяти о ванильных данных; сверка с игрой делается командой
    /// ls_checkitems, а неизвестное имя при выдаче просто пропускается с записью в лог.
    /// </summary>
    public static class LoadoutPlanner
    {
        /// <summary>Ярус: 0 Луга, 1 Чёрный лес, 2 Болото, 3 Горы. Остальные ярусы — в следующих этапах.</summary>
        public const int MaxTier = 3;

        public const int DefaultAmmoCount = 40;

        private static readonly string[] Swords = { null, "SwordBronze", "SwordIron", "SwordSilver" };
        private static readonly string[] Axes = { "AxeFlint", "AxeBronze", "AxeIron", "AxeIron" };
        private static readonly string[] Maces = { "Club", "MaceBronze", "MaceIron", "MaceSilver" };
        private static readonly string[] Spears = { "SpearFlint", "SpearBronze", "SpearElderbark", "SpearWolfFang" };
        private static readonly string[] Knives = { "KnifeFlint", "KnifeCopper", "KnifeCopper", "KnifeSilver" };
        private static readonly string[] Bows = { "Bow", "Bow", "Bow", "BowDraugrFang" };
        private static readonly string[] Shields = { "ShieldWood", "ShieldBronzeBuckler", "ShieldIronSquare", "ShieldSilver" };
        private static readonly string[] Ammo = { "ArrowWood", "ArrowBronze", "ArrowIron", "ArrowSilver" };

        private static readonly string[][] ArmorSets =
        {
            new[] { "ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather" },
            new[] { "ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze" },
            new[] { "ArmorIronChest", "ArmorIronLegs", "HelmetIron" },
            new[] { "ArmorWolfChest", "ArmorWolfLegs", "HelmetDrake" }
        };

        public static LoadoutPlan Plan(NpcProfile profile, int tier)
        {
            tier = Math.Max(0, Math.Min(MaxTier, tier));
            WeaponStyle style = profile != null ? profile.Style : WeaponStyle.Mace;

            var plan = new LoadoutPlan();

            AddCandidate(plan.WeaponCandidates, PickForTier(TableFor(style), tier));
            AddCandidate(plan.WeaponCandidates, PickForTier(TableFor(Alternative(style)), tier));
            AddCandidate(plan.WeaponCandidates, "Club");

            if (style != WeaponStyle.Bow)
            {
                plan.Shield = Shields[tier];
            }

            plan.Armor.AddRange(ArmorSets[tier]);

            if (style == WeaponStyle.Bow)
            {
                plan.Ammo = Ammo[tier];
                plan.AmmoCount = DefaultAmmoCount;
            }

            return plan;
        }

        /// <summary>Все имена предметов, которые планировщик может выдать (для проверки по базе игры).</summary>
        public static List<string> AllItemNames()
        {
            var names = new List<string>();
            var tables = new[] { Swords, Axes, Maces, Spears, Knives, Bows, Shields, Ammo };
            foreach (string[] table in tables)
            {
                foreach (string name in table) AddCandidate(names, name);
            }
            foreach (string[] set in ArmorSets)
            {
                foreach (string name in set) AddCandidate(names, name);
            }
            return names;
        }

        private static string[] TableFor(WeaponStyle style)
        {
            switch (style)
            {
                case WeaponStyle.Sword: return Swords;
                case WeaponStyle.Axe: return Axes;
                case WeaponStyle.Mace: return Maces;
                case WeaponStyle.Spear: return Spears;
                case WeaponStyle.Knife: return Knives;
                case WeaponStyle.Bow: return Bows;
                default: return Maces;
            }
        }

        /// <summary>Запасной стиль на случай, если основное оружие тело NPC анимировать не умеет.</summary>
        private static WeaponStyle Alternative(WeaponStyle style)
        {
            switch (style)
            {
                case WeaponStyle.Sword: return WeaponStyle.Axe;
                case WeaponStyle.Axe: return WeaponStyle.Sword;
                case WeaponStyle.Mace: return WeaponStyle.Axe;
                case WeaponStyle.Spear: return WeaponStyle.Mace;
                case WeaponStyle.Knife: return WeaponStyle.Sword;
                default: return WeaponStyle.Axe;
            }
        }

        /// <summary>Лучший предмет не выше указанного яруса (если на этом ярусе нет — берём ниже).</summary>
        private static string PickForTier(string[] table, int tier)
        {
            for (int t = Math.Min(tier, table.Length - 1); t >= 0; t--)
            {
                if (table[t] != null) return table[t];
            }
            return null;
        }

        private static void AddCandidate(List<string> list, string name)
        {
            if (!string.IsNullOrEmpty(name) && !list.Contains(name)) list.Add(name);
        }
    }
}
