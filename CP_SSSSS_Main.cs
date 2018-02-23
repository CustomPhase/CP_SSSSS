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
    public Shader sssssShader;
    public Shader sssMaskShader;
    public Shader copyDepthShader;

    [Range(1, 3)]
    public int downscale = 1;
    [Range(1, 3)]
    public int blurIterations = 1;
    [Range(0.01f, 1.6f)]
    public float scatterDistance = 0.4f;
    [Range(0f, 2f)]
    public float scatterIntensity = 1f;
    [Range(0.001f, 0.3f)]
    public float softDepthBias = 0.05f;
    [Range(0f, 1f)]
    public float affectDirect = 1.0f;

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

    CommandBuffer sssssRenderBuffer;

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

    Material m_SSSSSRenderMaterial;
    Material SSSSSRenderMaterial {
        get {
            if(m_SSSSSRenderMaterial == null && sssssShader != null) {
                m_SSSSSRenderMaterial = new Material(sssssShader) {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return m_SSSSSRenderMaterial;
        }
    }

    static Dictionary<Camera, CP_SSSSS_Main> instancies = new Dictionary<Camera, CP_SSSSS_Main>();

    static public CP_SSSSS_Main GetInstance(Camera camera)
    {
        if(instancies.ContainsKey(camera)) return instancies[camera];
        return null;
    }
    
    void Start()
    {
        instancies[GetComponent<Camera>()] = this;
    }

    void OnDestroy()
    {
        Camera k = null;
        foreach(var i in instancies) {
            if(i.Value == this) {
                k = i.Key;
                break;
            }
        }
        if(k) instancies.Remove(k);
    }

    void Reset()
    {
        sssssShader = Shader.Find("Hidden/CPSSSSSShader");
        sssMaskShader = Shader.Find("Hidden/CPSSSSSMask");
        copyDepthShader = Shader.Find("Hidden/CPSSSSSBlitDepthTextureToDepth");
    }

    void OnEnable()
    {
        if(!SystemInfo.supportsImageEffects) {
            enabled = false;
            return;
        }

        // Disable the image effect if the shader can't
        // run on the users graphics card
        if(!sssssShader || !sssssShader.isSupported) {
            enabled = false;
            return;
        }

        if(sssssRenderBuffer == null) ApplySSSSSRenderBuffer();
    }

    void OnDisable()
    {
        if(m_CopyDepthMaterial) DestroyImmediate(m_CopyDepthMaterial);
        foreach(var i in m_MaskMaterials) DestroyImmediate(i);

        m_MaskMaterials.Clear();

        m_CopyDepthMaterial = null;

        if(m_SSSSSRenderMaterial) DestroyImmediate(m_SSSSSRenderMaterial);
        m_SSSSSRenderMaterial = null;

        CleanupSSSSSRenderBuffer();
    }

    void OnPreRender()
    {
        if(sssssRenderBuffer == null) ApplySSSSSRenderBuffer();
        if(sssssRenderBuffer != null) UpdateSSSSSRenderBuffer();
    }

    void OnPostRender()
    {
        TargetMeshes.Clear();
        Shader.SetGlobalTexture("SSSMaskTexture", null);
    }
        
    void ApplySSSSSRenderBuffer()
    {
        sssssRenderBuffer = new CommandBuffer {
            name = "Screen Space Subsurface Scattering"
        };
        GetComponent<Camera>().AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, sssssRenderBuffer);
    }

    void UpdateSSSSSRenderBuffer()
    {
        sssssRenderBuffer.Clear();
            
        if(!Camera.current || TargetMeshes.Count == 0) return;

        AddMakeSSSMaskCommands(sssssRenderBuffer);

        int blurRT1 = Shader.PropertyToID("SSSSSBlur1");
        int blurRT2 = Shader.PropertyToID("SSSSSBlur2");
        int src = Shader.PropertyToID("SSSSSSource");
        int w = -1;
        int h = -1;
        Camera cam = Camera.current;
        if(cam != null) {
            w = cam.pixelWidth / downscale;
            h = cam.pixelHeight / downscale;
        }

        sssssRenderBuffer.GetTemporaryRT(blurRT1, w, h, 16, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
        sssssRenderBuffer.GetTemporaryRT(blurRT2, w, h, 16, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
        sssssRenderBuffer.GetTemporaryRT(src, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
        sssssRenderBuffer.SetGlobalFloat("SoftDepthBias", softDepthBias * 0.05f * 0.2f);

        sssssRenderBuffer.Blit(BuiltinRenderTextureType.CameraTarget, blurRT2);
        sssssRenderBuffer.Blit(BuiltinRenderTextureType.CameraTarget, src);

        //multipass pass blur
        for(int k = 1; k <= blurIterations; k++) {
            sssssRenderBuffer.SetGlobalFloat("BlurStr", Mathf.Clamp01(scatterDistance * 0.08f - k * 0.022f * scatterDistance));
            sssssRenderBuffer.SetGlobalVector("BlurVec", new Vector4(1, 0, 0, 0));
            sssssRenderBuffer.Blit(blurRT2, blurRT1, SSSSSRenderMaterial, 0);
            sssssRenderBuffer.SetGlobalVector("BlurVec", new Vector4(0, 1, 0, 0));
            sssssRenderBuffer.Blit(blurRT1, blurRT2, SSSSSRenderMaterial, 0);

            sssssRenderBuffer.SetGlobalVector("BlurVec", new Vector4(1, 1, 0, 0).normalized);
            sssssRenderBuffer.Blit(blurRT2, blurRT1, SSSSSRenderMaterial, 0);
            sssssRenderBuffer.SetGlobalVector("BlurVec", new Vector4(-1, 1, 0, 0).normalized);
            sssssRenderBuffer.Blit(blurRT1, blurRT2, SSSSSRenderMaterial, 0);
        }

        sssssRenderBuffer.SetGlobalTexture("BlurTex", blurRT2);
        sssssRenderBuffer.SetGlobalFloat("EffectStr", scatterIntensity);
        sssssRenderBuffer.SetGlobalFloat("PreserveOriginal", 1 - affectDirect);
        sssssRenderBuffer.Blit(src, BuiltinRenderTextureType.CameraTarget, SSSSSRenderMaterial, 1);

        sssssRenderBuffer.ReleaseTemporaryRT(blurRT1);
        sssssRenderBuffer.ReleaseTemporaryRT(blurRT2);
        sssssRenderBuffer.ReleaseTemporaryRT(src);
    }

    void CleanupSSSSSRenderBuffer()
    {
        if(sssssRenderBuffer != null) {
            sssssRenderBuffer.Clear();
            GetComponent<Camera>().RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, sssssRenderBuffer);
            sssssRenderBuffer = null;
        }
    }

    public void AddMakeSSSMaskCommands(CommandBuffer buffer)
    {
        if(!Camera.current || TargetMeshes.Count == 0) return;

        for(int i = m_MaskMaterials.Count; i < TargetMeshes.Count; i++) {
            if(sssMaskShader != null) {
                var material = new Material(sssMaskShader) {
                    hideFlags = HideFlags.HideAndDontSave
                };
                m_MaskMaterials.Add(material);
            }
        }

        if(m_MaskMaterials.Count < TargetMeshes.Count) return;

        int maskRT = Shader.PropertyToID("SSSMaskTexture");
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

        buffer.SetGlobalTexture("SSSMaskTexture", maskRT);
    }
}
