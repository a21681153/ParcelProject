using Microsoft.AspNetCore.Mvc;

namespace ParcelManagement2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestApiController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message = "API 控制器正常運作！", timestamp = DateTime.Now });
        }
        [HttpGet("hello")]
        public IActionResult Hello()
        {
            return Ok("Hello from API!");
        }
    }
}
