using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using System;

namespace Monobelisk
{
    public class CustomWoodsFile : WoodsFile
    {
        public CustomWoodsFile(string filePath, FileUsage usage, bool readOnly) : base(filePath, usage, readOnly)
        {
            if (!Load(filePath, usage, readOnly))
            {
                throw new Exception("Failed to load custom WOODS.WLD file.");
            }
        }
    }
}

