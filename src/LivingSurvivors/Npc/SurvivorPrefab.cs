using System;
using System.Collections;
using System.Collections.Generic;
using LivingSurvivors.Settings;
using LivingSurvivors.Util;
using UnityEngine;

namespace LivingSurvivors.Npc
{
    /// <summary>
    /// Создаёт префаб NPC: настроенный клон ванильного гуманоида (по умолчанию Dverger)
    /// и регистрирует его в ZNetScene, чтобы его можно было создавать и сохранять как обычное существо.
    /// Это сознательное решение ТЗ 4: «использовать совместимые с игрой гуманоидные модели и анимации,
    /// а не создавать отдельную систему скелетной анимации с нуля».
    ///
    /// Клон пересоздаётся при каждой загрузке мира (ZNetScene.Awake): так он не зависит от того,
    /// какие игровые ассеты были выгружены между сессиями.
    /// </summary>
    internal static class SurvivorPrefab
    {
        internal const string PrefabName = "LS_Survivor";

        /// <summary>Компоненты базового существа, которые выжившему не нужны (реплики и «раздражение» Двергров).</summary>
        private static readonly string[] StripComponentTypes = { "NpcTalk", "Aggravatable" };

        private static GameObject _container;
        private static GameObject _current;
        private static List<string> _fallbackWeapons = new List<string>();

        /// <summary>Текущий зарегистрированный префаб (клон), либо null.</summary>
        internal static GameObject Current
        {
            get { return _current; }
        }

        /// <summary>Имя ванильного префаба, из которого сделан клон.</summary>
        internal static string BaseName { get; private set; }

        /// <summary>«Родное» оружие базового существа — гарантия, что у NPC есть чем драться.</summary>
        internal static List<string> FallbackWeapons
        {
            get { return _fallbackWeapons; }
        }

        internal static bool RegisterConfigured(ZNetScene scene)
        {
            return Register(scene, ModConfig.BasePrefab.Value);
        }

        /// <summary>Создаёт клон базового префаба, настраивает его и регистрирует в сцене.</summary>
        internal static bool Register(ZNetScene scene, string baseName)
        {
            GameObject clone = null;
            try
            {
                if (scene == null) return false;

                GameObject basePrefab = scene.GetPrefab(baseName);
                if (basePrefab == null)
                {
                    Log.Error("Base prefab '" + baseName + "' not found. Check Npc.BasePrefab in the config (see ls_prefabs).");
                    return false;
                }

                EnsureContainer();
                RemoveCurrent(scene);

                // Клон создаётся внутри неактивного контейнера: Awake/ZNetView не запускаются,
                // пока из клона не создан настоящий экземпляр.
                clone = UnityEngine.Object.Instantiate(basePrefab, _container.transform);
                clone.name = PrefabName;
                Configure(clone);

                if (!AddToScene(scene, clone))
                {
                    UnityEngine.Object.Destroy(clone);
                    return false;
                }

                _current = clone;
                BaseName = baseName;
                Log.Info("Survivor prefab '" + PrefabName + "' registered (body: " + baseName + ").");
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Prefab registration failed: " + e);
                if (clone != null) UnityEngine.Object.Destroy(clone);
                return false;
            }
        }

        private static void EnsureContainer()
        {
            if (_container != null) return;

            _container = new GameObject("LivingSurvivors_PrefabContainer");
            _container.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_container);
        }

        private static void RemoveCurrent(ZNetScene scene)
        {
            if (_current == null) return;

            scene.m_prefabs.Remove(_current);
            Dictionary<int, GameObject> named = NamedPrefabs(scene);
            if (named != null) named.Remove(PrefabName.GetStableHashCode());

            UnityEngine.Object.Destroy(_current);
            _current = null;
        }

        /// <summary>m_namedPrefabs — приватный словарь ZNetScene, заполняется из m_prefabs в Awake.</summary>
        private static Dictionary<int, GameObject> NamedPrefabs(ZNetScene scene)
        {
            return Reflect.GetFieldValue(scene, "m_namedPrefabs") as Dictionary<int, GameObject>;
        }

        private static bool AddToScene(ZNetScene scene, GameObject prefab)
        {
            Dictionary<int, GameObject> named = NamedPrefabs(scene);
            if (named == null)
            {
                Log.Error("ZNetScene.m_namedPrefabs not found: the game API changed, the prefab cannot be registered.");
                return false;
            }

            named[PrefabName.GetStableHashCode()] = prefab;
            if (!scene.m_prefabs.Contains(prefab)) scene.m_prefabs.Add(prefab);
            return true;
        }

        private static void Configure(GameObject go)
        {
            // 1. Лишнее от базового существа.
            foreach (string typeName in StripComponentTypes)
            {
                int removed = Reflect.RemoveComponents(go, typeName);
                if (removed > 0) Log.Debug("Removed " + removed + " x " + typeName);
            }

            // 2. Персонаж: имя, фракция, здоровье. Фракция Players — дружелюбны игроку и воюют с монстрами.
            Character character = go.GetComponent<Character>();
            if (character == null) throw new InvalidOperationException("Base prefab has no Character component.");
            character.m_name = "$ls_survivor";
            character.m_faction = Character.Faction.Players;
            character.m_health = Mathf.Max(10f, ModConfig.BaseHealth.Value);

            // 3. Гуманоид: убираем стандартную и случайную экипировку (снаряжением управляет мод,
            //    оно сохраняется в ZDO), но запоминаем «родное» оружие как запасной вариант.
            Humanoid humanoid = go.GetComponent<Humanoid>();
            if (humanoid == null) throw new InvalidOperationException("Base prefab has no Humanoid component.");
            CollectFallbackWeapons(humanoid);
            int cleared = Reflect.ClearArrayFields(humanoid, typeof(Humanoid),
                fieldName => fieldName == "m_defaultItems" || fieldName.StartsWith("m_random"));
            Log.Debug("Cleared " + cleared + " default-gear arrays.");

            // 4. Дроп существа: выжившие не должны быть источником лута (вещи выбрасываются из инвентаря при гибели).
            IList drops = Reflect.GetFieldValue(go.GetComponent<CharacterDrop>(), "m_drops") as IList;
            if (drops != null) drops.Clear();

            // 5. Сеть: объект должен сохраняться вместе с миром.
            Reflect.TrySetField(go.GetComponent<ZNetView>(), "m_persistent", true);

            // 6. ИИ: не охотиться на игрока и его постройки.
            MonsterAI ai = go.GetComponent<MonsterAI>();
            if (ai != null)
            {
                Reflect.TrySetField(ai, "m_enableHuntPlayer", false);
                Reflect.TrySetField(ai, "m_attackPlayerObjects", false);
            }
            else
            {
                Log.Warn("Base prefab has no MonsterAI: following and combat will not work.");
            }

            // 7. Tameable даёт ванильные команды «следовать / ждать» и их сохранение между сессиями.
            Tameable tameable = go.GetComponent<Tameable>();
            if (tameable == null) tameable = go.AddComponent<Tameable>();
            Reflect.TrySetField(tameable, "m_commandable", true);
            Reflect.TrySetField(tameable, "m_fedDuration", 1000000f); // без «голода питомца»
            if (ai != null)
            {
                Reflect.TrySetField(tameable, "m_monsterAI", ai);
                Reflect.TrySetField(ai, "m_tamable", tameable);
            }

            // 8. Мозг выжившего: личность, сохранение, снаряжение, настройка ИИ по характеру.
            if (go.GetComponent<SurvivorBrain>() == null) go.AddComponent<SurvivorBrain>();

            if (go.GetComponentInChildren<VisEquipment>(true) == null)
            {
                Log.Warn("Body '" + go.name + "' has no VisEquipment: gear will work but will not be visible. "
                    + "Try another Npc.BasePrefab.");
            }
        }

        private static void CollectFallbackWeapons(Humanoid humanoid)
        {
            var names = new List<string>();
            AddNames(names, Reflect.GetFieldValue(humanoid, "m_defaultItems") as Array);
            AddNames(names, Reflect.GetFieldValue(humanoid, "m_randomWeapon") as Array);
            _fallbackWeapons = names;

            Log.Debug("Base body default items: " + (names.Count > 0 ? string.Join(", ", names.ToArray()) : "(none)"));
        }

        private static void AddNames(List<string> into, Array source)
        {
            foreach (string name in Reflect.NamesOf(source))
            {
                if (!into.Contains(name)) into.Add(name);
            }
        }
    }
}
