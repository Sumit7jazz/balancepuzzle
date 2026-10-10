using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visual Parity Sprint 1 — one-shot zen riverbed environment builder.
/// Menu: Balance Puzzle/Setup Zen Environment. Re-running is idempotent.
///
/// VISUALS ONLY (Stage 1 physics lock respected):
/// - No root/collider/Rigidbody/COM/config/spawn transform is touched.
/// - Stones keep their spawn pads + colliders; only pad *visual meshes* become
///   organic rock discs, and stone/altar *materials* gain procedural PBR maps.
/// - Adds: gradient mist skybox, linear green-tinted fog, collider-less water
///   plane, frosted-glass inspection card restyle. Warm sunlight already matches
///   spec (1, 0.96, 0.84) so the light is left untouched.
/// </summary>
public static class ZenEnvironmentSetup
{
    private const string ScenePath = "Assets/Scenes/Stage1_Sandbox.unity";
    private const string TexDir = "Assets/Art/Textures/";
    private const string EnvDir = "Assets/Art/Environment/";

    private static readonly string[] StoneIds = { "Stone_A", "Stone_B", "Stone_C", "Stone_D" };

    [MenuItem("Balance Puzzle/Setup Zen Environment")]
    public static void SetupFromMenu() => Apply();

    public static void Apply()
    {
        AssetDatabase.Refresh();
        System.IO.Directory.CreateDirectory("Assets/Art/Environment");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ConfigureTextureImporters();
        ApplyRockMaterials();
        HealPlatformMesh();
        ApplySkyboxAndFog();
        EnsureWater();
        EnsurePadDiscs();
        RestyleInspectionCard();

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("ZenEnvironmentSetup: applied (idempotent, visuals-only).");
    }

    // ---------------- textures & rock materials ----------------

    private static void ConfigureTextureImporters()
    {
        foreach (string name in AllTextureSets())
        {
            SetImporter(TexDir + name + "_Albedo.png", TextureImporterType.Default, true);
            SetImporter(TexDir + name + "_Normal.png", TextureImporterType.NormalMap, false);
        }
    }

    private static IEnumerable<string> AllTextureSets()
    {
        foreach (string id in StoneIds) yield return id;
        yield return "Altar";
    }

    private static void SetImporter(string path, TextureImporterType type, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("ZenEnvironmentSetup: texture missing: " + path);
            return;
        }
        if (importer.textureType == type && importer.sRGBTexture == srgb)
            return;
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.SaveAndReimport();
    }

    private static void ApplyRockMaterials()
    {
        foreach (string id in StoneIds)
            ApplyRockMaterial("Assets/Art/Stones/" + id + "_Rock.mat", id);
        ApplyRockMaterial("Assets/Art/Platform/Platform_Altar.mat", "Altar");
    }

    private static void ApplyRockMaterial(string matPath, string texSet)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Debug.LogError("ZenEnvironmentSetup: material missing: " + matPath);
            return;
        }
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + texSet + "_Albedo.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + texSet + "_Normal.png");
        if (albedo != null && mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", albedo);
        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mat.HasProperty("_BumpScale"))
            mat.SetFloat("_BumpScale", 0.9f);
        // Matte weathered finish (spec: smoothness ~0.3-0.4).
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.35f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
        // Albedo PNGs already carry the per-stone tint; pull the multiplier
        // slightly below white so sunlit rock reads as weathered mid-grey
        // (reference) instead of bleached beige.
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.72f));
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", new Color(0.72f, 0.72f, 0.72f));
        EditorUtility.SetDirty(mat);
    }

    // ---------------- self-healing: altar mesh link ----------------

    private static void HealPlatformMesh()
    {
        var platform = GameObject.Find("Platform");
        if (platform == null)
        {
            Debug.LogError("ZenEnvironmentSetup: Platform missing.");
            return;
        }
        var filter = platform.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            return;
        // The FBX sub-asset link broke (missing mesh reference); rebind to the
        // first Mesh inside Platform_Altar.fbx. Collider/transform untouched.
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Platform/Platform_Altar.fbx"))
        {
            if (asset is Mesh mesh)
            {
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
                Debug.Log("ZenEnvironmentSetup: rebound Platform mesh to '" + mesh.name + "'.");
                return;
            }
        }
        Debug.LogError("ZenEnvironmentSetup: no Mesh found in Platform_Altar.fbx.");
    }

    // ---------------- skybox & fog ----------------

    private static void ApplySkyboxAndFog()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(EnvDir + "ZenMistSkybox.mat");
        if (sky == null)
        {
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null)
            {
                Debug.LogError("ZenEnvironmentSetup: Skybox/Procedural shader not found.");
            }
            else
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, EnvDir + "ZenMistSkybox.mat");
            }
        }
        if (sky != null)
        {
            // Misty bamboo-grove morning: pale green-white zenith glow, muted
            // ground. Thick atmosphere = hazy, near-white horizon (reference).
            SetColor(sky, "_SkyTint", new Color(0.72f, 0.80f, 0.76f));
            SetColor(sky, "_GroundColor", new Color(0.42f, 0.47f, 0.43f));
            SetFloat(sky, "_AtmosphereThickness", 0.9f);
            SetFloat(sky, "_Exposure", 1.15f);
            SetFloat(sky, "_SunSize", 0.035f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        // Misty green-tinted depth fog (spec #D6E2D8).
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.839f, 0.886f, 0.847f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 14f;
        RenderSettings.fogEndDistance = 60f;
    }

    private static void SetColor(Material mat, string prop, Color value)
    {
        if (mat.HasProperty(prop))
            mat.SetColor(prop, value);
    }

    private static void SetFloat(Material mat, string prop, float value)
    {
        if (mat.HasProperty(prop))
            mat.SetFloat(prop, value);
    }

    // ---------------- water ----------------

    private static void EnsureWater()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(EnvDir + "RiverWaterMesh.asset");
        if (mesh == null)
        {
            mesh = BuildQuadMesh(70f);
            AssetDatabase.CreateAsset(mesh, EnvDir + "RiverWaterMesh.asset");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(EnvDir + "RiverWater.mat");
        if (mat == null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(lit != null ? lit : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, EnvDir + "RiverWater.mat");
        }
        // Shallow river sheen: pale green-grey, mostly opaque, glossy.
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", new Color(0.60f, 0.70f, 0.68f, 0.70f));
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", new Color(0.60f, 0.70f, 0.68f, 0.70f));
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f); // transparent
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f); // alpha blend
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.9f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.05f);
        // Subtle ripple breakup: reuse the altar granite normal at wide tiling.
        var ripple = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Altar_Normal.png");
        if (ripple != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", ripple);
            mat.SetTextureScale("_BumpMap", new Vector2(9f, 9f));
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mat.HasProperty("_BumpScale"))
            mat.SetFloat("_BumpScale", 0.18f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);

        var go = GameObject.Find("RiverWater");
        if (go == null)
        {
            go = new GameObject("RiverWater");
            Debug.Log("ZenEnvironmentSetup: created RiverWater.");
        }
        var filter = go.GetComponent<MeshFilter>();
        if (filter == null)
            filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        // Sits just below the altar top (y=0): surrounds the slab, never touches
        // pads (tops at y=0.1) or stones. No collider: pure dressing.
        go.transform.position = new Vector3(0f, -0.08f, 0f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        // Belt-and-braces: a collider here would break physics, so strip any.
        foreach (var collider in go.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(collider);
    }

    private static Mesh BuildQuadMesh(float size)
    {
        float h = size * 0.5f;
        var mesh = new Mesh { name = "RiverWaterMesh" };
        mesh.SetVertices(new List<Vector3>
        {
            new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h),
            new Vector3(h, 0f, h), new Vector3(-h, 0f, h),
        });
        mesh.SetUVs(0, new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
        });
        mesh.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0); // up-facing winding (+Y)
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------- spawn pads: organic discs (visual mesh only) ----------------

    private static void EnsurePadDiscs()
    {
        var disc = AssetDatabase.LoadAssetAtPath<Mesh>(EnvDir + "SpawnPadDiscMesh.asset");
        if (disc == null)
        {
            disc = BuildRockDiscMesh(radius: 0.5f, seed: 7);
            AssetDatabase.CreateAsset(disc, EnvDir + "SpawnPadDiscMesh.asset");
        }
        foreach (string pad in new[] { "Spawn_A", "Spawn_B", "Spawn_C", "Spawn_D" })
        {
            var go = GameObject.Find(pad);
            if (go == null)
            {
                Debug.LogError("ZenEnvironmentSetup: pad missing: " + pad);
                continue;
            }
            // Visual mesh swap ONLY: transform, BoxCollider and material stay.
            var filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != disc)
            {
                filter.sharedMesh = disc;
                EditorUtility.SetDirty(filter);
            }
        }
        // Weather the pad tint toward river rock (Stage1 Platform.mat). Value-only
        // tweak: GUID, shader and all physics refs untouched.
        var padMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stage1/Platform.mat");
        if (padMat != null)
        {
            if (padMat.HasProperty("_BaseColor"))
                padMat.SetColor("_BaseColor", new Color(0.52f, 0.51f, 0.48f));
            if (padMat.HasProperty("_Color"))
                padMat.SetColor("_Color", new Color(0.52f, 0.51f, 0.48f));
            EditorUtility.SetDirty(padMat);
        }
    }

    private static Mesh BuildRockDiscMesh(float radius, int seed)
    {
        // Low jittered river-pebble disc: domed top, short skirt, flat bottom.
        const int segments = 20;
        var rng = new System.Random(seed);
        float[] jitter = new float[segments];
        for (int i = 0; i < segments; i++)
            jitter[i] = 0.88f + 0.24f * (float)rng.NextDouble();

        var verts = new List<Vector3>();
        var tris = new List<int>();
        // Top center + two top rings (dome).
        verts.Add(new Vector3(0f, 0.10f, 0f)); // 0
        for (int ring = 1; ring <= 2; ring++)
        {
            float r = radius * ring / 2f;
            float y = ring == 1 ? 0.085f : 0.02f;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * r * jitter[i], y, Mathf.Sin(a) * r * jitter[i]));
            }
        }
        int ring1 = 1, ring2 = 1 + segments;
        for (int i = 0; i < segments; i++)
        {
            int n = (i + 1) % segments;
            tris.Add(0); tris.Add(ring1 + n); tris.Add(ring1 + i);
            tris.Add(ring1 + i); tris.Add(ring1 + n); tris.Add(ring2 + n);
            tris.Add(ring1 + i); tris.Add(ring2 + n); tris.Add(ring2 + i);
        }
        // Skirt: outer top ring down to bottom ring.
        int skirtTop = ring2, skirtBottom = verts.Count;
        for (int i = 0; i < segments; i++)
        {
            Vector3 top = verts[skirtTop + i];
            verts.Add(new Vector3(top.x * 0.96f, -0.10f, top.z * 0.96f));
        }
        for (int i = 0; i < segments; i++)
        {
            int n = (i + 1) % segments;
            tris.Add(skirtTop + i); tris.Add(skirtBottom + i); tris.Add(skirtTop + n);
            tris.Add(skirtTop + n); tris.Add(skirtBottom + i); tris.Add(skirtBottom + n);
        }
        // Bottom cap.
        int bottomCenter = verts.Count;
        verts.Add(new Vector3(0f, -0.10f, 0f));
        for (int i = 0; i < segments; i++)
        {
            int n = (i + 1) % segments;
            tris.Add(bottomCenter); tris.Add(skirtBottom + i); tris.Add(skirtBottom + n);
        }

        var mesh = new Mesh { name = "SpawnPadDiscMesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        var uvs = new List<Vector2>(verts.Count);
        foreach (Vector3 v in verts)
            uvs.Add(new Vector2(v.x / (radius * 2f) + 0.5f, v.z / (radius * 2f) + 0.5f));
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------- frosted-glass inspection card ----------------

    private static void RestyleInspectionCard()
    {
        // The card starts inactive, so GameObject.Find cannot see it — scan
        // all transforms (including inactive) instead.
        Transform panel = null;
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform t in all)
        {
            if (t.name == "InfoPanel" && t.GetComponent<UnityEngine.UI.Image>() != null)
            {
                panel = t;
                break;
            }
        }
        if (panel == null)
        {
            Debug.Log("ZenEnvironmentSetup: no InfoPanel found; card restyle skipped.");
            return;
        }
        var bg = panel.GetComponent<UnityEngine.UI.Image>();
        if (bg != null)
        {
            // Frosted glass: near-white, translucent.
            bg.color = new Color(0.93f, 0.95f, 0.92f, 0.55f);
            EditorUtility.SetDirty(bg);
        }
        var text = panel.GetComponentInChildren<UnityEngine.UI.Text>();
        if (text != null)
        {
            text.color = new Color(0.13f, 0.17f, 0.15f);
            EditorUtility.SetDirty(text);
        }
    }
}
