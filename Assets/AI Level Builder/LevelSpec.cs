using System;
using System.Collections.Generic;

/// <summary>
/// The JSON schema Codex writes to describe a level. Kept as plain serializable data —
/// no Unity-specific types — so it's easy for an external tool to generate correctly.
///
/// Coordinates are in TILE units, not world units (x=5 means 5 tiles from origin,
/// scaled by tileSize when placed). Y increases upward.
/// </summary>
[Serializable]
public class LevelSpec
{
    public string levelName;      // used for the LevelDataSO asset name, e.g. "Level_03"
    public string sceneName;      // used for the .unity scene name, usually matches levelName
    public int gridWidth;
    public int gridHeight;
    public float tileSize = 1f;

    public int startingMoves = 4;
    public int parMoveCount;
    public List<string> unlockedActions = new List<string>(); // e.g. "JumpAction", "DashAction"

    public List<TileSpec> tiles = new List<TileSpec>();
    public List<PrefabSpec> prefabs = new List<PrefabSpec>();
}

[Serializable]
public class TileSpec
{
    public int x;
    public int y;
    public string type; // must match a key in LevelBuilder.TilePaths, e.g. "Ground", "Hazard"
}

[Serializable]
public class PrefabSpec
{
    // Must match a key in LevelBuilder.PrefabPaths:
    // "PlayerPrefab", "DeathTrigger", "NextLevel", "MovableCrate"
    public string type;
    public float x;
    public float y;

    // Only used for trigger volumes (DeathTrigger / NextLevel) whose collider should be
    // resized to cover an area rather than a single tile. Defaults to a 1x1 tile.
    public float width = 1f;
    public float height = 1f;
}