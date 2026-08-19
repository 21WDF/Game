using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋子注册表（ScriptableObject）—— 持有所有 PieceData，提供按 id 查找。
/// 通过 CreateAssetMenu 创建：Create > Chess > Piece Registry，然后在 Inspector 中加入所有 PieceData。
/// </summary>
[CreateAssetMenu(fileName = "PieceRegistry", menuName = "Chess/Piece Registry", order = 1)]
public class PieceRegistry : ScriptableObject
{
    public List<PieceData> pieces = new();

    private Dictionary<int, PieceData> _lookup;

    private void BuildLookup()
    {
        _lookup = new Dictionary<int, PieceData>();
        if (pieces == null) return;
        foreach (var p in pieces)
        {
            if (p == null) continue;
            _lookup[p.id] = p;
        }
    }

    /// <summary>按 id 查找棋子定义；找不到返回 null</summary>
    public PieceData GetByID(int id)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(id, out var data);
        return data;
    }
}
