using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
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
    public class LogixTypeProcessor : NeosTypeProcessor
    {
        static readonly MethodInfo LogixGenerateUIMethod = typeof(LogixNode).GetMethod("GenerateUI", BindingFlags.Instance | BindingFlags.NonPublic);

        public List<string> OverloadCache = new List<string>();
        public override bool ValidateProcessor(Type neosType)
        {
            NodeOverload typeOverload = neosType.GetCustomAttribute<NodeOverload>();
            Category typeCategory = GetCategory(neosType);

            if (typeOverload != null && OverloadCache.Contains(typeOverload.FunctionName))
                return false;

            if(typeCategory != null && typeCategory.Paths.Any((s) => s == "Hidden"))
                return false;

            return neosType.IsSubclassOf(typeof(Component)) &&
                neosType.IsSubclassOf(typeof(LogixNode)) &&
                !TypeBlacklist.Contains(neosType.FullName);
        }

        public override object CreateInstance(Type neosType)
        {
            Data.OverloadSetting typeOverloadSetting = GetOverload(neosType);
            if (typeOverloadSetting is null || typeOverloadSetting.OverloadType is null)
            {
                UniLog.Log($"Missing LogiX overload for {neosType.FullName}");
                return null;
            }
            return InstanceSlot.AttachComponent(typeOverloadSetting.OverloadType);
        }

        public async override Task GenerateVisual(object typeInstance, Type neosType, bool force = false)
        {
            LogixNode targetInstance = typeInstance as LogixNode;
            Category typeCategory = GetCategory(neosType);
            string typeSafeName = GetSafeName(neosType);

            if (!(force || NeedsVisual(typeSafeName, typeCategory)))
                return;

            await BuildLogiXUI(targetInstance);

            Canvas logixVisual = VisualSlot.GetComponentInChildren<Canvas>();
            float aspectRatio = logixVisual.Size.Value.y / logixVisual.Size.Value.x;
            VisualCaptureCamera.OrthographicSize.Value = logixVisual.Size.Value.y / 20.0f;
            Bitmap2D logixImage = await VisualCaptureCamera.RenderToBitmap(new int2(128, (int)(128.0 * aspectRatio)));

            foreach (string path in typeCategory.Paths)
            {
                Directory.CreateDirectory($"{WikiAssetGenerator.BasePath}\\Logix\\{path}\\");
                logixImage.Save($"{WikiAssetGenerator.BasePath}\\Logix\\{path}\\{typeSafeName}Node.png", 100, true);
            }
        }

        public async override Task GenerateData(object typeInstance, Type neosType, bool force = false)
        {
            LogixNode targetInstance = typeInstance as LogixNode;
            Category typeCategory = GetCategory(neosType);
            string typeSafeName = GetSafeName(neosType);
            string typeName = neosType.GetCustomAttribute<NodeName>()?.Name ?? StringHelper.BeautifyName(neosType.Name);

            StringBuilder infoboxBuilder = new StringBuilder();
            infoboxBuilder.AppendLine("<languages></languages>");
            infoboxBuilder.AppendLine("<translate>");
            infoboxBuilder.AppendLine("<!--T:1-->");
            infoboxBuilder.AppendLine("{{Infobox Logix Node");
            infoboxBuilder.AppendLine($"| Name = {typeName}");
            infoboxBuilder.AppendLine($"| Image =[[File: {typeSafeName}Node.png | noframe | 128px | '{typeName}' LogiX node ]]");
            int inputCtr = 0;
            IEnumerable<MethodInfo> nodeImpulseImputs = neosType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.GetCustomAttribute<ImpulseTarget>() != null);
            foreach (MethodInfo impulseTarget in nodeImpulseImputs)
            {
                infoboxBuilder.AppendLine($"| Input{inputCtr}Type = Impulse | Input{inputCtr}Name = {impulseTarget.Name}");
                inputCtr++;
            }


            ///Standard sync inputs
            IEnumerable<IInputElement> inputs = targetInstance.GetSyncMembers<IInputElement>();

            foreach (IInputElement input in inputs)
            {
                infoboxBuilder.AppendLine($"| Input{inputCtr}Type = {input.GetType().GenericTypeArguments[0].GetNiceName().UppercaseFirst()} | Input{inputCtr}Name = {input.Name}");
                inputCtr++;
            }

            int outputCtr = 0;

            FieldInfo nodeContentField = neosType.GetField("Content", BindingFlags.Public | BindingFlags.Instance);
            if (nodeContentField != null)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = { ((nodeContentField.FieldType.IsGenericType || nodeContentField.FieldType.ContainsGenericParameters) ? nodeContentField.FieldType.GenericTypeArguments[0].GetNiceName().UppercaseFirst() : nodeContentField.FieldType.GetNiceName().UppercaseFirst())} | Output{outputCtr}Name = *");
                outputCtr++;
            }
            PropertyInfo nodeContentProperty = neosType.GetProperty("Content", BindingFlags.Public | BindingFlags.Instance);
            if (nodeContentProperty != null)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = { ((nodeContentProperty.PropertyType.IsGenericType || nodeContentProperty.PropertyType.ContainsGenericParameters) ? nodeContentProperty.PropertyType.GenericTypeArguments[0].GetNiceName().UppercaseFirst() : nodeContentProperty.PropertyType.GetNiceName().UppercaseFirst())} | Output{outputCtr}Name = *");
                outputCtr++;
            }



            IEnumerable<FieldInfo> nodeOutputPassthru = neosType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(F => F.GetCustomAttribute<AsOutput>() != null);
            foreach (FieldInfo nodePassthru in nodeOutputPassthru)
            {

                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = { ((nodePassthru.FieldType.IsGenericType || nodePassthru.FieldType.ContainsGenericParameters) ? nodePassthru.FieldType.GenericTypeArguments[0].GetNiceName().UppercaseFirst() : nodePassthru.FieldType.GetNiceName().UppercaseFirst())} | Output{outputCtr}Name = {nodePassthru.Name}");
                outputCtr++;
            }



            //Impulse outputs
            IEnumerable<Impulse> impulseOutputs = targetInstance.GetSyncMembers<Impulse>();

            foreach (Impulse output in impulseOutputs)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = Impulse | Output{outputCtr}Name = {output.Name}");
                outputCtr++;
            }



            //Standard sync outputs
            IEnumerable<IOutputElement> outputs = targetInstance.GetSyncMembers<IOutputElement>();

            foreach (IOutputElement output in outputs)
            {
                infoboxBuilder.AppendLine($"| Output{outputCtr}Type = {output.GetType().GenericTypeArguments[0].GetNiceName().UppercaseFirst()} | Output{outputCtr}Name = {output.Name}");
                outputCtr++;
            }


            infoboxBuilder.AppendLine("}}");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("<!--T:2-->");
            infoboxBuilder.AppendLine("The '''" + typeName + "''' node");
            infoboxBuilder.AppendLine("== Usage == <!--T:3-->");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("== Examples == <!--T:4-->");
            infoboxBuilder.AppendLine();
            infoboxBuilder.AppendLine("== Node Menu == <!--T:5-->");

            infoboxBuilder.AppendLine("</translate>");
            infoboxBuilder.AppendLine("[[Category:LogixStubs]]");
            if (neosType.IsGenericType)
                infoboxBuilder.AppendLine("[[Category:Generics{{#translation:}}]]");
            infoboxBuilder.AppendLine("[[Category:LogiX{{#translation:}}|" + typeName + "]]");

            foreach (string path in typeCategory.Paths)
            {
                if (path != "LogiX")
                {
                    infoboxBuilder.AppendLine("[[Category:" + path.Replace('/', ':') + "{{#translation:}}|" + typeName + "]]");
                }
                infoboxBuilder.AppendLine("{{:NodeMenu" + path.Replace('/', '-').Replace("LogiX", "") + "{{#translation:}}}}");
            }

            foreach (string path in typeCategory.Paths)
            {
                using (StreamWriter fileWriter = new StreamWriter($"{WikiAssetGenerator.BasePath}\\Logix\\{path}\\{typeSafeName}.txt"))
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
            return !typeCategory.Paths.All((path) => File.Exists($"{WikiAssetGenerator.BasePath}\\Logix\\{path}\\{typeSafeName}Node.png"));
        }

        public override bool NeedsData(string typeSafeName, Category typeCategory)
        {
            return !typeCategory.Paths.All((path) => File.Exists($"{WikiAssetGenerator.BasePath}\\Logix\\{path}\\{typeSafeName}.txt"));
        }
        private async Task BuildLogiXUI(LogixNode targetInstance)
        {
            LogixGenerateUIMethod.Invoke(targetInstance, new object[] { VisualSlot, 0.0f, 0.0f });
            VisualSlot.LocalScale = new float3(100, 100, 100);
            await new Updates(10);
        }
        protected override Data.OverloadSetting GetOverload(Type neosType)
        {
            NodeOverload typeOverload = neosType.GetCustomAttribute<NodeOverload>();
            if (Overloads.TryGetValue(neosType.FullName, out Data.OverloadSetting overloadSetting))
                return overloadSetting;
            if (typeOverload != null)
            {
                if (Overloads.TryGetValue(typeOverload.FunctionName, out overloadSetting))
                    return overloadSetting;
                Type foundTypeOverload = LogixHelper.GetMatchingOverload(typeOverload.FunctionName, null, new Func<Type, int>(GetTypeRank));
                if (foundTypeOverload != null)
                {
                    OverloadCache.Add(typeOverload.FunctionName);
                    return new Data.OverloadSetting { OverloadType = foundTypeOverload?.FullName };
                }
            }
            return new Data.OverloadSetting { OverloadType = neosType.FullName };
        }
        private static int GetTypeRank(Type neosType)
        {
            Type currentType = neosType;
            if (typeof(IVector).IsAssignableFrom(neosType))
                currentType = neosType.GetVectorBaseType();
            if (currentType == typeof(dummy))
                return 0;
            if (currentType == typeof(float))
                return 1;
            if (currentType == typeof(int))
                return 2;
            return neosType.GetTypeRank() + 3;
        }
    }
}
