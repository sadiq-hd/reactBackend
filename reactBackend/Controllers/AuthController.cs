using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using reactBackend.Data;
using reactBackend.Models;
using reactBackend.Dtos;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace reactBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string CreateToken(User user)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role),
        new Claim(ClaimTypes.Name, user.Username)
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration.GetSection("JWT:Key").Value!));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256); // تغيير إلى HmacSha256

            var token = new JwtSecurityToken(
                issuer: _configuration.GetSection("JWT:Issuer").Value,
                audience: _configuration.GetSection("JWT:Audience").Value,
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == loginDto.EmailOrPhone ||
            u.PhoneNumber == loginDto.EmailOrPhone);
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password))
            {
                return BadRequest("البريد الإلكتروني أو كلمة المرور غير صحيحة");
            }
            var token = CreateToken(user);
            return
                Ok(new { token, user });
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<User>>> GetAllUsers()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }

        
        [HttpGet("current")]
        [Authorize]
        public async Task<ActionResult<User>> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound($"لم يتم العثور على مستخدم برقم {id}");

            return Ok(user);
        }

        // البحث عن مستخدمين
        [HttpGet("search")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<User>>> SearchUsers([FromQuery] string searchTerm)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u =>
                    u.Username.Contains(searchTerm) ||
                    u.Email.Contains(searchTerm) ||
                    u.Name.Contains(searchTerm)
                );
            }

            var users = await query.ToListAsync();
            return Ok(users);
        }

        // الحصول على إحصائيات المستخدمين (للمدير فقط)
        [HttpGet("stats")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> GetUserStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var adminCount = await _context.Users.CountAsync(u => u.Role == "admin");
            var regularUserCount = await _context.Users.CountAsync(u => u.Role == "user");

            var stats = new
            {
                TotalUsers = totalUsers,
                AdminCount = adminCount,
                RegularUserCount = regularUserCount
            };

            return Ok(stats);
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
            {
                return BadRequest("البريد الإلكتروني مستخدم بالفعل");
            }

            var user = new User
            {
                Email = registerDto.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                Name = registerDto.Name,
                Username = registerDto.Username,
                PhoneNumber=registerDto.PhoneNumber,
                Role = "user" // الدور الافتراضي للمستخدمين الجدد
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم إنشاء الحساب بنجاح" });
        }
    }
}
