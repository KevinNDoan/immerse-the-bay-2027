using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zap.Constellation
{
    // Parametric geometry, not topic templates. Repeated structures use one draw per part.
    public static class StudyGeometry
    {
        public static bool IsPath(string shape) => shape == "tube" || shape == "helix" || shape == "links";

        public static Mesh Build(StudyPartData part, StudyPartData[] parts = null)
        {
            Mesh source;
            Vector3 size = IsPath(part.shape) ? Vector3.one : part.scale;
            if (part.shape == "links")
            {
                var links = part.links;
                if (links == null || parts == null || links.from < 0 || links.to < 0 || links.from >= parts.Length || links.to >= parts.Length) return null;
                int count = Mathf.Clamp(links.count, 2, 32);
                var segments = new CombineInstance[count];
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / (count - 1);
                    Vector3 a = PointOnPath(parts[links.from], t), b = PointOnPath(parts[links.to], t);
                    segments[i] = new CombineInstance { mesh = Tube(new[] { Vector3.Lerp(a,b,links.start), Vector3.Lerp(a,b,links.end) },
                        Mathf.Clamp(part.scale.x*.5f,.004f,.08f)), transform = Matrix4x4.identity };
                }
                source = new Mesh { indexFormat = IndexFormat.UInt32 }; source.CombineMeshes(segments, true, true);
                foreach (var segment in segments) Object.Destroy(segment.mesh);
            }
            else if (part.shape == "helix")
            {
                var curve = part.curve ?? new StudyCurveData();
                int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(curve.turns) * 40), 40, 240);
                var points = new Vector3[count + 1];
                for (int i = 0; i <= count; i++)
                {
                    float t = (float)i / count, a = (t * curve.turns * 360 + curve.phase) * Mathf.Deg2Rad;
                    points[i] = new Vector3(Mathf.Cos(a) * curve.radius, (t - .5f) * curve.height, Mathf.Sin(a) * curve.radius);
                }
                source = Tube(points, Mathf.Clamp(part.scale.x * .5f, .004f, .08f));
            }
            else if (part.shape == "ring")
            {
                var points = new Vector3[65];
                for (int i = 0; i < points.Length; i++)
                { float a = i * Mathf.PI * 2 / 64; points[i] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .48f; }
                source = Tube(points, .02f);
            }
            else if (part.shape == "tube")
            {
                if (part.points == null || part.points.Length < 2 || part.points.Length > 48) return null;
                source = Tube(part.points, Mathf.Clamp(part.scale.x * .5f, .004f, .08f));
            }
            else
            {
                PrimitiveType type;
                switch (part.shape)
                {
                    case "sphere": type = PrimitiveType.Sphere; break;
                    case "box": type = PrimitiveType.Cube; break;
                    case "cylinder": type = PrimitiveType.Cylinder; size.y *= .5f; break;
                    default: return null;
                }
                var primitive = GameObject.CreatePrimitive(type);
                primitive.SetActive(false);
                source = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                Object.Destroy(primitive);
            }
            var repeat = part.repeat ?? new StudyRepeatData();
            int copies = Mathf.Clamp(repeat.count, 1, IsPath(part.shape) ? 4 : 32);
            var instances = new CombineInstance[copies];
            for (int i = 0; i < copies; i++)
                instances[i] = new CombineInstance { mesh = source, transform =
                    Matrix4x4.TRS(repeat.step * i, Quaternion.Euler(0, repeat.angle * i, 0), Vector3.one) *
                    Matrix4x4.TRS(part.position, Quaternion.Euler(part.rotation), size) };
            var mesh = new Mesh { name = part.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(instances, true, true); mesh.RecalculateBounds();
            Object.Destroy(source);
            return mesh;
        }

        public static Vector3 PointOnPath(StudyPartData part, float t)
        {
            Vector3 point;
            if (part.shape == "helix")
            {
                var c = part.curve ?? new StudyCurveData();
                float a = (t*c.turns*360+c.phase)*Mathf.Deg2Rad;
                point = new Vector3(Mathf.Cos(a)*c.radius, (t-.5f)*c.height, Mathf.Sin(a)*c.radius);
            }
            else if (part.points != null && part.points.Length > 1)
            {
                float index = t*(part.points.Length-1); int i = Mathf.Min(Mathf.FloorToInt(index),part.points.Length-2);
                point = Vector3.Lerp(part.points[i],part.points[i+1],index-i);
            }
            else point = Vector3.zero;
            return part.position + Quaternion.Euler(part.rotation)*point;
        }

        public static Mesh Tube(Vector3[] points, float radius)
        {
            const int sides = 12;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var indices = new List<int>();
            Vector3 lastRight = Vector3.zero;
            for (int p = 0; p < points.Length; p++)
            {
                Vector3 tangent = points[Mathf.Min(p + 1, points.Length - 1)] - points[Mathf.Max(p - 1, 0)];
                if (tangent.sqrMagnitude < .000001f) tangent = Vector3.up;
                tangent.Normalize();
                Vector3 right = p == 0 ? Vector3.Cross(tangent, Mathf.Abs(tangent.y) > .9f ? Vector3.right : Vector3.up).normalized
                    : Vector3.ProjectOnPlane(lastRight, tangent).normalized;
                if (right.sqrMagnitude < .1f) right = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(tangent, right).normalized; lastRight = right;
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    Vector3 normal = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                    vertices.Add(points[p] + normal * radius); normals.Add(normal);
                    if (p == points.Length - 1) continue;
                    int x = p * sides + s, y = p * sides + (s + 1) % sides;
                    indices.Add(x); indices.Add(y); indices.Add(x + sides);
                    indices.Add(y); indices.Add(y + sides); indices.Add(x + sides);
                }
            }
            var mesh = new Mesh { name = "Smooth study curve", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
