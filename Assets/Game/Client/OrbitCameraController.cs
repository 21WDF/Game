using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 轨道相机控制器 —— 挂载在场景主相机上（或任意常驻 GameObject，target 指向主相机）。
/// 挂载时自动从相机当前姿态反推焦点/水平角/俯仰角/距离，进场画面与既有视角完全一致
///（不改动相机组件的投影/FOV/裁剪面/位置初值，只在运行时驱动 transform）。
///
/// 操作（纯新增键位，不与既有 WASD/Shift/滚轮的任何用途冲突——这三组键此前空闲）：
///   · WASD 平移焦点：W=视野前方、S=后方、A/D=视野左右（沿相机水平朝向，随转向联动）；
///     焦点限制在棋盘范围 + 可配置边距内
///   · 左 Shift + 鼠标移动：转向（水平角无限制、俯仰角限定上下限），无需按鼠标键
///   · 滚轮：拉远拉近（距离限定上下限）
///   · 三者可同时生效；相机始终注视焦点（LookAt）
///
/// UI 守卫：鼠标位于面板 UI（带 UIPanelRaycastBlocker 标记）上时转向/缩放不生效
///（复用 InputHandler.IsPointerOverPanel 公开接口，判断行为与棋盘点击穿透守卫完全一致）；
/// 键盘平移不受 UI 影响。
///
/// 相机移动后所有动态读 Camera.main 的既有功能（棋盘点击射线/血条定位/跳字朝向）自动跟随。
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("目标相机（空则用本物体上的 Camera）")]
    [Tooltip("受控相机；为空时自动取本物体上的 Camera")]
    public Camera targetCamera;

    [Header("配置资产（空则读 Resources/OrbitCameraConfig，再空则用运行时默认值）")]
    [Tooltip("Create > Chess > Orbit Camera Config 创建后拖入；改数值无需改代码")]
    public OrbitCameraConfig config;

    private OrbitCameraConfig _runtimeConfig;

    // ---- 轨道状态（Start 时从相机姿态反推，进场零变化）----
    private Vector3 _focus;          // 注视焦点（棋盘平面上）
    private float _yaw;              // 水平角（度，不限制）
    private float _pitch;            // 俯仰角（度，clamp 上下限）
    private float _distance;         // 相机到焦点距离（clamp 上下限）

    /// <summary>生效配置（懒加载：Inspector → Resources → 运行时默认实例）</summary>
    private OrbitCameraConfig Config
    {
        get
        {
            if (_runtimeConfig == null)
            {
                _runtimeConfig = config != null ? config
                    : (Resources.Load<OrbitCameraConfig>("OrbitCameraConfig") ?? ScriptableObject.CreateInstance<OrbitCameraConfig>());
            }
            return _runtimeConfig;
        }
    }

    private void Start()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            Debug.LogError("[OrbitCameraController] 未找到目标相机（targetCamera 为空且本物体无 Camera）", this);
            enabled = false;
            return;
        }

        // ---- 从相机当前姿态反推轨道参数（进场视角零变化）----
        Vector3 forward = targetCamera.transform.forward;
        Vector3 camPos = targetCamera.transform.position;

        // 视线与水平面(y=0，棋盘平面)求交得焦点；视线朝上/水平时兜底取相机正下方
        _focus = IntersectGround(camPos, forward);

        Vector3 toCam = camPos - _focus;
        _distance = toCam.magnitude;
        // 水平角：相机在焦点 的方位（0=+Z 北向，顺时针为正）
        _yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        // 俯仰角：与水平面的夹角（现有场景 60° 俯视 → 反推 ~50°，进场画面与既有完全一致）
        _pitch = Mathf.Asin(toCam.normalized.y) * Mathf.Rad2Deg;
        _pitch = Mathf.Clamp(_pitch, Config.pitchMin, Config.pitchMax);

        ApplyTransform();
    }

    /// <summary>视线与棋盘平面（y=0）求交；视线朝上/近似水平时兜底：焦点取相机正下方投影</summary>
    private static Vector3 IntersectGround(Vector3 origin, Vector3 dir)
    {
        if (dir.y < -0.0001f)
        {
            float t = -origin.y / dir.y;
            return origin + dir * t;
        }
        return new Vector3(origin.x, 0f, origin.z);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // ---- 1. WASD 平移（沿相机视线水平朝向；不受面板 UI 影响）----
        Vector2 move = Vector2.zero;
        if (kb.wKey.isPressed) move.y += 1f;
        if (kb.sKey.isPressed) move.y -= 1f;
        if (kb.aKey.isPressed) move.x -= 1f;
        if (kb.dKey.isPressed) move.x += 1f;
        if (move.sqrMagnitude > 0f)
        {
            // 视线前方向（W 方向）＝「相机 → 焦点」视线的水平投影。_yaw 是反推出的
            //「焦点 → 相机」方位角（Atan2(toCam.x, toCam.z)），与视线方向正好相差 180°→取反。
            // 验证（初始姿态 yaw=180°）：前方向 = +Z = 玩家所见远处 ✓；转 180° 后随之反向
            Vector3 forward = new Vector3(-Mathf.Sin(_yaw * Mathf.Deg2Rad), 0f, -Mathf.Cos(_yaw * Mathf.Deg2Rad));
            // 右方向（D 方向）＝ up × forward（Cross 的展开式，与前方向严格垂直联动）→ 屏幕右。
            // 验证 yaw=180°（看向 +Z）：right = +X ✓；yaw=90°（看向 −X）：right = +Z ✓
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 delta = (forward * move.y + right * move.x).normalized
                            * (Config.panSpeed * Time.deltaTime);
            _focus += delta;
            _focus = ClampFocusToBoard(_focus);
        }

        // ---- 2. 左 Shift + 鼠标移动转向（鼠标在面板 UI 上时不生效）----
        if (kb.leftShiftKey.isPressed && !InputHandler.IsPointerOverPanel())
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            if (mouseDelta.sqrMagnitude > 0f)
            {
                _yaw += mouseDelta.x * Config.rotateSensitivity;
                _pitch = Mathf.Clamp(_pitch - mouseDelta.y * Config.rotateSensitivity,
                    Config.pitchMin, Config.pitchMax);
            }
        }

        // ---- 3. 滚轮缩放（鼠标在面板 UI 上时不生效；上滚拉近、下滚拉远）----
        if (!InputHandler.IsPointerOverPanel())
        {
            Vector2 scroll = mouse.scroll.ReadValue();
            if (!Mathf.Approximately(scroll.y, 0f))
            {
                // scroll.y 为像素量（滚轮一档 ≈ 120；触控板为连续小量）→ 按档换算：
                // 每档 zoomSpeed 世界单位（不乘 deltaTime——滚轮是离散事件量，非持续速度）。
                // 方向：上滚（scroll.y>0）= 拉近（距离减小）、下滚 = 拉远（常规直觉口径）
                _distance = Mathf.Clamp(_distance - (scroll.y / 120f) * Config.zoomSpeed,
                    Config.distanceMin, Config.distanceMax);
            }
        }

        ApplyTransform();
    }

    /// <summary>焦点限制：棋盘六边形范围（世界距离半径）+ 可配置边距内（XZ 平面圆约束）</summary>
    private Vector3 ClampFocusToBoard(Vector3 focus)
    {
        // 棋盘六边形半径 → 世界距离：外接半径 = (radius + 0.5) * 行宽的一半…此处用近似外接圆
        //（initialRadius 圈的六边形棋盘，角落最远点 ≈ (radius + 0.5) * hexSize * √3/2 * …）；
        // 取「棋盘对角格世界距离的最大值」为半径最稳：直接量已有格子
        float radius = GetBoardWorldRadius();
        Vector2 xz = new Vector2(focus.x, focus.z);
        if (xz.magnitude > radius)
            xz = xz.normalized * radius;
        return new Vector3(xz.x, focus.y, xz.y);
    }

    /// <summary>棋盘世界半径（缓存 + 格数校验失效）：所有格子到中心的最大水平距离 + 边距。
    /// 棋盘格数变化（战争之城开局扩展 / 扩展石 / 删除石）时缓存自动失效重算——
    /// 焦点可移动范围始终与当前棋盘一致，不停留在扩展前的旧半径。
    /// 棋盘未初始化（无格子）时不缓存——避免脚本执行顺序导致缓存到 0 把焦点锁死在原点</summary>
    private float _boardRadiusCache = -1f;
    private int _boardRadiusCacheCount = -1;
    private float GetBoardWorldRadius()
    {
        var controller = ChessBoardController.Instance;
        var model = controller?.Model;
        if (model == null || model.Coords == null || model.Coords.Count == 0)
            return float.MaxValue;   // 棋盘未就绪：暂不限制（棋盘建好后下一帧即有正确缓存）

        if (_boardRadiusCache >= 0f && _boardRadiusCacheCount == model.CoordCount)
            return _boardRadiusCache;   // 格数未变：缓存有效

        float maxDist = 0f;
        foreach (var coord in model.Coords)
        {
            Vector3 world = controller.GetCellWorldPosition(coord);
            float dist = new Vector2(world.x, world.z).magnitude;
            if (dist > maxDist) maxDist = dist;
        }
        _boardRadiusCache = maxDist + Config.focusBoardMargin;
        _boardRadiusCacheCount = model.CoordCount;
        return _boardRadiusCache;
    }

    /// <summary>按轨道参数写相机 transform（位置 = 焦点 + 球坐标偏移；注视焦点）</summary>
    private void ApplyTransform()
    {
        if (targetCamera == null) return;
        float yawRad = _yaw * Mathf.Deg2Rad;
        float pitchRad = _pitch * Mathf.Deg2Rad;

        // 球坐标：pitch=0 水平、90 正上方；offset 指向「相机相对焦点」的方向
        Vector3 dir = new Vector3(
            Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(pitchRad) * Mathf.Cos(yawRad));

        targetCamera.transform.position = _focus + dir * _distance;
        targetCamera.transform.LookAt(_focus);
    }
}
