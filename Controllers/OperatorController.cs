using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace GameSupport.Controllers
{
    public class OperatorController : Controller
    {
        [Authorize(Roles = "Operator")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
