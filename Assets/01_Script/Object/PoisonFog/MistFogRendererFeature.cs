using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MistFogRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public Shader fogShader;
    }

    public Settings settings = new Settings();
    private MistFogPass fogPass;

    public override void Create()
    {
        if (settings.fogShader == null)
            settings.fogShader = Shader.Find("Hidden/MistFog");

        fogPass = new MistFogPass(settings.fogShader, settings.renderPassEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.fogShader == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game &&
            renderingData.cameraData.cameraType != CameraType.SceneView)
            return;
        renderer.EnqueuePass(fogPass);
    }

    protected override void Dispose(bool disposing)
    {
        fogPass?.Dispose();
    }

    class MistFogPass : ScriptableRenderPass
    {
        private Material fogMaterial;
        private int tempRTId;

        public MistFogPass(Shader shader, RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
            tempRTId = Shader.PropertyToID("_MistFogTempRT");
            if (shader != null)
                fogMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (fogMaterial == null) return;

            float density = Shader.GetGlobalFloat("_MistDensity");
            if (density < 0.01f) return;

            CommandBuffer cmd = CommandBufferPool.Get("MistFogPass");

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            cmd.GetTemporaryRT(tempRTId, desc);

            // source → tempRT (원본 복사)
            cmd.Blit(source, tempRTId);
            // tempRT → source (셰이더 적용하면서 복사)
            cmd.Blit(tempRTId, source, fogMaterial);

            cmd.ReleaseTemporaryRT(tempRTId);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void Dispose()
        {
            if (fogMaterial != null)
                CoreUtils.Destroy(fogMaterial);
        }
    }
}