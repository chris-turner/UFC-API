// See https://aka.ms/new-console-template for more information
using UFCDataImport;

Console.WriteLine("Hello, World!");
try
{
    await DataImport.getUFCStatsAsync();
}

catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
