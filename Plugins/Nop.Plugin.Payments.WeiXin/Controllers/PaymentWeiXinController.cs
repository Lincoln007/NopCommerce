﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.WeiXin.Helpers;
using Nop.Plugin.Payments.WeiXin.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;
using System.IO;
using System.Web;
using Nop.Core.Domain.Logging;

namespace Nop.Plugin.Payments.WeiXin.Controllers
{
    public class PaymentWeiXinController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly WeiXinPaymentSettings _weiXinPaymentSettings;
        private readonly PaymentSettings _paymentSettings;

        public PaymentWeiXinController(ISettingService settingService, 
            IPaymentService paymentService, IOrderService orderService, 
            IOrderProcessingService orderProcessingService, 
            ILogger logger, IWebHelper webHelper, IWorkContext workContext,
            WeiXinPaymentSettings weiXinPaymentSettings,
            PaymentSettings paymentSettings)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._logger = logger;
            this._webHelper = webHelper;
            _workContext = workContext;
            this._weiXinPaymentSettings = weiXinPaymentSettings;
            this._paymentSettings = paymentSettings;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel();
            model.AppId = _weiXinPaymentSettings.AppId;
            model.MchId = _weiXinPaymentSettings.MchId;
            model.AppSecret = _weiXinPaymentSettings.AppSecret;
            model.AdditionalFee = _weiXinPaymentSettings.AdditionalFee;

            return View("~/Plugins/Payments.WeiXin/Views/PaymentWeiXin/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _weiXinPaymentSettings.AppId = model.AppId;
            _weiXinPaymentSettings.MchId = model.MchId;
            _weiXinPaymentSettings.AppSecret = model.AppSecret;
            _weiXinPaymentSettings.AdditionalFee = model.AdditionalFee;
            _settingService.SaveSetting(_weiXinPaymentSettings);
            
            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();
            model.IsJsPay = false;
            return View("~/Plugins/Payments.WeiXin/Views/PaymentWeiXin/PaymentInfo.cshtml", model);
        }


        

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            paymentInfo.CustomValues.Add("IsJsPay", form["IsJsPay"]);
            return paymentInfo;
        }

        [HttpPost]
        public ActionResult QueryOrder(FormCollection form)
        {
            if (string.IsNullOrWhiteSpace(form["orderid"]))
            {
                return Content("error");
            }

            int orderId;
            if (!int.TryParse(form["orderid"], out orderId))
            {
                return Content("error");
            } 

            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            var result = order.PaymentStatus == PaymentStatus.Paid;

            return Content(result.ToString());
        }

        [HttpPost]
        public ActionResult ProcessPayment(FormCollection form)
        {
            var model = new WeiXinPaymentModel(Path.Combine(_webHelper.GetStoreHost(_webHelper.IsCurrentConnectionSecured()), "Plugins/PaymentWeiXin/QueryOrder"));
            var error = new WeiXinPaymentErrorModel();
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.WeiXin") as WeiXinPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
            {
                error.Message = "微信支付服务终止";
            }
            else
            {
                try
                {


                    if (form.HasKeys())
                    {
                        if (!string.IsNullOrWhiteSpace(form["result"]))
                        {
                            var wxModel = new WxPayData();
                            wxModel.FromXml(HttpUtility.HtmlDecode(form["result"]), _weiXinPaymentSettings.AppSecret);


                            if (wxModel.IsSet("code_url"))
                            {
                                model.QRCode = processor.GetQrCode(wxModel.GetValue("code_url").ToString());


                                if (!string.IsNullOrWhiteSpace(form["orderid"]))
                                {
                                    int orderId;
                                    if (int.TryParse(form["orderid"], out orderId))
                                    {
                                        var order = _orderService.GetOrderById(orderId);
                                        if (order != null)
                                        {
                                            if (order.Customer.Id == _workContext.CurrentCustomer.Id)
                                            {
                                                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                                                {
                                                    if (!string.IsNullOrWhiteSpace(form["total"]) &&
                                                        form["total"] == order.OrderTotal.ToString("0.00"))
                                                    {
                                                        model.OrderId = order.Id.ToString(CultureInfo.InvariantCulture);
                                                        model.Total = order.OrderTotal.ToString("￥0.00");
                                                    }
                                                    else
                                                    {
                                                        error.Message = "价格不匹配";
                                                    }
                                                }
                                                else
                                                {
                                                    if (order.PaymentStatus == PaymentStatus.Paid)
                                                    {
                                                        error.Message = "您已付款，请勿重复提交";
                                                    }
                                                    else
                                                    {
                                                        error.Message = "订单状态错误";
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                error.Message = "用户不匹配";
                                                
                                            }
                                        }
                                        else
                                        {
                                            error.Message = "订单号不存在";
                                        }
                                    }
                                    else
                                    {
                                        error.Message = "无法读取订单号";
                                    }
                                }
                                else
                                {
                                    error.Message = "订单号丢失";
                                }

                            }
                            else
                            {
                                error.Message = "无法读取二维码";
                            }
                        }
                        else
                        {
                            error.Message = "参数错误";
                        }

                        
                    }
                    else
                    {
                        error.Message = "没有参数";
                    }
                }
                catch (NopException ex)
                {
                    error.Message = ex.Message;
                }
            }

            
            
            if (error.HasError)
            {
                return View("~/Plugins/Payments.WeiXin/Views/PaymentWeiXin/Error.cshtml", error);
            }
            return View("~/Plugins/Payments.WeiXin/Views/PaymentWeiXin/ProcessPayment.cshtml", model);
        }


        [HttpPost]
        public ActionResult JsApiPayment(FormCollection form)
        {
            var model = new WeiXinPaymentModel(Path.Combine(_webHelper.GetStoreHost(_webHelper.IsCurrentConnectionSecured()), "Plugins/PaymentWeiXin/QueryOrder"));
            var error = new WeiXinPaymentErrorModel();
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.WeiXin") as WeiXinPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
            {
                error.Message = "微信支付服务终止";
            }
            else
            {
                try
                {
                    string totalFee = form["total"];
                    string order = form["orderid"];
                    string prepayId = form["prepay_id"];

                    if (string.IsNullOrWhiteSpace(prepayId) || string.IsNullOrWhiteSpace(totalFee) || string.IsNullOrWhiteSpace(prepayId))
                    {
                        error.Message = "页面传参出错,请返回重试";
                    }
                    else
                    {
                        int orderId;

                        if (int.TryParse(order, out orderId))
                        {
                            //JSAPI支付预处理
                            try
                            {
                                var notifyUrl = Path.Combine(_webHelper.GetStoreHost(_webHelper.IsCurrentConnectionSecured()),
                                        "Plugins/PaymentWeiXin/Notify");

                                var jsApiPay = new JsApiPay(_weiXinPaymentSettings, notifyUrl);
                                jsApiPay.TotalFee = (decimal.Parse(totalFee) * 100).ToString(CultureInfo.InvariantCulture);

                                var unifiedOrderResult = jsApiPay.GetUnifiedOrderResult(orderId, _webHelper.GetCurrentIpAddress(), notifyUrl);
                                model.JsApiParam = jsApiPay.GetJsApiParameters();//获取H5调起JS API参数                    
                                model.OrderId = unifiedOrderResult.ToPrintStr();
                            }
                            catch (Exception ex)
                            {
                                error.Message = "下单失败，请返回重试";
                            }
                        }
                        else
                        {
                            error.Message = "订单号错误";
                        }

                    }
                }
                catch (NopException ex)
                {
                    error.Message = ex.Message;
                }
            }

            if (error.HasError)
            {
                return View("~/Plugins/Payments.WeiXin/Views/PaymentWeiXin/Error.cshtml", error);
            }
            return View("~/Plugins/Payments.WeiXin/Views/PaymentWeiXin/ProcessPayment.cshtml", model);
        }

        [ValidateInput(false)]
        public ActionResult Notify(FormCollection form)
        {
            var s = Request.InputStream;
            var count = 0;
            var buffer = new byte[1024];
            var builder = new StringBuilder();
            while ((count = s.Read(buffer, 0, 1024)) > 0)
            {
                builder.Append(Encoding.UTF8.GetString(buffer, 0, count));
            }
            s.Flush();
            s.Close();
            s.Dispose();

            _logger.InsertLog(LogLevel.Information, GetType() + "Receive data from WeChat : " + builder);
            var data = new WxPayData();
            try
            {
                data.FromXml(builder.ToString(), _weiXinPaymentSettings.AppSecret);
            }
            catch (NopException ex)
            {
                var res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", ex.Message);

                Response.Write(res.ToXml());
                Response.End();
            }



            ProcessNotify(data);

            return Content("");
        }

        private void ProcessNotify(WxPayData data)
        {
            var notifyData = data;

            if (!notifyData.IsSet("transaction_id"))
            {
                var res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "支付结果中微信订单号不存在");
                Response.Write(res.ToXml());
                Response.End();
            }

            string transactionId = notifyData.GetValue("transaction_id").ToString();

            if (!QueryOrderWithTransactionId(transactionId))
            {
                var res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "订单查询失败");
                Response.Write(res.ToXml());
                Response.End();
            }
            else
            {
                var res = new WxPayData();
                int orderId;
                if (int.TryParse(data.GetValue("out_trade_no").ToString(), out orderId))
                {
                    var order = _orderService.GetOrderById(orderId);
                    if (order != null && _orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        _orderProcessingService.MarkOrderAsPaid(order);
                        res.SetValue("return_code", "SUCCESS");
                        res.SetValue("return_msg", "OK");
                    }
                    else
                    {
                        res.SetValue("return_code", "FAIL");
                        res.SetValue("return_msg", "无法将订单设为已付");
                    }
                }

                Response.Write(res.ToXml());
                Response.End();
            }
        }

        private bool QueryOrderWithTransactionId(string transactionId)
        {
            var req = new WxPayData();
            req.SetValue("transaction_id", transactionId);
            return QueryOrder(req);
        }

        private bool QueryOrderWithOrderId(string orderId)
        {
            var req = new WxPayData();
            req.SetValue("out_trade_no", orderId);
            return QueryOrder(req);
        }

        private bool QueryOrder(WxPayData req)
        {
            var res = WeiXinHelper.OrderQuery(req, _weiXinPaymentSettings);

            if (res.GetValue("return_code") != null && res.GetValue("result_code") != null &&
                res.GetValue("trade_state") != null)
            {
                if (res.GetValue("return_code").ToString() == "SUCCESS" &&
                res.GetValue("result_code").ToString() == "SUCCESS" &&
                res.GetValue("trade_state").ToString() == "SUCCESS")
                {
                    return true;
                }
            }

                return false;
        }

    }
}