using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGrassGenerator : MonoBehaviour
{
    public struct InputData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;

        public InputData(Vector3 position, Vector3 normal, Vector3 tangent)
        {
            this.position = position;
            this.normal = normal;
            this.tangent = tangent;
        }
    }

    public ComputeShader grassGenerationCS;
    public Material renderingMaterial;

    public bool castShadows = false;
    public float width = 0.5f;
    public float height = 1.0f;

    private List<InputData> inputDataList = new List<InputData>();

    private int kernelID;
    private int threadGroupSize;

    private int[] indirectArgs = new int[] { 0, 1, 0, 0 };

    private ComputeBuffer inputDataBuffer;
    private ComputeBuffer drawTrianglesBuffer;
    private ComputeBuffer indirectArgsBuffer;

    private const int INPUTDATA_STRIDE = sizeof(float) * (3 + 3 + 3);
    private const int DRAW_STRIDE = sizeof(float) * ((3 + 3 + 2) * 3);
    private const int INDIRECT_ARGS_STRIDE = sizeof(int) * 4;

    private const int MAX_TRIANGLES = 2;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private Bounds bounds;

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = meshFilter.sharedMesh;

        SetupBuffers();
        SetupData();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void SetupBuffers()
    {
        inputDataBuffer = new ComputeBuffer(mesh.vertexCount, INPUTDATA_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        drawTrianglesBuffer = new ComputeBuffer(mesh.vertexCount * MAX_TRIANGLES, DRAW_STRIDE, ComputeBufferType.Append);
        indirectArgsBuffer = new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, ComputeBufferType.IndirectArguments);
    }

    private void ReleaseBuffers()
    {
        ReleaseBuffer(inputDataBuffer);
        ReleaseBuffer(drawTrianglesBuffer);
        ReleaseBuffer(indirectArgsBuffer);
    }

    private void ReleaseBuffer(ComputeBuffer computeBuffer)
    {
        if (computeBuffer != null)
        {
            computeBuffer.Release();
            computeBuffer = null;
        }
    }

    private void SetupData()
    {
        if (mesh == null)
        {
            return;
        }
        inputDataList = new List<InputData>();
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            inputDataList.Add(new InputData(mesh.vertices[i], mesh.normals[i], mesh.tangents[i]));
        }

        inputDataBuffer.SetData(inputDataList);
        indirectArgsBuffer.SetData(indirectArgs);

        bounds = meshRenderer.bounds;
        bounds.Expand(Mathf.Max(width, height));

        kernelID = grassGenerationCS.FindKernel("GrassGeneration");
        grassGenerationCS.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        threadGroupSize = Mathf.CeilToInt((float)mesh.vertexCount / threadGroupSizeX);

        grassGenerationCS.SetBuffer(kernelID, "_InputDataBuffer", inputDataBuffer);
        grassGenerationCS.SetBuffer(kernelID, "_DrawTrianglesBuffer", drawTrianglesBuffer);
        grassGenerationCS.SetBuffer(kernelID, "_IndirectArgsBuffer", indirectArgsBuffer);

        grassGenerationCS.SetInt("_VertexCount", mesh.vertexCount);

        renderingMaterial.SetBuffer("_DrawTrianglesBuffer", drawTrianglesBuffer);

    }

    private void GenerateGeometry()
    {
        if (mesh == null || drawTrianglesBuffer == null || inputDataBuffer == null || indirectArgsBuffer == null)
        {
            return;
        }
        if (grassGenerationCS != null && renderingMaterial != null)
        {
            drawTrianglesBuffer.SetCounterValue(0);

            grassGenerationCS.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
            grassGenerationCS.SetFloat("_Width", width);
            grassGenerationCS.SetFloat("_Height", height);

            grassGenerationCS.Dispatch(kernelID, threadGroupSize, 1, 1);
        }
    }

    private void Update()
    {
        GenerateGeometry();

        Graphics.DrawProceduralIndirect(renderingMaterial,
            bounds,
            MeshTopology.Triangles,
            indirectArgsBuffer,
            0,
            null,
            null,
            castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : 
UnityEngine.Rendering.ShadowCastingMode.Off
,
            true,
            gameObject.layer);
    }

} 