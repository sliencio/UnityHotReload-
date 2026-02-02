# Unity 运行时热重载系统

一个强大的Unity运行时热重载系统，允许你在不停止Play模式的情况下修改和重载C#脚本。

## ✨ 核心特性

- 🔥 **运行时热重载**：在游戏运行时修改代码，无需重启
- 🔄 **MonoBehaviour自动替换**：智能替换GameObject上的组件
- 💾 **状态完整保存**：重载时自动保存并恢复所有字段数据
- 🎯 **可视化方法执行**：直接从编辑器执行方法，支持多种参数类型
- 🛠️ **Roslyn编译器**：使用Microsoft Roslyn进行高性能动态编译
- 📝 **拖放式界面**：简单直观的可视化编辑器

## 📋 系统要求

- Unity 2022.3 或更高版本
- .NET Framework 4.7.1 或更高版本
- NuGetForUnity 包管理器

## 🚀 快速开始

### 1. 安装 NuGetForUnity

通过Unity Package Manager添加：
```
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```

### 2. 安装依赖包

打开 `NuGet > Manage NuGet Packages`，安装：
- `Microsoft.CodeAnalysis.CSharp` (5.0.0)
- `Microsoft.CodeAnalysis.Common` (5.0.0)

其他依赖项会自动安装。

### 3. 导入热重载系统

将以下文件夹复制到你的Unity项目：
- `Assets/Editor/HotReload/`
- `Assets/Scripts/Utils/HotReload/`

## 📖 使用教程

### 打开热重载窗口

菜单：`Tools > Simple Hot Reload > 打开热重载窗口`

### 添加脚本

1. 拖放MonoScript或GameObject到窗口
2. 脚本自动添加到热重载列表
3. 可选：配置需要执行的方法

### 热重载流程

1. **进入Play模式**
2. **修改代码**（添加方法、修改逻辑等）
3. **点击"🔄 重新编译并重载"**
4. **立即生效**！

系统会自动：
- ✅ 编译新代码
- ✅ 替换GameObject上的组件
- ✅ 保留所有字段数据
- ✅ 使新方法立即可用

### 执行方法

1. 在脚本配置中点击"+ 添加方法调用"
2. 输入方法名
3. 添加参数（支持：String, Int, Float, Bool, Vector3）
4. 点击"▶ 执行"

## ⚠️ 重要说明

### 调试限制

**热重载的代码无法使用断点！**

原因：
- 调试器附加到原始程序集
- 动态编译的代码没有调试符号
- 源代码与运行时代码映射关系丢失

**解决方案**：
- 需要调试时：停止Play模式 → 修改代码 → 重新运行
- 快速迭代时：使用热重载

### MonoBehaviour注意事项

- ⚠️ 组件引用可能丢失
- ⚠️ 复杂序列化数据可能无法完全恢复
- 💡 建议：使用纯C#类获得更好的热重载体验

## 🎯 使用场景

### ✅ 适合使用热重载

- 快速调整游戏逻辑
- 测试UI交互
- 调整数值参数
- 快速原型验证
- 迭代开发

### ❌ 不适合使用热重载

- 需要断点调试
- 复杂bug排查
- 性能分析
- 生产环境构建

## 📦 依赖项说明

### 核心依赖

```xml
<package id="Microsoft.CodeAnalysis.CSharp" version="5.0.0" />
<package id="Microsoft.CodeAnalysis.Common" version="5.0.0" />
```

### 自动安装的依赖

- System.Collections.Immutable (9.0.0)
- System.Reflection.Metadata (9.0.0)
- System.Runtime.CompilerServices.Unsafe (6.1.0)
- System.Text.Encoding.CodePages (8.0.0)
- 以及其他必要的依赖项

## 🔧 高级配置

### 自定义编译选项

编辑 `RoslynHotReload.cs` 中的编译选项：

```csharp
compilationOptions = new CSharpCompilationOptions(
    OutputKind.DynamicallyLinkedLibrary,
    optimizationLevel: OptimizationLevel.Debug,
    allowUnsafe: true
);
```

### 添加自定义引用

在 `InitializeReferences()` 方法中添加：

```csharp
references.Add(MetadataReference.CreateFromFile(typeof(YourType).Assembly.Location));
```

## 🐛 已知问题

1. **断点不可用**（设计限制）
2. **组件引用可能丢失**（MonoBehaviour限制）
3. **复杂数据可能无法恢复**（序列化限制）

## 🤝 贡献指南

欢迎提交Pull Request！

### 开发环境

1. Fork本仓库
2. 创建特性分支：`git checkout -b feature/AmazingFeature`
3. 提交更改：`git commit -m 'Add some AmazingFeature'`
4. 推送分支：`git push origin feature/AmazingFeature`
5. 提交Pull Request

## 📄 开源协议

本项目采用 **MIT License** 开源协议。

这意味着你可以：
- ✅ 商业使用
- ✅ 修改代码
- ✅ 分发
- ✅ 私有使用

详见 [LICENSE](LICENSE) 文件。

## 🙏 致谢

- [Microsoft Roslyn](https://github.com/dotnet/roslyn) - 强大的C#编译器
- [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) - Unity的NuGet包管理器

## 📞 获取帮助

- 📧 提交Issue：[GitHub Issues](https://github.com/yourusername/unity-hot-reload/issues)
- 💬 讨论区：[GitHub Discussions](https://github.com/yourusername/unity-hot-reload/discussions)

## 🗺️ 路线图

- [ ] 支持更多参数类型
- [ ] 改进调试体验
- [ ] 添加性能监控
- [ ] 支持Assembly Definition
- [ ] 添加单元测试

## 📊 性能说明

- 编译时间：通常 < 1秒（取决于代码复杂度）
- 内存占用：每个热重载的程序集约 1-5MB
- 建议：不要在一次热重载中包含过多脚本

## 💡 最佳实践

### 1. 代码组织

```csharp
// ✅ 推荐：纯C#类
public class GameLogic
{
    public void UpdateScore(int points) { }
}

// ⚠️ 可用但有限制：MonoBehaviour
public class Player : MonoBehaviour
{
    public void TakeDamage(int damage) { }
}
```

### 2. 字段管理

```csharp
// ✅ 简单类型会自动保存恢复
public int health;
public string playerName;

// ⚠️ 引用类型可能丢失
public GameObject target; // 可能丢失
public Player otherPlayer; // 可能丢失
```

### 3. 方法设计

```csharp
// ✅ 推荐：无状态方法
public int Calculate(int a, int b) 
{
    return a + b;
}

// ✅ 推荐：明确的参数
public void SetPosition(float x, float y, float z) { }
```

## 🎓 教程视频

即将推出...

## 📝 更新日志

### v1.0.0 (2026-02-02)
- 🎉 首次发布
- ✨ 支持运行时热重载
- ✨ MonoBehaviour自动替换
- ✨ 可视化方法执行界面

---

**Star ⭐ 本项目如果它对你有帮助！**