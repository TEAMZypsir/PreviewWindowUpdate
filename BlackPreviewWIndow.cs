using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // ✅ Added to support file filtering

[BepInPlugin("com.pulledp0rk.pp-previewwindow", "Pp-PreviewWindow", "1.0.0")]
public class PpPreviewWindow : BaseUnityPlugin
{
    private Texture2D customBackground;
    private string imagePath;
    private Sprite cachedSprite;
    private Dictionary<GameObject, Sprite> panelOriginalSprites = new Dictionary<GameObject, Sprite>();
    private HashSet<GameObject> activePanels = new HashSet<GameObject>();
    private Dictionary<GameObject, Texture> originalMainTextures = new Dictionary<GameObject, Texture>();

    private void Start()
    {
        Logger.LogInfo("[Pp-PreviewWindow] Mod Loaded!");

        imagePath = FindBackgroundImage(); // ✅ Locate a valid image file dynamically

        if (imagePath == null)
        {
            Logger.LogError("[Pp-PreviewWindow] No valid background image found.");
            return;
        }

        customBackground = LoadTexture(imagePath);

        if (customBackground == null)
        {
            Logger.LogError("[Pp-PreviewWindow] Failed to load texture.");
            return;
        }

        // ✅ Ensure Unity properly tracks the texture and sprite
        customBackground.name = "background"; // ✅ Always name the texture "background"
        cachedSprite = TextureToSprite(customBackground);
        if (cachedSprite != null)
        {
            cachedSprite.name = "background"; // ✅ Always name the sprite "background"
        }

        StartCoroutine(PreloadPreviewPanels()); // ✅ Preload all preview panels at game start
    }

    // ✅ Finds a valid image file in the background folder
    private string FindBackgroundImage()
    {
        string pluginDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "background");
        if (!Directory.Exists(pluginDir)) return null;

        string[] validExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.gif" };
        string[] files = validExtensions
            .SelectMany(ext => Directory.GetFiles(pluginDir, ext))
            .ToArray();

        return files.Length > 0 ? files[0] : null; // ✅ Return the first valid image found
    }

    private IEnumerator PreloadPreviewPanels()
    {
        while (GameObject.Find("Preloader UI") == null || 
               GameObject.Find("ItemInfoWindowTemplate(Clone)/Inner/Contents/Preview Panel") == null)
        {
            Logger.LogWarning("[Pp-PreviewWindow] Waiting for UI elements to load...");
            yield return new WaitForSeconds(1f);
        }

        Logger.LogInfo("[Pp-PreviewWindow] UI elements loaded. Preloading all preview panels...");

        Image[] images = GameObject.FindObjectsOfType<Image>();
        foreach (var img in images)
        {
            if (img.name == "Preview Panel")
            {
                GameObject panel = img.gameObject;

                if (!panelOriginalSprites.ContainsKey(panel) && img.sprite != null)
                {
                    panelOriginalSprites[panel] = img.sprite;
                }

                if (!originalMainTextures.ContainsKey(panel) && img.material != null && img.material.mainTexture != null)
                {
                    originalMainTextures[panel] = img.material.mainTexture;
                }

                if (cachedSprite != null)
                {
                    UIHelper.UpdatePreviewWindow(img, cachedSprite);
                    Logger.LogInfo($"[Pp-PreviewWindow] Preloaded background for panel: {panel.name}");
                }
            }
        }

        Logger.LogInfo("[Pp-PreviewWindow] Preloading complete.");
        StartCoroutine(MonitorPreviewPanel()); 
    }

    private IEnumerator MonitorPreviewPanel()
    {
        while (true)
        {
            Image[] images = GameObject.FindObjectsOfType<Image>();
            foreach (var img in images)
            {
                if (img.name == "Preview Panel" && img.gameObject.activeInHierarchy)
                {
                    GameObject panel = img.gameObject;

                    if (!activePanels.Contains(panel))
                    {
                        activePanels.Add(panel);

                        if (!panelOriginalSprites.ContainsKey(panel) && img.sprite != null)
                        {
                            panelOriginalSprites[panel] = img.sprite;
                        }

                        if (!originalMainTextures.ContainsKey(panel) && img.material != null && img.material.mainTexture != null)
                        {
                            originalMainTextures[panel] = img.material.mainTexture;
                        }

                        if (cachedSprite != null)
                        {
                            UIHelper.UpdatePreviewWindow(img, cachedSprite);
                            Logger.LogInfo($"[Pp-PreviewWindow] Applied background to new preview panel: {panel.name}");
                        }
                    }
                }
            }

            activePanels.RemoveWhere(panel =>
            {
                if (panel == null || !panel.activeInHierarchy)
                {
                    if (panel != null && panelOriginalSprites.ContainsKey(panel))
                    {
                        Image img = panel.GetComponent<Image>();
                        if (img != null)
                        {
                            UIHelper.UpdatePreviewWindow(img, panelOriginalSprites[panel]);

                            if (originalMainTextures.ContainsKey(panel))
                            {
                                img.material.mainTexture = originalMainTextures[panel];
                                Logger.LogInfo($"[Pp-PreviewWindow] Restored original mainTexture for closed panel: {panel.name}");
                            }
                        }
                    }
                    panelOriginalSprites.Remove(panel);
                    originalMainTextures.Remove(panel);
                    return true;
                }
                return false;
            });

            yield return new WaitForSeconds(0.1f);
        }
    }

    public static class UIHelper
    {
        public static void UpdatePreviewWindow(Image backgroundImage, Sprite newSprite)
        {
            if (backgroundImage == null || newSprite == null)
            {
                Debug.LogError("[Pp-PreviewWindow] Error: Null reference in UpdatePreviewWindow.");
                return;
            }

            if (backgroundImage.material != null && backgroundImage.material.mainTexture != null &&
                backgroundImage.material.mainTexture.name == "info_window_back")
            {
                backgroundImage.material.mainTexture = newSprite.texture;
            }

            backgroundImage.sprite = newSprite;
            backgroundImage.overrideSprite = newSprite;
            backgroundImage.canvasRenderer.SetTexture(newSprite.texture);
            backgroundImage.SetAllDirty();

            Debug.Log($"[Pp-PreviewWindow] Successfully updated preview window background to {newSprite.name}");
        }
    }

    private Texture2D LoadTexture(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool isLoaded = texture.LoadImage(fileData);

        if (!isLoaded)
        {
            Logger.LogError("[Pp-PreviewWindow] Failed to load texture from file: " + filePath);
            return null;
        }

        return texture;
    }

    private Sprite TextureToSprite(Texture2D texture)
    {
        if (texture == null)
        {
            Logger.LogError("[Pp-PreviewWindow] Texture is null, cannot convert to sprite.");
            return null;
        }

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
