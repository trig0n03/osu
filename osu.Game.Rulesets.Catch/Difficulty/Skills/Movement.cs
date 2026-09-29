// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Aggregation;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1.6;
        protected override double StrainDecayBase => 0.05;

        private double clockRate = 1.0;

        protected override int SectionLength => (int)Math.Round(750 / clockRate);

        private int totalCombo;

        public Movement(Mod[] mods, int totalComboHits)
            : base(mods)
        {
            totalCombo = totalComboHits;
            clockRate = ModUtils.CalculateRateWithMods(mods);
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            return MovementEvaluator.EvaluateDifficultyOf(current);
        }

        public override double DifficultyValue()
        {
            var peaks = GetCurrentStrainPeaks().ToList();

            if (ObjectDifficulties.Count == 0)
                return 0;

            (double difficulty, _) = HarmonicSeries.Aggregate(peaks, harmonicScale: 15, decayExponent: 0.7);

            // The shortest maps are believed to be valued too much
            // lengthBonus decreases the value of maps with less than 600x combo at quadratic pace
            // 600x combo is chosen as it is around the standard amount of combo for "TV size" maps at Overdose level
            double lengthBonus = 0.8 + 0.2 * (1.0 - Math.Pow((Math.Min(totalCombo, 600.0) / 600.0) - 1.0, 2.0));
            difficulty *= lengthBonus;

            return difficulty;
        }
    }
}
