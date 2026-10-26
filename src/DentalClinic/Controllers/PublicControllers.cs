using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DentalClinic.Controllers;

public class HomeController : Controller
{
    private readonly ClinicDb _db;
    public HomeController(ClinicDb db) => _db = db;

    [Route("")]
    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            Services = await _db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(),
            Doctors = await _db.Doctors.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync(),
            Articles = (await _db.Articles.AsNoTracking().ToListAsync()).OrderByDescending(a => a.PublishedAt).Take(3).ToList(),
        };
        return View(vm);
    }

    [Route("cjenovnik")]
    public async Task<IActionResult> Prices() =>
        View(await _db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync());

    [Route("pitanja")]
    public IActionResult Faq() => View();

    [Route("privatnost")]
    public IActionResult Privacy() => View();

    [HttpGet, Route("kontakt")]
    public IActionResult Contact() => View(new ContactMessage());

    [HttpPost, Route("kontakt"), ValidateAntiForgeryToken, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Submit)]
    public async Task<IActionResult> Contact(ContactMessage model, bool consent)
    {
        if (!consent) ModelState.AddModelError("consent", "Za slanje poruke potrebna je saglasnost.");
        if (!ModelState.IsValid) return View(model);

        model.Id = 0;
        model.Handled = false;
        model.CreatedAt = DateTime.UtcNow;
        _db.Messages.Add(model);
        await _db.SaveChangesAsync();
        TempData["Sent"] = true;
        return RedirectToAction(nameof(Contact));
    }

    /// <summary>Prikazuje lijepu stranicu za 404 (i druge HTTP greške) umjesto prazne.</summary>
    public IActionResult NotFoundPage(int code = 404)
    {
        Response.StatusCode = code;
        return View(code);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    [Route("robots.txt")]
    public IActionResult Robots() =>
        Content($"User-agent: *\nDisallow: /admin\nSitemap: {Request.Scheme}://{Request.Host}/sitemap.xml\n", "text/plain");

    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var root = $"{Request.Scheme}://{Request.Host}";
        var urls = new List<string> { "/", "/usluge", "/tim", "/cjenovnik", "/savjeti", "/pitanja", "/kontakt", "/zakazivanje" };
        urls.AddRange((await _db.Services.Where(s => s.IsActive).Select(s => s.Slug).ToListAsync()).Select(s => $"/usluge/{s}"));
        urls.AddRange((await _db.Articles.Select(a => a.Slug).ToListAsync()).Select(s => $"/savjeti/{s}"));

        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var u in urls) sb.Append("  <url><loc>").Append(root).Append(u).Append("</loc></url>\n");
        sb.Append("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}

public class ServicesController : Controller
{
    private readonly ClinicDb _db;
    public ServicesController(ClinicDb db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync());

    public async Task<IActionResult> Details(string slug)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
        if (service is null) return NotFound();

        ViewBag.Doctors = await _db.Doctors.AsNoTracking()
            .Where(d => d.IsActive && d.Services.Any(x => x.ServiceId == service.Id)).OrderBy(d => d.SortOrder).ToListAsync();
        ViewBag.Others = await _db.Services.AsNoTracking()
            .Where(s => s.IsActive && s.Id != service.Id && s.Category == service.Category).Take(3).ToListAsync();
        return View(service);
    }
}

public class TeamController : Controller
{
    private readonly ClinicDb _db;
    public TeamController(ClinicDb db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Doctors.AsNoTracking().Include(d => d.Services).ThenInclude(s => s.Service)
            .Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync());
}

public class BlogController : Controller
{
    private readonly ClinicDb _db;
    public BlogController(ClinicDb db) => _db = db;

    public async Task<IActionResult> Index(string? category)
    {
        // datum sortiramo u memoriji: SQLite nema pouzdano sortiranje DateTime kolone kroz EF u svim slučajevima
        var all = (await _db.Articles.AsNoTracking().ToListAsync()).OrderByDescending(a => a.PublishedAt).ToList();
        ViewBag.Categories = all.Select(a => a.Category).Distinct().OrderBy(c => c).ToList();
        ViewBag.Category = category;
        return View(string.IsNullOrWhiteSpace(category) ? all : all.Where(a => a.Category == category).ToList());
    }

    public async Task<IActionResult> Details(string slug)
    {
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug);
        if (article is null) return NotFound();
        ViewBag.Related = (await _db.Articles.AsNoTracking().Where(a => a.Id != article.Id).ToListAsync())
            .OrderByDescending(a => a.Category == article.Category).ThenByDescending(a => a.PublishedAt).Take(3).ToList();
        return View(article);
    }
}

public class HomeViewModel
{
    public List<Service> Services { get; set; } = [];
    public List<Doctor> Doctors { get; set; } = [];
    public List<Article> Articles { get; set; } = [];
}
