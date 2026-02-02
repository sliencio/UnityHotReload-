using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Utils.HotReload;

namespace Editor
{
    /// <summary>
    /// 热重载窗口 - 支持拖拽脚本、配置方法调用和参数
    /// </summary>
    public class HotReloadWindow : EditorWindow
    {
        private static bool isReloadPending = false;
        private HotReloadConfig config;
        private ScrollView scriptListView;
        private Label statusLabel;

        [MenuItem("Tools/C# Hot Reload Window")]
        public static void ShowWindow()
        {
            HotReloadWindow window = GetWindow<HotReloadWindow>();
            window.titleContent = new GUIContent("C#热重载");
            window.minSize = new Vector2(650, 600);
        }

        private void OnEnable()
        {
            LoadOrCreateConfig();
        }

        private void LoadOrCreateConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:HotReloadConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<HotReloadConfig>(path);
            }
            
            if (config == null)
            {
                config = CreateInstance<HotReloadConfig>();
                AssetDatabase.CreateAsset(config, "Assets/Editor/HotReload/HotReloadConfig.asset");
                AssetDatabase.SaveAssets();
            }
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            // 标题
            Label titleLabel = new Label("🔥 C#热重载系统");
            titleLabel.style.fontSize = 18;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            root.Add(titleLabel);

            // 状态区域
            CreateStatusSection(root);

            // 主要操作按钮
            CreateMainButtons(root);

            CreateSeparator(root);

            // 脚本列表区域
            CreateScriptListSection(root);
        }

        private void CreateStatusSection(VisualElement root)
        {
            VisualElement statusBox = new VisualElement();
            statusBox.style.borderTopWidth = 1;
            statusBox.style.borderBottomWidth = 1;
            statusBox.style.borderLeftWidth = 1;
            statusBox.style.borderRightWidth = 1;
            statusBox.style.borderTopColor = Color.gray;
            statusBox.style.borderBottomColor = Color.gray;
            statusBox.style.borderLeftColor = Color.gray;
            statusBox.style.borderRightColor = Color.gray;
            statusBox.style.borderTopLeftRadius = 5;
            statusBox.style.borderTopRightRadius = 5;
            statusBox.style.borderBottomLeftRadius = 5;
            statusBox.style.borderBottomRightRadius = 5;
            statusBox.style.paddingTop = 10;
            statusBox.style.paddingBottom = 10;
            statusBox.style.paddingLeft = 10;
            statusBox.style.paddingRight = 10;
            statusBox.style.marginBottom = 10;

            statusLabel = new Label("就绪");
            statusLabel.style.fontSize = 12;
            statusBox.Add(statusLabel);

            root.Add(statusBox);
        }

        private void CreateMainButtons(VisualElement root)
        {
            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginBottom = 10;

            Button reloadButton = new Button(() => RecompileAndReload());
            reloadButton.text = "🔄 重新编译并重载";
            reloadButton.style.flexGrow = 1;
            reloadButton.style.height = 35;
            reloadButton.style.marginRight = 5;
            buttonRow.Add(reloadButton);

            root.Add(buttonRow);
        }

        private void CreateScriptListSection(VisualElement root)
        {
            Label sectionTitle = new Label("热重载脚本列表");
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 10;
            root.Add(sectionTitle);

            // 拖拽区域提示
            Label dragHint = new Label("💡 拖拽MonoScript或GameObject到下方区域");
            dragHint.style.fontSize = 11;
            dragHint.style.marginBottom = 5;
            root.Add(dragHint);

            // 拖拽区域
            VisualElement dropArea = new VisualElement();
            dropArea.style.minHeight = 50;
            dropArea.style.borderTopWidth = 2;
            dropArea.style.borderBottomWidth = 2;
            dropArea.style.borderLeftWidth = 2;
            dropArea.style.borderRightWidth = 2;
            dropArea.style.borderTopColor = Color.gray;
            dropArea.style.borderBottomColor = Color.gray;
            dropArea.style.borderLeftColor = Color.gray;
            dropArea.style.borderRightColor = Color.gray;
            dropArea.style.borderTopLeftRadius = 5;
            dropArea.style.borderTopRightRadius = 5;
            dropArea.style.borderBottomLeftRadius = 5;
            dropArea.style.borderBottomRightRadius = 5;
            dropArea.style.marginBottom = 10;
            dropArea.style.justifyContent = Justify.Center;
            dropArea.style.alignItems = Align.Center;

            Label dropLabel = new Label("拖拽脚本到这里");
            dropLabel.style.fontSize = 12;
            dropArea.Add(dropLabel);

            // 注册拖拽事件
            dropArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            dropArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                HandleDrop();
                evt.StopPropagation();
            });

            root.Add(dropArea);

            // 脚本列表
            scriptListView = new ScrollView();
            scriptListView.style.flexGrow = 1;
            scriptListView.style.borderTopWidth = 1;
            scriptListView.style.borderBottomWidth = 1;
            scriptListView.style.borderLeftWidth = 1;
            scriptListView.style.borderRightWidth = 1;
            scriptListView.style.borderTopColor = Color.gray;
            scriptListView.style.borderBottomColor = Color.gray;
            scriptListView.style.borderLeftColor = Color.gray;
            scriptListView.style.borderRightColor = Color.gray;
            scriptListView.style.borderTopLeftRadius = 5;
            scriptListView.style.borderTopRightRadius = 5;
            scriptListView.style.borderBottomLeftRadius = 5;
            scriptListView.style.borderBottomRightRadius = 5;
            scriptListView.style.paddingTop = 5;
            scriptListView.style.paddingBottom = 5;
            root.Add(scriptListView);

            RefreshScriptList();
        }

        private void HandleDrop()
        {
            foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
            {
                if (obj is MonoScript monoScript)
                {
                    AddScript(monoScript, null);
                }
                else if (obj is GameObject go)
                {
                    MonoBehaviour[] components = go.GetComponents<MonoBehaviour>();
                    foreach (MonoBehaviour component in components)
                    {
                        if (component != null)
                        {
                            MonoScript script = MonoScript.FromMonoBehaviour(component);
                            AddScript(script, go);
                        }
                    }
                }
            }
            
            SaveConfig();
            RefreshScriptList();
        }

        private void AddScript(MonoScript monoScript, GameObject targetGO)
        {
            if (monoScript == null) return;

            Type type = monoScript.GetClass();
            if (type == null) return;

            // 检查是否已存在
            string typeName = type.FullName;
            if (config.scripts.Any(s => s.typeName == typeName))
            {
                Debug.LogWarning($"脚本 {typeName} 已存在");
                return;
            }

            ScriptEntry entry = new ScriptEntry
            {
                scriptName = type.Name,
                scriptPath = AssetDatabase.GetAssetPath(monoScript),
                monoScript = monoScript,
                targetGameObject = targetGO,
                typeName = typeName,
                isMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(type),
                isFoldout = true
            };

            config.scripts.Add(entry);
            Debug.Log($"添加脚本: {entry.scriptName}");
        }

        private void RefreshScriptList()
        {
            scriptListView.Clear();

            if (config.scripts.Count == 0)
            {
                Label emptyLabel = new Label("📭 暂无脚本，请拖拽添加");
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                scriptListView.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < config.scripts.Count; i++)
            {
                int index = i;
                ScriptEntry entry = config.scripts[i];
                VisualElement scriptItem = CreateScriptItem(entry, index);
                scriptListView.Add(scriptItem);
            }
        }

        private VisualElement CreateScriptItem(ScriptEntry entry, int index)
        {
            VisualElement container = new VisualElement();
            container.style.marginBottom = 10;
            container.style.marginLeft = 5;
            container.style.marginRight = 5;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = Color.gray;
            container.style.borderBottomColor = Color.gray;
            container.style.borderLeftColor = Color.gray;
            container.style.borderRightColor = Color.gray;
            container.style.borderTopLeftRadius = 5;
            container.style.borderTopRightRadius = 5;
            container.style.borderBottomLeftRadius = 5;
            container.style.borderBottomRightRadius = 5;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;

            // 头部行
            VisualElement headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 5;

            // 折叠按钮
            Button foldoutButton = new Button(() =>
            {
                entry.isFoldout = !entry.isFoldout;
                SaveConfig();
                RefreshScriptList();
            });
            foldoutButton.text = entry.isFoldout ? "▼" : "▶";
            foldoutButton.style.width = 25;
            foldoutButton.style.height = 25;
            foldoutButton.style.marginRight = 5;
            headerRow.Add(foldoutButton);

            // 脚本名称
            Label nameLabel = new Label($"{entry.scriptName}");
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            headerRow.Add(nameLabel);

            // 类型标签
            Label typeLabel = new Label(entry.isMonoBehaviour ? "MonoBehaviour" : "C# Class");
            typeLabel.style.fontSize = 10;
            typeLabel.style.paddingTop = 3;
            typeLabel.style.paddingBottom = 3;
            typeLabel.style.paddingLeft = 6;
            typeLabel.style.paddingRight = 6;
            typeLabel.style.borderTopLeftRadius = 3;
            typeLabel.style.borderTopRightRadius = 3;
            typeLabel.style.borderBottomLeftRadius = 3;
            typeLabel.style.borderBottomRightRadius = 3;
            typeLabel.style.marginRight = 5;
            typeLabel.style.borderTopWidth = 1;
            typeLabel.style.borderBottomWidth = 1;
            typeLabel.style.borderLeftWidth = 1;
            typeLabel.style.borderRightWidth = 1;
            typeLabel.style.borderTopColor = Color.gray;
            typeLabel.style.borderBottomColor = Color.gray;
            typeLabel.style.borderLeftColor = Color.gray;
            typeLabel.style.borderRightColor = Color.gray;
            headerRow.Add(typeLabel);

            // 删除按钮
            Button deleteButton = new Button(() =>
            {
                config.scripts.RemoveAt(index);
                SaveConfig();
                RefreshScriptList();
            });
            deleteButton.text = "✖";
            deleteButton.style.width = 25;
            deleteButton.style.height = 25;
            headerRow.Add(deleteButton);

            container.Add(headerRow);

            // 展开内容
            if (entry.isFoldout)
            {
                VisualElement contentArea = new VisualElement();
                contentArea.style.paddingLeft = 30;
                contentArea.style.marginTop = 5;

                // GameObject引用（如果是MonoBehaviour）
                if (entry.isMonoBehaviour && entry.targetGameObject != null)
                {
                    Label goLabel = new Label($"GameObject: {entry.targetGameObject.name}");
                    goLabel.style.fontSize = 11;
                    goLabel.style.marginBottom = 5;
                    contentArea.Add(goLabel);
                }

                // 方法调用列表
                Label methodsTitle = new Label("方法调用:");
                methodsTitle.style.fontSize = 11;
                methodsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                methodsTitle.style.marginBottom = 5;
                contentArea.Add(methodsTitle);

                // 现有方法
                for (int i = 0; i < entry.methodCalls.Count; i++)
                {
                    int methodIndex = i;
                    VisualElement methodItem = CreateMethodCallItem(entry, methodIndex);
                    contentArea.Add(methodItem);
                }

                // 添加方法按钮
                Button addMethodButton = new Button(() =>
                {
                    entry.methodCalls.Add(new MethodCall { methodName = "" });
                    SaveConfig();
                    RefreshScriptList();
                });
                addMethodButton.text = "+ 添加方法调用";
                addMethodButton.style.height = 25;
                addMethodButton.style.fontSize = 11;
                addMethodButton.style.marginTop = 5;
                contentArea.Add(addMethodButton);

                container.Add(contentArea);
            }

            return container;
        }

        private VisualElement CreateMethodCallItem(ScriptEntry entry, int methodIndex)
        {
            MethodCall methodCall = entry.methodCalls[methodIndex];
            
            VisualElement methodContainer = new VisualElement();
            methodContainer.style.borderTopWidth = 1;
            methodContainer.style.borderBottomWidth = 1;
            methodContainer.style.borderLeftWidth = 1;
            methodContainer.style.borderRightWidth = 1;
            methodContainer.style.borderTopColor = Color.gray;
            methodContainer.style.borderBottomColor = Color.gray;
            methodContainer.style.borderLeftColor = Color.gray;
            methodContainer.style.borderRightColor = Color.gray;
            methodContainer.style.borderTopLeftRadius = 3;
            methodContainer.style.borderTopRightRadius = 3;
            methodContainer.style.borderBottomLeftRadius = 3;
            methodContainer.style.borderBottomRightRadius = 3;
            methodContainer.style.paddingTop = 8;
            methodContainer.style.paddingBottom = 8;
            methodContainer.style.paddingLeft = 8;
            methodContainer.style.paddingRight = 8;
            methodContainer.style.marginBottom = 5;

            // 方法名和按钮行
            VisualElement methodRow = new VisualElement();
            methodRow.style.flexDirection = FlexDirection.Row;
            methodRow.style.alignItems = Align.Center;
            methodRow.style.marginBottom = 8;

            Label methodLabel = new Label("方法名:");
            methodLabel.style.fontSize = 11;
            methodLabel.style.minWidth = 50;
            methodLabel.style.marginRight = 5;
            methodRow.Add(methodLabel);

            TextField methodNameField = new TextField();
            methodNameField.value = methodCall.methodName;
            methodNameField.style.minWidth = 100;
            methodNameField.style.maxWidth = 200;
            methodNameField.style.marginRight = 5;
            methodNameField.RegisterValueChangedCallback(evt =>
            {
                methodCall.methodName = evt.newValue;
                SaveConfig();
            });
            methodRow.Add(methodNameField);

            // 执行按钮
            Button executeButton = new Button(() => ExecuteMethod(entry, methodCall));
            executeButton.text = "▶ 执行";
            executeButton.style.width = 70;
            executeButton.style.height = 25;
            executeButton.style.marginRight = 5;
            methodRow.Add(executeButton);

            // 删除方法按钮
            Button deleteMethodButton = new Button(() =>
            {
                entry.methodCalls.RemoveAt(methodIndex);
                SaveConfig();
                RefreshScriptList();
            });
            deleteMethodButton.text = "✖";
            deleteMethodButton.style.width = 25;
            deleteMethodButton.style.height = 25;
            methodRow.Add(deleteMethodButton);

            methodContainer.Add(methodRow);

            // 参数列表
            if (methodCall.parameters.Count > 0)
            {
                Label paramLabel = new Label("参数:");
                paramLabel.style.fontSize = 10;
                paramLabel.style.marginBottom = 3;
                methodContainer.Add(paramLabel);
            }

            for (int i = 0; i < methodCall.parameters.Count; i++)
            {
                int paramIndex = i;
                VisualElement paramItem = CreateParameterItem(methodCall, paramIndex);
                methodContainer.Add(paramItem);
            }

            // 添加参数按钮
            Button addParamButton = new Button(() =>
            {
                methodCall.parameters.Add(new MethodParameter 
                { 
                    parameterName = "param",
                    parameterType = ParameterType.String 
                });
                SaveConfig();
                RefreshScriptList();
            });
            addParamButton.text = "+ 添加参数";
            addParamButton.style.height = 22;
            addParamButton.style.fontSize = 10;
            addParamButton.style.marginTop = 5;
            methodContainer.Add(addParamButton);

            return methodContainer;
        }

        private VisualElement CreateParameterItem(MethodCall methodCall, int paramIndex)
        {
            MethodParameter param = methodCall.parameters[paramIndex];
            
            VisualElement paramRow = new VisualElement();
            paramRow.style.flexDirection = FlexDirection.Row;
            paramRow.style.alignItems = Align.Center;
            paramRow.style.marginBottom = 3;
            paramRow.style.marginLeft = 10;
            paramRow.style.width = new StyleLength(new Length(90, LengthUnit.Percent));

            // 参数名
            TextField paramNameField = new TextField();
            paramNameField.value = param.parameterName;
            paramNameField.style.width = 100;
            paramNameField.style.marginRight = 5;
            paramNameField.RegisterValueChangedCallback(evt =>
            {
                param.parameterName = evt.newValue;
                SaveConfig();
            });
            paramRow.Add(paramNameField);

            // 类型选择
            EnumField typeField = new EnumField(param.parameterType);
            typeField.style.width = 90;
            typeField.style.marginRight = 5;
            typeField.RegisterValueChangedCallback(evt =>
            {
                param.parameterType = (ParameterType)evt.newValue;
                SaveConfig();
                RefreshScriptList();
            });
            paramRow.Add(typeField);

            // 值输入（根据类型）
            VisualElement valueField = CreateValueField(param);
            valueField.style.minWidth = 80;
            valueField.style.maxWidth = 150;
            valueField.style.marginRight = 5;
            paramRow.Add(valueField);

            // 删除参数按钮
            Button deleteParamButton = new Button(() =>
            {
                methodCall.parameters.RemoveAt(paramIndex);
                SaveConfig();
                RefreshScriptList();
            });
            deleteParamButton.text = "✖";
            deleteParamButton.style.width = 25;
            deleteParamButton.style.height = 22;
            paramRow.Add(deleteParamButton);

            return paramRow;
        }

        private VisualElement CreateValueField(MethodParameter param)
        {
            switch (param.parameterType)
            {
                case ParameterType.String:
                    TextField stringField = new TextField();
                    stringField.value = param.stringValue;
                    stringField.RegisterValueChangedCallback(evt =>
                    {
                        param.stringValue = evt.newValue;
                        SaveConfig();
                    });
                    return stringField;

                case ParameterType.Int:
                    IntegerField intField = new IntegerField();
                    intField.value = param.intValue;
                    intField.RegisterValueChangedCallback(evt =>
                    {
                        param.intValue = evt.newValue;
                        SaveConfig();
                    });
                    return intField;

                case ParameterType.Float:
                    FloatField floatField = new FloatField();
                    floatField.value = param.floatValue;
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        param.floatValue = evt.newValue;
                        SaveConfig();
                    });
                    return floatField;

                case ParameterType.Bool:
                    Toggle boolField = new Toggle();
                    boolField.value = param.boolValue;
                    boolField.RegisterValueChangedCallback(evt =>
                    {
                        param.boolValue = evt.newValue;
                        SaveConfig();
                    });
                    return boolField;

                case ParameterType.Vector3:
                    Vector3Field vector3Field = new Vector3Field();
                    vector3Field.value = param.vector3Value;
                    vector3Field.RegisterValueChangedCallback(evt =>
                    {
                        param.vector3Value = evt.newValue;
                        SaveConfig();
                    });
                    return vector3Field;

                default:
                    return new Label("Unknown Type");
            }
        }

        private void ExecuteMethod(ScriptEntry entry, MethodCall methodCall)
        {
            if (string.IsNullOrEmpty(methodCall.methodName))
            {
                Debug.LogError("方法名为空！");
                return;
            }

            try
            {
                object instance = null;
                Type actualType = null;

                if (entry.isMonoBehaviour && entry.targetGameObject != null)
                {
                    // 对于MonoBehaviour，从GameObject获取实际的组件实例
                    MonoBehaviour[] components = entry.targetGameObject.GetComponents<MonoBehaviour>();
                    
                    foreach (MonoBehaviour comp in components)
                    {
                        if (comp != null && comp.GetType().Name == entry.scriptName)
                        {
                            instance = comp;
                            actualType = comp.GetType();
                            break;
                        }
                    }
                    
                    if (instance == null)
                    {
                        Debug.LogError($"GameObject上没有找到组件: {entry.scriptName}");
                        return;
                    }
                    
                    // 检查是否需要从热重载的程序集获取新Type
                    Type hotReloadedType = FindHotReloadedType(entry.scriptName);
                    if (hotReloadedType != null && hotReloadedType != actualType)
                    {
                        Debug.LogWarning($"[热重载] 检测到新版本的类型: {entry.scriptName}");
                        Debug.LogWarning($"[热重载] 旧类型程序集: {actualType.Assembly.GetName().Name}");
                        Debug.LogWarning($"[热重载] 新类型程序集: {hotReloadedType.Assembly.GetName().Name}");
                        Debug.LogWarning($"[热重载] MonoBehaviour组件无法在运行时替换！");
                        Debug.LogWarning($"[热重载] 建议: 1) 停止运行 2) 修改代码 3) 重新运行");
                        Debug.LogWarning($"[热重载] 或者: 使用纯C#类而不是MonoBehaviour");
                        
                        // 尝试使用新Type（虽然实例是旧的，但至少可以看到方法列表）
                        actualType = hotReloadedType;
                    }
                }
                else
                {
                    // 非MonoBehaviour类 - 从RoslynHotReload获取或创建
                    instance = RoslynHotReload.Instance.GetInstance<object>(entry.typeName);
                    
                    if (instance == null)
                    {
                        // 自动创建实例
                        Debug.Log($"[RoslynHotReload] 实例不存在，自动创建: {entry.typeName}");
                        instance = RoslynHotReload.Instance.CreateAndRegister<object>(
                            entry.typeName,
                            entry.typeName
                        );
                    }
                    
                    if (instance == null)
                    {
                        Debug.LogError($"无法创建实例: {entry.scriptName}");
                        return;
                    }
                    
                    actualType = instance.GetType();
                }

                // 准备参数
                object[] parameters = new object[methodCall.parameters.Count];
                Type[] paramTypes = new Type[methodCall.parameters.Count];
                
                for (int i = 0; i < methodCall.parameters.Count; i++)
                {
                    MethodParameter param = methodCall.parameters[i];
                    switch (param.parameterType)
                    {
                        case ParameterType.String:
                            parameters[i] = param.stringValue;
                            paramTypes[i] = typeof(string);
                            break;
                        case ParameterType.Int:
                            parameters[i] = param.intValue;
                            paramTypes[i] = typeof(int);
                            break;
                        case ParameterType.Float:
                            parameters[i] = param.floatValue;
                            paramTypes[i] = typeof(float);
                            break;
                        case ParameterType.Bool:
                            parameters[i] = param.boolValue;
                            paramTypes[i] = typeof(bool);
                            break;
                        case ParameterType.Vector3:
                            parameters[i] = param.vector3Value;
                            paramTypes[i] = typeof(Vector3);
                            break;
                    }
                }

                // 使用实例的实际Type查找方法（这是热重载后的新Type）
                MethodInfo method = actualType.GetMethod(methodCall.methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, paramTypes, null);

                if (method == null)
                {
                    method = actualType.GetMethod(methodCall.methodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (method == null)
                {
                    // 列出所有可用的方法帮助用户
                    MethodInfo[] allMethods = actualType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    string availableMethods = string.Join(", ", allMethods.Select(m => m.Name));
                    
                    Debug.LogError($"找不到方法: {methodCall.methodName} (在类型 {actualType.FullName} 中)\n" +
                                   $"可用的方法: {availableMethods}\n" +
                                   $"提示: 如果刚添加了新方法，请先点击'重新编译并重载'按钮");
                    UpdateStatus($"❌ 找不到方法: {methodCall.methodName}");
                    return;
                }

                // 调用方法 - 现在instance和method的Type是匹配的
                object result = method.Invoke(instance, parameters);
                Debug.Log($"✅ 执行成功: {entry.scriptName}.{methodCall.methodName}() => {result}");
                UpdateStatus($"✅ 执行: {methodCall.methodName}()");
            }
            catch (Exception ex)
            {
                Debug.LogError($"执行方法失败: {ex.Message}\n{ex.StackTrace}");
                UpdateStatus($"❌ 执行失败: {ex.Message}");
            }
        }

        private Type FindHotReloadedType(string typeName)
        {
            // 在所有已加载的程序集中查找最新版本的类型
            // 优先查找动态编译的程序集（名称包含GUID的）
            Type foundType = null;
            System.Reflection.Assembly latestAssembly = null;
            
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (type != null)
                    {
                        // 如果是动态编译的程序集（包含GUID），优先使用
                        if (assembly.GetName().Name.Contains("_"))
                        {
                            foundType = type;
                            latestAssembly = assembly;
                        }
                        else if (foundType == null)
                        {
                            foundType = type;
                            latestAssembly = assembly;
                        }
                    }
                }
                catch (Exception)
                {
                    // 跳过无法访问的程序集
                }
            }
            
            return foundType;
        }

        private void RecompileAndReload()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("C#热重载",
                    "热重载只能在运行时使用！\n请先进入Play模式。", "确定");
                UpdateStatus("❌ 错误：不在Play模式");
                return;
            }

            // 检查是否有MonoBehaviour脚本
            bool hasMonoBehaviour = config.scripts.Any(s => s.isMonoBehaviour);
            if (hasMonoBehaviour)
            {
                bool proceed = EditorUtility.DisplayDialog("MonoBehaviour热重载",
                    "检测到MonoBehaviour脚本！\n\n" +
                    "✅ 系统将自动尝试：\n" +
                    "1. 编译新版本的代码\n" +
                    "2. 替换GameObject上的旧组件为新组件\n" +
                    "3. 保留并恢复组件的字段数据\n\n" +
                    "⚠️ 注意事项：\n" +
                    "• 组件的引用关系可能会丢失\n" +
                    "• 复杂的序列化数据可能无法完全恢复\n" +
                    "• 如果遇到问题，建议停止运行后重新启动\n\n" +
                    "💡 最佳实践：\n" +
                    "使用纯C#类（不继承MonoBehaviour）可获得更好的热重载体验\n\n" +
                    "是否继续编译并替换组件？",
                    "继续", "取消");
                
                if (!proceed)
                {
                    UpdateStatus("❌ 用户取消");
                    return;
                }
            }

            // 保存实例数据
            RoslynHotReload.Instance.SaveInstanceData();
            UpdateStatus("💾 已保存数据，正在编译...");
            Debug.Log("[RoslynHotReload] 已保存实例数据，开始编译...");

            // 编译所有已添加的脚本
            bool success = true;
            int compiledCount = 0;
            int monoBehaviourCount = 0;
            
            foreach (var entry in config.scripts)
            {
                if (entry.monoScript == null) continue;
                
                string scriptPath = AssetDatabase.GetAssetPath(entry.monoScript);
                if (string.IsNullOrEmpty(scriptPath)) continue;
                
                Debug.Log($"[RoslynHotReload] 编译脚本: {scriptPath}");
                
                if (entry.isMonoBehaviour)
                {
                    monoBehaviourCount++;
                    Debug.LogWarning($"[RoslynHotReload] ⚠️ {entry.scriptName} 是MonoBehaviour，热重载可能不会生效");
                }
                
                if (RoslynHotReload.Instance.CompileAndReloadScript(scriptPath))
                {
                    compiledCount++;
                }
                else
                {
                    success = false;
                    Debug.LogError($"[RoslynHotReload] 编译失败: {scriptPath}");
                }
            }

            if (success && compiledCount > 0)
            {
                // 尝试替换MonoBehaviour组件
                if (monoBehaviourCount > 0)
                {
                    ReplaceMonoBehaviourComponents();
                }
                
                string message = $"✅ 热重载完成！已编译 {compiledCount} 个脚本";
                if (monoBehaviourCount > 0)
                {
                    message += $"\n✅ 已尝试替换 {monoBehaviourCount} 个MonoBehaviour组件";
                }
                UpdateStatus(message);
                Debug.Log($"[RoslynHotReload] {message}");
            }
            else if (compiledCount == 0)
            {
                UpdateStatus("⚠️ 没有脚本需要编译");
                Debug.LogWarning("[RoslynHotReload] 没有脚本需要编译");
            }
            else
            {
                UpdateStatus("❌ 部分脚本编译失败，请查看Console");
                Debug.LogError("[RoslynHotReload] 部分脚本编译失败");
            }
        }
        private void ReplaceMonoBehaviourComponents()
        {
            Debug.Log("[热重载] 开始替换MonoBehaviour组件...");
            
            foreach (var entry in config.scripts)
            {
                if (!entry.isMonoBehaviour || entry.targetGameObject == null)
                    continue;
                
                try
                {
                    // 查找热重载后的新Type
                    Type newType = FindHotReloadedType(entry.scriptName);
                    if (newType == null)
                    {
                        Debug.LogWarning($"[热重载] 找不到热重载后的类型: {entry.scriptName}");
                        continue;
                    }
                    
                    // 获取GameObject上的旧组件
                    MonoBehaviour oldComponent = null;
                    MonoBehaviour[] components = entry.targetGameObject.GetComponents<MonoBehaviour>();
                    
                    foreach (MonoBehaviour comp in components)
                    {
                        if (comp != null && comp.GetType().Name == entry.scriptName)
                        {
                            oldComponent = comp;
                            break;
                        }
                    }
                    
                    if (oldComponent == null)
                    {
                        Debug.LogWarning($"[热重载] GameObject上找不到组件: {entry.scriptName}");
                        continue;
                    }
                    
                    Type oldType = oldComponent.GetType();
                    
                    // 检查是否真的需要替换（Type是否不同）
                    if (oldType == newType)
                    {
                        Debug.Log($"[热重载] {entry.scriptName} 已是最新版本，无需替换");
                        continue;
                    }
                    
                    Debug.Log($"[热重载] 准备替换组件: {entry.scriptName}");
                    Debug.Log($"[热重载]   旧Type程序集: {oldType.Assembly.GetName().Name}");
                    Debug.Log($"[热重载]   新Type程序集: {newType.Assembly.GetName().Name}");
                    
                    // 保存旧组件的字段值
                    Dictionary<string, object> fieldValues = new Dictionary<string, object>();
                    FieldInfo[] oldFields = oldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    foreach (FieldInfo field in oldFields)
                    {
                        try
                        {
                            fieldValues[field.Name] = field.GetValue(oldComponent);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[热重载] 无法保存字段 {field.Name}: {ex.Message}");
                        }
                    }
                    
                    // 销毁旧组件
                    UnityEngine.Object.DestroyImmediate(oldComponent);
                    
                    // 添加新组件
                    MonoBehaviour newComponent = (MonoBehaviour)entry.targetGameObject.AddComponent(newType);
                    
                    // 恢复字段值
                    FieldInfo[] newFields = newType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    int restoredCount = 0;
                    
                    foreach (FieldInfo newField in newFields)
                    {
                        if (fieldValues.TryGetValue(newField.Name, out object value))
                        {
                            try
                            {
                                if (value == null || newField.FieldType.IsAssignableFrom(value.GetType()))
                                {
                                    newField.SetValue(newComponent, value);
                                    restoredCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[热重载] 无法恢复字段 {newField.Name}: {ex.Message}");
                            }
                        }
                    }
                    
                    Debug.Log($"[热重载] ✅ 成功替换组件: {entry.scriptName} (恢复了 {restoredCount} 个字段)");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[热重载] 替换组件失败 {entry.scriptName}: {ex.Message}\n{ex.StackTrace}");
                }
            }
            
            Debug.Log("[热重载] MonoBehaviour组件替换完成！");
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private void CreateSeparator(VisualElement root)
        {
            VisualElement separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = Color.gray;
            separator.style.marginTop = 10;
            separator.style.marginBottom = 10;
            root.Add(separator);
        }

        private void SaveConfig()
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
    }
}