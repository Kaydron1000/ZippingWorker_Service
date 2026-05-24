using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ZippingWorker_Service.Controllers
{
    public class StatusContoller : Controller
    {
        // GET: StatusContoller
        public ActionResult Index()
        {
            return View();
        }

        // GET: StatusContoller/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: StatusContoller/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: StatusContoller/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: StatusContoller/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: StatusContoller/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: StatusContoller/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: StatusContoller/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
