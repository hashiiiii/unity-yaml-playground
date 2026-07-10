using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Headless generator for a UnityYAML diff corpus. Run twice:
//   Unity -batchmode -quit -projectPath . -executeMethod FixtureGenerator.Create   (commit as base)
//   Unity -batchmode -quit -projectPath . -executeMethod FixtureGenerator.Mutate   (commit on a branch, open a PR)
// Create produces one asset per supported extension under Assets/Fixtures.
// Mutate applies representative edits (modified fields, added/removed documents,
// renamed asset, added/deleted files) so a PR shows every diff category.
public static class FixtureGenerator
{
    const string Root = "Assets/Fixtures";
    static readonly List<string> Errors = new List<string>();

    public static void Create()
    {
        // No StartAssetEditing batching: later steps load assets created by earlier
        // ones, which requires each CreateAsset to import immediately.
        Step("material", CreateMaterial);
        Step("doomed material", CreateDoomedMaterial);
        Step("animation clips", CreateAnimationClips);
        Step("animator controller", CreateAnimatorController);
        Step("override controller", CreateOverrideController);
        Step("scriptable object", CreateScriptableObject);
        Step("physics materials", CreatePhysicsMaterials);
        Step("render texture", CreateRenderTexture);
        Step("terrain layer", CreateTerrainLayer);
        Step("avatar mask", CreateAvatarMask);
        Step("gui skin", CreateGuiSkin);
        Step("shader variants", CreateShaderVariants);
        Step("preset", CreatePreset);
        Step("lighting settings", CreateLightingSettings);
        Step("prefabs", CreatePrefabs);
        Step("scene", CreateScene);
        AssetDatabase.SaveAssets();
        Finish("Create");
    }

    public static void Mutate()
    {
        Step("material", MutateMaterial);
        Step("delete doomed material", () => AssetDatabase.DeleteAsset($"{Root}/Doomed.mat"));
        Step("animation clips", MutateAnimationClips);
        Step("added clip", CreateAddedClip);
        Step("animator controller", MutateAnimatorController);
        Step("override controller", MutateOverrideController);
        Step("scriptable object", MutateScriptableObject);
        Step("physics materials", MutatePhysicsMaterials);
        Step("render texture", MutateRenderTexture);
        Step("terrain layer", MutateTerrainLayer);
        Step("avatar mask", MutateAvatarMask);
        Step("gui skin", MutateGuiSkin);
        Step("shader variants", MutateShaderVariants);
        Step("preset", MutatePreset);
        Step("lighting settings", MutateLightingSettings);
        Step("prefabs", MutatePrefabs);
        Step("scene", MutateScene);
        AssetDatabase.SaveAssets();
        Finish("Mutate");
    }

    // ---- material (.mat): modified fields, keyword toggle; Doomed.mat covers file deletion ----

    static void CreateMaterial()
    {
        var mat = new Material(Shader.Find("Standard")) { color = new Color(0.8f, 0.2f, 0.2f, 1f) };
        mat.SetFloat("_Glossiness", 0.5f);
        mat.SetFloat("_Metallic", 0.1f);
        AssetDatabase.CreateAsset(mat, $"{Root}/Fixture.mat");
    }

    static void CreateDoomedMaterial()
    {
        AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), $"{Root}/Doomed.mat");
    }

    static void MutateMaterial()
    {
        var mat = Load<Material>($"{Root}/Fixture.mat");
        mat.color = new Color(0.1f, 0.4f, 0.9f, 1f);
        mat.SetFloat("_Metallic", 0.75f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 0.3f, 1f));
        EditorUtility.SetDirty(mat);
    }

    // ---- animation (.anim): key edits, added curve; Added.anim covers file addition ----

    static void CreateAnimationClips()
    {
        foreach (var name in new[] { "Bounce", "Spin" })
        {
            var clip = new AnimationClip { frameRate = 60 };
            var curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 2f);
            clip.SetCurve("", typeof(Transform), "localPosition.y", curve);
            AssetDatabase.CreateAsset(clip, $"{Root}/{name}.anim");
        }
    }

    static void MutateAnimationClips()
    {
        var clip = Load<AnimationClip>($"{Root}/Bounce.anim");
        clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.EaseInOut(0f, 0.5f, 2f, 3f));
        clip.SetCurve("", typeof(Transform), "localEulerAngles.z", AnimationCurve.Linear(0f, 0f, 1f, 90f));
        EditorUtility.SetDirty(clip);
    }

    static void CreateAddedClip()
    {
        var clip = new AnimationClip { frameRate = 30 };
        clip.SetCurve("", typeof(Transform), "localScale.x", AnimationCurve.Constant(0f, 1f, 1.2f));
        AssetDatabase.CreateAsset(clip, $"{Root}/Added.anim");
    }

    // ---- animator controller (.controller): added state/transition, changed fields ----

    static void CreateAnimatorController()
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath($"{Root}/Fixture.controller");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = Load<AnimationClip>($"{Root}/Bounce.anim");
        var walk = sm.AddState("Walk");
        walk.speed = 1.0f;
        var t = idle.AddTransition(walk);
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.duration = 0.25f;
    }

    static void MutateAnimatorController()
    {
        var controller = Load<AnimatorController>($"{Root}/Fixture.controller");
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        var sm = controller.layers[0].stateMachine;
        var run = sm.AddState("Run");
        run.speed = 2.0f;
        foreach (var child in sm.states)
        {
            if (child.state.name != "Walk") continue;
            child.state.speed = 1.5f;
            var t = child.state.AddTransition(run);
            t.AddCondition(AnimatorConditionMode.Greater, 3.0f, "Speed");
        }
        EditorUtility.SetDirty(controller);
    }

    // ---- override controller (.overrideController): clip reference swap ----

    static void CreateOverrideController()
    {
        var over = new AnimatorOverrideController(Load<AnimatorController>($"{Root}/Fixture.controller"));
        over["Bounce"] = Load<AnimationClip>($"{Root}/Spin.anim");
        AssetDatabase.CreateAsset(over, $"{Root}/Fixture.overrideController");
    }

    static void MutateOverrideController()
    {
        var over = Load<AnimatorOverrideController>($"{Root}/Fixture.overrideController");
        over["Bounce"] = Load<AnimationClip>($"{Root}/Bounce.anim");
        EditorUtility.SetDirty(over);
    }

    // ---- scriptable object (.asset): scalar edits, list reorder/add, reference clear ----

    static void CreateScriptableObject()
    {
        var data = ScriptableObject.CreateInstance<FixtureData>();
        data.hitPoints = 100;
        data.title = "fixture";
        data.weights = new[] { 0.1f, 0.2f, 0.7f };
        data.items.Add(new FixtureData.Item { itemName = "sword", cost = 50, consumable = false });
        data.items.Add(new FixtureData.Item { itemName = "potion", cost = 10, consumable = true });
        data.materialRef = Load<Material>($"{Root}/Fixture.mat");
        AssetDatabase.CreateAsset(data, $"{Root}/Fixture.asset");
    }

    static void MutateScriptableObject()
    {
        var data = Load<FixtureData>($"{Root}/Fixture.asset");
        data.hitPoints = 150;
        data.title = "fixture v2";
        data.weights = new[] { 0.7f, 0.2f, 0.1f, 0.05f };
        data.items.Reverse();
        data.items.Add(new FixtureData.Item { itemName = "shield", cost = 80, consumable = false });
        data.materialRef = null;
        data.tint = new Color(1f, 0.5f, 0f, 1f);
        EditorUtility.SetDirty(data);
    }

    // ---- physics materials (.physicMaterial / .physicsMaterial2D) ----

    static void CreatePhysicsMaterials()
    {
        var mat3d = new PhysicsMaterial("Fixture3D") { dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0.1f };
        AssetDatabase.CreateAsset(mat3d, $"{Root}/Fixture.physicMaterial");
        var mat2d = new PhysicsMaterial2D("Fixture2D") { friction = 0.4f, bounciness = 0.2f };
        AssetDatabase.CreateAsset(mat2d, $"{Root}/Fixture.physicsMaterial2D");
    }

    static void MutatePhysicsMaterials()
    {
        var mat3d = Load<PhysicsMaterial>($"{Root}/Fixture.physicMaterial");
        mat3d.bounciness = 0.9f;
        mat3d.bounceCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(mat3d);
        var mat2d = Load<PhysicsMaterial2D>($"{Root}/Fixture.physicsMaterial2D");
        mat2d.friction = 0.05f;
        EditorUtility.SetDirty(mat2d);
    }

    // ---- render texture (.renderTexture): size/AA edits via SerializedObject ----

    static void CreateRenderTexture()
    {
        var rt = new RenderTexture(256, 256, 24) { filterMode = FilterMode.Bilinear };
        AssetDatabase.CreateAsset(rt, $"{Root}/Fixture.renderTexture");
    }

    static void MutateRenderTexture()
    {
        var rt = Load<RenderTexture>($"{Root}/Fixture.renderTexture");
        var so = new SerializedObject(rt);
        so.FindProperty("m_Width").intValue = 512;
        so.FindProperty("m_Height").intValue = 512;
        so.FindProperty("m_AntiAliasing").intValue = 4;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rt);
    }

    // ---- terrain layer (.terrainlayer): value edits + FILE RENAME in Mutate ----

    static void CreateTerrainLayer()
    {
        var layer = new TerrainLayer { tileSize = new Vector2(15f, 15f), tileOffset = Vector2.zero, metallic = 0f };
        AssetDatabase.CreateAsset(layer, $"{Root}/Fixture.terrainlayer");
    }

    static void MutateTerrainLayer()
    {
        var layer = Load<TerrainLayer>($"{Root}/Fixture.terrainlayer");
        layer.tileSize = new Vector2(4f, 4f);
        layer.metallic = 0.3f;
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();
        // Rename after saving so the PR shows a renamed+modified file in one change.
        var err = AssetDatabase.MoveAsset($"{Root}/Fixture.terrainlayer", $"{Root}/Ground.terrainlayer");
        if (!string.IsNullOrEmpty(err)) throw new Exception(err);
    }

    // ---- avatar mask (.mask): humanoid part + transform path toggles ----

    static void CreateAvatarMask()
    {
        var mask = new AvatarMask { transformCount = 2 };
        mask.SetTransformPath(0, "Root");
        mask.SetTransformActive(0, true);
        mask.SetTransformPath(1, "Root/Arm");
        mask.SetTransformActive(1, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
        AssetDatabase.CreateAsset(mask, $"{Root}/Fixture.mask");
    }

    static void MutateAvatarMask()
    {
        var mask = Load<AvatarMask>($"{Root}/Fixture.mask");
        mask.SetTransformActive(1, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        EditorUtility.SetDirty(mask);
    }

    // ---- gui skin (.guiskin): nested style edits ----

    static void CreateGuiSkin()
    {
        var skin = ScriptableObject.CreateInstance<GUISkin>();
        skin.button = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        skin.label = new GUIStyle { fontSize = 11 };
        AssetDatabase.CreateAsset(skin, $"{Root}/Fixture.guiskin");
    }

    static void MutateGuiSkin()
    {
        var skin = Load<GUISkin>($"{Root}/Fixture.guiskin");
        skin.button.fontSize = 14;
        skin.button.normal.textColor = new Color(0.9f, 0.9f, 0.2f, 1f);
        EditorUtility.SetDirty(skin);
    }

    // ---- shader variants (.shadervariants): added variant ----

    static void CreateShaderVariants()
    {
        var collection = new ShaderVariantCollection();
        collection.Add(new ShaderVariantCollection.ShaderVariant(Shader.Find("Standard"), PassType.ForwardBase));
        AssetDatabase.CreateAsset(collection, $"{Root}/Fixture.shadervariants");
    }

    static void MutateShaderVariants()
    {
        var collection = Load<ShaderVariantCollection>($"{Root}/Fixture.shadervariants");
        collection.Add(new ShaderVariantCollection.ShaderVariant(Shader.Find("Standard"), PassType.ForwardBase, "_EMISSION"));
        EditorUtility.SetDirty(collection);
    }

    // ---- preset (.preset): re-captured from the mutated material ----

    static void CreatePreset()
    {
        var preset = new Preset(Load<Material>($"{Root}/Fixture.mat"));
        AssetDatabase.CreateAsset(preset, $"{Root}/Fixture.preset");
    }

    static void MutatePreset()
    {
        // Mutate() changes Fixture.mat first, so re-capturing snapshots the new values.
        var preset = Load<Preset>($"{Root}/Fixture.preset");
        preset.UpdateProperties(Load<Material>($"{Root}/Fixture.mat"));
        EditorUtility.SetDirty(preset);
    }

    // ---- lighting settings (.lighting) ----

    static void CreateLightingSettings()
    {
        var settings = new LightingSettings { bakedGI = true, realtimeGI = false, indirectResolution = 2f };
        AssetDatabase.CreateAsset(settings, $"{Root}/Fixture.lighting");
    }

    static void MutateLightingSettings()
    {
        var settings = Load<LightingSettings>($"{Root}/Fixture.lighting");
        settings.indirectResolution = 4f;
        settings.albedoBoost = 2f;
        EditorUtility.SetDirty(settings);
    }

    // ---- prefabs (.prefab + variant): component add/remove, value edits, child rename ----

    static void CreatePrefabs()
    {
        var root = new GameObject("Robot");
        try
        {
            root.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 5f;
            var fixture = root.AddComponent<FixtureBehaviour>();
            fixture.speed = 2.5f;
            fixture.label = "robot";

            var arm = new GameObject("Arm");
            arm.transform.SetParent(root.transform);
            arm.transform.localPosition = new Vector3(0.5f, 1f, 0f);
            arm.AddComponent<SphereCollider>().radius = 0.25f;

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 2f, 0f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Robot.prefab");

            // A variant with an override, so variant-specific YAML (PrefabInstance
            // document with m_Modifications) is exercised too.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.GetComponent<FixtureBehaviour>().level = 7;
                PrefabUtility.SaveAsPrefabAsset(instance, $"{Root}/RobotVariant.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void MutatePrefabs()
    {
        var contents = PrefabUtility.LoadPrefabContents($"{Root}/Robot.prefab");
        try
        {
            contents.GetComponent<Rigidbody>().mass = 12f;
            UnityEngine.Object.DestroyImmediate(contents.GetComponent<BoxCollider>());
            contents.AddComponent<CapsuleCollider>().height = 2.2f;
            var fixture = contents.GetComponent<FixtureBehaviour>();
            fixture.speed = 4.0f;
            fixture.offset = new Vector3(0f, 0.5f, 0f);
            var head = contents.transform.Find("Head");
            head.name = "Sensor";
            head.localPosition = new Vector3(0f, 2.2f, 0.1f);
            PrefabUtility.SaveAsPrefabAsset(contents, $"{Root}/Robot.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        var variantContents = PrefabUtility.LoadPrefabContents($"{Root}/RobotVariant.prefab");
        try
        {
            variantContents.GetComponent<FixtureBehaviour>().level = 9;
            variantContents.GetComponent<FixtureBehaviour>().label = "variant";
            PrefabUtility.SaveAsPrefabAsset(variantContents, $"{Root}/RobotVariant.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(variantContents);
        }
    }

    // ---- scene (.unity): object move/add/remove, light edits ----

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        light.color = Color.white;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Crate";
        cube.transform.position = new Vector3(0f, 0.5f, 0f);

        var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        child.name = "Marble";
        child.transform.SetParent(cube.transform);
        child.transform.localPosition = new Vector3(0f, 1f, 0f);

        EditorSceneManager.SaveScene(scene, $"{Root}/Playground.unity");
    }

    static void MutateScene()
    {
        var scene = EditorSceneManager.OpenScene($"{Root}/Playground.unity", OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects())
        {
            switch (go.name)
            {
                case "Sun":
                    var light = go.GetComponent<Light>();
                    light.intensity = 0.4f;
                    light.color = new Color(1f, 0.8f, 0.6f, 1f);
                    break;
                case "Crate":
                    go.transform.position = new Vector3(3f, 0.5f, -1f);
                    var marble = go.transform.Find("Marble");
                    if (marble != null) UnityEngine.Object.DestroyImmediate(marble.gameObject);
                    break;
            }
        }
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Floor";
        EditorSceneManager.SaveScene(scene);
    }

    // ---- plumbing ----

    static T Load<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new Exception($"missing asset: {path}");
        return asset;
    }

    static void Step(string label, Action action)
    {
        try
        {
            action();
            Debug.Log($"[FixtureGenerator] ok: {label}");
        }
        catch (Exception e)
        {
            Errors.Add($"{label}: {e.Message}");
            Debug.LogError($"[FixtureGenerator] FAILED: {label}: {e}");
        }
    }

    static void Finish(string mode)
    {
        if (Errors.Count > 0)
        {
            Debug.LogError($"[FixtureGenerator] {mode} finished with {Errors.Count} failure(s):\n  " + string.Join("\n  ", Errors));
            EditorApplication.Exit(1);
        }
        Debug.Log($"[FixtureGenerator] {mode} finished cleanly");
        EditorApplication.Exit(0);
    }
}
