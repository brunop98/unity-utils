using UnityEngine;

public class MeshRaycasterCPU : MonoBehaviour
{
    public bool drawMesh;
    public float _det;

    [Space]
    public Transform ray;

    public float hit;

    public GameObject meshObject;
    private Vector3 vert0;
    private Vector3 vert1;
    private Vector3 vert2;
    private Vector3 tmp1;
    private Vector3 tmp2;

    private Vector3 dir => ray.forward;

    //https://github.com/Unity-Technologies/UnityCsReference/blob/9034442437e6b5efe28c51d02e978a96a3ce5439/Editor/Mono/Utils/MathUtils.cs#L313

    private void OnDrawGizmos()
    {
        
        if(meshObject == null) return;
        if(!drawMesh) return;
        
        //draw ray
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(ray.position, dir);

        
        
        Mesh mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
        Matrix4x4 matrix = meshObject.transform.localToWorldMatrix;


        Vector3[] vertices = new Vector3[mesh.vertices.Length];

        for (int index = 0; index < vertices.Length; index++)
        {
            Vector3 vertex = matrix.MultiplyPoint3x4(mesh.vertices[index]);
            vertices[index] = vertex;
        }

        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            vert0 = vertices[mesh.triangles[i]];
            vert1 = vertices[mesh.triangles[i + 1]];
            vert2 = vertices[mesh.triangles[i + 2]];

            DrawVertexs();

            tmp1 = vert1 - vert0;
            tmp2 = vert2 - vert0;

            DrawLines();

            Vector3 tmp4 = Vector3.Cross(dir, tmp2);

            _det = Vector3.Dot(tmp1, tmp4);

            if (_det < Mathf.Epsilon) continue;

            Vector3 temp3 = ray.position - vert0;
            float u = Vector3.Dot(temp3, tmp4);

            if (u < 0.0f || u > _det) continue;

            tmp4 = Vector3.Cross(temp3, tmp1);

            float v = Vector3.Dot(dir, tmp4);

            if (v < 0.0f || u + v > _det)
                continue;

            hit = Vector3.Dot(tmp2, tmp4) * (1.0f / _det);
         

            //draw hit point
            Vector3 p = ray.position + (dir * hit);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(p, 0.01f);

        }
    }

    private void DrawLines()
    {
        if(!drawMesh) return;
        //draw line
        Gizmos.color = Color.green;
        Gizmos.DrawRay(vert0, tmp1);
        Gizmos.DrawRay(vert0, tmp2);
        Gizmos.DrawLine(vert1, vert2);
    }

    private void DrawVertexs()
    {
        if(!drawMesh) return;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(vert0, 0.01f);
        Gizmos.DrawSphere(vert1, 0.01f);
        Gizmos.DrawSphere(vert2, 0.01f);
    }
}