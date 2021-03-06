﻿using System;
using System.IO;
using NopImport.Console.Common;
using NopImport.Model.Common;
using NopImport.Model.Data;
using NopImport.Model.SearchModel;

namespace NopImport.Console.ChemistWarehouse
{
    public class CWProductHtmlReader: ProductHtmlReader
    {
        public CWProductHtmlReader()
        {
            var productSearchModel = new ProductSearchModel { BaseUrl = "http://www.chemistwarehouse.com.au" };
            productSearchModel.AddIdentifier("Name", IdentifierType.ElementContent, ".//*[@class='product-name']//h1");
            productSearchModel.AddIdentifier("OriginalPrice", IdentifierType.ElementContent, ".//div[@class='retailPrice']", null, false, 0, "Don't Pay RRP: $", string.Empty);
            productSearchModel.AddIdentifier("Picture", IdentifierType.Attribute, "src", ".//img[@class='product-thumbnail']");

            productSearchModel.AddIdentifier("MetaDescription", IdentifierType.Attribute, "content", ".//meta[@name='description']");
            productSearchModel.AddIdentifier("MetaKeywords", IdentifierType.Attribute, "content", ".//meta[@name='keywords']");

            productSearchModel.AddIdentifier("Description", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'description')]//div[@class='details']");
            productSearchModel.AddIdentifier("GeneralInfo", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'general-info')]//div[@class='details']");

            productSearchModel.AddIdentifier("Miscellaneous", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'miscellaneous')]//div[@class='details']");
            productSearchModel.AddIdentifier("DrugInteractions", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'druginteractions')]//div[@class='details']");
            productSearchModel.AddIdentifier("Warnings", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'warnings')]//div[@class='details']");
            productSearchModel.AddIdentifier("CommonUses", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'commonuses')]//div[@class='details']");
            productSearchModel.AddIdentifier("Ingredients", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'ingredients')]//div[@class='details']");
            productSearchModel.AddIdentifier("Directions", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'directions')]//div[@class='details']");
            productSearchModel.AddIdentifier("Indications", IdentifierType.ElementContent, ".//section[contains(@class, 'product-info-section') and contains(@class, 'indications')]//div[@class='details']");

            ProductSearchModel = productSearchModel;
        }

        public override Product ProcessItem(Product product)
        {
            if (!string.IsNullOrWhiteSpace(product.Picture))
            {
                product.Picture = product.Picture.Replace("200.jpg", "original.jpg");

                var extension = Path.GetExtension(product.Picture);

                var newFilename = Guid.NewGuid().ToString("N") + extension;


                if (FileDownloader.DownloadFile(product.Picture, newFilename))
                {
                    product.LocalPicture = newFilename;
                }
            }


            return base.ProcessItem(product);
        }
    }
}
