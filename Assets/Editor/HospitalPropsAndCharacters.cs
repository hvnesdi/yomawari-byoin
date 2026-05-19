// HospitalPropsAndCharacters.cs
//
// Adds corridor props (noticeboards / clocks / fire extinguishers / nurse
// counter / scattered papers) and demo enemy + NPC characters that
// react to HallucinationSystem.
//
// Entry points:
//   Tools/Add Hospital Props + Characters    (interactive)
//   HospitalPropsAndCharacters.RunBatch      (-executeMethod)
//   HospitalPropsAndCharacters.CaptureFour   (4 PNG screenshots)

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

public static class HospitalPropsAndCharacters
{
    const string MatDir  = "Assets/Materials";
    const string PropMatDir = "Assets/Materials/Props";
    const string TexDir  = "Assets/Textures/Generated";

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Add Hospital Props + Characters")]
    public static void Run()
    {
        Debug.Log("=== Props+Characters START ===");
        EnsureFolder(PropMatDir);
        BuildPropMaterials();
        BuildCharacterMaterials();
        Apply1F();
        Apply2F();
        Apply3F();
        ApplyBasement();
        BuildCharacterShowcase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== Props+Characters DONE ===");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    // ──────────────────────────────────────────────────────────────────
    // Materials
    // ──────────────────────────────────────────────────────────────────
    static Material _noticeFrame, _noticeFace, _clockFrame, _clockFace, _glass;
    static Material _extRed, _extLabel, _extMetal;
    static Material _wood, _phoneBlack, _paperFlat;
    static Material _guardUniform, _guardFace, _guardCap;
    static Material _shadowFigure;
    static Material _doctorCoat, _doctorPants, _doctorSkin;
    static Material _nurseUniform, _patientGown;
    static Material _ghostBody;

    static void BuildPropMaterials()
    {
        _noticeFrame = ColorMat("Prop_NoticeFrame", new Color(0.40f, 0.32f, 0.24f), 0.85f, 0f);
        _noticeFace  = TexMat("Prop_NoticeFace",  $"{TexDir}/Prop_Noticeboard.png", 0.20f);
        _clockFrame  = ColorMat("Prop_ClockFrame", new Color(0.48f, 0.44f, 0.40f), 0.55f, 0.45f);
        _clockFace   = TexMat("Prop_ClockFace",   $"{TexDir}/Prop_ClockFace.png", 0.10f);
        _glass       = ColorMat("Prop_Glass",     new Color(0.85f, 0.88f, 0.93f), 0.10f, 0.0f);
        _glass.SetFloat("_Surface", 1f);
        _glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        _glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        _glass.SetFloat("_ZWrite", 0f);
        _glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _glass.renderQueue = (int)RenderQueue.Transparent;
        var glassCol = _glass.GetColor("_BaseColor"); glassCol.a = 0.18f;
        _glass.SetColor("_BaseColor", glassCol);

        _extRed   = ColorMat("Prop_ExtRed",   new Color(0.70f, 0.12f, 0.12f), 0.45f, 0.05f);
        _extLabel = TexMat("Prop_ExtLabel",   $"{TexDir}/Prop_FireExtLabel.png", 0.30f);
        _extMetal = ColorMat("Prop_ExtMetal", new Color(0.55f, 0.55f, 0.55f), 0.40f, 0.85f);

        _wood       = ColorMat("Prop_Wood",       new Color(0.35f, 0.25f, 0.18f), 0.88f, 0f);
        _phoneBlack = ColorMat("Prop_PhoneBlack", new Color(0.10f, 0.10f, 0.11f), 0.40f, 0.05f);
        _paperFlat  = TexMat("Prop_PaperFlat",    $"{TexDir}/Prop_PaperChart.png", 0.50f);

        EditorUtility.SetDirty(_noticeFrame); EditorUtility.SetDirty(_noticeFace);
        EditorUtility.SetDirty(_clockFrame);  EditorUtility.SetDirty(_clockFace);
        EditorUtility.SetDirty(_glass);
        EditorUtility.SetDirty(_extRed); EditorUtility.SetDirty(_extLabel); EditorUtility.SetDirty(_extMetal);
        EditorUtility.SetDirty(_wood);  EditorUtility.SetDirty(_phoneBlack); EditorUtility.SetDirty(_paperFlat);
    }

    static void BuildCharacterMaterials()
    {
        // ── Guard ──
        _guardUniform = ColorMat("Char_GuardUniform", new Color(80/255f, 80/255f, 85/255f), 0.95f, 0f);
        _guardFace    = ColorMat("Char_GuardFace",    new Color(200/255f,170/255f,140/255f), 0.85f, 0f);
        _guardCap     = ColorMat("Char_GuardCap",     new Color(45/255f, 45/255f, 50/255f), 0.95f, 0f);

        // ── Shadow figure (high-hallucination guard) ──
        _shadowFigure = ColorMat("Char_ShadowFigure", new Color(10/255f, 10/255f, 15/255f), 1.0f, 0f);
        _shadowFigure.EnableKeyword("_EMISSION");
        _shadowFigure.SetColor("_EmissionColor", new Color(0.04f, 0.02f, 0.06f));
        _shadowFigure.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        // ── Doctor ──
        _doctorCoat  = ColorMat("Char_DoctorCoat",  new Color(0.94f, 0.94f, 0.93f), 0.90f, 0f);
        _doctorPants = ColorMat("Char_DoctorPants", new Color(0.45f, 0.46f, 0.48f), 0.95f, 0f);
        _doctorSkin  = _guardFace;

        // ── Nurse ──
        _nurseUniform = ColorMat("Char_NurseUniform", new Color(0.97f, 0.97f, 0.95f), 0.90f, 0f);

        // ── Patient ──
        _patientGown = ColorMat("Char_PatientGown", new Color(180/255f, 200/255f, 215/255f), 0.95f, 0f);

        // ── Ghost (high-hallucination NPC) ──
        _ghostBody = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        string ghostPath = $"{MatDir}/Char_Ghost.mat";
        AssetDatabase.CreateAsset(_ghostBody, ghostPath);
        _ghostBody = AssetDatabase.LoadAssetAtPath<Material>(ghostPath);
        _ghostBody.shader = Shader.Find("Universal Render Pipeline/Lit");
        _ghostBody.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.55f));
        _ghostBody.SetFloat("_Surface", 1f);     // transparent
        _ghostBody.SetFloat("_Blend", 0f);        // alpha
        _ghostBody.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        _ghostBody.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        _ghostBody.SetFloat("_ZWrite", 0f);
        _ghostBody.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _ghostBody.renderQueue = (int)RenderQueue.Transparent;
        _ghostBody.SetFloat("_Metallic", 0f);
        _ghostBody.SetFloat("_Smoothness", 0.10f);
        _ghostBody.EnableKeyword("_EMISSION");
        _ghostBody.SetColor("_EmissionColor", new Color(0.85f, 0.88f, 0.95f) * 0.45f);
        _ghostBody.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(_ghostBody);
    }

    static Material ColorMat(string name, Color col, float roughness, float metallic)
    {
        string p = $"{PropMatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, p);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        return mat;
    }

    static Material TexMat(string name, string diff, float smoothness)
    {
        string p = $"{PropMatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, p);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(diff);
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    // ──────────────────────────────────────────────────────────────────
    // Prop primitives
    // ──────────────────────────────────────────────────────────────────
    static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, Transform parent = null)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = name;
        c.transform.position = pos;
        if (rot.HasValue) c.transform.rotation = rot.Value;
        c.transform.localScale = scale;
        if (mat != null) c.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (parent != null) c.transform.SetParent(parent, true);
        return c;
    }

    static GameObject Quad(string name, Vector3 pos, Quaternion rot, Vector2 size, Material mat, Transform parent = null)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.position = pos;
        q.transform.rotation = rot;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        if (mat != null) q.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (parent != null) q.transform.SetParent(parent, true);
        return q;
    }

    static GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, Transform parent = null)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        c.transform.position = pos;
        if (rot.HasValue) c.transform.rotation = rot.Value;
        c.transform.localScale = scale;
        if (mat != null) c.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (parent != null) c.transform.SetParent(parent, true);
        return c;
    }

    static GameObject Sphere(string name, Vector3 pos, float r, Material mat, Transform parent = null)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = name;
        s.transform.position = pos;
        s.transform.localScale = Vector3.one * r * 2f;
        if (mat != null) s.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (parent != null) s.transform.SetParent(parent, true);
        return s;
    }

    // Composite: 0.8×0.6 m noticeboard at wall position, facing the wall normal.
    static GameObject MakeNoticeboard(string name, Vector3 wallPoint, Vector3 outwardNormal, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = wallPoint;
        // outwardNormal points away from the wall (into the room)
        Quaternion rot = Quaternion.LookRotation(-outwardNormal, Vector3.up);
        root.transform.rotation = rot;

        // Frame: wooden box behind the cork (0.85×0.65×0.04)
        var frame = Cube("Frame", root.transform.position + outwardNormal * 0.02f,
                          new Vector3(0.85f, 0.65f, 0.04f), _noticeFrame, rot, root.transform);
        // Cork face quad in front (0.78×0.58)
        var face = Quad("Face", root.transform.position + outwardNormal * 0.045f,
                         rot, new Vector2(0.78f, 0.58f), _noticeFace, root.transform);
        // The Quad's default normal is its -Z; LookRotation(-outwardNormal) makes
        // +Z point INTO the wall, so the visible side is correctly facing outward.
        return root;
    }

    // Composite: wall clock 0.30m diameter
    static GameObject MakeWallClock(string name, Vector3 wallPoint, Vector3 outwardNormal, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = wallPoint;
        Quaternion rot = Quaternion.LookRotation(-outwardNormal, Vector3.up);
        root.transform.rotation = rot;

        // Ring frame: thin cylinder on its side
        Quaternion ringRot = rot * Quaternion.Euler(90, 0, 0);
        var frame = Cylinder("Frame", root.transform.position + outwardNormal * 0.015f,
                              new Vector3(0.32f, 0.012f, 0.32f), _clockFrame, ringRot, root.transform);
        // Inset disc backing (matches the dial color, slightly recessed)
        var back = Cylinder("Backing", root.transform.position + outwardNormal * 0.010f,
                              new Vector3(0.29f, 0.011f, 0.29f), _clockFace, ringRot, root.transform);
        // Face quad in front
        var face = Quad("Face", root.transform.position + outwardNormal * 0.028f,
                         rot, new Vector2(0.28f, 0.28f), _clockFace, root.transform);
        // Glass cover (transparent quad, very slightly in front)
        var glass = Quad("Glass", root.transform.position + outwardNormal * 0.034f,
                          rot, new Vector2(0.28f, 0.28f), _glass, root.transform);
        return root;
    }

    // Composite: red fire extinguisher (cylinder + label + hose)
    static GameObject MakeExtinguisher(string name, Vector3 floorPos, Vector3 wallNormal, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = floorPos;

        // Body cylinder 0.18m diameter, 0.55m tall, center at y=0.32
        var body = Cylinder("Body", floorPos + Vector3.up * 0.32f,
                              new Vector3(0.18f, 0.275f, 0.18f), _extRed, Quaternion.identity, root.transform);
        // Top cap (slightly narrower)
        var cap = Cylinder("Cap", floorPos + Vector3.up * 0.60f,
                            new Vector3(0.14f, 0.03f, 0.14f), _extMetal, Quaternion.identity, root.transform);
        // Valve handle
        var valve = Cube("Valve", floorPos + Vector3.up * 0.66f,
                          new Vector3(0.10f, 0.04f, 0.04f), _extMetal, Quaternion.identity, root.transform);
        // Trigger lever
        Cube("Lever", floorPos + Vector3.up * 0.69f + new Vector3(0.05f, 0, 0),
              new Vector3(0.10f, 0.012f, 0.022f), _extMetal, Quaternion.identity, root.transform);
        // Hose - bent down via two thin cubes
        Cube("Hose1", floorPos + Vector3.up * 0.62f + new Vector3(0.10f, 0, 0),
              new Vector3(0.08f, 0.022f, 0.022f), _extMetal, Quaternion.identity, root.transform);
        Cube("Hose2", floorPos + Vector3.up * 0.52f + new Vector3(0.15f, 0, 0),
              new Vector3(0.022f, 0.20f, 0.022f), _extMetal, Quaternion.identity, root.transform);
        // Nozzle
        Cube("Nozzle", floorPos + Vector3.up * 0.40f + new Vector3(0.15f, 0, 0),
              new Vector3(0.04f, 0.05f, 0.04f), _extMetal, Quaternion.identity, root.transform);
        // Label decal facing the player (use wallNormal: extinguisher's "front" is opposite the wall)
        // wallNormal points away from wall into corridor, label should face same direction.
        Quaternion lblRot = Quaternion.LookRotation(-wallNormal, Vector3.up);
        Quad("Label", floorPos + Vector3.up * 0.32f + wallNormal * 0.091f,
              lblRot, new Vector2(0.26f, 0.46f), _extLabel, root.transform);
        // Wall bracket behind the cylinder
        Cube("Bracket", floorPos + Vector3.up * 0.40f - wallNormal * 0.10f,
              new Vector3(0.20f, 0.06f, 0.04f), _extMetal,
              Quaternion.LookRotation(wallNormal, Vector3.up), root.transform);
        return root;
    }

    // Composite: nurse counter (wood top, lower kickplate, side shelf, phone + papers)
    static GameObject MakeNurseCounter(string name, Vector3 corner, float length, float depth,
                                         Vector3 outwardNormal, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        Vector3 forward = outwardNormal.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 center = corner + right * (length * 0.5f) + forward * (depth * 0.5f);
        root.transform.position = center;
        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
        root.transform.rotation = rot;

        // Counter top 1.10m
        Cube("Top",       center + Vector3.up * 1.10f, new Vector3(length, 0.05f, depth + 0.05f),  _wood, rot, root.transform);
        // Front panel
        Cube("FrontPanel", center + Vector3.up * 0.55f + forward * (depth * 0.5f - 0.025f),
              new Vector3(length, 1.05f, 0.05f), _wood, rot, root.transform);
        // Side panels
        Cube("SideL", center + Vector3.up * 0.55f - right * (length * 0.5f - 0.025f),
              new Vector3(0.05f, 1.05f, depth), _wood, rot, root.transform);
        Cube("SideR", center + Vector3.up * 0.55f + right * (length * 0.5f - 0.025f),
              new Vector3(0.05f, 1.05f, depth), _wood, rot, root.transform);
        // Internal shelf
        Cube("Shelf", center + Vector3.up * 0.55f, new Vector3(length - 0.1f, 0.03f, depth - 0.05f), _wood, rot, root.transform);

        // Phone on top - cradle + handset
        Vector3 phoneCenter = center + Vector3.up * 1.155f - right * (length * 0.35f);
        Cube("PhoneBase",  phoneCenter, new Vector3(0.22f, 0.07f, 0.18f), _phoneBlack, rot, root.transform);
        Cube("PhoneHandle", phoneCenter + Vector3.up * 0.07f, new Vector3(0.24f, 0.05f, 0.06f), _phoneBlack, rot, root.transform);
        // Rotary dial
        Cylinder("PhoneDial", phoneCenter + Vector3.up * 0.05f + forward * 0.04f,
                  new Vector3(0.10f, 0.005f, 0.10f), _extMetal, rot * Quaternion.Euler(90, 0, 0), root.transform);

        // Stacked charts on the counter
        Vector3 chartsCenter = center + Vector3.up * 1.135f + right * (length * 0.25f);
        for (int i = 0; i < 5; i++)
        {
            float h = 0.005f;
            Cube($"Chart_{i}", chartsCenter + Vector3.up * (i * h + 0.003f) + new Vector3((i - 2) * 0.005f, 0, (i % 2 == 0 ? 0.01f : -0.01f)),
                  new Vector3(0.22f, h, 0.32f), _paperFlat, rot, root.transform);
        }

        // Internal shelf charts
        for (int i = 0; i < 4; i++)
        {
            Cube($"ShelfChart_{i}",
                  center + Vector3.up * 0.58f + right * ((i - 1.5f) * 0.18f),
                  new Vector3(0.16f, 0.06f, 0.30f), _paperFlat, rot, root.transform);
        }

        return root;
    }

    // Scatter a 0.21×0.30m paper sheet on the floor (rotated flat)
    static GameObject FloorPaper(string name, Vector3 pos, float yaw, Transform parent)
    {
        var q = Quad(name, pos, Quaternion.Euler(90, yaw, 0), new Vector2(0.21f, 0.30f), _paperFlat, parent);
        q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        return q;
    }

    static GameObject FloorMagazine(string name, Vector3 pos, float yaw, Transform parent)
    {
        // Small cube as folded magazine
        var c = Cube(name, pos, new Vector3(0.20f, 0.015f, 0.28f), _paperFlat,
                      Quaternion.Euler(0, yaw, 0), parent);
        return c;
    }

    static GameObject IVBag(string name, Vector3 pos, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = pos;
        // Bag: small box
        Cube("Bag", pos, new Vector3(0.18f, 0.04f, 0.22f), _glass, Quaternion.Euler(20f, 30f, 0f), root.transform);
        // Tubing: tiny cube
        Cube("Tube", pos + new Vector3(0.05f, 0.02f, 0.05f), new Vector3(0.012f, 0.012f, 0.18f),
              _extMetal, Quaternion.Euler(0, 30, 30), root.transform);
        return root;
    }

    // ──────────────────────────────────────────────────────────────────
    // Character composite (humanoid built from primitives)
    // ──────────────────────────────────────────────────────────────────
    static GameObject BuildHumanoid(string name, Vector3 pos, float yaw, Material body, Material face, Material extra,
                                     bool wearCap, Transform parent, out List<Renderer> bodyRends)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);

        bodyRends = new List<Renderer>();
        // Torso - capsule
        var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        torso.transform.SetParent(root.transform, false);
        torso.transform.localPosition = new Vector3(0, 1.10f, 0);
        torso.transform.localScale = new Vector3(0.55f, 0.45f, 0.32f);
        torso.GetComponent<MeshRenderer>().sharedMaterial = body;
        bodyRends.Add(torso.GetComponent<Renderer>());
        Object.DestroyImmediate(torso.GetComponent<Collider>());
        // Lower body (pants)
        var lower = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        lower.name = "Lower";
        lower.transform.SetParent(root.transform, false);
        lower.transform.localPosition = new Vector3(0, 0.45f, 0);
        lower.transform.localScale = new Vector3(0.50f, 0.45f, 0.30f);
        lower.GetComponent<MeshRenderer>().sharedMaterial = extra != null ? extra : body;
        bodyRends.Add(lower.GetComponent<Renderer>());
        Object.DestroyImmediate(lower.GetComponent<Collider>());
        // Arms
        for (int s = -1; s <= 1; s += 2)
        {
            var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            arm.name = "Arm_" + (s < 0 ? "L" : "R");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0.28f * s, 1.05f, 0);
            arm.transform.localScale = new Vector3(0.13f, 0.35f, 0.13f);
            arm.GetComponent<MeshRenderer>().sharedMaterial = body;
            bodyRends.Add(arm.GetComponent<Renderer>());
            Object.DestroyImmediate(arm.GetComponent<Collider>());
        }
        // Legs
        for (int s = -1; s <= 1; s += 2)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leg.name = "Leg_" + (s < 0 ? "L" : "R");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0.13f * s, 0.18f, 0);
            leg.transform.localScale = new Vector3(0.15f, 0.20f, 0.15f);
            leg.GetComponent<MeshRenderer>().sharedMaterial = extra != null ? extra : body;
            bodyRends.Add(leg.GetComponent<Renderer>());
            Object.DestroyImmediate(leg.GetComponent<Collider>());
        }
        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0, 1.62f, 0);
        head.transform.localScale = Vector3.one * 0.26f;
        head.GetComponent<MeshRenderer>().sharedMaterial = face != null ? face : body;
        bodyRends.Add(head.GetComponent<Renderer>());
        Object.DestroyImmediate(head.GetComponent<Collider>());
        if (wearCap)
        {
            // Cylinder cap, slightly wider
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Cap";
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0, 1.74f, 0);
            cap.transform.localScale = new Vector3(0.28f, 0.04f, 0.28f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = _guardCap;
            bodyRends.Add(cap.GetComponent<Renderer>());
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            // Cap brim
            var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brim.name = "CapBrim";
            brim.transform.SetParent(root.transform, false);
            brim.transform.localPosition = new Vector3(0, 1.71f, 0.10f);
            brim.transform.localScale = new Vector3(0.34f, 0.012f, 0.18f);
            brim.GetComponent<MeshRenderer>().sharedMaterial = _guardCap;
            bodyRends.Add(brim.GetComponent<Renderer>());
            Object.DestroyImmediate(brim.GetComponent<Collider>());
        }
        return root;
    }

    // ──────────────────────────────────────────────────────────────────
    // Scene-specific placements
    // ──────────────────────────────────────────────────────────────────

    static Transform EnsureRoot(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            for (int i = existing.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(existing.transform.GetChild(i).gameObject);
            return existing.transform;
        }
        return new GameObject(name).transform;
    }

    static void Apply1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        var root = EnsureRoot("ExtraProps_1F");

        // Corridor extents x∈[-2,+2] (inner faces -1.95 / +1.95), z∈[-16,+16]
        Vector3 nLeft  = new Vector3(+1f, 0f, 0f);  // wall on -X, points to +X
        Vector3 nRight = new Vector3(-1f, 0f, 0f);  // wall on +X, points to -X
        MakeNoticeboard("Noticeboard_1F_L1", new Vector3(-1.95f, 1.55f, -6f),  nLeft,  root);
        MakeNoticeboard("Noticeboard_1F_R1", new Vector3( 1.95f, 1.55f,  9f),  nRight, root);
        MakeWallClock("Clock_1F_L",          new Vector3(-1.95f, 2.35f, -2f),  nLeft,  root);
        MakeWallClock("Clock_1F_R",          new Vector3( 1.95f, 2.35f, 13f),  nRight, root);
        MakeExtinguisher("Ext_1F_L", new Vector3(-1.83f, 0.0f, -10f), nLeft,  root);
        MakeExtinguisher("Ext_1F_R", new Vector3( 1.83f, 0.0f,  6f),  nRight, root);

        // Floor scatter
        FloorPaper("Paper_1F_A",    new Vector3(-0.6f, 0.005f,  4f),  35f, root);
        FloorPaper("Paper_1F_B",    new Vector3( 0.7f, 0.005f, -7f), -25f, root);
        FloorPaper("Paper_1F_C",    new Vector3( 0.2f, 0.005f, 11f),  60f, root);
        FloorMagazine("Mag_1F",     new Vector3( 1.1f, 0.012f, -3f),  20f, root);
        IVBag("IVBag_1F",           new Vector3(-0.4f, 0.02f,  -4f), root);

        // ── Nurse Station inside Reception (z=-14 area, room is 6×3×6) ──
        // Reception extents: x∈[-3,+3], z∈[-17,-11]. Place counter at z=-12.5 facing +Z.
        MakeNurseCounter("NurseCounter_1F",
            corner: new Vector3(-1.5f, 0f, -12.7f),
            length: 3.0f, depth: 0.8f,
            outwardNormal: new Vector3(0, 0, 1f), parent: root);

        // ── Side shelf to the right of counter ──
        Cube("NurseSideShelf",
              new Vector3(2.6f, 0.75f, -14.5f),
              new Vector3(0.45f, 1.50f, 1.20f), _wood, Quaternion.identity, root);
        for (int i = 0; i < 4; i++)
        {
            Cube($"NurseShelfChart_{i}",
                  new Vector3(2.6f, 0.30f + i * 0.40f, -14.5f),
                  new Vector3(0.42f, 0.10f, 1.15f), _paperFlat, Quaternion.identity, root);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Props 1F applied");
    }

    static void Apply2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        var root = EnsureRoot("ExtraProps_2F");
        Vector3 nLeft  = new Vector3(+1f, 0f, 0f);
        Vector3 nRight = new Vector3(-1f, 0f, 0f);
        MakeNoticeboard("Noticeboard_2F_L1", new Vector3(-1.95f, 1.55f, -9f), nLeft,  root);
        MakeNoticeboard("Noticeboard_2F_R1", new Vector3( 1.95f, 1.55f, 7f),  nRight, root);
        MakeWallClock("Clock_2F_L",          new Vector3(-1.95f, 2.35f, 4f),  nLeft,  root);
        MakeWallClock("Clock_2F_R",          new Vector3( 1.95f, 2.35f, -18f),nRight, root);
        MakeExtinguisher("Ext_2F_L", new Vector3(-1.83f, 0.0f, -14f), nLeft,  root);
        MakeExtinguisher("Ext_2F_R", new Vector3( 1.83f, 0.0f,  16f), nRight, root);

        FloorPaper("Paper_2F_A", new Vector3(-0.7f, 0.005f,  0f),  45f, root);
        FloorPaper("Paper_2F_B", new Vector3( 0.9f, 0.005f, -11f),-30f, root);
        FloorMagazine("Mag_2F",  new Vector3( 0.4f, 0.012f, 18f),  10f, root);
        IVBag("IVBag_2F",        new Vector3(-1.0f, 0.02f,  12f), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Props 2F applied");
    }

    static void Apply3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        var root = EnsureRoot("ExtraProps_3F");
        // 3F corridor narrower: x∈[-1.5,+1.5]
        Vector3 nLeft  = new Vector3(+1f, 0f, 0f);
        Vector3 nRight = new Vector3(-1f, 0f, 0f);
        MakeNoticeboard("Noticeboard_3F_L1", new Vector3(-1.45f, 1.50f, -10f), nLeft,  root);
        MakeNoticeboard("Noticeboard_3F_R1", new Vector3( 1.45f, 1.50f,  9f),  nRight, root);
        MakeWallClock("Clock_3F_L", new Vector3(-1.45f, 2.20f, 2f),  nLeft,  root);
        MakeExtinguisher("Ext_3F_L", new Vector3(-1.35f, 0.0f, -15f), nLeft,  root);
        MakeExtinguisher("Ext_3F_R", new Vector3( 1.35f, 0.0f,  13f), nRight, root);
        FloorPaper("Paper_3F_A", new Vector3(-0.3f, 0.005f,  5f), 35f, root);
        FloorPaper("Paper_3F_B", new Vector3( 0.6f, 0.005f, -3f),-40f, root);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Props 3F applied");
    }

    static void ApplyBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        var root = EnsureRoot("ExtraProps_Bsm");
        // Basement no clocks/noticeboards (too clean), but a couple of extinguishers and scattered papers
        Vector3 nLeft  = new Vector3(+1f, 0f, 0f);
        Vector3 nRight = new Vector3(-1f, 0f, 0f);
        MakeExtinguisher("Ext_Bsm_L", new Vector3(-1.83f, 0.0f, -12f), nLeft,  root);
        MakeExtinguisher("Ext_Bsm_R", new Vector3( 1.83f, 0.0f,  10f), nRight, root);
        FloorPaper("Paper_Bsm_A", new Vector3( 0.3f, 0.005f, -3f), 25f, root);
        FloorPaper("Paper_Bsm_B", new Vector3(-0.6f, 0.005f,  6f),-65f, root);
        FloorPaper("Paper_Bsm_C", new Vector3( 0.0f, 0.005f, 12f), 95f, root);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Props Basement applied");
    }

    // ──────────────────────────────────────────────────────────────────
    // Character showcase scene
    //
    // Builds two enemy and two NPC instances in 1F so the screenshot
    // capture can show side-by-side gray/black guards and normal/ghost
    // NPCs. The HallucinationSystem hooks remain functional at runtime;
    // each character is built with EnemyAppearanceController /
    // NPCAppearanceController referencing both materials.
    // ──────────────────────────────────────────────────────────────────
    static void BuildCharacterShowcase()
    {
        // We use the 1F corridor as a stage. Position a roped-off area at z=-12..-13
        // out of the way of the noticeboard etc.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        var root = EnsureRoot("CharacterShowcase_1F");

        // ── Enemies side by side at z=-10, facing -Z (south) ──
        List<Renderer> rendsGuard, rendsShadow;
        var guardGO  = BuildHumanoid("Enemy_Guard_Preview",
            new Vector3(-1.0f, 0f, 14f), 180f, _guardUniform, _guardFace, _doctorPants,
            wearCap: true, parent: root, out rendsGuard);
        var enemyCtrlG = guardGO.AddComponent<EnemyAppearanceController>();
        enemyCtrlG.guardMaterial = _guardUniform;
        enemyCtrlG.shadowMaterial = _shadowFigure;
        enemyCtrlG.bodyRenderers = rendsGuard.ToArray();
        enemyCtrlG.overrideForScreenshot = true;
        enemyCtrlG.forceShadow = false;

        var shadowGO = BuildHumanoid("Enemy_Shadow_Preview",
            new Vector3( 1.0f, 0f, 14f), 180f, _shadowFigure, _shadowFigure, _shadowFigure,
            wearCap: true, parent: root, out rendsShadow);
        var enemyCtrlS = shadowGO.AddComponent<EnemyAppearanceController>();
        enemyCtrlS.guardMaterial = _guardUniform;
        enemyCtrlS.shadowMaterial = _shadowFigure;
        enemyCtrlS.bodyRenderers = rendsShadow.ToArray();
        enemyCtrlS.overrideForScreenshot = true;
        enemyCtrlS.forceShadow = true;

        // Make the shadow figure's cap, head, and lower body all the shadow material
        foreach (var r in rendsShadow)
            r.sharedMaterial = _shadowFigure;

        // ── NPC: doctor (normal) and ghost preview at z=-15 ──
        List<Renderer> docR, ghostR;
        var doctorGO = BuildHumanoid("NPC_Doctor_Preview",
            new Vector3(-1.0f, 0f, -10f), 0f, _doctorCoat, _doctorSkin, _doctorPants,
            wearCap: false, parent: root, out docR);
        var npcCtrlD = doctorGO.AddComponent<NPCAppearanceController>();
        npcCtrlD.kind = NPCAppearanceController.NPCKind.Doctor;
        npcCtrlD.normalMaterial = _doctorCoat;
        npcCtrlD.ghostMaterial = _ghostBody;
        npcCtrlD.bodyRenderers = docR.ToArray();
        npcCtrlD.overrideForScreenshot = true;
        npcCtrlD.forceGhost = false;

        var ghostGO = BuildHumanoid("NPC_Ghost_Preview",
            new Vector3( 1.0f, 0f, -10f), 0f, _ghostBody, _ghostBody, _ghostBody,
            wearCap: false, parent: root, out ghostR);
        var npcCtrlG = ghostGO.AddComponent<NPCAppearanceController>();
        npcCtrlG.kind = NPCAppearanceController.NPCKind.Doctor;
        npcCtrlG.normalMaterial = _doctorCoat;
        npcCtrlG.ghostMaterial = _ghostBody;
        npcCtrlG.bodyRenderers = ghostR.ToArray();
        npcCtrlG.overrideForScreenshot = true;
        npcCtrlG.forceGhost = true;
        foreach (var r in ghostR) r.sharedMaterial = _ghostBody;

        // Also a real "patient" NPC and "nurse" NPC nearby so the demo is richer
        List<Renderer> patR, nurseR;
        var patientGO = BuildHumanoid("NPC_Patient_Preview",
            new Vector3(-2.5f, 0f, -10f), 0f, _patientGown, _doctorSkin, _patientGown,
            wearCap: false, parent: root, out patR);
        var npcP = patientGO.AddComponent<NPCAppearanceController>();
        npcP.kind = NPCAppearanceController.NPCKind.Patient;
        npcP.normalMaterial = _patientGown;
        npcP.ghostMaterial = _ghostBody;
        npcP.bodyRenderers = patR.ToArray();
        npcP.overrideForScreenshot = true;
        npcP.forceGhost = false;

        var nurseGO = BuildHumanoid("NPC_Nurse_Preview",
            new Vector3(-1.7f, 0f, -10f), 0f, _nurseUniform, _doctorSkin, _nurseUniform,
            wearCap: false, parent: root, out nurseR);
        var npcN = nurseGO.AddComponent<NPCAppearanceController>();
        npcN.kind = NPCAppearanceController.NPCKind.Nurse;
        npcN.normalMaterial = _nurseUniform;
        npcN.ghostMaterial = _ghostBody;
        npcN.bodyRenderers = nurseR.ToArray();
        npcN.overrideForScreenshot = true;
        npcN.forceGhost = false;

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Character showcase built in 1F");
    }

    // ──────────────────────────────────────────────────────────────────
    // Screenshot capture
    // ──────────────────────────────────────────────────────────────────
    public static void CaptureFour()
    {
        try
        {
            string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";

            // 1F corridor with props — hide the showcase so we see clean corridor.
            // Camera near the north end, look south down the corridor.
            CaptureScene("Assets/Scenes/Hospital.unity",
                outDir + "/1F_CorridorProps.png",
                new Vector3(0.0f, 1.65f, 4f), new Vector3(6f, 180f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });

            // 2F corridor — re-shoot from middle so the props are at readable
            // distance instead of vanishing-point sized.
            CaptureScene("Assets/Scenes/Hospital2F.unity",
                outDir + "/2F_CorridorProps.png",
                new Vector3(0f, 1.65f, -12f), new Vector3(6f, 0f, 0f),
                hideRoots: null);

            // Enemy showcase: hide the NPC half of the showcase only.
            CaptureScene("Assets/Scenes/Hospital.unity",
                outDir + "/Enemy_Showcase.png",
                new Vector3(0f, 1.55f, 10.5f), new Vector3(3f, 0f, 0f),
                hideNames: new[] { "NPC_Doctor_Preview", "NPC_Ghost_Preview",
                                    "NPC_Patient_Preview", "NPC_Nurse_Preview" });

            // NPC showcase: hide the enemy half only.
            CaptureScene("Assets/Scenes/Hospital.unity",
                outDir + "/NPC_Showcase.png",
                new Vector3(0f, 1.55f, -7f), new Vector3(3f, 180f, 0f),
                hideNames: new[] { "Enemy_Guard_Preview", "Enemy_Shadow_Preview" });

            Debug.Log("=== Four screenshots captured ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            EditorApplication.Exit(1);
        }
    }

    static void CaptureScene(string scenePath, string outPath, Vector3 camPos, Vector3 camRot,
                              string[] hideRoots = null, string[] hideNames = null)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Hide requested roots / named objects for this capture (restored after).
        var restored = new List<GameObject>();
        if (hideRoots != null)
        {
            foreach (var n in hideRoots)
            {
                var hideGo = GameObject.Find(n);
                if (hideGo != null && hideGo.activeSelf) { hideGo.SetActive(false); restored.Add(hideGo); }
            }
        }
        if (hideNames != null)
        {
            foreach (var n in hideNames)
            {
                var hideGo = GameObject.Find(n);
                if (hideGo != null && hideGo.activeSelf) { hideGo.SetActive(false); restored.Add(hideGo); }
            }
        }

        var savedExp = new Dictionary<ColorAdjustments, (float, bool)>();
        var savedVig = new Dictionary<Vignette, (float, bool)>();
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (v.profile == null) continue;
            if (v.profile.TryGet<ColorAdjustments>(out var ca))
            {
                savedExp[ca] = (ca.postExposure.value, ca.postExposure.overrideState);
                ca.postExposure.value = ca.postExposure.value + 1.2f;
                ca.postExposure.overrideState = true;
            }
            if (v.profile.TryGet<Vignette>(out var vig))
            {
                savedVig[vig] = (vig.intensity.value, vig.intensity.overrideState);
                vig.intensity.value = Mathf.Max(0.15f, vig.intensity.value - 0.15f);
                vig.intensity.overrideState = true;
            }
        }

        var savedAmbMode = RenderSettings.ambientMode;
        var savedAmbLight = RenderSettings.ambientLight;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = savedAmbLight * 2.5f + new Color(0.08f, 0.08f, 0.09f);

        var fillGo = new GameObject("CaptureFill");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.intensity = 1.4f;
        fill.range = 24f;
        fill.color = new Color(1f, 0.95f, 0.88f);
        fill.shadows = LightShadows.None;
        fillGo.transform.position = camPos + Vector3.up * 0.3f;

        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.eulerAngles = camRot;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;

        int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Debug.Log($"Saved: {outPath} ({new FileInfo(outPath).Length / 1024} KB)");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(fillGo);
        RenderSettings.ambientMode = savedAmbMode;
        RenderSettings.ambientLight = savedAmbLight;
        foreach (var kv in savedExp) { kv.Key.postExposure.value = kv.Value.Item1; kv.Key.postExposure.overrideState = kv.Value.Item2; }
        foreach (var kv in savedVig) { kv.Key.intensity.value = kv.Value.Item1; kv.Key.intensity.overrideState = kv.Value.Item2; }
        foreach (var hg in restored) hg.SetActive(true);
        rt.Release();
    }

    // ──────────────────────────────────────────────────────────────────
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }
}
