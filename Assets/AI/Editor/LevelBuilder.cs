using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds a complete level (scene + tilemap + prefabs + LevelDataSO) from a JSON LevelSpec.
/// This is the ONLY script that needs to run inside the Unity Editor UI — everything upstream
/// of it (deciding the layout, writing the JSON) can be done by Codex from a terminal.
///
/// Command line usage (what Codex should run after writing a level JSON file):
///   Unity -batchmode -quit -projectPath "<path to project>" \
///         -executeMethod LevelBuilder.BuildFromCommandLine -levelJson "Assets/LevelSpecs/level_03.json"
///
/// Manual usage inside the Editor: Tools > Level Builder > Build From JSON...
/// </summary>
public static class LevelBuilder
{
    // ---- Point these at your actual project assets before handing this to Codex ----

    private static readonly Dictionary<string, string> TilePaths = new Dictionary<string, string>
    {
        // 3 x 3 grid starting at top left, going right and down. Each tile is 1x1 unit in world space.
        { "Top Left Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_0.asset"},
        { "Top Middle Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_1.asset" },
        { "Top Right Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_2.asset" },
        { "Middle Left Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_8.asset" },
        { "Middle Right Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_9.asset" },
        { "Bottom Left Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_15.asset" },
        { "Bottom Middle Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_16.asset" },
        { "Bottom Right Tile", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_17.asset" },
        // Hazard tile
        { "Spikes", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_4.asset" },
        // Door, 2 x 2 tiles, always need to be placed together
        { "Door Top Left", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_6.asset" },
        { "Door Top Right", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_7.asset" },
        { "Door Bottom Left", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_13.asset" },
        { "Door Bottom Right", "Assets/2D Sprites/Original/TileMap/OneLastJumpTileMap_14.asset" },

    };

    private static readonly Dictionary<string, string> PrefabPaths = new Dictionary<string, string>
    {
        { "PlayerPrefab", "Assets/Prefabs/Player.prefab" },
        { "DeathTrigger", "Assets/Prefabs/DeathTrigger.prefab" },
        { "NextLevel", "Assets/Prefabs/Finish.prefab" },
        { "MovableCrate", "Assets/Prefabs/Crate.prefab" },
    };

    private const string SceneOutputFolder = "Assets/Scenes/";
    private const string LevelDataOutputFolder = "Assets/Scripts/Levels/Level Data/";

    [MenuItem("Tools/Level Builder/Build From JSON...")]
    private static void BuildFromMenu()
    {
        string path = EditorUtility.OpenFilePanel("Select Level Spec JSON", "Assets", "json");
        if (string.IsNullOrEmpty(path)) return;

        if (path.StartsWith(Application.dataPath))
            path = "Assets" + path.Substring(Application.dataPath.Length);

        BuildLevelFromJson(path);
    }

    /// <summary>Entry point for Unity's -executeMethod when run from the command line.</summary>
    public static void BuildFromCommandLine()
    {
        string jsonPath = GetCommandLineArg("-levelJson");
        if (string.IsNullOrEmpty(jsonPath))
        {
            Debug.LogError("LevelBuilder: -levelJson argument not provided.");
            return;
        }

        BuildLevelFromJson(jsonPath);
    }

    public static void BuildLevelFromJson(string jsonAssetPath)
    {
        if (!File.Exists(jsonAssetPath))
        {
            Debug.LogError($"LevelBuilder: file not found at {jsonAssetPath}");
            return;
        }

        LevelSpec spec = JsonUtility.FromJson<LevelSpec>(File.ReadAllText(jsonAssetPath));
        if (spec == null)
        {
            Debug.LogError($"LevelBuilder: failed to parse {jsonAssetPath}");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildTilemap(spec);
        PlacePrefabs(spec);

        Directory.CreateDirectory(SceneOutputFolder);
        string scenePath = $"{SceneOutputFolder}{spec.sceneName}.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        AddSceneToBuildSettings(scenePath);
        CreateOrUpdateLevelData(spec);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"LevelBuilder: built '{spec.sceneName}' — {spec.tiles.Count} tiles, {spec.prefabs.Count} prefabs.");
    }

    [System.Obsolete]
    private static void BuildTilemap(LevelSpec spec)
    {
        var gridGO = new GameObject("Grid");
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(spec.tileSize, spec.tileSize, 0f);

        var tilemapGO = new GameObject("Ground");
        tilemapGO.transform.SetParent(gridGO.transform);
        var tilemap = tilemapGO.AddComponent<Tilemap>();
        tilemapGO.AddComponent<TilemapRenderer>();

        var tilemapCollider = tilemapGO.AddComponent<TilemapCollider2D>();
        tilemapGO.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        tilemapGO.AddComponent<CompositeCollider2D>();
        tilemapCollider.usedByComposite = true;

        var loadedTiles = new Dictionary<string, TileBase>();

        foreach (var t in spec.tiles)
        {
            if (!loadedTiles.TryGetValue(t.type, out TileBase tile))
            {
                if (!TilePaths.TryGetValue(t.type, out string tilePath))
                {
                    Debug.LogWarning($"LevelBuilder: unknown tile type '{t.type}' at ({t.x},{t.y}), skipping.");
                    continue;
                }

                tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
                loadedTiles[t.type] = tile;
            }

            if (tile == null) continue;
            tilemap.SetTile(new Vector3Int(t.x, t.y, 0), tile);
        }
    }

    private static void PlacePrefabs(LevelSpec spec)
    {
        foreach (var p in spec.prefabs)
        {
            if (!PrefabPaths.TryGetValue(p.type, out string prefabPath))
            {
                Debug.LogWarning($"LevelBuilder: unknown prefab type '{p.type}', skipping.");
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"LevelBuilder: prefab not found at {prefabPath}, skipping.");
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(p.x * spec.tileSize, p.y * spec.tileSize, 0f);

            // Trigger volumes (DeathTrigger / NextLevel) get resized to cover an area if specified
            var col = instance.GetComponent<BoxCollider2D>();
            if (col != null && (p.width != 1f || p.height != 1f))
            {
                col.size = new Vector2(p.width, p.height);
            }
        }
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void CreateOrUpdateLevelData(LevelSpec spec)
    {
        Directory.CreateDirectory(LevelDataOutputFolder);
        string assetPath = $"{LevelDataOutputFolder}{spec.levelName}.asset";

        LevelDataSO data = AssetDatabase.LoadAssetAtPath<LevelDataSO>(assetPath);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<LevelDataSO>();

        data.levelName = spec.levelName;
        data.sceneName = spec.sceneName;
        data.parMoveCount = spec.parMoveCount;
        data.startingMoves = spec.startingMoves;
        data.unlockedActionNames = new List<string>(spec.unlockedActions);

        if (isNew) AssetDatabase.CreateAsset(data, assetPath);
        else EditorUtility.SetDirty(data);
    }

    private static string GetCommandLineArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }
}