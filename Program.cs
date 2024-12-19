using Microsoft.OpenApi.Models;
using UFCData.DB;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UFC API", Description = "UFC Info and Statistics", Version = "v1" });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UFC API V1");
    });
}


//app.MapGet("/", () => "Hello World!");

app.MapGet("/fightcards/{id}", (int id) => UFCDataDB.GetFightCard(id));
app.MapGet("/fightcards", () => UFCDataDB.GetFightCards());

app.Run();
