using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    [Header("Tray")] public Transform trayRoot;
    public int trayCapacity = 7;

    [Header("Buttons")] public UnityEngine.UI.Button btnUndo, btnShuffle, btnAddTrayTile;

    private readonly List<Tile> tray = new(); // thứ tự trong khay (cuối danh sách = tile mới nhất)
    private List<Tile> allTiles; // tất cả tile còn tồn tại (board + tray)
    [SerializeField] GameObject hiddenTileContainer;

    // Lưu thông tin để undo đúng vị trí cũ
    private struct UndoStep
    {
        public Tile tile;
        public Vector3 prevPos; // vị trí trên board trước khi nhặt
    }

    private readonly Stack<UndoStep> undoStack = new();

    public IEnumerable<Tile> AllCurrentTiles => allTiles?.Where(t => t != null) ?? System.Linq.Enumerable.Empty<Tile>();

    void Start()
    {
        if (btnUndo) btnUndo.onClick.AddListener(Undo);
        if (btnShuffle) btnShuffle.onClick.AddListener(Shuffle);
        if (btnAddTrayTile) btnAddTrayTile.onClick.AddListener(AddTrayTile);

        FindObjectOfType<BoardGenerator>().BuildLevel("level1");
    }

    public void BindTiles(List<Tile> tiles) => allTiles = tiles;

    public void OnTileClicked(Tile t)
    {
        if (tray.Count >= trayCapacity) return;

        // Ghi lại vị trí cũ để hoàn tác LIFO
        undoStack.Push(new UndoStep { tile = t, prevPos = t.transform.position });

        // Đưa vào khay (luôn add vào cuối = tile mới nhất)
        tray.Add(t);
        Vector3 slot = GetTrayPos(tray.Count - 1);
        t.MoveToTray(slot, () =>
        {
            // Chỉ khi tile tới nơi mới check triple
            CheckTripleAndPruneUndo();
        });
    }

    Vector3 GetTrayPos(int idx)
    {
        if (trayRoot.childCount > idx)
            return trayRoot.GetChild(idx).position;
        // fallback: dàn ngang
        return trayRoot.position + new Vector3(idx * 1.0f, 0, 0);
    }

    // Chỉ match khi tile đã xuống khay; đồng thời dọn các undo-step vô hiệu (tile đã bị phá)
    void CheckTripleAndPruneUndo()
    {
        var groups = tray.GroupBy(x => x.id).Where(g => g.Count() >= 3).ToList();
        if (groups.Count > 0)
        {
            foreach (var g in groups)
            {
                int need = 3;
                for (int i = tray.Count - 1; i >= 0 && need > 0; i--) // xoá từ cuối cho an toàn
                {
                    if (tray[i].id == g.Key)
                    {
                        var tile = tray[i];
                        tray.RemoveAt(i);
                        allTiles.Remove(tile);

                        // Khi tile bị phá -> mọi UndoStep tham chiếu tới nó đều vô nghĩa
                        RemoveUndoStepsOf(tile);

                        tile.DestroyAnim(null);
                        need--;
                    }
                }
            }

            // Sắp lại vị trí các tile còn trong khay
            for (int i = 0; i < tray.Count; i++)
                tray[i].MoveToTray(GetTrayPos(i));
        }

        // Ngoài các tile vừa bị phá, cũng dọn các UndoStep đã "lỗi thời":
        // ví dụ tile không còn nằm ở cuối khay thì không thể undo ngay
        PruneDeadSteps();
    }

    // Xoá toàn bộ step liên quan tới tile đã huỷ
    void RemoveUndoStepsOf(Tile tile)
    {
        if (undoStack.Count == 0) return;
        var tmp = new Stack<UndoStep>(undoStack.Count);
        while (undoStack.Count > 0)
        {
            var s = undoStack.Pop();
            if (s.tile != tile) tmp.Push(s);
        }

        // đổ lại theo đúng thứ tự
        while (tmp.Count > 0) undoStack.Push(tmp.Pop());
    }

    // Loại bỏ các step có tile null (đã huỷ) để stack sạch sẽ
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

    // ✅ Chỉ cho phép Undo khi tile mới nhất đang ở cuối khay
    void Undo()
    {
        if (tray.Count == 0 || undoStack.Count == 0) return;

        var topTrayTile = tray[tray.Count - 1]; // tile mới nhất đang ở khay
        var topStep = undoStack.Peek(); // step mới nhất đã ghi

        // Chỉ undo nếu tile ở đỉnh stack CHÍNH LÀ tile cuối cùng trên khay
        if (topStep.tile != topTrayTile || topTrayTile == null)
            return; // không làm gì (đúng yêu cầu LIFO)

        // Hợp lệ -> thực hiện Undo
        undoStack.Pop();
        tray.RemoveAt(tray.Count - 1);

        // Trả về đúng vị trí cũ
        topTrayTile.MoveToBoard(topStep.prevPos);

        // Không cần re-layout khay vì ta luôn pop phần tử cuối -> không tạo "lỗ"
    }

    private bool isShuffling = false;

    void Shuffle()
    {
        if (isShuffling) return; // đang shuffle thì không cho bấm tiếp
        isShuffling = true;

        var boardTiles = allTiles.Where(t => t != null && !t.isInTray).ToList();
        if (boardTiles.Count <= 1)
        {
            isShuffling = false;
            return;
        }

        // Nhóm theo layerIndex
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
                // ✅ thêm callback OnComplete để đếm số tween hoàn thành
                layerTiles[i].MoveToBoard(poses[i], () =>
                {
                    tweenCount--;
                    if (tweenCount <= 0) isShuffling = false; // khi tất cả xong mới mở lại
                });
            }
        }

        // Nếu không có tween nào chạy thì mở lại luôn
        if (tweenCount == 0) isShuffling = false;
    }

    void AddTrayTile()
    {
        trayCapacity = 8;
        hiddenTileContainer.SetActive(true);
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