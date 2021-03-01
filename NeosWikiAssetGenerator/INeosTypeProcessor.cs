using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeosWikiAssetGenerator
{
    public interface INeosTypeProcessor
    {
        bool ValidateProcessor(Type neosType);
        object CreateInstance(Type neosType);
        Task GenerateVisual(object typeInstance, Type neosType, bool force = false);
        Task GenerateWikiData(object typeInstance, Type neosType, bool force = false);
        void DestroyInstance(object typeInstance);
    }
}
