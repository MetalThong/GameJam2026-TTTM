using TMPro;
using UnityEngine;

public static class TMPVietnameseFontBootstrap
{
    private const string PrimaryFontResourcePath = "Text/SVN-Determination Sans SDF";
    private const string FallbackFontResourcePath = "Fonts & Materials/LiberationSans SDF - Fallback";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmupBeforeSceneLoad()
    {
        Warmup();
    }

    public static void Warmup()
    {
        TMP_FontAsset primaryFont = Resources.Load<TMP_FontAsset>(PrimaryFontResourcePath);
        if (primaryFont == null)
        {
            Debug.LogWarning($"TMPVietnameseFontBootstrap: missing TMP font at Resources/{PrimaryFontResourcePath}.");
            return;
        }

        TMP_Settings.defaultFontAsset = primaryFont;
        TMP_FontAsset fallbackFont = Resources.Load<TMP_FontAsset>(FallbackFontResourcePath);
        if (fallbackFont != null)
        {
            AddFallback(primaryFont, fallbackFont);
            AddGlobalFallback(fallbackFont);
        }

        if (!primaryFont.TryAddCharacters(VietnameseTextUtility.FontWarmupCharacters, out string missingCharacters))
        {
            Debug.LogWarning($"TMPVietnameseFontBootstrap: SVN font is missing Vietnamese glyphs: {missingCharacters}");
        }
    }

    private static void AddFallback(TMP_FontAsset primaryFont, TMP_FontAsset fallbackFont)
    {
        if (primaryFont.fallbackFontAssetTable == null)
        {
            return;
        }

        if (!primaryFont.fallbackFontAssetTable.Contains(fallbackFont))
        {
            primaryFont.fallbackFontAssetTable.Add(fallbackFont);
        }
    }

    private static void AddGlobalFallback(TMP_FontAsset fallbackFont)
    {
        if (TMP_Settings.fallbackFontAssets == null || TMP_Settings.fallbackFontAssets.Contains(fallbackFont))
        {
            return;
        }

        TMP_Settings.fallbackFontAssets.Add(fallbackFont);
    }
}
