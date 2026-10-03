using BepInEx.Logging;

namespace LivingSurvivors.Util
{
    /// <summary>
    /// Журнал мода (ТЗ 24.6: «конфигурация, логирование и отладочные команды»).
    /// Пишет в BepInEx/LogOutput.log и в консоль BepInEx.
    /// </summary>
    internal static class Log
    {
        private static ManualLogSource _source;

        /// <summary>0 — только ошибки и предупреждения, 1 — информация, 2 — подробная отладка.</summary>
        internal static int Level = 1;

        internal static void Init(ManualLogSource source)
        {
            _source = source;
        }

        internal static void Info(string message)
        {
            if (Level >= 1 && _source != null) _source.LogInfo(message);
        }

        /// <summary>Подробности пишутся как Info с пометкой: уровень Debug в BepInEx по умолчанию скрыт.</summary>
        internal static void Debug(string message)
        {
            if (Level >= 2 && _source != null) _source.LogInfo("[debug] " + message);
        }

        internal static void Warn(string message)
        {
            if (_source != null) _source.LogWarning(message);
        }

        internal static void Error(string message)
        {
            if (_source != null) _source.LogError(message);
        }
    }
}
