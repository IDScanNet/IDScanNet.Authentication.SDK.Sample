using IDScanNet.Authentication;
using IDScanNet.Authentication.SDK;
using System;
using System.Collections.Generic;
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
    private static string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

    static async Task Main(string[] args)
    {
        Directory.CreateDirectory(logFolder);

        Console.WriteLine("Usage: IDScanNet.Authentication.SDK.Sample [folder]");
        Console.WriteLine();

        Console.WriteLine("CreateService started");
        var sw = Stopwatch.StartNew();
        using var service = await CreateServiceAsync();
        Console.WriteLine($"CreateService completed in {sw.ElapsedMilliseconds} ms");

        var folder = args.Length != 0 ? args[0] : Path.Combine("scanSets", "Failed");

        await AuthenticateAsync(service, folder);

        Console.WriteLine("Completed.");
        Console.WriteLine("Press \"Enter\" to close the sample application");
        Console.ReadLine();
    }

    private static async Task<IAuthenticationService> CreateServiceAsync(CancellationToken ct = default)
    {
        // Simple authentication settings
        var authenticationServiceSettings = new AuthenticationServiceSettings
        {
            // Set logging directory(default is c:\Users\Public\Documents\IDScan.net\Authentication.SDK\Logs\)
            LoggingDirectoryPath = logFolder,

            // HostDirectoryPath = @"C:\Program Files (x86)\IDScan.net\IDScanNet.Authentication.SDK\Host\"
        };

        var service = new AuthenticationService(authenticationServiceSettings);

        // Fired when the document processing stage has changed
        service.ProcessingStageChanged += (_, s) => Console.WriteLine($"Processing: {s.Status}");

        // Fired when the error has occurred
        service.ErrorReceived += (_, s) => Console.WriteLine($"Error: {s.Text}");

        // Initialize authentication service
        await service.InitializeAsync(ct);

        Console.WriteLine($"HostVersion: {(await service.CheckAuthenticationHostAsync(ct)).HostVersion}");

        return service;
    }

    private static async Task AuthenticateAsync(IAuthenticationService service, string folder, CancellationToken ct = default)
    {
        Console.WriteLine($"Authenticate \"{folder}\":");

        // Create a new authentication request
        var request = CreateAuthenticationRequest(folder);

        // Authenticate document
        var sw = Stopwatch.StartNew();
        var response = await service.ProcessAsync(request, ct);

        var sb = new StringBuilder();
        sb.AppendLine(DateTime.Now.ToString("o"));
        sb.AppendLine($"Authentication Result for \"{folder}\" in : {sw.ElapsedMilliseconds} ms");
        sb.AppendLine($"HostVersion: {(await service.CheckAuthenticationHostAsync()).HostVersion}");
        ResponseToString(sb, response);
        Console.WriteLine(sb.ToString());

        File.AppendAllText(Path.Combine(logFolder, "SampleLog.txt"), sb.ToString());
    }

    private static AuthenticationRequest CreateAuthenticationRequest(string folder)
    {
        var result = new AuthenticationRequest();
        result.Scan = new ScanResult();

        var directory = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder));

        TryAddRawData(RawDataSource.PDF417, "Pdf417RawData");

        TryAddImage(ImageType.ColorFront, "ScanNormalFront");
        TryAddImage(ImageType.ColorBack, "ScanNormalBack");
        TryAddImage(ImageType.UVFront, "ScanUvFront");
        TryAddImage(ImageType.UVBack, "ScanUvBack");
        TryAddImage(ImageType.IRFront, "ScanIrFront");
        TryAddImage(ImageType.IRBack, "ScanIrBack");
        TryAddImage(ImageType.Face, "Face");

        return result;

        void TryAddRawData(RawDataSource rawDataSource, string filePattern)
        {
            var file = directory.EnumerateFiles($"*{filePattern}*.*").FirstOrDefault();
            if (file != null)
            {
                result.Scan.RawItems ??= new();
                result.Scan.RawItems.TryAdd(rawDataSource, new RawData() { RawString = File.ReadAllText(file.FullName) });
            }
        }

        void TryAddImage(ImageType imageType, string filePattern)
        {
            var file = directory.EnumerateFiles($"*{filePattern}*.*").FirstOrDefault();
            if (file != null)
            {
                result.Scan.ScannedImages ??= new();
                result.Scan.ScannedImages.TryAdd(imageType, File.ReadAllBytes(file.FullName));
            }
        }
    }

    private static void ResponseToString(StringBuilder sb, AuthenticationResponse response)
    {
        if (response.Result == null)
        {
            sb.AppendLine("Result unassigned!");
            return;
        }

        sb.AppendLine("Authentication status: " + response.Result.AuthenticationStatus);

        sb.AppendLine("Tests:");
        FormatAuthenticationTestResults(response.Result.GroupedResults);

        var jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        sb.AppendLine();
        sb.AppendLine("Document property value: ");
        sb.AppendLine(JsonSerializer.Serialize(response.Document, jsonOptions));

        sb.AppendLine();
        sb.AppendLine("PlainDocument property value: ");
        sb.AppendLine(JsonSerializer.Serialize(response.PlainDocument, jsonOptions));

        void FormatAuthenticationTestResults(IEnumerable<AuthenticationTestResult> testResults, string indentStr = "  ")
        {
            foreach (var testResult in testResults ?? Enumerable.Empty<AuthenticationTestResult>())
            {
                sb.AppendLine($"{indentStr}{testResult.Name}: {testResult.Code} {testResult.TestStatus} {testResult.Confidence}");

                foreach (var match in testResult.CrossMatches ?? Enumerable.Empty<CrossMatch>())
                {
                    sb.AppendLine(
                        $"  {indentStr}Match \"{match.FieldName}\": {match.Item1.DataSourceString} = {match.Item1.Value}; {match.Item2.DataSourceString} = {match.Item2.Value}; Confidence = {match.Confidence}");
                }

                FormatAuthenticationTestResults(testResult.ChildTests, indentStr + "  ");
            }
        }
    }
}