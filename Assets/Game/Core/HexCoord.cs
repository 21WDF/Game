using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 六边形立方坐标（Cube Coordinate）
/// 不变式：q + r + s = 0
/// 本游戏使用平顶 Hex（Flat-top），平边朝上下
/// </summary>
[System.Serializable]
public struct HexCoord
{
    public int q; // 列
    public int r; // 行
    public int s; // = -q -r

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
        this.s = -q - r;
    }

    // ---- 6 方向邻居（平顶 Hex）----
    // 方向顺序：NE, E, SE, SW, W, NW
    internal static readonly HexCoord[] Directions = {
        new HexCoord( 1,  0),   // NE: 右上
        new HexCoord( 1, -1),   // E:  右
        new HexCoord( 0, -1),   // SE: 右下
        new HexCoord(-1,  0),   // SW: 左下
        new HexCoord(-1,  1),   // W:  左
        new HexCoord( 0,  1),   // NW: 左上
    };

    // ---- 距离（曼哈顿距离的 Hex 等价）----
    public int Distance(HexCoord other)
    {
        return Mathf.Max(
            Mathf.Abs(q - other.q),
            Mathf.Abs(r - other.r),
            Mathf.Abs(s - other.s)
        );
    }

    // ---- 获取某个方向的邻居 ----
    public HexCoord Neighbor(int directionIndex)
    {
        HexCoord dir = Directions[directionIndex];
        return new HexCoord(q + dir.q, r + dir.r);
    }

    // ---- 半径 R 内的所有 Hex 坐标 ----
    public static List<HexCoord> AllCoordsInRadius(int radius)
    {
        var result = new List<HexCoord>();
        for (int qv = -radius; qv <= radius; qv++)
        {
            int rMin = Mathf.Max(-radius, -qv - radius);
            int rMax = Mathf.Min(radius, -qv + radius);
            for (int rv = rMin; rv <= rMax; rv++)
            {
                result.Add(new HexCoord(qv, rv));
            }
        }
        return result;
    }

    // ---- Cube → 世界坐标（平顶 Hex）----
    public Vector3 ToWorld(float hexSize)
    {
        float x = hexSize * (3f / 2f * q);
        float z = hexSize * (Mathf.Sqrt(3f) / 2f * q + Mathf.Sqrt(3f) * r);
        return new Vector3(x, 0f, z);
    }

    // ---- 相等性 ----
    public override bool Equals(object obj) =>
        obj is HexCoord other && q == other.q && r == other.r && s == other.s;

    public override int GetHashCode() => (q, r, s).GetHashCode();

    public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
    public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

    public override string ToString() => $"({q}, {r}, {s})";
}
