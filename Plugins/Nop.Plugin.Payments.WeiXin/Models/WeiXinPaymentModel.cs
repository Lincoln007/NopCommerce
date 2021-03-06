﻿using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.WeiXin.Models
{
    public class WeiXinPaymentModel : BaseNopModel
    {
        public WeiXinPaymentModel(string queryOrderUrl)
        {
            QueryOrderUrl = queryOrderUrl;
        }


        [NopResourceDisplayName("Plugins.Payments.WeiXin.QRCode")]
        [AllowHtml]
        public string QRCode { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WeiXin.Total")]
        [AllowHtml]
        public string Total { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WeiXin.OrderId")]
        [AllowHtml]
        public string OrderId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WeiXin.OrderType")]
        [AllowHtml]
        public string OrderType { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WeiXin.JsApiParam")]
        [AllowHtml]
        public string JsApiParam { get; set; }


        [NopResourceDisplayName("Plugins.Payments.WeiXin.QueryOrderUrl")]
        [AllowHtml]
        public string QueryOrderUrl { get; set; }

    }
}