using System.Collections.Generic;
using LivingSurvivors.Core;

namespace LivingSurvivors.Util
{
    /// <summary>
    /// Русская и английская локализации (ТЗ 18). Язык берётся из настроек игры.
    /// Игровые токены вида $ls_survivor регистрируются в системе локализации Valheim;
    /// остальные строки мод подставляет сам через <see cref="Get"/>.
    /// </summary>
    internal static class Loc
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "ls_survivor", "Survivor" },

            { "trait_cautious", "Cautious" },
            { "trait_brave", "Brave" },
            { "trait_hardworking", "Hardworking" },
            { "trait_greedy", "Greedy" },
            { "trait_sociable", "Sociable" },
            { "trait_independent", "Independent" },
            { "trait_aggressive", "Aggressive" },
            { "trait_caring", "Caring" },
            { "trait_curious", "Curious" },

            { "style_sword", "Sword" },
            { "style_axe", "Axe" },
            { "style_mace", "Mace" },
            { "style_spear", "Spear" },
            { "style_knife", "Knife" },
            { "style_bow", "Bow" },

            { "skill_woodcutting", "Woodcutting" },
            { "skill_mining", "Mining" },
            { "skill_hunting", "Hunting" },
            { "skill_fishing", "Fishing" },
            { "skill_farming", "Farming" },
            { "skill_foraging", "Foraging" },
            { "skill_smithing", "Smithing" },
            { "skill_cooking", "Cooking" },
            { "skill_building", "Building" },
            { "skill_scouting", "Scouting" },
            { "skill_melee", "Melee" },
            { "skill_archery", "Archery" },

            { "evt_born", "Appeared in the world" },
            { "evt_died", "Died" },

            { "msg_gift_taken", "{0} takes: {1}" },
            { "msg_inventory_full", "{0} has no room left" },
            { "msg_not_gear", "{0} is not interested in that" }
        };

        private static readonly Dictionary<string, string> Russian = new Dictionary<string, string>
        {
            { "ls_survivor", "Выживший" },

            { "trait_cautious", "Осторожный" },
            { "trait_brave", "Смелый" },
            { "trait_hardworking", "Трудолюбивый" },
            { "trait_greedy", "Жадный" },
            { "trait_sociable", "Общительный" },
            { "trait_independent", "Независимый" },
            { "trait_aggressive", "Агрессивный" },
            { "trait_caring", "Заботливый" },
            { "trait_curious", "Любопытный" },

            { "style_sword", "Меч" },
            { "style_axe", "Топор" },
            { "style_mace", "Булава" },
            { "style_spear", "Копьё" },
            { "style_knife", "Нож" },
            { "style_bow", "Лук" },

            { "skill_woodcutting", "Рубка леса" },
            { "skill_mining", "Добыча руды" },
            { "skill_hunting", "Охота" },
            { "skill_fishing", "Рыбалка" },
            { "skill_farming", "Фермерство" },
            { "skill_foraging", "Собирательство" },
            { "skill_smithing", "Кузнечное дело" },
            { "skill_cooking", "Готовка" },
            { "skill_building", "Строительство" },
            { "skill_scouting", "Разведка" },
            { "skill_melee", "Ближний бой" },
            { "skill_archery", "Стрельба" },

            { "evt_born", "Появился в мире" },
            { "evt_died", "Погиб" },

            { "msg_gift_taken", "{0} принимает: {1}" },
            { "msg_inventory_full", "У {0} нет места" },
            { "msg_not_gear", "{0} это не интересно" }
        };

        internal static bool IsRussian()
        {
            Localization localization = Localization.instance;
            if (localization == null) return false;

            string language = Reflect.InvokeNoArgs(localization, "GetSelectedLanguage") as string;
            return language == "Russian";
        }

        /// <summary>Строка на текущем языке; если ключа нет — сам ключ (видно, чего не хватает).</summary>
        internal static string Get(string key)
        {
            Dictionary<string, string> table = IsRussian() ? Russian : English;
            string value;
            if (table.TryGetValue(key, out value)) return value;
            return English.TryGetValue(key, out value) ? value : key;
        }

        internal static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        internal static string TraitName(NpcTrait trait)
        {
            return Get("trait_" + trait.ToString().ToLowerInvariant());
        }

        internal static string StyleName(WeaponStyle style)
        {
            return Get("style_" + style.ToString().ToLowerInvariant());
        }

        internal static string SkillName(NpcSkill skill)
        {
            return Get("skill_" + skill.ToString().ToLowerInvariant());
        }

        /// <summary>Текст записи биографии на текущем языке.</summary>
        internal static string EventText(HistoryEntry entry)
        {
            string key = "evt_" + entry.Text;
            Dictionary<string, string> table = IsRussian() ? Russian : English;
            return table.ContainsKey(key) ? Get(key) : entry.Text;
        }

        /// <summary>Регистрирует игровые токены ($ls_survivor и т.п.) в локализации Valheim.</summary>
        internal static void ApplyToGame()
        {
            Localization localization = Localization.instance;
            if (localization == null) return;

            localization.AddWord("ls_survivor", Get("ls_survivor"));
        }
    }
}
