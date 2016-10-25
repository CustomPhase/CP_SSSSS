using UnityEngine;
using System.Collections;
using UnityEditor;

public enum CP_SSSSS_MaskSource
{
	mainTexture = 0,
	separateTexture = 1,
	wholeObject = 2
}

[RequireComponent(typeof(Renderer))]
public class CP_SSSSS_Object : MonoBehaviour {

	//public Texture skinMask;
	public Color subsurfaceColor = new Color(1,0.2f,0.1f,0);
	public CP_SSSSS_MaskSource maskSource = CP_SSSSS_MaskSource.mainTexture;
	public Texture2D maskTex;
	Renderer r;

	// Use this for initialization
	void Start () {
		r = GetComponent<Renderer>();
		//r.material.SetTexture("_SSMask", skinMask);
	}
	
	// Update is called once per frame
	void Update () {
		UpdateSSS();
	}

	void OnDisable()
	{
		r.material.SetColor("_SSColor", Color.black);
	}

	void OnEnable()
	{
		UpdateSSS();
	}

	void UpdateSSS()
	{
		if (r == null) r = GetComponent<Renderer>();
		r.material.SetColor("_SSColor", subsurfaceColor);
		r.material.SetInt("_MaskSource", (int)maskSource);
		if (maskSource==CP_SSSSS_MaskSource.separateTexture)
		{
			r.material.SetTexture("_MaskTex", maskTex);
		}
	}
}

#if UNITY_EDITOR

[CustomEditor(typeof(CP_SSSSS_Object))]
public class CP_SSSSS_Object_Editor : Editor
{
	string[] maskSourceNames = { "Main texture from current material (A)", "Separate texture (A)", "No mask, whole object is translucent" };
	public override void OnInspectorGUI()
	{
		CP_SSSSS_Object myScript = target as CP_SSSSS_Object;

		myScript.subsurfaceColor = EditorGUILayout.ColorField("Subsurface color", myScript.subsurfaceColor);

		//myScript.maskSource = (CP_SSSSS_MaskSource)EditorGUILayout.EnumPopup("Subsurface mask source:", myScript.maskSource);
		myScript.maskSource = (CP_SSSSS_MaskSource)EditorGUILayout.Popup("Subsurface mask source:", (int)myScript.maskSource, maskSourceNames);

		if (myScript.maskSource==CP_SSSSS_MaskSource.separateTexture)
		{
			myScript.maskTex = (Texture2D)EditorGUILayout.ObjectField("Mask texture (A):", myScript.maskTex, typeof(Texture2D), false);
		}

	}
}

#endif
