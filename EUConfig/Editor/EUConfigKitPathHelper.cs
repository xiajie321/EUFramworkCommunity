#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EUFramework.Extension.EUConfig.Editor
{
    /// <summary>
    /// EUConfig 路径管理工具
    /// 通过查找 EUConfig.asmdef 动态定位模块根目录
    /// </summary>
    public static class EUConfigKitPathHelper
    {
        private static string _moduleRoot;

        public static string GetModuleRoot()
        {
            if (!string.IsNullOrEmpty(_moduleRoot))
                return _moduleRoot;

            string[] guids = AssetDatabase.FindAssets("EUConfig t:asmdef");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "EUConfig.asmdef")
                {
                    _moduleRoot = Path.GetDirectoryName(path).Replace("\\", "/");
                    break;
                }
            }

            if (string.IsNullOrEmpty(_moduleRoot))
                Debug.LogError("[EUConfig] 无法找到 EUConfig.asmdef，请确保模块结构完整");

            return _moduleRoot;
        }

        public static string GetEditorPath()
            => Path.Combine(GetModuleRoot(), "Editor").Replace("\\", "/");

        public static string GetTemplatesPath()
            => Path.Combine(GetEditorPath(), "Templates").Replace("\\", "/");

        public static string GetScriptPath()
            => Path.Combine(GetModuleRoot(), "Script").Replace("\\", "/");

        public static string GetGeneratedPath()
            => Path.Combine(GetScriptPath(), "Generated").Replace("\\", "/");

        public static void ClearCache()
        {
            _moduleRoot = null;
        }
    }
}
#endif
