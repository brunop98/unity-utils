using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public static class MeshRaycaster
{

    private static int kernelIndex;
    private static uint threadGroupSizeX;
    private static uint threadGroupSizeY;
    private static uint threadGroupSizeZ;

    private static ComputeShader raycastShader;
    private const string RaycastShaderName = "PrefabBrush_MeshRaycaster";

    private static ComputeBuffer trianglesBuffer;
    private static ComputeBuffer vertexBuffer;
    private static ComputeBuffer resultBufferHits;
    private static ComputeBuffer resultNormalsBuffer;
    private static float[] hitDistances;
    private static Vector3[] resultNormals;
    private static int sizeX1;
    private static readonly int Epsilon = Shader.PropertyToID("epsilon");
    private static readonly int WorldToLocalMatrix = Shader.PropertyToID("worldToLocalMatrix");
    private static readonly int VertexBuffer = Shader.PropertyToID("vertexBuffer");
    private static readonly int TriangleBuffer = Shader.PropertyToID("triangleBuffer");
    private static readonly int ResultHits = Shader.PropertyToID("resultHits");
    private static readonly int RayOrigin = Shader.PropertyToID("rayOrigin");
    private static readonly int RayDirection = Shader.PropertyToID("rayDirection");
    private static readonly int ResultNormals = Shader.PropertyToID("resultNormals");

    public static bool Raycast(Ray r, Mesh m, Matrix4x4 matrix, out MeshRaycastResult result)
    {
        Initialize();

        if (m.vertices.Length == 0)
        {
            result = new MeshRaycastResult();
            return false;
        }

#if HARPIA_DEBUG
        Profiler.BeginSample("Prefab Brush - Mesh Raycaster");
#endif
        
        //Inputs
        if (vertexBuffer == null)
        {
#if HARPIA_DEBUG
            Debug.Log($"Initializing raycastShader - {m.vertices.Length} vertices");
#endif
            
            raycastShader.SetFloat(Epsilon, Mathf.Epsilon);
            raycastShader.SetMatrix(WorldToLocalMatrix, matrix);
            
            vertexBuffer = new ComputeBuffer(m.vertices.Length, sizeof(float) * 3);
            vertexBuffer.SetData(m.vertices);
            raycastShader.SetBuffer(kernelIndex, VertexBuffer, vertexBuffer);

            trianglesBuffer = new ComputeBuffer(m.triangles.Length, sizeof(int));
            trianglesBuffer.SetData(m.triangles);
            raycastShader.SetBuffer(kernelIndex, TriangleBuffer, trianglesBuffer);
            
            hitDistances = new float[m.triangles.Length / 3];
            resultBufferHits = new ComputeBuffer(hitDistances.Length, sizeof(float));
            resultBufferHits.SetData(hitDistances);
            raycastShader.SetBuffer(kernelIndex, ResultHits, resultBufferHits);
        
            resultNormals = new Vector3[hitDistances.Length];
            resultNormalsBuffer = new ComputeBuffer(hitDistances.Length, sizeof(float) * 3);
            resultNormalsBuffer.SetData(resultNormals);
            raycastShader.SetBuffer(kernelIndex, ResultNormals, resultNormalsBuffer);
            
            sizeX1 = Mathf.CeilToInt((hitDistances.Length / threadGroupSizeX) + 1);
        }
        
        raycastShader.SetVector(RayOrigin, r.origin);
        raycastShader.SetVector(RayDirection, r.direction);
        
        raycastShader.Dispatch(kernelIndex, sizeX1, (int)threadGroupSizeY, (int)threadGroupSizeZ);

        resultBufferHits.GetData(hitDistances);
        resultNormalsBuffer.GetData(resultNormals);

        float maxDistance = Mathf.Infinity;
        int resultIndex = -1;
        for (int index = hitDistances.Length - 1; index >= 0; index--)
        {
            float distance = hitDistances[index];
            if (distance >= 1_000_000) continue;
            if (distance < maxDistance)
            {
                maxDistance = distance;
                resultIndex = index;
            }
        }

        //No collision ray
        if (resultIndex == -1)
        {
            result = new MeshRaycastResult();
            return false;
        }

        Vector3 pos = r.origin + r.direction * maxDistance;

        result = new MeshRaycastResult(pos, resultNormals[resultIndex], maxDistance);

#if HARPIA_DEBUG
        Profiler.EndSample();
#endif

        return true;
    }

    private static void LogTriangules(int[] mTriangles)
    {
        string a = "triangules ";
        foreach (int mTriangle in mTriangles)
        {
            a += $" {mTriangle}";
        }
        Debug.Log($"{a}");
    }

    public static void Dispose()
    {
        trianglesBuffer?.Dispose();
        vertexBuffer?.Dispose();
        resultBufferHits?.Dispose();
        resultNormalsBuffer?.Dispose();
        
        hitDistances = null;
        trianglesBuffer = null;
        vertexBuffer = null;
        resultBufferHits = null;
        resultNormalsBuffer = null;
    }

    private static void LogBufferData<T>(ComputeBuffer orinalBuffer, int stride, string name)
    {
        Array data = new T[orinalBuffer.count * stride];
        orinalBuffer.GetData(data);
        Debug.Log($"{name} Data Length {data.Length}, stride {orinalBuffer.stride}:");

        for (int i = 0; i < data.Length; i += stride)
        {
            string a = "Data ";
            for (int j = 0; j < stride; j++)
            {
                a += $" {data.GetValue(i + j)}";
                if (j < stride - 1) a += ",";
            }
            Debug.Log(a);
        }
    }

    public static void Initialize()
    {
        if (raycastShader != null) return;

        FindShader();
        kernelIndex = raycastShader.FindKernel("MeshRaycastCS");
        raycastShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizeX, out threadGroupSizeY, out threadGroupSizeZ);
    }

    static void FindShader()
    {
        //find a asset called raycastShaderName
        string[] guids = AssetDatabase.FindAssets(RaycastShaderName);

        if (guids.Length == 0)
        {
            Debug.LogError("Mesh Raycaster shader not found");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        raycastShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
    }
}

public struct MeshRaycastResult
{
    public Vector3 position;
    public Vector3 normal;
    public float distance;

    //constructor
    public MeshRaycastResult(Vector3 position, Vector3 normal, float distance)
    {
        this.position = position;
        this.normal = normal;
        this.distance = distance;
    }

    public RaycastHit ToHitInfo()
    {
        RaycastHit hit = new RaycastHit();
        hit.point = position;
        hit.normal = normal;
        hit.distance = distance;
        return hit;
    }
}