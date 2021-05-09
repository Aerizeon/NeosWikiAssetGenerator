using FrooxEngine;
using FrooxEngine.LogiX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FrooxEngine.UIX;
using BaseX;
using CodeX;
using System.Threading.Tasks;
using System.Reflection;

namespace NeosWikiAssetGenerator.Type_Processors
{
    public class ComponentTypeProcessor : NeosTypeProcessor
    {
        public Dictionary<string, string> Overloads { get; set; } = new Dictionary<string, string>();

        public override bool ValidateProcessor(Type neosType)
        {
            return neosType.IsSubclassOf(typeof(Component)) && !neosType.IsSubclassOf(typeof(LogixNode)) && !TypeBlacklist.Contains(neosType.FullName);
        }

        public override object CreateInstance(Type neosType)
        {

            string targetInstanceType = GetOverload(neosType);
            if (targetInstanceType is null)
            {
                UniLog.Log($"Missing Component overload for {neosType.FullName}");
                return null;
            }
           return InstanceSlot.AttachComponent(targetInstanceType);
        }

        public async override Task GenerateVisual(object typeInstance, Type neosType, bool force = false)
        {
            Component targetInstance = typeInstance as Component;
            Category typeCategory = GetCategory(neosType);
            string typeSafeName = GetSafeName(neosType);

            if (!(force || NeedsVisual(typeSafeName, typeCategory)))
                return;

            Rect bounds = await BuildComponentUI(targetInstance);
            double aspectRatio = (bounds.height + 5.0) / bounds.width;

            VisualSlot.LocalPosition = new float3(0, ((((bounds.height / 2.0f) - bounds.Center.y) / 2.0f) + 5.0f) * 0.1f, 0.5f);
            VisualCaptureCamera.OrthographicSize.Value = (bounds.height + 5) / 20.0f;
            await new ToWorld();
            Bitmap2D componentImage = await VisualCaptureCamera.RenderToBitmap(new int2(400, (int)(400 * aspectRatio)));

            foreach (string path in typeCategory.Paths)
            {
                Directory.CreateDirectory($"{BasePath}\\Components\\{path}\\");
                componentImage.Save($"{BasePath}\\Components\\{path}\\{typeSafeName}Component.png", 100, true);
            }
            VisualSlot.DestroyChildren();
        }

        public async override Task GenerateData(object typeInstance, Type neosType, bool force = false)
        {
            Component targetInstance = typeInstance as Component;
            Category typeCategory = GetCategory(neosType);
            string typeSafeName = GetSafeName(neosType);
            string typeName = neosType.GetCustomAttribute<NodeName>()?.Name ?? StringHelper.BeautifyName(neosType.Name);
            

            if (!(force || NeedsData(typeSafeName, typeCategory)))
                return;

            StringBuilder infoboxBuilder = new StringBuilder();
            infoboxBuilder.AppendLine("<languages></languages>");
            infoboxBuilder.AppendLine("<translate>");
            infoboxBuilder.AppendLine("<!--T:1-->");
            infoboxBuilder.AppendLine("{{stub}}");
            infoboxBuilder.AppendLine("{{Infobox Component");
            infoboxBuilder.AppendLine($"|Image={typeSafeName}Component.png");
            infoboxBuilder.AppendLine($"|Name={typeName}");
            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:2-->");
            infoboxBuilder.AppendLine("== Fields ==");
            infoboxBuilder.AppendLine("{{Table ComponentFields");

            BuildComponentSyncMembers(infoboxBuilder, targetInstance, 3);

            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:3-->");
            infoboxBuilder.AppendLine("== Usage ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:4-->");
            infoboxBuilder.AppendLine("== Examples ==");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:5-->");
            infoboxBuilder.AppendLine("== Related Components ==");
            infoboxBuilder.AppendLine("</translate>");
            infoboxBuilder.AppendLine("[[Category:ComponentStubs]]");
            if (neosType.IsGenericType)
                infoboxBuilder.AppendLine("[[Category:Generics{{#translation:}}]]");
            infoboxBuilder.AppendLine("[[Category:Components{{#translation:}}|" + typeName + "]]");
            foreach (string path in typeCategory.Paths)
            {
                infoboxBuilder.AppendLine("[[Category:Components:" + path.Replace('/', ':') + "{{#translation:}}|" + typeName + "]]");

            }
            foreach (string path in typeCategory.Paths)
            {
                using (StreamWriter fileWriter = new StreamWriter($"{BasePath}\\Components\\{path}\\{typeSafeName}.txt"))
                {
                    await fileWriter.WriteAsync(infoboxBuilder.ToString());
                }
            }
        }

        public override void DestroyInstance(object typeInstance)
        {
        }

        public override bool NeedsVisual(string typeSafeName, Category typeCategory)
        {
            return !typeCategory.Paths.All((path) => File.Exists($"{BasePath}\\Components\\{path}\\{typeSafeName}Component.png"));
        }

        public override bool NeedsData(string typeSafeName, Category typeCategory)
        {
            return !typeCategory.Paths.All((path) => File.Exists($"{BasePath}\\Components\\{path}\\{typeSafeName}.txt"));
        }

        private async Task<Rect> BuildComponentUI(Component targetInstance)
        {
            UIBuilder ui = new UIBuilder(VisualSlot, 800, 5000, 0.1f);
            ui.Style.MinHeight = 30f;
            ui.Style.ForceExpandHeight = false;
            ui.Image(new color(141 / 255.0f, 186 / 255.0f, 104 / 255.0f));
            ui.VerticalLayout(4f, 0, Alignment.TopLeft);
            ui.Style.MinHeight = 30f;
            ui.Style.PreferredHeight = 30f;
            ui.Style.ForceExpandHeight = false;
            VerticalLayout content = ui.VerticalLayout(4f, 10f, Alignment.TopLeft);
            ui.Style.ChildAlignment = Alignment.TopLeft;
            {
                ui.HorizontalLayout(4f);
                ui.Style.FlexibleWidth = 1000f;
                ui.Button("<b>" + targetInstance.GetType().GetNiceName() + "</b>", color.White);

                ui.Style.FlexibleWidth = 0.0f;
                ui.Style.MinWidth = 32f;

                ui.Button("D", MathX.Lerp(color.Green, color.White, 0.7f));
                ui.Button("X", MathX.Lerp(color.Red, color.White, 0.7f));
                ui.NestOut();
            }
            if (targetInstance is ICustomInspector customInspector)
            {
                ui.Style.MinHeight = 24f;
                customInspector.BuildInspectorUI(ui);
            }
            else
                WorkerInspector.BuildInspectorUI(targetInstance, ui);
            await new Updates(5);
            return content.RectTransform.BoundingRect;
        }
        private void BuildComponentSyncMembers(StringBuilder infoboxBuilder, Worker target, int indexOffset = 0)
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
                            Type t = GetNeosWriteNodeOverload(memberType);
                            if (t != null)
                            {
                                if (t.IsGenericType)
                                    infoboxBuilder.AppendLine($"|{target.GetSyncMemberName(index)}|{t.Name.UppercaseFirst()}|TypeString{index - indexOffset}={t.GetNiceName().UppercaseFirst()}|");
                                else
                                    infoboxBuilder.AppendLine($"|{target.GetSyncMemberName(index)}|{t.GetNiceName().UppercaseFirst()}|");
                            }
                            else
                            {
                                UniLog.Log($"Missing WriteNode overload for {memberType.GetNiceName()}");
                                infoboxBuilder.AppendLine($"|{target.GetSyncMemberName(index)}|{memberType.Name.UppercaseFirst()}|TypeString{index - indexOffset}={memberType.GetNiceName().UppercaseFirst()}|");
                            }

                        }
                        else
                        {
                            if (target.GetSyncMember(index) is SyncObject syncField)
                            {
                                BuildComponentSyncMembers(infoboxBuilder, syncField);
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
        private string GetOverload(Type neosType)
        {
            if (neosType.IsGenericType || neosType.IsGenericTypeDefinition)
            {
                if (Overloads.TryGetValue(neosType.FullName, out string overloadTypeString))
                    return overloadTypeString;
                else
                    return null;
            }
            else
                return neosType.FullName;
        }
        private Type GetNeosWriteNodeOverload(Type inputType)
        {
            Type returnType = null;
            Type inputSubType = null;
            inputSubType = inputType.FindGenericBaseClass(typeof(SyncRef<>));
            if (inputSubType == null)
                inputSubType = inputType.EnumerateInterfacesRecursively().FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IValue<>));

            if (inputSubType != null)
                returnType = inputSubType.GetGenericArguments()[0];

            return returnType;
        }
    }
}
