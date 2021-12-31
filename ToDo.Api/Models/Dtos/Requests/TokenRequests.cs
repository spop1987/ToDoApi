using System.ComponentModel.DataAnnotations;

namespace ToDo.Api.Models.Dtos.Requests
{
    public class TokenRequests
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public string RefreshToken { get; set; }
    }
}