# EUConfig

基于 **Luban** 配置框架的静态访问封装模块。通过代码生成器将 Luban 生成的 `Tables` 类包装为统一的静态入口 `EUConfigKit`，底层资源加载依赖 **EURes（YooAsset）**。

---

## 目录结构

```
EUConfig/
├── Doc/                          本文档
├── Editor/
│   ├── Templates/
│   │   ├── EUConfigKit.Generated.sbn     代码模板（Scriban）
│   │   └── EUConfigKit.Generated.json    模板配置（变量定义 + asmdef 依赖）
│   ├── EUConfigCodeGenerator.cs          代码生成器（菜单入口）
│   ├── EUConfigKitPathHelper.cs          模块路径定位工具
│   └── EUConfig.Editor.asmdef
├── Script/
│   └── Generated/
│       └── EUConfigKit.Generated.cs      自动生成，勿手动修改
└── EUConfig.asmdef
```

---

## 快速上手

### 1. 前置准备

确保以下条件已满足：

- Luban 已生成 `Tables.cs` 及相关代码，输出目录为 `Assets/Script/HotUpdate/Generate/Luban/`
- `Assets/Script/HotUpdate/Generate/` 目录下存在 `HotUpdateGenerated.asmdef`（由代码生成器通过 `ensureAsmdef` 自动创建，若不存在执行一次"EUConfig 代码生成"即可恢复）
- EURes 已初始化（`EUResKit` 可正常使用）

### 2. 配置模板变量

打开 `Editor/Templates/EUConfigKit.Generated.json`，按需调整：

```json
{
    "requiredAssemblies": ["Luban.Runtime", "EURes", "YooAsset", "HotUpdateGenerated"],
    "ensureAsmdef": [
        {
            "directory": "Assets/Script/HotUpdate/Generate",
            "name": "HotUpdateGenerated",
            "references": ["Luban.Runtime"],
            "autoReferenced": true
        }
    ],
    "namespaceVariables": [
        { "key": "eu_config_namespace", "value": "EUConfig" },
        { "key": "eu_res_namespace", "value": "EURes" }
    ],
    "stringVariables": [
        { "key": "tables_class", "value": "Tables" },
        { "key": "config_address", "value": "Assets/EUResources/Remote/Config" }
    ]
}
```

| 字段 | 说明 |
|---|---|
| `tables_class` | Luban 生成的总表类名（默认 `Tables`，对应 Luban 配置中的 `manager`） |
| `config_address` | YooAsset 中配置文件资产的根路径 |
| `eu_config_namespace` | 生成代码的命名空间（取对应 asmdef 的 `rootNamespace`） |
| `eu_res_namespace` | EURes 模块的命名空间 |
| `ensureAsmdef` | 生成时检查指定目录是否存在对应 `.asmdef`，缺失则自动创建 |

### 3. 执行代码生成

菜单栏执行：

```
EUFramework → 生成 → EUConfig 代码生成
```

生成器会：
1. 读取 `Editor/Templates/` 下所有 `.sbn` 模板
2. 从伴生 `.json` 解析变量，用 Scriban 渲染
3. 检查 `ensureAsmdef` 中声明的目录，缺少 `.asmdef` 时自动创建
4. 输出到 `Script/Generated/EUConfigKit.Generated.cs`
5. 自动将 `requiredAssemblies` 中声明的程序集写入 `EUConfig.asmdef` 的 `references`

### 4. 初始化与使用

```csharp
// 游戏启动时初始化（调用一次）
EUConfigKit.Init();

// 获取配置表（T 为 Luban 生成的具体表类型）
var tbShop = EUConfigKit.GetTable<ShopConfig.TbShop>();
var shopData = tbShop.GetOrDefault(shopId);
```

如需在运行时切换资源路径：

```csharp
EUConfigKit.ConfigAddress = "Assets/EUResources/Remote/Config";
EUConfigKit.Init();
```

---

## 工作原理

```
EUConfigKit.Init()
    │
    ├─ 通过反射获取 Tables 构造函数
    ├─ 判断加载器返回类型（JSONNode / ByteBuf）
    │
    ├─ JSON 格式 → LoadJsonSync(file)
    │       └─ EUResKit.GetPackage().LoadAssetSync<TextAsset>(path + ".json")
    │
    └─ Bytes 格式 → LoadByteBufSync(file)
            └─ EUResKit.GetPackage().LoadAssetSync<TextAsset>(path + ".bytes")
```

`GetTable<T>()` 通过反射从 `Tables` 实例读取对应属性，无需在 `EUConfigKit` 中硬编码每张表的字段。

---

## 程序集依赖

```
EUConfig.asmdef
    ├── Luban.Runtime        Luban 运行时（ByteBuf 等）
    ├── EURes                EUResKit.GetPackage()
    ├── YooAsset             ResourcePackage.LoadAssetSync()
    └── HotUpdateGenerated   Tables 及 Luban 生成代码所在程序集
```

> **注意**：`HotUpdateGenerated.asmdef` 位于 `Assets/Script/HotUpdate/Generate/`，覆盖该目录下所有生成代码（包括 `Luban/` 子目录）。Luban 重新生成只清空 `Luban/` 子文件夹内容，不会影响上层的 asmdef 文件。若该文件意外丢失，执行一次"EUConfig 代码生成"即可通过 `ensureAsmdef` 自动恢复。

---

## 扩展新模板

如需生成更多配置相关代码（如配置版本管理、热更新校验等），在 `Editor/Templates/` 下新增 `.sbn` + `.json` 文件对即可，下次执行"EUConfig 代码生成"时会自动处理。

`.json` 规范：

```json
{
    "requiredAssemblies": ["程序集名"],
    "ensureAsmdef": [
        {
            "directory": "Assets/相对路径",
            "name": "程序集名",
            "references": ["依赖程序集名"],
            "autoReferenced": true
        }
    ],
    "namespaceVariables": [
        { "key": "模板变量名", "value": "程序集名（取其 rootNamespace）" }
    ],
    "stringVariables": [
        { "key": "模板变量名", "value": "直接字符串值" }
    ]
}
```

`ensureAsmdef` 中的每一项在生成时会检查 `directory` 目录下是否存在 `{name}.asmdef`，不存在则自动创建。
