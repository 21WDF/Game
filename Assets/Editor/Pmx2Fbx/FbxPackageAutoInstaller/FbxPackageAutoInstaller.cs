using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.IO;

namespace PMX2FBX.Editor
{
    /// <summary>
    /// 自动检测并安装 com.autodesk.fbx 依赖包。
    /// 检查状态存储在项目本地的 marker 文件中，每个项目独立记录。
    /// </summary>
    [InitializeOnLoad]
    public static class FbxPackageAutoInstaller
    {
        private const string PackageName = "com.autodesk.fbx";
        private const string MarkerFileName = "PMX2FBX_InstallerChecked.txt";

        private static ListRequest _listRequest;
        private static AddRequest _addRequest;

        private static string MarkerPath => Path.Combine(
            Application.dataPath.Replace("/Assets", ""), MarkerFileName);

        static FbxPackageAutoInstaller()
        {
            if (File.Exists(MarkerPath))
                return;

            EditorApplication.delayCall += CheckPackageInstalled;
        }

        private static void MarkDone()
        {
            try
            {
                File.WriteAllText(MarkerPath, "checked");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PMX2FBX] 无法写入 marker 文件：{e.Message}");
            }
        }

        private static void CheckPackageInstalled()
        {
            EditorApplication.delayCall -= CheckPackageInstalled;
            _listRequest = Client.List(true, false);
            EditorApplication.update += OnListProgress;
        }

        private static void OnListProgress()
        {
            if (_listRequest == null || !_listRequest.IsCompleted)
                return;

            EditorApplication.update -= OnListProgress;

            if (_listRequest.Status == StatusCode.Success)
            {
                bool alreadyInstalled = false;
                foreach (var pkg in _listRequest.Result)
                {
                    if (pkg.name == PackageName)
                    {
                        alreadyInstalled = true;
                        break;
                    }
                }

                if (alreadyInstalled)
                {
                    MarkDone();
                    Debug.Log($"[PMX2FBX] 依赖包 \"{PackageName}\" 已安装，无需处理。");
                }
                else
                {
                    AskUserAndInstall();
                }
            }
            else if (_listRequest.Status >= StatusCode.Failure)
            {
                Debug.LogWarning(
                    $"[PMX2FBX] 检测已安装包列表失败：{_listRequest.Error?.message}\n" +
                    $"将直接尝试安装 \"{PackageName}\"。");
                AskUserAndInstall();
            }

            _listRequest = null;
        }

        private static void AskUserAndInstall()
        {
            bool ok = EditorUtility.DisplayDialog(
                "PMX2FBX - 缺少依赖包",
                $"PMX2FBX 插件需要 \"{PackageName}\" 包，但检测到尚未安装。\n\n是否立即安装？\n安装后 Unity 会自动刷新并编译。",
                "立即安装", "稍后手动安装");

            if (ok)
            {
                Debug.Log($"[PMX2FBX] 用户确认，正在安装 \"{PackageName}\"…");
                InstallPackage();
            }
            else
            {
                MarkDone();
                Debug.Log($"[PMX2FBX] 用户取消。稍后可从菜单手动重置并检测：\nPMX2FBX / 检测并安装依赖包");
            }
        }

        private static void InstallPackage()
        {
            _addRequest = Client.Add(PackageName);
            EditorApplication.update += OnAddProgress;
        }

        private static void OnAddProgress()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
                return;

            EditorApplication.update -= OnAddProgress;

            if (_addRequest.Status == StatusCode.Success)
            {
                var pkg = _addRequest.Result;
                MarkDone();
                Debug.Log($"[PMX2FBX] 依赖包 \"{pkg.name}\" (版本 {pkg.version}) 安装成功！");

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                EditorUtility.DisplayDialog(
                    "PMX2FBX - 依赖安装完成",
                    $"依赖包 \"{pkg.name}\" (版本 {pkg.version}) 已自动安装完成。\n\n现在可以使用 PMX2FBX插件了。",
                    "好的");
            }
            else if (_addRequest.Status >= StatusCode.Failure)
            {
                var errMsg = _addRequest.Error?.message ?? "未知错误";
                Debug.LogError(
                    $"[PMX2FBX] 依赖包 \"{PackageName}\" 自动安装失败：{errMsg}\n" +
                    $"请手动安装：Window → Package Manager → 左上角\"+\" → Add package by name → 输入 \"{PackageName}\"");

                EditorUtility.DisplayDialog(
                    "PMX2FBX - 依赖安装失败",
                    $"自动安装 \"{PackageName}\" 失败：\n{errMsg}\n\n请手动安装：\n" +
                    $"Window → Package Manager → 左上角\"+\" → Add package by name → 输入 \"{PackageName}\"",
                    "好的");

                MarkDone();
            }

            _addRequest = null;
        }

        [MenuItem("PMX2FBX / 检测并安装依赖包 (com.autodesk.fbx)")]
        private static void ManualCheck()
        {
            if (File.Exists(MarkerPath))
                File.Delete(MarkerPath);
            CheckPackageInstalled();
        }
    }
}
