// HospitalPackFixV2.cs
//
// Second-pass fix on top of HospitalPackFix:
//   1. Upper wall plaster (Mat_Walllime01_C) was rendering near-black in
//      corridors — its base texture is dark and the pack expected
//      baked-lightmap illumination we don't have. Brighten the base color
//      and add a soft emission so the corridor reads as a hospital wall
//      rather than a void.
//   2. Wainscot tile (Mat_Tile02) gets a small brightness lift too so it
//      doesn't outshine the upper plaster (or vice versa).
//   3. Crank corridor PointLights: intensity 1.8, range 12, and put them
//      slightly off-center along X so both walls receive grazing light.
//   4. Add a second row of low fill PointLights at y=1.4 down the middle
//      so the mid-wall plaster is actually lit by something.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;

public static class HospitalPackFixV2
{
    const string MatDir = "Assets/Dnk_Dev/HospitalHorrorPack/Models/Materials";

    [MenuItem("Tools/Hospital Pack Fix V2 (Batch)")]
    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Hospital Pack Fix V2")]
    public static void Run()
    {
        Debug.Log("=== HospitalPackFixV2 START ===");
        BrightenWallMaterial();
        FixScene("Assets/Scenes/Hospital.unity",         "1F");
        FixScene("Assets/Scenes/Hospital2F.unity",       "2F");
        FixScene("Assets/Scenes/Hospital3F.unity",       "3F");
        FixScene("Assets/Scenes/HospitalBasement.unity", "Bsm");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalPackFixV2 DONE ===");
    }

    // ──────────────────────────────────────────────────────────────────
    static void BrightenWallMaterial()
    {
        // Upper wall plaster: pack texture is dark and the upper-wall mesh's
        // normals appear to face outward (away from corridor), so back faces
        // were being culled. Disable culling (double-sided) AND lift base
        // color and emission so it reads as a cream painted wall regardless
        // of which side we're viewing.
        var wall = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Walllime01_C.mat");
        if (wall != null)
        {
            wall.SetColor("_BaseColor", new Color(2.50f, 2.40f, 2.20f, 1f));
            wall.SetColor("_Color",     new Color(2.50f, 2.40f, 2.20f, 1f));
            wall.SetColor("_EmissionColor",
                new Color(235f/255f, 225f/255f, 210f/255f, 1f) * 0.60f);
            wall.EnableKeyword("_EMISSION");
            wall.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            wall.SetFloat("_Smoothness", 0.05f);
            wall.SetFloat("_Cull", 0f);
            wall.doubleSidedGI = true;
            EditorUtility.SetDirty(wall);
            Debug.Log("Mat_Walllime01_C brightened + double-sided");
        }

        // Wainscot tile: lift base color slightly + double-sided
        var tile = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Tile02.mat");
        if (tile != null)
        {
            tile.SetColor("_BaseColor", new Color(1.10f, 1.10f, 1.05f, 1f));
            tile.SetColor("_Color",     new Color(1.10f, 1.10f, 1.05f, 1f));
            tile.SetFloat("_Cull", 0f);
            tile.doubleSidedGI = true;
            EditorUtility.SetDirty(tile);
            Debug.Log("Mat_Tile02 lifted + double-sided");
        }
        // Ceiling: also double-sided
        var ceil = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Walllime02.mat");
        if (ceil != null)
        {
            ceil.SetFloat("_Cull", 0f);
            ceil.doubleSidedGI = true;
            EditorUtility.SetDirty(ceil);
            Debug.Log("Mat_Walllime02 double-sided");
        }
        AssetDatabase.SaveAssets();
    }

    // ──────────────────────────────────────────────────────────────────
    static void FixScene(string scenePath, string label)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) return;

        // Crank existing FixLights_<label> PointLights brighter and wider,
        // plus add a second row of mid-wall fills.
        var rootGo = GameObject.Find($"FixLights_{label}");
        if (rootGo != null)
        {
            foreach (var l in rootGo.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Point)
                {
                    l.intensity = 1.8f;
                    l.range = 12f;
                    l.shadows = LightShadows.None;
                    EditorUtility.SetDirty(l);
                }
            }
            Debug.Log($"[{label}] ceiling lights cranked");
        }

        // Add a second row of mid-wall fill lights so the upper-wall plaster
        // is actually receiving illumination from something close to it.
        var midRoot = GameObject.Find($"FixLights_{label}_Mid");
        if (midRoot != null)
        {
            for (int i = midRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(midRoot.transform.GetChild(i).gameObject);
        }
        else
        {
            midRoot = new GameObject($"FixLights_{label}_Mid");
        }
        float zStart, zEnd; int count;
        switch (label)
        {
            case "1F":  zStart = -12f; zEnd = 12f; count = 4; break;
            case "2F":  zStart = -18f; zEnd = 18f; count = 5; break;
            case "3F":  zStart = -16f; zEnd = 16f; count = 4; break;
            case "Bsm": zStart = -12f; zEnd = 12f; count = 4; break;
            default: zStart = 0f; zEnd = 0f; count = 0; break;
        }
        Color k6500 = new Color(0.98f, 0.96f, 1.00f);
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0.5f : (i / (float)(count - 1));
            float z = Mathf.Lerp(zStart, zEnd, t);
            var lg = new GameObject($"PMid_{label}_{i}");
            lg.transform.SetParent(midRoot.transform, true);
            lg.transform.position = new Vector3(0f, 1.4f, z);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 1.2f;
            l.range = 8f;
            l.color = k6500;
            l.shadows = LightShadows.None;
        }
        Debug.Log($"[{label}] mid-wall fills: {count}");

        EditorSceneManager.SaveScene(scene);
    }
}
