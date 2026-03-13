using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseStaticFiles();
app.UseSession();

// ── In-memory student store ──────────────────────────────────────────────
var students = new List<Student>();
var nextId = 1;

// ── LOGIN PAGE ───────────────────────────────────────────────────────────
app.MapGet("/", async (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/html";
    await ctx.Response.SendFileAsync("wwwroot/login.html");
});

// ── LOGIN POST ───────────────────────────────────────────────────────────
app.MapPost("/login", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    string username = form["username"].ToString().Trim();
    string password = form["password"].ToString().Trim();

    if (Authenticate(username, password))
    {
        ctx.Session.SetString("user", username);
        ctx.Response.Redirect("/grades");
    }
    else
    {
        ctx.Response.Redirect("/?error=1");
    }
});

// ── GRADE CALCULATOR PAGE (protected) ────────────────────────────────────
app.MapGet("/grades", async (HttpContext ctx) =>
{
    if (ctx.Session.GetString("user") == null)
    {
        ctx.Response.Redirect("/");
        return;
    }
    ctx.Response.ContentType = "text/html";
    await ctx.Response.SendFileAsync("wwwroot/grades.html");
});

// ── LOGOUT ───────────────────────────────────────────────────────────────
app.MapGet("/logout", (HttpContext ctx) =>
{
    ctx.Session.Clear();
    return Results.Redirect("/");
});

// ── STUDENT API ──────────────────────────────────────────────────────────
app.MapGet("/api/students", (HttpContext ctx) =>
{
    if (ctx.Session.GetString("user") == null) return Results.Unauthorized();
    return Results.Ok(students);
});

app.MapPost("/api/students", (HttpContext ctx, Student s) =>
{
    if (ctx.Session.GetString("user") == null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(s.Name)) return Results.BadRequest("Name is required.");
    if (new[] { s.Oral, s.Exam, s.Performance }.Any(v => v < 0 || v > 100))
        return Results.BadRequest("Scores must be 0-100.");

    s.Id = nextId++;
    s.FinalGrade = Math.Round(s.Oral * 0.3 + s.Exam * 0.5 + s.Performance * 0.2, 2);
    s.Remarks = s.FinalGrade >= 75 ? "Passed" : "Failed";
    students.Add(s);
    return Results.Created($"/api/students/{s.Id}", s);
});

app.MapDelete("/api/students/{id}", (HttpContext ctx, int id) =>
{
    if (ctx.Session.GetString("user") == null) return Results.Unauthorized();
    var s = students.FirstOrDefault(x => x.Id == id);
    if (s is null) return Results.NotFound();
    students.Remove(s);
    return Results.NoContent();
});

app.Run();

// ── AUTH ─────────────────────────────────────────────────────────────────
static bool Authenticate(string username, string password)
{
    var users = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "admin",  "admin123" },
        { "shinji", "pass1234" },
        { "badilla","pass5678" }
    };
    return users.TryGetValue(username, out string? stored) && stored == password;
}

// ── MODEL ─────────────────────────────────────────────────────────────────
public class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Oral { get; set; }
    public double Exam { get; set; }
    public double Performance { get; set; }
    public double FinalGrade { get; set; }
    public string Remarks { get; set; } = "";
}