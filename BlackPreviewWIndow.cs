using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

[BepInPlugin("com.pulledp0rk.blackpreviewwindow", "BlackPreviewWindow", "1.0.0")]
public class BlackPreviewWindow : BaseUnityPlugin
{
    private Texture2D customBackground;
    private string imagePath;
    private Sprite cachedSprite;
    private Dictionary<GameObject, Sprite> panelOriginalSprites = new Dictionary<GameObject, Sprite>();
    private HashSet<GameObject> activePanels = new HashSet<GameObject>();
    private Dictionary<GameObject, Texture> originalMainTextures = new Dictionary<GameObject, Texture>();

    private void Start()
    {
        Logger.LogInfo("[BlackPreviewWindow] Mod Loaded!");

        imagePath = Path.Combine(Paths.PluginPath, "BlackPreviewWindow", "background.png");

        if (!File.Exists(imagePath))
        {
            Logger.LogError("[BlackPreviewWindow] Background image not found: " + imagePath);
            return;
        }

        customBackground = LoadTexture(imagePath);

        if (customBackground == null)
        {
            Logger.LogError("[BlackPreviewWindow] Failed to load texture.");
            return;
        }

        // ✅ Ensure Unity properly tracks the texture and sprite
        customBackground.name = "background.png"; 
        cachedSprite = TextureToSprite(customBackground);
        if (cachedSprite != null)
        {
            cachedSprite.name = "background.png"; 
        }

        StartCoroutine(PreloadPreviewPanels()); // ✅ Preload all preview panels at game start
    }

    private IEnumerator PreloadPreviewPanels()
    {
        // ✅ Wait for UI to fully load
        while (GameObject.Find("Preloader UI") == null || 
               GameObject.Find("ItemInfoWindowTemplate(Clone)/Inner/Contents/Preview Panel") == null)
        {
            Logger.LogWarning("[BlackPreviewWindow] Waiting for UI elements to load...");
            yield return new WaitForSeconds(1f);
        }

        Logger.LogInfo("[BlackPreviewWindow] UI elements loaded. Preloading all preview panels...");

        Image[] images = GameObject.FindObjectsOfType<Image>();
        foreach (var img in images)
        {
            if (img.name == "Preview Panel")
            {
                GameObject panel = img.gameObject;

                // Store original background
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
                    Logger.LogInfo($"[BlackPreviewWindow] Preloaded background for panel: {panel.name}");
                }
            }
        }

        Logger.LogInfo("[BlackPreviewWindow] Preloading complete.");
        StartCoroutine(MonitorPreviewPanel()); // ✅ Continue monitoring for new panels
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
                            Logger.LogInfo($"[BlackPreviewWindow] Applied background to new preview panel: {panel.name}");
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
                                Logger.LogInfo($"[BlackPreviewWindow] Restored original mainTexture for closed panel: {panel.name}");
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
                Debug.LogError("[BlackPreviewWindow] Error: Null reference in UpdatePreviewWindow.");
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

            Debug.Log($"[BlackPreviewWindow] Successfully updated preview window background to {newSprite.name}");
        }
    }

    private Texture2D LoadTexture(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool isLoaded = texture.LoadImage(fileData);

        if (!isLoaded)
        {
            Logger.LogError("[BlackPreviewWindow] Failed to load texture from file: " + filePath);
            return null;
        }

        return texture;
    }

    private Sprite TextureToSprite(Texture2D texture)
    {
        if (texture == null)
        {
            Logger.LogError("[BlackPreviewWindow] Texture is null, cannot convert to sprite.");
            return null;
        }

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
