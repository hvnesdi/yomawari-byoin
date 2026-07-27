// HospitalPackArchFix.cs
//
// Follow-up fixes to commit 143af92 (PBR Hospital Pack architecture):
//   - Black gap between wall top and ceiling -> stretch each wall in Y so
//     its world-space top reaches (slightly above) the local ceiling bottom.
//   - Wall texture tiles too big -> ensure Mat_Walllime* _BaseMap / _MainTex
//     scale is (8, 8).
//   - Ceiling too dark -> swap floor material on ceiling instances for a new
//     bright ceiling material (Base Color RGB 220/215/205) and lift each
//     scene's Flat ambient to (0.3, 0.3, 0.3).
//   - Add a per-floor-level fill PointLight just under the ceiling.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public static class HospitalPackArchFix
{
    const string CeilingMatPath = "Assets/Materials/Mat_Ceiling_Bright.mat";
    const string FloorMatPath   = "Assets/Dnk_Dev/HospitalHorrorPack/Models/Materials/Mat_Tile01.mat";
    const string Wall01MatPath  = "Assets/Dnk_Dev/HospitalHorrorPack/Models/Materials/Mat_Walllime01_C.mat";
    const string Wall02MatPath  = "Assets/Dnk_Dev/HospitalHorrorPack/Models/Materials/Mat_Walllime02.mat";

    [MenuItem("Tools/Fix PBR Hospital Pack Architecture")]
    public static void Run()
    {
        Debug.Log("=== HospitalPackArchFix START ===");
        EnsureWallTiling(Wall01MatPath);
        EnsureWallTiling(Wall02MatPath);
        var ceilMat = EnsureCeilingMaterial();
        FixScene("Assets/Scenes/Hospital.unity", "1F", ceilMat);
        FixScene("Assets/Scenes/Hospital2F.unity", "2F", ceilMat);
        FixScene("Assets/Scenes/Hospital3F.unity", "3F", ceilMat);
        FixScene("Assets/Scenes/HospitalBasement.unity", "Bsm", ceilMat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalPackArchFix DONE ===");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    static void EnsureWallTiling(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { Debug.LogWarning($"Missing material: {path}"); return; }
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(8, 8));
        if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", new Vector2(8, 8));
        if (m.HasProperty("_BumpMap")) m.SetTextureScale("_BumpMap", new Vector2(8, 8));
        EditorUtility.SetDirty(m);
        Debug.Log($"  Wall tiling: {path} -> 8x8");
    }

    static Material EnsureCeilingMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(CeilingMatPath);
        if (m == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(FloorMatPath);
            if (src == null) { Debug.LogError($"Missing source material {FloorMatPath}"); return null; }
            AssetDatabase.CopyAsset(FloorMatPath, CeilingMatPath);
            AssetDatabase.ImportAsset(CeilingMatPath);
            m = AssetDatabase.LoadAssetAtPath<Material>(CeilingMatPath);
        }
        if (m == null) { Debug.LogError($"Could not create {CeilingMatPath}"); return null; }
        var c = new Color(220f / 255f, 215f / 255f, 205f / 255f, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        if (m.HasProperty("_BaseMap"))   m.SetTextureScale("_BaseMap", new Vector2(8, 8));
        if (m.HasProperty("_MainTex"))   m.SetTextureScale("_MainTex", new Vector2(8, 8));
        EditorUtility.SetDirty(m);
        Debug.Log($"  Ceiling material ready: {CeilingMatPath}");
        return m;
    }

    static void FixScene(string scenePath, string label, Material ceilMat)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"Cannot open {scenePath}"); return; }
        var rootGo = GameObject.Find($"PackArch_{label}");
        if (rootGo == null) { Debug.LogWarning($"No PackArch_{label} root in {scenePath}"); return; }
        var root = rootGo.transform;

        var walls = new List<GameObject>();
        var slabGOs = new List<GameObject>();   // P_Floor_*/P_Ceiling_* children we'll later split

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i).gameObject;
            var n = child.name;
            if (n.StartsWith("P_Wall_"))
                walls.Add(child);
            else if (n.StartsWith("P_Floor_") || n.StartsWith("P_Ceiling_"))
                slabGOs.Add(child);
        }

        // Split slabs into floors vs ceilings by Y clustering: cluster all
        // slab Y positions; within each "storey" (pair of nearby clusters
        // < 4.5m apart), the lower cluster is the floor and the upper is the
        // ceiling. The rotation-based detection from the previous editor pass
        // turned out to round-trip ambiguously through the YAML hint, so we
        // use spatial position instead.
        var allSlabRenderers = new List<(GameObject go, Renderer r)>();
        foreach (var s in slabGOs)
        {
            var r = s.GetComponentInChildren<Renderer>();
            if (r != null) allSlabRenderers.Add((s, r));
        }
        var slabYs = new List<float>();
        foreach (var pair in allSlabRenderers) slabYs.Add(pair.r.bounds.center.y);
        var slabClusters = ClusterValues(slabYs, 1.0f); // group within 1m
        slabClusters.Sort();

        // Pair adjacent clusters: floor (lower) -> ceiling (upper) if gap is 1.5–4.5m.
        var storeyPairs = new List<(float floorY, float ceilingY)>();
        for (int i = 0; i + 1 < slabClusters.Count; i++)
        {
            float gap = slabClusters[i + 1] - slabClusters[i];
            if (gap >= 1.5f && gap <= 4.5f)
            {
                storeyPairs.Add((slabClusters[i], slabClusters[i + 1]));
                i++; // consumed both clusters
            }
        }

        var floorRenderers = new List<Renderer>();
        var ceilingRenderers = new List<Renderer>();
        var ceilingGOs = new List<GameObject>();
        foreach (var pair in allSlabRenderers)
        {
            float y = pair.r.bounds.center.y;
            bool isCeiling = false;
            foreach (var sp in storeyPairs)
            {
                // If this slab's Y is within 0.5 of a ceiling Y, classify as ceiling.
                if (Mathf.Abs(y - sp.ceilingY) < 0.5f) { isCeiling = true; break; }
            }
            if (isCeiling) { ceilingRenderers.Add(pair.r); ceilingGOs.Add(pair.go); }
            else            floorRenderers.Add(pair.r);
        }

        Debug.Log($"[{label}] walls={walls.Count} floorRenderers={floorRenderers.Count} ceilingRenderers={ceilingRenderers.Count} storeys={storeyPairs.Count}");

        var floorClusters = ClusterByY(floorRenderers, useTop: true);
        var ceilingClusters = ClusterByY(ceilingRenderers, useTop: false);
        Debug.Log($"[{label}] floorClusters={floorClusters.Count} ceilingClusters={ceilingClusters.Count}");

        // Pair floor cluster -> nearest ceiling cluster above
        var pairs = new List<(YCluster floor, YCluster ceiling)>();
        foreach (var fc in floorClusters)
        {
            YCluster best = null;
            float bestDy = float.MaxValue;
            foreach (var cc in ceilingClusters)
            {
                float dy = cc.y - fc.y;
                if (dy > 0.5f && dy < bestDy) { bestDy = dy; best = cc; }
            }
            if (best != null) pairs.Add((fc, best));
        }

        // ── Fix walls: stretch Y so wall is flush with ceiling above and floor below.
        int wallsFixed = 0;
        foreach (var w in walls)
        {
            var b = CombinedBounds(w);
            if (b.size.y < 0.05f) continue;
            float curMidY = b.center.y;
            (YCluster floor, YCluster ceiling) chosen = (null, null);
            float chosenScore = float.MaxValue;
            foreach (var p in pairs)
            {
                if (curMidY > p.floor.y - 0.5f && curMidY < p.ceiling.y + 0.5f)
                {
                    float score = Mathf.Abs(curMidY - (p.floor.y + p.ceiling.y) * 0.5f);
                    if (score < chosenScore) { chosenScore = score; chosen = p; }
                }
            }
            if (chosen.floor == null || chosen.ceiling == null) continue;
            // Generous overlap: walls should slightly clip into floor and ceiling so
            // no seam line is ever visible at the camera height we shoot from.
            float targetBottom = chosen.floor.y - 0.10f;
            float targetTop    = chosen.ceiling.y + 0.20f;
            float targetH = targetTop - targetBottom;
            if (targetH < 0.05f) continue;
            float factor = targetH / b.size.y;
            var s = w.transform.localScale;
            w.transform.localScale = new Vector3(s.x, s.y * factor, s.z);
            // Recompute combined bounds after scale, translate so bottom == targetBottom.
            var nb = CombinedBounds(w);
            float dy = targetBottom - nb.min.y;
            var p2 = w.transform.position;
            w.transform.position = new Vector3(p2.x, p2.y + dy, p2.z);
            wallsFixed++;
        }
        Debug.Log($"[{label}] walls stretched: {wallsFixed}");

        // ── Replace material on ceiling instances.
        int swapped = 0;
        foreach (var cgo in ceilingGOs)
        {
            foreach (var r in cgo.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int j = 0; j < mats.Length; j++)
                {
                    if (mats[j] != ceilMat) { mats[j] = ceilMat; changed = true; }
                }
                if (changed) { r.sharedMaterials = mats; swapped++; }
            }
        }
        Debug.Log($"[{label}] ceiling renderers re-materialed: {swapped}");

        // ── Ambient.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f, 1f);
        RenderSettings.ambientIntensity = 1f;

        // ── Per-storey fill point lights, just under ceiling.
        var fillRoot = EnsureFillRoot(label);
        // Clear existing children of fillRoot (idempotent re-runs).
        for (int i = fillRoot.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(fillRoot.GetChild(i).gameObject);

        foreach (var p in pairs)
        {
            // Spread several lights across the floor bounding box for even fill.
            var fb = p.floor.bounds;
            float cy = p.ceiling.y - 0.4f;
            float xSize = fb.size.x;
            float zSize = fb.size.z;
            int nx = Mathf.Clamp(Mathf.CeilToInt(xSize / 10f), 1, 4);
            int nz = Mathf.Clamp(Mathf.CeilToInt(zSize / 10f), 1, 4);
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    float fx = (nx == 1) ? 0.5f : (ix + 0.5f) / nx;
                    float fz = (nz == 1) ? 0.5f : (iz + 0.5f) / nz;
                    var pos = new Vector3(
                        fb.min.x + fx * fb.size.x,
                        cy,
                        fb.min.z + fz * fb.size.z);
                    var go = new GameObject($"CeilingFill_{label}_{p.floor.y:0.0}_{ix}_{iz}");
                    go.transform.SetParent(fillRoot, false);
                    go.transform.position = pos;
                    var L = go.AddComponent<Light>();
                    L.type = LightType.Point;
                    L.intensity = 0.5f;
                    L.range = 14f;
                    L.color = new Color(1f, 0.97f, 0.93f);  // ~6500K-ish slight warm
                    L.useColorTemperature = true;
                    L.colorTemperature = 6500f;
                    L.shadows = LightShadows.None;
                    L.bounceIntensity = 0f;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[{label}] saved");
    }

    class YCluster
    {
        public float y;
        public Bounds bounds;
    }

    static List<float> ClusterValues(List<float> values, float tolerance)
    {
        var sorted = new List<float>(values);
        sorted.Sort();
        var result = new List<float>();
        var current = new List<float>();
        foreach (var v in sorted)
        {
            if (current.Count == 0 || v - current[0] < tolerance) current.Add(v);
            else
            {
                float sum = 0; foreach (var x in current) sum += x;
                result.Add(sum / current.Count);
                current.Clear(); current.Add(v);
            }
        }
        if (current.Count > 0)
        {
            float sum = 0; foreach (var x in current) sum += x;
            result.Add(sum / current.Count);
        }
        return result;
    }

    static List<YCluster> ClusterByY(List<Renderer> renderers, bool useTop)
    {
        var clusters = new List<YCluster>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            float y = useTop ? r.bounds.max.y : r.bounds.min.y;
            YCluster found = null;
            foreach (var c in clusters)
            {
                if (Mathf.Abs(c.y - y) < 1.0f) { found = c; break; }
            }
            if (found == null)
            {
                found = new YCluster { y = y, bounds = r.bounds };
                clusters.Add(found);
            }
            else
            {
                found.bounds.Encapsulate(r.bounds);
                // Update y as running average of contributing top/bottom
                found.y = (found.y + y) * 0.5f;
            }
        }
        return clusters;
    }

    /// <summary>
    /// 指定オブジェクト配下の全 Renderer を包むワールド空間の境界を返す。
    /// 壁は複数メッシュの組み合わせでできているため、単一 Renderer の bounds では足りない。
    /// </summary>
    static Bounds CombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    static Transform EnsureFillRoot(string label)
    {
        var name = $"PackArch_FillLights_{label}";
        var existing = GameObject.Find(name);
        if (existing != null) return existing.transform;
        return new GameObject(name).transform;
    }
}
