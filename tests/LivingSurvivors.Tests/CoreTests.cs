using System;
using System.Collections.Generic;
using LivingSurvivors.Core;
using Xunit;

namespace LivingSurvivors.Tests
{
    public class GeneratorTests
    {
        [Fact]
        public void SameSeed_GivesSameProfile()
        {
            NpcProfile a = NpcProfileGenerator.Generate(777, null, 0);
            NpcProfile b = NpcProfileGenerator.Generate(777, null, 0);

            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Traits, b.Traits);
            Assert.Equal(a.Skills, b.Skills);
            Assert.Equal(a.Style, b.Style);
        }

        [Fact]
        public void TakenNames_AreAvoided()
        {
            NpcProfile first = NpcProfileGenerator.Generate(1, null, 0);
            var taken = new HashSet<string> { first.Name };

            NpcProfile second = NpcProfileGenerator.Generate(1, taken, 0);

            Assert.NotEqual(first.Name, second.Name);
            Assert.StartsWith(first.Name, second.Name);
        }

        [Fact]
        public void Traits_AreTwoDistinctAndNeverConflicting()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                NpcProfile profile = NpcProfileGenerator.Generate(seed, null, 0);

                Assert.Equal(2, profile.Traits.Count);
                Assert.NotEqual(profile.Traits[0], profile.Traits[1]);
                Assert.False(NpcProfileGenerator.Conflicts(profile.Traits[0], profile.Traits[1]));
            }
        }

        [Fact]
        public void SkillsAndStyle_AreInValidRanges()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                NpcProfile profile = NpcProfileGenerator.Generate(seed, null, 0);

                foreach (byte skill in profile.Skills)
                {
                    Assert.InRange((int)skill, 5, 100);
                }
                Assert.True(Enum.IsDefined(typeof(WeaponStyle), profile.Style));
                Assert.StartsWith("ls-", profile.Id);
                Assert.Equal(11, profile.Id.Length);
            }
        }
    }

    public class SerializerTests
    {
        [Fact]
        public void RoundTrip_PreservesAllFields()
        {
            NpcProfile original = NpcProfileGenerator.Generate(12345, null, 7);
            original.AddEvent(7, "born");
            original.AddEvent(9, "died");

            string text = NpcProfileSerializer.Serialize(original);
            NpcProfile copy;

            Assert.True(NpcProfileSerializer.TryDeserialize(text, out copy));
            Assert.Equal(original.Id, copy.Id);
            Assert.Equal(original.Name, copy.Name);
            Assert.Equal(original.Seed, copy.Seed);
            Assert.Equal(original.Style, copy.Style);
            Assert.Equal(original.BornDay, copy.BornDay);
            Assert.Equal(original.Traits, copy.Traits);
            Assert.Equal(original.Skills, copy.Skills);
            Assert.Equal(2, copy.History.Count);
            Assert.Equal("died", copy.History[1].Text);
            Assert.Equal(9, copy.History[1].Day);
        }

        [Fact]
        public void Garbage_IsRejectedWithoutExceptions()
        {
            NpcProfile profile;

            Assert.False(NpcProfileSerializer.TryDeserialize(null, out profile));
            Assert.False(NpcProfileSerializer.TryDeserialize("", out profile));
            Assert.False(NpcProfileSerializer.TryDeserialize("this is not base64!!", out profile));

            // Версия 1, но данные обрезаны.
            Assert.False(NpcProfileSerializer.TryDeserialize(Convert.ToBase64String(new byte[] { 1, 2 }), out profile));

            // Неизвестная (более новая) версия.
            Assert.False(NpcProfileSerializer.TryDeserialize(Convert.ToBase64String(new byte[] { 99, 0, 0 }), out profile));
        }

        [Fact]
        public void History_IsLimitedToTheLatestEntries()
        {
            var profile = new NpcProfile { Id = "ls-test", Name = "Test" };
            for (int i = 0; i < 100; i++) profile.AddEvent(i, "evt" + i);

            Assert.Equal(NpcProfile.MaxHistory, profile.History.Count);
            Assert.Equal("evt99", profile.History[profile.History.Count - 1].Text);
            Assert.Equal("evt" + (100 - NpcProfile.MaxHistory), profile.History[0].Text);
        }
    }

    public class LoadoutPlannerTests
    {
        [Fact]
        public void Archer_GetsArrowsAndNoShield()
        {
            var profile = new NpcProfile { Id = "x", Style = WeaponStyle.Bow };

            LoadoutPlan plan = LoadoutPlanner.Plan(profile, 0);

            Assert.Equal("Bow", plan.WeaponCandidates[0]);
            Assert.Null(plan.Shield);
            Assert.Equal("ArrowWood", plan.Ammo);
            Assert.Equal(LoadoutPlanner.DefaultAmmoCount, plan.AmmoCount);
        }

        [Fact]
        public void Swordsman_InMeadows_FallsBackToAnotherWeapon()
        {
            var profile = new NpcProfile { Id = "x", Style = WeaponStyle.Sword };

            LoadoutPlan plan = LoadoutPlanner.Plan(profile, 0);

            // На Лугах мечей ещё нет: первым кандидатом становится запасной стиль, дубина остаётся в списке.
            Assert.Equal("AxeFlint", plan.WeaponCandidates[0]);
            Assert.Contains("Club", plan.WeaponCandidates);
            Assert.Equal("ShieldWood", plan.Shield);
        }

        [Fact]
        public void Tier_IsClamped()
        {
            var profile = new NpcProfile { Id = "x", Style = WeaponStyle.Sword };

            LoadoutPlan high = LoadoutPlanner.Plan(profile, 99);
            LoadoutPlan max = LoadoutPlanner.Plan(profile, LoadoutPlanner.MaxTier);
            Assert.Equal(max.WeaponCandidates, high.WeaponCandidates);
            Assert.Equal(max.Armor, high.Armor);

            LoadoutPlan low = LoadoutPlanner.Plan(null, -5);
            Assert.Equal("Club", low.WeaponCandidates[0]);
        }

        [Fact]
        public void AllItemNames_AreUniqueAndNotEmpty()
        {
            List<string> names = LoadoutPlanner.AllItemNames();

            Assert.NotEmpty(names);
            Assert.Equal(names.Count, new HashSet<string>(names).Count);
            Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
        }
    }

    public class BehaviorTuningTests
    {
        [Fact]
        public void Cautious_FleesEarlierThanBrave()
        {
            var cautious = new NpcProfile { Id = "c" };
            cautious.Traits.Add(NpcTrait.Cautious);
            var brave = new NpcProfile { Id = "b" };
            brave.Traits.Add(NpcTrait.Brave);

            Assert.True(BehaviorTuning.From(cautious).FleeHealthFraction
                        > BehaviorTuning.From(brave).FleeHealthFraction);
        }

        [Fact]
        public void Values_StayInBounds_EvenWithEveryTrait()
        {
            var profile = new NpcProfile { Id = "all" };
            foreach (NpcTrait trait in Enum.GetValues(typeof(NpcTrait))) profile.Traits.Add(trait);

            BehaviorTuning tuning = BehaviorTuning.From(profile);

            Assert.InRange(tuning.FleeHealthFraction, 0.05f, 0.60f);
            Assert.InRange(tuning.AlertRangeMultiplier, 0.6f, 1.6f);
        }

        [Fact]
        public void NullProfile_GivesDefaults()
        {
            BehaviorTuning tuning = BehaviorTuning.From(null);

            Assert.Equal(0.25f, tuning.FleeHealthFraction);
            Assert.Equal(1f, tuning.AlertRangeMultiplier);
        }
    }
}
