using UnityEngine;

/// <summary>
/// 游戏初始化 — 在棋盘生成后将初始棋子放到棋盘上
/// 挂载在场景中的 GameSetup GameObject 上。
///
/// Phase 2b：不再直接 Instantiate Unit Prefab，而是通过 PieceManager.SpawnPieceById 生成。
///           Inspector 中只需指定双方初始 PieceData（由 PieceRegistry 统一管理）与起始坐标。
///           硬约束：棋子生成必须经由 PieceManager，确保 Model/View/Layout/Turn 同步注册。
/// </summary>
public class GameSetup : MonoBehaviour
{
    [Header("初始棋子定义（PieceData SO）")]
    public PieceData p1PieceData;       // P1 初始棋子定义
    public PieceData p2PieceData;       // P2 初始棋子定义

    [Header("起始坐标（q, r）")]
    [Tooltip("在 Inspector 中配置；int.MinValue = 未配置")]
    public Vector2Int p1StartCoord = new Vector2Int(int.MinValue, int.MinValue);
    public Vector2Int p2StartCoord = new Vector2Int(int.MinValue, int.MinValue);

    private void Start()
    {
        // 战前流程由 GameFlowController 接管（选棋子→部署→对战）。
        // 棋子在部署阶段由 PieceManager.SpawnPieceById 生成，不再自动生成。
        // 下方 SpawnPieces 保留供参考但不再被调用；Inspector 字段（p1PieceData 等）已废弃。
    }

    private void SpawnPieces()
    {
        if (PieceManager.Instance == null)
        {
            Debug.LogError("[GameSetup] 场景中缺少 PieceManager！无法生成棋子。");
            return;
        }

        // 配置校验：起始坐标以 int.MinValue 作为"未配置"哨兵（0,0 是合法格子，不可用 0 判定）。
        if (p1StartCoord.x == int.MinValue || p2StartCoord.x == int.MinValue)
        {
            Debug.LogError($"[GameSetup] 起始坐标未注入：p1={p1StartCoord}, p2={p2StartCoord}。请在 Inspector 中配置。", this);
        }

        // 放置 P1
        if (p1PieceData != null)
        {
            var coord = new HexCoord(p1StartCoord.x, p1StartCoord.y);
            PieceManager.Instance.SpawnPieceById(p1PieceData.id, coord, PlayerSide.P1);
            Debug.Log($"[GameSetup] P1 棋子({p1PieceData.displayName})放置在 {coord}");
        }
        else
        {
            Debug.LogWarning("[GameSetup] 未指定 p1PieceData，跳过 P1 棋子生成");
        }

        // 放置 P2
        if (p2PieceData != null)
        {
            var coord = new HexCoord(p2StartCoord.x, p2StartCoord.y);
            PieceManager.Instance.SpawnPieceById(p2PieceData.id, coord, PlayerSide.P2);
            Debug.Log($"[GameSetup] P2 棋子({p2PieceData.displayName})放置在 {coord}");
        }
        else
        {
            Debug.LogWarning("[GameSetup] 未指定 p2PieceData，跳过 P2 棋子生成");
        }
    }
}
