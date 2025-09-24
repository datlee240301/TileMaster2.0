using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Settings")]
    public float padding = 1f;   // khoảng dư ra so với tile
    public float zoomSpeed = 5f; // tốc độ zoom mượt (chỉ khi setup)

    private Camera cam;
    private bool initialized = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic)
            cam.orthographic = true;
    }

    // gọi từ GameManager hoặc BoardGenerator khi build xong level
    public void SetupCameraOnce()
    {
        Tile[] tiles = FindObjectsOfType<Tile>();
        if (tiles.Length == 0) return;

        // tính bounds bao hết tile
        Bounds bounds = new Bounds(tiles[0].transform.position, Vector3.zero);
        foreach (var t in tiles)
        {
            if (t == null) continue;
            bounds.Encapsulate(t.transform.position);
        }

        // đặt camera ở center
        Vector3 targetPos = bounds.center;
        targetPos.z = transform.position.z; // giữ nguyên Z
        transform.position = targetPos;

        // tính size hợp lý
        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = bounds.size.x / bounds.size.y;

        float newSize;
        if (targetRatio > screenRatio)
        {
            newSize = bounds.size.x / (2f * screenRatio);
        }
        else
        {
            newSize = bounds.size.y / 2f;
        }

        newSize += padding;

        // set orthographic size
        cam.orthographicSize = newSize;

        initialized = true;
    }
}