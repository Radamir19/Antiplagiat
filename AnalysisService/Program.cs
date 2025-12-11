var builder = WebApplication.CreateBuilder(args);

// 1. Добавляем контроллеры
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Добавляем HTTP-клиент (чтобы Analysis мог стучаться в FileStorage)
builder.Services.AddHttpClient(); 

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection();

app.UseAuthorization();

//Включаем маршрутизацию
app.MapControllers();

app.Run();