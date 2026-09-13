using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录 / 注册面板（大厅切换账号用）—— 输入账号 + 密码；登录 / 注册按钮；错误提示文本。
/// 认证全部走 PlayerProfileService（转发 IAuthProvider；联机换远端实现后本 UI 零改动）。
/// 账号不存在 → 提示先注册；账号已存在 → 提示直接登录（本地场景从简，不区分密码错误/账号不存在）。
/// 登录成功后由 PlayerProfileService.OnProfileChanged 驱动大厅 UI 刷新（金币/账号/解锁状态）。
///
/// Editor 搭建：挂在 LoginPanel 根节点上；
///   panelRoot（自身）/ accountInput / passwordInput / loginButton / registerButton / closeButton / messageText 由 Inspector 拖入。
/// </summary>
public class UI_LoginPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField accountInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI messageText;

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        SetMessage(string.Empty);
        if (accountInput != null) accountInput.text = string.Empty;
        if (passwordInput != null) passwordInput.text = string.Empty;
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnLoginClicked()
    {
        string account = accountInput != null ? accountInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            SetMessage("请输入账号与密码");
            return;
        }

        if (PlayerProfileService.Login(account, password))
        {
            SetMessage("登录成功");
            Hide();
        }
        else
        {
            SetMessage(PlayerProfileService.GetProfile(account) == null
                ? "账号不存在，请先注册"
                : "密码错误");
        }
    }

    private void OnRegisterClicked()
    {
        string account = accountInput != null ? accountInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            SetMessage("请输入账号与密码");
            return;
        }

        if (PlayerProfileService.Register(account, password))
        {
            SetMessage("注册成功，已自动登录");
            Hide();
        }
        else
        {
            SetMessage("账号已存在，请直接登录");
        }
    }

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }
}
