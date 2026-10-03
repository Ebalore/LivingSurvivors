using LivingSurvivors.Settings;
using LivingSurvivors.Util;
using UnityEngine;

namespace LivingSurvivors.Npc
{
    /// <summary>
    /// Создаёт выживших. На Этапе 1 вызывается консольной командой ls_spawn;
    /// на Этапах 6–7 этим же кодом будет пользоваться менеджер групп (появление при создании мира).
    /// </summary>
    internal static class SurvivorSpawner
    {
        /// <summary>
        /// Создаёт одного выжившего рядом с игроком. index разводит несколько NPC по сторонам.
        /// Возвращает null и пишет причину в журнал, если создать не удалось.
        /// </summary>
        internal static SurvivorBrain Spawn(Player player, int index, int tier)
        {
            if (player == null) return null;

            if (SurvivorRegistry.Count >= ModConfig.MaxNpcCount.Value)
            {
                Log.Warn("Survivor limit reached (Npc.MaxNpcCount = " + ModConfig.MaxNpcCount.Value + ").");
                return null;
            }

            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(SurvivorPrefab.PrefabName) : null;
            if (prefab == null)
            {
                Log.Error("Prefab '" + SurvivorPrefab.PrefabName + "' is not registered (see the log for registration errors).");
                return null;
            }

            Vector3 position = FindSpawnPoint(player, index);
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);

            // Параметры, которые мозг прочитает в Start (он выполнится позже в этом же кадре).
            ZNetView nview = instance.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                nview.GetZDO().Set(SurvivorBrain.KeyTier, tier);
            }

            // Приручаем: это включает ванильную логику «питомца» — следование, защиту, сохранение команд.
            MonsterAI ai = instance.GetComponent<MonsterAI>();
            if (ai != null) ai.MakeTame();

            SurvivorBrain brain = instance.GetComponent<SurvivorBrain>();
            if (brain != null && ModConfig.AutoFollowOnSpawn.Value)
            {
                brain.SetFollow(player, true);
            }
            return brain;
        }

        /// <summary>Точка перед игроком, на земле; несколько NPC разводятся влево и вправо.</summary>
        private static Vector3 FindSpawnPoint(Player player, int index)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float side = (index % 2 == 0 ? 1f : -1f) * (1.5f + 1.2f * (index / 2));
            Vector3 wanted = player.transform.position + forward * 3.5f + right * side;

            Vector3 ground;
            if (GroundUtil.TryGetGround(wanted, 6f, 20f, out ground))
            {
                return ground + Vector3.up * 0.1f;
            }
            return wanted + Vector3.up * 0.5f;
        }
    }
}
