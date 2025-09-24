using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    [Header("Tray")]
    public Transform trayRoot;
    public int trayCapacity = 7;

    [Header("Buttons")]
    public UnityEngine.UI.Button btnUndo, btnShuffle, btnHint;

    private readonly List<Tile> tray = new();   // tile trong khay (thứ tự)
    private List<Tile> allTiles;                // tất cả tile còn tồn tại (board + tray)

    private struct UndoStep
    {
        public Tile tile;
        public Vector3 prevPos;
    }
    private readonly Stack<UndoStep> undoStack = new();

    public IEnumerable<Tile> AllCurrentTiles => allTiles?.Where(t => t != null) ?? System.Linq.Enumerable.Empty<Tile>();

    // flags
    private bool isShuffling = false;
    private bool isHintRunning = false;

    void Start()
    {
        if (btnUndo) btnUndo.onClick.AddListener(Undo);
        if (btnShuffle) btnShuffle.onClick.AddListener(() => { if (!isShuffling && !isHintRunning) Shuffle(); });
        if (btnHint) btnHint.onClick.AddListener(() => { if (!isShuffling && !isHintRunning) Hint(); });

        FindObjectOfType<BoardGenerator>().BuildLevel("level5");
        
    }

    public void BindTiles(List<Tile> tiles) => allTiles = tiles;

    public void OnTileClicked(Tile t)
    {
        if (isShuffling || isHintRunning) return; // không cho click khi shuffle hoặc hint
        if (tray.Count >= trayCapacity) return;

        undoStack.Push(new UndoStep { tile = t, prevPos = t.transform.position });
        tray.Add(t);

        Vector3 slot = GetTrayPos(tray.Count - 1);
        t.MoveToTray(slot, () =>
        {
            CheckTripleAndPruneUndo();
        });
    }

    Vector3 GetTrayPos(int idx)
    {
        if (trayRoot.childCount > idx)
            return trayRoot.GetChild(idx).position;
        return trayRoot.position + new Vector3(idx * 1.0f, 0, 0);
    }

    void CheckTripleAndPruneUndo()
    {
        var groups = tray.GroupBy(x => x.id).Where(g => g.Count() >= 3).ToList();
        if (groups.Count > 0)
        {
            foreach (var g in groups)
            {
                int need = 3;
                for (int i = tray.Count - 1; i >= 0 && need > 0; i--)
                {
                    if (tray[i].id == g.Key)
                    {
                        var tile = tray[i];
                        tray.RemoveAt(i);
                        allTiles.Remove(tile);
                        RemoveUndoStepsOf(tile);

                        tile.DestroyAnim(null);
                        need--;
                    }
                }
            }

            // cập nhật lại vị trí tray
            for (int i = 0; i < tray.Count; i++)
                tray[i].MoveToTray(GetTrayPos(i));
        }

        PruneDeadSteps();

        // ✅ check thắng game
        if (allTiles.Count == 0)
        {
            Debug.Log("Win");
        }
    }


    void RemoveUndoStepsOf(Tile tile)
    {
        if (undoStack.Count == 0) return;
        var tmp = new Stack<UndoStep>(undoStack.Count);
        while (undoStack.Count > 0)
        {
            var s = undoStack.Pop();
            if (s.tile != tile) tmp.Push(s);
        }
        while (tmp.Count > 0) undoStack.Push(tmp.Pop());
    }

    void PruneDeadSteps()
    {
        if (undoStack.Count == 0) return;
        var tmp = new Stack<UndoStep>(undoStack.Count);
        while (undoStack.Count > 0)
        {
            var s = undoStack.Pop();
            if (s.tile != null) tmp.Push(s);
        }
        while (tmp.Count > 0) undoStack.Push(tmp.Pop());
    }

    void Undo()
    {
        if (isShuffling || isHintRunning) return;
        if (tray.Count == 0 || undoStack.Count == 0) return;

        var topTrayTile = tray[tray.Count - 1];
        var topStep = undoStack.Peek();

        if (topStep.tile != topTrayTile || topTrayTile == null)
            return; // chỉ cho undo tile mới nhất

        undoStack.Pop();
        tray.RemoveAt(tray.Count - 1);

        topTrayTile.MoveToBoard(topStep.prevPos);
    }

    void Shuffle()
    {
        if (isShuffling) return;
        isShuffling = true;

        var boardTiles = allTiles.Where(t => t != null && !t.isInTray).ToList();
        if (boardTiles.Count <= 1)
        {
            isShuffling = false;
            return;
        }

        var groups = boardTiles.GroupBy(t => t.layerIndex);
        int tweenCount = 0;

        foreach (var group in groups)
        {
            var layerTiles = group.ToList();
            if (layerTiles.Count <= 1) continue;

            var poses = layerTiles.Select(t => t.transform.position).ToList();
            poses.Shuffle();

            for (int i = 0; i < layerTiles.Count; i++)
            {
                tweenCount++;
                layerTiles[i].MoveToBoard(poses[i], () =>
                {
                    tweenCount--;
                    if (tweenCount <= 0) isShuffling = false;
                });
            }
        }

        if (tweenCount == 0) isShuffling = false;
    }

    void Hint()
    {
        if (isHintRunning) return;

        // tìm id có ít nhất 3 tile selectable
        var group = allTiles.Where(t => t != null && !t.isInTray && t.IsSelectable())
                            .GroupBy(t => t.id)
                            .FirstOrDefault(g => g.Count() >= 3);
        if (group == null) return;

        isHintRunning = true;
        StartCoroutine(HintSequence(group.Take(3).ToList()));
    }

    System.Collections.IEnumerator HintSequence(List<Tile> tiles)
    {
        foreach (var t in tiles)
        {
            tray.Add(t);
            Vector3 slot = GetTrayPos(tray.Count - 1);

            bool arrived = false;
            t.MoveToTray(slot, () => { arrived = true; });

            yield return new WaitUntil(() => arrived);
            CheckTripleAndPruneUndo();
            yield return new WaitForSeconds(0.1f);
        }

        isHintRunning = false;
    }
}

public static class ShuffleExt
{
    public static void Shuffle<T>(this IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
