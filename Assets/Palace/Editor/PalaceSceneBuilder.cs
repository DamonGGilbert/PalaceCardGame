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
    /// Creates the Card and Seat prefabs plus the MainMenu and Game scenes, with every
    /// reference already connected, and puts both scenes in Build Settings (MainMenu first).
    /// Re-running it overwrites those assets.
    ///
    /// Menu: Palace > Setup > 3 - Build Prefabs and Scenes
    /// </summary>
    public static class PalaceSceneBuilder
    {
        public const string PrefabFolder = "Assets/Palace/Prefabs";
        public const string SceneFolder = "Assets/Palace/Scenes";
        public const string CardPrefabPath = PrefabFolder + "/Card.prefab";
        public const string SeatPrefabPath = PrefabFolder + "/Seat.prefab";
        public const string ScenePath = SceneFolder + "/" + GameLaunch.GameScene + ".unity";
        public const string MenuScenePath = SceneFolder + "/" + GameLaunch.MainMenuScene + ".unity";

        [MenuItem("Palace/Setup/3 - Build Prefabs and Scenes", priority = 3)]
        public static void BuildPrefabsAndScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!PalaceProjectSetup.HasTextMeshProEssentials())
                Debug.LogWarning("[Palace] TextMeshPro Essential Resources are missing, so text may not show. Run Palace > Setup > 1 - Apply Project Settings first.");

            // Opening a scene unloads unused assets, so assets are (re)loaded after every NewScene.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var themes = LoadThemes();
            PalaceArtGenerator.EnsureFolder(PrefabFolder);
            PalaceArtGenerator.EnsureFolder(SceneFolder);
            BuildCardPrefab(themes.card);
            BuildSeatPrefab(themes.table, themes.card, AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath));

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildGameScene(scene);

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMenuScene(scene);

            SetBuildScenes();
            Debug.Log("[Palace] Built prefabs and the MainMenu and Game scenes. Open MainMenu and press Play.");
        }

        private static (CardTheme card, TableTheme table) LoadThemes()
        {
            var card = AssetDatabase.LoadAssetAtPath<CardTheme>(PalaceArtGenerator.CardThemePath);
            var table = AssetDatabase.LoadAssetAtPath<TableTheme>(PalaceArtGenerator.TableThemePath);
            if (card == null || table == null)
            {
                PalaceArtGenerator.GenerateAll();
                card = AssetDatabase.LoadAssetAtPath<CardTheme>(PalaceArtGenerator.CardThemePath);
                table = AssetDatabase.LoadAssetAtPath<TableTheme>(PalaceArtGenerator.TableThemePath);
            }
            return (card, table);
        }

        // ------------------------------------------------------------------ prefabs

        private static void BuildCardPrefab(CardTheme theme)
        {
            var go = NewUIObject("Card");
            var view = go.AddComponent<CardView>();
            view.Theme = theme;
            view.ShowBack();
            PrefabUtility.SaveAsPrefabAsset(go, CardPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void BuildSeatPrefab(TableTheme tableTheme, CardTheme cardTheme, CardView cardPrefab)
        {
            var go = NewUIObject("Seat");
            var seat = go.AddComponent<SeatView>();
            seat.Configure(tableTheme, cardTheme, cardPrefab);
            seat.EnsureBuilt();
            seat.SetPlayer("Player", 0);
            PrefabUtility.SaveAsPrefabAsset(go, SeatPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ scenes

        private static void BuildGameScene(Scene scene)
        {
            var themes = LoadThemes();
            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath);
            var seatPrefab = AssetDatabase.LoadAssetAtPath<SeatView>(SeatPrefabPath);

            Canvas canvas = CreateCameraCanvasAndEvents(themes.table);

            var tableRt = UIBuild.Rect("Table", canvas.transform, TableView.ReferenceSize, Vector2.zero);
            var table = tableRt.gameObject.AddComponent<TableView>();
            table.Configure(themes.table, themes.card, seatPrefab, cardPrefab);
            table.EnsureBuilt();

            var hud = UIBuild.Stretch("HUD (Safe Area)", canvas.transform);
            hud.gameObject.AddComponent<SafeAreaFitter>();

            var gameGo = new GameObject("Game");
            gameGo.AddComponent<AppBootstrap>();
            gameGo.AddComponent<GameController>().Configure(table, themes.table, themes.card, hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildMenuScene(Scene scene)
        {
            var themes = LoadThemes();
            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath);

            Canvas canvas = CreateCameraCanvasAndEvents(themes.table);
            var menuRoot = UIBuild.Rect("Menu", canvas.transform, TableView.ReferenceSize, Vector2.zero);

            var menuGo = new GameObject("MainMenu");
            menuGo.AddComponent<AppBootstrap>();
            menuGo.AddComponent<MainMenuController>().Configure(themes.table, themes.card, cardPrefab, menuRoot);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        /// <summary>Camera, a 1920x1080 "Expand" canvas with the room background, and an EventSystem.</summary>
        private static Canvas CreateCameraCanvasAndEvents(TableTheme tableTheme)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.03f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            var canvasGo = NewUIObject("Canvas");
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

            UIBuild.StretchImage("Background", canvasGo.transform, tableTheme.background, Color.white);

            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#if ENABLE_INPUT_SYSTEM
            if (inputSystemModule != null) esGo.AddComponent(inputSystemModule);
            else esGo.AddComponent<StandaloneInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
            return canvas;
        }

        private static GameObject NewUIObject(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UIBuild.UILayer;
            return go;
        }

        private static void SetBuildScenes()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => s.path != MenuScenePath && s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
