using UnityEngine;
using System.Collections;

#if UNITY_5_4_OR_NEWER
[ImageEffectAllowedInSceneView]
#endif
[ImageEffectOpaque]
[ExecuteInEditMode]
public class CP_SSSSS_Main : MonoBehaviour
{
	public Shader shader;
	public Shader maskReplacementShader;
	RenderTexture tempBlur;
	RenderTexture tempBlur2;

	private Material m_Material;
	Material material
	{
		get
		{
			if (m_Material == null)
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
	[Range(0.01f, 1f)]
	public float scatterDistance = 0.4f;
	[Range(0f,2f)]
	public float scatterIntensity = 1f;
	[Range(0.001f, 0.3f)]
	public float softDepthBias = 0.05f;
	[Range(0f, 1f)]
	public float affectDirect = 0.5f;

	Camera maskRenderCamera;
	RenderTexture maskTexture;
	[HideInInspector]
	public string camName = "SSSSSMaskRenderCamera";
		
	void OnDisable() {
		if (m_Material)
		{
			DestroyImmediate(m_Material);
		}

		if (maskTexture != null)
			Object.DestroyImmediate(maskTexture);

		m_Material = null;
		maskTexture = null;
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
			enabled = false;

		RenderMasks();
	}

	// Called by camera to apply image effect
	void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		//maskTexture = RenderTexture.GetTemporary(source.width, source.height, source.depth, RenderTextureFormat.ARGB32);
		if (maskTexture == null || (maskTexture.width != source.width || maskTexture.height != source.height))
		{
			if (maskTexture!=null)
				RenderTexture.ReleaseTemporary(maskTexture);

			maskTexture = RenderTexture.GetTemporary(source.width, source.height, source.depth, RenderTextureFormat.ARGB32);
			maskTexture.hideFlags = HideFlags.HideAndDontSave;
		}

		RenderMasks();
		material.SetTexture("_MaskTex", maskTexture);

		tempBlur = RenderTexture.GetTemporary(source.width / downscale, source.height / downscale, source.depth, source.format);
		tempBlur2 = RenderTexture.GetTemporary(source.width / downscale, source.height / downscale, source.depth, source.format);
		tempBlur.filterMode = FilterMode.Bilinear;
		tempBlur2.filterMode = FilterMode.Bilinear;
		material.SetFloat("_SoftDepthBias", softDepthBias*0.05f*0.2f);

		Graphics.Blit(source, tempBlur2);

		//multipass pass blur
		/**/
		for (int k = 1; k <= blurIterations; k++)
		{
			material.SetFloat("_BlurStr", Mathf.Clamp01(scatterDistance * 0.12f - k*0.02f));
			material.SetVector("_BlurVec", new Vector4(1, 0, 0, 0));
			Graphics.Blit(tempBlur2, tempBlur, material, 0);
			material.SetVector("_BlurVec", new Vector4(0, 1, 0, 0));
			Graphics.Blit(tempBlur, tempBlur2, material, 0);

			material.SetVector("_BlurVec", new Vector4(1, 1, 0, 0).normalized);
			Graphics.Blit(tempBlur2, tempBlur, material, 0);
			material.SetVector("_BlurVec", new Vector4(-1, 1, 0, 0).normalized);
			Graphics.Blit(tempBlur, tempBlur2, material, 0);
		}

		//Combine
		material.SetTexture("_BlurTex", tempBlur2);
		material.SetFloat("_EffectStr", scatterIntensity);
		material.SetFloat("_PreserveOriginal", 1-affectDirect);
		Graphics.Blit(source, destination, material, 1);

		RenderTexture.ReleaseTemporary(tempBlur);
		RenderTexture.ReleaseTemporary(tempBlur2);
		//RenderTexture.ReleaseTemporary(maskTexture);
	}

	void RenderMasks()
	{
		CheckCamera();
		//Hack to remove the "Screen position out of view frustum" error on Unity startup
		if (Camera.current!=null)
		maskRenderCamera.Render();
	}

	void CheckCamera()
	{
		if (maskRenderCamera==null)
		{
			GameObject camgo = GameObject.Find(camName);
			if (camgo==null)
			{
				camgo = new GameObject(camName);
				camgo.hideFlags = HideFlags.HideAndDontSave;
				maskRenderCamera = camgo.AddComponent<Camera>();
				maskRenderCamera.enabled = false;
			} else
			{
				maskRenderCamera = camgo.GetComponent<Camera>();
				maskRenderCamera.enabled = false;
			}
		}

		Camera cam = Camera.current;

		if (cam == null) cam = Camera.main;

		maskRenderCamera.CopyFrom(cam);
		maskRenderCamera.renderingPath = RenderingPath.Forward;
		maskRenderCamera.hdr = false;
		maskRenderCamera.targetTexture = maskTexture;
		maskRenderCamera.SetReplacementShader(maskReplacementShader, "RenderType");
	}
}
