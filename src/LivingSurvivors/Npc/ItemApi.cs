using LivingSurvivors.Util;
using UnityEngine;

namespace LivingSurvivors.Npc
{
    /// <summary>
    /// Единственное место, где мод работает с API предметов Valheim (ItemDrop / ObjectDB).
    /// Если после обновления игры что-то перестанет собираться или работать с предметами —
    /// чинить нужно здесь. NPC используют только настоящие игровые предметы (ТЗ 8).
    /// </summary>
    internal static class ItemApi
    {
        /// <summary>
        /// Создаёт настоящий экземпляр предмета по имени префаба (как при подборе игроком).
        /// null, если такого предмета в игре нет.
        /// </summary>
        internal static ItemDrop.ItemData Create(string prefabName, int amount)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;

            ObjectDB db = ObjectDB.instance;
            if (db == null) return null;

            GameObject prefab = db.GetItemPrefab(prefabName);
            if (prefab == null)
            {
                Log.Debug("Item prefab not found: " + prefabName);
                return null;
            }

            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return null;

            ItemDrop.ItemData data = drop.m_itemData.Clone();
            data.m_dropPrefab = prefab;
            data.m_stack = Mathf.Clamp(amount, 1, Mathf.Max(1, data.m_shared.m_maxStackSize));
            data.m_equipped = false;
            return data;
        }

        /// <summary>Выбрасывает предмет в мир (используется при гибели NPC).</summary>
        internal static void Drop(ItemDrop.ItemData item, Vector3 position)
        {
            if (item == null) return;
            ItemDrop.DropItem(item, item.m_stack, position, Quaternion.identity);
        }

        /// <summary>Тип предмета строкой ("OneHandedWeapon", "Bow", "Chest", "Ammo"...).</summary>
        internal static string TypeName(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null ? item.m_shared.m_itemType.ToString() : "";
        }

        internal static bool IsOneHanded(ItemDrop.ItemData item)
        {
            return TypeName(item) == "OneHandedWeapon";
        }

        /// <summary>Оружие: одноручное, двуручное (в т.ч. левое) или лук.</summary>
        internal static bool IsWeapon(ItemDrop.ItemData item)
        {
            string type = TypeName(item);
            return type == "Bow" || type.EndsWith("Weapon") || type.StartsWith("TwoHandedWeapon");
        }

        internal static bool IsBow(ItemDrop.ItemData item)
        {
            return TypeName(item) == "Bow";
        }

        internal static bool IsAmmo(ItemDrop.ItemData item)
        {
            string type = TypeName(item);
            return type == "Ammo" || type == "AmmoNonEquipable";
        }

        /// <summary>Экипировка или боеприпасы — то, что NPC имеет смысл принимать от игрока.</summary>
        internal static bool IsGearOrAmmo(ItemDrop.ItemData item)
        {
            return item != null && (item.IsEquipable() || IsAmmo(item));
        }

        internal static string DisplayName(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return "?";
            Localization localization = Localization.instance;
            return localization != null ? localization.Localize(item.m_shared.m_name) : item.m_shared.m_name;
        }
    }
}
