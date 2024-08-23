Shader "GSRenderer"
{
    Properties
    {
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend OneMinusDstAlpha One

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
         #pragma enable_d3d11_debug_symbols 
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 quadVertexPosOS : POSITION;
                float2 uv :         TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv :          TEXCOORD0;
                float3 quadVertexPosOS : TEXCOORD1;
                half4  vcolor : TEXCOORD2;
                int4 instanceid2 : BLENDINDICES;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct SplatData
            {
                float3 position;
                float3 sigma0;
                float3 sigma1;
                float4 color;
            };

            uniform StructuredBuffer<SplatData> splatDatas;
            uniform StructuredBuffer<int> depthIndex;
            //uniform TEXTURE2D(colorTex);
            //SAMPLER(point_clamp_sampler);
            // uniform StructuredBuffer<float3> positions;
            // uniform StructuredBuffer<int4> colors;
            // uniform StructuredBuffer<float3> sigma0;
            // uniform StructuredBuffer<float3> sigma1;
            //uniform StructuredBuffer<float3> scales;
            //uniform StructuredBuffer<float4> rotations;
            //uniform float sigma[6];

            // float3x3 CalcMatrixFromRotationScale(float4 rot, float3 scale)
            // {
            //     float3x3 ms = float3x3(
            //         scale.x, 0, 0,
            //         0, scale.y, 0,
            //         0, 0, scale.z
            //     );
            //     float x = rot.x;
            //     float y = rot.y;
            //     float z = rot.z;
            //     float w = rot.w;
            //     float3x3 mr = float3x3(
            //         1-2*(y*y + z*z),   2*(x*y - w*z),   2*(x*z + w*y),
            //           2*(x*y + w*z), 1-2*(x*x + z*z),   2*(y*z - w*x),
            //           2*(x*z - w*y),   2*(y*z + w*x), 1-2*(x*x + y*y)
            //     );
            //     return mul(mr, ms);
            // }
            //
            // void CalcCovariance3D(float3x3 rotMat, out float3 sigma0, out float3 sigma1)
            // {
            //     float3x3 sig = mul(rotMat, transpose(rotMat));
            //     sigma0 = float3(sig._m00, sig._m01, sig._m02);
            //     sigma1 = float3(sig._m11, sig._m12, sig._m22);
            // }

            float3 CalcCovariance2D(float3 viewPos, float3 cov3d0, float3 cov3d1, float4x4 viewMatrix, float4x4 matrixP, float4 screenParams)
            {
                // this is needed in order for splats that are visible in view but clipped "quite a lot" to work
                float aspect = matrixP._m00 / matrixP._m11;
                float tanFovX = rcp(matrixP._m00);
                float tanFovY = rcp(matrixP._m11 * aspect);
                float limX = 1.3 * tanFovX;
                float limY = 1.3 * tanFovY;
                viewPos.x = clamp(viewPos.x / viewPos.z, -limX, limX) * viewPos.z;
                viewPos.y = clamp(viewPos.y / viewPos.z, -limY, limY) * viewPos.z;

                float focal = screenParams.x * matrixP._m00 * 0.5;

                float3x3 J = float3x3(
                    focal / viewPos.z, 0, -(focal * viewPos.x) / (viewPos.z * viewPos.z),
                    0, focal / viewPos.z, -(focal * viewPos.y) / (viewPos.z * viewPos.z),
                    0, 0, 0
                );
                float3x3 W = (float3x3)viewMatrix;
                float3x3 T = mul(J, W);
                float3x3 V = float3x3(
                    cov3d0.x, cov3d0.y, cov3d0.z,
                    cov3d0.y, cov3d1.x, cov3d1.y,
                    cov3d0.z, cov3d1.y, cov3d1.z
                );
                float3x3 cov = mul(T, mul(V, transpose(T)));

                // Low pass filter to make each splat at least 1px size.
                cov._m00 += 0.3;
                cov._m11 += 0.3;
                return float3(cov._m00, cov._m01, cov._m11);
            }

            void DecomposeCovariance(float3 cov2d, out float2 v1, out float2 v2)
            {
                // same as in antimatter15/splat
                float diag1 = cov2d.x, diag2 = cov2d.z, offDiag = cov2d.y;
                float mid = 0.5f * (diag1 + diag2);
                float radius = length(float2((diag1 - diag2) * 0.5, offDiag));
                float lambda1 = mid + radius;
                float lambda2 = max(mid - radius, 0.1);
                float2 diagVec = normalize(float2(offDiag, lambda1 - diag1));
               // diagVec.y = -diagVec.y;
                float maxSize = 4096.0;
                v1 = min(sqrt(2.0 * lambda1), maxSize) * diagVec;
                v2 = min(sqrt(2.0 * lambda2), maxSize) * float2(diagVec.y, -diagVec.x);
            }

            Varyings vert(Attributes i, uint instanceID : SV_InstanceID)
            {

                int index = depthIndex[instanceID];
                //int index = instanceID;
                SplatData splatData = splatDatas[index];

                Varyings o = (Varyings)0;

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.instanceid2 = instanceID;

                float3 vCenterPos = splatData.position;
                o.quadVertexPosOS = i.quadVertexPosOS;

                //float3 scale = scales[index];
                //float4 rotation = rotations[index];
                //float4 posWS = float4(mul(i.quadVertexPosOS.xyz * scale, rotation) + vCenterPos, 1.0);
                float4 posVS = mul(UNITY_MATRIX_MV, float4(vCenterPos, 1.0));
                float4 posCS = mul(GetViewToHClipMatrix(), posVS);

                //o.positionCS = TransformObjectToHClip(mul(CalcMatrixFromRotationScale(rotation, scale), i.quadVertexPosOS.xyz) + vCenterPos);
                float4 color = clamp(posCS.z / posCS.w + 1.0, 0.0, 1.0) * splatData.color;
                o.vcolor = color;
                //ALPHA scale
//if(color.a <= 0.2f) {
//	posCS.z = posCS.w * 1.5f;
//}

float clip = 1.2 * posCS.w;
if (posCS.z < -clip || posCS.x < -clip || posCS.x > clip || posCS.y < -clip || posCS.y > clip)
{
    o.positionCS = asfloat(0x7fc00000);
    return o;
}

bool behindCam = posCS.w <= 0;
if (behindCam)
{
    o.positionCS = asfloat(0x7fc00000); // NaN discards the primitive
    return o;
}
else
{
    //float3x3 splatRotScaleMat = CalcMatrixFromRotationScale(rotation, scale);
    //float3 cov3d0, cov3d1;
    //CalcCovariance3D(splatRotScaleMat, cov3d0, cov3d1);

    float scale2 = 1.0;
    float3 cov3d0 = splatData.sigma0 * scale2;
    float3 cov3d1 = splatData.sigma1 * scale2;

    float3 cov2d = CalcCovariance2D(posVS, cov3d0, cov3d1, UNITY_MATRIX_V, UNITY_MATRIX_P, _ScreenParams);
    float2 majorAxis, minorAxis;
    DecomposeCovariance(cov2d, majorAxis, minorAxis);

    if (dot(majorAxis, majorAxis) < 4.0 && dot(minorAxis, minorAxis) < 4.0) {
        o.positionCS = asfloat(0x7fc00000); // NaN discards the primitive
    return o;
    }
    float2 deltaScreenPos = (i.quadVertexPosOS.x * majorAxis + i.quadVertexPosOS.y * minorAxis) * 2 / _ScreenParams.xy;
    o.positionCS = posCS;
    o.positionCS.xy += deltaScreenPos * posCS.w;
}

//float4 color = colorTex.SampleLevel(point_clamp_sampler, float2(index % 1024 / 1024.0f, index / 1024 / 1024.0f), 0);

//color.rgb = pow(color.rgb, 2.2);


return o;
}

half4 frag(Varyings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

  float A = -dot(i.quadVertexPosOS, i.quadVertexPosOS);
				if (A < -4.0) discard;
				float B = exp(A) * i.vcolor.a;
				return half4(B * i.vcolor.rgb, B);
}
ENDHLSL
}
    }
}