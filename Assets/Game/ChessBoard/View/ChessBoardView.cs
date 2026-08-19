using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋盘视图（纯渲染层，不包含业务逻辑）
/// 职责：根据 Model 提供的坐标实例化 HexTile、维护「坐标 → 格子」映射、
///       提供世界坐标查询（GetCellWorldPosition）与高亮清除。
/// 不持有任何 PieceModel / 业务数据，由 ChessBoardController 拥有与驱动。
/// </summary>
public class ChessBoardView
{
    private readonly GameObject _hexTilePrefab;
    private readonly float _hexSize;
    private readonly Transform _parent;

    private readonly Dictionary<HexCoord, HexTile> _tiles = new();

    public ChessBoardView(GameObject hexTilePrefab, float hexSize, Transform parent)
    {
        _hexTilePrefab = hexTilePrefab;
        _hexSize = hexSize;
        _parent = parent;
    }

    // ==========================================
    //  生成
    // ==========================================
    /// <summary>根据 Model 的坐标集合实例化所有格子</summary>
    public void GenerateView(IReadOnlyList<HexCoord> coords)
    {
        foreach (var coord in coords)
        {
            Vector3 worldPos = coord.ToWorld(_hexSize);
            GameObject tileObj = Object.Instantiate(_hexTilePrefab, worldPos, Quaternion.identity, _parent);
            var tile = tileObj.GetComponent<HexTile>();
            if (tile != null)
            {
                tile.Initialize(coord);
                _tiles[coord] = tile;
            }
            else
            {
                Debug.LogError($"[ChessBoardView] HexTile Prefab 缺少 HexTile 脚本！");
            }
        }
        Debug.Log($"[ChessBoardView] 生成 {_tiles.Count} 个格子");
    }

    // ==========================================
    //  单格新增 / 移除（棋盘道具系统）
    // ==========================================
    /// <summary>新增单个格子（已存在则返回原实例）。供 ChessBoardController.AddTile 调用。</summary>
    public HexTile CreateTile(HexCoord coord)
    {
        if (_tiles.TryGetValue(coord, out var existing)) return existing;
        Vector3 worldPos = coord.ToWorld(_hexSize);
        GameObject tileObj = Object.Instantiate(_hexTilePrefab, worldPos, Quaternion.identity, _parent);
        var tile = tileObj.GetComponent<HexTile>();
        if (tile != null)
        {
            tile.Initialize(coord);
            _tiles[coord] = tile;
        }
        else
        {
            Debug.LogError($"[ChessBoardView] HexTile Prefab 缺少 HexTile 脚本！");
        }
        return tile;
    }

    /// <summary>移除单个格子（从映射移除并 Destroy GameObject）。供 ChessBoardController.RemoveTile 调用。</summary>
    public void DestroyTile(HexCoord coord)
    {
        if (_tiles.TryGetValue(coord, out var tile))
        {
            _tiles.Remove(coord);
            if (tile != null) Object.Destroy(tile.gameObject);
        }
    }

    // ==========================================
    //  查询
    // ==========================================
    /// <summary>根据坐标获取格子视图</summary>
    public HexTile GetTile(HexCoord coord)
    {
        _tiles.TryGetValue(coord, out var tile);
        return tile;
    }

    /// <summary>坐标 → 世界坐标（供棋子生成等模块定位使用）</summary>
    public Vector3 GetCellWorldPosition(HexCoord coord)
    {
        return coord.ToWorld(_hexSize);
    }

    /// <summary>所有格子的迭代（用于批量高亮清除等）</summary>
    public IEnumerable<HexTile> AllTiles => _tiles.Values;

    // ==========================================
    //  高亮
    // ==========================================
    /// <summary>清除所有格子的战术高亮</summary>
    public void ClearAllHighlights()
    {
        foreach (var tile in _tiles.Values)
            tile.overlay?.Hide();
    }

    // ==========================================
    //  路径预览（LineRenderer —— View 层持有，InputHandler 仅调用 Controller 转发）
    //  归属调整：原由 InputHandler 创建/持有，违反 MVC（输入层不应持有 View 层 LineRenderer）。
    //            现收归 View 层，懒加载，首次显示时创建。
    // ==========================================
    private PathPreview _pathPreview;

    /// <summary>懒加载创建 PathPreview（挂在 _parent 下的独立 GameObject）</summary>
    private void InitializePathPreview()
    {
        var go = new GameObject("PathPreview");
        go.transform.SetParent(_parent, false);
        _pathPreview = go.AddComponent<PathPreview>();
    }

    /// <summary>显示路径预览线（供 ChessBoardController 转发；InputHandler 调用）</summary>
    public void ShowPathPreview(List<HexCoord> path)
    {
        if (_pathPreview == null) InitializePathPreview();
        _pathPreview.ShowPath(path);
    }

    /// <summary>隐藏路径预览线</summary>
    public void HidePathPreview()
    {
        _pathPreview?.HidePath();
    }
}
