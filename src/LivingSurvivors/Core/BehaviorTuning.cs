namespace LivingSurvivors.Core
{
    /// <summary>
    /// Превращает черты характера в конкретные параметры ИИ (ТЗ 3.3: «черты должны
    /// влиять на решения, а не быть только текстовым описанием»).
    /// На Этапе 1 это два параметра ванильного MonsterAI; список будет расти
    /// вместе с модулями ИИ (скорость работы, склонность делиться едой и т.д.).
    /// </summary>
    public sealed class BehaviorTuning
    {
        /// <summary>Доля здоровья (0..1), ниже которой NPC начинает отступать.</summary>
        public float FleeHealthFraction = 0.25f;

        /// <summary>Множитель дальности обнаружения угроз.</summary>
        public float AlertRangeMultiplier = 1f;

        public static BehaviorTuning From(NpcProfile profile)
        {
            var tuning = new BehaviorTuning();
            if (profile == null) return tuning;

            if (profile.Has(NpcTrait.Cautious))
            {
                tuning.FleeHealthFraction += 0.20f;
                tuning.AlertRangeMultiplier -= 0.10f;
            }
            if (profile.Has(NpcTrait.Brave))
            {
                tuning.FleeHealthFraction -= 0.15f;
            }
            if (profile.Has(NpcTrait.Aggressive))
            {
                tuning.FleeHealthFraction -= 0.05f;
                tuning.AlertRangeMultiplier += 0.25f;
            }
            if (profile.Has(NpcTrait.Curious))
            {
                tuning.AlertRangeMultiplier += 0.15f;
            }
            if (profile.Has(NpcTrait.Caring))
            {
                tuning.FleeHealthFraction += 0.05f;
            }

            tuning.FleeHealthFraction = Clamp(tuning.FleeHealthFraction, 0.05f, 0.60f);
            tuning.AlertRangeMultiplier = Clamp(tuning.AlertRangeMultiplier, 0.6f, 1.6f);
            return tuning;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
