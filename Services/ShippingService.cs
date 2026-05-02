using EasyPost;
using EasyPost.Parameters;
using EasyPost.Models.API;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using BizSuite.Data;
using System;
using System.Linq;

namespace BizSuite.Services
{
    public interface IShippingService
    {
        Task<string> GenerateShippingLabelAsync(int companyId, int orderId, string customerName, string customerAddress);
    }

    public class ShippingService : IShippingService
    {
        private readonly Client _client;
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        public ShippingService(DbHelper db, IConfiguration config)
        {
            _db = db;
            _config = config;
            string apiKey = _config["EasyPost:ApiKey"] ?? "";
            _client = new Client(new ClientConfiguration(apiKey));
        }

        public async Task<string> GenerateShippingLabelAsync(int companyId, int orderId, string customerName, string customerAddress)
        {
            // Note: In local dev with placeholder keys, this will throw unless handled
            try
            {
                if (_config["EasyPost:ApiKey"] == "EZAK_placeholder_test_key_here")
                {
                    return $"MOCK_TRACK_{DateTime.Now.Ticks}";
                }

                // 1. Get Company Address (Origin)
                var dtCompany = _db.ExecuteQuery("SELECT TOP 1 CompanyName, Email, Phone, Address FROM Companies WHERE CompanyId = @C", DbHelper.P("@C", companyId));
                if (dtCompany.Rows.Count == 0) return null;
                var compRow = dtCompany.Rows[0];

                var fromAddressParams = new EasyPost.Parameters.Address.Create
                {
                    Name = compRow["CompanyName"].ToString(),
                    Street1 = compRow["Address"].ToString() ?? "123 Business Rd",
                    City = "San Francisco", // Mocked detail as address usually is one line in this DB
                    State = "CA",
                    Zip = "94107",
                    Country = "US",
                    Phone = compRow["Phone"].ToString() ?? "555-0000",
                    Email = compRow["Email"].ToString() ?? ""
                };
                var fromAddress = await _client.Address.Create(fromAddressParams);

                // 2. Parse destination
                var parts = (customerAddress ?? "100 Main St, City, ST 10000").Split(',');
                var city = parts.Length > 1 ? parts[1].Trim() : "Anytown";
                var toAddressParams = new EasyPost.Parameters.Address.Create
                {
                    Name = customerName,
                    Street1 = parts[0].Trim(),
                    City = city,
                    State = "NY", // Assumed default if missing for test
                    Zip = "10000",
                    Country = "US"
                };
                var toAddress = await _client.Address.Create(toAddressParams);

                // 3. Create a Custom Parcel at 1kg (35.2 oz)
                var parcelParams = new EasyPost.Parameters.Parcel.Create
                {
                    Weight = 35.2 // Weight in ounces (1 kg)
                };
                var parcel = await _client.Parcel.Create(parcelParams);

                // 4. Create Shipment
                var shipmentParams = new EasyPost.Parameters.Shipment.Create
                {
                    FromAddress = fromAddress,
                    ToAddress = toAddress,
                    Parcel = parcel
                };
                var shipment = await _client.Shipment.Create(shipmentParams);

                // 5. Buy the cheapest label
                if (shipment.Rates != null && shipment.Rates.Any())
                {
                    var rate = shipment.LowestRate();
                    shipment = await _client.Shipment.Buy(shipment.Id, new EasyPost.Parameters.Shipment.Buy(rate));
                    
                    // Return the tracking number
                    return shipment.TrackingCode;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EasyPost Error: " + ex.Message);
                return null;
            }
        }
    }
}
