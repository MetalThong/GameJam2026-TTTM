using TMPro;
using UnityEditor;
using UnityEngine;

namespace TTTM.EditorTools
{
    public static class TMPFontAssetGenerator
    {
        private const string SourceFontPath = "Assets/_Project/Resources/Text/SVN-Determination Sans.otf";
        private const string OutputFontAssetPath = "Assets/_Project/Resources/Text/SVN-Determination Sans SDF.asset";
        private const string FallbackFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

        [InitializeOnLoadMethod]
        private static void GenerateMissingFontsOnEditorLoad()
        {
            EditorApplication.delayCall += GenerateDeterminationSansIfMissing;
        }

        [MenuItem("Tools/TTTM/Generate TMP Font Assets/SVN Determination Sans")]
        public static void GenerateDeterminationSans()
        {
            GenerateDeterminationSans(overwriteExisting: true);
        }

        private static void GenerateDeterminationSansIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath) != null)
            {
                return;
            }

            GenerateDeterminationSans(overwriteExisting: false);
        }

        private static void GenerateDeterminationSans(bool overwriteExisting)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                throw new MissingReferenceException($"Cannot find source font at {SourceFontPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath) != null)
            {
                if (!overwriteExisting)
                {
                    return;
                }

                if (!AssetDatabase.DeleteAsset(OutputFontAssetPath))
                {
                    throw new MissingReferenceException($"Cannot replace TMP font asset at {OutputFontAssetPath}");
                }
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            fontAsset.name = "SVN-Determination Sans SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            AssignFallbackFont(fontAsset);

            Texture2D atlasTexture = fontAsset.atlasTexture;
            if (atlasTexture != null)
            {
                atlasTexture.name = "SVN-Determination Sans SDF Atlas";
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = "SVN-Determination Sans SDF Material";
            }

            AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);

            if (atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Generated TMP font asset: {OutputFontAssetPath}");
        }

        private static void AssignFallbackFont(TMP_FontAsset fontAsset)
        {
            TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontAssetPath);
            if (fallbackFont == null)
            {
                Debug.LogWarning($"Generated {fontAsset.name} without fallback font. Missing asset at {FallbackFontAssetPath}");
                return;
            }

            fontAsset.fallbackFontAssetTable.Clear();
            fontAsset.fallbackFontAssetTable.Add(fallbackFont);
        }
    }
}
