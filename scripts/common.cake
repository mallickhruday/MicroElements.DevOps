#load versioning.cake
#load scriptParam.cake
using System.Linq;
using System.ComponentModel;
using System.Reflection;

// todo: dependency
// done: auto creation
// todo: remove versioning.cake dependency
// done: factory to param
// DirectoryPath and FilePath ext
// done: DirectoryPath param!!!
// done: add resources dirs
// done: --devopsRoot --devopsVersion

/// <summary>
/// Converts value to ParamValue.
/// </summary>
public static ParamValue<T> ToParamValue<T>(this T value, ParamSource source = ParamSource.Conventions)
    => !Equals(value, default(T)) ? new ParamValue<T>(value, source) : ParamValue<T>.NoValue;

public static Type CakeGlobalType() => typeof(ScriptArgs).DeclaringType.GetTypeInfo();

public static ParamValue<T> ArgumentOrEnvVar<T>(this ICakeContext context, string name)
{
    if(context.HasArgument(name))
        return new ParamValue<T>(context.Argument<T>(name, default(T)), ParamSource.CommandLine);
    if(context.HasEnvironmentVariable(name))
        return new ParamValue<T>((T)Convert.ChangeType(context.EnvironmentVariable(name), typeof(T)), ParamSource.EnvironmentVariable);
    return new ParamValue<T>(default(T), ParamSource.NoValue);
}

public static string NormalizePath(this string path) => path.ToLowerInvariant().Replace('\\', '/').TrimEnd('/');

public static string GetVersionFromCommandLineArgs(ScriptArgs args)
{
    var context = args.Context;
    var commandLineArgs = System.Environment.GetCommandLineArgs();
    context.Debug("CommandLineArgs: "+System.String.Join(" ", commandLineArgs));

    string devops_version = "";
    foreach(var arg in commandLineArgs.Select(NormalizePath))
    {
        if(arg.Contains("tools") && arg.Contains("microelements.devops"))
        {
            //C:\Projects\ProjName\tools\microelements.devops\0.2.0\scripts\main.cake
            var segments = context.File(arg).Path.Segments;
            int index = System.Array.IndexOf(segments, "microelements.devops");
            if(index>0 && index<segments.Length-1)
            {
                devops_version = segments[index+1];
                break;
            }
        }
    }

    return devops_version;
}

public static DirectoryPath GetDevopsToolDir(this ScriptArgs args)
{
    var devops_version = GetVersionFromCommandLineArgs(args);
    var devops_tool_dir = args.ToolsDir/$"microelements.devops/{devops_version}";
    return devops_tool_dir;
}

public static string GetTemplate(this ScriptArgs args, string fileName)
{
    var templateFileName = args.Context.File(fileName).Path;

    if(templateFileName.IsRelative)
    {
        foreach (var templateDir in args.TemplatesDir.Values)
        {
            var fullTemplateFileName = templateDir.CombineWithFilePath(templateFileName);
            if(System.IO.File.Exists(fullTemplateFileName.FullPath))
            {
                templateFileName = fullTemplateFileName;
                break;
            }
        }
    }

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

public static IEnumerable<T> AsEnumerable<T>(this T value) => new T[]{value};

public static IEnumerable<T> AsEnumerable<T>(this object value) => (new T[]{(T)value});

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
