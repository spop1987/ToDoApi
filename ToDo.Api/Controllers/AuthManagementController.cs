using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ToDo.Api.Configuration;
using ToDo.Api.Models.Dtos.Requests;
using ToDo.Api.Models.Dtos.Responses;

namespace ToDo.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthManagementController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JwtConfig _jwtConfig;
        public AuthManagementController(UserManager<IdentityUser> userManager,
                                        IOptionsMonitor<JwtConfig> optionsMonitor)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto user)
        {
            if(ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);
                if(existingUser != null)
                {
                    return BadRequest(GetRegistrationResponse("Email is already in use", false));
                }

                var newUser = new IdentityUser(){
                    Email = user.Email,
                    UserName = user.Username
                };
                var isCreated = await _userManager.CreateAsync(newUser, user.Password);
                if(isCreated.Succeeded){
                    var jwtToken = GenerateJwtToken(newUser);
                    return Ok(new RegistrationResponse(){
                        Success = true,
                        Token = jwtToken
                    });
                }else{
                    return BadRequest(GetRegistrationResponse("Unable to create User", false));
                }
            }

            return BadRequest(GetRegistrationResponse("Invalid payload", false));
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest user)
        {
            if(ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);

                if(existingUser == null)
                {
                    return BadRequest(GetRegistrationResponse("Invalid login request", false));
                }

                var isCorrect = await _userManager.CheckPasswordAsync(existingUser, user.Password);

                if(!isCorrect)
                {
                    return BadRequest(GetRegistrationResponse("Invalid login request", false));
                }

                var jwtToken = GenerateJwtToken(existingUser);

                return Ok(new RegistrationResponse(){
                    Success = true,
                    Token = jwtToken
                });
            }

            return BadRequest(GetRegistrationResponse("Invalid payload", false));
        }

        private string GenerateJwtToken(IdentityUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor{
                Subject = new ClaimsIdentity(new []{
                    new Claim("Id", user.Id),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(6),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = jwtTokenHandler.WriteToken(token);

            return jwtToken;
        }

        private RegistrationResponse GetRegistrationResponse(string message, bool status)
        {
            return new RegistrationResponse{
                Errors = new List<string>(){
                    message
                },
                Success = status
            };
        }
    }
}