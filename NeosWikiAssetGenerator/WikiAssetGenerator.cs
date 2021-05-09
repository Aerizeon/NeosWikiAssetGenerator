using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.FinalIK;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Actions;
using FrooxEngine.LogiX.Avatar;
using FrooxEngine.LogiX.Cast;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.Display;
using FrooxEngine.LogiX.Input;
using FrooxEngine.LogiX.Math;
using FrooxEngine.LogiX.Network;
using FrooxEngine.LogiX.Operators;
using FrooxEngine.LogiX.Physics;
using FrooxEngine.LogiX.Playback;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.LogiX.References;
using FrooxEngine.LogiX.Transform;
using FrooxEngine.LogiX.Twitch;
using FrooxEngine.LogiX.Utility;
using FrooxEngine.UIX;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NeosWikiAssetGenerator.Type_Processors;

namespace NeosWikiAssetGenerator
{
    [Category("Add-Ons/Generators/Wiki")]
    public class WikiAssetGenerator : Component, ICustomInspector
    {
        private StringBuilder componentErrors = new StringBuilder();
        public readonly Sync<string> ComponentName;
        public readonly Sync<int> CaptureIndex;

        List<NeosTypeProcessor> Processors = new List<NeosTypeProcessor>();
        VerticalLayout lastContent = null;
        Text componentProgress = null;
        protected override void OnAttach()
        {
            base.OnAttach();

        }
        protected override void OnAwake()
        {
            base.OnAwake();
        }
        public void BuildInspectorUI(UIBuilder ui)
        {
            WorkerInspector.BuildInspectorUI(this, ui);

            ui.Button("Generate Component and LogiX visuals", (b, e) => { StartTask(async () => { await GenerateNew(); }); });
            ui.Button("Generate Wiki InfoBoxes and tables", (b, e) => { GenerateInfoBoxes(); });
            ui.Button("Generate Wiki Index pages", (b, e) => { GenerateWikiIndexPages(); });
            ui.Button("Capture Image", (b, e) => { CaptureImage(); });
            componentProgress = ui.Text("0/0", true, Alignment.MiddleCenter, false);
        }

        private async Task GenerateNew()
        {
            Processors.Clear();
            ComponentTypeProcessor ComponentProcessor = new ComponentTypeProcessor();
            LogixTypeProcessor LogixProcessor = new LogixTypeProcessor();
            IEnumerable<Type> frooxEngineTypes = AppDomain.CurrentDomain.GetAssemblies().Where(T => T.GetName().Name == "FrooxEngine").SelectMany(T => T.GetTypes());
            IEnumerable<Type> frooxEngineComponentTypes = frooxEngineTypes.Where(T => T.Namespace != null && T.Namespace.StartsWith("FrooxEngine")).Where(T => T.IsSubclassOf(typeof(Component)) || T.IsSubclassOf(typeof(LogixNode)));

            Camera ComponentCamera = Slot.GetComponentOrAttach<Camera>();
            ComponentCamera.Projection.Value = CameraProjection.Orthographic;
            ComponentCamera.Clear.Value = CameraClearMode.Color;
            ComponentCamera.ClearColor.Value = new color(new float4());
            ComponentCamera.Postprocessing.Value = false;
            ComponentCamera.RenderShadows.Value = false;
            ComponentCamera.SelectiveRender.Clear();
            ComponentCamera.SelectiveRender.Add();
            NeosTypeProcessor.VisualCaptureCamera = ComponentCamera;

            NeosTypeProcessor.TypeBlacklist = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("D:\\Data\\NeosWiki\\TypeBlacklist.json"));

            ComponentProcessor.Overloads = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("D:\\Data\\NeosWiki\\ComponentOverloads.json"));
            if (ComponentProcessor.Overloads == null)
            {
                UniLog.Log("Could not load ComponentOverloads");
                ComponentProcessor.Overloads = new Dictionary<string, string>();
            }

            LogixProcessor.Overloads = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("D:\\Data\\NeosWiki\\LogixOverloads.json"));
            if (LogixProcessor.Overloads == null)
            {
                UniLog.Log("Could not load LogixOverloads");
                LogixProcessor.Overloads = new Dictionary<string, string>();
            }

            Processors.Add(LogixProcessor);
            Processors.Add(ComponentProcessor);

            List<string> logixErrorTypes = new List<string>();
            List<string> componentErrorTypes = new List<string>();
            try
            {
                int totalComponents = frooxEngineComponentTypes.Count();
                int completedComponents = 0;
                foreach (Type neosType in frooxEngineComponentTypes)
                {
                    NeosTypeProcessor.VisualSlot = Slot.AddSlot("Visual", false); ;
                    NeosTypeProcessor.InstanceSlot = Slot.AddSlot("ComponentTarget", false);
                    ComponentCamera.SelectiveRender[0] = NeosTypeProcessor.VisualSlot;
                    NeosTypeProcessor.VisualSlot.LocalPosition = new float3(0, 0, 0.5f);
                    foreach (NeosTypeProcessor currentProcessor in Processors)
                    {
                        try
                        {
                            if (currentProcessor.ValidateProcessor(neosType))
                            {
                                object instance = currentProcessor.CreateInstance(neosType);
                                if (instance != null)
                                {
                                    await currentProcessor.GenerateVisual(instance, instance.GetType());
                                    await currentProcessor.GenerateData(instance, instance.GetType());
                                    currentProcessor.DestroyInstance(instance);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            componentErrorTypes.Add(neosType.FullName);
                            UniLog.Log($"Component generation failed for {neosType.FullName}: {ex}");
                        }
                    }
                    NeosTypeProcessor.VisualSlot.Destroy(false);
                    NeosTypeProcessor.InstanceSlot.Destroy(false);
                    await new Updates(2);
                    completedComponents++;
                    componentProgress.Content.Value = $"{completedComponents} / {totalComponents} ({Math.Round(((float)completedComponents / (float)totalComponents) * 100.0f)}%)";
                }
            }
            catch (Exception ex)
            {
                UniLog.Log("Processor Failed: " + ex);
                //Log.Error(ex, "Processor failed:");
            }
            File.WriteAllText("D:\\Data\\NeosWiki\\logixErrorTypes.json", JsonConvert.SerializeObject(logixErrorTypes, Formatting.Indented));
            File.WriteAllText("D:\\Data\\NeosWiki\\componentErrorTypes.json", JsonConvert.SerializeObject(componentErrorTypes, Formatting.Indented));

            List<string> assemblyNames = new List<string>
            {
                "FrooxEngine",
                "CodeX",
                "BaseX",
                "CloudX.Shared",
                "PostX",
                "QuantityX",
                "CommandX",
                "ArchiteX"
            };
            frooxEngineTypes = AppDomain.CurrentDomain.GetAssemblies().Where(T => assemblyNames.Contains(T.GetName().Name)).SelectMany(T => T.GetTypes());
            List<EnumData> neosEnumTypes = new List<EnumData>();
            foreach (Type neosType in frooxEngineTypes)
            {
                if(neosType.IsEnum)
                {
                    EnumData enumData = new EnumData
                    {
                        Name = neosType.FullName,
                        Values = System.Enum.GetNames(neosType).ToList()
                    };
                    neosEnumTypes.Add(enumData);
                }
            }
            File.WriteAllText("D:\\Data\\NeosWiki\\enumTypes.json", JsonConvert.SerializeObject(neosEnumTypes, Formatting.Indented));
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
            System.IO.File.WriteAllText("D:\\Data\\NeosWiki\\Table_ComponentFields.txt", sb.ToString());
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
            System.IO.File.WriteAllText("D:\\Data\\NeosWiki\\Table_ComponentTriggers.txt", sb.ToString());
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
            System.IO.File.WriteAllText("D:\\Data\\NeosWiki\\Infobox_Logix_Node.txt", sb.ToString());
            #endregion
        }
        private void GenerateWikiIndexPages()
        {
            IEnumerable<Type> frooxEngineTypes = AppDomain.CurrentDomain.GetAssemblies().Where(T => T.GetName().Name == "FrooxEngine").SelectMany(T => T.GetTypes());
            IEnumerable<Type> frooxEngineComponentTypes = frooxEngineTypes.Where(T => T.Namespace != null && T.Namespace.StartsWith("FrooxEngine")).Where(T => T.IsSubclassOf(typeof(Component)) || T.IsSubclassOf(typeof(LogixNode)));

            ComponentPathNode componentRoot = new ComponentPathNode() { Name = "Components" };
            ComponentPathNode logixRoot = new ComponentPathNode() { Name = "Logix Nodes" };
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

                    ComponentPathNode currentNode = componentRoot;
                    if (componentType.IsSubclassOf(typeof(LogixNode)))
                        currentNode = logixRoot;
                    int depth = 0;
                    foreach (string pathNode in (path + "/" + componentType.GetNiceName()).Split('/'))
                    {
                        if (currentNode.Children == null)
                            currentNode.Children = new List<ComponentPathNode>();
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
                    currentNode.Inherits = GetBaseTypes(componentType).ToList();
                    currentNode.TypeName = componentType.FullName;
                    
                }
            }

            StringBuilder JPSearcherStringBuilder = new StringBuilder();
            Stack<ComponentPathNode> nodeStack = new Stack<ComponentPathNode>();
            nodeStack.Push(componentRoot);
            while (nodeStack.Count > 0)
            {
                ComponentPathNode currentNode = nodeStack.Pop();
                JPSearcherStringBuilder.Append(':', currentNode.Depth).Append(" ").AppendLine(currentNode.Name);
                if (currentNode.Children == null)
                    continue;
                currentNode.Children.Sort();
                foreach (ComponentPathNode child in currentNode.Children)
                    nodeStack.Push(child);
            }
            File.WriteAllText("D:\\Data\\NeosWiki\\Components.json",
                JsonConvert.SerializeObject(componentRoot,
                Formatting.Indented,
                new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore
                }));
            nodeStack = new Stack<ComponentPathNode>();
            nodeStack.Push(logixRoot);
            while (nodeStack.Count > 0)
            {
                ComponentPathNode currentNode = nodeStack.Pop();
                JPSearcherStringBuilder.Append(':', currentNode.Depth).Append(" ").AppendLine(currentNode.Name);
                if (currentNode.Children == null)
                    continue;
                currentNode.Children.Sort();
                foreach (ComponentPathNode child in currentNode.Children)
                    nodeStack.Push(child);
            }
            File.WriteAllText("D:\\Data\\NeosWiki\\Logix.json",
                JsonConvert.SerializeObject(logixRoot,
                Formatting.Indented,
                new JsonSerializerSettings()
                {
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                }));
            File.WriteAllText("D:\\Data\\NeosWiki\\JPSearcherIndex.txt", JPSearcherStringBuilder.ToString());

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
                logixTex.Save($"D:\\Data\\NeosWiki\\CustomCapture{CaptureIndex}.png", 100, true);
            }
        }
        

        private IEnumerable<string> GetBaseTypes(Type parentType)
        {
            List<Type> implementedTypes = new List<Type>(20);
            IEnumerable<Type> availableTypes = Assembly.GetAssembly(parentType).GetTypes();

            Type currentType = parentType;

            while (currentType != null)
            { 
                implementedTypes.AddRange(availableTypes.Where(type => type != null && currentType.IsSubclassOf(type)));
                implementedTypes.AddRange(currentType.GetInterfaces());
                currentType = currentType.BaseType;
                if (currentType == typeof(object))
                    break;
            }

            return implementedTypes.Distinct().Select(inheritedType => inheritedType.FullName);
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
        [JsonIgnore]
        public int Depth { get; set; } = 0;
        public string TypeName { get; set; }
        public List<string> Inherits { get; set; }
        public List<ComponentPathNode> Children { get; set; } = null;
        public int CompareTo(object other)
        {
            return Name.CompareTo(((ComponentPathNode)other).Name) ;
        }
    }

    class EnumData
    {
        public string Name { get; set; }
        public List<string> Values { get; set; }
    }
}
