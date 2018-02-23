using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class CP_SSSSS_Object : MonoBehaviour {
	[Tooltip("Material has no Scattering Color Parameter, thease are used instead of. If Material has Scattering Color Parameter and you want to use it, stay empty.")]
	public TextureAndColor[] scatteringColorsPerSubmesh;

	Renderer r;

	// Use this for initialization
	void Start () {
		r = GetComponent<Renderer>();
		parameters.Clear();
		subMeshIndicies.Clear();
		if(r) {
			var sharedMaterials = r.sharedMaterials;
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

	void OnWillRenderObject()
	{
		if(r) {
			var camera = Camera.current;
			var sssssMain = camera.gameObject.GetComponent<CP_SSSSS_Main>();
			if(sssssMain && sssssMain.isActiveAndEnabled) {
				if(Application.isPlaying) {
					for(int i = 0; i < subMeshIndicies.Count; ++i) {
						sssssMain.AddRenderer(r, subMeshIndicies[i], parameters[i]);
					}
				}
				else {
					var sharedMaterials = r.sharedMaterials;
					for(int i = 0; i < sharedMaterials.Length; ++i) {
						var material = sharedMaterials[i];
						if(scatteringColorsPerSubmesh != null && i < scatteringColorsPerSubmesh.Length && scatteringColorsPerSubmesh[i].color != Color.black && material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent) {
							subMeshIndicies.Add(i);
							sssssMain.AddRenderer(r, i, CP_SSSSS_Main.MakeSSSParameterFromTextureAndColor(scatteringColorsPerSubmesh[i]));
						}
						else if(CP_SSSSS_Main.HasMaterialSSSParameter(material) && material.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent) {
							sssssMain.AddRenderer(r, i, CP_SSSSS_Main.MakeSSSParameterFromMaterial(material));
						}
					}
				}
			}
		}
	}

	List<CP_SSSSS_Main.SSSParameter> parameters = new List<CP_SSSSS_Main.SSSParameter>();
	List<int> subMeshIndicies = new List<int>();

	void Reset()
	{
		scatteringColorsPerSubmesh = new TextureAndColor[] {
			new TextureAndColor {
				texture = null,
				color = new Color(1.0f, 0.2f, 0.1f, 1.0f)
			}
		};
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
