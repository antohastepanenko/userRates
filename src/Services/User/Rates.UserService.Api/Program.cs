using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Infrastructure;
using Rates.BuildingBlocks.Infrastructure.Security;
using Rates.UserService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddRatesConfiguration();

builder.Services.AddRatesJwt(builder.Configuration);
builder.Services.AddRatesInfrastructure(builder.Configuration);
builder.Services.AddRatesUserInfrastructure(builder.Configuration);
builder.Services.AddRatesApplication(typeof(Rates.UserService.Application.AssemblyMarker).Assembly);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRatesCommonPipeline();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Rates.UserService" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.MapControllers();

app.Run();

public partial class Program;