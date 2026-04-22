using Vintagestory.API.Common;

namespace PlayerModelLib.Utils;

/// <summary>
/// Is used to pass error messages down without need to pass ICoreAPI or ILogger up. <br/><br/>
/// default(Result) is success. <br/><br/>
/// Use <c>&amp;&amp;</c> and <c>||</c> operators when you want short-circuits, use <c>&amp;</c> and <c>|</c> when dont.
/// </summary>
public readonly struct Result
{
    public bool IsSuccess => _errors is null or { Count: 0 };
    public bool HasWarnings => _warnings is { Count: > 0 };
    public bool HasErrors => _errors is { Count: > 0 };
    public IReadOnlyList<string> Errors => _errors ?? [];
    public IReadOnlyList<string> Warnings => _warnings ?? [];

    public void LogErrorsAndWarnings(ICoreAPI api, object caller) => LogErrorsAndWarnings(api, caller.GetType());
    public void LogErrorsAndWarnings(ICoreAPI api, Type caller)
    {
        if (_errors is { Count: > 0 })
        {
            foreach (string error in _errors)
            {
                LoggerUtil.Error(api, caller, error);
            }
        }
        if (_warnings is { Count: > 0 })
        {
            foreach (string error in _warnings)
            {
                LoggerUtil.Warn(api, caller, error);
            }
        }
    }

    public static Result Success() => default;
    public static Result Error(string error) => new([error], null);
    public static Result Warning(string warning) => new(null, [warning]);

    public static Result operator +(Result a, Result b)
    {
        if (a is { _errors: null, _warnings: null } && b is { _errors: null, _warnings: null })
        {
            return default;
        }

        if (a is { _errors: null, _warnings: null })
        {
            return b;
        }
        
        if (b is { _errors: null, _warnings: null })
        {
            return a;
        }

        return new(
            MergeLists(a._errors, b._errors),
            MergeLists(a._warnings, b._warnings)
        );
    }
    public static Result operator &(Result a, Result b)
    {
        if (a is { _errors: null, _warnings: null } && b is { _errors: null, _warnings: null })
        {
            return default;
        }

        if (a is { _errors: null, _warnings: null })
        {
            return b;
        }
        
        if (b is { _errors: null, _warnings: null })
        {
            return a;
        }

        return new(
            MergeLists(a._errors, b._errors),
            MergeLists(a._warnings, b._warnings)
        );
    }
    public static Result operator |(Result a, Result b)
    {
        if (a.IsSuccess && b.IsSuccess)
        {
            if (a is { _warnings: null } && b is { _warnings: null })
                return default;

            return new(null, MergeLists(a._warnings, b._warnings));
        }

        if (a.IsSuccess) return new(null, a._warnings);
        if (b.IsSuccess) return new(null, b._warnings);

        return new(
            MergeLists(a._errors, b._errors),
            MergeLists(a._warnings, b._warnings)
        );
    }

    public static bool operator true(Result r) => r.IsSuccess;
    public static bool operator false(Result r) => !r.IsSuccess;


    public override string ToString()
    {
        return IsSuccess switch
        {
            true when HasWarnings => $"Success ({_warnings!.Count} warning(s))",
            true => "Success",
            _ => $"Failure ({_errors!.Count} error(s))"
        };
    }



    private Result(List<string>? errors, List<string>? warnings)
    {
        _errors = errors;
        _warnings = warnings;
    }


    private readonly List<string>? _errors;
    private readonly List<string>? _warnings;


    private static List<string>? MergeLists(List<string>? first, List<string>? second)
    {
        if (first is null or { Count: 0 })
        {
            return second;
        }
        
        if (second is null or { Count: 0 })
        {
            return first;
        }

        List<string> merged = [.. first, .. second];
        return merged;
    }
}