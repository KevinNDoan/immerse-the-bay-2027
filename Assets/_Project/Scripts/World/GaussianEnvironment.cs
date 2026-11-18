using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zap.World
{
    // Native mobile Gaussian clouds. Ordinary MeshRenderer submission keeps URP/XR
    // stereo handling intact; only the vertex positions come from structured buffers.
    public sealed class GaussianEnvironment : MonoBehaviour
    {
        public const int MaximumCount = 500000;
        public int Count { get; private set; }
        private Mesh _mesh;
        private Material _material;
        private ComputeBuffer _points, _order;
        private Vector3[] _centers;
        private DepthSorter _sorter;
        private Task<int[]> _sort;
        private float _nextSort;
        private Vector3 _lastForward;
        private double _frameTime;
        private int _frames;
        private float _reportAt;

        public static float[] Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) throw new InvalidDataException("Missing splat data.");
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (reader.ReadUInt32() != 0x3150535a) throw new InvalidDataException("Invalid splat header.");
            int count = reader.ReadInt32();
            if (count < 1 || count > MaximumCount || bytes.Length != 8L + count * 64L)
                throw new InvalidDataException("Invalid splat count.");
            var points = new float[count * 16]; Buffer.BlockCopy(bytes,8,points,0,bytes.Length-8);
            for (int i=0;i<points.Length;i++)
                if (float.IsNaN(points[i]) || float.IsInfinity(points[i]) || Mathf.Abs(points[i]) > 100000000)
                    throw new InvalidDataException("Invalid splat value.");
            for (int i=0;i<count;i++)
            {
                int k=i*16;
                if (points[k+3]<0 || points[k+3]>1 || points[k+4]<0 || points[k+7]<0 || points[k+9]<0)
                    throw new InvalidDataException("Invalid splat covariance or opacity.");
            }
            return points;
        }

        public void Load(byte[] bytes)
        {
            var values = Decode(bytes); Count=values.Length/16;
            if (SystemInfo.graphicsShaderLevel < 45) throw new InvalidDataException("This device cannot render the splat environment.");
            var shader=Shader.Find("Zap/GaussianEnvironment");
            if (shader==null || !shader.isSupported) throw new InvalidDataException("The splat shader is unavailable.");
            _material=new Material(shader);
            _points=new ComputeBuffer(Count,64); _points.SetData(values);
            _order=new ComputeBuffer(Count,4);
            var order=new int[Count]; _centers=new Vector3[Count];
            var vertices=new Vector3[Count*4]; var indices=new int[Count*6];
            var bounds=new Bounds(new Vector3(values[0],values[1],values[2]), Vector3.zero);
            for(int i=0;i<Count;i++)
            {
                order[i]=i; int k=i*16; var p=new Vector3(values[k],values[k+1],values[k+2]);
                _centers[i]=p; bounds.Encapsulate(p);
                for(int v=0;v<4;v++) vertices[i*4+v]=p;
                int t=i*6,b=i*4; indices[t]=b; indices[t+1]=b+1; indices[t+2]=b+2;
                indices[t+3]=b+2; indices[t+4]=b+1; indices[t+5]=b+3;
            }
            bounds.Expand(20);
            _order.SetData(order); _material.SetBuffer("_Splats",_points); _material.SetBuffer("_Order",_order);
            _sorter=new DepthSorter(_centers);
            _mesh=new Mesh {name="Mobile Gaussian quads",indexFormat=IndexFormat.UInt32};
            _mesh.vertices=vertices; _mesh.triangles=indices; _mesh.bounds=bounds; _mesh.UploadMeshData(true);
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            _reportAt=Time.unscaledTime+10;
            Debug.Log($"[Splats] Loaded {Count} native Gaussians; no textured mesh export.");
        }

        // Linear-time bucket sort on a worker, shared between eyes. Sorting only
        // changes with view direction; translation adds a constant to every depth.
        public static int[] SortBackToFront(Vector3[] centers, Vector3 forward)
            => new DepthSorter(centers).Sort(forward);

        private sealed class DepthSorter
        {
            const int Bins=32768;
            readonly Vector3[] centers;
            readonly float[] depths;
            readonly int[] counts=new int[Bins],keys,result;
            public DepthSorter(Vector3[] positions)
            { centers=positions;depths=new float[positions.Length];keys=new int[positions.Length];result=new int[positions.Length]; }
            public int[] Sort(Vector3 forward)
            {
                Array.Clear(counts,0,counts.Length);
                float min=float.MaxValue,max=float.MinValue;
                for(int i=0;i<centers.Length;i++) { float d=Vector3.Dot(centers[i],forward); depths[i]=d; min=Mathf.Min(min,d);max=Mathf.Max(max,d); }
                float scale=(Bins-1)/Mathf.Max(.001f,max-min);
                for(int i=0;i<keys.Length;i++) { int key=Mathf.Clamp((int)((max-depths[i])*scale),0,Bins-1); keys[i]=key; counts[key]++; }
                int offset=0;
                for(int b=0;b<Bins;b++) { int n=counts[b];counts[b]=offset;offset+=n; }
                for(int i=0;i<keys.Length;i++) result[counts[keys[i]]++]=i;
                return result;
            }
        }

        private void LateUpdate()
        {
            if (_order==null || Camera.main==null) return;
            if (_sort!=null && _sort.IsCompleted)
            {
                if (_sort.Status==TaskStatus.RanToCompletion) _order.SetData(_sort.Result);
                else Debug.LogWarning("[Splats] Depth sorting failed.");
                _sort=null;
            }
            var forward=transform.InverseTransformDirection(Camera.main.transform.forward);
            if (_sort==null && Time.unscaledTime>=_nextSort && (_lastForward==Vector3.zero || Vector3.Angle(forward,_lastForward)>.75f))
            {
                _lastForward=forward; _nextSort=Time.unscaledTime+.08f;
                var sorter=_sorter; _sort=Task.Run(()=>sorter.Sort(forward));
            }
            _frameTime+=Time.unscaledDeltaTime;_frames++;
            if(Time.unscaledTime>=_reportAt)
            {
                Debug.Log($"[Splats] {Count} points, application frame average {_frameTime*1000/Math.Max(1,_frames):F1} ms (includes headset sleep).");
                _frameTime=0;_frames=0;_reportAt=Time.unscaledTime+10;
            }
        }

        private void OnDestroy()
        {
            // A pending sort owns only managed immutable positions, never GPU resources.
            _points?.Release();_order?.Release();_points=null;_order=null;
            if(_mesh!=null) Destroy(_mesh); if(_material!=null) Destroy(_material);
        }
    }
}
