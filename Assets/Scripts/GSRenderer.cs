using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class GSRenderer : MonoSingleton<GSRenderer>
{
    public string FileName;

    private byte[] splatDataBuffer;

    [HideInInspector]
    public int vertexCount = 0;
    [HideInInspector]
    public int nSkipVertex = 0;

    private SplatData[] arrSplatDatas;


    public GraphicsBuffer buffer_SplatDatas;
    public GraphicsBuffer buffer_depthIndex;

    const int rowLength = 3 * 4 + 3 * 4 + 4 + 4;
    private Matrix4x4 viewProj;
    private Matrix4x4 worldMat4;
    private Matrix4x4 lastViewProj = default;
    private Camera mainCamera;

    private readonly int ID_splatDatas = Shader.PropertyToID("splatDatas");
    private readonly int ID_depthIndex = Shader.PropertyToID("depthIndex");
    private readonly int ID_positions = Shader.PropertyToID("positions");
    private readonly int ID_scales = Shader.PropertyToID("scales");
    private readonly int ID_colors = Shader.PropertyToID("colors");
    private readonly int ID_rotations = Shader.PropertyToID("rotations");
    private readonly int ID_sigma0 = Shader.PropertyToID("sigma0");
    private readonly int ID_sigma1 = Shader.PropertyToID("sigma1");

    public struct SplatData
    {
        public float3 position;
        public float3 sigma0;
        public float3 sigma1;
        public float4 color;
    }

    private void OnEnable()
    {
        Application.targetFrameRate = 120;
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();



#if UNITY_EDITOR
        string strPath = Application.dataPath + "/Data/" + FileName;
#else
        string strPath = Application.persistentDataPath + "/" + FileName;
#endif

        if (!File.Exists(strPath))
        {
            Debug.Log("FilePath is invalid : " + strPath);
            return;
        }

        splatDataBuffer = Array.Empty<byte>();

        using (FileStream fileStream = new FileStream(strPath, FileMode.Open, FileAccess.Read))
        {
            splatDataBuffer = new byte[fileStream.Length];
            // float[] d = new float[3];
            // splatDataBuffer.CopyTo(d);

            int len = fileStream.Read(splatDataBuffer, 0, splatDataBuffer.Length);

            vertexCount = splatDataBuffer.Length / rowLength;


            Debug.Log("Loaded VertexCount : " + Mathf.Floor(vertexCount));

            if (splatDataBuffer[0] == 112 &&
                splatDataBuffer[1] == 108 &&
                splatDataBuffer[2] == 121 &&
                splatDataBuffer[3] == 10)
            {
                //TODO =======================
                Debug.Log("Loaded PLY File : " + strPath);
            }
            else
            {
                Debug.Log("Loaded Splat File : " + strPath);

                int splatDataSize = UnsafeUtility.SizeOf<SplatData>();

                buffer_SplatDatas = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount,
                    splatDataSize);
                buffer_depthIndex = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount,
                    UnsafeUtility.SizeOf<int>());


                arrSplatDatas = new SplatData[vertexCount];
                depthIndex = new uint[vertexCount];

                //RenderTexture rt = RenderTexture.GetTemporary(1024, 1024, 0, RenderTextureFormat.ARGBFloat);

                for (int i = 0; i < vertexCount; i++)
                {
                    depthIndex[i] = (uint)i;

                    int nStartPos = i * rowLength;

                    SplatData data = new SplatData();

                    data.position = new float3();

                    data.position.x = BitConverter.ToSingle(new[]
                    {
                        this.splatDataBuffer[nStartPos + 0],
                        this.splatDataBuffer[nStartPos + 1],
                        this.splatDataBuffer[nStartPos + 2],
                        this.splatDataBuffer[nStartPos + 3]
                    });

                    data.position.y = -BitConverter.ToSingle(new[]
                    {
                        this.splatDataBuffer[nStartPos + sizeof(float) + 0],
                        this.splatDataBuffer[nStartPos + sizeof(float) + 1],
                        this.splatDataBuffer[nStartPos + sizeof(float) + 2],
                        this.splatDataBuffer[nStartPos + sizeof(float) + 3]
                    });

                    data.position.z = BitConverter.ToSingle(new[]
                    {
                        this.splatDataBuffer[nStartPos + 2 * sizeof(float) + 0],
                        this.splatDataBuffer[nStartPos + 2 * sizeof(float) + 1],
                        this.splatDataBuffer[nStartPos + 2 * sizeof(float) + 2],
                        this.splatDataBuffer[nStartPos + 2 * sizeof(float) + 3]
                    });

                    float3 scale = new float3();
                    scale.x = BitConverter.ToSingle(new[]
                    {
                        this.splatDataBuffer[nStartPos + 3 * sizeof(float) + 0],
                        this.splatDataBuffer[nStartPos + 3 * sizeof(float) + 1],
                        this.splatDataBuffer[nStartPos + 3 * sizeof(float) + 2],
                        this.splatDataBuffer[nStartPos + 3 * sizeof(float) + 3]
                    });

                    scale.y = BitConverter.ToSingle(new[]
                    {
                        this.splatDataBuffer[nStartPos + 4 * sizeof(float) + 0],
                        this.splatDataBuffer[nStartPos + 4 * sizeof(float) + 1],
                        this.splatDataBuffer[nStartPos + 4 * sizeof(float) + 2],
                        this.splatDataBuffer[nStartPos + 4 * sizeof(float) + 3]
                    });

                    scale.z = BitConverter.ToSingle(new[]
                    {
                        this.splatDataBuffer[nStartPos + 5 * sizeof(float) + 0],
                        this.splatDataBuffer[nStartPos + 5 * sizeof(float) + 1],
                        this.splatDataBuffer[nStartPos + 5 * sizeof(float) + 2],
                        this.splatDataBuffer[nStartPos + 5 * sizeof(float) + 3]
                    });

                    data.color = new float4();
                    data.color.x = this.splatDataBuffer[nStartPos + 6 * sizeof(float) + 0] / 255.0f;
                    data.color.y = this.splatDataBuffer[nStartPos + 6 * sizeof(float) + 1] / 255.0f;
                    data.color.z = this.splatDataBuffer[nStartPos + 6 * sizeof(float) + 2] / 255.0f;
                    data.color.w = this.splatDataBuffer[nStartPos + 6 * sizeof(float) + 3] / 255.0f;


                    float4 rotation = new float4();
                    rotation.w = (splatDataBuffer[nStartPos + 6 * sizeof(float) + 4 * sizeof(byte) + 0] - 128.0f) /
                                 128.0f;
                    rotation.x = (splatDataBuffer[nStartPos + 6 * sizeof(float) + 4 * sizeof(byte) + 1] - 128.0f) /
                                 128.0f;
                    rotation.y = (splatDataBuffer[nStartPos + 6 * sizeof(float) + 4 * sizeof(byte) + 2] - 128.0f) /
                                 128.0f;
                    rotation.z = (splatDataBuffer[nStartPos + 6 * sizeof(float) + 4 * sizeof(byte) + 3] - 128.0f) /
                                 128.0f;

                    float4 q = rotation;
                    float3 s = scale;
                    float3x3 M = CalcMatrixFromRotationScale2(q, s);

                    // M = math.transpose(M);
                    // Debug.LogError("M ==>" + M);

                    CalcCovariance3D(M, out float3 sigma0, out float3 sigma1);
                    // arrSigma0[i] = sigma0;
                    // arrSigma1[i] = sigma1;

                    data.sigma0 = sigma0;
                    data.sigma1 = sigma1;

                    arrSplatDatas[i] = data;
                    //arrSplatPositions[i] = data.position;
                    //arrRotations[i] = q;
                    //arrSplatScales[i] = scale;
                    // arrSplatColors[i] = new int4(data.color[0],
                    //     data.color[1],
                    //     data.color[2],
                    //     data.color[3]);

                }


                //==========Optimized
                List<SplatData> listDatas = new List<SplatData>();
                for (int i = 0; i < vertexCount; i++)
                {
                    var data = arrSplatDatas[i];
                    if (data.color.w >= (1 / 255.0f))
                        listDatas.Add(data);
                }
                arrSplatDatas = listDatas.ToArray();
                vertexCount = listDatas.Count;
                Debug.Log("Optimized Count : " + vertexCount);

                buffer_SplatDatas.SetData(arrSplatDatas);
                //buffer_Positions.SetData(arrSplatPositions);
                //buffer_Scales.SetData(arrSplatScales);
                //buffer_Rotations.SetData(arrRotations);
                //buffer_Colors.SetData(arrSplatColors);
                //buffer_Sigma0.SetData(arrSigma0);
                //buffer_Sigma1.SetData(arrSigma1);

                Shader.SetGlobalBuffer(ID_splatDatas, buffer_SplatDatas);
                // Shader.SetGlobalBuffer(ID_positions, buffer_Positions);
                // Shader.SetGlobalBuffer(ID_colors, buffer_Colors);
                // Shader.SetGlobalBuffer(ID_sigma0, buffer_Sigma0);
                // Shader.SetGlobalBuffer(ID_sigma1, buffer_Sigma1);

                buffer_depthIndex.SetData(depthIndex);
                Shader.SetGlobalBuffer(ID_depthIndex, buffer_depthIndex);
                //Shader.SetGlobalBuffer(ID_rotations, buffer_Rotations);
                //Shader.SetGlobalBuffer(ID_scales, buffer_Scales);
            }


        }
    }

    float3x3 CalcMatrixFromRotationScale(float4 rot, float3 scale)
    {
        float3x3 ms = new float3x3(
            scale.x, 0, 0,
            0, scale.y, 0,
            0, 0, scale.z
        );
        float x = rot.x;
        float y = rot.y;
        float z = rot.z;
        float w = rot.w;
        float3x3 mr = new float3x3(
            (1 - 2 * (y * y + z * z)) * scale.x, 2 * (x * y - w * z) * scale.y, 2 * (x * z + w * y) * scale.z,
            2 * (x * y + w * z) * scale.x, (1 - 2 * (x * x + z * z)) * scale.y, 2 * (y * z - w * x) * scale.z,
            2 * (x * z - w * y) * scale.x, 2 * (y * z + w * x) * scale.y, (1 - 2 * (x * x + y * y)) * scale.z
        );

        //return GSTools.MatrixMultiply3x3(ms, mr);
        return mr;
    }

    float3x3 CalcMatrixFromRotationScale2(float4 rot, float3 scale)
    {
        var q = math.quaternion(-rot.x, rot.y, -rot.z, rot.w);
        var axis1 = math.mul(q, math.float3(scale.x, 0, 0));
        var axis2 = math.mul(q, math.float3(0, scale.y, 0));
        var axis3 = math.mul(q, math.float3(0, 0, scale.z));


        return new float3x3(axis1, axis2, axis3);
    }
    void CalcCovariance3D(float3x3 rotMat, out float3 sigma0, out float3 sigma1)
    {
        //float3x3 sig2 = GSTools.MatrixMultiply3x3(GSTools.MatrixTranspose(rotMat), rotMat);
        float3x3 sig = math.mul(rotMat, math.transpose(rotMat));
        sigma0 = new float3(sig[0][0], sig[0][1], sig[0][2]);
        sigma1 = new float3(sig[1][1], sig[1][2], sig[2][2]);
    }

    private uint[] depthIndex;
    private const int len = 256 * 256;

    private Stopwatch sw = new Stopwatch();

    private void runSort()
    {
        //sw.Restart();

        float maxDepth = -float.MaxValue;
        float minDepth = float.MaxValue;
        float[] sizeList = new float[vertexCount];

        float3 forward = new float3(viewProj.m20, viewProj.m21, viewProj.m22);
        float3 worldpos = new float3(worldMat4.m03, worldMat4.m13, worldMat4.m23);
        float origDepth = math.dot(forward, worldpos) * 4096;


        for (int i = 0; i < vertexCount; i++)
        {
            float3 pos = arrSplatDatas[i].position;
            float curDepth = math.dot(forward, pos) * 4096;


            sizeList[i] = (int)curDepth;
            if (curDepth > maxDepth) maxDepth = curDepth;
            if (curDepth < minDepth) minDepth = curDepth;
        }


        // nSkipVertex = math.max(nSkipVertex - 300,0);

        float depthInv = len / (maxDepth - minDepth);
        uint[] counts0 = new uint[len];
        int origIdx = math.clamp(Mathf.FloorToInt((origDepth - minDepth) * depthInv), 0, len - 1);
        nSkipVertex = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            sizeList[i] = ((sizeList[i] - minDepth) * depthInv);

            int idx = Mathf.FloorToInt(sizeList[i]);

            if (idx > len - 1)
            {
                idx = len - 1;
            }

            if (idx < 0)
            {
                idx = 0;
            }

            if (origIdx > idx)
            {
                nSkipVertex++;
            }

            counts0[idx]++;
        }

        uint[] starts0 = new uint[len];
        for (uint i = 1; i < len; i++)
        {
            starts0[i] = starts0[i - 1] + counts0[i - 1];
        }

        depthIndex = new uint[vertexCount - nSkipVertex];

        for (uint i = 0; i < vertexCount; i++)
        {
            int idx = Mathf.FloorToInt(sizeList[i]);
            if (idx > len - 1)
            {
                idx = len - 1;
            }

            if (idx < 0)
            {
                idx = 0;
            }

            if (starts0[idx] >= nSkipVertex)
            {
                depthIndex[starts0[idx]++ - nSkipVertex] = i;
            }
        }
    }


    public void ThrottledSort()
    {

        if (splatDataBuffer == null || splatDataBuffer.Length == 0 || vertexCount == 0)
        {
            return;
        }

        viewProj = mainCamera.projectionMatrix * mainCamera.worldToCameraMatrix;
        worldMat4 = mainCamera.cameraToWorldMatrix;

        //has scale components inside ViewProj matrix,need to normalize it
        float dot = lastViewProj.m20 * viewProj.m20 +
                    lastViewProj.m21 * viewProj.m21 +
                    lastViewProj.m22 * viewProj.m22;

        float len1 = math.length(new float3(lastViewProj.m10, lastViewProj.m11, lastViewProj.m12));
        float len2 = math.length(new float3(viewProj.m10, viewProj.m11, viewProj.m12));
        dot += lastViewProj.m10 / len1 * viewProj.m10 / len2 +
                    lastViewProj.m11 / len1 * viewProj.m11 / len2 +
                    lastViewProj.m12 / len1 * viewProj.m12 / len2;


        len1 = math.length(new float3(lastViewProj.m00, lastViewProj.m01, lastViewProj.m02));
        len2 = math.length(new float3(viewProj.m00, viewProj.m01, viewProj.m02));
        dot += lastViewProj.m00 / len1 * viewProj.m00 / len2 +
                    lastViewProj.m01 / len1 * viewProj.m01 / len2 +
                    lastViewProj.m02 / len1 * viewProj.m02 / len2;



        if (Mathf.Abs(dot - 3.0f) < 0.01f)
        {
            return;
        }

        runSort();
        lastViewProj = viewProj;

        UpdateBuffer();

    }

    private void UpdateBuffer()
    {
        buffer_depthIndex.SetData(depthIndex);
        Shader.SetGlobalBuffer(ID_depthIndex, buffer_depthIndex);

    }

    private void OnDisable()
    {
        buffer_SplatDatas?.Dispose();
        buffer_depthIndex?.Dispose();

        splatDataBuffer = null;

        vertexCount = 0;
    }


}
