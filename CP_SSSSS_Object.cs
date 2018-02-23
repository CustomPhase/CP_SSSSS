using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class CP_SSSSS_Object : MonoBehaviour
{
    [Tooltip("Material has no Scattering Color Parameter, thease are used instead of. If Material has Scattering Color Parameter and you want to use it, stay empty.")]
    public TextureAndColor[] scatteringColorsPerSubmesh;

    Renderer m_Renderer;
    Renderer Renderer { get { if(!m_Renderer) m_Renderer = GetComponent<Renderer>(); return m_Renderer; } }

    List<CP_SSSSS_Main.SSSParameter> parameters = new List<CP_SSSSS_Main.SSSParameter>();
    List<int> subMeshIndicies = new List<int>();

    void Start()
    {
        parameters.Clear();
        subMeshIndicies.Clear();
        if(Renderer) {
            var sharedMaterials = Renderer.sharedMaterials;
            for(int i = 0; i < sharedMaterials.Length; ++i) {
                var material = sharedMaterials[i];
                if(i < scatteringColorsPerSubmesh.Length && scatteringColorsPerSubmesh[i].color != Color.black && material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent) {
                    subMeshIndicies.Add(i);
                    parameters.Add(CP_SSSSS_Main.MakeSSSParameterFromTextureAndColor(scatteringColorsPerSubmesh[i]));
                }
                else if(CP_SSSSS_Main.HasMaterialSSSParameter(material) && material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent) {
                    subMeshIndicies.Add(i);
                    parameters.Add(CP_SSSSS_Main.MakeSSSParameterFromMaterial(material));
                }
            }
        }
    }

    void Reset()
    {
        scatteringColorsPerSubmesh = new TextureAndColor[] {
            new TextureAndColor {
                texture = null,
                color = new Color(1.0f, 0.2f, 0.1f, 1.0f)
            }
        };
    }

    void OnWillRenderObject()
    {
        if(Renderer) {
            var camera = Camera.current;
            var sssssMain = CP_SSSSS_Main.GetInstance(camera);
            if(sssssMain && sssssMain.isActiveAndEnabled) {
                if(Application.isPlaying) {
                    for(int i = 0; i < subMeshIndicies.Count; ++i) {
                        sssssMain.AddRenderer(Renderer, subMeshIndicies[i], parameters[i]);
                    }
                }
                else {
                    var sharedMaterials = Renderer.sharedMaterials;
                    for(int i = 0; i < sharedMaterials.Length; ++i) {
                        var material = sharedMaterials[i];
                        if(scatteringColorsPerSubmesh != null && i < scatteringColorsPerSubmesh.Length && scatteringColorsPerSubmesh[i].color != Color.black && material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent) {
                            subMeshIndicies.Add(i);
                            sssssMain.AddRenderer(Renderer, i, CP_SSSSS_Main.MakeSSSParameterFromTextureAndColor(scatteringColorsPerSubmesh[i]));
                        }
                        else if(CP_SSSSS_Main.HasMaterialSSSParameter(material) && material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent) {
                            sssssMain.AddRenderer(Renderer, i, CP_SSSSS_Main.MakeSSSParameterFromMaterial(material));
                        }
                    }
                }
            }
        }
    }
}

[System.Serializable]
public struct TextureAndColor
{
    public Texture texture;
    public Color color;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(TextureAndColor))]
public class TextureAndColorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var textureRect = new Rect(position.x, position.y, 120, position.height);
        var labelRect = new Rect(textureRect.x + textureRect.width, position.y, 70, position.height);
        var colorRect = new Rect(labelRect.x + labelRect.width, position.y, 65, position.height);

        EditorGUI.PropertyField(textureRect, property.FindPropertyRelative("texture"), GUIContent.none);
        EditorGUI.LabelField(labelRect, "SSS Map");
        EditorGUI.PropertyField(colorRect, property.FindPropertyRelative("color"), GUIContent.none);

        EditorGUI.EndProperty();
    }
}
#endif
