using UnityEngine;

/// <summary>
/// 轨道相机配置（ScriptableObject）—— 相机视角控制全部参数（数据驱动，代码零硬编码）。
/// 对标 GameConfig/MonsoonConfig 范式：Create > Chess > Orbit Camera Config 创建资产后
/// 拖入 OrbitCameraController；未拖入时回退 Resources/OrbitCameraConfig，再回退运行时默认值。
/// 修改数值无需改代码，直接在资产 Inspector 调整。
/// </summary>
[CreateAssetMenu(menuName = "Chess/Orbit Camera Config")]
public class OrbitCameraConfig : ScriptableObject
{
    [Header("平移（WASD 沿相机水平朝向移动焦点）")]
    [Tooltip("焦点平移速度（世界单位/秒）")]
    public float panSpeed = 12f;

    [Tooltip("焦点允许越出棋盘半径的边距（世界单位；焦点被限制在棋盘范围+该边距内）")]
    public float focusBoardMargin = 2f;

    [Header("转向（按住左 Shift + 移动鼠标；水平角不限制）")]
    [Tooltip("转向灵敏度（度/屏幕像素）")]
    public float rotateSensitivity = 0.25f;

    [Tooltip("俯仰角下限（度；不低于水平面）")]
    public float pitchMin = 15f;

    [Tooltip("俯仰角上限（度）")]
    public float pitchMax = 85f;

    [Header("缩放（滚轮改变相机到焦点的距离）")]
    [Tooltip("缩放速度（世界单位/滚轮档；滚轮一档约 120 像素量，触控板连续小量按比例缩放）")]
    public float zoomSpeed = 3f;

    [Tooltip("距离下限（世界单位）")]
    public float distanceMin = 4f;

    [Tooltip("距离上限（世界单位）")]
    public float distanceMax = 30f;
}
