﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yintai.Hangzhou.Data.Models;
using Yintai.Hangzhou.Repository.Contract;

namespace Yintai.Hangzhou.Repository.Impl
{
    public class ShippingAddressRepository : RepositoryBase<ShippingAddressEntity, int>, IShippingAddressRepository
    {
    }
}
