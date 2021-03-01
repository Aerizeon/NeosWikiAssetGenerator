using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NeosWikiAssetGenerator
{
    public class NeosTypeProcessor : INeosTypeProcessor
    {
        public static string BasePath = "D:\\NeosWiki\\NewGenerator\\";
        public static List<string> TypeBlacklist = new List<string>();
        public static Camera VisualCaptureCamera;
        public static Slot VisualSlot;
        public static Slot InstanceSlot;

        public Dictionary<string, string> Overloads { get; set; } = new Dictionary<string, string>();

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
            throw new NotImplementedException();
        }

        public virtual Task GenerateWikiData(object typeInstance, Type neosType, bool force = false)
        {
            throw new NotImplementedException();
        }

        public virtual void DestroyInstance(object typeInstance)
        {

        }
        public virtual bool NeedsVisual(string typeSafeName, Category typeCategory)
        {
            return true;
        }
        public virtual bool NeedsWikiData(string typeSafeName, Category typeCategory)
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
    }
}
