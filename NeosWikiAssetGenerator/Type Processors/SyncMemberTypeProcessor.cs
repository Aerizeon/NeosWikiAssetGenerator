using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NeosWikiAssetGenerator.Type_Processors
{
    class SyncMemberTypeProcessor : NeosTypeProcessor
    {
        public override bool ValidateProcessor(Type neosType)
        {
            return neosType.IsSubclassOf(typeof(SyncObject)) && !TypeBlacklist.Contains(neosType.FullName);
        }

        public override object CreateInstance(Type neosType)
        {
            SyncObject obj = Activator.CreateInstance(neosType) as SyncObject;
            obj.Initialize(Engine.Current.WorldManager.FocusedWorld, Engine.Current.WorldManager.FocusedWorld.RootSlot);
            return obj;
        }

        public override async Task GenerateVisual(object typeInstance, Type neosType, bool force = false)
        {
            SyncObject targetInstance = typeInstance as SyncObject;
            string typeSafeName = GetSafeName(neosType);

            if (!(force || NeedsVisual(typeSafeName)))
                return;

            Rect bounds = await BuildSyncMemberUI(targetInstance);
            double aspectRatio = (bounds.height + 5.0) / bounds.width;

            VisualSlot.LocalPosition = new float3(0, ((((bounds.height / 2.0f) - bounds.Center.y) / 2.0f) + 5.0f) * 0.1f, 0.5f);
            VisualCaptureCamera.OrthographicSize.Value = (bounds.height + 5) / 20.0f;
            await new ToWorld();
            Bitmap2D componentImage = await VisualCaptureCamera.RenderToBitmap(new int2(400, (int)(400 * aspectRatio)));
            componentImage.Save($"{WikiAssetGenerator.BasePath}\\SyncTypes\\{typeSafeName}SyncType.png", 100, true);
            VisualSlot.DestroyChildren();
        }

        public override async Task GenerateData(object typeInstance, Type neosType, bool force = false)
        {

            SyncObject targetInstance = typeInstance as SyncObject;
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
            infoboxBuilder.AppendLine($"|Image={typeSafeName}SyncType.png");
            infoboxBuilder.AppendLine($"|Name={typeName}");
            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:2-->");
            infoboxBuilder.AppendLine("== Fields ==");
            infoboxBuilder.AppendLine("{{Table ComponentFields");

            BuildWorkerSyncMembers(infoboxBuilder, targetInstance, 0);

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
            infoboxBuilder.AppendLine("[[Category:SyncTypeStubs]]");
            if (neosType.IsGenericType)
                infoboxBuilder.AppendLine("[[Category:Generics{{#translation:}}]]");
            infoboxBuilder.AppendLine("[[Category:SyncTypes{{#translation:}}|" + typeName + "]]");
            foreach (string path in typeCategory.Paths)
            {
                infoboxBuilder.AppendLine("[[Category:SyncTypes:" + path.Replace('/', ':') + "{{#translation:}}|" + typeName + "]]");

            }

            using (StreamWriter fileWriter = new StreamWriter($"{WikiAssetGenerator.BasePath}\\SyncTypes\\{typeSafeName}.txt"))
            {
                await fileWriter.WriteAsync(infoboxBuilder.ToString());
            }
        }

        public override bool NeedsVisual(string typeSafeName, Category typeCategory = null)
        {
            return !File.Exists($"{WikiAssetGenerator.BasePath}\\SyncTypes\\{typeSafeName}SyncType.png");
        }

        public override bool NeedsData(string typeSafeName, Category typeCategory)
        {
            return !File.Exists($"{WikiAssetGenerator.BasePath}\\SyncTypes\\{typeSafeName}.txt");
        }

        private async Task<Rect> BuildSyncMemberUI(SyncObject targetInstance)
        {
            UIBuilder ui = new UIBuilder(VisualSlot, 800, 5000, 0.1f);
            ui.Style.MinHeight = 30f;
            ui.Style.ForceExpandHeight = false;
            ui.Image(new color(141 / 255.0f, 186 / 255.0f, 104 / 255.0f));
            ui.VerticalLayout(4f, 0, Alignment.TopLeft);
            ui.Style.MinHeight = 30f;
            ui.Style.PreferredHeight = 30f;
            ui.Style.ForceExpandHeight = true;
            VerticalLayout content = ui.VerticalLayout(4f, 10f, Alignment.TopLeft);
            /*ui.Style.ChildAlignment = Alignment.TopLeft;
            {
                ui.HorizontalLayout(4f);
                ui.Style.FlexibleWidth = 1000f;
                ui.Button("<b>" + targetInstance.GetType().GetNiceName() + "</b>", color.White);

                ui.Style.FlexibleWidth = 0.0f;
                ui.Style.MinWidth = 32f;

                ui.Button("D", MathX.Lerp(color.Green, color.White, 0.7f));
                ui.Button("X", MathX.Lerp(color.Red, color.White, 0.7f));
                ui.NestOut();
            }*/
            //WorkerInspector.BuildInspectorUI(targetInstance, ui);
            SyncMemberEditorBuilder.Build(targetInstance, null, null, ui);
            await new Updates(5);
            return content.RectTransform.BoundingRect;
        }
    }
}
