using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using KeyGate.Api.Entities;

namespace KeyGate.Api.Services;

public class SpreadsheetService
{
    private static readonly string[] IndividualHeaders = new[]
    {
        "FullName", "EmailOrEmployeeId", "Department", "Sex", "Age",
        "Province", "CityMunicipality", "Barangay", "Sectors", "ServiceAvailed", "Status"
    };

    private static readonly string[] ExportIndividualHeaders = new[]
    {
        "FullName", "EmailOrEmployeeId", "Department", "Sex", "Age",
        "Province", "CityMunicipality", "Barangay",
        "Student", "Gov't", "PWD", "LGBTQ", "Sr. Citizens", "OSY", "Indigent", "Others",
        "ServiceAvailed", "Status"
    };

    private static readonly string[] SectorNames = new[]
    {
        "Student", "Government Workforce", "PWD", "LGBTQ",
        "Sr. Citizens", "OSY", "Indigent", "Others"
    };

    private static readonly string[] SectorExportKeys = new[]
    {
        "Student", "Gov't", "PWD", "LGBTQ", "Sr. Citizens", "OSY", "Indigent", "Others"
    };

    private static List<string> ParseSectors(string? sectorsJson)
    {
        if (string.IsNullOrWhiteSpace(sectorsJson))
            return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(sectorsJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static string[] FilterHeaders(string[] allHeaders, HashSet<string>? selectedColumns)
    {
        if (selectedColumns is null || selectedColumns.Count == 0)
            return allHeaders;
        return allHeaders.Where(h => selectedColumns.Contains(h)).ToArray();
    }

    private static readonly string[] DeviceHeaders = new[]
    {
        "DeviceName", "DeviceFingerprint", "Location", "Status"
    };

    private static readonly string[] SessionHeaders = new[]
    {
        "Device", "User", "Started", "Ended", "Duration", "EndReason"
    };

    public record IndividualRow(
        string FullName,
        string EmailOrEmployeeId,
        string? Department,
        string? Sex,
        int? Age,
        string? Province,
        string? CityMunicipality,
        string? Barangay,
        string? Sectors,
        string? ServiceAvailed,
        string? Status);

    public record DeviceRow(
        string DeviceName,
        string DeviceFingerprint,
        string? Location,
        string? Status);

    public record ImportResult<T>(List<T> Rows, List<string> Errors);

    private static string HeaderToDisplay(string header)
    {
        return System.Text.RegularExpressions.Regex.Replace(header, "([a-z])([A-Z])", "$1 $2");
    }

    private static string FormatDuration(int? totalSeconds)
    {
        if (totalSeconds is null) return "";
        if (totalSeconds.Value < 60) return $"{totalSeconds.Value}s";
        var h = totalSeconds.Value / 3600;
        var m = (totalSeconds.Value % 3600) / 60;
        return h > 0 ? $"{h}h {m}m" : $"{m}m";
    }

    public byte[] ExportIndividualsToExcel(List<Individual> individuals, HashSet<string>? columns = null)
    {
        var headers = FilterHeaders(ExportIndividualHeaders, columns);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Individuals");

        var sectorKeys = new HashSet<string> { "Student", "Gov't", "PWD", "LGBTQ", "Sr. Citizens", "OSY", "Indigent", "Others" };
        int sectorStartCol = -1;
        int sectorCount = 0;

        int colIdx = 0;
        foreach (var h in headers)
        {
            colIdx++;
            var cell = ws.Cell(1, colIdx);
            cell.Value = HeaderToDisplay(h);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

            if (sectorKeys.Contains(h))
            {
                if (sectorStartCol == -1) sectorStartCol = colIdx;
                sectorCount++;
                var sub = ws.Cell(2, colIdx);
                sub.Value = h;
                sub.Style.Font.Bold = true;
                sub.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            }
            else
            {
                var merged = ws.Range(1, colIdx, 2, colIdx);
                merged.Merge();
                merged.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
        }

        if (sectorStartCol > 0 && sectorCount > 0)
        {
            var sectorHeader = ws.Range(1, sectorStartCol, 1, sectorStartCol + sectorCount - 1);
            sectorHeader.Merge();
            sectorHeader.Value = "SECTOR";
            sectorHeader.Style.Font.Bold = true;
            sectorHeader.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            sectorHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (int row = 0; row < individuals.Count; row++)
        {
            var i = individuals[row];
            var sectors = ParseSectors(i.Sectors);
            int dataColIdx = 0;
            foreach (var h in headers)
            {
                dataColIdx++;
                var val = h switch
                {
                    "FullName" => i.FullName ?? "",
                    "EmailOrEmployeeId" => i.EmailOrEmployeeId ?? "",
                    "Department" => i.Department ?? "",
                    "Sex" => i.Sex ?? "",
                    "Age" => i.Age?.ToString() ?? "",
                    "Province" => i.Province ?? "",
                    "CityMunicipality" => i.CityMunicipality ?? "",
                    "Barangay" => i.Barangay ?? "",
                    "Student" => sectors.Contains("Student") ? "✓" : "",
                    "Gov't" => sectors.Contains("Government Workforce") ? "✓" : "",
                    "PWD" => sectors.Contains("PWD") ? "✓" : "",
                    "LGBTQ" => sectors.Contains("LGBTQ") ? "✓" : "",
                    "Sr. Citizens" => sectors.Contains("Sr. Citizens") ? "✓" : "",
                    "OSY" => sectors.Contains("OSY") ? "✓" : "",
                    "Indigent" => sectors.Contains("Indigent") ? "✓" : "",
                    "Others" => sectors.Contains("Others") ? "✓" : "",
                    "ServiceAvailed" => i.ServiceAvailed ?? "",
                    "Status" => i.Status.ToString(),
                    _ => ""
                };
                ws.Cell(row + 3, dataColIdx).Value = val;
            }
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportDevicesToExcel(List<Device> devices, HashSet<string>? columns = null)
    {
        var headers = FilterHeaders(DeviceHeaders, columns);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Devices");

        for (int col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = HeaderToDisplay(headers[col]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        }

        for (int row = 0; row < devices.Count; row++)
        {
            var d = devices[row];
            var colIdx = 0;
            foreach (var h in headers)
            {
                object val = h switch
                {
                    "DeviceName" => d.DeviceName ?? "",
                    "DeviceFingerprint" => d.DeviceFingerprint ?? "",
                    "Location" => d.Location ?? "",
                    "Status" => d.Status.ToString(),
                    _ => ""
                };
                ws.Cell(row + 2, ++colIdx).Value = val.ToString() ?? "";
            }
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportSessionsToExcel(List<Session> sessions, HashSet<string>? columns = null)
    {
        var headers = FilterHeaders(SessionHeaders, columns);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sessions");

        for (int col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = HeaderToDisplay(headers[col]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        }

        for (int row = 0; row < sessions.Count; row++)
        {
            var s = sessions[row];
            var colIdx = 0;
            foreach (var h in headers)
            {
                object val = h switch
                {
                    "Device" => s.Device?.DeviceName ?? "",
                    "User" => s.Individual?.FullName ?? "",
                    "Started" => s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    "Ended" => s.EndedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Active",
                    "Duration" => FormatDuration(s.DurationSeconds),
                    "EndReason" => s.EndReason?.ToString() ?? "",
                    _ => ""
                };
                ws.Cell(row + 2, ++colIdx).Value = val.ToString() ?? "";
            }
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportIndividualsToCsv(List<Individual> individuals, HashSet<string>? columns = null)
    {
        var headers = FilterHeaders(ExportIndividualHeaders, columns);
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, new UTF8Encoding(true));
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        });

        foreach (var header in headers)
        {
            csv.WriteField(HeaderToDisplay(header));
        }
        csv.NextRecord();

        foreach (var i in individuals)
        {
            var sectors = ParseSectors(i.Sectors);
            foreach (var h in headers)
            {
                csv.WriteField(h switch
                {
                    "FullName" => i.FullName ?? "",
                    "EmailOrEmployeeId" => i.EmailOrEmployeeId ?? "",
                    "Department" => i.Department ?? "",
                    "Sex" => i.Sex ?? "",
                    "Age" => i.Age?.ToString() ?? "",
                    "Province" => i.Province ?? "",
                    "CityMunicipality" => i.CityMunicipality ?? "",
                    "Barangay" => i.Barangay ?? "",
                    "Student" => sectors.Contains("Student") ? "✓" : "",
                    "Gov't" => sectors.Contains("Government Workforce") ? "✓" : "",
                    "PWD" => sectors.Contains("PWD") ? "✓" : "",
                    "LGBTQ" => sectors.Contains("LGBTQ") ? "✓" : "",
                    "Sr. Citizens" => sectors.Contains("Sr. Citizens") ? "✓" : "",
                    "OSY" => sectors.Contains("OSY") ? "✓" : "",
                    "Indigent" => sectors.Contains("Indigent") ? "✓" : "",
                    "Others" => sectors.Contains("Others") ? "✓" : "",
                    "ServiceAvailed" => i.ServiceAvailed ?? "",
                    "Status" => i.Status.ToString(),
                    _ => ""
                });
            }
            csv.NextRecord();
        }

        csv.Flush();
        writer.Flush();
        return stream.ToArray();
    }

    public byte[] ExportDevicesToCsv(List<Device> devices, HashSet<string>? columns = null)
    {
        var headers = FilterHeaders(DeviceHeaders, columns);
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, new UTF8Encoding(true));
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        });

        foreach (var header in headers)
        {
            csv.WriteField(HeaderToDisplay(header));
        }
        csv.NextRecord();

        foreach (var d in devices)
        {
            foreach (var h in headers)
            {
                csv.WriteField(h switch
                {
                    "DeviceName" => d.DeviceName ?? "",
                    "DeviceFingerprint" => d.DeviceFingerprint ?? "",
                    "Location" => d.Location ?? "",
                    "Status" => d.Status.ToString(),
                    _ => ""
                });
            }
            csv.NextRecord();
        }

        csv.Flush();
        writer.Flush();
        return stream.ToArray();
    }

    public byte[] ExportSessionsToCsv(List<Session> sessions, HashSet<string>? columns = null)
    {
        var headers = FilterHeaders(SessionHeaders, columns);
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, new UTF8Encoding(true));
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        });

        foreach (var header in headers)
        {
            csv.WriteField(HeaderToDisplay(header));
        }
        csv.NextRecord();

        foreach (var s in sessions)
        {
            foreach (var h in headers)
            {
                csv.WriteField(h switch
                {
                    "Device" => s.Device?.DeviceName ?? "",
                    "User" => s.Individual?.FullName ?? "",
                    "Started" => s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    "Ended" => s.EndedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Active",
                    "Duration" => FormatDuration(s.DurationSeconds),
                    "EndReason" => s.EndReason?.ToString() ?? "",
                    _ => ""
                });
            }
            csv.NextRecord();
        }

        csv.Flush();
        writer.Flush();
        return stream.ToArray();
    }

    public ImportResult<IndividualRow> ImportIndividuals(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".csv"
            ? ImportIndividualsFromCsv(fileStream)
            : ImportIndividualsFromExcel(fileStream);
    }

    public ImportResult<DeviceRow> ImportDevices(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".csv"
            ? ImportDevicesFromCsv(fileStream)
            : ImportDevicesFromExcel(fileStream);
    }

    private ImportResult<IndividualRow> ImportIndividualsFromExcel(Stream stream)
    {
        var rows = new List<IndividualRow>();
        var errors = new List<string>();

        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        var headerMap = BuildHeaderMap(ws.Row(1), IndividualHeaders);

        for (int row = 2; row <= lastRow; row++)
        {
            var rowErrors = new List<string>();

            var fullName = GetCellString(ws, row, headerMap, "FullName");
            var emailOrId = GetCellString(ws, row, headerMap, "EmailOrEmployeeId");

            if (string.IsNullOrWhiteSpace(fullName))
                rowErrors.Add($"Row {row}: FullName is required.");
            if (string.IsNullOrWhiteSpace(emailOrId))
                rowErrors.Add($"Row {row}: EmailOrEmployeeId is required.");

            int? age = null;
            var ageStr = GetCellString(ws, row, headerMap, "Age");
            if (!string.IsNullOrWhiteSpace(ageStr) && int.TryParse(ageStr, out var parsedAge))
                age = parsedAge;

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            rows.Add(new IndividualRow(
                fullName!,
                emailOrId!,
                GetCellString(ws, row, headerMap, "Department"),
                GetCellString(ws, row, headerMap, "Sex"),
                age,
                GetCellString(ws, row, headerMap, "Province"),
                GetCellString(ws, row, headerMap, "CityMunicipality"),
                GetCellString(ws, row, headerMap, "Barangay"),
                GetCellString(ws, row, headerMap, "Sectors"),
                GetCellString(ws, row, headerMap, "ServiceAvailed"),
                GetCellString(ws, row, headerMap, "Status")));
        }

        return new ImportResult<IndividualRow>(rows, errors);
    }

    private ImportResult<DeviceRow> ImportDevicesFromExcel(Stream stream)
    {
        var rows = new List<DeviceRow>();
        var errors = new List<string>();

        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        var headerMap = BuildHeaderMap(ws.Row(1), DeviceHeaders);

        for (int row = 2; row <= lastRow; row++)
        {
            var rowErrors = new List<string>();

            var deviceName = GetCellString(ws, row, headerMap, "DeviceName");
            var fingerprint = GetCellString(ws, row, headerMap, "DeviceFingerprint");

            if (string.IsNullOrWhiteSpace(deviceName))
                rowErrors.Add($"Row {row}: DeviceName is required.");
            if (string.IsNullOrWhiteSpace(fingerprint))
                rowErrors.Add($"Row {row}: DeviceFingerprint is required.");

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            rows.Add(new DeviceRow(
                deviceName!,
                fingerprint!,
                GetCellString(ws, row, headerMap, "Location"),
                GetCellString(ws, row, headerMap, "Status")));
        }

        return new ImportResult<DeviceRow>(rows, errors);
    }

    private ImportResult<IndividualRow> ImportIndividualsFromCsv(Stream stream)
    {
        var rows = new List<IndividualRow>();
        var errors = new List<string>();

        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null
        });

        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(h => h.Trim()).ToList() ?? new();

        int rowNum = 1;
        while (csv.Read())
        {
            rowNum++;
            var rowErrors = new List<string>();

            var fullName = GetCsvField(csv, headers, "FullName");
            var emailOrId = GetCsvField(csv, headers, "EmailOrEmployeeId");

            if (string.IsNullOrWhiteSpace(fullName))
                rowErrors.Add($"Row {rowNum}: FullName is required.");
            if (string.IsNullOrWhiteSpace(emailOrId))
                rowErrors.Add($"Row {rowNum}: EmailOrEmployeeId is required.");

            int? age = null;
            var ageStr = GetCsvField(csv, headers, "Age");
            if (!string.IsNullOrWhiteSpace(ageStr) && int.TryParse(ageStr, out var parsedAge))
                age = parsedAge;

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            rows.Add(new IndividualRow(
                fullName!,
                emailOrId!,
                GetCsvField(csv, headers, "Department"),
                GetCsvField(csv, headers, "Sex"),
                age,
                GetCsvField(csv, headers, "Province"),
                GetCsvField(csv, headers, "CityMunicipality"),
                GetCsvField(csv, headers, "Barangay"),
                GetCsvField(csv, headers, "Sectors"),
                GetCsvField(csv, headers, "ServiceAvailed"),
                GetCsvField(csv, headers, "Status")));
        }

        return new ImportResult<IndividualRow>(rows, errors);
    }

    private ImportResult<DeviceRow> ImportDevicesFromCsv(Stream stream)
    {
        var rows = new List<DeviceRow>();
        var errors = new List<string>();

        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null
        });

        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(h => h.Trim()).ToList() ?? new();

        int rowNum = 1;
        while (csv.Read())
        {
            rowNum++;
            var rowErrors = new List<string>();

            var deviceName = GetCsvField(csv, headers, "DeviceName");
            var fingerprint = GetCsvField(csv, headers, "DeviceFingerprint");

            if (string.IsNullOrWhiteSpace(deviceName))
                rowErrors.Add($"Row {rowNum}: DeviceName is required.");
            if (string.IsNullOrWhiteSpace(fingerprint))
                rowErrors.Add($"Row {rowNum}: DeviceFingerprint is required.");

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            rows.Add(new DeviceRow(
                deviceName!,
                fingerprint!,
                GetCsvField(csv, headers, "Location"),
                GetCsvField(csv, headers, "Status")));
        }

        return new ImportResult<DeviceRow>(rows, errors);
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow, string[] expectedHeaders)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCell = headerRow.LastCellUsed();
        var maxCol = lastCell is not null ? lastCell.Address.ColumnNumber : 0;
        for (int col = 1; col <= maxCol; col++)
        {
            var value = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(value))
                map[value] = col;
        }
        return map;
    }

    private static string? GetCellString(IXLWorksheet ws, int row, Dictionary<string, int> headerMap, string header)
    {
        if (!headerMap.TryGetValue(header, out var col))
            return null;
        var val = ws.Cell(row, col).GetString().Trim();
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private static string? GetCsvField(CsvReader csv, List<string> headers, string header)
    {
        if (!headers.Contains(header))
            return null;
        var val = csv.GetField(header)?.Trim();
        return string.IsNullOrEmpty(val) ? null : val;
    }
}
