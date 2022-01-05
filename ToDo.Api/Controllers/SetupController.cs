using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToDo.Api.Data;

namespace ToDo.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SetupController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<SetupController> _logger;
        public SetupController(ApiDbContext context,
                               UserManager<IdentityUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               ILogger<SetupController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetAllRoles()
        {
            var roles = _context.Roles.ToList();
            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(string name)
        {
            // check if the role exists
            var roleExist = await _roleManager.RoleExistsAsync(name);

            if(!roleExist){
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(name));
                
                // we need to check if the role has been added successfully
                if(roleResult.Succeeded){
                    _logger.LogInformation($"The role {name} has been added succesfully");
                    return Ok(new {
                        result = $"The role {name} has been added succesfully"
                    });
                }else{
                     _logger.LogInformation($"The role {name} has not been added");
                    return BadRequest(new {
                        error = $"The role {name} has not been added"
                    });                  
                }
            }
            return BadRequest(new {error = "Role aleready exists"});
        }

        [HttpGet]
        [Route("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            return Ok(users);
        }

        [HttpPost]
        [Route("AddUserToRole")]
        public async Task<IActionResult> AddUserToRole(string email, string roleName)
        {
            //check if the user exists
            var user = await _userManager.FindByEmailAsync(email);

            if(user == null){
                _logger.LogInformation($"The user with the email: {email} does not been exists");
                return BadRequest(new {
                    error = $"The user {email} does not exists"
                });     
            }
            //check if the role exists
            var roleExists = await _roleManager.RoleExistsAsync(roleName);

            if(!roleExists){
                _logger.LogInformation($"The role {roleName} does not been exists");
                return BadRequest(new {
                    error = $"The role {roleName} does not exists"
                }); 
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);

            // check if the users is assigned to the role successfully
            if(result.Succeeded){
                return Ok(new {
                    result = $"The user with email: {email} was added to the role {roleName}"
                });
            }else{
                _logger.LogInformation($"The user with email: {email} was not abel to be added to the role {roleName}");
                return BadRequest(new {
                    error = $"The user with email: {email} was not abel to be added to the role {roleName}"
                }); 
            }
        }

        [HttpGet]
        [Route("GetUserRoles")]
        public async Task<IActionResult> GetUserRoles(string email)
        {
            // check if the email is valid
            var user = await _userManager.FindByEmailAsync(email);

            if(user == null){
                _logger.LogInformation($"The user with the email: {email} does not been exists");
                return BadRequest(new {
                    error = $"The user {email} does not exists"
                });     
            }

            //return the roles
            var roles = await _userManager.GetRolesAsync(user);
            return Ok(roles);
        }

        [HttpPost]
        [Route("RemoveUserFromRole")]
        public async Task<IActionResult> RemoveUserFromRole(string email, string roleName)
        {
            //check if the user exists
            var user = await _userManager.FindByEmailAsync(email);

            if(user == null){
                _logger.LogInformation($"The user with the email: {email} does not been exists");
                return BadRequest(new {
                    error = $"The user {email} does not exists"
                });     
            }
            //check if the role exists
            var roleExists = await _roleManager.RoleExistsAsync(roleName);

            if(!roleExists){
                _logger.LogInformation($"The role {roleName} does not been exists");
                return BadRequest(new {
                    error = $"The role {roleName} does not exists"
                }); 
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);

            // check if the users is assigned to the role successfully
            if(result.Succeeded){
                return Ok(new {
                    result = $"The user with email: {email} has been removed from the role {roleName}"
                });
            }else{
                _logger.LogInformation($"The user with email: {email} was not abel to be removed from the role {roleName}");
                return BadRequest(new {
                    error = $"The user with email: {email} was not abel to be removed from the role {roleName}"
                }); 
            }
        }
    }
}