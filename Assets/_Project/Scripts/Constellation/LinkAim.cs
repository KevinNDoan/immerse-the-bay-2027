using System.Collections.Generic;
using UnityEngine;

namespace Zap.Constellation
{
    public static class LinkAim
    {
        const float MinDistance = 0.05f;
        const float DirectionEpsilonSqr = 1e-10f;

        // Index of the position closest in angle to the ray, among those within maxAngleDegrees of it and within maxDistance of origin. -1 when none.
        // skip is an index to ignore, or -1. Positions closer than 0.05 m to origin are ignored.
        public static int Nearest(IReadOnlyList<Vector3> positions, Vector3 origin, Vector3 direction, float maxAngleDegrees, float maxDistance, int skip)
        {
            if (positions == null) return -1;
            // Written as "not greater" so a direction that is not a number is rejected too.
            if (!(direction.sqrMagnitude > DirectionEpsilonSqr)) return -1;

            Vector3 forward = direction.normalized;
            int best = -1;
            float bestAngle = maxAngleDegrees;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == skip) continue;
                Vector3 offset = positions[i] - origin;
                float distance = offset.magnitude;
                if (distance < MinDistance || distance > maxDistance) continue;

                float angle = Vector3.Angle(forward, offset);
                if (angle > maxAngleDegrees) continue;
                if (best >= 0 && angle >= bestAngle) continue;
                best = i;
                bestAngle = angle;
            }

            return best;
        }
    }
}
