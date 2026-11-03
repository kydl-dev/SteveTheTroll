using System.IO;
using System.Reflection;
using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Unity;

/// <summary>
///     Small images embedded in the DLL as resources (csproj EmbeddedResource with a LogicalName) and turned into sprites
///     in game - for art that needs no
///     Unity rebuild (T-200: the Troll Hub's build menu icon). Bigger art stays in the AssetBundle (BundleAssets).
/// </summary>
internal static class EmbeddedImages
{
    // ImageConversion.LoadImage(Texture2D, byte[], bool) by cached reflection: the game's Unity 6 also has a ReadOnlySpan overload that net48 cannot
    // name, so a plain call stops the compiler with CS0518 (Claude\MISTAKES.md standing lesson 11)
    private static MethodInfo _loadImage;

    private static bool Load(Texture2D tex, byte[] data, bool markNonReadable)
    {
        if (_loadImage == null)
        {
            _loadImage = typeof(ImageConversion).GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
        }

        if (_loadImage == null)
        {
            ModLog.Warn(
                "EmbeddedImages: ImageConversion.LoadImage(Texture2D, byte[], bool) not found in this game version");
            return false;
        }

        bool loaded = (bool)_loadImage.Invoke(null, new object[] { tex, data, markNonReadable });
        return loaded;
    }

    /// The sprite of the PNG resource `logicalName`, or null (logged) when it is missing or not a PNG. Expected outcome: a sprite of the image's size.
    internal static Sprite Sprite(string logicalName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using (Stream s = asm.GetManifestResourceStream(logicalName))
        {
            if (s == null)
            {
                ModLog.Warn("EmbeddedImages: no resource '" + logicalName + "' in the DLL");
                return null;
            }

            byte[] bytes = new byte[s.Length];
            int read = s.Read(bytes, 0, bytes.Length);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = logicalName };
            if (read != bytes.Length || !Load(tex, bytes, false))
            {
                ModLog.Warn("EmbeddedImages: '" + logicalName + "' could not be read as an image (" + read + " of " +
                            bytes.Length + " bytes)");
                return null;
            }

            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = Path.GetFileNameWithoutExtension(logicalName);
            ModLog.Verbose("EmbeddedImages: '" + logicalName + "' -> sprite " + tex.width + " x " + tex.height);
            ModLog.Check("EmbeddedImages: " + logicalName + " width", tex.width, 128, 0, "px");
            return sprite;
        }
    }
}
