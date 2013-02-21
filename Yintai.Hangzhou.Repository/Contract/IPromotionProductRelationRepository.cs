﻿using System.Collections.Generic;
using System.Linq;
using Yintai.Hangzhou.Data.Models;

namespace Yintai.Hangzhou.Repository.Contract
{
    public interface IPromotionProductRelationRepository : IRepository<Promotion2ProductEntity, int>
    {
        List<Promotion2ProductEntity> GetList(int promotionId);

        List<Promotion2ProductEntity> GetList4Product(List<int> productids);

        IQueryable<Promotion2ProductEntity> GetListByProduct4Linq(List<int> productids);

        IQueryable<Promotion2ProductEntity> GetListByPromotionLinq(int promotionId);

        bool Exists(int promotionid, int productid);

        void DeletedByProduct(int productId);
    }
}
