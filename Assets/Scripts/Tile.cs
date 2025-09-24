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
    [SerializeField] float scaleInTray;

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

        // order theo layerIndex (layer 0 = 1, layer 1 = 2, ...)
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
            onArrive?.Invoke();
        });

        // ✅ nhỏ lại khi vào khay
        transform.DOScale(scaleInTray, 0.25f);

        if (iconSR) iconSR.sortingOrder = 9999; // luôn nổi trong khay
    }

    public void MoveToBoard(Vector3 pos, Action onArrive = null)
    {
        isInTray = false;
        transform.DOKill();
        transform.DOMove(pos, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            onArrive?.Invoke();
        });

        // ✅ to lại khi trả về board
        transform.DOScale(1f, 0.25f);

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

    private void OnMouseDown()
    {
        if (IsSelectable()) onClicked?.Invoke(this);
    }

}
