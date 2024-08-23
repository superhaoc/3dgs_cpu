using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GSRenderPassFeature : ScriptableRendererFeature
{
    public Material matRenderer;

    class CustomRenderPass : ScriptableRenderPass
    {
        private GSRenderPassFeature _renderFeature;

        private static Mesh mesh;
        private RenderParams rp;

        private Vector3[] PrimitivesVertices = {
            new(-2, -2, 0),
            new(2, -2, 0),
            new(2, 2, 0),
            new(-2, 2, 0)
        };

        private int[] PrimitivesTriangles = {
            0, 1, 2,
            0, 2, 3
        };

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (GSRenderer.Instance.vertexCount == 0 || mesh == null || _renderFeature.matRenderer == null)
                return;

            if (!renderingData.cameraData.camera.CompareTag("MainCamera"))
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("Procedural Instancing");

            GSRenderer.Instance.ThrottledSort();



            cmd.DrawMeshInstancedProcedural(mesh, 0, _renderFeature.matRenderer, 0, GSRenderer.Instance.vertexCount - GSRenderer.Instance.nSkipVertex);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
        public CustomRenderPass(GSRenderPassFeature renderFeature)
        {
            _renderFeature = renderFeature;

            mesh = new Mesh();
            mesh.SetVertices(PrimitivesVertices);
            mesh.SetTriangles(PrimitivesTriangles, 0);

        }
    }

    CustomRenderPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(this);

        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


