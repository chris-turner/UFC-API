// See https://aka.ms/new-console-template for more information
using UFCDataImport;

try
{
    await DataImport.GetUFCStatsAsync();
}

catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
