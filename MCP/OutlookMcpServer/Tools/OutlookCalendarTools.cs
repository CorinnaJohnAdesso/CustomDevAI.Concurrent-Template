using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace OutlookMcpServer;

[McpServerToolType]
internal class OutlookCalendarTools(TimeProvider timeProvider)
{
    private JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool, Description("Returns the current UTC date and time.")]
    public DateTimeOffset GetTime()
    {
        return timeProvider.GetUtcNow();
    }

    // -----------------------------------------------------------------------
    // Tool: get_calendar_appointments
    // -----------------------------------------------------------------------
    [McpServerTool, Description(
        "Reads appointments from the local Outlook calendar for a specified time range. " +
        "Returns subject, date/time, location, duration, organizer and description.")]
    public string GetCalendarAppointments(
        [Description("Start date of the range (format: YYYY-MM-DD), e.g. '2025-05-01'")] string startDate,
        [Description("End date of the range (format: YYYY-MM-DD), e.g. '2025-05-31'")] string endDate,
        [Description("Optional: search term to filter appointments (leave empty for all appointments)")] string? filter = null,
        [Description("Optional: maximum number of appointments to return (default: 100)")] int maxResults = 100)
    {
        // --- 1. Parse dates ---------------------------------------------------
        if (!DateOnly.TryParse(startDate, out var start))
            return $"Error: Invalid start date '{startDate}'. Please use format YYYY-MM-DD.";

        if (!DateOnly.TryParse(endDate, out var end))
            return $"Error: Invalid end date '{endDate}'. Please use format YYYY-MM-DD.";

        if (end < start)
            return "Error: The end date is before the start date.";

        if (maxResults is < 1 or > 1000)
            maxResults = 100;

        // --- 2. Connect to Outlook --------------------------------------------
        Outlook.Application? outlookApp = null;
        Outlook.NameSpace? ns = null;
        Outlook.MAPIFolder? calendarFolder = null;
        Outlook.MAPIFolder? privatFolder = null;
        Outlook.Items? items = null;
        Outlook.Items? privatItems = null;

        try
        {
            outlookApp = new Outlook.Application();
            ns = outlookApp.GetNamespace("MAPI");
            ns.Logon(Profile: Type.Missing, Password: Type.Missing,
                     ShowDialog: false, NewSession: false);

            calendarFolder = ns.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar);

            // --- 3. Build Outlook filter string (DASL) -------------------------
            var startDt = start.ToDateTime(TimeOnly.MinValue);
            var endDt = end.ToDateTime(new TimeOnly(23, 59, 59));
            string restrict = $"[Start] >= '{startDt:g}' AND [Start] <= '{endDt:g}'";

            // --- 4. Collect results from all calendars ------------------------
            var appointments = new List<AppointmentInfo>();

            // Default calendar
            items = calendarFolder.Items;
            items.IncludeRecurrences = true;
            items.Sort("[Start]");
            CollectFromItems(items, restrict, filter, maxResults, appointments);

            // "Privat" calendar (subfolder of the default calendar)
            try
            {
                privatFolder = calendarFolder.Folders["Privat"] as Outlook.MAPIFolder;
                if (privatFolder != null)
                {
                    privatItems = privatFolder.Items;
                    privatItems.IncludeRecurrences = true;
                    privatItems.Sort("[Start]");
                    CollectFromItems(privatItems, restrict, filter, maxResults, appointments);
                }
            }
            catch { /* ignore missing folder */ }

            if (appointments.Count == 0)
            {
                string filterHint = string.IsNullOrWhiteSpace(filter) ? "" : $" (filter: '{filter}')";
                return $"No appointments found in the range {start:yyyy-MM-dd} - {end:yyyy-MM-dd}{filterHint}.";
            }

            // --- 5. Format output --------------------------------------------
            return FormatAppointments(appointments, start, end, filter, maxResults);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(ex, jsonOptions);
        }
        finally
        {
            // Release COM objects
            if (privatItems != null) Marshal.ReleaseComObject(privatItems);
            if (privatFolder != null) Marshal.ReleaseComObject(privatFolder);
            if (items != null) Marshal.ReleaseComObject(items);
            if (calendarFolder != null) Marshal.ReleaseComObject(calendarFolder);
            if (ns != null) { try { ns.Logoff(); } catch { } Marshal.ReleaseComObject(ns); }
            if (outlookApp != null) Marshal.ReleaseComObject(outlookApp);
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void CollectFromItems(
        Outlook.Items? items,
        string restrict,
        string? filter,
        int maxResults,
        List<AppointmentInfo> appointments)
    {
        if (items == null) return;

        Outlook.Items? filtered = items.Restrict(restrict);
        if (filtered == null) return;

        foreach (object item in filtered)
        {
            if (appointments.Count >= maxResults) break;

            if (item is not Outlook.AppointmentItem appt)
                continue;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                bool match = (appt.Subject?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                          || (appt.Body?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                          || (appt.Location?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!match)
                {
                    Marshal.ReleaseComObject(appt);
                    continue;
                }
            }

            appointments.Add(new AppointmentInfo
            {
                Subject = appt.Subject ?? "(no subject)",
                Start = appt.Start,
                End = appt.End,
                Location = appt.Location,
                Organizer = appt.Organizer,
                IsAllDay = appt.AllDayEvent,
                IsRecurring = appt.IsRecurring,
                Categories = appt.Categories,
                Body = TruncateBody(appt.Body, 300),
                Sensitivity = appt.Sensitivity.ToString(),
            });

            Marshal.ReleaseComObject(appt);
        }
    }

    private string FormatAppointments(
        List<AppointmentInfo> appointments,
        DateOnly start, DateOnly end,
        string? filter, int maxResults)
    {
        var result = new
        {
            dateRange = new
            {
                from = start.ToString("yyyy-MM-dd"),
                to = end.ToString("yyyy-MM-dd"),
            },
            filter = string.IsNullOrWhiteSpace(filter) ? null : filter,
            count = appointments.Count,
            limitedTo = appointments.Count >= maxResults ? (int?)maxResults : null,
            appointments = appointments
                .OrderBy(a => a.Start)
                .Select(appt => new
                {
                    subject = appt.Subject,
                    start = appt.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = appt.End.ToString("yyyy-MM-ddTHH:mm:ss"),
                    isAllDay = appt.IsAllDay,
                    durationMinutes = appt.IsAllDay ? (int?)null : (int)(appt.End - appt.Start).TotalMinutes,
                    location = string.IsNullOrWhiteSpace(appt.Location) ? null : appt.Location,
                    organizer = string.IsNullOrWhiteSpace(appt.Organizer) ? null : appt.Organizer,
                    categories = string.IsNullOrWhiteSpace(appt.Categories) ? null : appt.Categories,
                    isRecurring = appt.IsRecurring,
                    sensitivity = TranslateSensitivity(appt.Sensitivity),
                    body = string.IsNullOrWhiteSpace(appt.Body) ? null : appt.Body,
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(result, jsonOptions);
    }

    private string TruncateBody(string? body, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        body = body.Trim().Replace("\r\n", " ").Replace("\n", " ");
        if (body.Length <= maxLen) return body;
        return body[..maxLen] + "...";
    }

    private string TranslateSensitivity(string sensitivity) => sensitivity switch
    {
        "olConfidential" => "Confidential",
        "olPersonal" => "Personal",
        "olPrivate" => "Private",
        _ => sensitivity,
    };

    // -----------------------------------------------------------------------
    // Data model
    // -----------------------------------------------------------------------
    private class AppointmentInfo
    {
        public string Subject { get; init; } = "";
        public DateTime Start { get; init; }
        public DateTime End { get; init; }
        public string? Location { get; init; }
        public string? Organizer { get; init; }
        public bool IsAllDay { get; init; }
        public bool IsRecurring { get; init; }
        public string? Categories { get; init; }
        public string Body { get; init; } = "";
        public string Sensitivity { get; init; } = "";
    }
}
