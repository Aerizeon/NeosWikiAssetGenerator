using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using BaseX;
using System.Linq;

namespace NeosWikiAssetGenerator.Type_Processors
{
    public class NeosTypeProcessor : INeosTypeProcessor
    {
        public static List<string> TypeBlacklist = new List<string>();
        public static Camera VisualCaptureCamera;
        public static Slot VisualSlot;
        public static Slot InstanceSlot;
        public static Lua LuaInstance = new Lua(LuaIntegerType.Int32, LuaFloatType.Float);
        public static dynamic LuaEnvironment = LuaInstance.CreateEnvironment();

        public Dictionary<string, Data.OverloadSetting> Overloads { get; set; } = new Dictionary<string, Data.OverloadSetting>();

        public virtual bool ValidateProcessor(Type neosType)
        {
            throw new NotImplementedException();
        }

        public virtual object CreateInstance(Type neosType)
        {
            throw new NotImplementedException();
        }
        public virtual Task GenerateVisual(object typeInstance, Type neosType, bool force = false)
        {
            return Task.CompletedTask;
        }

        public virtual Task GenerateData(object typeInstance, Type neosType, bool force = false)
        {
            return Task.CompletedTask;
        }

        public virtual void DestroyInstance(object typeInstance)
        {

        }
        public virtual bool NeedsVisual(string typeSafeName, Category typeCategory)
        {
            return true;
        }
        public virtual bool NeedsData(string typeSafeName, Category typeCategory)
        {
            return true;
        }

        public Category GetCategory(Type neosType)
        {
            return neosType.GetCustomAttribute<Category>() ?? new Category("Uncategorized");
        }

        public string GetSafeName(Type neosType)
        {
            return neosType.Name.CoerceValidFileName();
        }

        protected void BuildWorkerSyncMembers(StringBuilder infoboxBuilder, Worker target, int indexOffset = 0)
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
                                BuildWorkerSyncMembers(infoboxBuilder, syncField);
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
        protected Data.OverloadSetting GetOverload(Type neosType)
        {
            if (Overloads.TryGetValue(neosType.FullName, out Data.OverloadSetting overloadSetting))
                return overloadSetting;
            else if (neosType.IsGenericType || neosType.IsGenericTypeDefinition)
                return null;
            else
                return new Data.OverloadSetting { OverloadType = neosType.FullName };
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

        public virtual Task Finalize()
        {
            return Task.CompletedTask;
        }
    }
}
