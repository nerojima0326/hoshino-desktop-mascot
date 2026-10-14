using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace HoshinoMascot.EditorTools
{
    /// <summary>
    /// "3. Set Up Selected Model Materials" 메뉴를 쓴 모델에만 동작합니다.
    /// FBX에 적힌 텍스처 파일 이름으로 프로젝트 안의 텍스처를 찾아 연결하고, Cutout 머티리얼로 만듭니다.
    /// (Unity가 텍스처를 자동으로 못 찾는 경우가 있어서 직접 연결합니다)
    /// </summary>
    public class MascotMaterialPostprocessor : AssetPostprocessor
    {
        public const string Marker = "HoshinoMascot.MmdMaterials";

        // Unity 기본 머티리얼 처리 뒤에 실행되도록 순서를 늦춥니다
        public override int GetPostprocessOrder() => 100;

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!(assetImporter is ModelImporter importer) || importer.userData == null || !importer.userData.Contains(Marker)) return;

            Texture texture = null;
            if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription textureProperty))
            {
                texture = textureProperty.texture;
                if (texture == null) texture = FindTexture(textureProperty.relativePath, textureProperty.path);
            }

            Color color = Color.white;
            if (texture == null && description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
                color = new Color(diffuse.x, diffuse.y, diffuse.z);
            // Blender FBX는 투명도를 TransparencyFactor(1 - 알파)로 적습니다. MMD 표정용 재질은 알파 0입니다
            if (description.TryGetProperty("TransparencyFactor", out float transparency))
                color.a = Mathf.Clamp01(1f - transparency);
            else if (description.TryGetProperty("Opacity", out float opacity))
                color.a = Mathf.Clamp01(opacity);

            MascotSetup.MakeCutout(material);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", color);
        }

        Texture FindTexture(params string[] paths)
        {
            string modelFolder = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                string fallback = null;
                foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
                {
                    string candidate = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(candidate) != name) continue;
                    context.DependsOnSourceAsset(candidate);
                    // 모델과 같은 폴더(또는 그 아래)에 있는 것을 먼저 씁니다
                    if (candidate.StartsWith(modelFolder + "/")) return AssetDatabase.LoadAssetAtPath<Texture2D>(candidate);
                    fallback ??= candidate;
                }
                if (fallback != null) return AssetDatabase.LoadAssetAtPath<Texture2D>(fallback);
            }
            return null;
        }
    }
}
