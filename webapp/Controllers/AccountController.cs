using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using webapp.Data;
using webapp.Models;

namespace webapp.Controllers;

public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly MyDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public AccountController(MyDbContext context, IConfiguration configuration, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(User model, IFormFile? profileImage)
    {
        if (ModelState.IsValid)
        {
            // Profil Resmi Yükleme
            if (profileImage != null)
            {
                string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + profileImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(fileStream);
                }
                model.ProfilePicturePath = "/uploads/" + uniqueFileName;
            }

            // Şifre Hashleme
            model.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);

            _context.Users.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }
        return View(model);
    }
    
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }
    
    [HttpPost]
    public IActionResult Login(User model)
    {
        var user = _context.Users.SingleOrDefault(u => u.Username == model.Username);
    
        if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            var tokenString = GenerateToken(user);
            
            // Token'ı cookie'ye ekle
            Response.Cookies.Append("token", tokenString);
            
            // Basit bir yönlendirme: Giriş başarılıysa Profile git
            return RedirectToAction("Profile", new { id = user.Id });
        }

        ModelState.AddModelError(string.Empty, "Geçersiz kullanıcı adı veya şifre.");
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Profile(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, User model, IFormFile? profileImage)
    {
        var userToUpdate = await _context.Users.FindAsync(id);
        if (userToUpdate == null) return NotFound();

        userToUpdate.Name = model.Name;
        userToUpdate.Username = model.Username;
        userToUpdate.Email = model.Email;

        if (profileImage != null)
        {
            string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + profileImage.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await profileImage.CopyToAsync(fileStream);
            }
            userToUpdate.ProfilePicturePath = "/uploads/" + uniqueFileName;
        }

        if (!string.IsNullOrEmpty(model.Password))
        {
             userToUpdate.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
        }

        _context.Users.Update(userToUpdate);
        await _context.SaveChangesAsync();

        return RedirectToAction("Profile", new { id = userToUpdate.Id });
    }
    
    [HttpPost]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("token");
        return RedirectToAction("Index", "Home");
    }

    private string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}