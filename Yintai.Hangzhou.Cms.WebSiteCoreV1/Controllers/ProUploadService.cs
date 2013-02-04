﻿
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using Yintai.Architecture.Framework.ServiceLocation;
using Yintai.Architecture.ImageTool.Models;
using Yintai.Hangzhou.Cms.WebSiteCoreV1.Controllers;
using Yintai.Hangzhou.Cms.WebSiteCoreV1.Models;
using Yintai.Hangzhou.Model.Enums;
using Yintai.Hangzhou.Service.Contract;

namespace Yintai.Hangzhou.Cms.WebSiteCoreV1.Controllers
{
    public class ProUploadService
    {
        private string _filePath;
        private string _Session_Key = "probulksession";
        private ProBulkUploadController _context;
        private Hangzhou.Data.Models.YintaiHangzhouContext _dbContext;
        private IResourceService _resourceService;
        private static Dictionary<string, Type> cols = new Dictionary<string, Type>() { 
               { "name",typeof(string)},
               {"BrandName",typeof(string)},
               { "Description",typeof(string)},
               { "Price",typeof(decimal)},
               { "DescripOfPromotion",typeof(string)},
               { "DescripOfProBeginDate", typeof(DateTime)},
               { "DescripOfProEndDate", typeof(DateTime)},
               { "InUserId", typeof(int)},
               { "Tag", typeof(string)},
               { "Store",typeof(string)},
               { "Promotions",typeof(string)},
               { "ItemCode", typeof(string)},
               { "Subjects", typeof(string)},
               { "UploadGroupId", typeof(int)},
               { "InDate", typeof(DateTime)},
               { "Status",typeof(int)}
            };
       
        public ProUploadService(string filePath,ProBulkUploadController context)
        {
            this._filePath = filePath;
            _dbContext = new Data.Models.YintaiHangzhouContext();
            _context = context;
            _resourceService = ServiceLocator.Current.Resolve<IResourceService>();
        }
        public ProUploadService():this(null,null)
        { 
            
        }
        public ProUploadService(ProBulkUploadController context)
            : this(null, context)
        { }
        public IEnumerable<ProductUploadInfo> Stage()
        {
            if (!EnsureProFileOnDisk())
                return null;
           var result = TransferProFileToStage();
        
           return result;
        }
        public IEnumerable<ImageUploadInfo> ImageStage()
        {
            if (!EnsureProFileOnDisk())
                return null;
            return TransferImageToStage();
        }
        public IEnumerable<ProductUploadInfo> List(int? groupId)
        {
            if (groupId == null)
                yield break;
            foreach (var p in _dbContext.ProductStages.Where(o => o.UploadGroupId == groupId.Value))
            {
                yield return new ProductUploadInfo()
                {
                    Brand = p.BrandName
                    ,
                    Descrip = p.Description
                    ,
                    DescripOfPromotion = p.DescripOfPromotion
                    ,
                    DescripOfPromotionBeginDate = p.DescripOfProBeginDate
                    ,
                    DescripOfPromotionEndDate = p.DescripOfProEndDate
                    ,
                    ItemCode = p.ItemCode
                    ,
                    Price = p.Price
                    ,
                    PromotionIds = p.Promotions
                    ,
                    Store = p.Store
                    ,
                    Tag = p.Tag
                    ,
                    Title = p.name
                    ,
                    SubjectIds = p.Subjects
                    ,
                    SessionU = p.UploadGroupId.Value
                    ,Id = p.id
                    ,GroupId = p.UploadGroupId.Value
                };
            }
        }
        internal IEnumerable<ImageUploadInfo> ListImages(int? groupId)
        {
            if (groupId == null)
                yield break;
            foreach (var img in _dbContext.ResourceStages.Where(i => i.UploadGroupId == groupId))
            {
                yield return new ImageUploadInfo() {
                     ItemCode = img.ItemCode
                     ,
                     fileSize = img.Size
                    ,
                     FileName = img.Name
                    ,
                     Width = img.Width
                    ,
                     Height = img.Height

                };
            }
        }
        private IEnumerable<ImageUploadInfo> TransferImageToStage()
        {
            FileInfo imageFile = new FileInfo(_filePath);
            int jobId = _context.JobId;

            FileInfor fileInfor = _resourceService.SaveStage(imageFile, 4, SourceType.Product);
            if (fileInfor != null)
            {
                var itemNames = Path.GetFileNameWithoutExtension(_filePath).Split('@');
                int sortOrder = 1;
                int.TryParse(itemNames.Length > 1 ? itemNames[1] : "1", out sortOrder);
                var entity = _dbContext.ResourceStages.Create();
                entity.ContentSize = fileInfor.FileSize;
                entity.ExtName = fileInfor.FileExtName;
                entity.Name = fileInfor.FileName;
                entity.Width = fileInfor.Width;
                entity.Height = fileInfor.Height;
                entity.Size = fileInfor.Width.ToString(CultureInfo.InvariantCulture) + "x" + fileInfor.Height.ToString(CultureInfo.InvariantCulture);
                entity.SortOrder = sortOrder;
                entity.ItemCode = itemNames[0] ;
                entity.InUser = _context.CurrentUser.CustomerId;
                entity.InDate = DateTime.Now;
                entity.UploadGroupId = jobId;
                _dbContext.ResourceStages.Add(entity);
                _dbContext.SaveChanges();
                yield return new ImageUploadInfo()
                {
                    ItemCode = entity.ItemCode
                    ,
                    fileSize = entity.Size
                    ,
                    FileName = entity.Name
                    ,
                    Width = entity.Width
                    ,
                    Height = entity.Height

                };
            }
            else
            {
                 yield break;
            }

        }
        public IEnumerable<ProductValidateResult> Validate()
        {
            return ValidateProFileOnStage();
        }

        private bool EnsureProFileOnDisk()
        {
            return !string.IsNullOrEmpty(_filePath) &&
                File.Exists(_filePath);

        }

        private IEnumerable<ProductValidateResult> ValidateProFileOnStage()
        {
           return _dbContext.Database.SqlQuery<ProductValidateResult>(@"exec dbo.ProductStageValidate @inUser,@jobId",
                 new[] {new SqlParameter("inUser", _context.CurrentUser.CustomerId)
                   ,new SqlParameter("jobId",_context.JobId)//todo: get from current context later
                });

        }
        private object mapCellValueToStrongType<T>(ICell cell)
        {
            object finalValue = default(T);
            if (cell == null)
                return DBNull.Value;
            switch (cell.CellType) { 
                case CellType.BOOLEAN:
                    finalValue = cell.BooleanCellValue;
                    break;
                case CellType.STRING:
                    finalValue = cell.StringCellValue;
                    break;
                case CellType.NUMERIC:
                    finalValue = cell.ToString();
                    break;
                default:
                    break;
            }
            return finalValue;
        }
        private IEnumerable<ProductUploadInfo> TransferProFileToStage()
        {
            HSSFWorkbook hssfWB;
            int jobId = _context.JobId;
            if (jobId<=0)
            {
                //create job id first
                var job = _dbContext.ProductUploadJobs.Create();
                job.FileName = Path.GetFileName(_filePath);
                job.InDate = DateTime.Now;
                job.InUser = _context.CurrentUser.CustomerId;
                _dbContext.ProductUploadJobs.Add(job);
                _dbContext.SaveChanges();
                _context.JobId = job.Id;
                jobId = job.Id;
            }
            DataTable dt = new DataTable();
          
            foreach(var col in cols.Keys)
            {
                Type colType;
                cols.TryGetValue(col,out colType);
                dt.Columns.Add(col,colType);
            }
            using (var file = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            {
                hssfWB = new HSSFWorkbook(file);
                System.Collections.IEnumerator rows = hssfWB.GetSheetAt(0).GetRowEnumerator();

                rows.MoveNext();
                while (rows.MoveNext())
                {
                    var row = (HSSFRow)rows.Current;
                    DataRow dr = dt.NewRow();
                    int i = 0;
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(1));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(7));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(2));
                    dr[i++] = mapCellValueToStrongType<decimal?>(row.GetCell(6));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(3));
                    dr[i++] = mapCellValueToStrongType<DateTime?>(row.GetCell(4));
                    dr[i++] = mapCellValueToStrongType<DateTime?>(row.GetCell(5));
                    dr[i++] = _context.CurrentUser.CustomerId; //hard code here, todo, get from current context
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(8));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(9));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(10));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(0));
                    dr[i++] = mapCellValueToStrongType<string>(row.GetCell(11));
                    dr[i++] = jobId;
                    dr[i++] = DateTime.Now;
                    dr[i++] = ProUploadStatus.ProductsOnStage;
                    dt.Rows.Add(dr);
                }
            }


            using (SqlConnection destinationConnection = new SqlConnection(_dbContext.Database.Connection.ConnectionString))
            {
                using (var bulkCopy = new SqlBulkCopy(destinationConnection))
                {
                    foreach (var col in cols.Keys)
                    {
                        bulkCopy.ColumnMappings.Add(col, col);
                    }
                    bulkCopy.DestinationTableName = "dbo.ProductStage";
                    destinationConnection.Open();
                    bulkCopy.WriteToServer(dt);
                    DeleteTempFile();
                }
            }
            return List(jobId);
        }

        private void DeleteTempFile()
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }



        internal IEnumerable<ProductPublishResult> Publish()
        {
            var result = _dbContext.Database.SqlQuery<ProductPublishResult>(@"exec dbo.ProductStagePublish2 @inUser,@jobId"
                     , new[] { new SqlParameter("inUser",_context.CurrentUser.CustomerId )
                     ,new SqlParameter("jobId",_context.JobId)});
           
           // _context.JobId = 0;
            return result;

        }




        internal IEnumerable<ProductUploadJob> JobList()
        {
            return from j in _dbContext.ProductUploadJobs
                   where j.InUser == _context.CurrentUser.CustomerId
                   orderby j.InDate descending
                   select new ProductUploadJob() { 
                     JobId = j.Id
                     ,InDate = j.InDate
                   };
        }

        internal void Delete(int p)
        {
            _dbContext.Database.ExecuteSqlCommand(@"exec dbo.ProductBulkDelete @jobId",
                new[] { new SqlParameter("@jobId", p) });
        }

        internal ProductUploadInfo UploadItemDetail(int p)
        {
            return (from i in _dbContext.ProductStages
                    where i.id == p
                    select new ProductUploadInfo() {
                         Id = i.id
                         , Brand = i.BrandName
                         , Descrip = i.Description
                         , DescripOfPromotion = i.DescripOfPromotion
                         , DescripOfPromotionBeginDate = i.DescripOfProBeginDate
                         , DescripOfPromotionEndDate = i.DescripOfProEndDate
                         , ItemCode = i.ItemCode
                         , Price = i.Price
                         , PromotionIds = i.Promotions
                         , SessionU = i.UploadGroupId.Value
                         , Store = i.Store
                         , SubjectIds = i.Subjects
                         , Tag = i.Tag
                         , Title = i.name
                         ,GroupId = i.UploadGroupId.Value
                    }).FirstOrDefault();
        }

        internal ProductUploadInfo ItemDetailUpdate(ProductUploadInfo updatedModel)
        {
            var entity = _dbContext.ProductStages.Where(i => i.id == updatedModel.Id).First();
            entity.ItemCode = updatedModel.ItemCode;
            entity.name = updatedModel.Title;
            entity.Price = updatedModel.Price;
            entity.Promotions = updatedModel.PromotionIds;
            entity.Store = updatedModel.Store;
            entity.Subjects = updatedModel.SubjectIds;
            entity.Tag = updatedModel.Tag;
            entity.BrandName = updatedModel.Brand;
            entity.DescripOfProBeginDate = updatedModel.DescripOfPromotionBeginDate;
            entity.DescripOfProEndDate = updatedModel.DescripOfPromotionEndDate;
            entity.DescripOfPromotion = updatedModel.DescripOfPromotion;
            _dbContext.SaveChanges();
            return updatedModel;
        }

        internal void DeleteItem(int id)
        {
            var entity = _dbContext.ProductStages.Find(id);
            _dbContext.ProductStages.Remove(entity);
            _dbContext.SaveChanges();
        }
    }
}