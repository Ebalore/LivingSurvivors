using System;
using HarmonyLib;
using LivingSurvivors.DevTools;
using LivingSurvivors.Npc;
using LivingSurvivors.Settings;
using LivingSurvivors.Util;

namespace LivingSurvivors.Patches
{
    /// <summary>
    /// Применяет патчи по одному. Сбой одного патча (например, после обновления игры изменилась
    /// сигнатура метода) отключает только его функцию и пишется в журнал — мод продолжает работать.
    /// Патчи нарочно ссылаются на методы строками и на аргументы по индексам (__0, __1),
    /// чтобы не зависеть от неподтверждённых имён.
    /// </summary>
    internal static class PatchRunner
    {
        private static readonly Type[] Patches =
        {
            typeof(Patch_ZNetScene_Awake),
            typeof(Patch_Terminal_InitTerminal),
            typeof(Patch_Tameable_GetHoverText),
            typeof(Patch_Tameable_UseItem)
        };

        internal static void ApplyAll(Harmony harmony)
        {
            foreach (Type patch in Patches)
            {
                try
                {
                    harmony.PatchAll(patch);
                    Log.Debug("Patch applied: " + patch.Name);
                }
                catch (Exception e)
                {
                    Log.Error("Patch " + patch.Name + " failed, its feature is disabled: " + e.Message);
                }
            }
        }
    }

    /// <summary>Мир загружен: регистрируем префаб выжившего и игровые токены локализации.</summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class Patch_ZNetScene_Awake
    {
        private static void Postfix(ZNetScene __instance)
        {
            try
            {
                Loc.ApplyToGame();
                SurvivorPrefab.RegisterConfigured(__instance);
            }
            catch (Exception e)
            {
                Log.Error("ZNetScene.Awake postfix failed: " + e);
            }
        }
    }

    /// <summary>Консоль инициализирована: добавляем команды ls_*.</summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class Patch_Terminal_InitTerminal
    {
        private static void Postfix()
        {
            DebugCommands.Register();
        }
    }

    /// <summary>Добавляет к подсказке над NPC его характер и стиль боя.</summary>
    [HarmonyPatch(typeof(Tameable), "GetHoverText")]
    internal static class Patch_Tameable_GetHoverText
    {
        private static void Postfix(Tameable __instance, ref string __result)
        {
            try
            {
                if (!ModConfig.ShowProfileInHover.Value || string.IsNullOrEmpty(__result)) return;

                SurvivorBrain brain = __instance.GetComponent<SurvivorBrain>();
                if (brain == null || brain.Profile == null) return;

                __result += "\n<color=#B8B8B8>" + brain.DescribeShort() + "</color>";
            }
            catch (Exception)
            {
                // Подсказка — не критичная функция: при любой ошибке оставляем ванильный текст.
            }
        }
    }

    /// <summary>
    /// «Отдать предмет NPC»: Shift + клавиша слота с экипировкой или стрелами, глядя на NPC.
    /// Без Shift ничего не перехватывается — иначе обычная смена оружия клавишами хотбара
    /// отдавала бы меч игрока, когда он смотрит на NPC.
    /// </summary>
    [HarmonyPatch(typeof(Tameable), "UseItem")]
    internal static class Patch_Tameable_UseItem
    {
        // __0 — Humanoid user, __1 — ItemDrop.ItemData item (по индексам аргументов оригинала).
        private static bool Prefix(Tameable __instance, Humanoid __0, ItemDrop.ItemData __1, ref bool __result)
        {
            try
            {
                if (!ModConfig.AllowGiveItems.Value) return true;
                if (!ZInput.GetButton("AltPlace")) return true;

                SurvivorBrain brain = __instance.GetComponent<SurvivorBrain>();
                if (brain == null) return true;

                if (brain.TryReceiveItem(__0, __1))
                {
                    __result = true;
                    return false; // ванильную обработку («кормление») пропускаем
                }
            }
            catch (Exception e)
            {
                Log.Error("Give-item handler failed: " + e.Message);
            }
            return true;
        }
    }
}
