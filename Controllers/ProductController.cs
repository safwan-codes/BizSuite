using Microsoft.AspNetCore.Mvc;
using BizSuite.Models;
using BizSuite.Repositories;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;

namespace BizSuite.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductRepository _repository;
        private readonly IWebHostEnvironment _env;

        public ProductController(IProductRepository repository, IWebHostEnvironment env)
        {
            _repository = repository;
            _env = env;
        }

        public IActionResult Index()
        {
            var products = _repository.GetAll(GetCompanyId());
            return View(products);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Product product)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(product.ProductName))
                {
                    ModelState.AddModelError("ProductName", "Product name is required.");
                    return View(product);
                }

                if (product.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + product.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        product.ImageFile.CopyTo(fileStream);
                    }
                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                product.CompanyId = GetCompanyId();

                // ARCHITECT FIX: Get proper User ID from Session instead of hardcoding 1
                product.CreatedBy = HttpContext.Session.GetInt32("UserId") ?? 1;

                _repository.Insert(product);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Database Error: " + ex.Message);
                return View(product);
            }
        }

        public IActionResult Edit(int id)
        {
            var product = _repository.GetById(id, GetCompanyId());
            if (product == null || product.ProductId == 0) return NotFound();

            return View(product);
        }

        [HttpPost]
        public IActionResult Edit(Product product)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(product.ProductName))
                {
                    ModelState.AddModelError("ProductName", "Product name is required.");
                    return View(product);
                }

                if (product.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + product.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        product.ImageFile.CopyTo(fileStream);
                    }
                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                product.CompanyId = GetCompanyId();

                // ARCHITECT FIX: Get proper User ID from Session instead of hardcoding 1
                product.CreatedBy = HttpContext.Session.GetInt32("UserId") ?? 1;

                _repository.Update(product);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Database Error: " + ex.Message);
                return View(product);
            }
        }

        public IActionResult Delete(int id)
        {
            var product = _repository.GetById(id, GetCompanyId());
            if (product == null || product.ProductId == 0) return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            _repository.Delete(id, GetCompanyId());
            return RedirectToAction("Index");
        }

        private int GetCompanyId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            return claim != null ? int.Parse(claim.Value) : (HttpContext.Session.GetInt32("CompanyId") ?? 1);
        }
    }
}