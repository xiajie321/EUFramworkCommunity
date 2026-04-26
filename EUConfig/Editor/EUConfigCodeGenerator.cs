#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scriban;
using Scriban.Runtime;
using UnityEditor;
using UnityEngine;

namespace EUFramework.Extension.EUConfig.Editor
{
    /// <summary>
    /// EUConfig 代码生成器
    /// 扫描 Editor/Templates 下所有 .sbn 文件，读取伴生 .json，用 Scriban 渲染后写入目标目录
    /// 支持 outputDirectory（输出目录，相对于项目根，不填则默认写到 Script/Generated）
    /// 支持 namespaceVariables（从 asmdef 读取 rootNamespace）、stringVariables（直接字符串值）
    /// 支持 requiredAssemblies 自动更新 EUConfig.asmdef 引用（可选）
    /// 支持 ensureAsmdef 按需创建 .asmdef（可选）
    /// </summary>
    public static class EUConfigCodeGenerator
    {
        // ── JSON 数据模型 ────────────────────────────────────────────────────────

        [System.Serializable]
        private class StringPair
        {
            public string key;
            public string value;
        }

        [System.Serializable]
        private class SidecarConfig
        {
            public string outputDirectory;
            public List<string> requiredAssemblies = new List<string>();
            public List<EnsureAsmdefEntry> ensureAsmdef = new List<EnsureAsmdefEntry>();
            public List<StringPair> namespaceVariables = new List<StringPair>();
            public List<StringPair> stringVariables = new List<StringPair>();

            public Dictionary<string, string> GetNamespaceVariables() => ToDictionary(namespaceVariables);
            public Dictionary<string, string> GetStringVariables() => ToDictionary(stringVariables);

            private static Dictionary<string, string> ToDictionary(List<StringPair> pairs)
            {
                var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var pair in pairs ?? new List<StringPair>())
                {
                    if (!string.IsNullOrEmpty(pair?.key))
                        result[pair.key] = pair.value;
                }
                return result;
            }
        }

        [System.Serializable]
        private class EnsureAsmdefEntry
        {
            public string directory;
            public string name;
            public List<string> references = new List<string>();
            public bool autoReferenced = true;
        }

        [System.Serializable]
        private class AsmdefVersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }

        [System.Serializable]
        private class AsmdefData
        {
            public string name;
            public string rootNamespace;
            public List<string> references = new List<string>();
            public List<string> includePlatforms = new List<string>();
            public List<string> excludePlatforms = new List<string>();
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public List<string> precompiledReferences = new List<string>();
            public bool autoReferenced = true;
            public List<string> defineConstraints = new List<string>();
            public List<AsmdefVersionDefine> versionDefines = new List<AsmdefVersionDefine>();
            public bool noEngineReferences;
        }

        // ── 菜单入口 ──────────────────────────────────────────────────────────────

        [MenuItem("EUFramework/生成/EUConfig 代码生成")]
        public static void GenerateFromMenu() => GenerateAll();

        public static void GenerateAll()
        {
            string templatesPath = EUConfigKitPathHelper.GetTemplatesPath();
            string generatedPath = EUConfigKitPathHelper.GetGeneratedPath();

            if (!Directory.Exists(templatesPath))
            {
                Debug.LogError($"[EUConfig] 模板目录不存在: {templatesPath}");
                return;
            }

            if (!Directory.Exists(generatedPath))
                Directory.CreateDirectory(generatedPath);

            var requiredAssemblies = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var sbnPath in Directory.GetFiles(templatesPath, "*.sbn", SearchOption.AllDirectories))
                GenerateFromTemplate(sbnPath, generatedPath, requiredAssemblies);

            if (requiredAssemblies.Count > 0)
                UpdateAsmdefReferences(requiredAssemblies);

            AssetDatabase.Refresh();
            Debug.Log("[EUConfig] 代码生成完成");
        }

        private static void GenerateFromTemplate(string sbnFullPath, string generatedDir, HashSet<string> requiredAssemblies)
        {
            string templateContent = File.ReadAllText(sbnFullPath, System.Text.Encoding.UTF8);
            var scriptObject = new ScriptObject();

            string outputDir = generatedDir;

            string jsonPath = Path.ChangeExtension(sbnFullPath, ".json");
            if (File.Exists(jsonPath))
            {
                string jsonText = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var config = JsonUtility.FromJson<SidecarConfig>(jsonText) ?? new SidecarConfig();

                if (!string.IsNullOrEmpty(config.outputDirectory))
                {
                    string dataPath    = Application.dataPath.Replace("\\", "/");
                    string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);
                    outputDir = Path.GetFullPath(projectRoot + config.outputDirectory.Replace("\\", "/").TrimEnd('/'))
                                    .Replace("\\", "/");
                    if (!Directory.Exists(outputDir))
                        Directory.CreateDirectory(outputDir);
                }

                foreach (var asm in config.requiredAssemblies ?? Enumerable.Empty<string>())
                    requiredAssemblies.Add(asm);

                EnsureAsmdefFiles(config.ensureAsmdef);

                foreach (var kv in config.GetNamespaceVariables())
                {
                    string ns = GetAssemblyRootNamespace(kv.Value);
                    scriptObject.Add(kv.Key, string.IsNullOrEmpty(ns) ? kv.Value : ns);
                }

                foreach (var kv in config.GetStringVariables())
                    scriptObject.Add(kv.Key, kv.Value);
            }

            var template = Template.Parse(templateContent);
            if (template.HasErrors)
            {
                Debug.LogError($"[EUConfig] 模板解析错误 {sbnFullPath}:\n{template.Messages}");
                return;
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);
            string result = template.Render(context);

            string baseName = Path.GetFileNameWithoutExtension(sbnFullPath);
            if (!baseName.EndsWith(".Generated", System.StringComparison.OrdinalIgnoreCase))
                baseName += ".Generated";

            File.WriteAllText(
                Path.Combine(outputDir, baseName + ".cs").Replace("\\", "/"),
                result, System.Text.Encoding.UTF8);

            Debug.Log($"[EUConfig] 已生成: {baseName}.cs → {outputDir}/");
        }

        // ── ensureAsmdef：按需创建 .asmdef ──────────────────────────────────────

        /// <summary>
        /// 对每一条 ensureAsmdef 条目，检查目标目录是否已有对应 .asmdef；不存在则自动创建。
        /// </summary>
        private static void EnsureAsmdefFiles(List<EnsureAsmdefEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            string dataPath    = Application.dataPath.Replace("\\", "/");
            string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.directory) || string.IsNullOrEmpty(entry.name)) continue;

                string assetDir   = entry.directory.Replace("\\", "/").TrimEnd('/');
                string fullDir    = Path.GetFullPath(projectRoot + assetDir).Replace("\\", "/");
                string asmdefPath = Path.Combine(fullDir, entry.name + ".asmdef").Replace("\\", "/");

                if (File.Exists(asmdefPath)) continue;

                if (!System.IO.Directory.Exists(fullDir))
                    System.IO.Directory.CreateDirectory(fullDir);

                var asmdef = new AsmdefData
                {
                    name = entry.name,
                    rootNamespace = "",
                    references = entry.references ?? new List<string>(),
                    autoReferenced = entry.autoReferenced
                };

                WriteAsmdef(asmdefPath, asmdef);
                Debug.Log($"[EUConfig] 已自动创建 {entry.name}.asmdef → {assetDir}/");
            }
        }

        // ── asmdef 引用更新 ───────────────────────────────────────────────────────

        private static void UpdateAsmdefReferences(HashSet<string> requiredAssemblies)
        {
            string asmdefAssetPath = null;
            foreach (var guid in AssetDatabase.FindAssets("EUConfig t:asmdef"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p) == "EUConfig.asmdef") { asmdefAssetPath = p; break; }
            }

            if (string.IsNullOrEmpty(asmdefAssetPath))
            {
                Debug.LogWarning("[EUConfig] 未找到 EUConfig.asmdef，跳过引用更新");
                return;
            }

            string fullPath = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(Application.dataPath), asmdefAssetPath));
            var asmdef = ReadAsmdef(fullPath);

            var refs = asmdef.references ?? new List<string>();
            var existingNames = new HashSet<string>(
                refs, System.StringComparer.OrdinalIgnoreCase);

            bool changed = false;
            foreach (var asm in requiredAssemblies)
            {
                string name = asm.StartsWith("GUID:", System.StringComparison.OrdinalIgnoreCase)
                    ? ResolveGuidToAssemblyName(asm.Substring(5))
                    : asm;
                if (!string.IsNullOrEmpty(name) && existingNames.Add(name))
                {
                    refs.Add(name);
                    changed = true;
                }
            }

            if (!changed) return;

            asmdef.references = refs;
            WriteAsmdef(fullPath, asmdef);
            Debug.Log("[EUConfig] EUConfig.asmdef 引用已更新");
        }

        // ── 工具方法 ──────────────────────────────────────────────────────────────

        private static string ResolveGuidToAssemblyName(string guid)
        {
            try
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) return string.Empty;
                string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath));
                if (!File.Exists(fullPath)) return string.Empty;
                return ReadAsmdef(fullPath).name ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string GetAssemblyRootNamespace(string assemblyName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{assemblyName} t:asmdef"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(p).Equals(assemblyName, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), p));
                    return ReadAsmdef(fullPath).rootNamespace ?? string.Empty;
                }
                catch { }
            }
            return string.Empty;
        }

        private static AsmdefData ReadAsmdef(string fullPath)
        {
            if (!File.Exists(fullPath))
                return new AsmdefData();

            return JsonUtility.FromJson<AsmdefData>(
                File.ReadAllText(fullPath, System.Text.Encoding.UTF8)) ?? new AsmdefData();
        }

        private static void WriteAsmdef(string fullPath, AsmdefData asmdef)
        {
            File.WriteAllText(fullPath, JsonUtility.ToJson(asmdef, true), System.Text.Encoding.UTF8);
        }
    }
}
#endif
