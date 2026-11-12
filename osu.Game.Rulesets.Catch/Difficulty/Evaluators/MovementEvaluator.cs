// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        private const double direction_change_bonus = 21.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current, double circleSize)
        {
            var catchLast = (CatchDifficultyHitObject)current.Previous(0);
            var catchCurrent = (CatchDifficultyHitObject)current;
            var catchNext = (CatchDifficultyHitObject)current.Next(0);

            if (catchLast == null || catchNext == null)
                return 0;

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            double clockRate = current.ClockRate;

            double weightedStrainTime = catchNext.StrainTime + 16 / clockRate;

            double distanceAddition = (DiffUtils.Pow(Math.Abs(catchNext.DistanceMoved), 1.3) / 510);
            double sqrtStrain = Math.Sqrt(weightedStrainTime);

            double edgeDashBonus = 0;

            // Direction change bonus.
            if (Math.Abs(catchNext.DistanceMoved) > 0.1)
            {
                if (Math.Abs(catchCurrent.DistanceMoved) > 0.1 && catchNext.Direction != catchCurrent.Direction)
                {
                    double bonusFactor = Math.Min(50, Math.Abs(catchNext.DistanceMoved)) / 50;
                    double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(catchCurrent.DistanceMoved)) / 70, 0.38);

                    distanceAddition += direction_change_bonus / Math.Sqrt(catchCurrent.StrainTime + 16 / clockRate) * bonusFactor * antiflowFactor * Math.Max(1 - DiffUtils.Pow(weightedStrainTime / 1000, 3), 0);
                }

                // Base bonus for every movement, giving some weight to streams.
                distanceAddition += 12.5 * Math.Min(Math.Abs(catchNext.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                    / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 6) / sqrtStrain;
            }

            // Repetitive spacing nerf: it is harder to catch irregular distances, especially in case of non-hyperdashes which require one to "miss the rhythm on purpose".
            // Penalty is given based on the ratio between the current (incoming) normalised spacing and the previous normalised spacing.
            double repetitiveSpacingWeight = 0;

            var nextFlow = catchNext.Flow;
            var currentFlow = catchCurrent.Flow;

            // Penalty is applied only to the direction changes.
            if (nextFlow.IsFirst(catchNext))
            {
                for (int i = 0; i < Math.Min(currentFlow.Index, 8); i++)
                {
                    var prevFlow = currentFlow.Previous(i);

                    if (prevFlow != null && prevFlow.Distance != 0)
                    {
                        double currentSpacing = Math.Abs(currentFlow.Distance / currentFlow.StrainTime);
                        double prevSpacing = Math.Abs(prevFlow.Distance / prevFlow.StrainTime);

                        double relativeDifference = Math.Abs(currentSpacing / prevSpacing - 1);
                        relativeDifference = Math.Max(0, relativeDifference - 0.05); // Handle inaccuracies in repetitive patterns.

                        if (relativeDifference <= 0.15)
                            repetitiveSpacingWeight += (1.0 - relativeDifference / 0.15) * DiffUtils.Pow(0.9, i);
                    }
                }
            }

            double repetitiveSpacingPenalty = catchNext.BaseObject.HyperDash ? DiffUtils.Pow(0.97, repetitiveSpacingWeight) : DiffUtils.Pow(0.93, repetitiveSpacingWeight);
            distanceAddition *= repetitiveSpacingPenalty;

            // Linear spacing nerf.
            // It reduces the difficulty of streams (series of notes facing the same direction) with consecutive notes preserving the angle.
            int linearSpacingCount = 0;

            for (int i = 0; i < Math.Min(catchNext.Index, 10); i++)
            {
                var catchPrevObj = (CatchDifficultyHitObject)catchNext.Previous(i);

                // Only same direction movements matter as they do not take any additional inputs.
                if (catchNext.Direction != catchPrevObj.Direction || catchNext.Distance == 0 || catchPrevObj.Distance == 0)
                    break;

                double nextSpacing = Math.Abs(catchNext.Distance / catchNext.StrainTime);
                double prevSpacing = Math.Abs(catchPrevObj.Distance / catchPrevObj.StrainTime);

                double relativeDifference = Math.Abs(nextSpacing / prevSpacing - 1);

                if (relativeDifference > 0.05)
                    break;

                linearSpacingCount++;
            }

            distanceAddition *= DiffUtils.Pow(0.6, linearSpacingCount);

            // Bonus for edge dashes.
            if (catchNext.LastObject.DistanceToHyperDash <= 30.0f)
            {
                if (!catchNext.LastObject.HyperDash)
                {
                    // Precision value (time window to hit the note) is lower for higher CS and for fixed distance to the hyperdash.
                    edgeDashBonus += 3.0 + 0.1 * DiffUtils.Pow(circleSize, 0.8);
                    if (catchCurrent.LastObject.HyperDash) // Edge dashes are harder when they appear right after a hyperdash.
                        edgeDashBonus *= 1.4;
                    if (catchNext.Direction == catchCurrent.Direction)
                        edgeDashBonus *= 0.75;
                }

                distanceAddition *= 1.0 + edgeDashBonus * (30 - catchNext.LastObject.DistanceToHyperDash) / 30;
            }

            // There is an edge case where horizontal back and forth sliders create "buzz" patterns which are repeated "movements" with a distance lower than
            // the platter's width but high enough to be considered a movement due to the absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH offsets
            // We are detecting this exact scenario. The first back and forth is counted but all subsequent ones are nullified.
            // To achieve that, we need to store the exact distances between consecutive objects.
            if (Math.Abs(catchNext.Distance) <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2
                && Precision.AlmostEquals(catchNext.Distance, -catchCurrent.Distance)
                && Precision.AlmostEquals(catchCurrent.Distance, -catchLast.Distance)
                && Precision.AlmostEquals(catchNext.StrainTime, catchCurrent.StrainTime)
                && Precision.AlmostEquals(catchCurrent.StrainTime, catchLast.StrainTime))
                distanceAddition = 0;

            return distanceAddition / weightedStrainTime;
        }
    }
}
