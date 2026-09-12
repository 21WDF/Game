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

    // ==========================================
    //  零素材视觉反馈（闪白 · 元素光环 · 大招扩散）—— v2：MPB 颜色 + 内置 Cylinder 压扁薄圆盘
    //  设计约定：
    //    · 三态（选中/已行动/闪白）全部收敛到 _BaseColor 通道（GenshinToon shader 真实读取的
    //      颜色属性；_EmissionColor 在该 shader 中不存在，故 emission 方案废弃——见 v2 诊断）。
    //      协调靠「乘法优先级（闪白 > 选中 > 已行动）+ 状态缓存 + 闪白每帧重算 + 结束精确复原」。
    //    · 光环/扩散 = 运行时惰性创建的 Cylinder 薄圆盘（轴向沿 Y 天然平铺地面；场景根层级，
    //      不挂动作宿主 → 不随动作晃；组装时不带任何 Collider → 不影响射线点击命中）。
    //      材质 Resources.Load("PieceAuraMaterial")，MPB 按实例上色，不克隆共享材质。
    //    · 全部纯异步表现：不锁输入、不参与任何结算；时长/幅度来自 GameConfig「棋子视觉反馈」区块。
    // ==========================================
    private bool _selected;          // 选中态缓存（闪白结束/过程中重放用）
    private bool _acted;             // 已行动态缓存（与 _selected 同思路；闪白 OnUpdate 按缓存重算组合色）
    private bool _flashPlaying;      // 闪白进行中：SetSelected/SetActed 只缓存状态，闪白结束统一重放
    private float _flashV;           // 闪白进度 0→1（sin 曲线驱动「亮起→回落」）
    private float _pulseV;           // 元素光环反应脉冲进度
    private float _spreadV;          // 大招扩散进度
    private Transform _aura;         // 元素光环（Cylinder 压扁薄圆盘，场景根）
    private MeshRenderer _auraRenderer;
    private MaterialPropertyBlock _auraMpb;
    private Vector3 _auraBaseScale;  // 光环基础缩放（可见半径 → 换算后的 localScale）
    private Color _auraColor;        // 光环当前元素色（含 alpha）
    private Transform _spread;       // 大招扩散环（Cylinder 压扁薄圆盘，场景根）
    private MeshRenderer _spreadRenderer;
    private MaterialPropertyBlock _spreadMpb;
    private Material _auraMaterial;  // 共享无贴图材质（Resources 懒加载缓存）

    private const float AuraAlpha = 0.45f;        // 元素光环透明度（未入配置清单，常量）
    private const float SpreadMaxAlpha = 0.8f;    // 大招扩散起始透明度
    private const string AuraMaterialPath = "PieceAuraMaterial";
    private const float DiscThickness = 0.02f;    // 薄圆盘厚度（世界单位；恒定，避免与地面 z-fighting）
    private const float FlashBright = 2.5f;       // 闪白峰值亮度（_BaseColor × 2.5 白亮；GenshinToon 未 clamp albedo，>1 可读出高亮）

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
        LogRendererDiagnostics();   // 一次性诊断：确认颜色通道真实存在（不再凭假设写属性名）
    }

    // ==========================================
    //  绑定（由 PieceManager.SpawnPieceById 调用）
    // ==========================================
    public void BindModel(PieceModel model)
    {
        if (Model != null)
            Model.OnElementChanged -= HandleElementChanged;
        Model = model;
        if (model != null)
        {
            model.OnElementChanged += HandleElementChanged;
            if (model.Data != null)
                gameObject.name = $"Piece_{model.Data.displayName}_{model.Owner}";
            // 初始元素态（防御性：绑定前已带元素附着的场景，如复活后重建）
            ApplyAuraElement(model.AffixedElement);
        }
    }

    // ==========================================
    //  视图同步（由 PieceManager 在移动/生成时调用）
    // ==========================================
    /// <summary>瞬移到指定世界坐标（Phase 1 无动画；后续 Phase 可改为协程/缓动）</summary>
    public void SnapToPosition(Vector3 worldPos)
    {
        transform.position = worldPos;
        SyncAuraPosition();   // 光环固定跟随格子位置（根层级独立，不随动作晃）
    }

    // ==========================================
    //  动作表现（程序化 · DOTween 驱动）—— v2：动作宿主 = 棋子根的运行时父节点，全结构通用
    //  设计约定：
    //    · 动作宿主 _actionHost 是棋子根的父节点（惰性创建，不新增预制体/不改场景结构）。
    //      实际棋子 prefab 均为 fbx 的 PrefabInstance：PieceView/BoxCollider/Renderer 都在根对象上，
    //      子节点是骨骼层级（无 Renderer）——因此不搬移任何子节点，而是把整棵树挂到宿主下随宿主一起动，
    //      对「Renderer 在根 / 在子节点」两种结构都通用，不再依赖「模型必须在子节点」的假设。
    //    · 移动动画（AnimateMove）仍作用于棋子根 transform；动作只作用于宿主。
    //      二者是不同 transform：互不 Kill、互不争抢位置通道。
    //    · 动作不改变逻辑坐标：动作开始/结束时宿主与棋子根都被强制对齐到逻辑位置
    //      （Model.Coord → 棋盘世界坐标），不残留任何偏移。
    //    · 动作不锁输入、不阻塞逻辑：纯异步表现，不碰移动的 IsPieceAnimating 锁定。
    //    · 坐标系约定：宿主 __PieceActionHost 无父节点（场景根）→ 其 localPosition 与 position 是
    //      同一个值（世界坐标）。因此宿主的一切位移一律使用世界坐标语义（position / DOMove），
    //      禁止使用 localPosition / DOLocalMove——否则"相对偏移"会被解释为"移动到世界坐标
    //      (0,-0.6,0) 等绝对位置"。缩放（localScale / DOScale / DOPunchScale）对无父节点对象而言
    //      局部即世界，无歧义。
    //    · 幅度/时长来自 GameConfig「棋子动作表现」区块；召唤出现时长未入配置清单，用常量。
    // ==========================================

    /// <summary>动作宿主（棋子根的运行时父节点；动作 tween 全部挂在这个 transform 上）</summary>
    private Transform _actionHost;

    /// <summary>棋子当前逻辑位置（Model.Coord → 棋盘世界坐标；棋盘不可用时回退当前世界位置）</summary>
    private Vector3 GetLogicalPosition()
    {
        if (Model != null && ChessBoardController.Instance != null)
            return ChessBoardController.Instance.GetCellWorldPosition(Model.Coord)
                 + Vector3.up * (_config != null ? _config.pieceYOffset : 0f);
        return transform.position;
    }

    /// <summary>惰性构建动作宿主：把棋子根整体搬入新建父节点 __PieceActionHost（worldPositionStays，视觉零变化）。
    /// 不搬移任何子节点、不改任何 prefab 结构；动作作用于宿主 = 整棵树（含根上的 Renderer 与 Collider）随动。
    /// 首次构建时输出一次结构诊断（根自身 Renderer / 子节点 Renderer·Collider 分布 / 动作作用对象），
    /// 若未来出现无法支持的结构可据此告警，不允许再出现"动作悄悄不播"。</summary>
    private void EnsureActionHost()
    {
        if (_actionHost != null) return;
        var host = new GameObject("__PieceActionHost");
        host.transform.position = transform.position;
        host.transform.rotation = Quaternion.identity;
        _actionHost = host.transform;
        transform.SetParent(_actionHost, true);   // worldPositionStays：视觉上零变化

        int rendererChildren = 0, colliderChildren = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.GetComponentInChildren<Renderer>(includeInactive: true) != null) rendererChildren++;
            if (child.GetComponentInChildren<Collider>(includeInactive: true) != null) colliderChildren++;
        }
        Debug.Log($"[PieceView] 动作宿主构建：{gameObject.name} | 根自身含 Renderer=" +
                  $"{GetComponent<Renderer>() != null}，" +
                  $"子节点 {transform.childCount} 个（含 Renderer {rendererChildren}，含 Collider {colliderChildren}）；" +
                  $"动作作用对象 = __PieceActionHost（棋子根父节点，整树随动，含 Renderer 与 Collider）");
    }

    /// <summary>动作开始：杀死旧动作 tween，把宿主与棋子根对齐到逻辑位置（消除上一次动作被中途替换 / 移动补偿的残留）。
    /// 若棋子正被 AnimateMove 移动（罕见），下一帧移动 tween 会重新接管位置，无持久偏差。</summary>
    private void BeginAction()
    {
        EnsureActionHost();
        DOTween.Kill(_actionHost, complete: false);
        Vector3 logical = GetLogicalPosition();
        _actionHost.position = logical;
        transform.position = logical;
        _actionHost.localScale = Vector3.one;
    }

    /// <summary>动作结束：宿主与棋子根强制回到逻辑位置 + 复位缩放（双保险；移动 tween 若仍在运行，下一帧由其重新接管）。
    /// 死亡动作不调用本方法——棋子即将销毁，保持缩小/下沉形态直至消失，避免回弹闪烁。</summary>
    private void EndAction()
    {
        Vector3 logical = GetLogicalPosition();
        _actionHost.position = logical;
        transform.position = logical;
        _actionHost.localScale = Vector3.one;
    }

    /// <summary>攻击动作：朝目标方向前冲约 attackDashDistance 再弹回原位（作用于动作宿主，整树随动）。
    /// 纯表现；由 PieceManager 在 AttackPiece 结算完成时驱动。</summary>
    public void PlayAttack(Vector3 targetPosition)
    {
        BeginAction();
        float dur = _config != null ? _config.attackDuration : 0.25f;
        float dash = _config != null ? _config.attackDashDistance : 0.3f;

        Vector3 dir = targetPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;  // 兜底方向（目标与自身重叠）
        dir.Normalize();

        Vector3 basePos = _actionHost.position;
        var seq = DOTween.Sequence().SetTarget(_actionHost);
        seq.Append(_actionHost.DOMove(basePos + dir * dash, dur * 0.45f).SetEase(Ease.OutQuad));   // 前冲
        seq.Append(_actionHost.DOMove(basePos, dur * 0.55f).SetEase(Ease.InQuad));                  // 弹回
        seq.OnComplete(EndAction);

        PlaySfxAttack();   // 音效接入点（攻击）
    }

    /// <summary>受击·完整：沿受击反方向位移 hitDisplacement + 短促缩放脉冲，随后回位（作用于动作宿主，整树随动）。
    /// fromPosition 为伤害来源方位置（用于计算受击反方向）；来源不可得时退化为仅脉冲。
    /// 由 PieceManager 在直接伤害来源（普攻/溅射/大招）命中时驱动。</summary>
    public void TakeHit(Vector3? fromPosition)
    {
        BeginAction();
        FlashHit(false);   // 受击闪白·完整（颜色维度反馈，与位移/抖动同时发生）
        float dur = _config != null ? _config.hitDuration : 0.2f;
        float dist = _config != null ? _config.hitDisplacement : 0.15f;

        if (!fromPosition.HasValue)
        {
            // 来源位置不可得 → 退化为仅脉冲（同轻量）
            var tween = _actionHost.DOPunchScale(new Vector3(0.05f, 0.05f, 0.05f), Mathf.Min(0.15f, dur * 0.7f), 3, 1f);
            tween.SetTarget(_actionHost);
            tween.OnComplete(() => _actionHost.localScale = Vector3.one);
            PlaySfxHit();   // 音效接入点（受击）
            return;
        }

        Vector3 dir = _actionHost.position - fromPosition.Value;   // 受击反方向
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
        else dir = -transform.forward;
        Vector3 basePos = _actionHost.position;

        var seq = DOTween.Sequence().SetTarget(_actionHost);
        seq.Append(_actionHost.DOMove(basePos + dir * dist, dur * 0.35f).SetEase(Ease.OutQuad));   // 退
        seq.Join(_actionHost.DOPunchScale(new Vector3(0.05f, 0.05f, 0.05f), Mathf.Min(0.15f, dur * 0.7f), 3, 1f));  // 与退同步：命中瞬间脉冲
        seq.Append(_actionHost.DOMove(basePos, dur * 0.65f).SetEase(Ease.InQuad));                  // 回位
        seq.OnComplete(EndAction);

        PlaySfxHit();   // 音效接入点（受击）
    }

    /// <summary>受击·轻量：仅短促缩放脉冲（无位移）——DoT / 环境伤害（元素格持续伤害、雷暴残留、感电 DoT、
    /// 反伤等）用，避免每回合全场抖动。由 PieceManager 受击分级统一驱动。</summary>
    public void TakeLightHit()
    {
        BeginAction();
        FlashHit(true);    // 受击闪白·轻量（DoT/环境唯一可视反馈，强度弱于完整）
        float dur = _config != null ? _config.hitDuration : 0.2f;
        var tween = _actionHost.DOPunchScale(new Vector3(0.05f, 0.05f, 0.05f), Mathf.Min(0.15f, dur * 0.7f), 2, 1f);
        tween.SetTarget(_actionHost);
        tween.OnComplete(() => _actionHost.localScale = Vector3.one);

        PlaySfxHit();   // 音效接入点（受击）
    }

    /// <summary>死亡动作：缩小 + 下沉（倒伏感），播完回调 onComplete（由 PieceManager 延后销毁视图）。
    /// 逻辑层死亡处理由 PieceManager 在调用本方法前立即完成；本方法只负责视觉。
    /// 播完不复位（棋子即将销毁，保持缩小/下沉形态），视图与动作宿主在 OnDestroy 中一并清理。</summary>
    public void PlayDeath(Action onComplete = null)
    {
        BeginAction();
        float dur = _config != null ? _config.deathDuration : 0.3f;

        // 世界坐标语义（宿主无父节点）：从宿主当前位置沿世界 Y 轴下沉 0.6（保持原幅度），
        // 而非移动到绝对坐标 (0, -0.6, 0)
        Vector3 sinkBase = _actionHost.position;
        var seq = DOTween.Sequence().SetTarget(_actionHost);
        seq.Join(_actionHost.DOScale(0.02f, dur).SetEase(Ease.InCubic));
        seq.Join(_actionHost.DOMove(sinkBase + Vector3.down * 0.6f, dur).SetEase(Ease.InCubic));
        seq.OnComplete(() => onComplete?.Invoke());

        PlaySfxDeath();   // 音效接入点（死亡）
    }

    /// <summary>召唤出现动作：由小放大 + 轻微上浮（出现感），动作结束复位到逻辑位置。
    /// 仅召唤物（isSummon）生成时由 PieceManager 调用；开局部署的棋子不播（避免喧闹）。</summary>
    public void PlaySummonAppearance()
    {
        BeginAction();
        const float summonDuration = 0.25f;   // 召唤出现时长（未纳入 GameConfig 5 字段清单，用常量；与验收表 ≤0.25s 一致）

        // 世界坐标语义（宿主无父节点）：在召唤格位置略下方（-0.3）出现，再升回召唤格，
        // 全程不离开召唤格所在的世界坐标；而非跳到世界原点
        Vector3 tilePos = _actionHost.position;
        _actionHost.position = tilePos + Vector3.down * 0.3f;
        _actionHost.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        var seq = DOTween.Sequence().SetTarget(_actionHost);
        seq.Join(_actionHost.DOScale(1f, summonDuration).SetEase(Ease.OutBack));
        seq.Join(_actionHost.DOMove(tilePos, summonDuration).SetEase(Ease.OutQuad));
        seq.OnComplete(EndAction);

        PlaySfxSummon();   // 音效接入点（召唤出现）
    }

    /// <summary>销毁时清理动作宿主与零素材视觉对象（宿主/光环/扩散都是运行时父节点/根级对象，
    /// 不会随棋子根一起销毁，需显式清理，防孤儿节点）</summary>
    private void OnDestroy()
    {
        if (Model != null)
            Model.OnElementChanged -= HandleElementChanged;
        DOTween.Kill(this, complete: false);   // 闪白 tween（目标 = 本组件）
        if (_aura != null)
        {
            DOTween.Kill(_aura, complete: false);
            Destroy(_aura.gameObject);
        }
        if (_spread != null)
        {
            DOTween.Kill(_spread, complete: false);
            Destroy(_spread.gameObject);
        }
        if (_actionHost != null && _actionHost.gameObject != null)
            Destroy(_actionHost.gameObject);
    }

    // ---- 音效接入位（本期空实现；将来接入音效系统时只填充以下四个方法，不触动动作逻辑）----
    /// <summary>音效接入点（攻击）：PlayAttack 触发时调用</summary>
    private void PlaySfxAttack() { /* 音效接入点：播放「攻击」音效 */ }

    /// <summary>音效接入点（受击）：TakeHit / TakeLightHit 触发时调用</summary>
    private void PlaySfxHit() { /* 音效接入点：播放「受击」音效 */ }

    /// <summary>音效接入点（死亡）：PlayDeath 触发时调用</summary>
    private void PlaySfxDeath() { /* 音效接入点：播放「死亡」音效 */ }

    /// <summary>音效接入点（召唤出现）：PlaySummonAppearance 触发时调用</summary>
    private void PlaySfxSummon() { /* 音效接入点：播放「召唤出现」音效 */ }

    // ==========================================
    //  选中高亮（由 BattleView 订阅 OnSelectionChanged 调用）
    // ==========================================
    /// <summary>切换选中高亮（同闪白/变灰收敛到 _BaseColor 通道，乘法叠加）。
    /// 选中 = 基础色 × 黄色提亮。闪白进行中只缓存状态、延迟到闪白结束重放——
    /// 保证闪白后四种组合（选中/变灰/叠加/皆无）精确复原。</summary>
    public void SetSelected(bool selected)
    {
        if (_mpb == null || _renderers == null) return;
        _selected = selected;
        if (_flashPlaying) return;   // 闪白中：缓存状态，闪白 OnUpdate/结束按最新缓存取色，不中途抢占
        ApplyStateColor();
    }

    // ==========================================
    //  已行动变灰标记（绑定 HasAttackedThisTurn 语义；纯视觉不锁交互）
    // ==========================================
    /// <summary>已行动标记：acted=true 去饱和变灰（基础色 × actedPieceColor），false 恢复原色。
    /// 与选中/闪白同为 _BaseColor 通道，靠「优先级 + 缓存 + 每帧重算」协调，而非通道隔离。</summary>
    public void SetActed(bool acted)
    {
        if (_mpb == null || _renderers == null) return;
        _acted = acted;
        if (_flashPlaying) return;   // 闪白中：缓存状态，闪白结束按最新缓存重放
        ApplyStateColor();
    }

    /// <summary>运行时诊断（一次性）：打印每个棋子渲染器的 shader 名称 + 颜色属性是否存在 +
    /// 当前 _BaseColor 值。用于验证「颜色通道是否真实存在」——不再凭假设写属性名。</summary>
    private void LogRendererDiagnostics()
    {
        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r.sharedMaterial == null) continue;
            var mat = r.sharedMaterial;
            Debug.Log($"[PieceView 诊断] {gameObject.name} | renderer={r.name} | shader={mat.shader.name} | " +
                      $"HasBaseColor={mat.HasProperty("_BaseColor")} | HasEmissionColor={mat.HasProperty("_EmissionColor")} | " +
                      $"baseColor={mat.GetColor("_BaseColor")} | matHasKeywordEMISSION={mat.IsKeywordEnabled("_EMISSION")}");
        }
    }

    // ==========================================
    //  零素材视觉反馈实现（闪白 · 元素光环 · 大招扩散）
    // ==========================================

    // ---- 闪白（颜色维度反馈；完整/轻量共用，强度分级）----
    /// <summary>受击闪白：_BaseColor「白亮 → 回落」，与位移/抖动动作同时发生（纯异步，不锁输入）。
    /// 协调：闪白期间每帧按最新缓存（_selected/_acted）重算组合色，结束/打断时精确复原——
    /// 选中/已行动/闪白三态任意组合任意时序都能回到正确视觉。light=true 时强度 × lightHitFlashFactor。</summary>
    public void FlashHit(bool light)
    {
        if (_mpb == null || _renderers == null) return;
        float intensity = _config != null ? _config.hitFlashIntensity : 0.8f;
        if (light)
            intensity *= _config != null ? _config.lightHitFlashFactor : 0.6f;
        float dur = _config != null ? _config.hitFlashDuration : 0.12f;

        DOTween.Kill(this, complete: false);   // 防重入：新闪白顶掉旧闪白
        _flashPlaying = true;
        _flashV = 0f;

        var tween = DOTween.To(() => _flashV, v => _flashV = v, 1f, dur)
            .SetTarget(this).SetEase(Ease.Linear);
        tween.OnUpdate(() =>
        {
            float k = Mathf.Sin(_flashV * Mathf.PI);          // 0→1→0：亮起再回落
            Color state = GetStateColor();                     // 每帧取当前选中/已行动态 → 闪白中切换状态也能正确回落
            Color flashColor = Color.Lerp(state, Color.white * FlashBright, k * intensity);
            SetBaseColor(flashColor);
        });
        tween.OnComplete(() =>
        {
            _flashPlaying = false;
            ApplyStateColor();                                 // 精确复原（四种组合之一）
        });
    }

    /// <summary>当前状态组合色（乘法优先级：闪白 > 选中 > 已行动）。
    /// 基础色 = 原色(1,1,1) × 选中提亮(1.35 黄) × 变灰系数(actedPieceColor)。</summary>
    private Color GetStateColor()
    {
        Color c = Color.white;
        if (_selected) c *= new Color(1f, 1f, 0.6f) * 1.35f;         // 选中：偏黄提亮
        if (_acted) c *= _config != null ? _config.actedPieceColor : new Color(0.55f, 0.55f, 0.55f, 1f);   // 已行动：去饱和变灰
        return c;
    }

    /// <summary>按缓存状态写回 _BaseColor（闪白结束/非闪白期的统一出口）</summary>
    private void ApplyStateColor()
    {
        if (_mpb == null) return;
        SetBaseColor(GetStateColor());
    }

    /// <summary>写 _BaseColor（带属性存在性自校验——GenshinToon 确实声明 _BaseColor；
    /// 若未来换 shader 缺属性立即报警，不静默失效）</summary>
    private void SetBaseColor(Color c)
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (!_renderers[0].sharedMaterial.HasProperty("_BaseColor"))
        {
            Debug.LogWarning($"[PieceView] shader {_renderers[0].sharedMaterial.shader.name} 无 _BaseColor 属性，视觉反馈禁用（逻辑不受影响）", this);
            return;
        }
        _mpb.SetColor("_BaseColor", c);
        ApplyMpbToRenderers();
    }

    private void ApplyMpbToRenderers()
    {
        if (_renderers == null) return;
        foreach (var r in _renderers)
            r.SetPropertyBlock(_mpb);
    }

    // ---- 元素光环（Cylinder 压扁薄圆盘，地面语义）----
    /// <summary>共享无贴图材质（用户手工创建的 PieceAuraMaterial；Resources 懒加载缓存）</summary>
    private Material GetAuraMaterial()
    {
        if (_auraMaterial == null)
            _auraMaterial = Resources.Load<Material>(AuraMaterialPath);
        return _auraMaterial;
    }

    /// <summary>可见半径 R 的薄圆盘换算：Cylinder 原始尺寸为直径 1（半径 0.5）、高 2、轴向沿 Y。
    /// 要得到可见半径 R（世界单位）→ localScale = (R×2, 薄厚, R×2)——scale.x/z 必须 ×2 才是
    /// 可见半径（原始半径 0.5）；y 保持恒定薄厚（避免与地面 z-fighting）。轴向本就沿 Y，天然平铺，
    /// 不得做任何旋转。</summary>
    private static Vector3 DiscScale(float visibleRadius)
        => new Vector3(visibleRadius * 2f, DiscThickness, visibleRadius * 2f);

    /// <summary>运行时构建地面薄圆盘对象（Cylinder）：网格取自临时原始体的 sharedMesh（内置共享资源，
    /// 不生成新网格），再用纯净 GameObject 组装 MeshFilter+MeshRenderer → 不带任何 Collider，
    /// 不影响射线点击命中。场景根层级，不挂动作宿主 → 不随受击/攻击动作晃动。
    /// 朝向：Cylinder 原始体轴向沿 Y（半径在 XZ 平面、高沿 Y）→ 压扁后即地面圆盘，天然平铺，无需旋转。</summary>
    private Transform CreateGroundRing(string name)
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var tmpFilter = tmp.GetComponent<MeshFilter>();
        Mesh mesh = tmpFilter != null ? tmpFilter.sharedMesh : null;
        Destroy(tmp);   // 临时体销毁（含其自带 Collider）

        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetAuraMaterial();
        return go.transform;   // 不做任何旋转（轴向沿 Y 即平铺）
    }

    /// <summary>惰性创建元素光环：仅当棋子第一次有元素附着时创建（无附着/无表现不创建）；
    /// 之后隐藏用 SetActive(false) 而非销毁，OnDestroy 时彻底清理。</summary>
    private void EnsureAura()
    {
        if (_aura != null) return;
        if (GetAuraMaterial() == null)
        {
            Debug.LogWarning($"[PieceView] 未找到 {AuraMaterialPath}（Assets/Resources/ 下），元素光环与扩散禁用（仅缺视觉，不影响逻辑）");
            return;
        }
        _aura = CreateGroundRing("__PieceAura");
        _auraRenderer = _aura.GetComponent<MeshRenderer>();
        _auraMpb = new MaterialPropertyBlock();
        _auraBaseScale = DiscScale(_config != null ? _config.auraRadius : 0.6f);   // 可见半径 = auraRadius
        _aura.localScale = _auraBaseScale;
        _aura.gameObject.SetActive(false);   // 默认隐藏，附着出现才显示
    }

    /// <summary>元素附着显示/隐藏/换色（由 Model.OnElementChanged 订阅驱动；纯表现，不参与结算）。
    /// None → 隐藏；非 None → 显示 + 元素色（复用 ElementColorMapper 现有颜色映射，不新增硬编码）+ 锚定当前格。</summary>
    private void ApplyAuraElement(ElementType element)
    {
        if (element == ElementType.None)
        {
            if (_aura != null) _aura.gameObject.SetActive(false);
            return;
        }
        EnsureAura();
        if (_aura == null) return;
        _auraColor = ElementColorMapper.GetElementDamageColor(element);
        _auraColor.a = AuraAlpha;
        _auraMpb.SetColor("_BaseColor", _auraColor);
        _auraRenderer.SetPropertyBlock(_auraMpb);
        _aura.gameObject.SetActive(true);
        SyncAuraPosition();
    }

    /// <summary>元素附着变化监听（订阅 Model.OnElementChanged）：驱动光环显示/隐藏/换色。
    /// 纯表现通知，不参与任何结算。</summary>
    private void HandleElementChanged(ElementType element)
        => ApplyAuraElement(element);

    /// <summary>光环/扩散的地面锚点：逻辑格中心 + 离地高度（注意：不加 pieceYOffset——那是棋子
    /// 中心偏移，地面视觉用格面位置）。Model/棋盘不可用时回退棋子当前世界位置。</summary>
    private void SyncGroundVisualPosition(Transform groundFx)
    {
        if (groundFx == null) return;
        Vector3 ground;
        if (Model != null && ChessBoardController.Instance != null)
            ground = ChessBoardController.Instance.GetCellWorldPosition(Model.Coord);
        else
            ground = transform.position;
        groundFx.position = ground + Vector3.up * (_config != null ? _config.auraHeight : 0.02f);
    }

    /// <summary>光环位置同步（SnapToPosition / 逐格移动 / 附着出现时调用；不随动作晃）</summary>
    private void SyncAuraPosition() => SyncGroundVisualPosition(_aura);

    /// <summary>反应触发脉冲（纯表现）：光环「放大 + 亮起再回落」，时长 reactionPulseDuration。
    /// 无光环/已隐藏时无操作（反应消耗清空元素后光环消失，无可脉冲）；不改变颜色语义——
    /// 亮度以当前元素色为基色向白偏亮，回落精确还原。</summary>
    public void PulseAura()
    {
        if (_aura == null || !_aura.gameObject.activeSelf) return;
        float dur = _config != null ? _config.reactionPulseDuration : 0.25f;
        DOTween.Kill(_aura, complete: false);   // 防重入：连续反应只保留最新一次脉冲
        _pulseV = 0f;

        var tween = DOTween.To(() => _pulseV, v => _pulseV = v, 1f, dur).SetTarget(_aura);
        tween.OnUpdate(() =>
        {
            float k = Mathf.Sin(_pulseV * Mathf.PI);
            _aura.localScale = _auraBaseScale * (1f + k * 0.6f);
            _auraMpb.SetColor("_BaseColor", Color.Lerp(_auraColor, Color.white, k * 0.5f));
            _auraRenderer.SetPropertyBlock(_auraMpb);
        });
        tween.OnComplete(() =>
        {
            _aura.localScale = _auraBaseScale;
            _auraMpb.SetColor("_BaseColor", _auraColor);
            _auraRenderer.SetPropertyBlock(_auraMpb);
        });
    }

    // ---- 大招扩散光环（仪式感；独立对象，与元素光环互不干扰）----
    private void EnsureSpread()
    {
        if (_spread != null) return;
        if (GetAuraMaterial() == null) return;   // 缺材质静默跳过（EnsureAura 已告警一次）
        _spread = CreateGroundRing("__PieceUltimateSpread");
        _spreadRenderer = _spread.GetComponent<MeshRenderer>();
        _spreadMpb = new MaterialPropertyBlock();
        _spread.gameObject.SetActive(false);
    }

    /// <summary>大招释放扩散光环（纯表现）：释放者脚下圆环「由小放大并淡出」，时长 ultimateSpreadDuration。
    /// 独立于元素光环（无元素附着的释放者也播）；锚定释放者当前逻辑格（Execute 后调用 →
    /// 雷驰突进等移动释放者的大招锚定最终位置）；不参与结算、不阻塞能量消耗/回合流转。</summary>
    public void PlayUltimateSpread()
    {
        if (Model == null || Model.IsDestroyed || !gameObject.activeInHierarchy) return;
        EnsureSpread();
        if (_spread == null) return;
        float dur = _config != null ? _config.ultimateSpreadDuration : 0.35f;
        float maxRadius = _config != null ? _config.ultimateMaxRadius : 1.5f;

        SyncGroundVisualPosition(_spread);
        _spread.gameObject.SetActive(true);
        DOTween.Kill(_spread, complete: false);
        _spreadV = 0f;
        _spread.localScale = DiscScale(0.2f);   // 起始可见半径 0.2（换算见 DiscScale）

        // 扩散色：释放者先天元素色；无先天元素 → 白
        Color baseColor = Model.Data != null && Model.Data.innateElement != ElementType.None
            ? ElementColorMapper.GetElementDamageColor(Model.Data.innateElement)
            : Color.white;
        baseColor.a = SpreadMaxAlpha;
        _spreadMpb.SetColor("_BaseColor", baseColor);
        _spreadRenderer.SetPropertyBlock(_spreadMpb);

        var tween = DOTween.To(() => _spreadV, v => _spreadV = v, 1f, dur).SetTarget(_spread);
        tween.OnUpdate(() =>
        {
            _spread.localScale = DiscScale(Mathf.Lerp(0.2f, maxRadius, _spreadV));   // 可见半径 0.2 → maxRadius
            baseColor.a = Mathf.Lerp(SpreadMaxAlpha, 0f, _spreadV);
            _spreadMpb.SetColor("_BaseColor", baseColor);
            _spreadRenderer.SetPropertyBlock(_spreadMpb);
        });
        tween.OnComplete(() => _spread.gameObject.SetActive(false));
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
                SyncAuraPosition();   // 光环逐格吸附到新格子（固定跟随格子位置）
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
