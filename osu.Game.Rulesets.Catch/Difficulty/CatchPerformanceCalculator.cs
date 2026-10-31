// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchPerformanceCalculator : PerformanceCalculator
    {
        private const double performance_multiplier = 1.24;

        private int num300;
        private int num100;
        private int num50;
        private int numKatu;
        private int numMiss;

        public CatchPerformanceCalculator()
            : base(new CatchRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var catchAttributes = (CatchDifficultyAttributes)attributes;

            num300 = score.GetCount300() ?? 0; // HitResult.Great
            num100 = score.GetCount100() ?? 0; // HitResult.LargeTickHit
            num50 = score.GetCount50() ?? 0; // HitResult.SmallTickHit
            numKatu = score.GetCountKatu() ?? 0; // HitResult.SmallTickMiss
            numMiss = Math.Max(0, score.GetCountMiss() ?? 0); // HitResult.Miss PLUS HitResult.LargeTickMiss

            double scoreMaxCombo = Math.Clamp(score.MaxCombo, 0, catchAttributes.MaxCombo);

            // We are heavily relying on aim in catch the beat
            double value = DiffUtils.Pow(5 * Math.Max(1, catchAttributes.StarRating / 0.0049) - 4, 2) / 100000;

            value *= DiffUtils.Pow(0.97, numMiss);

            // Combo scaling
            if (catchAttributes.MaxCombo > 0)
                value *= Math.Min(DiffUtils.Pow(scoreMaxCombo, 0.35) / DiffUtils.Pow(catchAttributes.MaxCombo, 0.35), 1);

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            double clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            // this is the same as osu!, so there's potential to share the implementation... maybe
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(difficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            double approachRate = preempt > 1200 ? -(preempt - 1800) / 120 : -(preempt - 1200) / 150 + 5;

            double approachRateFactor = 1;
            if (approachRate >= 9)
                approachRateFactor += Math.Pow((approachRate - 9) / 2, 2) * 0.25; // 25% for AR11
            if (approachRate < 8 && approachRate >= 5)
                approachRateFactor += 0.025 * (7 - approachRate); // 5% for AR5
            if (approachRate < 5) // Pace of time->AR function is slower below AR5
                approachRateFactor += 0.025 * (2 + 1.25 * (5 - approachRate)); // 20.875% for AR0
            
            if (!score.Mods.Any(m => m is ModFlashlight))
                value *= approachRateFactor;

            // HD bonus: hidden gives almost nothing on max approach rate, and more the lower it is.
            double hiddenFactor = 1.01;
            if (score.Mods.Any(m => m is ModHidden))
            {
                if (approachRate < 11 && approachRate >= 8)
                    hiddenFactor += 0.25 * Math.Pow(((11 - approachRate) / 3), 1.6); // 26% for AR8
                if (approachRate < 8 && approachRate >= 5)
                    hiddenFactor += 0.25 * (1.0 + 1.65 * (8 - approachRate) / 3); // tangent line to the function above at AR8
                if (approachRate < 5) // Pace of time->AR function is slower below AR5
                    hiddenFactor += 0.25 * (1 + 1.65 * (3 + 1.25 * (5 - approachRate)) / 3); // around 153% for AR0
                
                if (!score.Mods.Any(m => m is ModFlashlight))
                    value *= hiddenFactor;
            }

            // Flashlight bonuses.
            if (score.Mods.Any(m => m is ModFlashlight))
            {
                value *= 1.07; // Base Flashlight bonus: it is always harder to play with Flashlight than without it.

                // Flashlight approach rate bonus: the higher approach rate is, the harder flashlight is.
                double flashlightApproachRateFactor = 1;

                if (approachRate >= 0)
                    flashlightApproachRateFactor += 0.6 * Math.Pow(approachRate / 11, 3); // 0% for AR0, 60% for AR11

                // Make sure that FL doesn't give less pp than NM
                flashlightApproachRateFactor = Math.Max(approachRateFactor, flashlightApproachRateFactor);

                if (score.Mods.Any(m => m is ModHidden))
                {
                    // HDFL bonus: when AR is low, the main difficulty is HD, so we take hiddenFactor.
                    // When AR is high, the main difficulty is FL, so we take flashlightApproachRateFactor.
                    value *= Math.Max(flashlightApproachRateFactor, approachRateFactor * hiddenFactor);
                    value *= 1.05; // Base Hidden Flashlight bonus.
                }
                else
                    value *= flashlightApproachRateFactor;

                // Flashlight length bonus: the higher the cumulative distance is, the more player has to memorise.
                float cumulativeDistance = catchAttributes.CumulativeDistance;
                value *= 1 + 2 * (DiffUtils.Pow(1 + cumulativeDistance / 100000, 0.2) - 1);
            }

            value *= DiffUtils.Pow(accuracy(), 5.5);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.9, 1 - 0.02 * numMiss);

            value *= performance_multiplier;

            return new CatchPerformanceAttributes
            {
                Total = value
            };
        }

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => num50 + num100 + num300 + numMiss + numKatu;
        private int totalSuccessfulHits() => num50 + num100 + num300;
        private int totalComboHits() => numMiss + num100 + num300;
    }
}
