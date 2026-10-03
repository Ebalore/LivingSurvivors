using System;
using System.Collections.Generic;
using LivingSurvivors.Core;
using LivingSurvivors.Util;
using UnityEngine;

namespace LivingSurvivors.Npc
{
    /// <summary>
    /// Какие параметры (триггеры анимаций) есть у аниматора тела NPC.
    /// Урон в Valheim наносится событиями анимации атаки: если у тела нет нужной анимации,
    /// оружие в руках есть, а ударов нет. Поэтому оружие выбирается только из тех,
    /// чью анимацию тело умеет проигрывать.
    /// </summary>
    internal sealed class AnimatorSupport
    {
        private readonly HashSet<string> _names; // null — неизвестно, разрешаем всё

        private AnimatorSupport(HashSet<string> names)
        {
            _names = names;
        }

        internal bool IsKnown
        {
            get { return _names != null; }
        }

        internal int Count
        {
            get { return _names != null ? _names.Count : 0; }
        }

        internal static AnimatorSupport From(Animator animator)
        {
            if (animator == null) return new AnimatorSupport(null);

            try
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    names.Add(parameter.name);
                }
                return new AnimatorSupport(names.Count > 0 ? names : null);
            }
            catch (Exception e)
            {
                Log.Debug("Animator parameters unavailable: " + e.Message);
                return new AnimatorSupport(null);
            }
        }

        internal bool HasParam(string name)
        {
            return _names == null || _names.Contains(name);
        }

        /// <summary>Умеет ли тело проиграть анимацию атаки этого предмета.</summary>
        internal bool CanUse(ItemDrop.ItemData item)
        {
            if (_names == null || item == null || item.m_shared == null) return true;
            return CanPlay(Reflect.GetFieldValue(item.m_shared, "m_attack"));
        }

        private bool CanPlay(object attack)
        {
            if (attack == null) return true;

            string animation = Reflect.GetFieldValue(attack, "m_attackAnimation") as string;
            if (!string.IsNullOrEmpty(animation) && !_names.Contains(animation)) return false;

            string draw = Reflect.GetFieldValue(attack, "m_drawAnimationState") as string;
            if (!string.IsNullOrEmpty(draw) && !_names.Contains(draw)) return false;

            return true;
        }
    }

    /// <summary>
    /// Выдаёт NPC стартовое снаряжение по плану из ядра (<see cref="LoadoutPlanner"/>):
    /// реальные предметы, реальная экипировка. Несуществующие имена пропускаются с записью в лог.
    /// </summary>
    internal static class LoadoutService
    {
        /// <summary>Выдаёт и надевает комплект. Возвращает краткое описание для консоли и журнала.</summary>
        internal static string GiveKit(SurvivorBrain brain, int tier)
        {
            Humanoid body = brain.Body;
            if (body == null || brain.Profile == null) return "no body";

            Inventory inventory = body.GetInventory();
            AnimatorSupport support = AnimatorSupport.From(brain.Animator);
            LoadoutPlan plan = LoadoutPlanner.Plan(brain.Profile, tier);

            Log.Debug(string.Format("[{0}] kit: tier {1}, style {2}, animator parameters known: {3} ({4})",
                brain.DisplayName, tier, brain.Profile.Style, support.IsKnown, support.Count));

            // 1. Оружие: первое существующее в игре и анимируемое этим телом.
            ItemDrop.ItemData weapon = ChooseWeapon(plan, support);
            string weaponName = "none";
            if (weapon == null)
            {
                Log.Warn(brain.DisplayName + ": no weapon this body can animate. "
                    + "Try another Npc.BasePrefab (see ls_bodyinfo / ls_trybody).");
            }
            else if (inventory.AddItem(weapon))
            {
                body.EquipItem(weapon, false);
                weaponName = weapon.m_dropPrefab != null ? weapon.m_dropPrefab.name : ItemApi.DisplayName(weapon);
            }
            else
            {
                Log.Warn(brain.DisplayName + ": the weapon did not fit into the inventory.");
            }

            // 2. Щит — только к одноручному оружию и только если у тела есть анимация блока.
            string shieldName = "none";
            if (weapon != null && plan.Shield != null && ItemApi.IsOneHanded(weapon) && support.HasParam("blocking"))
            {
                ItemDrop.ItemData shield = ItemApi.Create(plan.Shield, 1);
                if (shield != null && inventory.AddItem(shield))
                {
                    body.EquipItem(shield, false);
                    shieldName = plan.Shield;
                }
            }

            // 3. Броня: работает и без визуала — броня считается по надетым предметам.
            int armorCount = 0;
            foreach (string armorName in plan.Armor)
            {
                ItemDrop.ItemData armor = ItemApi.Create(armorName, 1);
                if (armor == null) continue; // такого предмета в игре нет — уже записано в журнал

                if (inventory.AddItem(armor))
                {
                    body.EquipItem(armor, false);
                    armorCount++;
                }
                else
                {
                    Log.Warn(brain.DisplayName + ": " + armorName + " did not fit into the inventory.");
                }
            }

            // 4. Стрелы — только если в руках лук.
            if (weapon != null && ItemApi.IsBow(weapon) && plan.Ammo != null)
            {
                ItemDrop.ItemData ammo = ItemApi.Create(plan.Ammo, plan.AmmoCount);
                if (ammo != null) inventory.AddItem(ammo);
            }

            return string.Format("weapon={0} shield={1} armor={2}", weaponName, shieldName, armorCount);
        }

        private static ItemDrop.ItemData ChooseWeapon(LoadoutPlan plan, AnimatorSupport support)
        {
            // Сначала желаемое по плану, затем «родное» оружие базового существа как гарантия.
            var candidates = new List<string>(plan.WeaponCandidates);
            foreach (string fallback in SurvivorPrefab.FallbackWeapons)
            {
                if (!candidates.Contains(fallback)) candidates.Add(fallback);
            }

            foreach (string name in candidates)
            {
                ItemDrop.ItemData item = ItemApi.Create(name, 1);
                if (item == null || !ItemApi.IsWeapon(item)) continue;

                if (!support.CanUse(item))
                {
                    Log.Debug("Skip " + name + ": the body has no animation for it.");
                    continue;
                }
                return item;
            }
            return null;
        }
    }
}
