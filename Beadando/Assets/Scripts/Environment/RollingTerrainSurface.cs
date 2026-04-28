using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class RollingTerrainSurface : MonoBehaviour
{
    public float heightScale = 1.4f;
    public float outerHillStrength = 1f;
    public float centralFlatRadius = 0.33f;
    public Vector2 noiseTiling = new Vector2(0.9f, 0.9f);
    public Vector2 noiseOffset = new Vector2(11.7f, 23.4f);

    [SerializeField] private Mesh sourceMesh;
    private Mesh generatedMesh;

    private void OnEnable()
    {
        Regenerate();
    }

    private void OnValidate()
    {
        ScheduleRegenerate();
    }

    private void Awake()
    {
        Regenerate();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            Regenerate();
        }
    }

    private void ScheduleRegenerate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedRegenerate;
        EditorApplication.delayCall += DelayedRegenerate;
#else
        Regenerate();
#endif
    }

#if UNITY_EDITOR
    private void DelayedRegenerate()
    {
        if (this == null)
        {
            return;
        }

        Regenerate();
    }
#endif

    private void Regenerate()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        if (meshFilter == null || meshCollider == null)
        {
            return;
        }

        Mesh sourceMesh = GetSourceMesh(meshFilter);

        if (sourceMesh == null)
        {
            return;
        }

        if (generatedMesh == null)
        {
            generatedMesh = Instantiate(sourceMesh);
            generatedMesh.name = $"{sourceMesh.name}_RollingTerrainInstance";
        }
        else
        {
            generatedMesh.Clear();
            generatedMesh.vertices = sourceMesh.vertices;
            generatedMesh.triangles = sourceMesh.triangles;
            generatedMesh.normals = sourceMesh.normals;
            generatedMesh.uv = sourceMesh.uv;
        }

        Vector3[] sourceVertices = sourceMesh.vertices;
        Vector3[] deformedVertices = new Vector3[sourceVertices.Length];

        for (int i = 0; i < sourceVertices.Length; i++)
        {
            Vector3 vertex = sourceVertices[i];
            float terrainHeight = EvaluateHeight(vertex);
            deformedVertices[i] = new Vector3(vertex.x, terrainHeight, vertex.z);
        }

        generatedMesh.vertices = deformedVertices;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        meshFilter.sharedMesh = generatedMesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = generatedMesh;
    }

    private Mesh GetSourceMesh(MeshFilter meshFilter)
    {
        if (sourceMesh == null && meshFilter.sharedMesh != null && meshFilter.sharedMesh != generatedMesh)
        {
            sourceMesh = meshFilter.sharedMesh;
        }

        if (sourceMesh == null)
        {
            return null;
        }

        return sourceMesh;
    }

    private float EvaluateHeight(Vector3 vertex)
    {
        float sampleX = vertex.x * transform.localScale.x;
        float sampleZ = vertex.z * transform.localScale.z;
        float radialDistance = new Vector2(sampleX, sampleZ).magnitude;
        float normalizedRadius = Mathf.Clamp01(radialDistance / 38f);
        float centerFlattening = Mathf.InverseLerp(centralFlatRadius, 1f, normalizedRadius);

        float perlinA = Mathf.PerlinNoise(sampleX * noiseTiling.x * 0.1f + noiseOffset.x, sampleZ * noiseTiling.y * 0.1f + noiseOffset.y) - 0.5f;
        float perlinB = Mathf.PerlinNoise(sampleZ * (noiseTiling.x * 0.065f) + noiseOffset.y, sampleX * (noiseTiling.y * 0.065f) + noiseOffset.x) - 0.5f;
        float rollingNoise = (perlinA * 0.7f + perlinB * 0.3f) * centerFlattening;

        float southRidge = GaussianHill(sampleX, sampleZ, new Vector2(2f, -31f), new Vector2(24f, 10f), 0.8f);
        float northKnoll = GaussianHill(sampleX, sampleZ, new Vector2(-24f, 25f), new Vector2(12f, 12f), 0.65f);
        float eastRise = GaussianHill(sampleX, sampleZ, new Vector2(31f, 10f), new Vector2(11f, 13f), 0.5f);

        float composedHeight = rollingNoise + (southRidge + northKnoll + eastRise) * outerHillStrength;
        return composedHeight * heightScale;
    }

    private float GaussianHill(float sampleX, float sampleZ, Vector2 center, Vector2 spread, float amplitude)
    {
        float dx = (sampleX - center.x) / Mathf.Max(0.01f, spread.x);
        float dz = (sampleZ - center.y) / Mathf.Max(0.01f, spread.y);
        return Mathf.Exp(-(dx * dx + dz * dz)) * amplitude;
    }
}
