using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ToDo.Api.Data;

namespace ToDo.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClaimsSetupController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<ClaimsSetupController> _logger;
        public ClaimsSetupController(ApiDbContext context,
                               UserManager<IdentityUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               ILogger<ClaimsSetupController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllClaims(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if(user == null){
                _logger.LogInformation($"The user with the email: {email} does not been exists");
                return BadRequest(new {
                    error = $"The user {email} does not exists"
                });     
            }

            var userClaims = await _userManager.GetClaimsAsync(user);
            return Ok(userClaims);
        }

        [HttpPost]
        [Route("AddClaimsToUser")]
        public async Task<IActionResult> AddClaimsToUser(string email, string claimName, string claimValue)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if(user == null){
                _logger.LogInformation($"The user with the email: {email} does not been exists");
                return BadRequest(new {
                    error = $"The user {email} does not exists"
                });     
            }

            var userClaim = new Claim(claimName, claimValue);
            var result = await _userManager.AddClaimAsync(user, userClaim);

            if(result.Succeeded){
                return Ok(new {
                    result = $"User {user.Email} has a claim {claimName} added to them"
                });
            }

            return BadRequest(new {
                error = $"Unable to add claim {claimName} to the user {user.Email}"
            });
        }
    }
}