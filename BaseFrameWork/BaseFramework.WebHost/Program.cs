var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/preview", () => Results.Json(new
{
    title = "Inspector Preview",
    members = new object[]
    {
        new { kind = "integer", displayName = "Intensity", value = 3, step = 1 },
        new { kind = "double", displayName = "Scale", value = 12.5, step = 5 },
        new { kind = "enum", displayName = "Mode", value = "Ready", options = new [] { "Draft", "Ready", "Disabled" } },
        new { kind = "boolean", displayName = "Enabled", value = true },
        new { kind = "string", displayName = "Note", value = "Cross-platform preview" },
        new { kind = "method", displayName = "Apply Parameters", parameters = new object[]
            {
                new { kind = "double", name = "delta", value = 0.0, step = 5 },
                new { kind = "integer", name = "repeat", value = 0, step = 1 },
                new { kind = "boolean", name = "invert", value = false },
                new { kind = "enum", name = "mode", value = "Ready", options = new [] { "Draft", "Ready", "Disabled" } },
                new { kind = "string", name = "note", value = "" }
            }
        }
    }
}));

app.Run();
