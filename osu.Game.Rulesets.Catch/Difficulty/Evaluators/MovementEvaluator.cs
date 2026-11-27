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
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            var catchLast = (CatchDifficultyHitObject?)current.Previous();
            var catchLastLast = (CatchDifficultyHitObject?)current.Previous(1);

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            double clockRate = current.ClockRate;

            const double time_shift = 16 / clockRate; // We add 16 milliseconds to each StrainTime to decrease the difference between the fastest timings.

            double weightedStrainTime = catchCurrent.StrainTime + time_shift;

            double distanceAddition = (DiffUtils.Pow(Math.Abs(catchCurrent.DistanceMoved), 1.3) / 510);

            // Direction change bonus.
            if (Math.Abs(catchCurrent.DistanceMoved) > 0.1)
            {
                if (catchLast != null && Math.Abs(catchLast.DistanceMoved) > 0.1 && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
                {
                    const double direction_change_bonus = 21;
                    const double full_distance_cutoff = 50;

                    double bonusFactor = Math.Min(full_distance_cutoff, Math.Abs(catchCurrent.DistanceMoved)) / full_distance_cutoff;
                    double antiflowFactor = Math.Min(full_distance_cutoff, Math.Abs(catchLast.DistanceMoved)) / full_distance_cutoff;

                    distanceAddition += direction_change_bonus / Math.Sqrt(catchLast.StrainTime + time_shift) * bonusFactor * antiflowFactor * Math.Max(1 - DiffUtils.Pow(weightedStrainTime / 1000, 3), 0);
                }

                // Base bonus for every movement, giving some weight to streams.
                distanceAddition += 12.5 * Math.Min(Math.Abs(catchCurrent.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                    / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 6) / Math.Sqrt(weightedStrainTime);
            }

            // Linear spacing nerf.
            int linearSpacingCount = 0;

            for (int i = 0; i < Math.Min(current.Index, 10); i++)
            {
                var catchPrevObj = (CatchDifficultyHitObject)catchCurrent.Previous(i)!;

                // Only same direction movements matter as they do not take any additional inputs.
                if (Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchPrevObj.DistanceMoved) || catchCurrent.DistanceMoved == 0 || catchPrevObj.DistanceMoved == 0)
                    break;

                double currentSpacing = Math.Abs(catchCurrent.DistanceMoved / catchCurrent.StrainTime);
                double prevSpacing = Math.Abs(catchPrevObj.DistanceMoved / catchPrevObj.StrainTime);

                double relativeDifference = Math.Abs(currentSpacing / prevSpacing - 1);

                if (relativeDifference > 0.05)
                    break;

                linearSpacingCount++;
            }

            distanceAddition *= DiffUtils.Pow(0.7, linearSpacingCount);

            // Bonus for edge dashes.
            double edgeDashBonus = 0;

            if (catchCurrent.LastObject.DistanceToHyperDash <= 20f)
            {
                if (!catchCurrent.LastObject.HyperDash)
                    edgeDashBonus += 5.7;

                distanceAddition *= 1 + edgeDashBonus * ((20 - catchCurrent.LastObject.DistanceToHyperDash) / 20)
                                                        * DiffUtils.Pow((Math.Min(catchCurrent.StrainTime * clockRate, 265) / 265), 1.5); // Edge Dashes are easier at lower ms values
            }

            // There is an edge case where horizontal back and forth sliders create "buzz" patterns which are repeated "movements" with a distance lower than
            // the platter's width but high enough to be considered a movement due to the absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH offsets
            // We are detecting this exact scenario. The first back and forth is counted but all subsequent ones are nullified.
            // To achieve that, we need to store the exact distances (distance ignoring absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH)
            if (catchLast != null && catchLastLast != null
                                  && Math.Abs(catchCurrent.ExactDistanceMoved) <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2
                                  && Precision.AlmostEquals(catchCurrent.ExactDistanceMoved, -catchLast.ExactDistanceMoved)
                                  && Precision.AlmostEquals(catchLast.ExactDistanceMoved, -catchLastLast.ExactDistanceMoved)
                                  && Precision.AlmostEquals(catchCurrent.StrainTime, catchLast.StrainTime)
                                  && Precision.AlmostEquals(catchLast.StrainTime, catchLastLast.StrainTime))
                distanceAddition = 0;

            return distanceAddition / weightedStrainTime;
        }
    }
}
