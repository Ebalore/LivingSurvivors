using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LivingSurvivors.Util
{
    /// <summary>
    /// Безопасный доступ к полям и компонентам по имени.
    /// Нужен там, где точное имя/тип члена в сборках Valheim 1.0 не подтверждено источниками:
    /// вместо ошибки компиляции или падения игры получаем запись в лог и работаем дальше.
    /// Все поля ищутся и в базовых классах, включая приватные.
    /// </summary>
    internal static class Reflect
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<string, FieldInfo> FieldCache = new Dictionary<string, FieldInfo>();

        internal static FieldInfo FindField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            string key = type.FullName + "::" + name;
            FieldInfo field;
            if (FieldCache.TryGetValue(key, out field)) return field;

            for (Type t = type; t != null && field == null; t = t.BaseType)
            {
                field = t.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
            }

            FieldCache[key] = field; // отсутствие тоже кэшируем
            return field;
        }

        internal static object GetFieldValue(object target, string name)
        {
            if (target == null) return null;
            FieldInfo field = FindField(target.GetType(), name);
            return field != null ? field.GetValue(target) : null;
        }

        internal static T GetFieldOr<T>(object target, string name, T fallback)
        {
            object value = GetFieldValue(target, name);
            if (value is T) return (T)value;
            return fallback;
        }

        internal static bool TrySetField(object target, string name, object value)
        {
            if (target == null) return false;

            FieldInfo field = FindField(target.GetType(), name);
            if (field == null)
            {
                Log.Debug("Field not found: " + target.GetType().Name + "." + name);
                return false;
            }

            try
            {
                object converted = value;
                if (converted != null
                    && !field.FieldType.IsInstanceOfType(converted)
                    && field.FieldType.IsPrimitive
                    && converted is IConvertible)
                {
                    converted = Convert.ChangeType(converted, field.FieldType);
                }

                field.SetValue(target, converted);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Cannot set " + target.GetType().Name + "." + name + ": " + e.Message);
                return false;
            }
        }

        /// <summary>Вызов метода без аргументов по имени; null, если метода нет или он упал.</summary>
        internal static object InvokeNoArgs(object target, string methodName)
        {
            if (target == null) return null;
            try
            {
                MethodInfo method = target.GetType().GetMethod(
                    methodName, InstanceFlags, null, Type.EmptyTypes, null);
                return method != null ? method.Invoke(target, null) : null;
            }
            catch (Exception e)
            {
                Log.Debug("Invoke " + methodName + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Заменяет массивы-поля, объявленные именно в типе <paramref name="declaringType"/> и прошедшие фильтр
        /// по имени, пустыми массивами того же типа (например, стандартную и случайную экипировку Humanoid).
        /// Базовые классы нарочно не затрагиваются — чтобы не обнулить посторонние поля.
        /// </summary>
        internal static int ClearArrayFields(object target, Type declaringType, Func<string, bool> nameFilter)
        {
            if (target == null || declaringType == null) return 0;

            int cleared = 0;
            foreach (FieldInfo field in declaringType.GetFields(InstanceFlags | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsArray || !nameFilter(field.Name)) continue;
                field.SetValue(target, Array.CreateInstance(field.FieldType.GetElementType(), 0));
                cleared++;
            }
            return cleared;
        }

        /// <summary>Удаляет с объекта (и дочерних) все компоненты типа с данным именем. Возвращает их число.</summary>
        internal static int RemoveComponents(GameObject gameObject, string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null) return 0;

            Component[] found = gameObject.GetComponentsInChildren(type, true);
            foreach (Component component in found)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
            return found.Length;
        }

        /// <summary>Имена GameObject из массива (для массива префабов); дубликаты отбрасываются.</summary>
        internal static List<string> NamesOf(Array array)
        {
            var names = new List<string>();
            if (array == null) return names;

            foreach (object item in array)
            {
                GameObject go = item as GameObject;
                if (go != null && !names.Contains(go.name)) names.Add(go.name);
            }
            return names;
        }
    }
}
