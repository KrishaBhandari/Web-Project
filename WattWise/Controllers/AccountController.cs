using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WattWise.Data;
using WattWise.Models;
using WattWise.ViewModels;
using BC = BCrypt.Net.BCrypt;

namespace WattWise.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApplicationDbContext db, ILogger<AccountController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── GET /Account/Register ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return View(new RegisterViewModel());
    }

    // ── POST /Account/Register ────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Duplicate email check
        var exists = await _db.Users.AnyAsync(u => u.Email == model.Email.ToLower());
        if (exists)
        {
            ModelState.AddModelError("Email", "An account with this email already exists.");
            return View(model);
        }

        var user = new User
        {
            FullName     = model.FullName.Trim(),
            Email        = model.Email.Trim().ToLower(),
            PhoneNumber  = model.PhoneNumber.Trim(),
            PasswordHash = BC.HashPassword(model.Password),
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("New user registered: {Email}", user.Email);

        await SignInUser(user, persistent: false);
        TempData["Success"] = $"Welcome, {user.FullName}! Your account has been created.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ── GET /Account/Login ────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    // ── POST /Account/Login ───────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == model.Email.Trim().ToLower() && u.IsActive);

        if (user == null || !BC.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        await SignInUser(user, model.RememberMe);
        _logger.LogInformation("User logged in: {Email}", user.Email);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    // ── POST /Account/Logout ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ── Helper: sign in and build claims principal ─────────────────────────
    private async Task SignInUser(User user, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name,           user.FullName),
            new(ClaimTypes.Email,          user.Email)
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = persistent });
    }
}
