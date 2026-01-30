using backEndGamesTito.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTRO DE SERVIÇOS (Injeção de Dependência)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("GamesTitoPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<backEndGamesTito.API.Services.EmailService>();

var app = builder.Build();

// 2. CONFIGURAÇÃO DO PIPELINE (A ordem importa aqui!)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // Primeiro: Garante que a conexão é segura

// A REGRA DE OURO: O CORS deve vir ANTES da Autenticação/Autorização
app.UseCors("GamesTitoPolicy");

app.UseAuthorization(); // Só depois de permitir o CORS verificamos as permissões

app.MapControllers(); // Por fim, entrega a requisição ao Controller

app.Run();