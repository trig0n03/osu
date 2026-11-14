// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public class CatchDifficultyHitObject : DifficultyHitObject
    {
        public const float NORMALIZED_HALF_CATCHER_WIDTH = 41.0f;
        private const float absolute_player_positioning_error = 16.0f;

        public new PalpableCatchHitObject BaseObject => (PalpableCatchHitObject)base.BaseObject;

        public new PalpableCatchHitObject LastObject => (PalpableCatchHitObject)base.LastObject;

        public CatchDifficultyHitObject Prev => (CatchDifficultyHitObject)Previous(0);

        public CatchDifficultyHitObject Next => (CatchDifficultyHitObject)Next(0);

        /// <summary>
        /// Normalized position of <see cref="BaseObject"/>.
        /// </summary>
        public readonly float NormalizedPosition;

        /// <summary>
        /// Normalized position of <see cref="LastObject"/>.
        /// </summary>
        public readonly float LastNormalizedPosition;

        /// <summary>
        /// Normalized position of the player required to catch <see cref="BaseObject"/>, assuming the player moves as little as possible.
        /// </summary>
        public float PlayerPosition { get; private set; }

        /// <summary>
        /// Normalized position of the player after catching <see cref="LastObject"/>.
        /// </summary>
        public float LastPlayerPosition { get; private set; }

        /// <summary>
        /// Normalized distance between <see cref="LastPlayerPosition"/> and <see cref="PlayerPosition"/>.
        /// </summary>
        /// <remarks>
        /// The sign of the value indicates the direction of the movement: negative is left and positive is right.
        /// </remarks>
        public float DistanceMoved { get; private set; }

        /// <summary>
        /// Normalized distance between <see cref="LastNormalizedPosition"/> at its <see cref="NormalizedPosition"/>.
        /// </summary>
        public float Distance { get; private set; }

        /// <summary>
        /// Normalised width of the catcher based on NORMALIZED_HALF_CATCHER_WIDTH
        /// </summary>
        public float NormalizedCatcherWidth { get; private set; }

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="CatchDifficultyHitObject"/>, with a minimum of 40ms.
        /// </summary>
        public readonly double StrainTime;

        /// <summary>
        /// The sign of the <see cref="Distance"/>: negative is left and positive is right (speaking of the relation between previous and current <see cref="CatchDifficultyHitObject"/>).
        /// </summary>
        public int Direction;

        /// <summary>
        /// The sign of the <see cref="DistanceMoved"/>: negative is left and positive is right (speaking of the relation between previous and current <see cref="CatchDifficultyHitObject"/>).
        /// </summary>
        public int MovementDirection;

        /// <summary>
        /// Subsequence of consecutive objects facing the same direction that the current <see cref="CatchDifficultyHitObject"/> is a part of.
        /// The last <see cref="CatchDifficultyHitObject"/> of each flow is a direction change.
        /// </summary>
        public Flow Flow;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, float halfCatcherWidth, List<DifficultyHitObject> objects, int index, List<Flow> flows)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            // We will scale everything by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = NORMALIZED_HALF_CATCHER_WIDTH / halfCatcherWidth;

            NormalizedPosition = BaseObject.EffectiveX * scalingFactor;
            LastNormalizedPosition = LastObject.EffectiveX * scalingFactor;

            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            StrainTime = Math.Max(40, DeltaTime);

            NormalizedCatcherWidth = NORMALIZED_HALF_CATCHER_WIDTH * 2;

            setMovementState();

            // Divide all hit objects into flows, i.e. series of consecutive notes facing the same direction

            if (Prev != null && Prev.Flow.Direction == MovementDirection)
            {
                Prev.Flow.Add(this);
                Flow = Prev.Flow;
            }
            else // Previous object is a direction change (or null)
            {
                Flow = new Flow(new List<CatchDifficultyHitObject> { this }, flows, flows.Count);
                flows.Add(Flow);
            }
        }

        private void setMovementState()
        {
            LastPlayerPosition = Index == 0 ? LastNormalizedPosition : Prev.PlayerPosition;

            PlayerPosition = Math.Clamp(
                LastPlayerPosition,
                NormalizedPosition - (NORMALIZED_HALF_CATCHER_WIDTH - absolute_player_positioning_error),
                NormalizedPosition + (NORMALIZED_HALF_CATCHER_WIDTH - absolute_player_positioning_error)
            );

            DistanceMoved = PlayerPosition - LastPlayerPosition;
            Distance = NormalizedPosition - LastNormalizedPosition;

            // After a hyperdash we ARE in the correct position. Always!
            if (LastObject.HyperDash)
                PlayerPosition = NormalizedPosition;

            Direction = Math.Sign(Distance);
            MovementDirection = Math.Sign(DistanceMoved);
        }
    }
}
