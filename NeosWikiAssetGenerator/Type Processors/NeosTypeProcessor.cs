using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NeosWikiAssetGenerator.Type_Processors
{
    public class NeosTypeProcessor : INeosTypeProcessor
    {
        public static string BasePath = "D:\\Data\\NeosWiki\\NewGenerator\\";
        public static List<string> TypeBlacklist = new List<string>();
        public static Camera VisualCaptureCamera;
        public static Slot VisualSlot;
        public static Slot InstanceSlot;

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

        public virtual Task Finalize()
        {
            return Task.CompletedTask;
        }
    }
}
