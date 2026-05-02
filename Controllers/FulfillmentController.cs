using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BizSuite.Repositories;
using BizSuite.Models;
using System.Linq;

namespace BizSuite.Controllers
{
    [Authorize]
    public class FulfillmentController : Controller
    {
        private readonly IFulfillmentRepository _repo;

        public FulfillmentController(IFulfillmentRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            var pendingOrders = _repo.GetPendingFulfillments();
            return View(pendingOrders);
        }

        public IActionResult Details(int id)
        {
            var vm = _repo.GetOrderDetailsForFulfillment(id);
            if (vm == null || vm.Header == null)
                return NotFound();

            return View(vm);
        }

        [HttpPost]
        public IActionResult PackOrder(int orderId)
        {
            _repo.UpdateOrderFulfillmentStatus(orderId, "Packed");
            
            bool isAjaxCall = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isAjaxCall)
            {
                var vm = _repo.GetOrderDetailsForFulfillment(orderId);
                return PartialView("_OrderDetailsPartial", vm);
            }

            TempData["Success"] = "Order marked as Packed and is ready for shipping label generation.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ShipOrder(int orderId, string carrier, string trackingNumber)
        {
            _repo.UpdateOrderFulfillmentStatus(orderId, "Shipped", trackingNumber, carrier);
            
            bool isAjaxCall = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isAjaxCall)
            {
                var vm = _repo.GetOrderDetailsForFulfillment(orderId);
                return PartialView("_OrderDetailsPartial", vm);
            }

            TempData["Success"] = "Order has been shipped!";
            return RedirectToAction("Index");
        }
    }
}
