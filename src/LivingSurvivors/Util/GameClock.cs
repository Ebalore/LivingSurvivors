namespace LivingSurvivors.Util
{
    /// <summary>Игровое время мира (для биографии NPC и будущих расписаний).</summary>
    internal static class GameClock
    {
        /// <summary>Длина игровых суток в секундах (ванильное значение).</summary>
        private const double DayLengthSeconds = 1800.0;

        /// <summary>Номер текущего игрового дня; 0, если мир ещё не загружен.</summary>
        internal static int Day
        {
            get
            {
                ZNet net = ZNet.instance;
                return net != null ? (int)(net.GetTimeSeconds() / DayLengthSeconds) : 0;
            }
        }
    }
}
