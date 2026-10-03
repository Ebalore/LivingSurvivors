using System;
using System.IO;
using System.Text;

namespace LivingSurvivors.Core
{
    /// <summary>
    /// Версионированная сериализация профиля в строку base64 (ТЗ 16: «версионирование
    /// данных и миграции сохранений»). Строка кладётся в ZDO NPC и сохраняется вместе с миром.
    /// Чтобы добавить поле: увеличьте <see cref="CurrentVersion"/>, допишите запись в
    /// <see cref="Serialize"/> и добавьте ветку чтения/миграции в <see cref="TryDeserialize"/>.
    /// </summary>
    public static class NpcProfileSerializer
    {
        public const byte CurrentVersion = 1;

        public static string Serialize(NpcProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(CurrentVersion);
                writer.Write(profile.Id ?? "");
                writer.Write(profile.Name ?? "");
                writer.Write(profile.Seed);
                writer.Write((byte)profile.Style);
                writer.Write(profile.BornDay);

                writer.Write((byte)profile.Traits.Count);
                foreach (NpcTrait trait in profile.Traits)
                {
                    writer.Write((byte)trait);
                }

                writer.Write((byte)profile.Skills.Length);
                writer.Write(profile.Skills);

                writer.Write((byte)profile.History.Count);
                foreach (HistoryEntry entry in profile.History)
                {
                    writer.Write(entry.Day);
                    writer.Write(entry.Text ?? "");
                }

                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        /// <summary>
        /// Читает профиль. Возвращает false (и не бросает исключений) для пустой,
        /// повреждённой или более новой неизвестной версии строки.
        /// </summary>
        public static bool TryDeserialize(string base64, out NpcProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(base64)) return false;

            try
            {
                byte[] data = Convert.FromBase64String(base64);
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    byte version = reader.ReadByte();
                    switch (version)
                    {
                        case 1:
                            profile = ReadV1(reader);
                            return profile != null;

                        // Новые версии и миграции со старых добавлять здесь.
                        default:
                            return false;
                    }
                }
            }
            catch (Exception)
            {
                // FormatException (не base64), EndOfStreamException (обрезанные данные) и т.п.
                profile = null;
                return false;
            }
        }

        private static NpcProfile ReadV1(BinaryReader reader)
        {
            var profile = new NpcProfile();
            profile.Id = reader.ReadString();
            profile.Name = reader.ReadString();
            profile.Seed = reader.ReadInt32();
            profile.Style = (WeaponStyle)reader.ReadByte();
            profile.BornDay = reader.ReadInt32();

            int traitCount = reader.ReadByte();
            for (int i = 0; i < traitCount; i++)
            {
                byte raw = reader.ReadByte();
                if (Enum.IsDefined(typeof(NpcTrait), (NpcTrait)raw))
                {
                    profile.Traits.Add((NpcTrait)raw);
                }
            }

            int skillCount = reader.ReadByte();
            byte[] skills = reader.ReadBytes(skillCount);
            for (int i = 0; i < skills.Length && i < profile.Skills.Length; i++)
            {
                profile.Skills[i] = skills[i];
            }

            int historyCount = reader.ReadByte();
            for (int i = 0; i < historyCount; i++)
            {
                int day = reader.ReadInt32();
                string text = reader.ReadString();
                profile.AddEvent(day, text);
            }

            if (!Enum.IsDefined(typeof(WeaponStyle), profile.Style))
            {
                profile.Style = WeaponStyle.Mace;
            }

            return string.IsNullOrEmpty(profile.Id) ? null : profile;
        }
    }
}
