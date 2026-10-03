using UnityEngine;

namespace LivingSurvivors.Util
{
    /// <summary>Поиск поверхности под точкой — для появления NPC и «подтягивания» к игроку.</summary>
    internal static class GroundUtil
    {
        private static int _mask;

        /// <summary>Слои твёрдой поверхности (имена слоёв Valheim).</summary>
        private static int Mask
        {
            get
            {
                if (_mask == 0)
                {
                    _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
                }
                return _mask;
            }
        }

        /// <summary>
        /// Луч вниз из точки на heightAbove выше заданной.
        /// Для помещений берите небольшой heightAbove, чтобы не попасть на крышу.
        /// </summary>
        internal static bool TryGetGround(Vector3 around, float heightAbove, float maxDistance, out Vector3 ground)
        {
            Vector3 origin = new Vector3(around.x, around.y + heightAbove, around.z);
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, maxDistance, Mask))
            {
                ground = hit.point;
                return true;
            }

            ground = around;
            return false;
        }

        /// <summary>Точка ниже уровня воды (не телепортировать NPC в море).</summary>
        internal static bool IsBelowWater(float y)
        {
            ZoneSystem zones = ZoneSystem.instance;
            float waterLevel = zones != null ? Reflect.GetFieldOr<float>(zones, "m_waterLevel", 30f) : 30f;
            return y < waterLevel + 0.1f;
        }
    }
}
