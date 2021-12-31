using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ToDo.Api.Configuration;
using ToDo.Api.Data;
using ToDo.Api.Models;
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
        private readonly TokenValidationParameters _tokenValidationParams;
        private readonly ApiDbContext _apiDbContext;
        public AuthManagementController(UserManager<IdentityUser> userManager,
                                        IOptionsMonitor<JwtConfig> optionsMonitor,
                                        TokenValidationParameters tokenValidationParams,
                                        ApiDbContext apiDbContext)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
            _tokenValidationParams = tokenValidationParams;
            _apiDbContext = apiDbContext;
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
                    var jwtToken = await GenerateJwtToken(newUser);
                    return Ok(jwtToken);
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

                var jwtToken = await GenerateJwtToken(existingUser);

                return Ok(jwtToken);
            }

            return BadRequest(GetRegistrationResponse("Invalid payload", false));
        }

        [HttpPost]
        [Route("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequests tokenRequests)
        {
            if(ModelState.IsValid)
            {
                var result = await VerifyAndGenerateToken(tokenRequests);
                if(result == null)
                {
                    return BadRequest(GetRegistrationResponse("Invalid Tokens", false));
                }

                return Ok(result);
            }

            return BadRequest(GetRegistrationResponse("Invalid payload", false));
        }

        private async Task<AuthResult> GenerateJwtToken(IdentityUser user)
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
                Expires = DateTime.UtcNow.AddSeconds(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = jwtTokenHandler.WriteToken(token);

            var refreshToken = new RefreshToken()
            {
                JwtId = token.Id,
                IsUsed = false,
                IsRevoked = false,
                UserId = user.Id,
                AddedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                Token = RandomString(35) + Guid.NewGuid()
            };

            await _apiDbContext.RefreshTokens.AddAsync(refreshToken);
            await _apiDbContext.SaveChangesAsync();

            return GetAuthResult(token: jwtToken, refreshToken: refreshToken.Token, success: true);
        }
        private async Task<AuthResult> VerifyAndGenerateToken(TokenRequests tokenRequests)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            try
            {
                // Validation 1 - Validation JWT token format
                var tokenInVerification = jwtTokenHandler.ValidateToken(tokenRequests.Token, _tokenValidationParams, out var validatedToken);
                
                // Validation 2 - Valdiate encryption algorithm
                if(validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);
                    if(result == false)
                    {
                        return null;
                    }
                }
                // Validation 3 - Validate expiry date
                var utcExpiryDate = long.Parse(tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp).Value);
                var expiryDate = UnixTimeStampToDateTime(utcExpiryDate);
                if(expiryDate > DateTime.UtcNow)
                {
                    return GetAuthResult(error:  "Token has not yet expired");
                }

                // Validation 4 - validate the existaence of the token
                var storedToken = await _apiDbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == tokenRequests.RefreshToken);
                if(storedToken == null)
                {
                    return GetAuthResult(error: "Token does not exist");
                }

                // validation 5 - validate if is used or not
                if(storedToken.IsUsed)
                {
                    return GetAuthResult(error: "Token has been used");
                }

                // validation 6 - validate if is revoked    
                if(storedToken.IsRevoked)
                {
                    return GetAuthResult(error: "Token has been revoked");
                }

                // validation 7 - validate the id
                var jsonTokenId = tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;
                if(storedToken.JwtId != jsonTokenId)
                {
                    return GetAuthResult(error: "Token does not match");
                }

                // update the current token
                storedToken.IsUsed = true;
                _apiDbContext.RefreshTokens.Update(storedToken);
                await _apiDbContext.SaveChangesAsync();

                // generate new token
                var dbUser = await _userManager.FindByIdAsync(storedToken.UserId);
                return await GenerateJwtToken(dbUser);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1,1,0,0,0,0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTimeVal;
        }

        private AuthResult GetAuthResult(string? token = null, string? refreshToken = null, bool success = false, string error = "Everything is ok")
        {
            return new AuthResult{
                Token = token,
                RefreshToken = refreshToken,
                Success = success,
                Errors = new List<string>(){
                    error
                }
            };     
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

        private string RandomString(int length)
        {
            var random  = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var randomString = new string(Enumerable.Repeat(chars, length)
                .Select(x => x[random.Next(x.Length)]).ToArray());
            return randomString;
        }
    }
}