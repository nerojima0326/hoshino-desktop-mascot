using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HoshinoMascot.EditorTools
{
    /// <summary>
    /// 메뉴: Tools > Hoshino Mascot
    /// 투명 창에 필요한 Player Settings와 씬 구성을 한 번에 맞춥니다.
    /// </summary>
    public static class MascotSetup
    {
        const string Title = "Hoshino Mascot";

        [MenuItem("Tools/Hoshino Mascot/1. Apply Player Settings")]
        static void ApplyPlayerSettings()
        {
            // 투명 창은 Direct3D11 + DXGI flip model 끔 조합에서만 동작합니다 (Direct3D12는 안 됨)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.useFlipModelSwapchain = false;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            EditorUtility.DisplayDialog(Title,
                "Player Settings를 바꿨어요.\n\n" +
                "- 그래픽 API: Direct3D11\n" +
                "- Use DXGI flip model swapchain: 끔\n" +
                "- 창 모드, 크기 조절 가능, 백그라운드에서 계속 실행",
                "확인");
        }

        [MenuItem("Tools/Hoshino Mascot/2. Make Selected Model a Mascot")]
        static void MakeSelectedModelMascot()
        {
            GameObject model = Selection.activeGameObject;
            if (model == null || !model.scene.IsValid())
            {
                EditorUtility.DisplayDialog(Title, "Hierarchy에 올려 둔 모델의 가장 위 오브젝트를 선택한 뒤 다시 실행해 주세요.", "확인");
                return;
            }

            Undo.SetCurrentGroupName("Make Hoshino Mascot");
            int undoGroup = Undo.GetCurrentGroup();

            AddIfMissing<MascotSpeech>(model);
            AddIfMissing<MascotController>(model);
            AddIfMissing<MascotMenu>(model);

            Undo.RecordObject(model.transform, "Place Mascot");
            model.transform.position = Vector3.zero;

            Camera camera = SetupCamera();
            FitCameraToModel(camera, model);
            EnsureDirectionalLight();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = model;

            EditorUtility.DisplayDialog(Title,
                "마스코트 컴포넌트를 붙이고 카메라를 설정했어요.\n\n" +
                "남은 일:\n" +
                "1. UniWindowController 프리팹을 씬에 추가\n" +
                "2. Is Transparent, Is Topmost, Should Fit Monitor 체크\n" +
                "3. Windows로 빌드해서 실행 (에디터에서는 배경이 투명해지지 않아요)",
                "확인");
        }

        static void AddIfMissing<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null) Undo.AddComponent<T>(target);
        }

        [MenuItem("Tools/Hoshino Mascot/3. Set Up Selected Model Materials")]
        static void SetUpModelMaterials()
        {
            GameObject selected = Selection.activeGameObject;
            Object source = selected != null ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(selected) : null;
            string modelPath = AssetDatabase.GetAssetPath(source != null ? source : selected);
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer))
            {
                EditorUtility.DisplayDialog(Title, "FBX 모델(Project 창의 모델 파일이나 씬에 놓은 모델)을 선택한 뒤 다시 실행해 주세요.", "확인");
                return;
            }

            // 모델 안에 들어 있는 텍스처와 머티리얼을 꺼내서 고칠 수 있는 파일로 만듭니다
            string parent = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string folderName = Path.GetFileNameWithoutExtension(modelPath) + "_Materials";
            string folder = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, folderName);

            // 표시를 남겨 두면 다시 불러올 때 MascotMaterialPostprocessor가 텍스처를 연결합니다
            if (string.IsNullOrEmpty(importer.userData)) importer.userData = MascotMaterialPostprocessor.Marker;
            else if (!importer.userData.Contains(MascotMaterialPostprocessor.Marker)) importer.userData += ";" + MascotMaterialPostprocessor.Marker;
            importer.SaveAndReimport();

            importer.ExtractTextures(folder);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (!(asset is Material material)) continue;
                string materialPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + material.name + ".mat");
                AssetDatabase.ExtractAsset(material, materialPath);
            }
            AssetDatabase.WriteImportSettingsIfDirty(modelPath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);

            int count = 0;
            bool lilToon = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                lilToon |= ApplyToonShader(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
                count++;
            }
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(Title,
                $"머티리얼 {count}개를 설정했어요. ({folder})\n\n" +
                (lilToon
                    ? "- lilToon, Cutout (속눈썹 같은 투명한 부분 처리)\n- 머리카락·옷·피부: 윤곽선과 그림자\n"
                    : "- Standard 셰이더, Cutout (속눈썹 같은 투명한 부분 처리)\n- 반짝임과 반사 끔\n\n" +
                      "lilToon을 설치하고 이 메뉴를 다시 누르면 애니메이션 느낌의 툰 셰이더로 바뀝니다."),
                "확인");
        }

        // MMD에서 흔히 쓰는 재질 이름. 눈·속눈썹·입 같은 얼굴 부품에는 윤곽선을 넣지 않습니다
        static readonly Regex OutlineMaterials = new Regex("髪|アホ毛|肌|頭|服|シャツ|スカート|ネクタイ|ベルト|靴|手袋|袖|hair|skin|body|cloth|shirt|skirt|shoe|glove", RegexOptions.IgnoreCase);
        static readonly Regex ShadowMaterials = new Regex("髪|アホ毛|肌|服|シャツ|スカート|ネクタイ|ベルト|靴|手袋|袖|hair|skin|body|cloth|shirt|skirt|shoe|glove", RegexOptions.IgnoreCase);

        /// <summary>lilToon이 있으면 lilToon Cutout으로, 없으면 Standard Cutout으로 만듭니다. lilToon을 썼으면 true.</summary>
        internal static bool ApplyToonShader(Material material)
        {
            if (material == null) return false;
            Shader lilToon = Shader.Find("lilToon");
            // lilToon의 설정 함수는 공개되어 있지 않아서 리플렉션으로 부릅니다 (lilToon이 없어도 이 스크립트가 컴파일되게)
            Type utils = Type.GetType("lilToon.lilMaterialUtils, lilToon.Editor");
            Type renderingMode = Type.GetType("lilToon.RenderingMode, lilToon.Editor");
            Type transparentMode = Type.GetType("lilToon.TransparentMode, lilToon.Editor");
            MethodInfo setup = utils?.GetMethod("SetupMaterialWithRenderingMode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (lilToon == null || setup == null || renderingMode == null || transparentMode == null)
            {
                MakeCutout(material);
                return false;
            }

            Undo.RecordObject(material, "Set Up Material");
            Texture mainTexture = material.mainTexture;
            Color color = material.color;
            bool outline = OutlineMaterials.IsMatch(material.name);
            material.shader = lilToon;
            material.shaderKeywords = Array.Empty<string>();
            setup.Invoke(null, new object[]
            {
                material, Enum.Parse(renderingMode, "Cutout"), Enum.Parse(transparentMode, "Normal"),
                outline, false, false, false,
            });
            material.renderQueue = -1;
            material.SetTexture("_MainTex", mainTexture);
            material.SetColor("_Color", color);
            material.SetFloat("_Cutoff", 0.5f);
            if (ShadowMaterials.IsMatch(material.name)) material.SetFloat("_UseShadow", 1f);
            EditorUtility.SetDirty(material);
            return true;
        }

        /// <summary>
        /// MMD 모델은 텍스처 알파로 속눈썹·눈 모양을 잘라내고, 표정용 재질(눈물 등)은 알파 0으로 숨깁니다.
        /// 반투명(Fade)으로 두면 그리는 순서가 꼬이므로 Cutout으로 맞춥니다.
        /// </summary>
        internal static void MakeCutout(Material material)
        {
            if (material == null) return;
            if (AssetDatabase.Contains(material)) Undo.RecordObject(material, "Set Up Material");
            material.shader = Shader.Find("Standard");
            material.SetFloat("_Mode", 1f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Glossiness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_GlossyReflections", 0f);
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            if (AssetDatabase.Contains(material)) EditorUtility.SetDirty(material);
        }

        /// <summary>캐릭터 키가 화면 높이의 1/4 정도가 되게 카메라 크기를 맞춥니다.</summary>
        static void FitCameraToModel(Camera camera, GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            camera.orthographicSize = Mathf.Max(0.5f, bounds.size.y * 2f);
        }

        static Camera SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Camera");
                camera = cameraObject.AddComponent<Camera>();
            }

            Undo.RecordObject(camera, "Setup Camera");
            Undo.RecordObject(camera.transform, "Setup Camera");
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            // 직교 카메라라서 화면 어디에 있어도 캐릭터 크기가 같습니다
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            // 알파 0인 배경이 투명 창의 "뚫린 부분"이 됩니다
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            return camera;
        }

        static void EnsureDirectionalLight()
        {
#if UNITY_2023_1_OR_NEWER
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            Light[] lights = Object.FindObjectsOfType<Light>();
#endif
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional) return;
            }

            var lightObject = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Light");
            lightObject.transform.rotation = Quaternion.Euler(30f, 20f, 0f);
            Light created = lightObject.AddComponent<Light>();
            created.type = LightType.Directional;
        }
    }
}
