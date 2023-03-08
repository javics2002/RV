//Robin Seibold - Outlines - Devlog 2 | A Cozy Creature Collecting and Management Game
//https://www.youtube.com/watch?v=LMqio9NsqmM

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceOutilines : ScriptableRendererFeature {
	[System.Serializable]
	class ViewSpaceNormalsTextureSettings {
		public RenderTextureFormat colorFormat { get; internal set; }
		public int depthBufferBits { get; internal set; }
		public FilterMode filterMode { get; internal set; }
		public Color backgroundColor { get; internal set; }
	}

	class ViewSpaceNormalsTexturePass : ScriptableRenderPass {
		readonly RenderTargetHandle normals;
		readonly List<ShaderTagId> shaderTagIdList;
		readonly Material normalsMaterial;

		ViewSpaceNormalsTextureSettings normalsTextureSettings;
		FilteringSettings filteringSettings, occluderFilteringSettings;

		public ViewSpaceNormalsTexturePass(RenderPassEvent renderPassEvent,  LayerMask outlinesLayerMask,
			ViewSpaceNormalsTextureSettings settings) {
			this.renderPassEvent = renderPassEvent;

			normals.Init("_SceneViewSpaceNormals");
			shaderTagIdList = new List<ShaderTagId>() {
				new ShaderTagId("UniversalForward"),
				new ShaderTagId("UniversalForwardOnly"),
				new ShaderTagId("LightweightForward"),
				new ShaderTagId("SRPDefaultUnlit")
			};
			normalsMaterial = new Material(Shader.Find("Hidden/ViewSpaceNormalsShader"));

			filteringSettings = new FilteringSettings(RenderQueueRange.opaque, outlinesLayerMask);
			occluderFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, outlinesLayerMask);
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
			RenderTextureDescriptor normalsTextureDescriptor = cameraTextureDescriptor;
			normalsTextureDescriptor.colorFormat = normalsTextureSettings.colorFormat;
			normalsTextureDescriptor.depthBufferBits = normalsTextureSettings.depthBufferBits;

			cmd.GetTemporaryRT(normals.id, normalsTextureDescriptor, normalsTextureSettings.filterMode);

			ConfigureTarget(normals.Identifier());
			ConfigureClear(ClearFlag.All, normalsTextureSettings.backgroundColor);
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
			CommandBuffer cmd = CommandBufferPool.Get();
			using (new ProfilingScope(cmd, new ProfilingSampler("SceneViewSpaceNormalsTextureCreation"))) {
				if (!normalsMaterial)
					return;

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, 
					renderingData.cameraData.defaultOpaqueSortFlags),
					occluderSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData,
					renderingData.cameraData.defaultOpaqueSortFlags);
				drawSettings.overrideMaterial = normalsMaterial;
				context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
				context.DrawRenderers(renderingData.cullResults, ref occluderSettings, ref occluderFilteringSettings);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		public override void OnCameraCleanup(CommandBuffer cmd) {
			cmd.ReleaseTemporaryRT(normals.id);
		}
	}

	class ScreenSpaceOutlinePass : ScriptableRenderPass {
		readonly Material screenSpaceOutlineMaterial;

		RenderTargetIdentifier cameraColorTarget, temporaryBuffer;
		int temporaryBufferID = Shader.PropertyToID("_TemporaryBuffer");

		public ScreenSpaceOutlinePass(RenderPassEvent renderPassEvent) {
			this.renderPassEvent = renderPassEvent;
			screenSpaceOutlineMaterial = new Material(Shader.Find("Hidden/OutlineShader"));
		}

		public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
			cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
			if (!screenSpaceOutlineMaterial)
				return;

			CommandBuffer cmd = CommandBufferPool.Get();
			using (new ProfilingScope(cmd, new ProfilingSampler("ScreenSpaceOutlines"))) {
				Blit(cmd, cameraColorTarget, temporaryBuffer);
				Blit(cmd, temporaryBuffer, cameraColorTarget, screenSpaceOutlineMaterial);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}

	[SerializeField]
	RenderPassEvent renderPassEvent;
	[SerializeField]
	ViewSpaceNormalsTextureSettings viewSpaceNormalsTextureSettings;
	[SerializeField]
	LayerMask outlinesLayerMask;

	ViewSpaceNormalsTexturePass viewSpaceNormalsTexutrePass;
	ScreenSpaceOutlinePass screenSpaceOutlinePass;

	public override void Create() {
		viewSpaceNormalsTexutrePass = new ViewSpaceNormalsTexturePass(renderPassEvent, 
			outlinesLayerMask, viewSpaceNormalsTextureSettings);
		screenSpaceOutlinePass = new ScreenSpaceOutlinePass(renderPassEvent);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
		renderer.EnqueuePass(viewSpaceNormalsTexutrePass);
		renderer.EnqueuePass(screenSpaceOutlinePass);
	}
}
