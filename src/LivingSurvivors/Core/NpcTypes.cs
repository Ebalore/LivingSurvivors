namespace LivingSurvivors.Core
{
    /// <summary>
    /// Черты характера (ТЗ 3.3). Не просто текст: каждая черта влияет на поведение
    /// через <see cref="BehaviorTuning"/> и на стартовые навыки через генератор.
    /// Значения хранятся в сохранении как byte — порядок не менять, новые добавлять в конец.
    /// </summary>
    public enum NpcTrait : byte
    {
        Cautious = 0,
        Brave,
        Hardworking,
        Greedy,
        Sociable,
        Independent,
        Aggressive,
        Caring,
        Curious
    }

    /// <summary>
    /// Навыки NPC (ТЗ 3.3, раздел 6). Хранятся в сохранении по индексу — порядок не менять.
    /// </summary>
    public enum NpcSkill : byte
    {
        Woodcutting = 0,
        Mining,
        Hunting,
        Fishing,
        Farming,
        Foraging,
        Smithing,
        Cooking,
        Building,
        Scouting,
        Melee,
        Archery
    }

    /// <summary>Предпочитаемый стиль боя / тип оружия (ТЗ 3.3).</summary>
    public enum WeaponStyle : byte
    {
        Sword = 0,
        Axe,
        Mace,
        Spear,
        Knife,
        Bow
    }

    /// <summary>
    /// Запись в биографии. <see cref="Text"/> — ключ события (например "born", "died"),
    /// а не готовая фраза: локализация происходит при показе.
    /// </summary>
    public struct HistoryEntry
    {
        public int Day;
        public string Text;

        public HistoryEntry(int day, string text)
        {
            Day = day;
            Text = text;
        }
    }
}
