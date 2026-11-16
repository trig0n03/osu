// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public class Flow
    {
        private readonly IReadOnlyList<Flow> flows;
        private readonly List<CatchDifficultyHitObject> hitObjects;

        public readonly int Index;
        public readonly int Direction;

        public float Distance { get; private set; }
        public double StrainTime { get; private set; }

        public Flow(List<CatchDifficultyHitObject> hitObjects, List<Flow> flows, int index)
        {
            this.flows = flows;
            this.hitObjects = hitObjects;
            Index = index;
            Direction = hitObjects.First().Direction;
            Distance = hitObjects.Aggregate(0.0f, (acc, h) => acc + h.Distance);
            StrainTime = Math.Max(hitObjects.Aggregate(0.0, (acc, h) => acc + h.DeltaTime), 40);
        }

        public void Add(CatchDifficultyHitObject hitObject)
        {
            hitObjects.Add(hitObject);
            Distance = hitObjects.Aggregate(0.0f, (acc, h) => acc + h.Distance);
            StrainTime = Math.Max(hitObjects.Aggregate(0.0, (acc, h) => acc + h.DeltaTime), 40);
        }

        // Return true whenever the given object is a direction change.
        // We treat the last object of the map as a direction change.
        public bool IsLast(CatchDifficultyHitObject hitObject) => hitObject.Index == hitObjects.Last().Index;

        public Flow? Previous(int backwardsIndex)
        {
            int index = Index - (backwardsIndex + 1);
            return index >= 0 && index < flows.Count ? flows[index] : null;
        }
    }
}
