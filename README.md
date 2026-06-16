# Nexus 游戏引擎 - 项目说明文档

> 跨平台 C++ 游戏引擎，采用 C++ 引擎核心 + C# WPF 编辑器架构，当前支持 Windows 平台，计划扩展至 Linux。

---

## 目录

- [1. 项目概览](#1-项目概览)
- [2. 架构设计](#2-架构设计)
- [3. 解决方案结构](#3-解决方案结构)
- [4. Engine - 引擎核心](#4-engine---引擎核心)
  - [4.1 类型系统](#41-类型系统)
  - [4.2 Generation-based ID 系统](#42-generation-based-id-系统)
  - [4.3 实体组件系统](#43-实体组件系统)
  - [4.4 数学库封装](#44-数学库封装)
  - [4.5 脚本系统](#45-脚本系统)
  - [4.6 构建优化配置](#46-构建优化配置)
- [5. EngineDLL - C ABI 导出层](#5-enginedll---c-abi-导出层)
- [6. EngineTest - 单元测试](#6-enginetest---单元测试)
- [7. NexusEditor - 编辑器](#7-nexuseditor---编辑器)
  - [7.1 MVVM 架构](#71-mvvm-架构)
  - [7.2 P/Invoke 互操作](#72-pinvoke-互操作)
  - [7.3 撤销/重做系统](#73-撤销重做系统)
  - [7.4 多选编辑](#74-多选编辑)
  - [7.5 项目模板系统](#75-项目模板系统)
  - [7.6 脚本热加载](#76-脚本热加载)
  - [7.7 VS COM 自动化](#77-vs-com-自动化)
- [8. 技术栈总览](#8-技术栈总览)
- [9. 项目文件树](#9-项目文件树)
- [10. 构建与运行](#10-构建与运行)
- [11. 后续开发路线](#11-开发路线图)

---

## 1. 项目概览

| 属性 | 说明 |
|------|------|
| **项目名称** | Nexus |
| **项目类型** | 跨平台 C++ 游戏引擎 |
| **仓库** | GitHub / Gitee 双平台同步 |
| **平台支持** | Windows（主要）、Linux（计划） |
| **渲染后端** | Direct3D 12、Vulkan（计划） |
| **构建工具** | Visual Studio 2026（v145 工具链） |
| **C++ 标准** | C++20 |
| **C# 运行时** | .NET 10.0 |
| **开源许可** | 见 LICENSE.txt |

引擎核心采用数据导向设计（DOD），以 SoA 布局存储组件数据，通过 Generation-based ID 管理实体生命周期。编辑器基于 WPF + MVVM 架构，通过 C ABI + P/Invoke 实现跨语言互操作。

---

## 2. 架构设计

```
┌──────────────────────────────────────────────────────────┐
│                     NexusEditor (C# WPF)                  │
│              MVVM · DataContract 序列化 · 撤销/重做        │
└────────────────────────┬─────────────────────────────────┘
                         │ P/Invoke ([DllImport])
                         v
┌──────────────────────────────────────────────────────────┐
│                    EngineDLL.dll (C++ 动态库)              │
│                  extern "C" __declspec(dllexport)          │
└────────────────────────┬─────────────────────────────────┘
                         │ 静态链接
                         v
┌──────────────────────────────────────────────────────────┐
│                    Engine.lib (C++ 静态库)                 │
│      实体管理 · Transform(SoA) · 脚本注册 · 数学库          │
└──────────────────────────────────────────────────────────┘
```

**关键设计决策：**

- **C ABI 桥接层**：EngineDLL 使用 `extern "C"` 导出，避免 C++ name mangling，C# 端通过 `[DllImport]` 直接调用
- **数据导向设计**：Transform 组件采用 SoA 布局，批量操作时缓存命中率最大化
- **编辑器/引擎双模式**：用户项目可编译为独立 exe 或编辑器挂载的 dll
- **Generation-based ID**：32 位 ID 拆分为 22 位索引 + 10 位代数，防止悬空引用

---

## 3. 解决方案结构

**Nexus.slnx**（Visual Studio 2026+ 新格式）包含 4 个子项目：

| 项目 | 类型 | 语言 | 输出 | 说明 |
|------|------|------|------|------|
| **Engine** | StaticLibrary | C++20 | `Engine.lib` | 引擎核心逻辑 |
| **EngineDLL** | DynamicLibrary | C++20 | `EngineDLL.dll` | C ABI 导出层 |
| **EngineTest** | Application | C++20 | `EngineTest.exe` | 单元测试 |
| **NexusEditor** | WinExe | C# (.NET 10) | `NexusEditor.exe` | WPF 编辑器 |

所有项目仅配置 x64 平台，输出目录统一为 `x64/`。

---

## 4. Engine - 引擎核心

### 4.1 类型系统

`Common/PrimitiveTypes.h` 定义精确宽度类型别名，避免平台依赖：

```cpp
namespace nexus {
    using u64 = uint64_t;  using u32 = uint32_t;
    using u16 = uint16_t;  using u8  = uint8_t;
    using s64 = int64_t;   using s32 = int32_t;
    using s16 = int16_t;   using s8  = int8_t;
    using f32 = float;

    constexpr u32 u32_invalid_id{ 0xffff'ffffui32 };  // 无效 ID 哨兵值
}
```

### 4.2 Generation-based ID 系统

`Common/Id.h` 实现了引擎最核心的基础设施：

```
32-bit ID 布局:
┌──────────────┬──────────────────────┐
│  10 bits     │      22 bits         │
│  generation  │       index          │
└──────────────┴──────────────────────┘
```

- **索引**：实体在数组中的位置，销毁后进入 `free_ids` 队列等待复用
- **代数**：每次销毁时递增，复用时检测代数不匹配即可判定"悬空引用"
- **阈值机制**：`min_deleted_elements = 1024`，空闲列表超过阈值才启用复用

Debug 模式下 ID 被强化为独立结构体（`DEFINE_TYPED_ID` 宏），编译期防止类型混淆：

```cpp
#if _DEBUG
    #define DEFINE_TYPED_ID(name) \
        struct name { id::id_type _id; /* 显式构造/比较运算符 */ };
#else
    #define DEFINE_TYPED_ID(name) using name = id::id_type;
#endif
```

### 4.3 实体组件系统

`Components/Entity.h/cpp` 管理实体生命周期：

```
全局存储:
  generations[]  →  代数数组（索引 →代数号）
  transforms[]   →  所有 Transform 组件
  scripts[]      →  所有 Script 组件
  free_ids       →  待复用 ID 队列
```

- `create(entity_info)` — 从 free_ids 取 ID 或追加新 ID，初始化组件
- `remove(entity_id)` — 递增代数号，将 ID 加入 free_ids，移除组件
- `is_alive(entity_id)` — 比较当前代数与存储代数

**Transform 组件**（`Components/Transform.h/cpp`）采用 SoA 布局：

```cpp
// 独立数组存储，而非每个实体一个结构体
utl::vector<math::v4> rotations;   // 四元数
utl::vector<math::v3> positions;   // 位置
utl::vector<math::v3> scales;      // 缩放
```

这使批量变换操作时数据连续访问，缓存命中率最大化。

### 4.4 数学库封装

`Utilities/MathTypes.h` 基于 DirectXMath 封装，预留跨平台切换能力：

```cpp
namespace nexus::math {
    using v2   = DirectX::XMFLOAT2;
    using v3   = DirectX::XMFLOAT3;
    using v4   = DirectX::XMFLOAT4;
    using v4a  = DirectX::XMFLOAT4A;    // 16 字节对齐
    using m4x4 = DirectX::XMFLOAT4X4;
    using m4x4a = DirectX::XMFLOAT4X4A; // 16 字节对齐

    constexpr f32 pi{ 3.141592653589793238462643383279502884197169f };
}
```

跨平台时可无缝切换为 GLM 或其他数学库，只需修改类型别名。

### 4.5 脚本系统

`Components/Script.h/cpp` 实现基于静态注册的脚本工厂：

```cpp
// 自动注册宏 — 静态变量在 main() 之前初始化
#define REGISTER_SCRIPT(TYPE)                                          \
    class TYPE##_registrar {                                           \
        static const u8 _reg;                                          \
    };                                                                 \
    const u8 TYPE##_registrar::_reg =                                  \
        nexus::script::register_script(                               \
            typeid(TYPE).hash_code(),                                  \
            [](game_entity::entity e) -> entity_script* {              \
                return new TYPE(e);                                    \
            }                                                          \
        );
```

运行时通过 `typeid` 哈希查找注册表，创建对应脚本实例。

### 4.6 构建优化配置

Engine 静态库针对性能做了极致优化：

| 配置项 | Debug | Release |
|--------|-------|---------|
| 异常处理 | 禁用 | 禁用 |
| 缓冲区安全检查 | 禁用 | 禁用 |
| 浮点模型 | Fast | Fast |
| 调用约定 | FastCall | FastCall |
| 多处理器编译 | 启用 | 启用 |
| CFG (控制流保护) | — | 禁用 |
| 并行代码生成 | — | 启用 |
| RTTI | 禁用 | 禁用 |

---

## 5. EngineDLL - C ABI 导出层

将引擎核心的 C++ API 封装为 C 风格导出函数，供 C# 编辑器通过 P/Invoke 调用。

**核心导出函数：**

```cpp
#define EDITOR_INTERFACE extern "C" __declspec(dllexport)

EDITOR_API id::id_type CreateGameEntity(game_entity_descriptor* e);
EDITOR_API void        RemoveGameEntity(id::id_type id);
// ... 更多实体/组件操作
```

**数据转换：**
- 编辑器侧使用 Euler 角（直观），引擎侧使用四元数（高性能）
- `XMQuaternionRotationRollPitchYawFromVector` 做 Euler → Quaternion 转换

**DLL 入口**（`dllmain.cpp`）：
- `#pragma comment(lib, "engine.lib")` 静态链接引擎核心
- Debug 模式启用 CRT 内存泄漏检测

---

## 6. EngineTest - 单元测试

自建轻量测试框架：

```cpp
class Test {
public:
    virtual bool initialize() = 0;
    virtual void run() = 0;
    virtual void shutdown() = 0;
};
```

**实体组件压力测试**（`TestEnlityComponents.h`）：
- 循环 10,000 次随机创建/删除实体
- 每步验证 `is_alive()` 断言（代数验证机制）
- 确保 Generation-based ID 在高频操作下的正确性

---

## 7. NexusEditor - 编辑器

### 7.1 MVVM 架构

严格遵循 Model-View-ViewModel 模式：

| 层 | 组件 | 说明 |
|----|------|------|
| **Model** | `GameEntity`, `Component`, `Transform` | 数据模型 + `[DataContract]` 序列化 |
| **ViewModel** | `MSEntity`, `MSComponent`, `MSTransform` | 多选聚合 ViewModel |
| **View** | `WorldEditorView`, `GameEntityView`, etc. | XAML 声明式 UI |
| **Command** | `RelayCommand<T>` | `ICommand` 模式实现 |
| **Base** | `ViewModelBase` | `INotifyPropertyChanged` + 循环引用序列化 |

**数据模板自动路由**：`DataTemplate` 根据 ViewModel 类型自动选择对应视图（如 `MSTransform` → `TransformView`）。

### 7.2 P/Invoke 互操作

`DllWrapper/EngineAPI.cs` 封装引擎 DLL 调用：

```csharp
// C# 侧声明
[DllImport("EngineDLL.dll", CharSet = CharSet.Unicode)]
private static extern uint CreateGameEntity(ref GameEntityDescriptor desc);

// 实体状态同步
// IsActive = true  → EngineAPI.CreateGameEntity()
// IsActive = false → EngineAPI.RemoveGameEntity()
```

### 7.3 撤销/重做系统

`Utilities/UndoRedo.cs` 支持两种操作记录模式：

1. **Action 委托模式**：`new UndoRedoAction(name, redoAction, undoAction)`
2. **反射模式**：`new UndoRedoAction(target, property, oldValue, newValue)` — 用反射设置属性值

维护 `_undoList` / `_redoList` 双端队列，UI 绑定 `UndoRedoView` 显示操作历史。快捷键 `Ctrl+Z` 撤销、`Ctrl+Y` 重做。

### 7.4 多选编辑

`MSGameEntity` / `MSTransform` 聚合多个选中对象的属性：

- 混合值用 `null` 表示（如多个实体位置不同时显示为空）
- `GetMixedValue<T>(List<T>, Func<T, float>)` 工具方法判断值是否一致
- `bool?` 类型的 `IsActive`、`float?` 类型的 `PosX/Y/Z` 等

### 7.5 项目模板系统

4 种内置项目模板：

| 模板 | ProjectType | 说明 |
|------|-------------|------|
| `EmptyProject` | 空项目 | 含 MSVC 解决方案/项目模板文件 |
| `FirstProject` | 第一人称 | 预设场景数据 |
| `ThirdPersonProject` | 第三人称 | 预设场景数据 |
| `TopDownPorject` | 俯视角 | 预设场景数据 |

每个模板包含 `template.xml`（元数据）、`Icon.png`、`Screenshot.png`、`project.nexus`（预设数据）。

**空项目模板额外包含** MSVC 构建 templates：
- `MSVCSolution` — `.sln` 模板（`{0}` 项目名、`{1}` 项目 GUID、`{2}` 方案 GUID）
- `MSVCProject` — `.vcxproj` 模板（4 种构建配置，含 `USE_WITH_EDITOR` 宏）

用户项目构建配置：

| 配置 | 类型 | 说明 |
|------|------|------|
| Debug | Application (.exe) | 独立调试 |
| DebugEditor | DynamicLibrary (.dll) | 编辑器挂载调试 |
| Release | Application (.exe) | 发布 |
| ReleaseEditor | DynamicLibrary (.dll) | 编辑器挂载发布 |

### 7.6 脚本热加载

```
用户新建脚本 (.h/.cpp)
    │
    v
编辑器生成模板代码 (REGISTER_SCRIPT 宏)
    │
    v
VS 编译为 DLL (DebugEditor / ReleaseEditor 配置)
    │
    v
EngineDLL 运行时加载 → 注册到 script_registry
```

`NewScriptDialog` 生成包含 `REGISTER_SCRIPT` 宏的模板代码，VS COM 自动化将新文件添加到解决方案。

### 7.7 VS COM 自动化

`GameDev/VisualStudio.cs` 通过 `EnvDTE80.DTE2` COM 接口控制 Visual Studio：

- `OpenVisualStudio(solutionPath)` — 通过 ROT（Running Object Table）查找或新建 VS 实例
- `AddFilesToSolution()` — 自动添加新建脚本文件到解决方案
- `CloseVisualStudio()` — 关闭 VS 实例
- 支持 VS 2026（ProgID: `VisualStudio.DTE.18.0`）

## 8. 技术栈总览

| 层面 | 技术选型 |
|------|----------|
| **引擎语言** | C++20 (MSVC v145) |
| **编辑器语言** | C# (.NET 10.0) |
| **构建系统** | MSBuild + Visual Studio 2026 |
| **解决方案格式** | `.slnx` (VS 2026+ XML 格式) |
| **数学库** | DirectXMath (Windows)，可切换为 GLM |
| **渲染后端** | Direct3D 12、Vulkan（计划） |
| **编辑器 UI** | WPF (.NET) |
| **跨语言互操作** | C ABI (`extern "C"`) + P/Invoke (`[DllImport]`) |
| **序列化** | `DataContractSerializer` (XML)，项目文件扩展名 `.nexus` |
| **IDE 集成** | EnvDTE / EnvDTE80 COM 自动化 |
| **架构模式** | ECS (引擎) + MVVM (编辑器) |
| **数据布局** | SoA (Structure of Arrays) |
| **实体管理** | Generation-based ID (22-bit 索引 + 10-bit 代数) |
| **脚本注册** | 静态全局变量 + typeid 哈希注册表 |
| **平台** | Windows x64 (当前)，Linux (计划) |

---

## 9. 项目文件树

```
Nexus/
├── Nexus.slnx                              # 解决方案文件
├── LICENSE.txt                              # 开源许可
├── README.md                                # 项目说明
│
├── Engine/                                  # 引擎核心静态库 (C++)
│   ├── Engine.vcxproj
│   ├── Common/
│   │   ├── CommonHanders.h                  # 公共头文件聚合入口
│   │   ├── Id.h                             # Generation-based ID 系统
│   │   └── PrimitiveTypes.h                 # 基础类型别名
│   ├── Components/
│   │   ├── ComponentsCommon.h               # 组件公共头文件
│   │   ├── Entity.h / Entity.cpp            # 实体生命周期管理
│   │   ├── Script.h / Script.cpp            # 脚本组件 + 注册表
│   │   └── Transform.h / Transform.cpp      # Transform 组件 (SoA)
│   ├── EngineAPI/
│   │   ├── GameEntity.h                     # 实体公开 API
│   │   ├── ScriptComponent.h                # 脚本组件公开接口
│   │   └── TransformComponent.h             # Transform 公开接口
│   ├── Utilities/
│   │   ├── MathTypes.h                      # DirectXMath 封装
│   │   └── Utilities.h                      # 工具函数 (errase_unordered 等)
│   └── Core/
│       └── Main.cpp                         # 占位入口
│
├── EngineDLL/                               # C ABI 导出层 (C++ 动态库)
│   ├── EngineDLL.vcxproj
│   ├── dllmain.cpp                          # DLL 入口 + 内存泄漏检测
│   └── EngineAPI.cpp                        # C ABI 导出函数
│
├── EngineTest/                              # 单元测试 (C++)
│   ├── EngineTest.vcxproj
│   ├── Test.h                               # 测试框架抽象基类
│   ├── TestEnlityComponents.h               # 实体组件压力测试
│   └── Main.cpp                             # 测试入口
│
├── NexusEditor/                             # 编辑器 (C# / WPF)
│   ├── NexusEditor.csproj                   # .NET 10.0 项目文件
│   ├── App.xaml / App.xaml.cs               # 应用程序入口
│   ├── MainWindow.xaml / .cs                # 主窗口
│   ├── EnginePathDialog.xaml / .cs          # 引擎路径配置对话框
│   ├── AssemblyInfo.cs
│   ├── Common/
│   │   ├── ViewModelBase.cs                 # MVVM ViewModel 基类
│   │   ├── RelayCommand.cs                  # ICommand 模式实现
│   │   └── Transform.cs                     # Transform + MSTransform
│   ├── Components/
│   │   ├── Component.cs                     # 组件抽象基类 + MSComponent
│   │   └── GameEntity.cs                    # 实体模型 + MSGameEntity
│   ├── Dictionaries/
│   │   ├── EditorColors.xaml                # 深色主题色彩定义
│   │   └── ControlTemplates.xaml / .cs      # 控件模板/样式
│   ├── DllWrapper/
│   │   └── EngineAPI.cs                     # P/Invoke 引擎 API 封装
│   ├── Editors/WorldEditor/
│   │   ├── WorldEditorView.xaml / .cs       # 世界编辑器主视图
│   │   ├── ProjectLayoutView.xaml / .cs     # 场景层级视图
│   │   ├── GameEntityView.xaml / .cs        # 实体属性面板
│   │   ├── TransformView.xaml / .cs         # Transform 组件 UI
│   │   └── ComponentView.xaml / .cs         # 通用组件容器
│   ├── GameDev/
│   │   ├── VisualStudio.cs                  # VS COM 自动化控制
│   │   └── NewScriptDialog.xaml / .cs       # 新建脚本对话框
│   ├── GameProject/
│   │   ├── Project.cs                       # 项目数据模型 + 序列化
│   │   ├── Scene.cs                         # 场景数据模型
│   │   ├── NewProject.cs                    # 新建项目逻辑 + 模板
│   │   ├── OpenProject.cs                   # 最近项目管理
│   │   ├── ProjectBrowserDialog.xaml / .cs  # 项目浏览器
│   │   ├── NewProjectView.xaml / .cs        # 新建项目视图
│   │   └── OpenProjectView.xaml / .cs       # 打开项目视图
│   ├── ProjectTemplates/
│   │   ├── EmptyProject/                    # 空项目模板
│   │   ├── FirstProject/                    # 第一人称模板
│   │   ├── ThirdPersonProject/              # 第三人称模板
│   │   └── TopDownPorject/                  # 俯视角模板
│   ├── Themes/
│   │   └── Generic.xaml                     # 主题入口
│   └── Utilities/
│       ├── Serializer.cs                    # XML 序列化工具
│       ├── Logger.cs / LoggerView.xaml/.cs  # 日志系统
│       ├── UndoRedo.cs / UndoRedoView.xaml/.cs # 撤销/重做
│       ├── Utilities.cs                     # 通用工具函数
│       └── Controls/
│           ├── NumberBox.cs                 # 数值输入控件 (拖拽改值)
│           ├── ScalarBox.cs                 # 标量输入控件
│           └── VectorBox.cs                 # 向量输入控件
│
└── x64/                                     # 构建输出目录
    ├── Engine.lib                           # 引擎静态库
    ├── EngineDLL.dll                        # 引擎动态库
    ├── EngineTest.exe                       # 测试可执行文件
    ├── NexusEditor.exe                      # 编辑器可执行文件
    └── *.pdb / *.json                       # 调试符号和配置
```

---

## 10. 构建与运行

### 环境要求

- **IDE**：Visual Studio 2026（v145 工具链）
- **SDK**：Windows SDK、.NET 10.0 SDK
- **平台**：Windows x64

### 构建步骤

1. 克隆仓库后用 VS 2026 打开 `Nexus.slnx`
2. 选择 `Debug` 或 `Release` 配置，平台 `x64`
3. 构建解决方案（`Ctrl+Shift+B`）
4. 输出在 `x64/` 目录下

### 运行编辑器

1. 启动 `NexusEditor.exe`
2. 首次启动需配置引擎路径（指向 `Engine/EngineAPI` 目录存在的根路径）
3. 在项目浏览器中新建或打开项目

---

## 11. 后续开发路线

- [ ] **渲染系统**：完善 Direct3D 12 渲染管线，实现 Vulkan 后端
- [ ] **Linux 支持**：引擎核心跨平台编译，编辑器使用 Qt 或 Avalonia 重写
- [ ] **更多组件**：Mesh、Light、Camera、Physics 等组件
- [ ] **资源管理**：资源加载、缓存、热重载系统
- [ ] **场景编辑**：3D 视口、Gizmo、拾取系统
- [ ] **序列化升级**：从 XML 迁移到二进制格式，提升加载性能
- [ ] **脚本调试**：断点调试、变量检查
- [ ] **物理引擎**：集成物理模拟
