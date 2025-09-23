using System;
using UnityEngine;
using DG.Tweening;

public class Tile : MonoBehaviour
{
    [Header("Data")]
    public int id;
    public int layerIndex;
    public Vector2Int grid;
    public bool isInTray { get; private set; }

    [Header("Refs")]
    [SerializeField] SpriteRenderer iconSR;
    public Collider2D col { get; private set; }

    private Action<Tile> onClicked;
    private BoardGenerator board;

    void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    public void Init(BoardGenerator board, int id, int layer, Vector2Int grid, Sprite icon, Action<Tile> onClicked)
    {
        this.board = board;
        this.id = id;
        this.layerIndex = layer;
        this.grid = grid;
        this.onClicked = onClicked;

        if (iconSR) iconSR.sprite = icon;

        // Order theo layerIndex: dưới cùng = 1, trên tăng dần
        if (iconSR) iconSR.sortingOrder = layerIndex + 1;

        isInTray = false;
    }

    public bool IsSelectable() => !isInTray && !board.HasCoveringTile(this);

    public void SetActiveState(bool canSelect)
    {
        if (!iconSR) return;
        iconSR.color = canSelect ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
    }

    public void MoveToTray(Vector3 trayPos, Action onArrive = null)
    {
        isInTray = true;
        transform.DOKill();
        transform.DOMove(trayPos, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            onArrive?.Invoke(); // chỉ khi tới nơi mới xử lý match
        });
        transform.DOScale(1f, 0.2f);
        if (iconSR) iconSR.sortingOrder = 9999; // trong khay luôn nổi
    }

    public void MoveToBoard(Vector3 pos, Action onArrive = null)
    {
        isInTray = false;
        transform.DOKill();
        transform.DOMove(pos, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            onArrive?.Invoke();
        });
        transform.DOScale(1f, 0.2f);
        if (iconSR) iconSR.sortingOrder = layerIndex + 1;
    }


    public void DestroyAnim(Action onComplete)
    {
        transform.DOKill();
        transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        });
    }

    private void OnMouseUpAsButton()
    {
        if (IsSelectable()) onClicked?.Invoke(this);
    }
}
