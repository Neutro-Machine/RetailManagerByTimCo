﻿using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TRMDataManager.Library.Internal;
using TRMDataManager.Library.Models;

namespace TRMDataManager.Library.DataAccess
{
    public class SaleData
    {
        private readonly IConfiguration config;

        public SaleData(IConfiguration config)
        {
            this.config = config;//tercer cambio
        }

        public List<ProductModel> GetProducts()
        {
            SqlDataAccess sql = new SqlDataAccess(config);

            var output = sql.LoadData<ProductModel, dynamic>("dbo.spGetAllProduct", new { }, "TRMData");

            return output;
        }

        public void SaveSale(SaleModel saleInfo, string userId)
        {
            // TODO: Make this SOLID/DRY/Btter
            // Start filling in the sale detail models we will save to the database
            List<SaleDetailDbModel> details = new List<SaleDetailDbModel>();
            ProductData product = new ProductData(config);
            var taxRate = ConfigHelper.GetTaxRate()/100;

            foreach (var item in saleInfo.SaleDetails)
            {
                var detail = new SaleDetailDbModel
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                };

                // Get the information about this product
                var productInfo = product.GetProductById(detail.ProductId);

                if (productInfo == null)
                {
                    throw new Exception($"El producto con Id { detail.ProductId } no se encuentra en la base de datos.");
                }

                detail.PurchasePrice = (productInfo.RetailPrice * detail.Quantity);

                if (productInfo.IsTaxable)
                {
                    detail.Tax = (detail.PurchasePrice * taxRate);
                }

                details.Add(detail);
            }

            // Create the sale model
            SaleDbModel sale = new SaleDbModel
            {
                SubTotal = details.Sum(x => x.PurchasePrice),
                Tax = details.Sum(x => x.Tax),
                CashierId = userId
            };

            sale.Total = sale.SubTotal + sale.Tax;

            using(SqlDataAccess sql = new SqlDataAccess(config))
            {
                try
                {
                    sql.StartTransaction("TRMData");

                    // Save the sale model
                    sql.SaveDataInTransaction("dbo.spSale_Insert", sale);

                    // Get the Id from the sale model
                    sale.Id = sql.LoadDataInTransaction<int, dynamic>("dbo.spSale_Lookup", new
                    { CashierId = sale.CashierId, SaleDate = sale.SaleDate }).FirstOrDefault();

                    // Finish filling in the sale details model
                    foreach (var item in details)
                    {
                        item.SaleId = sale.Id;
                        // Save the sale detail model
                        sql.SaveDataInTransaction("dbo.spSaleDetail_Insert", item);
                    }

                    sql.CommitTransaction();
                }
                catch 
                {
                    sql.RollBackTransaction();
                    throw;
                }
            }
        }

        public List<SaleReportModel> GetSaleReport()
        {
            SqlDataAccess sql = new SqlDataAccess(config);

            var output = sql.LoadData<SaleReportModel, dynamic>("dbo.spSale_SaleReport", new { }, "TRMData");

            return output;
        }
    }
}
