using System.Collections.Generic;
using UnityEngine;
using System.IO;

[System.Serializable]
public class CellData 
{ 
    public int x; 
    public int y; 
    public int id = -1; // -1 = random
}

[System.Serializable]
public class LayerData 
{ 
    public float offsetX = 0f;
    public float offsetY = 0f;
    public List<CellData> cells; 
}

[System.Serializable]
public class LevelData 
{ 
    public List<LayerData> layers; 
}

public class BoardGenerator : MonoBehaviour
{
    [Header("Refs")]
    public Transform boardRoot;
    public Tile tilePrefab;
    public Sprite[] tileIcons;

    [Header("Settings")]
    public Vector2 cellSize = new Vector2(0.8f, 0.9f);

    private readonly List<Tile> tiles = new();
    private GameManager gm;
    private CameraController cameraController;

    void Awake()
    {
        gm = FindObjectOfType<GameManager>();
        cameraController = FindObjectOfType<CameraController>();
    }

    void Update()
    {
        foreach (var t in tiles)
        {
            if (t == null || t.isInTray) continue;
            bool canSelect = !HasCoveringTile(t);
            t.SetActiveState(canSelect);
        }
    }

    public void BuildLevel(string levelName)
    {
        string path = Path.Combine(Application.dataPath, "Resources/Levels/" + levelName + ".json");
        if (!File.Exists(path))
        {
            Debug.LogError("Không tìm thấy file: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        LevelData data = JsonUtility.FromJson<LevelData>(json);

        int totalCells = GetTotalCells(data);
        List<int> bag = MakeIdBag(totalCells, tileIcons.Length);
        int bagIdx = 0;

        for (int layerIndex = 0; layerIndex < data.layers.Count; layerIndex++)
        {
            LayerData layerData = data.layers[layerIndex];
            foreach (var c in layerData.cells)
            {
                var t = Instantiate(tilePrefab, boardRoot);
                int id = (c.id >= 0) ? c.id : bag[bagIdx++];

                Vector3 worldPos = GridToWorld(new Vector2Int(c.x, c.y), layerData, layerIndex);
                t.transform.position = worldPos;

                var icon = tileIcons[id % tileIcons.Length];
                t.Init(this, id, layerIndex, new Vector2Int(c.x, c.y), icon, gm.OnTileClicked);
                tiles.Add(t);
            }
        }

        gm.BindTiles(tiles);
        if (cameraController != null)
            cameraController.SetupCameraOnce();
    }

    int GetTotalCells(LevelData d)
    {
        int c = 0;
        foreach (var L in d.layers) c += L.cells.Count;
        return c;
    }

    List<int> MakeIdBag(int total, int types)
    {
        total = Mathf.CeilToInt(total / 3f) * 3;

        var bag = new List<int>(total);
        for (int i = 0; i < total; i++) bag.Add(i % types);

        for (int i = bag.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (bag[i], bag[r]) = (bag[r], bag[i]);
        }
        return bag;
    }

    Vector3 GridToWorld(Vector2Int g, LayerData layerData, int layerIndex)
    {
        Vector3 p = new Vector3(g.x * cellSize.x, g.y * cellSize.y, 0f);
        p += new Vector3(layerIndex * layerData.offsetX, layerIndex * layerData.offsetY, 0f);
        return boardRoot.TransformPoint(p);
    }

    public bool HasCoveringTile(Tile t)
    {
        foreach (var other in gm.AllCurrentTiles)
        {
            if (other == null || other == t) continue;
            if (other.isInTray) continue;
            if (other.layerIndex <= t.layerIndex) continue;

            if (other.col != null && t.col != null && other.col.bounds.Intersects(t.col.bounds))
                return true;
        }
        return false;
    }
}
