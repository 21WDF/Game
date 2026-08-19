using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 棋子视图（Piece 模块的 View）—— 挂载在棋子 Prefab 根对象上的轻量渲染组件。
/// 职责：仅负责渲染表现（位置同步、动画钩子），不持有任何业务逻辑或战斗数据。
///       运行时状态由 <see cref="PieceModel"/> 持有，行为由 <see cref="PieceManager"/> 驱动。
///
/// 设计要点：
///   1) 本类取代旧的 fat-object <c>Unit</c>，去掉 maxHP/attack/moveRange 等数据字段，
///      这些数据现由 <see cref="PieceData"/>（SO）定义、<see cref="PieceModel"/> 运行时持有。
///   2) 保留脚本 GUID（6dcd93020ea80f041865f664a5dc62de），使旧 Unit_P1/Unit_P2 Prefab
///      的脚本引用在重命名后仍然有效；旧序列化字段（unitName/maxHP/...）会被 Unity 忽略。
///   3) 与 <see cref="PieceModel"/> 的绑定通过 <see cref="BindModel"/> 由 PieceManager 注入，
///      避免视图反向创建模型，保持单向依赖（Controller → Model/View）。
/// </summary>
public class PieceView : MonoBehaviour
{
    /// <summary>当前绑定的运行时模型（由 PieceManager 在生成时注入）</summary>
    public PieceModel Model { get; private set; }

    /// <summary>是否已绑定模型</summary>
    public bool IsBound => Model != null;

    // 选中高亮（MaterialPropertyBlock 隔离每棋子发射色，不克隆材质）
    private MaterialPropertyBlock _mpb;
    private Renderer[] _renderers;
    private Animator _animator;
    private GameConfig _config;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _animator = GetComponentInChildren<Animator>(includeInactive: true);
        _config = Resources.Load<GameConfig>("GameConfig");
        // 启动时对 sharedMaterial 开启发射关键字（EnableKeyword 幂等，共享材质只需开一次）
        foreach (var r in _renderers)
        {
            if (r.sharedMaterial != null)
                r.sharedMaterial.EnableKeyword("_EMISSION");
        }
    }

    // ==========================================
    //  绑定（由 PieceManager.SpawnPieceById 调用）
    // ==========================================
    public void BindModel(PieceModel model)
    {
        Model = model;
        if (model != null && model.Data != null)
            gameObject.name = $"Piece_{model.Data.displayName}_{model.Owner}";
    }

    // ==========================================
    //  视图同步（由 PieceManager 在移动/生成时调用）
    // ==========================================
    /// <summary>瞬移到指定世界坐标（Phase 1 无动画；后续 Phase 可改为协程/缓动）</summary>
    public void SnapToPosition(Vector3 worldPos)
    {
        transform.position = worldPos;
    }

    /// <summary>死亡销毁前的视图清理钩子（后续 Phase 可在此播放死亡动画）</summary>
    public void PlayDeath()
    {
        // Phase 1：直接销毁；Phase 2+ 可在此播放粒子/动画后再 Destroy
        // 注意：实际 Destroy 由 PieceManager 统一调用，这里只做表现层准备
    }

    // ==========================================
    //  选中高亮（由 BattleView 订阅 OnSelectionChanged 调用）
    // ==========================================
    /// <summary>切换选中高亮：selected=true 发射黄色×0.3，false 发射黑色（不可见）。
    /// 用 MaterialPropertyBlock 逐 renderer 覆盖，不修改共享材质、不克隆。</summary>
    public void SetSelected(bool selected)
    {
        if (_mpb == null || _renderers == null) return;
        Color emissionColor = selected ? new Color(1f, 1f, 0f) * 0.3f : Color.black;
        _mpb.SetColor("_EmissionColor", emissionColor);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_mpb);
    }

    // ==========================================
    //  已行动变灰标记（绑定 HasAttackedThisTurn 语义；纯视觉不锁交互）
    // ==========================================
    /// <summary>已行动标记：acted=true 去饱和变灰，false 恢复原色。
    /// 复用 _mpb/_renderers——_BaseColor 与选中发光 _EmissionColor 是 MPB 里的独立属性，叠加不冲突。</summary>
    public void SetActed(bool acted)
    {
        if (_mpb == null || _renderers == null) return;
        Color actedColor = _config != null ? _config.actedPieceColor : new Color(0.55f, 0.55f, 0.55f, 1f);
        _mpb.SetColor("_BaseColor", acted ? actedColor : Color.white);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_mpb);
    }

    // ==========================================
    //  移动动画（DOTween 逐格滑动 + Animator 表现）
    // ==========================================
    /// <summary>切换移动动画状态：isMoving=true 播 Walk/Run，false 回 Idle。
    /// Animator.speed 由 GameConfig 的 walkAnimSpeed/runAnimSpeed 控制。</summary>
    public void SetMoving(bool isMoving, bool isRunning)
    {
        if (_animator == null) return;
        _animator.SetBool("IsMoving", isMoving);
        _animator.SetBool("IsRunning", isRunning);
        _animator.speed = isMoving
            ? (isRunning ? (_config != null ? _config.runAnimSpeed : 1f)
                         : (_config != null ? _config.walkAnimSpeed : 1f))
            : 1f;
    }

    /// <summary>停止正在进行的移动动画（不触发 OnComplete，避免重复解锁）</summary>
    public void StopMoveAnimation()
    {
        DOTween.Kill(transform, complete: false);
    }

    /// <summary>沿路径逐格滑动动画。每格动画完成后立即同步逻辑（SetCoord + 占据表），
    /// 全部完成后触发 onComplete。durationPerTile=0 时逐格瞬移（无分支）。</summary>
    public void AnimateMove(List<Vector3> worldPath, List<HexCoord> hexPath,
        float durationPerTile, bool isRunning, Action<HexCoord> onTileTraversed, Action onComplete)
    {
        // 路径过短 → 直接触发完成回调（安全兜底，确保 IsPieceAnimating 被重置）
        if (worldPath == null || hexPath == null || worldPath.Count < 2 || hexPath.Count < 2)
        {
            onComplete?.Invoke();
            return;
        }

        // 杀掉正在进行的移动动画（防重叠）
        DOTween.Kill(transform, complete: false);

        // 开始移动动画表现（Walk/Run）
        SetMoving(true, isRunning);

        var seq = DOTween.Sequence();
        int count = Mathf.Min(worldPath.Count, hexPath.Count) - 1;
        for (int i = 0; i < count; i++)
        {
            int idx = i;  // 闭包捕获
            seq.Append(transform.DOMove(worldPath[idx + 1], durationPerTile).SetEase(Ease.Linear));
            // 平滑转向当前步移动方向（与 DOMove 并行；0.1s 完成）
            if (durationPerTile > 0f)
            {
                Vector3 dir = worldPath[idx + 1] - worldPath[idx];
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    seq.Join(transform.DORotateQuaternion(Quaternion.LookRotation(dir), Mathf.Min(0.1f, durationPerTile)));
            }
            // 每格动画完成 → 逻辑立即跟进（SetCoord + 占据表 + 途经回调）
            seq.AppendCallback(() =>
            {
                Model.SetCoord(hexPath[idx + 1]);
                PieceLayoutModel.Instance.MovePiece(hexPath[idx], hexPath[idx + 1]);
                onTileTraversed?.Invoke(hexPath[idx + 1]);
            });
        }
        seq.SetTarget(transform);  // 让 DOTween.Kill(transform) 能找到此 Sequence
        seq.OnComplete(() =>
        {
            SetMoving(false, false);  // 回 Idle
            onComplete?.Invoke();
        });
    }
}
