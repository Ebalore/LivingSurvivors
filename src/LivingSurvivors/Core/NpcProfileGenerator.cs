using System;
using System.Collections.Generic;

namespace LivingSurvivors.Core
{
    /// <summary>
    /// Создаёт уникальную личность NPC по зерну (seed): один и тот же seed всегда
    /// даёт одну и ту же личность — это упрощает тесты и отладку.
    /// </summary>
    public static class NpcProfileGenerator
    {
        private static readonly string[] MaleNames =
        {
            "Bjorn", "Ragnar", "Leif", "Ivar", "Halvard", "Ulf", "Sigurd", "Erik", "Olaf", "Gunnar",
            "Torsten", "Harald", "Rurik", "Ketil", "Arne", "Dag", "Eyvind", "Floki", "Geir", "Hakon",
            "Knut", "Magnus", "Orm", "Snorri", "Thorvald", "Vidar", "Yngvar", "Starkad"
        };

        private static readonly string[] FemaleNames =
        {
            "Astrid", "Freydis", "Ingrid", "Sigrid", "Helga", "Thora", "Gudrun", "Ragnhild", "Solveig", "Aslaug",
            "Brynhild", "Embla", "Gunhild", "Hild", "Ingunn", "Jorunn", "Katla", "Liv", "Runa", "Signy",
            "Svanhild", "Tora", "Unn", "Yrsa", "Eira", "Sunniva", "Vigdis", "Alvilda"
        };

        private static readonly string[] Epithets =
        {
            "the Quiet", "Stormborn", "Ironside", "the Wanderer", "Longstride", "Oakheart",
            "the Bold", "Wolfsbane", "the Elder", "Fairwind", "Stonefist", "the Swift"
        };

        /// <param name="seed">Зерно генератора.</param>
        /// <param name="takenNames">Уже занятые имена (можно null) — генератор постарается не повторять.</param>
        /// <param name="bornDay">Игровой день появления.</param>
        public static NpcProfile Generate(int seed, ICollection<string> takenNames, int bornDay)
        {
            var rng = new Random(seed);
            var profile = new NpcProfile();
            profile.Seed = seed;
            profile.BornDay = bornDay;
            profile.Id = "ls-" + ((uint)seed).ToString("x8");
            profile.Name = PickName(rng, takenNames);
            PickTraits(rng, profile);
            RollSkills(rng, profile);
            profile.Style = PickStyle(rng, profile);
            return profile;
        }

        private static string PickName(Random rng, ICollection<string> taken)
        {
            string[] pool = rng.Next(2) == 0 ? MaleNames : FemaleNames;
            string first = pool[rng.Next(pool.Length)];
            if (!IsTaken(first, taken)) return first;

            for (int i = 0; i < 12; i++)
            {
                string candidate = first + " " + Epithets[rng.Next(Epithets.Length)];
                if (!IsTaken(candidate, taken)) return candidate;
            }

            for (int n = 2; n < 1000; n++)
            {
                string candidate = first + " " + n;
                if (!IsTaken(candidate, taken)) return candidate;
            }

            return first + " " + rng.Next(1000, 9999);
        }

        private static bool IsTaken(string name, ICollection<string> taken)
        {
            return taken != null && taken.Contains(name);
        }

        private static void PickTraits(Random rng, NpcProfile profile)
        {
            var all = (NpcTrait[])Enum.GetValues(typeof(NpcTrait));
            int guard = 0;
            while (profile.Traits.Count < 2 && guard++ < 200)
            {
                NpcTrait candidate = all[rng.Next(all.Length)];
                if (profile.Traits.Contains(candidate)) continue;

                bool conflict = false;
                for (int i = 0; i < profile.Traits.Count; i++)
                {
                    if (Conflicts(profile.Traits[i], candidate))
                    {
                        conflict = true;
                        break;
                    }
                }
                if (!conflict) profile.Traits.Add(candidate);
            }
        }

        /// <summary>Противоположные черты в одном персонаже не сочетаются.</summary>
        public static bool Conflicts(NpcTrait a, NpcTrait b)
        {
            return IsPair(a, b, NpcTrait.Cautious, NpcTrait.Brave)
                || IsPair(a, b, NpcTrait.Aggressive, NpcTrait.Caring)
                || IsPair(a, b, NpcTrait.Independent, NpcTrait.Sociable);
        }

        private static bool IsPair(NpcTrait a, NpcTrait b, NpcTrait x, NpcTrait y)
        {
            return (a == x && b == y) || (a == y && b == x);
        }

        private static void RollSkills(Random rng, NpcProfile profile)
        {
            for (int i = 0; i < NpcProfile.SkillCount; i++)
            {
                profile.Skills[i] = (byte)(5 + rng.Next(11));
            }

            foreach (NpcTrait trait in profile.Traits)
            {
                foreach (NpcSkill skill in Affinity(trait))
                {
                    profile.SetSkill(skill, profile.GetSkill(skill) + 8);
                }
            }

            // Две «специализации» — то, в чём NPC заметно лучше остальных.
            int first = rng.Next(NpcProfile.SkillCount);
            int second;
            do
            {
                second = rng.Next(NpcProfile.SkillCount);
            }
            while (second == first);

            profile.SetSkill((NpcSkill)first, profile.GetSkill((NpcSkill)first) + 25 + rng.Next(21));
            profile.SetSkill((NpcSkill)second, profile.GetSkill((NpcSkill)second) + 25 + rng.Next(21));
        }

        private static NpcSkill[] Affinity(NpcTrait trait)
        {
            switch (trait)
            {
                case NpcTrait.Hardworking: return new[] { NpcSkill.Woodcutting, NpcSkill.Mining, NpcSkill.Building };
                case NpcTrait.Greedy: return new[] { NpcSkill.Mining, NpcSkill.Smithing };
                case NpcTrait.Brave: return new[] { NpcSkill.Melee };
                case NpcTrait.Aggressive: return new[] { NpcSkill.Melee };
                case NpcTrait.Cautious: return new[] { NpcSkill.Archery, NpcSkill.Scouting };
                case NpcTrait.Curious: return new[] { NpcSkill.Scouting, NpcSkill.Foraging, NpcSkill.Fishing };
                case NpcTrait.Caring: return new[] { NpcSkill.Cooking, NpcSkill.Farming };
                case NpcTrait.Sociable: return new[] { NpcSkill.Cooking, NpcSkill.Building };
                case NpcTrait.Independent: return new[] { NpcSkill.Hunting, NpcSkill.Scouting };
                default: return new NpcSkill[0];
            }
        }

        private static WeaponStyle PickStyle(Random rng, NpcProfile profile)
        {
            int melee = profile.GetSkill(NpcSkill.Melee);
            int archery = profile.GetSkill(NpcSkill.Archery);
            if (archery >= melee + 10) return WeaponStyle.Bow;

            bool bold = profile.Has(NpcTrait.Brave) || profile.Has(NpcTrait.Aggressive);
            bool careful = profile.Has(NpcTrait.Cautious) || profile.Has(NpcTrait.Caring);
            int roll = rng.Next(100);

            if (bold)
            {
                if (roll < 40) return WeaponStyle.Sword;
                if (roll < 75) return WeaponStyle.Axe;
                return WeaponStyle.Mace;
            }

            if (careful)
            {
                if (roll < 45) return WeaponStyle.Spear;
                if (roll < 65) return WeaponStyle.Knife;
                if (roll < 85) return WeaponStyle.Sword;
                return WeaponStyle.Mace;
            }

            switch (roll % 5)
            {
                case 0: return WeaponStyle.Sword;
                case 1: return WeaponStyle.Axe;
                case 2: return WeaponStyle.Mace;
                case 3: return WeaponStyle.Spear;
                default: return WeaponStyle.Knife;
            }
        }
    }
}
