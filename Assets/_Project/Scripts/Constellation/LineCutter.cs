using UnityEngine;

namespace Zap.Constellation
{
    public static class LineCutter
    {
        const float LengthEpsilonSqr = 1e-10f;
        const float ParallelEpsilon = 1e-6f;

        public static bool SweepCrosses(
            Vector3 previousOrigin, Vector3 previousDirection,
            Vector3 currentOrigin, Vector3 currentDirection, float range,
            Vector3 lineStart, Vector3 lineEnd, out float distanceFromOrigin)
        {
            distanceFromOrigin = 0f;
            if (range <= 0f) return false;
            if (previousDirection.sqrMagnitude <= LengthEpsilonSqr || currentDirection.sqrMagnitude <= LengthEpsilonSqr)
                return false;

            Vector3 p0 = previousOrigin;
            Vector3 p1 = previousOrigin + previousDirection.normalized * range;
            Vector3 c1 = currentOrigin + currentDirection.normalized * range;
            Vector3 c0 = currentOrigin;
            Vector3 segment = lineEnd - lineStart;

            bool crossed = false;
            float best = 0f;
            if (SegmentCrossesTriangle(lineStart, segment, p0, p1, c1, out Vector3 hit))
            {
                best = Vector3.Distance(currentOrigin, hit);
                crossed = true;
            }

            if (SegmentCrossesTriangle(lineStart, segment, p0, c1, c0, out hit))
            {
                float distance = Vector3.Distance(currentOrigin, hit);
                if (!crossed || distance < best) best = distance;
                crossed = true;
            }

            if (!crossed) return false;
            distanceFromOrigin = best;
            return true;
        }

        static bool SegmentCrossesTriangle(
            Vector3 lineStart, Vector3 segment,
            Vector3 v0, Vector3 v1, Vector3 v2,
            out Vector3 hit)
        {
            hit = default;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 pvec = Vector3.Cross(segment, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (det > -ParallelEpsilon && det < ParallelEpsilon) return false;

            float invDet = 1f / det;
            Vector3 tvec = lineStart - v0;
            float u = Vector3.Dot(tvec, pvec) * invDet;
            if (u < 0f || u > 1f) return false;

            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float v = Vector3.Dot(segment, qvec) * invDet;
            if (v < 0f || u + v > 1f) return false;

            float t = Vector3.Dot(edge2, qvec) * invDet;
            if (t < 0f || t > 1f) return false;

            hit = lineStart + segment * t;
            return true;
        }
    }
}
