﻿
namespace Yintai.Hangzhou.Data.Models.Mapping
{
    public partial class StoreEntityMap
    {
        partial void Init()
        {
            this.Property(t => t.Longitude).HasPrecision(10, 6);
            this.Property(t => t.Latitude).HasPrecision(10, 6);
        }
    }
}
