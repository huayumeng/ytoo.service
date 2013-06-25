﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Web.Mvc;
using Yintai.Architecture.Common.Models;
using Yintai.Hangzhou.Cms.WebSiteV1.Models;
using Yintai.Hangzhou.Cms.WebSiteV1.Util;
using Yintai.Hangzhou.Data.Models;
using Yintai.Hangzhou.Model;
using Yintai.Hangzhou.Model.Enums;
using Yintai.Hangzhou.Model.Filters;
using Yintai.Hangzhou.Repository.Contract;
using Yintai.Hangzhou.Service.Logic;

namespace Yintai.Hangzhou.Cms.WebSiteV1.Controllers
{
    [AdminAuthorize]
    public class OrderController : UserController
    {
        private IOrderRepository _orderRepo;
        private IUserAuthRepository _userAuthRepo;
        private ICustomerRepository _customerRepo;
        private IOrderLogRepository _orderLogRepo;
        private IOrderItemRepository _orderItemRepo;
        private IInboundPackRepository _inboundRepo;
        private IOutboundItemRepository _outbounditemRepo;
        private IOutboundRepository _outboundRepo;
        private ISectionRepository _sectionRepo;
        private IRMARepository _rmaRepo;
        private IRMALogRepository _rmalogRepo;
        private IRMAItemRepository _rmaitemRepo;
        public OrderController(IOrderRepository orderRepo,
            IUserAuthRepository userAuthRepo,
            ICustomerRepository customerRepo,
            IOrderLogRepository orderLogRepo,
            IOrderItemRepository orderItemRepo,
            IInboundPackRepository inboundRepo,
            IOutboundRepository outboundRepo,
            IOutboundItemRepository outbounditemRepo,
            ISectionRepository sectionRepo,
            IRMARepository rmaRepo,
            IRMALogRepository rmalogRepo,
            IRMAItemRepository rmaitemRepo
            )
        {
            _orderRepo = orderRepo;
            _userAuthRepo = userAuthRepo;
            _customerRepo = customerRepo;
            _orderLogRepo = orderLogRepo;
            _orderItemRepo = orderItemRepo;
            _inboundRepo = inboundRepo;
            _outboundRepo = outboundRepo;
            _outbounditemRepo = outbounditemRepo;
            _sectionRepo = sectionRepo;
            _rmaRepo = rmaRepo;
            _rmalogRepo = rmalogRepo;
            _rmaitemRepo = rmaitemRepo;
        }
        public ActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public ActionResult List(OrderSearchOption search, PagerRequest request)
        {
            return View(search);
        }
        [HttpPost]
        public JsonResult ListP(OrderSearchOption search, PagerRequest request)
        {
            int totalCount;
            search.CurrentUser = CurrentUser.CustomerId;
            search.CurrentUserRole = CurrentUser.Role;
            var dbContext = _orderRepo.Context;
            var linq = dbContext.Set<OrderEntity>().Where(p => (string.IsNullOrEmpty(search.OrderNo) || p.OrderNo == search.OrderNo) &&
                (!search.CustomerId.HasValue || p.CustomerId == search.CustomerId.Value) &&
                (!search.Status.HasValue || p.Status == (int)search.Status.Value) &&
                     (!search.Store.HasValue || p.StoreId == search.Store.Value) &&
                      (!search.Brand.HasValue || p.BrandId == search.Brand.Value) &&
                      (!search.FromDate.HasValue || p.CreateDate >= search.FromDate.Value) &&
                      (!search.ToDate.HasValue || p.CreateDate <= search.ToDate.Value) &&
                p.Status != (int)DataStatus.Deleted);
            linq = _userAuthRepo.AuthFilter(linq, search.CurrentUser, search.CurrentUserRole) as IQueryable<OrderEntity>;

            var linq2 = linq.Join(dbContext.Set<UserEntity>().Where(u => u.Status != (int)DataStatus.Deleted),
                    o => o.CustomerId,
                    i => i.Id,
                    (o, i) => new { O = o, C = i })
                .GroupJoin(dbContext.Set<RMAEntity>(),
                    o=>o.O.OrderNo,
                    i=>i.OrderNo,
                    (o,i)=>new {O=o.O,C=o.C,RMA=i})
                .GroupJoin(dbContext.Set<ShipViaEntity>().Where(s => s.Status != (int)DataStatus.Deleted),
                    o => o.O.ShippingVia,
                    i => i.Id,
                    (o, i) => new { O = o.O, C = o.C,RMA=o.RMA, S = i.FirstOrDefault() })
                .GroupJoin(dbContext.Set<StoreEntity>(),
                    o => o.O.StoreId,
                    i => i.Id,
                    (o, i) => new { O = o.O, C = o.C,RMA=o.RMA, S = o.S, Store = i.FirstOrDefault() })
                .GroupJoin(dbContext.Set<BrandEntity>(),
                    o => o.O.BrandId,
                    i => i.Id,
                    (o, i) => new { O = o.O, C = o.C,RMA=o.RMA, S = o.S, Store = o.Store, Brand = i.FirstOrDefault() }).OrderByDescending(o => o.O.CreateDate)
                    ;


            totalCount = linq2.Count();

            var skipCount = (request.PageIndex - 1) * request.PageSize;

            var linq3 = skipCount == 0 ? linq2.Take(request.PageSize) : linq2.Skip(skipCount).Take(request.PageSize);


            var vo = from l in linq3.ToList()
                     select new OrderViewModel().FromEntity<OrderViewModel>(l.O, p =>
                     {
                         p.ShippingViaMethod = l.S;
                         p.Customer = new CustomerViewModel().FromEntity<CustomerViewModel>(l.C);
                         p.StoreName = l.Store.Name;
                         p.ShippingViaMethod_Name = l.S==null?string.Empty:l.S.Name;
                         if (l.RMA != null)
                             p.RMAs = l.RMA.OrderByDescending(r=>r.CreateDate).Select(r => new RMAViewModel().FromEntity<RMAViewModel>(r));

                     });

            var v = new Pager<OrderViewModel>(request, totalCount) { Data = vo.ToList() };
            return Json(v);
        }
        public ActionResult List(OrderSearchOption search)
        {

            return View(search);
        }

        public ActionResult Details(string orderNo)
        {
            var linq = _orderRepo.Get(o => o.OrderNo == orderNo);
            linq = _userAuthRepo.AuthFilter(linq, CurrentUser.CustomerId, CurrentUser.Role) as IQueryable<OrderEntity>;
            var order = linq.FirstOrDefault();
            if (order == null)
            {
                throw new ArgumentNullException("orderNo");
            }
            var model = new OrderViewModel().FromEntity<OrderViewModel>(order, o =>
            {
                o.Customer = new CustomerViewModel().FromEntity<CustomerViewModel>(_customerRepo.Find(o.CustomerId));
                o.Logs = _orderLogRepo.Get(l => l.OrderNo == o.OrderNo).OrderByDescending(ol=>ol.CreateDate).ToList().Select(l => new OrderLogViewModel().FromEntity<OrderLogViewModel>(l));
                o.Items = _orderRepo.Context.Set<OrderItemEntity>().Where(oi => oi.OrderNo == o.OrderNo && oi.Status != (int)DataStatus.Deleted)
                        .ToList()
                        .Select(oi => new OrderItemViewModel().FromEntity<OrderItemViewModel>(oi));
                o.ShippingViaMethod = _orderRepo.Context.Set<ShipViaEntity>().Where(s => s.Id == o.ShippingVia).FirstOrDefault();
                o.StoreName = _orderRepo.Context.Set<StoreEntity>().Where(s => s.Id == o.StoreId).FirstOrDefault().Name;
                o.RMAs = _rmaRepo.Get(r => r.OrderNo == o.OrderNo)
                         .GroupJoin(_rmaitemRepo.GetAll(), ou => ou.RMANo, i => i.RMANo, (ou, i) => new { R = ou, RI = i.FirstOrDefault() })
                         .OrderByDescending(r=>r.R.CreateDate)
                         .ToList()
                         .Select(r => new RMAViewModel().FromEntity<RMAViewModel>(r.R, rm => {
                             rm.Item = new RMAItemViewModel().FromEntity<RMAItemViewModel>(r.RI);
                         }));
                        

            });
            return View(model);
        }
        public ActionResult ChangeStoreItem(string orderNo)
        {
            var order = _orderRepo.Context.Set<OrderItemEntity>().Where(o => o.OrderNo == orderNo)
                .GroupJoin(_orderRepo.Context.Set<ResourceEntity>().Where(r => r.Status != (int)DataStatus.Deleted && r.SourceType == (int)SourceType.Product),
                    o => o.ProductId,
                    i => i.SourceId,
                    (o, i) => new { O = o, R = i.FirstOrDefault() });
            var model = new ConfirmStoreItemViewModel();
            model.Items = order.ToList().Select(o => new OrderItemViewModel().FromEntity<OrderItemViewModel>(o.O, p =>
            {
                p.ProductResource = new ResourceViewModel().FromEntity<ResourceViewModel>(o.R);
            }));
            ViewBag.OrderNo = orderNo;
            return View(model);
        }
        [HttpPost]
        public ActionResult ChangeStoreItem(ConfirmStoreItemViewModel model, string orderNo)
        {
            if (!ModelState.IsValid)
            {
                foreach (var item in model.Items)
                {
                    item.ProductResource = new ResourceViewModel().FromEntity<ResourceViewModel>(_orderRepo.Context.Set<ResourceEntity>().Where(r => r.SourceType == (int)SourceType.Product && r.SourceId == item.ProductId).FirstOrDefault());
                }
                ViewBag.OrderNo = orderNo;
                return View(model);
            }
            var orderEntity = _orderRepo.Get(o => o.OrderNo == orderNo).FirstOrDefault();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ModelState.AddModelError(string.Empty, errorMsg);
                return View(model);
            }
            using (var ts = new TransactionScope())
            {
                foreach (var item in model.Items)
                {
                    var itemEntity = _orderItemRepo.Find(item.Id);
                    itemEntity.StoreItemNo = item.StoreItemNo;
                    itemEntity.StoreItemDesc = item.StoreItemDesc;
                    itemEntity.UpdateDate = DateTime.Now;
                    itemEntity.UpdateUser = CurrentUser.CustomerId;
                    _orderItemRepo.Update(itemEntity);
                }
                orderEntity.Status = (int)OrderStatus.AgentConfirmed;
                orderEntity.UpdateDate = DateTime.Now;
                orderEntity.UpdateUser = CurrentUser.CustomerId;
                _orderRepo.Update(orderEntity);
                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "专柜确认商品编码。",
                    Type = (int)OrderOpera.FromOperator
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }
        public ActionResult Shipping(string orderNo)
        {
            var order = _orderRepo.Context.Set<OrderItemEntity>().Where(o => o.OrderNo == orderNo).FirstOrDefault();
            if (order == null)
            {
                throw new ArgumentNullException("orderNo");
            }
            var model = new OrderViewModel().FromEntity<OrderViewModel>(order);
            ViewBag.OrderNo = orderNo;
            return View(model);
        }
        [HttpPost]
        public ActionResult Shipping(OrderViewModel item, string orderNo)
        {
            item.Status = (int)OrderStatus.Shipped;
            if (!ModelState.IsValid)
            {
                var order = _orderRepo.Context.Set<OrderItemEntity>().Where(o => o.OrderNo == orderNo).FirstOrDefault();
                ViewBag.OrderNo = orderNo;
                return View(new OrderViewModel().FromEntity<OrderViewModel>(order));
            }
            var itemEntity = _orderRepo.Get(o => o.OrderNo == orderNo).First();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(itemEntity.StoreId, itemEntity.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ModelState.AddModelError(string.Empty, errorMsg);
                ViewBag.OrderNo = orderNo;
                return View(new OrderViewModel().FromEntity<OrderViewModel>(itemEntity));
            }
            if (itemEntity.Status != (int)OrderStatus.PreparePack)
            {
                ViewBag.OrderNo = orderNo;
                ViewBag.Message = "订单状态无法发货";
                return RedirectToAction("Details", new { OrderNo = orderNo });
            }
            using (var ts = new TransactionScope())
            {
                var outboundEntity = _outboundRepo.Get(o => o.SourceNo == orderNo && o.SourceType == (int)OutboundType.Order).FirstOrDefault();
                outboundEntity.Status = (int)OutboundStatus.Shipped;
                outboundEntity.UpdateDate = DateTime.Now;
                outboundEntity.UpdateUser = CurrentUser.CustomerId;
                _outboundRepo.Update(outboundEntity);

                itemEntity.ShippingVia = item.ShippingVia;
                itemEntity.ShippingNo = item.ShippingNo;
                itemEntity.Status = (int)OrderStatus.Shipped;
                itemEntity.UpdateDate = DateTime.Now;
                itemEntity.UpdateUser = CurrentUser.CustomerId;
                _orderRepo.Update(itemEntity);

                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "填写快递信息并发货。",
                    Type = (int)OrderOpera.FromOperator
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }

        public ActionResult Reject(string orderNo)
        {
            var order = _orderRepo.Context.Set<OrderItemEntity>().Where(o => o.OrderNo == orderNo).FirstOrDefault();
            if (order == null)
            {
                throw new ArgumentNullException("orderNo");
            }
            ViewBag.OrderNo = orderNo;
            return View(new InboundPackViewModel());
        }
        [HttpPost]
        public ActionResult Reject(InboundPackViewModel item, string orderNo)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.OrderNo = orderNo;
                return View(item);
            }
            var itemEntity = _orderRepo.Get(o => o.OrderNo == orderNo).First();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(itemEntity.StoreId, itemEntity.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ModelState.AddModelError(string.Empty, errorMsg);
                return View(item);
            }
            using (var ts = new TransactionScope())
            {
                //step1：update order status

             
                itemEntity.Status = (int)OrderStatus.CustomerConfirmed;
                itemEntity.UpdateDate = DateTime.Now;
                itemEntity.UpdateUser = CurrentUser.CustomerId;
                _orderRepo.Update(itemEntity);
                //step2: persist inbound package
                _inboundRepo.Insert(new InboundPackageEntity()
                {
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    ShippingNo = item.ShippingNo,
                    ShippingVia = item.ShippingVia,
                    SourceNo = orderNo,
                    SourceType = (int)InboundType.CustomerReject

                });

                //step3: log it
                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "客户拒收。",
                    Type = (int)OrderOpera.FromOperator
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }
        public ActionResult PreparePack(string orderNo)
        {
            var orderEntity = _orderRepo.Get(o => o.OrderNo == orderNo).First();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ViewBag.Message = errorMsg;
                return RedirectToAction("Details", new { OrderNo = orderNo});
            }
            if (orderEntity.Status != (int)OrderStatus.OrderPrinted)
            {
                ViewBag.OrderNo = orderNo;
                ViewBag.Message = "订单状态无法打包";
                return RedirectToAction("Details", new { OrderNo = orderNo });  
            }
            using (var ts = new TransactionScope())
            {
                orderEntity.Status = (int)OrderStatus.PreparePack;
                orderEntity.UpdateUser = CurrentUser.CustomerId;
                orderEntity.UpdateDate = DateTime.Now;
                _orderRepo.Update(orderEntity);
                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "准备打包。",
                    Type = (int)OrderOpera.FromCustomer
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }
        public ActionResult Received(string orderNo)
        {
            var orderEntity = _orderRepo.Get(o => o.OrderNo == orderNo).First();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ViewBag.Message = errorMsg;
                return RedirectToAction("Details", new { OrderNo = orderNo });
            }
            using (var ts = new TransactionScope())
            {
                orderEntity.Status = (int)OrderStatus.CustomerReceived;
                orderEntity.UpdateUser = CurrentUser.CustomerId;
                orderEntity.UpdateDate = DateTime.Now;
                _orderRepo.Update(orderEntity);
                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "客户已签收。",
                    Type = (int)OrderOpera.CustomerReceived
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }
        public ActionResult Convert2Sale(string orderNo)
        {
            var orderEntity = _orderRepo.Get(o => o.OrderNo == orderNo).First();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ViewBag.Message = errorMsg;
                return RedirectToAction("Details", new { OrderNo = orderNo });
            }
            using (var ts = new TransactionScope())
            {
                orderEntity.Status = (int)OrderStatus.Convert2Sales;
                orderEntity.UpdateUser = CurrentUser.CustomerId;
                orderEntity.UpdateDate = DateTime.Now;
                _orderRepo.Update(orderEntity);
                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "转为销售。",
                    Type = (int)OrderOpera.FromCustomer
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }

        [HttpPost]
        public ActionResult Void(string orderNo)
        {
            var order = _orderRepo.Get(o => o.OrderNo == orderNo).First();
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(order.StoreId, order.BrandId, out errorMsg))
            {
                ViewBag.OrderNo = orderNo;
                ViewBag.Message = errorMsg;
                return RedirectToAction("Details", new { OrderNo = orderNo });
            }
            using (var ts = new TransactionScope())
            {

                order.Status = (int)OrderStatus.Void;
                order.UpdateDate = DateTime.Now;
                order.UpdateUser = CurrentUser.CustomerId;
                _orderRepo.Update(order);

                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "作废订单。",
                    Type = (int)OrderOpera.FromOperator
                });
                ts.Complete();
            }
            return RedirectToAction("Details", new { OrderNo = orderNo });
        }

        public ActionResult Reject2Customer(string rmano)
        {
            var rmaEntity = _rmaRepo.Get(o => o.RMANo == rmano).FirstOrDefault();
            if (rmaEntity == null)
                throw new ArgumentException("rmano");
            if (rmaEntity.Status != (int)RMAStatus.Created)
                throw new ArgumentException("rma status is not correct");
            return View(new RMAViewModel().FromEntity<RMAViewModel>(rmaEntity));

        }
        [HttpPost]
        public ActionResult Reject2Customer(RMAViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);
            var rmaEntity = _rmaRepo.Get(o => o.RMANo == model.RMANo).FirstOrDefault();
            if (rmaEntity == null)
                throw new ArgumentException("rmano");
            string errorMsg;
            var orderEntity = _orderRepo.Get(o => o.OrderNo == rmaEntity.OrderNo).First();
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                ModelState.AddModelError(string.Empty, errorMsg);
                return View(model);
            }
            using (var ts = new TransactionScope())
            {
                rmaEntity.RejectReason = model.RejectReason;
                rmaEntity.UpdateDate = DateTime.Now;
                rmaEntity.Status = (int)RMAStatus.Reject2Customer;
                rmaEntity.UpdateUser = CurrentUser.CustomerId;
                _rmaRepo.Update(rmaEntity);

                _rmalogRepo.Insert(new RMALogEntity()
                {
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    Operation = "不符合退货要求，退回客户！",
                    RMANo = model.RMANo
                });
                ts.Complete();
            }
            return RedirectToAction("details", new { orderNo = model.OrderNo });

        }

        public ActionResult VoidRMA(string rmano)
        {
            var rmaEntity = _rmaRepo.Get(o => o.RMANo == rmano).FirstOrDefault();
            if (rmaEntity == null)
                throw new ArgumentException("rmano");
            if (rmaEntity.Status != (int)RMAStatus.Created)
                throw new ArgumentException("cannot void!");
            string errorMsg;
            var orderEntity = _orderRepo.Get(o => o.OrderNo == rmaEntity.OrderNo).First();
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                ViewBag.Message = errorMsg;
                return RedirectToAction("details", new { orderNo = rmaEntity.OrderNo });
            }
            using (var ts = new TransactionScope())
            {
                rmaEntity.UpdateDate = DateTime.Now;
                rmaEntity.Status = (int)RMAStatus.Void;
                rmaEntity.UpdateUser = CurrentUser.CustomerId;
                _rmaRepo.Update(rmaEntity);

                _rmalogRepo.Insert(new RMALogEntity()
                {
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    Operation = "作废订单！",
                    RMANo = rmano
                });
                ts.Complete();
            }
            return RedirectToAction("details", new { orderNo = rmaEntity.OrderNo });
        }

        public ActionResult PrintRMA(string rmaNo)
        {
            var rmaEntity = _rmaRepo.Get(r=>r.RMANo == rmaNo)
                           .GroupJoin(_rmaitemRepo.Get(ri=>ri.RMANo == rmaNo),
                                    o=>o.RMANo,
                                    i=>i.RMANo,
                                    (o,i)=>new {R=o,RI=i.FirstOrDefault()})
                            .FirstOrDefault();
            string errorMsg;
            var orderEntity = _orderRepo.Get(o => o.OrderNo == rmaEntity.R.OrderNo).First();
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                throw new ArgumentException(errorMsg);
            }
            var model = new RMAViewModel().FromEntity<RMAViewModel>(rmaEntity.R, p => {
                p.Item = new RMAItemViewModel().FromEntity<RMAItemViewModel>(rmaEntity.RI);
            });
            return View(model);
        }
        [HttpPost]
        public JsonResult PrintRMA(RMAViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return FailResponse(ModelState.First(e => e.Value.Errors.Count() > 0).Value.Errors[0].ErrorMessage);
            }
            string errorMsg;
            var confirmModel = _orderRepo.Get(o => o.OrderNo == model.OrderNo).FirstOrDefault();
            if (!OrderViewModel.IsAuthorized(confirmModel.StoreId, confirmModel.BrandId, out errorMsg))
            {
                return FailResponse(errorMsg);
            }
            using (var ts = new TransactionScope())
            {
                var entity = _rmaRepo.Get(r => r.RMANo == model.RMANo).First();
                entity.GiftReason = model.GiftReason;
                entity.PostalFeeReason = model.PostalFeeReason;
                entity.InvoiceReason = model.InvoiceReason;
                entity.RebatePointReason = model.RebatePointReason;
                entity.rebatepostfee = model.rebatepostfee;
                entity.chargepostfee = model.chargepostfee;
                entity.ChargeGiftFee = model.ChargeGiftFee;
                entity.ActualAmount = entity.RMAAmount - (entity.chargepostfee ?? 0) + (entity.rebatepostfee ?? 0) - (entity.ChargeGiftFee ?? 0);
                entity.Status = (int)RMAStatus.PrintRMA;
                entity.UpdateDate = DateTime.Now;
                entity.UpdateUser = CurrentUser.CustomerId;
                _rmaRepo.Update(entity);

                _rmalogRepo.Insert(new RMALogEntity()
                {
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    Operation = "打印退货单",
                    RMANo = model.RMANo
                });

                ts.Complete();

            }
            return SuccessResponse();
        }
        public ActionResult CreateRMA(string orderNo)
        {
            string errorMsg = string.Empty;
            var orderEntity = _orderRepo.Get(o => o.OrderNo == orderNo && o.Status == (int)OrderStatus.Convert2Sales).FirstOrDefault();
            if (orderEntity == null)
                errorMsg = "订单状态不能创建退货单！";
            var rmaEntity = _rmaRepo.Get(r => r.OrderNo == orderNo && (r.Status == (int)RMAStatus.Created || r.Status == (int)RMAStatus.PackageReceived))
                                .FirstOrDefault();
            if (rmaEntity != null)
                errorMsg += "订单已经创建了退货单，请将已有退货单作废，然后再创建！";
            if (!string.IsNullOrEmpty(errorMsg))
            {
                ViewBag.Message = errorMsg;
                return RedirectToAction("details", new { OrderNo = orderNo });
            }
            if (!OrderViewModel.IsAuthorized(orderEntity.StoreId, orderEntity.BrandId, out errorMsg))
            {
                throw new ArgumentException(errorMsg);
            }
            var orderItemEntity = _orderItemRepo.Get(o => o.Status != (int)DataStatus.Deleted && o.OrderNo == orderNo).FirstOrDefault();
            using (var ts = new TransactionScope())
            {
                var newEntity = _rmaRepo.Insert(new RMAEntity()
                {
                    RMAAmount = orderEntity.TotalAmount,
                    Status = (int)RMAStatus.Created,
                    OrderNo = orderEntity.OrderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    Reason = "现场退货",
                    RMANo = OrderRule.CreateRMACode(),
                    RMAType = (int)RMAType.FromOffline,
                    UpdateDate = DateTime.Now,
                    UpdateUser = CurrentUser.CustomerId
                });
                _rmaitemRepo.Insert(new RMAItemEntity()
                {
                    ItemPrice = orderItemEntity.ItemPrice,
                    ProductDesc = orderItemEntity.ProductDesc,
                    ProductId = orderItemEntity.ProductId,
                    Quantity = orderItemEntity.Quantity,
                    RMANo = newEntity.RMANo,
                    Status = (int)DataStatus.Normal,
                    UnitPrice = orderItemEntity.UnitPrice,
                    ExtendPrice = orderItemEntity.ExtendPrice,
                    CreateDate = DateTime.Now,
                    StoreDesc = orderItemEntity.StoreItemDesc,
                    StoreItem = orderItemEntity.StoreItemNo,
                    UpdateDate = DateTime.Now
                });
                _rmalogRepo.Insert(new RMALogEntity()
                {
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    Operation = "创建线下退货单",
                    RMANo = newEntity.RMANo
                });
                ts.Complete();
            }
            return RedirectToAction("details", new { OrderNo = orderNo});


        }
        #region Reports to print
        public ActionResult PrintOrder(string orderNo)
        {
            if (string.IsNullOrEmpty(orderNo))
                throw new ArgumentNullException("orderNo");
            var confirmModel = _orderRepo.Get(o => o.OrderNo == orderNo).FirstOrDefault();
            if (null == confirmModel)
                throw new ArgumentException("订单号不存在！");
            var sectionModel = _orderRepo.Context.Set<SectionEntity>().Where(s => s.StoreId == confirmModel.StoreId && s.BrandId == confirmModel.BrandId).FirstOrDefault();
            if (null == sectionModel)
                throw new ArgumentException("专柜不存在！");
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(confirmModel.StoreId, confirmModel.BrandId, out errorMsg))
            {
                throw new ArgumentException(errorMsg);
            }
            var confirmItemsModel = _orderItemRepo.Get(o => o.OrderNo == orderNo && o.Status != (int)DataStatus.Deleted).ToList();
            var result = RenderReport("confirmorderreport", r =>
            {
                r.Database.Tables[0].SetDataSource(new[] { 
                    new OrderConfirmReportViewModel(){
                         Memo = confirmModel.Memo,
                          NeedInvoice = confirmModel.NeedInvoice.ToReportString(),
                           OrderNo = confirmModel.OrderNo,
                            SecionPerson = sectionModel.ContactPerson,
                             SectionName = sectionModel.Name,
                              SectionPhone = sectionModel.ContactPhone,
                               ShippingAddress = confirmModel.ShippingAddress,
                                ShippingContactPerson = confirmModel.ShippingContactPerson,
                                 ShippingContactPhone = confirmModel.ShippingContactPhone,
                                  ShippingZipCode = confirmModel.ShippingZipCode,
                                   SectionId = sectionModel.Id.ToString()
                    }
                });
                int index = 0;
                r.Database.Tables[1]
                    .SetDataSource(confirmItemsModel.Select(i => new OrderConfirmItemReportViewModel().FromEntity<OrderConfirmItemReportViewModel>(i, oi =>
                    {
                        oi.ItemId = index++;
                    })));
            });
            using (var ts = new TransactionScope())
            {
                confirmModel.Status = (int)OrderStatus.OrderPrinted;
                confirmModel.UpdateDate = DateTime.Now;
                confirmModel.UpdateUser = CurrentUser.CustomerId;
                _orderRepo.Update(confirmModel);

                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "打印订单。",
                    Type = (int)OrderOpera.FromOperator
                });
                ts.Complete();
            }
            return result;

        }

        public ActionResult PrintShipping(string orderNo)
        {
            if (string.IsNullOrEmpty(orderNo))
                throw new ArgumentNullException("orderNo");
            var confirmModel = _orderRepo.Get(o => o.OrderNo == orderNo).FirstOrDefault();
            if (null == confirmModel)
                throw new ArgumentException("订单号不存在！");
            string errorMsg;
            if (!OrderViewModel.IsAuthorized(confirmModel.StoreId, confirmModel.BrandId, out errorMsg))
            {
                ViewBag.Message = errorMsg;
                throw new ArgumentException(errorMsg);
            }
            //step1 :create outbound 

            if (!_outboundRepo.Get(o => o.SourceNo == orderNo && o.SourceType == (int)OutboundType.Order).Any())
            {
                using (var ts = new TransactionScope())
                {
                    var outboundEntity = _outboundRepo.Insert(new OutboundEntity()
                    {
                        CreateDate = DateTime.Now,
                        CreateUser = CurrentUser.CustomerId,
                        OutboundNo = OrderRule.CreateShippingCode(confirmModel.StoreId.ToString()),
                        ShippingAddress = confirmModel.ShippingAddress,
                        ShippingContactPerson = confirmModel.ShippingContactPerson,
                        ShippingContactPhone = confirmModel.ShippingContactPhone,
                        ShippingZipCode = confirmModel.ShippingZipCode,
                        SourceNo = orderNo,
                        SourceType = (int)OutboundType.Order,
                        Status = (int)OutboundStatus.Created,
                        UpdateDate = DateTime.Now,
                        UpdateUser = CurrentUser.CustomerId
                    });
                    foreach (var orderitem in _orderItemRepo.Get(o => o.OrderNo == orderNo && o.Status != (int)DataStatus.Deleted))
                    {
                        _outbounditemRepo.Insert(new OutboundItemEntity()
                        {
                            CreateDate = DateTime.Now,
                            ItemPrice = orderitem.ItemPrice,
                            ExtendPrice = orderitem.ExtendPrice,
                            OutboundNo = outboundEntity.OutboundNo,
                            ProductDesc = orderitem.ProductDesc,
                            ProductId = orderitem.ProductId,
                            Quantity = orderitem.Quantity,
                            StoreItemDesc = orderitem.StoreItemDesc,
                            StoreItemNo = orderitem.StoreItemNo,
                            UnitPrice = orderitem.UnitPrice ?? 0m,
                            UpdateDate = DateTime.Now
                        });
                    }
                    confirmModel.Status = (int)OrderStatus.PreparePack;
                    confirmModel.UpdateDate = DateTime.Now;
                    confirmModel.UpdateUser = CurrentUser.CustomerId;
                    _orderRepo.Update(confirmModel);

                    _orderLogRepo.Insert(new OrderLogEntity()
                    {
                        OrderNo = orderNo,
                        CreateDate = DateTime.Now,
                        CreateUser = CurrentUser.CustomerId,
                        CustomerId = CurrentUser.CustomerId,
                        Operation = "打印发货单。",
                        Type = (int)OrderOpera.FromOperator
                    });
                    ts.Complete();
                }
            }
            //step2: print shipping 
            var outboundModel = _outboundRepo.Get(o => o.SourceNo == orderNo && o.SourceType == (int)OutboundType.Order).FirstOrDefault();

            var sectionModel = _sectionRepo.Get(s => s.BrandId == confirmModel.BrandId && s.StoreId == confirmModel.StoreId).FirstOrDefault();
            if (null == sectionModel)
                throw new ArgumentException("专柜不存在！");


            var confirmItemsModel = _outbounditemRepo.Get(o => o.OutboundNo == outboundModel.OutboundNo).ToList();
            return RenderReport("outboundreport", r =>
           {

               r.Database.Tables[0].SetDataSource(new[] { 
                    new OrderShipReportViewModel(){                       
                          NeedInvoice = confirmModel.NeedInvoice.ToReportString(),
                           OrderNo = confirmModel.OrderNo,
                            SecionPerson = sectionModel.ContactPerson,
                             SectionName = sectionModel.Name,
                              SectionPhone = sectionModel.ContactPhone,
                               ShippingAddress = outboundModel.ShippingAddress,
                                ShippingContactPerson = outboundModel.ShippingContactPerson,
                                 ShippingContactPhone = outboundModel.ShippingContactPhone,
                                  ShippingZipCode = outboundModel.ShippingZipCode,
                                  //todo: change it to invoice amount later
                                  InvoiceAmount = confirmModel.TotalAmount,
                                   InvoiceDetail = confirmModel.InvoiceDetail,
                                    InvoiceSubject = confirmModel.InvoiceSubject,
                                     OutboundNo = outboundModel.OutboundNo
                    }
                });
               r.Database.Tables[1]
                   .SetDataSource(confirmItemsModel.Select(i => new OrderShipItemReportViewModel().FromEntity<OrderShipItemReportViewModel>(i)));
               r.SetParameterValue("Notice", ConfigurationManager.AppSettings["OrderShipNotice"]);
               r.SetParameterValue("Operator", CurrentUser.NickName);
           });


        }

        public ActionResult PrintSales(string orderNo)
        {
            if (string.IsNullOrEmpty(orderNo))
                throw new ArgumentNullException("orderNo");
            var confirmModel = _orderRepo.Get(o => o.OrderNo == orderNo).FirstOrDefault();
            if (null == confirmModel)
                throw new ArgumentException("订单号不存在！");


            var sectionModel = _sectionRepo.Get(s => s.BrandId == confirmModel.BrandId && s.StoreId == confirmModel.StoreId).FirstOrDefault();
            if (null == sectionModel)
                throw new ArgumentException("专柜不存在！");

            string errorMsg;
            if (!OrderViewModel.IsAuthorized(confirmModel.StoreId, confirmModel.BrandId, out errorMsg))
            {
                ViewBag.Message = errorMsg;
                throw new ArgumentException(errorMsg);
            }
            var confirmItemsModel = this._orderItemRepo.Get(o => o.OrderNo == orderNo).ToList();
            var result = RenderReport("convert2salelistreport", r =>
            {

                r.Database.Tables[0]
                    .SetDataSource(confirmItemsModel.Select(i => new Order2SaleReportViewModel().FromEntity<Order2SaleReportViewModel>(i, s =>
                    {
                        s.SectionName = sectionModel.Name;
                        s.SectionPerson = sectionModel.ContactPerson;
                        var receivedLog = _orderLogRepo.Get(o => o.OrderNo == orderNo && o.Type == (int)OrderOpera.CustomerReceived).FirstOrDefault();
                        s.RecDate = receivedLog == null ? DateTime.MinValue : receivedLog.CreateDate;

                    })));
                r.SetParameterValue("Operator", CurrentUser.NickName);
            });
            using (var ts = new TransactionScope())
            {
                confirmModel.Status = (int)OrderStatus.Convert2Sales;
                confirmModel.UpdateDate = DateTime.Now;
                confirmModel.UpdateUser = CurrentUser.CustomerId;
                _orderRepo.Update(confirmModel);

                _orderLogRepo.Insert(new OrderLogEntity()
                {
                    OrderNo = orderNo,
                    CreateDate = DateTime.Now,
                    CreateUser = CurrentUser.CustomerId,
                    CustomerId = CurrentUser.CustomerId,
                    Operation = "打印销售单转化销售。",
                    Type = (int)OrderOpera.FromOperator
                });
                ts.Complete();
            }
            return result;


        }
        public ActionResult PrintRMAReport(string rmaNo)
        {
            var entity = _rmaRepo.Get(r => r.RMANo == rmaNo).First();
            return  RenderReport("rmareport", r =>
                {
                  
                    r.Database.Tables[0]
                        .SetDataSource(new []{
                            new RMAReportViewModel().FromEntity<RMAReportViewModel>(entity, rm =>
                        {
                            var rmaItemEntity = _rmaitemRepo.Get(ri => ri.RMANo == rmaNo).FirstOrDefault();
                            if (rmaItemEntity == null)
                                return;
                            rm.StoreItem = rmaItemEntity.StoreItem;
                            rm.UnitPrice = rmaItemEntity.UnitPrice==null?string.Empty:rmaItemEntity.UnitPrice.ToString();
                            rm.ItemPrice = rmaItemEntity.ItemPrice;
                            rm.Quantity = rmaItemEntity.Quantity;
                        })});
                    r.SetParameterValue("Operator", CurrentUser.NickName);
                });
        }
      

        #endregion

    }
}