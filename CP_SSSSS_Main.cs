using UnityEngine;
using System.Collections;

namespace UnityStandardAssets.ImageEffects
{
	//[ImageEffectAllowedInSceneView]
	[ImageEffectOpaque]
	//[ExecuteInEditMode]
	public class CP_SSSSS_Main : ImageEffectBase
	{
		public Shader maskReplacementShader;
		RenderTexture tempBlur;
		RenderTexture tempBlur2;
		
		[Range(1,3)]
		public int downscale = 1;
		[Range(1, 3)]
		public int blurIterations = 1;
		[Range(0.1f, 1f)]
		public float blurStrength = 0.4f;
		[Range(0f,2f)]
		public float scatterIntensity = 1f;
		[Range(0.001f, 0.3f)]
		public float softDepthBias = 0.05f;
		[Range(0f, 1f)]
		public float affectDirect = 0.5f;

		Camera maskRenderCamera;
		RenderTexture maskTexture;
		string camName = "SSSSSMaskRenderCamera";

		// Called by camera to apply image effect
		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			maskTexture = RenderTexture.GetTemporary(source.width, source.height, source.depth, RenderTextureFormat.ARGB32);
			RenderMasks();
			//material.SetFloat("_BlurStr", blurStrength*0.12f);
			tempBlur = RenderTexture.GetTemporary(source.width / downscale, source.height / downscale, source.depth, source.format);
			tempBlur2 = RenderTexture.GetTemporary(source.width / downscale, source.height / downscale, source.depth, source.format);
			tempBlur.filterMode = FilterMode.Bilinear;
			tempBlur2.filterMode = FilterMode.Bilinear;
			material.SetFloat("_SoftDepthBias", softDepthBias*0.05f*0.2f);
			//material.SetFloat("_SoftDepthBias", softDepthBias * 1);

			Graphics.Blit(source, tempBlur2);

			//multipass pass blur
			/**/
			for (int k = 1; k <= blurIterations; k++)
			{
				material.SetFloat("_BlurStr", blurStrength * 0.12f - k*0.02f);
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
			material.SetTexture("_MaskTex", maskTexture);
			material.SetFloat("_EffectStr", scatterIntensity);
			material.SetFloat("_PreserveOriginal", 1-affectDirect);
			Graphics.Blit(source, destination, material, 1);

			RenderTexture.ReleaseTemporary(tempBlur);
			RenderTexture.ReleaseTemporary(tempBlur2);
			RenderTexture.ReleaseTemporary(maskTexture);
		}

		void RenderMasks()
		{
			CheckCamera();
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
}
