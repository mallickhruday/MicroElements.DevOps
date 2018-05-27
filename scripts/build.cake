#load common.cake

//////////////////////////////////////////////////////////////////////
// TOOL ARGUMENTS 
//////////////////////////////////////////////////////////////////////

public static void ToolArguments(ScriptArgs args)
{
    // todo: is there ability to do it best?
    var nugetSourcesArg = new string[]{args.nuget_source1, args.nuget_source2, args.nuget_source3, args.upload_nuget}.Where(s => !string.IsNullOrEmpty(s)).Aggregate("", (s, s1) => $@"{s} --source ""{s1}""");
    var runtimeArg = args.RuntimeName != "any" ? $" --runtime {args.RuntimeName}" : "";
    var sourceLinkArgs =" /p:SourceLinkCreate=true";
    var noSourceLinkArgs =" /p:SourceLinkCreate=false";
    var sourceLinkArgsFull =" /p:SourceLinkCreate=true /p:SourceLinkServerType={SourceLinkServerType} /p:SourceLinkUrl={SourceLinkUrl}";
    var testResultsDirArgs = $" --results-directory {args.TestResultsDir}";

    args.Param<string>("nugetSourcesArg")
        .WithValue(a=>new string[]{a.nuget_source1, a.nuget_source2, a.nuget_source3, a.upload_nuget}.Where(s => !string.IsNullOrEmpty(s)).Aggregate("", (s, s1) => $@"{s} --source ""{s1}"""))
        .Build();
}

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

public static void Build(ScriptArgs args)
{
    var context = args.Context;

    var nugetSourcesArg = new string[]{args.nuget_source1, args.nuget_source2, args.nuget_source3, args.upload_nuget}.Where(s => !string.IsNullOrEmpty(s)).Aggregate("", (s, s1) => $@"{s} --source ""{s1}""");
    var sourceLinkArgs =" /p:SourceLinkCreate=true";
    var noSourceLinkArgs =" /p:SourceLinkCreate=false";
    var sourceLinkArgsFull =" /p:SourceLinkCreate=true /p:SourceLinkServerType={SourceLinkServerType} /p:SourceLinkUrl={SourceLinkUrl}";

    var settings = new DotNetCoreBuildSettings 
    { 
        Configuration = args.Configuration,
        ArgumentCustomization = arg => arg
            .Append(nugetSourcesArg)
            .Append(noSourceLinkArgs)
    };

    var projectsMask = $"{args.SrcDir}/**/*.csproj";
    var projects = context.GetFiles(projectsMask).ToList();
    context.Information($"ProjectsMask: {projectsMask}, Found: {projects.Count} project(s).");
    foreach(var project in projects)
    {
        context.Information($"Building project: {project}");
        context.DotNetCoreBuild(project.FullPath, settings);
    }
}

public static void Test(ScriptArgs args)
{
    var context = args.Context;

    var testResultsDirArgs = $" --results-directory {args.TestResultsDir}";
    var projectsMask = $"{args.TestDir}/**/*.csproj";

    var test_projects = context.GetFiles(projectsMask).ToList();
    context.Information($"TestProjectsMask: {projectsMask}, Found: {test_projects.Count} test project(s).");
    for (int testProjNum = 0; testProjNum < test_projects.Count; testProjNum++)
    {
        var test_project = test_projects[testProjNum];
        var logFilePath = $"test-result-{testProjNum+1}.trx";
        var loggerArgs = $" --logger trx;logfilename={logFilePath}";
        var testSettings = new DotNetCoreTestSettings()
        {
            Configuration = args.Configuration,
            //NoBuild = true,
            ArgumentCustomization = arg => arg
                .Append(testResultsDirArgs)
                .Append(loggerArgs)
        };
        context.DotNetCoreTest(test_project.FullPath, testSettings);
    }
}
