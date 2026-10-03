using System.Collections.Generic;
using UnityEngine;

namespace LivingSurvivors.Npc
{
    /// <summary>
    /// Реестр загруженных (существующих как объекты) выживших.
    /// Защищает от дубликатов по стабильному ID (ТЗ 16): если в мире оказались два объекта
    /// с одним ID (откат сохранения, сбой сети), лишний будет удалён владельцем.
    /// Знает только о загруженных NPC — NPC в выгруженных зонах появятся здесь при загрузке зоны.
    /// </summary>
    internal static class SurvivorRegistry
    {
        private static readonly Dictionary<string, SurvivorBrain> ById = new Dictionary<string, SurvivorBrain>();
        private static readonly List<SurvivorBrain> All = new List<SurvivorBrain>();

        internal static int Count
        {
            get
            {
                Prune();
                return All.Count;
            }
        }

        /// <summary>Снимок списка (безопасно изменять реестр во время обхода).</summary>
        internal static List<SurvivorBrain> Snapshot()
        {
            Prune();
            return new List<SurvivorBrain>(All);
        }

        /// <summary>Регистрирует NPC. Возвращает false, если живой объект с таким ID уже есть.</summary>
        internal static bool Register(SurvivorBrain brain)
        {
            if (brain == null || brain.Profile == null) return false;
            Prune();

            string id = brain.Profile.Id;
            SurvivorBrain existing;
            if (ById.TryGetValue(id, out existing) && existing != null && existing != brain)
            {
                return false;
            }

            ById[id] = brain;
            if (!All.Contains(brain)) All.Add(brain);
            return true;
        }

        internal static void Unregister(SurvivorBrain brain)
        {
            if (brain == null) return;

            All.Remove(brain);
            if (brain.Profile != null)
            {
                SurvivorBrain existing;
                if (ById.TryGetValue(brain.Profile.Id, out existing) && existing == brain)
                {
                    ById.Remove(brain.Profile.Id);
                }
            }
        }

        /// <summary>Имена загруженных NPC — чтобы новые не повторяли их.</summary>
        internal static HashSet<string> TakenNames()
        {
            Prune();
            var names = new HashSet<string>();
            foreach (SurvivorBrain brain in All)
            {
                if (brain.Profile != null) names.Add(brain.Profile.Name);
            }
            return names;
        }

        internal static SurvivorBrain Nearest(Vector3 position, float maxDistance)
        {
            Prune();
            SurvivorBrain best = null;
            float bestDistance = maxDistance;
            foreach (SurvivorBrain brain in All)
            {
                float distance = Vector3.Distance(brain.transform.position, position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = brain;
                }
            }
            return best;
        }

        /// <summary>Убирает уничтоженные Unity-объекты (сравнение с null у MonoBehaviour перегружено).</summary>
        private static void Prune()
        {
            All.RemoveAll(brain => brain == null);

            List<string> dead = null;
            foreach (KeyValuePair<string, SurvivorBrain> pair in ById)
            {
                if (pair.Value == null)
                {
                    if (dead == null) dead = new List<string>();
                    dead.Add(pair.Key);
                }
            }
            if (dead != null)
            {
                foreach (string id in dead) ById.Remove(id);
            }
        }
    }
}
