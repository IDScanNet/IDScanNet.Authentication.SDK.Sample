using IDScanNet.Authentication;
using IDScanNet.Authentication.SDK;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Usage: IDScanNet.Authentication.SDK.Sample [folder]");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        Console.WriteLine("CreateService started");
        using var service = await CreateService();
        Console.WriteLine($"CreateService completed in {sw.ElapsedMilliseconds} ms");

        var folders = args.Length != 0
            ? args
            : new string[] { Path.Combine("scanSets", "Failed") };

        foreach (var folder in folders)
        {
            await Authenticate(service, folder);
        }

        Console.WriteLine("Completed.");
        Console.WriteLine("Press \"Enter\" to close the sample application");
        Console.ReadLine();
    }

    private static async Task<IAuthenticationService> CreateService(CancellationToken ct = default)
    {
        Directory.CreateDirectory("Authentication Logs");
        //Simple authentication settings
        var authenticationServiceSettings = new AuthenticationServiceSettings
        {
            //Set logging directory(default is c:\Users\Public\Documents\IDScan.net\Authentication.SDK\Logs\)
            LoggingDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Authentication Logs"),

            //HostDataDirectoryPath - folder with data-files; can be substituted with a different value in case it's part of an app and data-files are somewhere close
            HostDataDirectoryPath = @"C:\ProgramData\IDScan.net\IDScanNet.Authentication.SDK.Data",

            //HostDirectoryPath = @""C:\Program Files (x86)\IDScan.net\IDScanNet.Authentication.SDK\Host\"

            //Advanced authentication settings:
            //Host - pipe name for connection. If it's not stated, then the default one will be used
            //Port - ignore this for now, as it would be required sometime in the future
        };

        var service = new AuthenticationService(authenticationServiceSettings);

        //Fired when the document processing stage is changed
        service.ProcessingStageChanged += (_, s) => Console.WriteLine($"Processing: {s.Status}");

        //Fired when the error has occurred
        service.ErrorReceived += (_, s) => Console.WriteLine("Error: {s.Text}");

        //Asynchronously initialize authentication service
        await service.InitializeAsync(ct);

        return service;
    }

    private static async Task Authenticate(IAuthenticationService service, string folder, CancellationToken ct = default)
    {
        Console.WriteLine("----------------------------------------------------------------------------------------");
        Console.WriteLine($"Authenticate \"{folder}\":");

        //Create a new authentication request
        var request = new AuthenticationRequest();
        request.Id = Guid.NewGuid();
        request.Scan = CreateScanResult(folder);

        //Asynchronously authenticate document
        var sw = Stopwatch.StartNew();
        var response = await service.ProcessAsync(request, ct);
        Console.WriteLine($"Authentication Result for \"{folder}\" in : {sw.ElapsedMilliseconds} ms");

        Console.WriteLine(ResponseToString(response));
    }

    private static ScanResult CreateScanResult(string folder)
    {
        var result = new ScanResult();

        var documentDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder));
        foreach (var file in documentDir.EnumerateFiles())
        {
            if (string.Compare(file.Name, "Pdf417RawData.txt", true) == 0)
            {
                (result.RawItems ??= new()).Add(
                    RawDataSource.PDF417,
                    new RawData() { RawString = File.ReadAllText(file.FullName) });

                continue;
            }

            ImageType? imageType = file.Name.Split('.')[0].ToLower() switch
            {
                "normal" => ImageType.ColorFront,
                "normalback" => ImageType.ColorBack,
                "uv" => ImageType.UVFront,
                "uvback" => ImageType.UVBack,
                "ir" => ImageType.IRFront,
                "irback" => ImageType.IRBack,
                "face" => ImageType.Face,
                _ => null,
            };

            if (!imageType.HasValue ||
                result.ScannedImages?.ContainsKey(imageType.Value) == true)
            {
                continue;
            }

            (result.ScannedImages ??= new()).Add(
                imageType.Value,
                File.ReadAllBytes(file.FullName));
        }

        return result;
    }

    private static string ResponseToString(AuthenticationResponse response)
    {
        if (response.Result == null)
        {
            return "Result unassigned!";
        }

        var sb = new StringBuilder();
        sb
            .AppendLine()
            .AppendLine("Authentication status = " + response.Result?.AuthenticationStatus);

        sb
            .AppendLine()
            .AppendLine("Tests:");

        var testGroups = response.Result.Results.GroupBy(a => a.TestGroup).ToList();
        foreach (var testGroup in testGroups)
        {
            sb.AppendLine($"{testGroup.Key}:");
            foreach (var testResult in testGroup)
            {
                sb.AppendLine($"    {testResult.Name} - {testResult.Type} {testResult.TestStatus} {testResult.Confidence}");

                if (testResult.CrossMatches != null)
                {
                    foreach (var match in testResult.CrossMatches)
                    {
                        sb.AppendLine(
                            $"          {match.FieldName} - {match.Item1.DataSourceString} = {match.Item1.Value};  {match.Item2.DataSourceString} = {match.Item2.Value} Confidence = {testResult.Confidence}");
                    }
                }
            }
        }

        var jsonOpts = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        sb
            .AppendLine()
            .AppendLine("Document property value: ")
            .AppendLine(JsonSerializer.Serialize(response.Document, jsonOpts));

        sb
            .AppendLine()
            .AppendLine("PlainDocument property value: ")
            .AppendLine(JsonSerializer.Serialize(response.PlainDocument, jsonOpts));

        return sb.ToString();
    }
}