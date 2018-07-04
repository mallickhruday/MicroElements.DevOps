#load versioning.cake
#load scriptParam.cake
using System.Linq;
using System.ComponentModel;
using System.Reflection;

// todo: dependency
// todo: auto creation
// todo: remove versioning.cake dependency
// todo: factory to param
// DirectoryPath and FilePath ext
// todo: DirectoryPath param!!!
// todo: add resources dirs

/// <summary>
/// Converts value to ParamValue.
/// </summary>
public static ParamValue<T> ToParamValue<T>(this T value, ParamSource source = ParamSource.Conventions)
    => new ParamValue<T>(value, source);

public static Type CakeGlobalType() => typeof(ScriptArgs).DeclaringType.GetTypeInfo();

public static ParamValue<T> ArgumentOrEnvVar<T>(this ICakeContext context, string name)
{
    if(context.HasArgument(name))
        return new ParamValue<T>(context.Argument<T>(name, default(T)), ParamSource.CommandLine);
    if(context.HasEnvironmentVariable(name))
        return new ParamValue<T>((T)Convert.ChangeType(context.EnvironmentVariable(name), typeof(T)), ParamSource.EnvironmentVariable);
    return new ParamValue<T>(default(T), ParamSource.NoValue);
}

public static IEnumerable<ValueGetter<T>> ArgumentOrEnvVar<T>(string name)
{
    if(typeof(T)==typeof(string) || typeof(T)==typeof(bool))
    {
        yield return new ValueGetter<T>(
            a=>a.Context.HasArgument(name),
            a=>a.Context.Argument<T>(name, default(T)),
            ParamSource.CommandLine);
        yield return new ValueGetter<T>(
            a=>a.Context.HasEnvironmentVariable(name),
            a=>(T)Convert.ChangeType(a.Context.EnvironmentVariable(name), typeof(T)),
            ParamSource.EnvironmentVariable);
    }
    if(typeof(T)==typeof(DirectoryPath))
    {
        object ToDirPath(string input) => new DirectoryPath(input);
        yield return new ValueGetter<T>(
            a=>a.Context.HasArgument(name),
            a=>(T)ToDirPath(a.Context.Argument<string>(name, null)),
            ParamSource.CommandLine);
        yield return new ValueGetter<T>(
            a=>a.Context.HasEnvironmentVariable(name),
            a=>(T)ToDirPath(a.Context.EnvironmentVariable(name)),
            ParamSource.EnvironmentVariable);
    }
}

public static string GetVersionFromCommandLineArgs(ICakeContext context)
{
    var commandLineArgs = System.Environment.GetCommandLineArgs();
    context.Information("CommandLineArgs: "+System.String.Join(" ", commandLineArgs));

    string devops_version = "";
    foreach(var arg in commandLineArgs.Select(a=>a.ToLower()))
    {
        if(arg.Contains("microelements.devops"))
        {
            //C:\Projects\ProjName\tools\microelements.devops\0.2.0\scripts\main.cake
            var segments = context.File(arg).Path.Segments;
            int index = System.Array.IndexOf(segments, "microelements.devops");
            if(index>0 && index<segments.Length-1)
            devops_version = segments[index+1];
        }
    }

    return devops_version;
}

public static DirectoryPath GetDevopsToolDir(this ScriptArgs args)
{
    var devops_version       = GetVersionFromCommandLineArgs(args.Context);
    var devops_tool_dir      = args.ToolsDir.Value.Combine("microelements.devops").Combine(devops_version);
    return devops_tool_dir;
}

public static string GetTemplate(this ScriptArgs args, string fileName)
{
    var templateFileName = args.Context.File(fileName).Path;
    templateFileName = templateFileName.IsRelative? args.TemplatesDir.Value.CombineWithFilePath(templateFileName) : templateFileName;
    string templateText = System.IO.File.ReadAllText(templateFileName.FullPath);
    return templateText;
}

public static string GetResource(this ScriptArgs args, string fileName)
{
    var resourceFileName = args.Context.File(fileName).Path;
    resourceFileName = resourceFileName.IsRelative? args.ResourcesDir.Value.CombineWithFilePath(resourceFileName) : resourceFileName;
    string resourceText = System.IO.File.ReadAllText(resourceFileName.FullPath);
    return resourceText;
}

public static void AddFileFromResource(this ScriptArgs args, string name, DirectoryPath destinationDir, string destinationName = null)
{
    var context = args.Context;
    var destinationFile = destinationDir.CombineWithFilePath(destinationName??name);

    if(context.FileExists(destinationFile))
        context.Information($"{destinationFile} file already exists.");
    else
    {
        var content = args.GetResource($"{name}");
        System.IO.File.WriteAllText(destinationFile.FullPath, content);
        context.Information($"{destinationFile} created.");
    }
}

public static void AddFileFromTemplate(
    this ScriptArgs args,
    string name,
    DirectoryPath destinationDir,
    string destinationName = null,
    Func<string,string> modifyTemplate = null)
{
    var context = args.Context;
    var destinationFile = destinationDir.CombineWithFilePath(destinationName??name);

    if(context.FileExists(destinationFile))
        context.Information($"{destinationFile} file already exists.");
    else
    {
        var content = args.GetTemplate($"{name}");
        if(modifyTemplate!=null)
            content = modifyTemplate(content);
        System.IO.File.WriteAllText(destinationFile.FullPath, content);
        context.Information($"{destinationFile} created.");
    }
}

public static string FillTags(string inputXml, ScriptArgs args)
{
    foreach (var key in args.ParamKeys)
    {
        var value = args.GetStringParam(key);
        inputXml = inputXml.Replace($"${key}$", $"{value}");
        inputXml = inputXml.Replace($"{{{key}}}", $"{value}");
        inputXml = inputXml.Replace($"<{key}></{key}>", $"<{key}>{value}</{key}>");
    }
    return inputXml;
}

public static T CheckNotNull<T>(this T value, string paramName)
{
    if(value == null)
        throw new ArgumentNullException(paramName??"value");
    return value;
}

public static IEnumerable<T> NotNull<T>(this IEnumerable<T> collection) => collection ?? Array.Empty<T>();

public static ICollection<T> NotNull<T>(this ICollection<T> collection) => collection ?? Array.Empty<T>();

public static T[] NotNull<T>(this T[] collection) => collection ?? Array.Empty<T>();

public class ProcessUtils
{
    public static (int ExitCode, string Output) StartProcessAndReturnOutput(
        ICakeContext context,
        FilePath fileName,
        ProcessArgumentBuilder args,
        string workingDirectory = null,
        bool printOutput = false)
    {
        if(printOutput)
            context.Information($"{fileName} {args.Render()}");
        
        var processSettings = new ProcessSettings { Arguments = args, RedirectStandardOutput = true };
        if(workingDirectory!=null)
            processSettings.WorkingDirectory = workingDirectory;
        IEnumerable<string> redirectedStandardOutput;
        var exitCodeWithArgument = context.StartProcess(fileName, processSettings, out redirectedStandardOutput );

        StringBuilder outputString = new StringBuilder();
        foreach(var line in redirectedStandardOutput)
        {
            if(printOutput)
                context.Information(line);
            outputString.AppendLine(line);
        }
        return (exitCodeWithArgument, outputString.ToString());
    }
}

/// <summary>
/// Temporary sets logging verbosity.
/// </summary>
/// <example>
/// <code>
/// // Temporary sets logging verbosity to Diagnostic.
/// using(context.UseVerbosity(Verbosity.Diagnostic))
/// {
///     context.DotNetCoreBuild(project, settings);
/// }
/// </code>
/// </example>
public static VerbosityChanger UseVerbosity(this ICakeContext context, Verbosity newVerbosity) =>
     new VerbosityChanger(context.Log, newVerbosity);

/// <summary>
/// Temporary sets logging verbosity to Diagnostic.
/// </summary>
/// <example>
/// <code>
/// // Temporary sets logging verbosity to Diagnostic.
/// using(context.UseDiagnosticVerbosity())
/// {
///     context.DotNetCoreBuild(project, settings);
/// }
/// </code>
/// </example>
public static VerbosityChanger UseDiagnosticVerbosity(this ICakeContext context) =>
    context.UseVerbosity(Verbosity.Diagnostic);

public class VerbosityChanger : IDisposable
{
    ICakeLog _log;
    Verbosity _oldVerbosity;

    public VerbosityChanger(ICakeLog log, Verbosity newVerbosity)
    {
        _log = log;
        _oldVerbosity = log.Verbosity;
        _log.Verbosity = newVerbosity;
    }

    public void Dispose() => _log.Verbosity = _oldVerbosity;
}
