// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Aggregation;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1.9;
        protected override double StrainDecayBase => 0.05;

        protected override int SectionLength => 750;

        public Movement(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            return MovementEvaluator.EvaluateDifficultyOf(current);
        }

        public override double DifficultyValue()
        {
            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var peaks = GetCurrentStrainPeaks().Where(p => p > 0).ToList();

            if (ObjectDifficulties.Count == 0)
                return 0;

            (double difficulty, _) = HarmonicSeries.Aggregate(peaks, harmonicScale: 15, decayExponent: 0.7);

            return difficulty;
        }
    }
}
