using System.Reflection;

namespace Spamlang;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ArgumentAttribute : Attribute
{
    public string Name { get; }
    public string? ShortName { get; }
    public bool Required { get; } = false;

    public ArgumentAttribute(string name, string? shortName = null, bool required = false)
    {
        Name = name;
        ShortName = shortName;
        Required = required;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class PositionalArgsListAttribute : Attribute
{
}

// TODO: Shitty, rewrite
public static class ArgumentsParser
{
    public sealed class Result<T>
    {
        public T? Value { get; init; }
        public List<string> Errors { get; init; } = [];
    }

    private struct ParseParameter
    {
        public bool IsRequired { get; init; }
        public bool IsBoolean { get; init; }
        public string Name { get; init; }
        public string? ShortName { get; init; }
        public PropertyInfo Property { get; init; }
    }

    private class ParseParameters
    {
        public List<ParseParameter> Parameters { get; } = [];

        public void Add(ParseParameter parameter)
        {
            Parameters.Add(parameter);
        }

        public ParseParameter? GetParameter(string anyName)
        {
            return Parameters.FirstOrDefault(parameter => parameter.Name == anyName || parameter.ShortName == anyName);
        }

        public bool IsArg(string anyName)
        {
            return GetParameter(anyName) != null;
        }

        public bool IsBoolean(string anyName)
        {
            return GetParameter(anyName)?.IsBoolean ?? false;
        }
    }

    private class RawParsed
    {
        public readonly List<string> Positional = [];
        public readonly Dictionary<string, string?> Args = new();
        public readonly Dictionary<string, string?> ShortArgs = new();
        public readonly List<string> OrphanArgs = [];
    }

    public static Result<T> Parse<T>(string[] args) where T : new()
    {
        ParseParameters parameters = new();

        PropertyInfo? posArgsProperty = null;
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ArgumentAttribute>() is { } arg)
            {
                ParseParameter parameter = new()
                {
                    Name = arg.Name,
                    ShortName = arg.ShortName,
                    IsBoolean = property.PropertyType == typeof(bool),
                    IsRequired = arg.Required,
                    Property = property
                };
                parameters.Add(parameter);
            }
            else if (property.GetCustomAttribute<PositionalArgsListAttribute>() is not null)
            {
                posArgsProperty = property;
            }
        }

        RawParsed parsed = ParseArguments(args, parameters);

        List<string> errors = [];
        foreach (string orphanArg in parsed.OrphanArgs)
        {
            errors.Add($"Argument with no value specified: {orphanArg}");
        }

        object resultValue = new T();
        if (posArgsProperty != null)
        {
            posArgsProperty.SetValue(resultValue, parsed.Positional);
        }

        foreach (ParseParameter param in parameters.Parameters)
        {
            string? paramValue = null;
            string? paramShortValue = null;
            bool hasParamValue = parsed.Args.TryGetValue(param.Name, out paramValue);
            bool hasParamShortValue = param.ShortName != null &&
                                      parsed.ShortArgs.TryGetValue(param.ShortName, out paramShortValue);

            if (hasParamValue && hasParamShortValue)
            {
                errors.Add($"Both long and short specified: {param.Name}, {param.ShortName}");
                continue;
            }

            if (!hasParamValue && !hasParamShortValue)
            {
                if (param.IsRequired)
                {
                    errors.Add($"Missing required argument: {param.Name}");
                }

                continue;
            }

            string? value = hasParamValue ? paramValue : paramShortValue;
            string specifiedParamName = hasParamValue ? param.Name : param.ShortName!;
            if (value == null)
            {
                if (!param.IsBoolean)
                {
                    errors.Add($"No value specified for argument: {specifiedParamName}");
                    continue;
                }

                param.Property.SetValue(resultValue, true);
            }
            else
            {
                if (param.IsBoolean)
                {
                    errors.Add($"Boolean argument cannot have a value: {specifiedParamName}");
                    continue;
                }

                param.Property.SetValue(resultValue, value);
            }
        }

        Result<T> result = new()
        {
            Value = (T)resultValue,
            Errors = errors
        };

        return result;
    }

    private static RawParsed ParseArguments(string[] args, ParseParameters parameters)
    {
        RawParsed parsed = new();

        string? pendingName = null;
        string? pendingShortName = null;

        foreach (string arg in args)
        {
            if (arg.StartsWith("--"))
            {
                EnsureNoPending();
                string name = arg[2..];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (parameters.IsBoolean(name))
                {
                    parsed.Args[name] = null;
                }
                else if (parameters.IsArg(name))
                {
                    pendingName = name;
                }
                else
                {
                    parsed.OrphanArgs.Add(name);
                }
            }
            else if (arg.StartsWith('-'))
            {
                EnsureNoPending();
                string shortName = arg[1..];
                if (string.IsNullOrEmpty(shortName))
                {
                    continue;
                }

                if (parameters.IsBoolean(shortName))
                {
                    parsed.ShortArgs[shortName] = null;
                }
                else if (parameters.IsArg(shortName))
                {
                    pendingShortName = shortName;
                }
                else
                {
                    parsed.OrphanArgs.Add(shortName);
                }
            }
            else
            {
                if (pendingName != null)
                {
                    parsed.Args[pendingName] = arg;
                    pendingName = null;
                }
                else if (pendingShortName != null)
                {
                    parsed.ShortArgs[pendingShortName] = arg;
                    pendingShortName = null;
                }
                else
                {
                    parsed.Positional.Add(arg);
                }
            }
        }

        EnsureNoPending();

        return parsed;

        void EnsureNoPending()
        {
            if (pendingName != null)
            {
                parsed.OrphanArgs.Add(pendingName);
                pendingName = null;
            }

            if (pendingShortName != null)
            {
                parsed.OrphanArgs.Add(pendingShortName);
                pendingShortName = null;
            }
        }
    }
}