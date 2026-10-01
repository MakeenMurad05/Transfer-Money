using API.ExceptionHandling;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();



builder.Services.AddControllers();
var app = builder.Build();

app.UseExceptionHandler();  



app.MapControllers();


app.Run();
