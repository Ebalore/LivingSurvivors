using System;
using System.Collections.Generic;

namespace LivingSurvivors.Core
{
    /// <summary>
    /// Личность одного выжившего (ТЗ 3.3): стабильный ID, имя, характер, навыки,
    /// стиль боя, биография. Чистый C# без зависимостей от Unity/Valheim —
    /// поэтому логику можно тестировать без запуска игры.
    /// </summary>
    public sealed class NpcProfile
    {
        public const int MaxHistory = 24;
        public const int MaxHistoryTextLength = 120;

        public static readonly int SkillCount = Enum.GetValues(typeof(NpcSkill)).Length;

        /// <summary>Стабильный идентификатор (ТЗ 16). Не меняется всю жизнь NPC.</summary>
        public string Id = "";

        public string Name = "";
        public int Seed;
        public WeaponStyle Style;

        /// <summary>Игровой день, когда NPC появился в мире.</summary>
        public int BornDay;

        public readonly List<NpcTrait> Traits = new List<NpcTrait>();

        /// <summary>Уровни навыков 0..100, индекс = (int)NpcSkill.</summary>
        public readonly byte[] Skills = new byte[SkillCount];

        public readonly List<HistoryEntry> History = new List<HistoryEntry>();

        public bool Has(NpcTrait trait)
        {
            return Traits.Contains(trait);
        }

        public int GetSkill(NpcSkill skill)
        {
            return Skills[(int)skill];
        }

        public void SetSkill(NpcSkill skill, int value)
        {
            Skills[(int)skill] = (byte)Math.Max(0, Math.Min(100, value));
        }

        /// <summary>Добавляет событие в биографию; хранится не более <see cref="MaxHistory"/> последних.</summary>
        public void AddEvent(int day, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > MaxHistoryTextLength) text = text.Substring(0, MaxHistoryTextLength);

            History.Add(new HistoryEntry(day, text));
            if (History.Count > MaxHistory)
            {
                History.RemoveRange(0, History.Count - MaxHistory);
            }
        }
    }
}
