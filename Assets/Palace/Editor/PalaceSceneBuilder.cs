using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Palace.EditorTools
{
    /// <summary>
    /// Creates the Card and Seat prefabs and the Game scene with every reference already
    /// connected. Re-running it overwrites those three assets.
    ///
    /// Menu: Palace > Setup > 3 - Build Prefabs and Game Scene
    /// </summary>
    public static class PalaceSceneBuilder
    {
        public const string PrefabFolder = "Assets/Palace/Prefabs";
        public const string SceneFolder = "Assets/Palace/Scenes";
        public const string CardPrefabPath = PrefabFolder + "/Card.prefab";
        public const string SeatPrefabPath = PrefabFolder + "/Seat.prefab";
        public const string ScenePath = SceneFolder + "/Game.unity";

        [MenuItem("Palace/Setup/3 - Build Prefabs and Game Scene", priority = 3)]
        public static void BuildPrefabsAndScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Open the new scene FIRST: opening a scene unloads unused assets, which would
            // invalidate any theme or prefab references loaded before it.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!PalaceProjectSetup.HasTextMeshProEssentials())
                Debug.LogWarning("[Palace] TextMeshPro Essential Resources are missing, so text may not show. Run Palace > Setup > 1 - Apply Project Settings first.");

            var cardTheme = AssetDatabase.LoadAssetAtPath<CardTheme>(PalaceArtGenerator.CardThemePath);
            var tableTheme = AssetDatabase.LoadAssetAtPath<TableTheme>(PalaceArtGenerator.TableThemePath);
            if (cardTheme == null || tableTheme == null)
            {
                PalaceArtGenerator.GenerateAll();
                cardTheme = AssetDatabase.LoadAssetAtPath<CardTheme>(PalaceArtGenerator.CardThemePath);
                tableTheme = AssetDatabase.LoadAssetAtPath<TableTheme>(PalaceArtGenerator.TableThemePath);
            }

            PalaceArtGenerator.EnsureFolder(PrefabFolder);
            PalaceArtGenerator.EnsureFolder(SceneFolder);

            CardView cardPrefab = BuildCardPrefab(cardTheme);
            SeatView seatPrefab = BuildSeatPrefab(tableTheme, cardTheme, cardPrefab);
            BuildScene(scene, tableTheme, cardTheme, cardPrefab, seatPrefab);

            Debug.Log("[Palace] Built " + CardPrefabPath + ", " + SeatPrefabPath + " and " + ScenePath + ". Press Play to preview the table.");
        }

        private static CardView BuildCardPrefab(CardTheme theme)
        {
            var go = NewUIObject("Card");
            var view = go.AddComponent<CardView>();
            view.Theme = theme;
            view.ShowBack();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CardPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab.GetComponent<CardView>();
        }

        private static SeatView BuildSeatPrefab(TableTheme tableTheme, CardTheme cardTheme, CardView cardPrefab)
        {
            var go = NewUIObject("Seat");
            var seat = go.AddComponent<SeatView>();
            seat.Configure(tableTheme, cardTheme, cardPrefab);
            seat.EnsureBuilt();
            seat.SetPlayer("Player", 0);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, SeatPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab.GetComponent<SeatView>();
        }

        private static void BuildScene(Scene scene, TableTheme tableTheme, CardTheme cardTheme, CardView cardPrefab, SeatView seatPrefab)
        {
            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.03f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            // Canvas: 1920x1080 reference, "Expand" so the whole table always fits any screen shape.
            var canvasGo = NewUIObject("GameCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var background = UIBuild.StretchImage("Background", canvasGo.transform, tableTheme.background, Color.white);
            background.raycastTarget = false;

            // Table
            var tableRt = UIBuild.Rect("Table", canvasGo.transform, TableView.ReferenceSize, Vector2.zero);
            var table = tableRt.gameObject.AddComponent<TableView>();
            table.Configure(tableTheme, cardTheme, seatPrefab, cardPrefab);
            table.EnsureBuilt();

            // HUD inside the safe area (buttons, menus, banners go here)
            var hud = UIBuild.Stretch("HUD (Safe Area)", canvasGo.transform);
            hud.gameObject.AddComponent<SafeAreaFitter>();

            // Event system with the right input module for this project's input settings
            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#if ENABLE_INPUT_SYSTEM
            if (inputSystemModule != null) esGo.AddComponent(inputSystemModule);
            else esGo.AddComponent<StandaloneInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            // App settings + Stage 1 preview harness
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<AppBootstrap>();
            var preview = gameGo.AddComponent<TableLayoutPreview>();
            preview.Configure(table, hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        private static GameObject NewUIObject(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UIBuild.UILayer;
            return go;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
