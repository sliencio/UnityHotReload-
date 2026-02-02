using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using UnityEngine;

namespace Utils.HotReload
{
    /// <summary>
    /// 使用Roslyn实现的真正运行时热重载
    /// </summary>
    public class RoslynHotReload
    {
        private static RoslynHotReload instance;
        public static RoslynHotReload Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoslynHotReload();
                }
                return instance;
            }
        }

        private Dictionary<string, object> managedInstances = new Dictionary<string, object>();
        private Dictionary<string, Dictionary<string, object>> instanceData = new Dictionary<string, Dictionary<string, object>>();
        private List<MetadataReference> references;
        private CSharpCompilationOptions compilationOptions;

        private RoslynHotReload()
        {
            InitializeReferences();
            compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: true
            );
            Debug.Log("[RoslynHotReload] 初始化完成 - 使用Roslyn编译器");
        }

        private void InitializeReferences()
        {
            references = new List<MetadataReference>();

            try
            {
                // 添加核心.NET引用
                references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location));
                
                // 添加Unity引用
                references.Add(MetadataReference.CreateFromFile(typeof(UnityEngine.Debug).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(UnityEngine.MonoBehaviour).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(UnityEngine.Vector3).Assembly.Location));

                // 添加所有已加载的程序集
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                        {
                            references.Add(MetadataReference.CreateFromFile(assembly.Location));
                        }
                    }
                    catch (Exception)
                    {
                        // 跳过无法引用的程序集
                    }
                }

                Debug.Log($"[RoslynHotReload] 已加载 {references.Count} 个程序集引用");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynHotReload] 初始化引用失败: {ex.Message}");
            }
        }

        public void RegisterInstance(string key, object instance)
        {
            if (instance == null)
            {
                Debug.LogError($"[RoslynHotReload] 无法注册空实例: {key}");
                return;
            }

            managedInstances[key] = instance;
            Debug.Log($"[RoslynHotReload] 已注册实例: {key}");
        }

        public T GetInstance<T>(string key) where T : class
        {
            if (managedInstances.TryGetValue(key, out object instance))
            {
                return instance as T;
            }
            return null;
        }

        public T CreateAndRegister<T>(string key, string typeName) where T : class
        {
            try
            {
                Type type = FindType(typeName);
                if (type == null)
                {
                    Debug.LogError($"[RoslynHotReload] 找不到类型: {typeName}");
                    return null;
                }

                object instance = Activator.CreateInstance(type);
                RegisterInstance(key, instance);
                
                return instance as T;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynHotReload] 创建实例失败: {ex.Message}");
                return null;
            }
        }

        public void SaveInstanceData()
        {
            instanceData.Clear();
            
            foreach (var kvp in managedInstances)
            {
                string key = kvp.Key;
                object instance = kvp.Value;
                
                if (instance == null) continue;

                Dictionary<string, object> fieldData = new Dictionary<string, object>();
                Type type = instance.GetType();
                
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        fieldData[field.Name] = field.GetValue(instance);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RoslynHotReload] 无法保存字段 {field.Name}: {ex.Message}");
                    }
                }
                
                instanceData[key] = fieldData;
                Debug.Log($"[RoslynHotReload] 已保存实例数据: {key} ({fieldData.Count} 个字段)");
            }
        }

        /// <summary>
        /// 编译并重载指定的脚本文件
        /// </summary>
        public bool CompileAndReloadScript(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"[RoslynHotReload] 文件不存在: {filePath}");
                    return false;
                }

                string sourceCode = File.ReadAllText(filePath);
                return CompileAndReloadSource(sourceCode, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynHotReload] 读取文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 编译并重载源代码
        /// </summary>
        public bool CompileAndReloadSource(string sourceCode, string assemblyName = "HotReloadedAssembly")
        {
            try
            {
                // 解析源代码
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

                // 创建唯一的程序集名称
                string uniqueAssemblyName = $"{Path.GetFileNameWithoutExtension(assemblyName)}_{Guid.NewGuid():N}";

                // 创建编译
                CSharpCompilation compilation = CSharpCompilation.Create(
                    uniqueAssemblyName,
                    new[] { syntaxTree },
                    references,
                    compilationOptions
                );

                // 编译到内存
                using (var ms = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(ms);

                    if (!result.Success)
                    {
                        // 编译失败
                        StringBuilder errorMessage = new StringBuilder();
                        errorMessage.AppendLine($"[RoslynHotReload] 编译失败:");

                        IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                            diagnostic.IsWarningAsError ||
                            diagnostic.Severity == DiagnosticSeverity.Error);

                        foreach (Diagnostic diagnostic in failures)
                        {
                            errorMessage.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
                            
                            if (diagnostic.Location.IsInSource)
                            {
                                var lineSpan = diagnostic.Location.GetLineSpan();
                                errorMessage.AppendLine($"    at line {lineSpan.StartLinePosition.Line + 1}");
                            }
                        }

                        Debug.LogError(errorMessage.ToString());
                        return false;
                    }

                    // 加载编译后的程序集
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());
                    
                    Debug.Log($"[RoslynHotReload] 编译成功: {assemblyName}");

                    // 重载所有相关实例
                    ReloadInstancesFromAssembly(assembly);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynHotReload] 编译异常: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private void ReloadInstancesFromAssembly(Assembly assembly)
        {
            Debug.Log("[RoslynHotReload] 开始重载实例...");
            
            List<string> keys = new List<string>(managedInstances.Keys);
            Type[] newTypes = assembly.GetTypes();
            
            foreach (string key in keys)
            {
                object oldInstance = managedInstances[key];
                if (oldInstance == null) continue;

                try
                {
                    Type oldType = oldInstance.GetType();
                    string typeName = oldType.Name; // 使用简单名称匹配
                    
                    // 在新程序集中查找对应的类型
                    Type newType = newTypes.FirstOrDefault(t => t.Name == typeName);
                    
                    if (newType == null)
                    {
                        Debug.LogWarning($"[RoslynHotReload] 在新程序集中找不到类型: {typeName}");
                        continue;
                    }

                    // 创建新实例
                    object newInstance = Activator.CreateInstance(newType);
                    
                    // 恢复字段数据
                    if (instanceData.TryGetValue(key, out Dictionary<string, object> fieldData))
                    {
                        RestoreFieldData(newInstance, fieldData);
                    }
                    
                    // 更新引用
                    managedInstances[key] = newInstance;
                    
                    Debug.Log($"[RoslynHotReload] 已重载实例: {key} -> {newType.FullName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RoslynHotReload] 重载实例失败 {key}: {ex.Message}");
                }
            }
            
            Debug.Log("[RoslynHotReload] 重载完成！");
        }

        private void RestoreFieldData(object instance, Dictionary<string, object> fieldData)
        {
            Type type = instance.GetType();
            
            foreach (var kvp in fieldData)
            {
                string fieldName = kvp.Key;
                object value = kvp.Value;
                
                try
                {
                    FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && (value == null || field.FieldType.IsAssignableFrom(value.GetType())))
                    {
                        field.SetValue(instance, value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RoslynHotReload] 无法恢复字段 {fieldName}: {ex.Message}");
                }
            }
        }

        private Type FindType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        public string GetInstancesInfo()
        {
            if (managedInstances.Count == 0)
            {
                return "没有已注册的实例";
            }

            string info = $"已注册 {managedInstances.Count} 个实例:\n";
            foreach (var kvp in managedInstances)
            {
                string key = kvp.Key;
                object instance = kvp.Value;
                string typeName = instance?.GetType().FullName ?? "null";
                info += $"  - {key}: {typeName}\n";
            }
            return info;
        }

        public void Clear()
        {
            managedInstances.Clear();
            instanceData.Clear();
            Debug.Log("[RoslynHotReload] 已清除所有实例");
        }
    }
}