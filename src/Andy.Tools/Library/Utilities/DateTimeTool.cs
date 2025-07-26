using System.Globalization;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.Utilities;

/// <summary>
/// Tool for date and time operations.
/// </summary>
public class DateTimeTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "datetime_tool",
        Name = "Date Time Tool",
        Description = "Performs date and time operations including formatting, parsing, and calculations",
        Version = "1.0.0",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Parameters =
        [
            new()
            {
                Name = "operation",
                Description = "The date/time operation to perform",
                Type = "string",
                Required = true,
                AllowedValues =
                [
                    "now", "parse", "format", "add", "subtract", "diff", "convert_timezone",
                    "is_valid", "day_of_week", "day_of_year", "days_in_month", "is_leap_year",
                    "start_of_day", "end_of_day", "start_of_month", "end_of_month",
                    "start_of_year", "end_of_year", "business_days", "age_calculation"
                ]
            },
            new()
            {
                Name = "date_input",
                Description = "Input date/time string or timestamp",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "format",
                Description = "Date/time format string (e.g., 'yyyy-MM-dd HH:mm:ss')",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "target_format",
                Description = "Target format for formatting operations",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "timezone",
                Description = "Timezone identifier (e.g., 'UTC', 'America/New_York')",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "target_timezone",
                Description = "Target timezone for conversion operations",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "amount",
                Description = "Amount to add/subtract (number)",
                Type = "number",
                Required = false
            },
            new()
            {
                Name = "unit",
                Description = "Time unit for add/subtract operations",
                Type = "string",
                Required = false,
                AllowedValues = ["years", "months", "days", "hours", "minutes", "seconds", "milliseconds"]
            },
            new()
            {
                Name = "end_date",
                Description = "End date for difference calculations",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "culture",
                Description = "Culture identifier for parsing/formatting (e.g., 'en-US', 'fr-FR')",
                Type = "string",
                Required = false,
                DefaultValue = "en-US"
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var operation = GetParameter<string>(parameters, "operation");
        var dateInput = GetParameter<string>(parameters, "date_input");
        var format = GetParameter<string>(parameters, "format");
        var targetFormat = GetParameter<string>(parameters, "target_format");
        var timezone = GetParameter<string>(parameters, "timezone");
        var targetTimezone = GetParameter<string>(parameters, "target_timezone");
        var amount = GetParameter<double?>(parameters, "amount");
        var unit = GetParameter<string>(parameters, "unit");
        var endDate = GetParameter<string>(parameters, "end_date");
        var culture = GetParameter(parameters, "culture", "en-US");

        try
        {
            ReportProgress(context, $"Performing {operation} operation...", 20);

            var result = await PerformDateTimeOperationAsync(
                operation, dateInput, format, targetFormat, timezone, targetTimezone,
                amount, unit, endDate, culture, context);

            ReportProgress(context, "Date/time operation completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["operation_time"] = result.OperationTime,
                ["culture_used"] = culture
            };

            // Add operation-specific metadata
            foreach (var kvp in result.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }

            return ToolResults.Success(
                result.Output,
                $"Successfully performed {operation} operation",
                metadata
            );
        }
        catch (ArgumentException ex)
        {
            return ToolResults.InvalidParameter("date_input or parameters", dateInput ?? "N/A", ex.Message);
        }
        catch (FormatException ex)
        {
            return ToolResults.InvalidParameter("format or date_input", format ?? dateInput ?? "N/A", $"Invalid format: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Date/time operation failed: {ex.Message}", "DATETIME_ERROR", details: ex);
        }
    }

    private static async Task<DateTimeOperationResult> PerformDateTimeOperationAsync(
        string operation,
        string? dateInput,
        string? format,
        string? targetFormat,
        string? timezone,
        string? targetTimezone,
        double? amount,
        string? unit,
        string? endDate,
        string culture,
        ToolExecutionContext context)
    {
        var startTime = DateTime.UtcNow;
        var result = new DateTimeOperationResult();
        var cultureInfo = new CultureInfo(culture);

        try
        {
            result.Output = operation.ToLowerInvariant() switch
            {
                "now" => GetCurrentDateTime(timezone, targetFormat, result),
                "parse" => ParseDateTime(dateInput, format, cultureInfo, result),
                "format" => FormatDateTime(dateInput, format, targetFormat, cultureInfo, result),
                "add" => AddToDateTime(dateInput, amount, unit, format, cultureInfo, result),
                "subtract" => SubtractFromDateTime(dateInput, amount, unit, format, cultureInfo, result),
                "diff" => CalculateDateDifference(dateInput, endDate, unit, format, cultureInfo, result),
                "convert_timezone" => ConvertTimezone(dateInput, timezone, targetTimezone, format, result),
                "is_valid" => ValidateDateTime(dateInput, format, cultureInfo, result),
                "day_of_week" => GetDayOfWeek(dateInput, format, cultureInfo, result),
                "day_of_year" => GetDayOfYear(dateInput, format, cultureInfo, result),
                "days_in_month" => GetDaysInMonth(dateInput, format, cultureInfo, result),
                "is_leap_year" => IsLeapYear(dateInput, format, cultureInfo, result),
                "start_of_day" => GetStartOfDay(dateInput, format, targetFormat, cultureInfo, result),
                "end_of_day" => GetEndOfDay(dateInput, format, targetFormat, cultureInfo, result),
                "start_of_month" => GetStartOfMonth(dateInput, format, targetFormat, cultureInfo, result),
                "end_of_month" => GetEndOfMonth(dateInput, format, targetFormat, cultureInfo, result),
                "start_of_year" => GetStartOfYear(dateInput, format, targetFormat, cultureInfo, result),
                "end_of_year" => GetEndOfYear(dateInput, format, targetFormat, cultureInfo, result),
                "business_days" => CalculateBusinessDays(dateInput, endDate, format, cultureInfo, result),
                "age_calculation" => CalculateAge(dateInput, endDate, format, cultureInfo, result),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }
        finally
        {
            result.OperationTime = DateTime.UtcNow - startTime;
        }

        await Task.CompletedTask;
        return result;
    }

    private static object GetCurrentDateTime(string? timezone, string? targetFormat, DateTimeOperationResult result)
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                now = TimeZoneInfo.ConvertTimeFromUtc(now, timeZoneInfo);
                result.Metadata["timezone"] = timezone;
            }
            catch
            {
                result.Metadata["timezone_error"] = $"Unknown timezone: {timezone}";
            }
        }

        var formatted = !string.IsNullOrEmpty(targetFormat) ? now.ToString(targetFormat) : now.ToString("O");

        result.Metadata["current_utc"] = DateTime.UtcNow.ToString("O");
        result.Metadata["format_used"] = targetFormat ?? "ISO 8601";

        return new
        {
            formatted_date = formatted,
            iso_date = now.ToString("O"),
            unix_timestamp = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            unix_timestamp_ms = ((DateTimeOffset)now).ToUnixTimeMilliseconds(),
            year = now.Year,
            month = now.Month,
            day = now.Day,
            hour = now.Hour,
            minute = now.Minute,
            second = now.Second,
            day_of_week = now.DayOfWeek.ToString(),
            day_of_year = now.DayOfYear
        };
    }

    private static object ParseDateTime(string? dateInput, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Date input is required for parse operation");
        }

        DateTime parsedDate;

        if (!string.IsNullOrEmpty(format))
        {
            parsedDate = DateTime.ParseExact(dateInput, format, culture);
            result.Metadata["parse_method"] = "exact_format";
            result.Metadata["format_used"] = format;
        }
        else
        {
            parsedDate = DateTime.Parse(dateInput, culture);
            result.Metadata["parse_method"] = "auto_parse";
        }

        result.Metadata["input_string"] = dateInput;
        result.Metadata["culture_used"] = culture.Name;

        return new
        {
            parsed_date = parsedDate.ToString("O"),
            year = parsedDate.Year,
            month = parsedDate.Month,
            day = parsedDate.Day,
            hour = parsedDate.Hour,
            minute = parsedDate.Minute,
            second = parsedDate.Second,
            day_of_week = parsedDate.DayOfWeek.ToString(),
            day_of_year = parsedDate.DayOfYear,
            unix_timestamp = ((DateTimeOffset)parsedDate).ToUnixTimeSeconds()
        };
    }

    private static object FormatDateTime(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Date input is required for format operation");
        }

        if (string.IsNullOrEmpty(targetFormat))
        {
            throw new ArgumentException("Target format is required for format operation");
        }

        var parsedDate = ParseDateTimeInput(dateInput, format, culture);
        var formatted = parsedDate.ToString(targetFormat, culture);

        result.Metadata["input_date"] = dateInput;
        result.Metadata["target_format"] = targetFormat;
        result.Metadata["culture_used"] = culture.Name;

        return new
        {
            formatted_date = formatted,
            original_date = parsedDate.ToString("O"),
            format_used = targetFormat
        };
    }

    private static object AddToDateTime(string? dateInput, double? amount, string? unit, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Date input is required for add operation");
        }

        if (!amount.HasValue)
        {
            throw new ArgumentException("Amount is required for add operation");
        }

        if (string.IsNullOrEmpty(unit))
        {
            throw new ArgumentException("Unit is required for add operation");
        }

        var parsedDate = ParseDateTimeInput(dateInput, format, culture);
        var newDate = AddTimeUnit(parsedDate, amount.Value, unit);

        result.Metadata["original_date"] = parsedDate.ToString("O");
        result.Metadata["amount_added"] = amount.Value;
        result.Metadata["unit"] = unit;

        return new
        {
            original_date = parsedDate.ToString("O"),
            new_date = newDate.ToString("O"),
            amount_added = amount.Value,
            unit,
            difference_days = (newDate - parsedDate).TotalDays
        };
    }

    private static object SubtractFromDateTime(string? dateInput, double? amount, string? unit, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Date input is required for subtract operation");
        }

        if (!amount.HasValue)
        {
            throw new ArgumentException("Amount is required for subtract operation");
        }

        if (string.IsNullOrEmpty(unit))
        {
            throw new ArgumentException("Unit is required for subtract operation");
        }

        var parsedDate = ParseDateTimeInput(dateInput, format, culture);
        var newDate = AddTimeUnit(parsedDate, -amount.Value, unit);

        result.Metadata["original_date"] = parsedDate.ToString("O");
        result.Metadata["amount_subtracted"] = amount.Value;
        result.Metadata["unit"] = unit;

        return new
        {
            original_date = parsedDate.ToString("O"),
            new_date = newDate.ToString("O"),
            amount_subtracted = amount.Value,
            unit,
            difference_days = (parsedDate - newDate).TotalDays
        };
    }

    private static object CalculateDateDifference(string? dateInput, string? endDate, string? unit, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Start date is required for diff operation");
        }

        if (string.IsNullOrEmpty(endDate))
        {
            throw new ArgumentException("End date is required for diff operation");
        }

        var startDateTime = ParseDateTimeInput(dateInput, format, culture);
        var endDateTime = ParseDateTimeInput(endDate, format, culture);
        var timeSpan = endDateTime - startDateTime;

        var difference = unit?.ToLowerInvariant() switch
        {
            "years" => timeSpan.TotalDays / 365.25,
            "months" => timeSpan.TotalDays / 30.44, // Average month length
            "days" => timeSpan.TotalDays,
            "hours" => timeSpan.TotalHours,
            "minutes" => timeSpan.TotalMinutes,
            "seconds" => timeSpan.TotalSeconds,
            "milliseconds" => timeSpan.TotalMilliseconds,
            _ => timeSpan.TotalDays
        };

        result.Metadata["start_date"] = startDateTime.ToString("O");
        result.Metadata["end_date"] = endDateTime.ToString("O");
        result.Metadata["unit"] = unit ?? "days";

        return new
        {
            start_date = startDateTime.ToString("O"),
            end_date = endDateTime.ToString("O"),
            difference,
            unit = unit ?? "days",
            total_days = timeSpan.TotalDays,
            total_hours = timeSpan.TotalHours,
            total_minutes = timeSpan.TotalMinutes,
            total_seconds = timeSpan.TotalSeconds
        };
    }

    private static object ConvertTimezone(string? dateInput, string? timezone, string? targetTimezone, string? format, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Date input is required for timezone conversion");
        }

        if (string.IsNullOrEmpty(targetTimezone))
        {
            throw new ArgumentException("Target timezone is required for timezone conversion");
        }

        var parsedDate = ParseDateTimeInput(dateInput, format, CultureInfo.InvariantCulture);

        // If source timezone is specified, convert from that timezone to UTC first
        if (!string.IsNullOrEmpty(timezone))
        {
            var sourceTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            parsedDate = TimeZoneInfo.ConvertTimeToUtc(parsedDate, sourceTimeZone);
        }

        // Convert to target timezone
        var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimezone);
        var convertedDate = TimeZoneInfo.ConvertTimeFromUtc(parsedDate, targetTimeZone);

        result.Metadata["source_timezone"] = timezone ?? "Local";
        result.Metadata["target_timezone"] = targetTimezone;

        return new
        {
            original_date = parsedDate.ToString("O"),
            converted_date = convertedDate.ToString("O"),
            source_timezone = timezone ?? "Local",
            target_timezone = targetTimezone,
            offset_hours = targetTimeZone.GetUtcOffset(convertedDate).TotalHours
        };
    }

    private static object ValidateDateTime(string? dateInput, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            result.Metadata["validation_result"] = false;
            result.Metadata["error"] = "Input is null or empty";
            return new { is_valid = false, error = "Input is null or empty" };
        }

        try
        {
            DateTime parsedDate = !string.IsNullOrEmpty(format) ? DateTime.ParseExact(dateInput, format, culture) : DateTime.Parse(dateInput, culture);
            result.Metadata["validation_result"] = true;
            result.Metadata["parsed_date"] = parsedDate.ToString("O");

            return new
            {
                is_valid = true,
                parsed_date = parsedDate.ToString("O"),
                format_used = format ?? "auto-detect"
            };
        }
        catch (Exception ex)
        {
            result.Metadata["validation_result"] = false;
            result.Metadata["error"] = ex.Message;

            return new
            {
                is_valid = false,
                error = ex.Message,
                input = dateInput
            };
        }
    }

    private static object GetDayOfWeek(string? dateInput, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "day_of_week");

        result.Metadata["date"] = parsedDate.ToString("O");

        return new
        {
            date = parsedDate.ToString("O"),
            day_of_week = parsedDate.DayOfWeek.ToString(),
            day_of_week_number = (int)parsedDate.DayOfWeek,
            is_weekend = parsedDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
        };
    }

    private static object GetDayOfYear(string? dateInput, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "day_of_year");

        result.Metadata["date"] = parsedDate.ToString("O");

        return new
        {
            date = parsedDate.ToString("O"),
            day_of_year = parsedDate.DayOfYear,
            total_days_in_year = DateTime.IsLeapYear(parsedDate.Year) ? 366 : 365,
            remaining_days = (DateTime.IsLeapYear(parsedDate.Year) ? 366 : 365) - parsedDate.DayOfYear
        };
    }

    private static object GetDaysInMonth(string? dateInput, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "days_in_month");
        var daysInMonth = DateTime.DaysInMonth(parsedDate.Year, parsedDate.Month);

        result.Metadata["date"] = parsedDate.ToString("O");

        return new
        {
            date = parsedDate.ToString("O"),
            year = parsedDate.Year,
            month = parsedDate.Month,
            month_name = parsedDate.ToString("MMMM", culture),
            days_in_month = daysInMonth,
            is_leap_year = DateTime.IsLeapYear(parsedDate.Year)
        };
    }

    private static object IsLeapYear(string? dateInput, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "is_leap_year");
        var isLeap = DateTime.IsLeapYear(parsedDate.Year);

        result.Metadata["year"] = parsedDate.Year;

        return new
        {
            year = parsedDate.Year,
            is_leap_year = isLeap,
            days_in_year = isLeap ? 366 : 365,
            days_in_february = isLeap ? 29 : 28
        };
    }

    private static object GetStartOfDay(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "start_of_day");
        var startOfDay = parsedDate.Date;

        return FormatDateTimeResult(startOfDay, targetFormat, culture, result);
    }

    private static object GetEndOfDay(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "end_of_day");
        var endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1);

        return FormatDateTimeResult(endOfDay, targetFormat, culture, result);
    }

    private static object GetStartOfMonth(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "start_of_month");
        var startOfMonth = new DateTime(parsedDate.Year, parsedDate.Month, 1);

        return FormatDateTimeResult(startOfMonth, targetFormat, culture, result);
    }

    private static object GetEndOfMonth(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "end_of_month");
        var daysInMonth = DateTime.DaysInMonth(parsedDate.Year, parsedDate.Month);
        var endOfMonth = new DateTime(parsedDate.Year, parsedDate.Month, daysInMonth, 23, 59, 59, 999);

        return FormatDateTimeResult(endOfMonth, targetFormat, culture, result);
    }

    private static object GetStartOfYear(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "start_of_year");
        var startOfYear = new DateTime(parsedDate.Year, 1, 1);

        return FormatDateTimeResult(startOfYear, targetFormat, culture, result);
    }

    private static object GetEndOfYear(string? dateInput, string? format, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var parsedDate = ParseRequiredDateTime(dateInput, format, culture, "end_of_year");
        var endOfYear = new DateTime(parsedDate.Year, 12, 31, 23, 59, 59, 999);

        return FormatDateTimeResult(endOfYear, targetFormat, culture, result);
    }

    private static object CalculateBusinessDays(string? dateInput, string? endDate, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        if (string.IsNullOrEmpty(dateInput))
        {
            throw new ArgumentException("Start date is required for business days calculation");
        }

        if (string.IsNullOrEmpty(endDate))
        {
            throw new ArgumentException("End date is required for business days calculation");
        }

        var startDate = ParseDateTimeInput(dateInput, format, culture).Date;
        var endDateTime = ParseDateTimeInput(endDate, format, culture).Date;

        var businessDays = 0;
        var current = startDate;

        while (current <= endDateTime)
        {
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                businessDays++;
            }

            current = current.AddDays(1);
        }

        var totalDays = (endDateTime - startDate).Days + 1;
        var weekendDays = totalDays - businessDays;

        result.Metadata["start_date"] = startDate.ToString("O");
        result.Metadata["end_date"] = endDateTime.ToString("O");

        return new
        {
            start_date = startDate.ToString("O"),
            end_date = endDateTime.ToString("O"),
            business_days = businessDays,
            total_days = totalDays,
            weekend_days = weekendDays,
            percentage_business_days = Math.Round((double)businessDays / totalDays * 100, 2)
        };
    }

    private static object CalculateAge(string? dateInput, string? endDate, string? format, CultureInfo culture, DateTimeOperationResult result)
    {
        var birthDate = ParseRequiredDateTime(dateInput, format, culture, "age_calculation");
        var referenceDate = string.IsNullOrEmpty(endDate) ? DateTime.Today : ParseDateTimeInput(endDate, format, culture).Date;

        if (birthDate > referenceDate)
        {
            throw new ArgumentException("Birth date cannot be in the future");
        }

        var age = referenceDate.Year - birthDate.Year;
        if (birthDate.Date > referenceDate.AddYears(-age))
        {
            age--;
        }

        var totalDays = (referenceDate - birthDate).Days;
        var totalMonths = ((referenceDate.Year - birthDate.Year) * 12) + referenceDate.Month - birthDate.Month;
        if (referenceDate.Day < birthDate.Day)
        {
            totalMonths--;
        }

        result.Metadata["birth_date"] = birthDate.ToString("O");
        result.Metadata["reference_date"] = referenceDate.ToString("O");

        return new
        {
            birth_date = birthDate.ToString("O"),
            reference_date = referenceDate.ToString("O"),
            age_years = age,
            age_months = totalMonths,
            age_days = totalDays,
            age_hours = totalDays * 24,
            next_birthday = GetNextBirthday(birthDate, referenceDate).ToString("O"),
            days_until_birthday = (GetNextBirthday(birthDate, referenceDate) - referenceDate).Days
        };
    }

    // Helper methods
    private static DateTime ParseDateTimeInput(string? dateInput, string? format, CultureInfo culture)
    {
        return string.IsNullOrEmpty(dateInput)
            ? throw new ArgumentException("Date input cannot be null or empty")
            : !string.IsNullOrEmpty(format)
            ? DateTime.ParseExact(dateInput, format, culture)
            : DateTime.Parse(dateInput, culture);
    }

    private static DateTime ParseRequiredDateTime(string? dateInput, string? format, CultureInfo culture, string operation)
    {
        return string.IsNullOrEmpty(dateInput)
            ? throw new ArgumentException($"Date input is required for {operation} operation")
            : ParseDateTimeInput(dateInput, format, culture);
    }

    private static DateTime AddTimeUnit(DateTime date, double amount, string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "years" => date.AddYears((int)amount),
            "months" => date.AddMonths((int)amount),
            "days" => date.AddDays(amount),
            "hours" => date.AddHours(amount),
            "minutes" => date.AddMinutes(amount),
            "seconds" => date.AddSeconds(amount),
            "milliseconds" => date.AddMilliseconds(amount),
            _ => throw new ArgumentException($"Unknown time unit: {unit}")
        };
    }

    private static object FormatDateTimeResult(DateTime dateTime, string? targetFormat, CultureInfo culture, DateTimeOperationResult result)
    {
        var formatted = !string.IsNullOrEmpty(targetFormat) ? dateTime.ToString(targetFormat, culture) : dateTime.ToString("O");

        result.Metadata["target_format"] = targetFormat ?? "ISO 8601";

        return new
        {
            date_time = dateTime.ToString("O"),
            formatted,
            unix_timestamp = ((DateTimeOffset)dateTime).ToUnixTimeSeconds()
        };
    }

    private static DateTime GetNextBirthday(DateTime birthDate, DateTime referenceDate)
    {
        var nextBirthday = new DateTime(referenceDate.Year, birthDate.Month, birthDate.Day);
        if (nextBirthday <= referenceDate)
        {
            nextBirthday = nextBirthday.AddYears(1);
        }

        return nextBirthday;
    }

    private class DateTimeOperationResult
    {
        public object? Output { get; set; }
        public TimeSpan OperationTime { get; set; }
        public Dictionary<string, object?> Metadata { get; set; } = [];
    }
}
