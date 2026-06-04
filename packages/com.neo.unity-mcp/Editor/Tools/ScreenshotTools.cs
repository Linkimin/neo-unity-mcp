// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using Neo.UnityMcp.Registry;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("Screenshot")]
    internal static class ScreenshotTools
    {
        private const string ImagePrefix = "data:image/png;base64,";

        [NeoTool("capture_game_view", "Capture the Game View (main camera). Returns a base64 PNG data URI.")]
        [ReadOnlyTool]
        public static object CaptureGameView(
            [ToolParam("Width in pixels (default 512).", Required = false)] int width = 0,
            [ToolParam("Height in pixels (default 512).", Required = false)] int height = 0)
        {
            width = Mathf.Clamp(width > 0 ? width : 512, 64, 4096);
            height = Mathf.Clamp(height > 0 ? height : 512, 64, 4096);

            var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
                return Response.Error("NO_CAMERA", new { hint = "Add a Camera to the scene to capture the Game View." });

            return CaptureWithUI(camera, width, height);
        }

        [NeoTool("capture_scene_view", "Capture the Scene View camera. Returns a base64 PNG data URI.")]
        [ReadOnlyTool]
        public static object CaptureSceneView(
            [ToolParam("Width in pixels (default = view size).", Required = false)] int width = 0,
            [ToolParam("Height in pixels (default = view size).", Required = false)] int height = 0)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return Response.Error("NO_SCENE_VIEW", new { hint = "Open a Scene View window first." });

            var camera = sceneView.camera;
            if (camera == null)
                return Response.Error("NO_SCENE_CAMERA");

            if (width <= 0 || height <= 0)
            {
                width = Mathf.RoundToInt(camera.pixelWidth);
                height = Mathf.RoundToInt(camera.pixelHeight);
            }
            width = Mathf.Clamp(width, 64, 4096);
            height = Mathf.Clamp(height, 64, 4096);

            return CaptureFromCamera(camera, width, height);
        }

        private static object CaptureWithUI(Camera camera, int width, int height)
        {
            RenderTexture renderTexture = null;
            RenderTexture previousTarget = null;
            RenderTexture previousActive = null;
            Texture2D screenshot = null;
            var overlayCanvases = new List<Canvas>();

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.Create();

                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.activeInHierarchy)
                    {
                        overlayCanvases.Add(canvas);
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + 0.1f;
                    }
                }

                previousTarget = camera.targetTexture;
                previousActive = RenderTexture.active;

                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();

                var base64 = Convert.ToBase64String(screenshot.EncodeToPNG());
                return Response.Success("Captured Game View.", new { image = ImagePrefix + base64, width, height });
            }
            catch (Exception ex)
            {
                return Response.Error("CAPTURE_FAILED", new { error = ex.Message });
            }
            finally
            {
                foreach (var canvas in overlayCanvases)
                {
                    if (canvas != null)
                    {
                        canvas.worldCamera = null;
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    }
                }
                if (camera != null) camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (renderTexture != null) { renderTexture.Release(); UnityEngine.Object.DestroyImmediate(renderTexture); }
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        private static object CaptureFromCamera(Camera camera, int width, int height)
        {
            RenderTexture renderTexture = null;
            RenderTexture previousTarget = null;
            RenderTexture previousActive = null;
            Texture2D screenshot = null;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.Create();

                previousTarget = camera.targetTexture;
                previousActive = RenderTexture.active;

                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();

                var base64 = Convert.ToBase64String(screenshot.EncodeToPNG());
                return Response.Success("Captured Scene View.", new { image = ImagePrefix + base64, width, height });
            }
            catch (Exception ex)
            {
                return Response.Error("CAPTURE_FAILED", new { error = ex.Message });
            }
            finally
            {
                if (camera != null) camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (renderTexture != null) { renderTexture.Release(); UnityEngine.Object.DestroyImmediate(renderTexture); }
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }
    }
}
