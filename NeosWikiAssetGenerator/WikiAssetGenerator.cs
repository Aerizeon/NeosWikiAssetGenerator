using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Display;
using FrooxEngine.LogiX.Input;
using FrooxEngine.LogiX.Math;
using FrooxEngine.LogiX.Operators;
using FrooxEngine.LogiX.Physics;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.LogiX.Transform;
using FrooxEngine.LogiX.Utility;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NeosWikiAssetGenerator
{
    [Category("Epsilion")]
    public class WikiAssetGenerator : Component, ICustomInspector
    {
        private static List<Type> TypeBlacklist;
        private static Dictionary<Type, Type> GenericTypeInstantiationMap = new Dictionary<Type, Type>();
        private static Dictionary<string, Type> LogixOverloadPreferences;
        private StringBuilder componentErrors = new StringBuilder();
        public readonly Sync<string> ComponentName;
        public readonly Sync<int> CaptureIndex;
        VerticalLayout lastContent = null;
        Text componentProgress = null;
        protected override void OnAttach()
        {
            base.OnAttach();
            BuildTypeBlacklist();
            BuildTypeMap();
            BuildOverloadPreferences();
            GenerateInfoBoxes();
            GenerateWikiIndexPages();
        }

        public void BuildInspectorUI(UIBuilder ui)
        {
            WorkerInspector.BuildInspectorUI((Worker)this, ui);
            ui.Button("Generate named Component", (b, e) => { StartTask(async () => { await GenerateComponentVisuals(ComponentName.Value); }); });
            ui.Button("Generate Component and LogiX visuals", (b, e) => { StartTask(async () => { await GenerateComponentVisuals(); }); });
            ui.Button("Generate Wiki InfoBoxes and tables", (b, e) => { GenerateInfoBoxes(); });
            ui.Button("Generate Wiki Index pages", (b, e) => { GenerateWikiIndexPages(); });
            ui.Button("Capture Image", (b, e) => { CaptureImage(); });
            componentProgress = ui.Text("", true, Alignment.MiddleCenter, false);
        }

        private void GenerateInfoBoxes()
        {
            /*
             * Generate large tables for MediaWiki, since we don't have access to for loops in the
             * templating system yet (Needs an upgrade!)
             */
            StringBuilder sb = new StringBuilder();
            #region Components Field Table
            sb.AppendLine("{| class=\"wikitable\" style=\"font-size:10pt;\"");
            sb.AppendLine("! colspan=\"3\" style=\"background: lightblue; font-size:10pt;\" | Fields");
            sb.AppendLine("|- style=\"font-size:10pt; text-align:center; font-weight:bold;\"");
            sb.AppendLine("| Name");
            sb.AppendLine("| Type");
            sb.AppendLine("| Description");
            sb.AppendLine("|-");

            sb.AppendLine("| <code>persistent</code>");
            sb.AppendLine("| '''[[:Category:Types:Bool| Bool]]'''");
            sb.AppendLine("| {{Template:ComponentDescriptions:Persistent}}");
            sb.AppendLine("|-");
            sb.AppendLine("| <code>UpdateOrder</code>");
            sb.AppendLine("| '''[[:Category:Types:Int| Int]]'''");
            sb.AppendLine("| {{Template:ComponentDescriptions:UpdateOrder}}");
            sb.AppendLine("|-");
            sb.AppendLine("| <code>Enabled</code>");
            sb.AppendLine("| '''[[:Category:Types:Bool| Bool]]'''");
            sb.AppendLine("| {{Template:ComponentDescriptions:Enabled}}");
            for (int ix = 0; ix <= 175; ix++)
            {
                sb.AppendLine("{{#if:{{{" + (ix * 3 + 1) + "|}}}|");
                sb.AppendLine("{{!}}-");
                sb.AppendLine("{{!}}<code>{{{" + (ix * 3 + 1) + "}}}</code>");
                sb.AppendLine("{{!}}'''[[:Category:Types:{{{" + (ix * 3 + 2) + "}}}{{!}}{{#if:{{{TypeString" + ix + "|}}}|{{{TypeString" + ix + "}}}|{{{" + (ix * 3 + 2) + "}}}}}]]'''");
                sb.AppendLine("{{!}}{{{" + (ix * 3 + 3) + "}}}");
                sb.AppendLine("}}");
                sb.AppendLine("|-");
            }
            sb.AppendLine("|}");
            System.IO.File.WriteAllText("D:\\NeosWiki\\Table_ComponentFields.txt", sb.ToString());
            #endregion
            sb.Clear();

            #region Components Trigger Table
            sb.AppendLine("{| class=\"wikitable\" style=\"font-size:10pt;\"");
            sb.AppendLine("! colspan=\"3\" style=\"background: lightblue; font-size:10pt;\" | Triggers");
            sb.AppendLine("|- style=\"font-size:10pt; text-align:center; font-weight:bold;\"");
            sb.AppendLine("| Name");
            sb.AppendLine("| Arguments");
            sb.AppendLine("| Description");
            for (int ix = 0; ix <= 32; ix++)
            {
                sb.AppendLine("{{#if:{{{" + (ix * 3 + 1) + "|}}}|");
                sb.AppendLine("{{!}}-");
                sb.AppendLine("{{!}}<code>{{{" + (ix * 3 + 1) + "}}}</code>");
                sb.AppendLine("{{!}}{{{" + (ix * 3 + 2) + "}}}");
                sb.AppendLine("{{!}}{{{" + (ix * 3 + 3) + "}}}");
                sb.AppendLine("}}");
                sb.AppendLine("|-");
            }
            sb.AppendLine("|}");
            System.IO.File.WriteAllText("D:\\NeosWiki\\Table_ComponentTriggers.txt", sb.ToString());
            #endregion
            sb.Clear();
            #region Logix InfoBox
            sb.AppendLine("{| class=\"wikitable\" style=\"float: right; margin-left: 1em; clear: right;\"");
            sb.AppendLine("! colspan=\"3\" style=\"background: lightblue; width: 200px; font-size:10pt;\" | {{{Name}}}");
            sb.AppendLine("|-");
            sb.AppendLine("| colspan=\"3\" style=\"text-align:center; font-size:7pt;\" | {{{Image}}}<br />");
            sb.AppendLine("|-");
            sb.AppendLine("| colspan=\"3\" style=\"background: lightblue; text-align:center; font-size:9pt;\" | <b>Inputs</b>");
            for (int ix = 0; ix <= 32; ix++)
            {
                sb.AppendLine("{{#if:{{{Input" + ix + "Type|}}}|");
                sb.AppendLine("{{!}}-");
                sb.AppendLine("! style=\"background:{{{{{Input" + ix + "Type}}}-color}};\"{{!}} &nbsp;");
                //sb.AppendLine("{{!}}'''[[:Category:Types:{{{Input" + ix + "Type}}}| {{{Input" + ix + "Type}}}}}]]'''");
                sb.AppendLine("{{!}}'''[[:Category:Types:{{{Input" + ix + "Type}}}{{!}}{{#if:{{{Input" + ix + "TypeString|}}}|{{{Input" + ix + "TypeString}}}|{{{Input" + ix + "Type}}}}}]]'''");
                sb.AppendLine("{{!}}{{{Input" + ix + "Name}}}");
                sb.AppendLine("}}");
                sb.AppendLine("|-");
            }
            sb.AppendLine("| colspan=\"3\" style=\"background: lightblue; text-align:center; font-size:9pt;\" | <b>Outputs</b>");
            for (int ix = 0; ix <= 32; ix++)
            {
                sb.AppendLine("{{#if:{{{Output" + ix + "Type|}}}|");
                sb.AppendLine("{{!}}-");
                sb.AppendLine("! style=\"background:{{{{{Output" + ix + "Type}}}-color}};\"{{!}} &nbsp;");
                sb.AppendLine("{{!}}'''[[:Category:Types:{{{Output" + ix + "Type}}}{{!}}{{#if:{{{Output" + ix + "TypeString|}}}|{{{Output" + ix + "TypeString}}}|{{{Output" + ix + "Type}}}}}]]'''");
                sb.AppendLine("{{!}}{{{Output" + ix + "Name}}}");
                sb.AppendLine("}}");
                sb.AppendLine("|-");
            }
            sb.AppendLine("|}");
            System.IO.File.WriteAllText("D:\\NeosWiki\\Infobox_Logix_Node.txt", sb.ToString());
            #endregion
        }
        private void GenerateWikiIndexPages()
        {
            IEnumerable<Type> frooxEngineTypes = AppDomain.CurrentDomain.GetAssemblies().Where(T => T.GetName().Name == "FrooxEngine").SelectMany(T => T.GetTypes());
            IEnumerable<Type> frooxEngineComponentTypes = frooxEngineTypes.Where(T => T.Namespace != null && T.Namespace.StartsWith("FrooxEngine")).Where(T => T.IsSubclassOf(typeof(Component)) || T.IsSubclassOf(typeof(LogixNode)));

            ComponentPathNode rootNode = new ComponentPathNode();
            List<string> componentPaths = new List<string>(3000);


            foreach (Type componentType in frooxEngineComponentTypes)
            {
                //Read the component's category attribute, so we know where it goes
                Category componentCategory = componentType.GetCustomAttribute<Category>();
                //Add this to uncategorized
                if (componentCategory == null)
                    componentCategory = new Category("Uncategorized");
                foreach (string path in componentCategory.Paths)
                {
                    //This category just has a bunch of CastClass nodes in it. Not very useful for a logix search
                    //especially since they're not visible.
                    if (path.Contains("Hidden"))
                        continue;
                    ComponentPathNode currentNode = rootNode;
                    int depth = 0;
                    foreach (string pathNode in (path + "/" + componentType.Name).Split('/'))
                    {
                        ComponentPathNode _currentNode = currentNode.Children.SingleOrDefault(N => N.Name == pathNode);
                        if (_currentNode == null)
                        {
                            ComponentPathNode child = new ComponentPathNode() { Name = pathNode, Depth = depth };
                            currentNode.Children.Add(child);
                            _currentNode = child;
                        }
                        currentNode = _currentNode;
                        depth++;
                    }
                }
            }

            StringBuilder LogixSearcherBuilder = new StringBuilder();
            Stack<ComponentPathNode> nodeStack = new Stack<ComponentPathNode>();
            nodeStack.Push(rootNode);
            while (nodeStack.Count > 0)
            {
                ComponentPathNode currentNode = nodeStack.Pop();
                LogixSearcherBuilder.Append(':', currentNode.Depth).Append(" ").AppendLine(currentNode.Name);
                currentNode.Children.Sort();
                foreach (ComponentPathNode child in currentNode.Children)
                    nodeStack.Push(child);
            }
            System.IO.File.WriteAllText("D:\\NeosWiki\\LogixSearcherIndex.txt", LogixSearcherBuilder.ToString());
        }

        private async void CaptureImage()
        {
            Camera componentCamera = Slot.GetComponent<Camera>();
            if (componentCamera != null)
            {

                double aspectRatio = ((double)lastContent.RectTransform.BoundingRect.height + 5.0) / (double)lastContent.RectTransform.BoundingRect.width;
                Slot componentVisual = Slot.FindChild(S => S.Name == "Visual");
                componentVisual.LocalPosition = new float3(0, ((((lastContent.RectTransform.BoundingRect.height / 2.0f) - lastContent.RectTransform.BoundingRect.Center.y) / 2.0f) + 5.0f) * 0.001f, 0.5f);
                componentCamera.OrthographicSize.Value = (lastContent.RectTransform.BoundingRect.height + 5) / 2000.0f;
                Bitmap2D logixTex = await componentCamera.RenderToBitmap(new int2(512, (int)(512.0f * aspectRatio)));
                logixTex.Save($"D:\\NeosWiki\\CustomCapture{CaptureIndex}.png", 100, true);
            }
        }
        private async Task GenerateComponentVisuals(string targetComponentName = null)
        {
            if (componentProgress != null)
                componentProgress.Content.Value = "Starting...";
            //Sync with datamodel
            //await new ToWorld();
            BuildTypeBlacklist();
            BuildTypeMap();
            BuildOverloadPreferences();
            try
            {
                World.RootSlot.GetComponentsInChildren<SlotGizmo>().ForEach(s => s.Slot.Destroy());
                Slot.DestroyChildren();
                Slot ComponentSlot = Slot.AddSlot("ComponentTarget", false);
                Slot ComponentVisual = Slot.AddSlot("Visual", false);
                ComponentVisual.LocalPosition = new float3(0, 0, 0.5f);
                Camera ComponentCamera = Slot.GetComponentOrAttach<Camera>();
                ComponentCamera.Projection.Value = CameraProjection.Orthographic;
                ComponentCamera.Clear.Value = CameraClearMode.Color;
                ComponentCamera.ClearColor.Value = new color(new float4());
                ComponentCamera.Postprocessing.Value = false;
                ComponentCamera.RenderShadows.Value = false;
                ComponentCamera.SelectiveRender.Clear();
                ComponentCamera.SelectiveRender.Add(ComponentVisual);
                IEnumerable<Type> frooxEngineTypes = AppDomain.CurrentDomain.GetAssemblies().Where(T => T.GetName().Name == "FrooxEngine").SelectMany(T => T.GetTypes());
                IEnumerable<Type> frooxEngineComponentTypes = frooxEngineTypes.Where(T => T.Namespace != null && T.Namespace.StartsWith("FrooxEngine")).Where(T => T.IsSubclassOf(typeof(Component)) || T.IsSubclassOf(typeof(LogixNode))).Where(T => targetComponentName == null || T.Name.Contains(targetComponentName));
                MethodInfo LogixGenerateUI = typeof(LogixNode).GetMethod("GenerateUI", BindingFlags.Instance | BindingFlags.NonPublic);
                Dictionary<string, bool> OverloadStatus = new Dictionary<string, bool>();
                int totalComponents = frooxEngineComponentTypes.Count();
                int currentComponent = 0;
                foreach (Type componentType in frooxEngineComponentTypes)
                {
                    if (componentProgress != null)
                        componentProgress.Content.Value = $"Capturing {++currentComponent}/{totalComponents}...";
                    File.WriteAllText($"D:\\NeosWiki\\_Errors.txt", componentErrors.ToString());
                    await new ToWorld();
                    string targetPath = ("D:\\NeosWiki\\" + (componentType.IsSubclassOf(typeof(LogixNode)) ? "Logix" : "Components") + "\\");
                    string componentSafeName = componentType.Name.CoerceValidFileName();
                    //Check if this is one of the blacklisted types that
                    //crashes or otherwise causes issues when attached.
                    //and skip it if it is.
                    if (TypeBlacklist.Contains(componentType))
                    {
                        await Task.Delay(10);
                        continue;
                    }
                    //Read the component's category attribute, so we know where it goes
                    Category componentCategory = componentType.GetCustomAttribute<Category>();
                    //if this type doesn't have a category attribute, it should go in "Uncategorized".
                    if (componentCategory == null)
                        componentCategory = new Category("Uncategorized");

                    //IEnumerable<FieldInfo> nodeInputFields = logixType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(F => F.FieldType.GetInterfaces().Contains(typeof(IInputElement)));
                    //IEnumerable<FieldInfo> nodeOuptutFields = logixType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(F => F.FieldType.GetInterfaces().Contains(typeof(IOutputElement)) || F.Name == "Content");
                    //IEnumerable<PropertyInfo> nodeInputProperties = logixType.GetProperties(BindingFlags.Public).Where(F => F.PropertyType.GetInterfaces().Contains(typeof(IInputElement)));
                    //IEnumerable<PropertyInfo> nodeOutputProperties = logixType.GetProperties(BindingFlags.Public).Where(F => F.PropertyType.GetInterfaces().Contains(typeof(IOutputElement)) || F.Name == "Content");

                    //Check if this component has been rendered already
                    if (String.IsNullOrEmpty(targetComponentName) && componentCategory.Paths.All((path) => File.Exists($"{targetPath}{path}\\{componentSafeName}Node.png") || File.Exists($"{targetPath}{path}\\{componentSafeName}Component.png")))
                    {
                        NodeOverload overload = componentType.GetCustomAttribute<NodeOverload>();
                        if (overload != null)
                        {
                            if (OverloadStatus.ContainsKey(overload.FunctionName))
                                OverloadStatus[overload.FunctionName] = true;
                            else
                                OverloadStatus.Add(overload.FunctionName, true);
                        }
                        await Task.Delay(10);
                        continue;
                    }
                    try
                    {
                        Component targetComponent = null;
                        if (componentType.ContainsGenericParameters || componentType.IsGenericType)
                        {
                            if (GenericTypeInstantiationMap.TryGetValue(componentType, out Type instanceType))
                            {
                                targetComponent = ComponentSlot.AttachComponent(instanceType);
                            }
                            else
                            {
                                try
                                {
                                    GenericTypes types = componentType.GetCustomAttribute<GenericTypes>();
                                    if (types != null)
                                    {
                                        targetComponent = ComponentSlot.AttachComponent(componentType.MakeGenericType(typeof(float)));
                                    }
                                    else if (componentType.IsCastableTo(typeof(LogixNode)))
                                    {
                                        targetComponent = ComponentSlot.AttachComponent(componentType.MakeGenericType(typeof(Object)));
                                    }
                                    else
                                    {
                                        targetComponent = ComponentSlot.AttachComponent(componentType.MakeGenericType(typeof(T)));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    componentErrors.AppendLine("Failed to attach Component: '" + componentType.Name + "' - " + ex.Message);
                                    await Task.Yield();
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            targetComponent = ComponentSlot.AttachComponent(componentType);
                        }
                        if (targetComponent != null)
                        {
                            if (targetComponent is LogixNode node)
                            {
                                ComponentVisual.LocalPosition = new float3(0, 0, 0.5f);
                                ComponentVisual.LocalScale = new float3(100f, 100f, 100f);
                                string componentName = componentType.GetCustomAttribute<NodeName>()?.Name ?? componentType.Name;
                                NodeOverload overload = componentType.GetCustomAttribute<NodeOverload>();
                                if (overload != null)
                                {
                                    if (OverloadStatus.TryGetValue(overload.FunctionName, out bool isOverloadHandled))
                                        if (isOverloadHandled)
                                            continue;

                                    if (LogixOverloadPreferences.TryGetValue(overload.FunctionName, out Type targetType))
                                    {
                                        if (targetType != componentType)
                                        {
                                            if (!OverloadStatus.ContainsKey(overload.FunctionName))
                                                OverloadStatus.Add(overload.FunctionName, false);
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        //If this is a float type, try to make it a default.
                                        //Otherwise, print an error
                                        string[] nameParts = componentType.GetNiceName().Split('_');
                                        if (!nameParts.Skip(1).All(P => P == "Float" || P == "float"))
                                        {
                                            if (!OverloadStatus.ContainsKey(overload.FunctionName))
                                                OverloadStatus.Add(overload.FunctionName, false);
                                            continue;
                                        }
                                    }
                                    if (OverloadStatus.ContainsKey(overload.FunctionName))
                                        OverloadStatus[overload.FunctionName] = true;
                                    else
                                        OverloadStatus.Add(overload.FunctionName, true);
                                }
                                
                                LogixGenerateUI.Invoke(node, new object[] { ComponentVisual, 0.0f, 0.0f });
                                ComponentVisual.LocalScale = new float3(100, 100, 100);
                                await Task.Delay(150);
                                Canvas logixVisual = ComponentVisual.GetComponentInChildren<Canvas>();
                                float aspectRatio = logixVisual.Size.Value.y / logixVisual.Size.Value.x;
                                ComponentCamera.OrthographicSize.Value = logixVisual.Size.Value.y / 20.0f;
                                Bitmap2D logixTex = await ComponentCamera.RenderToBitmap(new int2(128, (int)(128.0 * aspectRatio)));
                                foreach (string path in componentCategory.Paths)
                                {
                                    Directory.CreateDirectory($"{targetPath}{path}\\");
                                    logixTex.Save($"{targetPath}{path}\\{componentSafeName}Node.png", 100, true);
                                }

                                string logixWikiEntry = ComposeLogixWikiEntry(componentType, node, componentName, componentSafeName);

                                foreach (string path in componentCategory.Paths)
                                {
                                    Directory.CreateDirectory($"{targetPath}{path}\\");
                                    string[] pathParts = path.Split('/');
                                    string infoboxOutput = logixWikiEntry;
                                    if (pathParts.Length > 1)
                                    {
                                        infoboxOutput += "[[Category:" + string.Join(": ", pathParts) + "{{#translation:}}]]\r\n"
                                        + "{{:NodeMenu-" + string.Join("-", pathParts.Skip(1)) + "{{#translation:}}}}\r\n";
                                    }

                                    File.WriteAllText($"{targetPath}{path}\\{componentSafeName}.txt", infoboxOutput);
                                }

                                ComponentSlot.Destroy();
                                while (!ComponentSlot.IsDestroyed)
                                    await Task.Delay(500);

                                ComponentVisual.DestroyChildren();
                                ComponentSlot = Slot.AddSlot("ComponentTarget", false);


                            }
                            else
                            {
                                UIBuilder ui = new UIBuilder(ComponentVisual, 800, 5000, 0.1f);
                                ui.Style.MinHeight = 30f;
                                ui.Style.ForceExpandHeight = false;
                                ui.Image(new color(141 / 255.0f, 186 / 255.0f, 104 / 255.0f));
                                ui.VerticalLayout(4f, 0, Alignment.TopLeft);
                                ui.Style.MinHeight = 30f;
                                ui.Style.PreferredHeight = 30f;
                                ui.Style.ForceExpandHeight = false;
                                VerticalLayout content = ui.VerticalLayout(4f, 10f, Alignment.TopLeft);
                                lastContent = content;
                                ui.Style.ChildAlignment = Alignment.TopLeft;
                                {
                                    ui.HorizontalLayout(4f);
                                    ui.Style.FlexibleWidth = 1000f;
                                    ui.Button("<b>" + targetComponent.GetType().GetNiceName() + "</b>", color.White);

                                    ui.Style.FlexibleWidth = 0.0f;
                                    ui.Style.MinWidth = 32f;

                                    ui.Button("D", MathX.Lerp(color.Green, color.White, 0.7f));
                                    ui.Button("X", MathX.Lerp(color.Red, color.White, 0.7f));
                                    ui.NestOut();
                                }
                                if (targetComponent is ICustomInspector customInspector)
                                {
                                    ui.Style.MinHeight = 24f;
                                    customInspector.BuildInspectorUI(ui);
                                }
                                else
                                    WorkerInspector.BuildInspectorUI(targetComponent, ui);
                                await Task.Delay(100);
                                //content.LayoutRectHeightChanged();
                                double aspectRatio = ((double)content.RectTransform.BoundingRect.height + 5.0) / (double)content.RectTransform.BoundingRect.width;

                                ComponentVisual.LocalPosition = new float3(0, ((((content.RectTransform.BoundingRect.height / 2.0f) - content.RectTransform.BoundingRect.Center.y) / 2.0f) + 5.0f) * 0.1f, 0.5f);
                                ComponentCamera.OrthographicSize.Value = (content.RectTransform.BoundingRect.height + 5) / 20.0f;
                                await new ToWorld();
                                Bitmap2D logixTex = await ComponentCamera.RenderToBitmap(new int2(400, (int)(400 * aspectRatio)));
                                foreach (string path in componentCategory.Paths)
                                {
                                    Directory.CreateDirectory($"{targetPath}{path}\\");
                                    logixTex.Save($"{targetPath}{path}\\{componentSafeName}Component.png", 100, true);
                                }
                                string componentWikiEntry = ComposeComponentWikiEntry(componentType, targetComponent, componentSafeName);
                                foreach (string path in componentCategory.Paths)
                                {
                                    Directory.CreateDirectory($"{targetPath}{path}\\");
                                    string[] pathParts = path.Split('/');
                                    string infoboxOutput = componentWikiEntry;
                                    infoboxOutput += "[[Category:Components:" + string.Join(":", pathParts) + "{{#translation:}}|" + componentType.Name + " (Component){{#translation:}}]]";
                                    File.WriteAllText($"{targetPath}{path}\\{componentSafeName}.txt", infoboxOutput);
                                }
                            }

                            if (targetComponentName == null)
                                ComponentSlot.RemoveComponent(targetComponent);
                        }
                    }
                    catch (Exception ex)
                    {

                        componentErrors.AppendLine("Error: Failed to Load Component '" + componentType.Name + "' - " + ex.Message);
                    }
                    if (targetComponentName == null)
                    {
                        while (!ComponentSlot.IsDestroyed)
                        {
                            ComponentSlot.Destroy();
                            if (!ComponentSlot.IsDestroyed)
                                await Task.Delay(100);
                        }
                        while (ComponentVisual.ChildrenCount != 0)
                        {
                            ComponentVisual.DestroyChildren();
                            if (ComponentVisual.ChildrenCount != 0)
                                await Task.Delay(100);
                        }
                        ComponentVisual.RemoveAllComponents(C => true);
                        ComponentSlot = Slot.AddSlot("ComponentTarget", false);
                    }
                }
                foreach (KeyValuePair<string, bool> overloadState in OverloadStatus)
                {
                    if (!overloadState.Value)
                        componentErrors.AppendLine($"Error: Missing LogiX overload definition for '{overloadState.Key}'");
                }
            }
            catch (Exception ex)
            {
                componentErrors.AppendLine("CRITICAL ERROR:" + ex.Message);
            }

        }

        private string ComposeLogixWikiEntry(Type componentType, LogixNode node, string componentName, string componentSafeName)
        {

            StringBuilder infoboxBuilder = new StringBuilder();
            infoboxBuilder.AppendLine("<languages></languages>");
            infoboxBuilder.AppendLine("<translate>");
            infoboxBuilder.AppendLine("<!--T:1-->");
            infoboxBuilder.AppendLine("{{Infobox Logix Node");
            infoboxBuilder.AppendLine($"| Name = {componentName}");
            infoboxBuilder.AppendLine($"| Image =[[File: {componentSafeName}Node.png | noframe | 128px | '{componentName}' LogiX node ]]");
            int inputCtr = 0;
            IEnumerable<MethodInfo> nodeImpulseImputs = componentType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.GetCustomAttribute<ImpulseTarget>() != null);
            foreach (MethodInfo impulseTarget in nodeImpulseImputs)
            {
                infoboxBuilder.AppendLine($"| Input{inputCtr}Type = Impulse | Input{inputCtr}Name = {impulseTarget.Name}");
                inputCtr++;
            }


            ///Standard sync inputs
            IEnumerable<IInputElement> inputs = node.GetSyncMembers<IInputElement>();

            foreach (IInputElement input in inputs)
            {
                infoboxBuilder.AppendLine($"| Input{inputCtr}Type = {input.GetType().GenericTypeArguments[0].GetNiceName().UppercaseFirst()} | Input{inputCtr}Name = {input.Name}");
                inputCtr++;
            }

            int outputCtr = 0;

            FieldInfo nodeContentField = componentType.GetField("Content", BindingFlags.Public | BindingFlags.Instance);
            if (nodeContentField != null)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = { ((nodeContentField.FieldType.IsGenericType || nodeContentField.FieldType.ContainsGenericParameters) ? nodeContentField.FieldType.GenericTypeArguments[0].GetNiceName().UppercaseFirst() : nodeContentField.FieldType.GetNiceName().UppercaseFirst())} | Output{outputCtr}Name = *");
                outputCtr++;
            }
            PropertyInfo nodeContentProperty = componentType.GetProperty("Content", BindingFlags.Public | BindingFlags.Instance);
            if (nodeContentProperty != null)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = { ((nodeContentProperty.PropertyType.IsGenericType || nodeContentProperty.PropertyType.ContainsGenericParameters) ? nodeContentProperty.PropertyType.GenericTypeArguments[0].GetNiceName().UppercaseFirst() : nodeContentProperty.PropertyType.GetNiceName().UppercaseFirst())} | Output{outputCtr}Name = *");
                outputCtr++;
            }



            IEnumerable<FieldInfo> nodeOutputPassthru = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(F => F.GetCustomAttribute<AsOutput>() != null);
            foreach (FieldInfo nodePassthru in nodeOutputPassthru)
            {

                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = { ((nodePassthru.FieldType.IsGenericType || nodePassthru.FieldType.ContainsGenericParameters) ? nodePassthru.FieldType.GenericTypeArguments[0].GetNiceName().UppercaseFirst() : nodePassthru.FieldType.GetNiceName().UppercaseFirst())} | Output{outputCtr}Name = {nodePassthru.Name}");
                outputCtr++;
            }



            //Impulse outputs
            IEnumerable<Impulse> impulseOutputs = node.GetSyncMembers<Impulse>();

            foreach (Impulse output in impulseOutputs)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = Impulse | Output{outputCtr}Name = {output.Name}");
                outputCtr++;
            }



            //Standard sync outputs
            IEnumerable<IOutputElement> outputs = node.GetSyncMembers<IOutputElement>();

            foreach (IOutputElement output in outputs)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = {output.GetType().GenericTypeArguments[0].GetNiceName().UppercaseFirst()} | Output{outputCtr}Name = {output.Name}");
                outputCtr++;
            }


            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:2-->");
            infoboxBuilder.AppendLine("== Intoduction ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:3-->");
            infoboxBuilder.AppendLine("== Usage == ");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:4-->");
            infoboxBuilder.AppendLine("== Examples ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:5-->");
            infoboxBuilder.AppendLine("== Node Menu ==");

            infoboxBuilder.AppendLine("</translate>");
            if (componentType.IsGenericType)
                infoboxBuilder.AppendLine("[[Category:Generics{{#translation:}}]]");
            infoboxBuilder.AppendLine("[[Category:LogiX{{#translation:}}]]");
            return infoboxBuilder.ToString();
        }

        private string ComposeComponentWikiEntry(Type componentType, Component component, string componentSafeName)
        {
            string componentName = componentType.Name;
            StringBuilder infoboxBuilder = new StringBuilder();
            infoboxBuilder.AppendLine("<languages></languages>");
            infoboxBuilder.AppendLine("<translate>");
            infoboxBuilder.AppendLine("<!--T:1-->");
            infoboxBuilder.AppendLine("{{stub}}");
            infoboxBuilder.AppendLine("{{Infobox Component");
            infoboxBuilder.AppendLine($"|Image={componentSafeName}Component.png");
            infoboxBuilder.AppendLine($"|Name={componentName}");
            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:2-->");
            infoboxBuilder.AppendLine("== Intoduction ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:3-->");
            infoboxBuilder.AppendLine("== Usage ==");
            infoboxBuilder.AppendLine("{{Table ComponentFields");
            BuildSyncMembers(component, 3);

            void BuildSyncMembers(Worker target, int indexOffset = 0)
            {
                try
                {
                    for (int index = indexOffset; index < target.SyncMemberCount; ++index)
                    {
                        if (target.GetSyncMemberFieldInfo(index).GetCustomAttribute<HideInInspectorAttribute>() == null)
                        {
                            Type memberType = target.GetSyncMemberFieldInfo(index).FieldType;
                            if (memberType.IsGenericType && memberType.GenericTypeArguments.Length > 0)
                            {
                                Type syncMemberType = memberType.GenericTypeArguments[0];
                                //Check if this is a nested generic - i.e: Sync<Sync<float>>
                                if (syncMemberType.IsGenericType)
                                {
                                    Type memberGenericType = syncMemberType.GetGenericTypeDefinition();
                                    string memberTypeName = syncMemberType.GetNiceName();
                                    int bracketIndex = memberTypeName.IndexOf('<');
                                    if (bracketIndex > 0 && bracketIndex < memberTypeName.Length - 1)
                                    {
                                        char[] typeChars = memberTypeName.ToCharArray();
                                        typeChars[0] = char.ToUpper(typeChars[0]);
                                        typeChars[bracketIndex + 1] = char.ToUpper(typeChars[bracketIndex + 1]);
                                        memberTypeName = new string(typeChars);
                                    }
                                    //We want to link to the generic base (i.e Sync<T> or Sync`1), but we want to show the full name (Sync<float>);
                                    infoboxBuilder.AppendLine($"|{target.GetSyncMemberName(index)}|{memberGenericType.Name.UppercaseFirst()}|TypeString{index - indexOffset}={memberTypeName}| ");
                                }
                                else
                                {
                                    infoboxBuilder.AppendLine($"|{target.GetSyncMemberName(index)}|{syncMemberType.GetNiceName().UppercaseFirst()}| ");
                                }
                            }
                            else
                            {
                                if (target.GetSyncMember(index) is SyncObject syncField)
                                {
                                    BuildSyncMembers(syncField);
                                }
                                else
                                {
                                    infoboxBuilder.AppendLine($"|{target.GetSyncMemberName(index)}|{memberType.GetNiceName().UppercaseFirst()}| ");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UniLog.Log(ex.Message);
                }
            }

            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:4-->");
            infoboxBuilder.AppendLine("== Behavior ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:5-->");
            infoboxBuilder.AppendLine("== Examples ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:6-->");
            infoboxBuilder.AppendLine("== Related Components ==");
            infoboxBuilder.AppendLine("</translate>");
            if (componentType.IsGenericType)
                infoboxBuilder.AppendLine("[[Category:Generics{{#translation:}}]]");
            infoboxBuilder.AppendLine("[[Category:Components{{#translation:}}|" + componentName + " (Component){{#translation:}}]]");
            return infoboxBuilder.ToString();
        }

        private void BuildTypeBlacklist()
        {
            TypeBlacklist = new List<Type>()
            {
                ///These crash
                typeof(DebugCrash),
                typeof(DebugFingerPoseCompensation), //Causes hard crash

                ///These crash, but might be fixable
                //typeof(DynamicValueVariable<>),
                typeof(DataPresetValue<>),
                //typeof(ButtonValueShift<>),
                //typeof(Slider<>),

                 //These break things when spawned
                typeof(FullBodyCalibrator),
                //typeof(FullBodyCalibratorDialog),
                typeof(FrooxEngine.Undo.UndoManager),

                //This doesn't seem useful to anyone except Frooxius
                typeof(LegacyRadiantScreenWrapper<>),
                //Not sure how to use this
                typeof(ImplementableComponent<>),
                typeof(QuantityTextEditorParser<>),

            };
        }

        /// <summary>
        /// This builds the table that maps empty generics to a useful demonstration generic type.
        /// </summary>
        private void BuildTypeMap()
        {
            GenericTypeInstantiationMap = new Dictionary<Type, Type>()
            {
                {typeof(AssetProxy<>),typeof(AssetProxy<IAsset>) },
                {typeof(LocalAssetProvider<>), typeof(LocalAssetProvider<Font>) },
                {typeof(AssetLoader<>),typeof(AssetLoader<IAsset>) },
                {typeof(ReferenceOptionDescriptionDriver<>), typeof(ReferenceOptionDescriptionDriver<IWorldElement>) },
                {typeof(DynamicReference<>), typeof(DynamicReference<IWorldElement>) },
                {typeof(DynamicReferenceVariable<>), typeof(DynamicReferenceVariable<IWorldElement>) },
                {typeof(DynamicReferenceVariableDriver<>), typeof(DynamicReferenceVariableDriver<IWorldElement>) },
                {typeof(DynamicReferenceVariableReset<>), typeof(DynamicReferenceVariableReset<IWorldElement>) },
                {typeof(DynamicValueVariable<>), typeof(DynamicValueVariable<int>) },
                {typeof(DataPresetReference<>), typeof(DataPresetReference<IWorldElement>) },
                {typeof(ReferenceRadio<>), typeof(ReferenceRadio<Sync<T>>) },
                {typeof(DelegateProxy<>), typeof(DelegateProxy<Action>) },
                {typeof(DelegateProxySource<>), typeof(DelegateProxySource<Action>) },
                {typeof(ValueField<>), typeof(ValueField<int>) },
                {typeof(ValueFieldProxy<>), typeof(ValueFieldProxy<int>) },
                {typeof(MaterialProviderBase<>), typeof(MaterialProviderBase<Material>) },
                {typeof(ProceduralAssetProvider<>), typeof(ProceduralAssetProvider<Texture2D>) },
                {typeof(AssetProvider<>), typeof(AssetProvider<Texture2D>) },
                {typeof(DynamicAssetProvider<>), typeof(DynamicAssetProvider<Texture2D>) },
                {typeof(MultiTextureFader<>), typeof(MultiTextureFader<Texture2D>) },
                {typeof(TextEditorParser<>), typeof(TextEditorParser<int>) },
                //{typeof(QuantityTextEditorParser<>), typeof(QuantityTextEditorParser<QuantityX.Angle>) },
                {typeof(DelegateTag<>), typeof(DelegateTag<Action>) },
                {typeof(ValueTag<>), typeof(ValueTag<int>) },
                {typeof(CallbackValueArgument<>), typeof(CallbackValueArgument<int>) },
               // {typeof(StaticAssetProvider<,,>), typeof(StaticAssetProvider<Texture2D, DummyMetadata, FrooxEngine.TextureVariantDescriptor>) },
                {typeof(GenericModalDialogSpawner<>), typeof(GenericModalDialogSpawner<WikiAssetGenerator>) },
                {typeof(GenericUserspaceDialogSpawner<>), typeof(GenericUserspaceDialogSpawner<WikiAssetGenerator>) },
                {typeof(ControllerNode<>), typeof(ControllerNode<FrooxEngine.IndexController>) },
                {typeof(EnumInput<>), typeof(EnumInput<Enum>) },
                {typeof(TextFieldNodeBase<>), typeof(TextFieldNodeBase<string>) },
                //{typeof(FireOnChangeBase<>), typeof(FireOnChangeBase<int>) },
                {typeof(EnumToInt<>), typeof(EnumToInt<Enum>) },
                {typeof(ButtonDelegateRelay<>), typeof(ButtonDelegateRelay<Action>) },
                {typeof(ReferenceField<>), typeof(ReferenceField<IWorldElement>) },
                {typeof(ReferenceMultiDriver<>), typeof(ReferenceMultiDriver<IWorldElement>) },
                {typeof(ReferenceEqualityDriver<>), typeof(ReferenceEqualityDriver<IWorldElement>) },
                {typeof(ButtonEnumShift<>), typeof(ButtonEnumShift<int>) },
                {typeof(ButtonReferenceCycle<>), typeof(ButtonReferenceCycle<IWorldElement>) },
                {typeof(ButtonReferenceSet<>), typeof(ButtonReferenceSet<IWorldElement>) },
                {typeof(ReferenceCopy<>), typeof(ReferenceCopy<IWorldElement>) },
                {typeof(ReferenceUserOverride<>), typeof(ReferenceUserOverride<IWorldElement>) },
                {typeof(AssetMultiplexer<>), typeof(AssetMultiplexer<ITexture2D>) },
                {typeof(ReferenceMultiplexer<>), typeof(ReferenceMultiplexer<IWorldElement>) },
                {typeof(NeosEnumEditor<>), typeof(NeosEnumEditor<Enum>) },
                {typeof(AssetFrameSlot<>), typeof(AssetFrameSlot<ITexture2D>) },
                //{typeof(LogixOperator<>), typeof(LogixOperator<int>) },
                //{typeof(DualInputOperator<>), typeof(DualInputOperator<int>) },
                //{typeof(EqualsNode<>), typeof(EqualsNode<float>) },
                {typeof(FrooxEngine.LogiX.Data.DynamicVariableInput<>), typeof(FrooxEngine.LogiX.Data.DynamicVariableInput<string>) },
                {typeof(FrooxEngine.LogiX.Data.DynamicVariableInputWithEvents<>), typeof(FrooxEngine.LogiX.Data.DynamicVariableInputWithEvents<string>) },
                {typeof(Slider<>), typeof(Slider<float>) },
                {typeof(ButtonValueShift<>), typeof(ButtonValueShift<float>) },
                {typeof(NullCoalesce<>), typeof(NullCoalesce<Object>) }
            };
        }
        /// <summary>
        /// This builds a table that maps overloaded logix nodes to a specific logix node,
        /// so we have consistency.
        /// </summary>
        private void BuildOverloadPreferences()
        {
            LogixOverloadPreferences = new Dictionary<string, Type>()
            {
                {"BoundingBoxEncapsulate",typeof(EncapsulateBounds)},
                {"Display",typeof(Display_Dummy)},
                {"IndexOfFirstMatch",typeof(IndexOfFirstMatch<char>)},
                {"FindCharacterController",typeof(FindCharacterControllerFromUser)},
                {"SimplexNoise",typeof(SimplexNoise_1D)},
                {"ToString", typeof(ToString_DateTime) },
                {"AND", typeof(AND_Bool) },
                {"AND_Multi", typeof(AND_Multi_Bool) },
                {"NAND", typeof(NAND_Bool) },
                {"NAND_Multi", typeof(NAND_Multi_Bool) },
                {"OR", typeof(OR_Bool) },
                {"OR_Multi", typeof(OR_Multi_Bool) },
                {"NOR", typeof(NOR_Bool) },
                {"NOR_Multi", typeof(NOR_Multi_Bool) },
                {"XOR", typeof(XOR_Bool) },
                {"XOR_Multi", typeof(XOR_Multi_Bool) },
                {"XNOR", typeof(XNOR_Bool) },
                {"XNOR_Multi", typeof(XNOR_Multi_Bool) },
                {"NOT", typeof(NOT_Bool) },
                {"NotEquals", typeof(NotEquals_Float) },
                {"++", typeof(Inc_Float) },
                {"--", typeof(Dec_Float) },
                {"ShiftLeft", typeof(ShiftLeft_Int) },
                {"ShiftRight", typeof(ShiftRight_Int) },
                {"RotateLeft", typeof(RotateLeft_Int) },
                {"RotateRight", typeof(RotateRight_Int) },
                {"Remap11_01", typeof(Remap11_01_Float) },
                {"Slerp", typeof(FrooxEngine.LogiX.Math.Quaternions.Slerp_floatQ) },
                {"MultiSlerp", typeof(FrooxEngine.LogiX.Math.Quaternions.MultiSlerp_floatQ) },
                {"ConstantSlerp", typeof(FrooxEngine.LogiX.Math.Quaternions.ConstantSlerp_floatQ) },
                {"EulerAngles", typeof(FrooxEngine.LogiX.Math.Quaternions.EulerAngles_floatQ) },
                {"FromEuler", typeof(FrooxEngine.LogiX.Math.Quaternions.FromEuler_floatQ) },
                {"AxisAngle", typeof(FrooxEngine.LogiX.Math.Quaternions.AxisAngle_floatQ) },
                {"LookRotation", typeof(FrooxEngine.LogiX.Math.Quaternions.LookRotation_floatQ)},
                {"FromToRotation", typeof(FrooxEngine.LogiX.Math.Quaternions.FromToRotation_floatQ) },
                {"InverseRotation", typeof(FrooxEngine.LogiX.Math.Quaternions.InverseRotation_floatQ) },
                {"ToAxisAngle", typeof(FrooxEngine.LogiX.Math.Quaternions.ToAxisAngle_floatQ) },
                {"Construct2", typeof(Construct_Float2) },
                {"Deconstruct2", typeof(Deconstruct_Float2) },
                {"Construct3", typeof(Construct_Float3) },
                {"Deconstruct3", typeof(Deconstruct_Float3) },
                {"PackRows", typeof(PackRows_Float2x2) },
                {"UnpackRows", typeof(UnpackRows_Float2x2) },
                {"PackColumns", typeof(PackColumns_Float2x2) },
                {"UnpackColumns", typeof(UnpackColumns_Float2x2) },
                {"Cross", typeof(Cross_Float3) },
                {"Reflect", typeof(Reflect_Float3) },
                {"Inverse", typeof(Inverse_Float3x3) },
                {"Determinant", typeof(Determinant_Float2x2) },
                {"Dot", typeof(Dot_Float2) },
                {"Conditional", typeof(Conditional_Float)},
            };
        }
        enum Enum
        {
            val_1 = 0
        }
    }

    /// <summary>
    /// This serves as a generic class that can be used to instantiate Components
    /// It is named 'T' so that the renders of components/nodes show something
    /// generic looking.
    /// </summary>
    class T : IEquatable<T>
    {
        public bool Equals(T other)
        {
            return true;
        }
    }
    class ComponentPathNode : IComparable
    {
        public string Name { get; set; }
        public int Depth { get; set; } = 0;
        public List<ComponentPathNode> Children { get; set; } = new List<ComponentPathNode>();
        public int CompareTo(object other)
        {
            return ((ComponentPathNode)other).Name.CompareTo(Name);
        }
    }
}
