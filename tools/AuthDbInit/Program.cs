using AuthDbInit;

var dbPath = args.ElementAtOrDefault(0);
var username = args.ElementAtOrDefault(1);
var password = args.ElementAtOrDefault(2);

if (string.IsNullOrWhiteSpace(dbPath) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("[Error] Usage: dotnet run --project tools/AuthDbInit -- <db-path> <username> <password>");
    return 1;
}

var connectionString = dbPath.Contains('=') ? dbPath : $"Data Source={dbPath}";

var result = await AuthDbInitializer.InitializeAsync(connectionString, username, password);
if (result == InitializationResult.AlreadyExists)
{
    Console.WriteLine($"[Warning] User '{username}' already exists in '{dbPath}'; leaving it unchanged.");
}
else
{
    Console.WriteLine($"[Information] Created user '{username}' in '{dbPath}'.");
}

return 0;
