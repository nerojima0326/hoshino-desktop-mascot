using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
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
            AddIfMissing<MascotMotion>(model);
            AddIfMissing<MascotMobile>(model);
            AddIfMissing<MascotOverlay>(model);

            Undo.RecordObject(model.transform, "Place Mascot");
            model.transform.position = Vector3.zero;

            Camera camera = SetupCamera();
            FitCameraToModel(camera, model);
            EnsureDirectionalLight();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = model;

            EditorUtility.DisplayDialog(Title,
                "마스코트 컴포넌트를 붙이고 카메라를 설정했어요.\n\n" +
                "남은 일 (Windows):\n" +
                "1. UniWindowController 프리팹을 씬에 추가\n" +
                "2. Is Transparent, Is Topmost, Should Fit Monitor 체크\n" +
                "3. Windows로 빌드해서 실행 (에디터에서는 배경이 투명해지지 않아요)\n\n" +
                "모바일(Android/iOS)은 이대로 빌드하면 됩니다. 배경은 Mascot Mobile에서 바꿔요.\n" +
                "Android 홈 화면에 띄우려면 4. Apply Android Settings 도 눌러 주세요.",
                "확인");
        }

        [MenuItem("Tools/Hoshino Mascot/4. Apply Android Settings")]
        static void ApplyAndroidSettings()
        {
            EditorUtility.DisplayDialog(Title,
                "Android 설정을 바꿨어요.\n\n" + ApplyAndroidPlayerSettings() +
                "\n빌드할 때 \"다른 앱 위에 표시\" 권한과 홈 화면 서비스가 저절로 들어갑니다.",
                "확인");
        }

        /// <summary>홈 화면 띄우기에 필요한 Player Settings를 맞추고, 무엇을 바꿨는지 돌려줍니다.</summary>
        static string ApplyAndroidPlayerSettings()
        {
            // 홈 화면에 띄웠을 때 캐릭터가 없는 곳이 투명해야 홈 화면이 보입니다
            PlayerSettings.preserveFramebufferAlpha = true;
            string changes = "- Preserve Framebuffer Alpha: 켬 (홈 화면에 띄울 때 배경을 투명하게)\n";
            if (UseActivityEntryPoint()) changes += "- Application Entry Point: Activity (홈 화면 띄우기가 쓰는 방식)\n";
#if UNITY_6000_0_OR_NEWER
            // 켜 두면 홈 화면에 띄울 때마다 Unity 로고가 홈 화면 위에 나옵니다 (Unity 6부터는 누구나 끌 수 있습니다)
            if (PlayerSettings.SplashScreen.show)
            {
                PlayerSettings.SplashScreen.show = false;
                changes += "- 시작 화면(Unity 로고): 끔 (Windows 빌드도 같이 꺼집니다)\n";
            }
#endif
            return changes;
        }

        const string ApkPathKey = "HoshinoMascot.ApkPath";

        /// <summary>
        /// Android 설정 맞추기, 빌드 대상 바꾸기, APK 빌드를 한 번에 합니다.
        /// 휴대폰이 USB로 연결되어 있으면 빌드한 뒤 바로 설치할 수도 있습니다.
        /// </summary>
        [MenuItem("Tools/Hoshino Mascot/5. Build Android APK")]
        static void BuildAndroidApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(Title,
                    "Android Build Support가 설치되어 있지 않아요.\n\n" +
                    "Unity Hub > Installs > 이 Unity 버전의 ⚙ > Add modules > Android Build Support\n" +
                    "(그 아래 OpenJDK, Android SDK & NDK Tools도 체크)를 설치하고 Unity를 다시 켜 주세요.",
                    "확인");
                return;
            }

            string[] scenes = ScenesToBuild();
            if (scenes == null) return;

            int choice = EditorUtility.DisplayDialogComplex(Title,
                "Android용 APK를 만들어요.\n\n" +
                "휴대폰을 USB로 연결하고 USB 디버깅을 켜 두었으면, 만든 뒤 바로 설치할 수 있어요.\n" +
                "처음 빌드는 10분 넘게 걸릴 수 있어요.",
                "APK만 만들기", "취소", "만들고 휴대폰에 설치");
            if (choice == 1) return;
            bool install = choice == 2;

            string lastPath = EditorPrefs.GetString(ApkPathKey, "");
            string path = EditorUtility.SaveFilePanel("APK 저장 위치",
                string.IsNullOrEmpty(lastPath) ? "" : Path.GetDirectoryName(lastPath),
                string.IsNullOrEmpty(lastPath) ? "Hoshino.apk" : Path.GetFileName(lastPath),
                "apk");
            if (string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(ApkPathKey, path);

            ApplyAndroidPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = false; // 스토어용 .aab가 아니라 바로 설치할 수 있는 .apk로
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = install ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(Title,
                    $"빌드가 실패했어요 ({report.summary.result}).\n\n" +
                    "Console 창의 빨간 오류를 확인해 주세요." +
                    (install ? "\n휴대폰 설치를 골랐다면 USB 연결과 USB 디버깅이 켜져 있는지도 확인해 주세요." : ""),
                    "확인");
                return;
            }

            double megabytes = File.Exists(path) ? new FileInfo(path).Length / (1024.0 * 1024.0) : 0.0;
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog(Title,
                $"APK를 만들었어요 ({megabytes:0.0} MB).\n{path}\n\n" +
                (install
                    ? "휴대폰에 설치하고 실행했어요.\n"
                    : "이 파일을 휴대폰에 옮겨서 열면 설치돼요. (처음에는 '출처를 알 수 없는 앱 설치'를 허용해야 해요)\n") +
                "\n홈 화면에 띄우려면 앱에서 호시노를 길게 눌러 \"홈 화면에 띄우기\"를 고르세요.",
                "확인");
        }

        /// <summary>빌드할 씬 목록. 지금 열린 씬에 호시노가 있으면 그 씬으로 시작합니다. 못 만들면 null.</summary>
        static string[] ScenesToBuild()
        {
            // 빌드는 저장된 씬 파일로 하므로, 고친 내용이 있으면 저장할지 먼저 묻습니다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;

            string first = null;
            if (HasMascotInOpenScene())
            {
                first = SceneManager.GetActiveScene().path;
                if (string.IsNullOrEmpty(first))
                {
                    EditorUtility.DisplayDialog(Title, "호시노가 있는 씬을 먼저 저장해 주세요. (File > Save)", "확인");
                    return null;
                }
            }

            List<string> scenes = BuildScenes(first);
            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog(Title, "호시노를 넣은 씬을 열고 다시 눌러 주세요.", "확인");
                return null;
            }
            return scenes.ToArray();
        }

        /// <summary>
        /// Build Settings에 켜 둔 씬 목록에서 호시노가 있는 씬을 맨 앞에 둡니다.
        /// first가 없으면 씬 파일에서 MascotController를 쓰는 씬을 찾습니다 (목록에 없어도 넣습니다).
        /// </summary>
        static List<string> BuildScenes(string first)
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path)) scenes.Add(scene.path);
            }

            if (string.IsNullOrEmpty(first))
            {
                List<string> mascotScenes = FindMascotScenes();
                first = scenes.Find(mascotScenes.Contains) ?? (mascotScenes.Count > 0 ? mascotScenes[0] : null);
            }
            if (!string.IsNullOrEmpty(first))
            {
                scenes.Remove(first);
                scenes.Insert(0, first);
            }
            return scenes;
        }

        /// <summary>씬 파일(텍스트)에 MascotController 스크립트가 들어 있는 씬을 찾습니다.</summary>
        static List<string> FindMascotScenes()
        {
            var found = new List<string>();
            string scriptGuid = null;
            foreach (string guid in AssetDatabase.FindAssets("MascotController t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(MascotController))
                {
                    scriptGuid = guid;
                    break;
                }
            }
            if (scriptGuid == null) return found;

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                try
                {
                    if (File.ReadAllText(path).Contains("guid: " + scriptGuid)) found.Add(path);
                }
                catch (IOException)
                {
                    // 읽지 못한 씬은 건너뜁니다
                }
            }
            return found;
        }

        /// <summary>
        /// 화면 없이(-batchmode) Android APK를 만듭니다. GitHub Actions(GameCI unity-builder) 같은 클라우드 빌드에서 부릅니다.
        /// 예) Unity -batchmode -quit -projectPath . -buildTarget Android
        ///       -executeMethod HoshinoMascot.EditorTools.MascotSetup.BuildAndroidFromCommandLine -customBuildPath Build/Hoshino.apk
        /// 서명 키는 -androidKeystoreName, -androidKeystorePass, -androidKeyaliasName, -androidKeyaliasPass로 받습니다 (없으면 디버그 키).
        /// 끝나면 성공 0, 실패 1로 Unity를 종료합니다.
        /// </summary>
        public static void BuildAndroidFromCommandLine()
        {
            Dictionary<string, string> args = CommandLineArguments();
            string path = args.TryGetValue("customBuildPath", out string customPath) && !string.IsNullOrEmpty(customPath)
                ? customPath
                : "Build/Android/Hoshino.apk";
            if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "Hoshino.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            List<string> scenes = BuildScenes(null);
            if (scenes.Count == 0)
            {
                Debug.LogError("[Hoshino Mascot] 빌드할 씬이 없습니다. 호시노를 넣은 씬(.unity)이 저장소에 올라가 있는지 확인해 주세요.");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[Hoshino Mascot] 빌드할 씬: " + string.Join(", ", scenes));

            Debug.Log("[Hoshino Mascot] Android 설정\n" + ApplyAndroidPlayerSettings());
            ApplyKeystore(args);
            EditorUserBuildSettings.buildAppBundle = false; // 바로 설치할 수 있는 .apk
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[Hoshino Mascot] 빌드 결과: {report.summary.result} ({path})");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>-이름 값 형태의 명령줄 인자. 값이 없으면 빈 문자열입니다.</summary>
        static Dictionary<string, string> CommandLineArguments()
        {
            var result = new Dictionary<string, string>();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-")) continue;
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("-");
                result[args[i].TrimStart('-')] = hasValue ? args[i + 1] : "";
            }
            return result;
        }

        // 늘 같은 키로 서명해야 휴대폰에서 앱을 지우지 않고 새 APK로 업데이트할 수 있습니다
        static void ApplyKeystore(Dictionary<string, string> args)
        {
            if (!args.TryGetValue("androidKeystoreName", out string keystore) || string.IsNullOrEmpty(keystore)) return;
            if (!File.Exists(keystore))
            {
                Debug.LogWarning("[Hoshino Mascot] 서명 키 파일이 없어서 디버그 키로 서명합니다: " + keystore);
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            if (args.TryGetValue("androidKeystorePass", out string keystorePass)) PlayerSettings.Android.keystorePass = keystorePass;
            if (args.TryGetValue("androidKeyaliasName", out string aliasName)) PlayerSettings.Android.keyaliasName = aliasName;
            if (args.TryGetValue("androidKeyaliasPass", out string aliasPass)) PlayerSettings.Android.keyaliasPass = aliasPass;
        }

        static bool HasMascotInOpenScene()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<MascotController>() != null;
#else
            return Object.FindObjectOfType<MascotController>() != null;
#endif
        }

        /// <summary>
        /// Unity 2023.1 이상은 Android 실행 방식을 Activity와 GameActivity 중에서 고릅니다.
        /// 홈 화면 띄우기는 Activity 방식의 Unity 플레이어를 쓰므로 Activity로 맞춥니다.
        /// (그 전 버전에는 이 설정이 없어서 리플렉션으로 찾습니다)
        /// </summary>
        static bool UseActivityEntryPoint()
        {
            PropertyInfo property = typeof(PlayerSettings.Android).GetProperty("applicationEntry", BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.PropertyType.IsEnum) return false;
            property.SetValue(null, Enum.Parse(property.PropertyType, "Activity"));
            return true;
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
