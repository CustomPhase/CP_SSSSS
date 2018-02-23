using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
#if UNITY_5_4_OR_NEWER
[ImageEffectAllowedInSceneView]
#endif
[ImageEffectOpaque]
[ExecuteInEditMode]
public class CP_SSSSS_Main : MonoBehaviour
{
	public Shader shader;
	public Shader maskShader;
	public Shader copyDepthShader;

	CommandBuffer buffer;
	CameraEvent camEvent = CameraEvent.BeforeImageEffectsOpaque;

	private Material m_Material;
	Material material
	{
		get
		{
			if (m_Material == null && shader != null)
			{
				m_Material = new Material(shader);
				m_Material.hideFlags = HideFlags.HideAndDontSave;
			}
			return m_Material;
		}
	}
		
	[Range(1,3)]
	public int downscale = 1;
	[Range(1, 3)]
	public int blurIterations = 1;
	[Range(0.01f, 1.6f)]
	public float scatterDistance = 0.4f;
	[Range(0f,2f)]
	public float scatterIntensity = 1f;
	[Range(0.001f, 0.3f)]
	public float softDepthBias = 0.05f;
	[Range(0f, 1f)]
	public float affectDirect = 0.5f;

	void OnDisable() {
		if (m_Material)
		{
			DestroyImmediate(m_Material);
		}
		if(m_CopyDepthMaterial) DestroyImmediate(m_CopyDepthMaterial);
		foreach(var i in m_MaskMaterials) DestroyImmediate(i);

		m_Material = null;
		m_CopyDepthMaterial = null;
		m_MaskMaterials.Clear();

		CleanupBuffer();
	}

	void OnEnable()
	{
		if (!SystemInfo.supportsImageEffects)
		{
			enabled = false;
			return;
		}

		// Disable the image effect if the shader can't
		// run on the users graphics card
		if (!shader || !shader.isSupported)
		{
			enabled = false;
			return;
		}

		CleanupBuffer();
		if(buffer == null) ApplyBuffer();
	}

	private void OnPreRender()
	{
		if(buffer == null) ApplyBuffer();
		if(buffer != null) UpdateBuffer();
	}

	void OnPostRender()
	{
		TargetMeshes.Clear();
		Shader.SetGlobalTexture("SSSMaskTexture", null);
	}

	public struct SSSParameter
	{
		public Texture scatteringMap;
		public Vector2 scatteringMapOffset;
		public Vector2 scatteringMapScale;
		public Color scatteringColor;
	}
	
	static int scatteringMapID = Shader.PropertyToID("ScatteringMap");
	static int scatteringColorID = Shader.PropertyToID("ScatteringColor");

	public static bool HasMaterialSSSParameter(Material material)
	{
		return material.HasProperty(scatteringColorID) && material.HasProperty(scatteringMapID);
	}

	public static SSSParameter MakeSSSParameterFromMaterial(Material material)
	{
		return new SSSParameter {
			scatteringMap = material.GetTexture(scatteringMapID),
			scatteringMapOffset = material.mainTextureOffset,
			scatteringMapScale = material.mainTextureScale,
			scatteringColor = material.GetColor(scatteringColorID),
		};
	}

	public static SSSParameter MakeSSSParameterFromTextureAndColor(TextureAndColor src)
	{
		return new SSSParameter {
			scatteringMap = src.texture,
			scatteringMapOffset = Vector2.zero,
			scatteringMapScale = Vector2.one,
			scatteringColor = src.color,
		};
	}

	public struct TargetMesh
	{
		public TargetMesh(Renderer renderer, int subMeshIndex, SSSParameter sssParameter)
		{
			this.renderer = renderer;
			this.subMeshIndex = subMeshIndex;
			this.sssParameter = sssParameter;
		}
		public Renderer renderer;
		public int subMeshIndex;
		public SSSParameter sssParameter;
	}

	List<TargetMesh> m_TargetMeshes = new List<TargetMesh>(256);
	List<TargetMesh> TargetMeshes { get { return m_TargetMeshes; } }

	public void AddRenderer(Renderer renderer, int subMeshIndex, SSSParameter sssParameter)
	{
		TargetMeshes.Add(new TargetMesh(renderer, subMeshIndex, sssParameter));
	}

	List<Material> m_MaskMaterials = new List<Material>();

	Material m_CopyDepthMaterial;
	Material CopyDepthMaterial {
		get {
			if(m_CopyDepthMaterial == null && copyDepthShader) {
				m_CopyDepthMaterial = new Material(copyDepthShader) {
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			return m_CopyDepthMaterial;
		}
	}
    
	void Reset()
	{
		shader = Shader.Find("Hidden/CPSSSSSShader");
		maskShader = Shader.Find("Hidden/CPSSSSSMask");
		copyDepthShader = Shader.Find("Hidden/CPSSSSSBlitDepthTextureToDepth");
	}
	
	void ApplyBuffer()
	{
		buffer = new CommandBuffer();
		buffer.name = "Screen Space Subsurface Scattering";
		GetComponent<Camera>().AddCommandBuffer(camEvent, buffer);	
	}

	void UpdateBuffer()
	{
		buffer.Clear();
		
		if(TargetMeshes.Count == 0) return;

		AddMakeSSSMaskCommands(buffer);

		int blurRT1 = Shader.PropertyToID("_CPSSSSSBlur1");
		int blurRT2 = Shader.PropertyToID("_CPSSSSSBlur2");
		int src = Shader.PropertyToID("_CPSSSSSSource");
		int w = -1;
		int h = -1;
		Camera cam = Camera.current;
		if (cam != null)
		{
			w = cam.pixelWidth / downscale;
			h = cam.pixelHeight / downscale;
		}

		//buffer.GetTemporaryRT(blurRT1, -1, -1, 16, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
		buffer.GetTemporaryRT(blurRT1, w, h, 16, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
		//buffer.GetTemporaryRT(blurRT2, -1, -1, 16, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
		buffer.GetTemporaryRT(blurRT2, w, h, 16, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
		buffer.GetTemporaryRT(src, -1, -1, 24, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
		buffer.SetGlobalFloat("_SoftDepthBias", softDepthBias * 0.05f * 0.2f);

		buffer.Blit(BuiltinRenderTextureType.CameraTarget, blurRT2);
		//buffer.Blit(BuiltinRenderTextureType.CurrentActive, sourceBuf);
		buffer.Blit(BuiltinRenderTextureType.CameraTarget, src);

		//multipass pass blur
		for (int k = 1; k <= blurIterations; k++)
		{
			buffer.SetGlobalFloat("_BlurStr", Mathf.Clamp01(scatterDistance * 0.08f - k * 0.022f * scatterDistance));
			buffer.SetGlobalVector("_BlurVec", new Vector4(1, 0, 0, 0));
			buffer.Blit(blurRT2, blurRT1, material, 0);
			buffer.SetGlobalVector("_BlurVec", new Vector4(0, 1, 0, 0));
			buffer.Blit(blurRT1, blurRT2, material, 0);

			buffer.SetGlobalVector("_BlurVec", new Vector4(1, 1, 0, 0).normalized);
			buffer.Blit(blurRT2, blurRT1, material, 0);
			buffer.SetGlobalVector("_BlurVec", new Vector4(-1, 1, 0, 0).normalized);
			buffer.Blit(blurRT1, blurRT2, material, 0);
		}
		
		//buffer.Blit(blurRT2, blurBuf);

		buffer.SetGlobalTexture("_BlurTex", blurRT2);
		buffer.SetGlobalFloat("_EffectStr", scatterIntensity);
		buffer.SetGlobalFloat("_PreserveOriginal", 1 - affectDirect);
		buffer.Blit(src, BuiltinRenderTextureType.CameraTarget, material, 1);

		buffer.ReleaseTemporaryRT(blurRT1);
		buffer.ReleaseTemporaryRT(blurRT2);
		buffer.ReleaseTemporaryRT(src);
	}

	void CleanupBuffer()
	{
		if (buffer!=null)
		{
			buffer.Clear();
			GetComponent<Camera>().RemoveCommandBuffer(camEvent, buffer);
			buffer = null;
		}
	}

	public void AddMakeSSSMaskCommands(CommandBuffer buffer)
	{
		if(TargetMeshes.Count == 0) return;

		for(int i = m_MaskMaterials.Count; i < TargetMeshes.Count; i++) {
			if(maskShader != null) {
				var material = new Material(maskShader) {
					hideFlags = HideFlags.HideAndDontSave
				};
				m_MaskMaterials.Add(material);
			}
		}

		if(m_MaskMaterials.Count < TargetMeshes.Count) return;

		int maskRT = Shader.PropertyToID("_MaskTex");
		buffer.GetTemporaryRT(maskRT, -1, -1, 24, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
		buffer.Blit(BuiltinRenderTextureType.None, maskRT, CopyDepthMaterial);
		buffer.SetRenderTarget(maskRT);

		for(int i = 0; i < TargetMeshes.Count; i++) {
			var ii = TargetMeshes[i];
			var maskMaterial = m_MaskMaterials[i];
			maskMaterial.mainTexture = ii.sssParameter.scatteringMap;
			maskMaterial.mainTextureOffset = ii.sssParameter.scatteringMapOffset;
			maskMaterial.mainTextureScale = ii.sssParameter.scatteringMapScale;
			maskMaterial.SetColor(scatteringColorID, ii.sssParameter.scatteringColor);
			buffer.DrawRenderer(ii.renderer, maskMaterial, ii.subMeshIndex);
		}

		buffer.SetGlobalTexture("_MaskTex", maskRT);
	}
}
