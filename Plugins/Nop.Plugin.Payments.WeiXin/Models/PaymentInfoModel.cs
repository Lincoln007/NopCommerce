﻿using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.WeiXin.Models
{
    public class PaymentInfoModel : BaseNopModel
    {
        public bool IsJsPay { get; set; }
    }
}