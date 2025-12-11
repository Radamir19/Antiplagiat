var builder = WebApplication.CreateBuilder(args);

//Добавляем поддержку контроллеров
builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Настройка Swagger (чтобы работал и в Production, и в Development внутри Docker)
app.UseSwagger();
app.UseSwaggerUI();

// Отключаем HTTPS (чтобы не было конфликтов в Docker)
// app.UseHttpsRedirection(); 

app.UseAuthorization();

//Включаем маршрутизацию контроллеров
app.MapControllers(); 

app.Run();