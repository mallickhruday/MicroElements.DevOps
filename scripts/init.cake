#load common.cake

// see: https://github.com/micro-elements/MicroElements.DevOps.Tutorial/blob/master/docs/01_project_structure.md
public static void CreateProjectStructure(this ScriptArgs args)
{
    var context = args.Context;

    if(context.DirectoryExists(args.SrcDir))
        context.Information("src already exists.");
    else
    {
        context.CreateDirectory(args.SrcDir);
        context.Information("src created.");
    }

    if(context.DirectoryExists(args.TestDir))
        context.Information("test already exists.");
    {
        context.CreateDirectory(args.TestDir);
        context.Information("test created.");
    }
}

public static void AddBuildProps(this ScriptArgs args)
{
    args.AddFileFromTemplate("Directory.Build.props.xml", args.SrcDir, "Directory.Build.props");
}

public static void AddEditorConfig(this ScriptArgs args)
{
    args.AddFileFromResource(".editorconfig", args.RootDir);
}

public static void CreateCommonProjectFiles(this ScriptArgs args)
{
    var context = args.Context;

    var version_props_file_name = args.RootDir + context.File("version.props");
    if(context.FileExists(version_props_file_name))
        context.Information("version.props file already exists.");
    else
    {
        var version_props_content = GetTemplate(args, "version.props.xml");
        System.IO.File.WriteAllText(version_props_file_name, version_props_content);
        context.Information("version.props created.");
    }

    var common_props_file_name = args.RootDir + context.File("common.props");
    if(context.FileExists(common_props_file_name))
        context.Information("common.props file already exists.");
    else
    {
        var common_props_content = GetTemplate(args, "common.props.xml");
        // common props filling
        FillProjectAttributes(args);
        common_props_content = FillTags(common_props_content, args);

        System.IO.File.WriteAllText(common_props_file_name, common_props_content);
        context.Information("common.props created.");
    }

    context.Information("Adding common.props import...");
    context.Information("Processing files: "+args.SrcDir.Path + "/**/*.csproj");
    var projectFiles = context.GetFiles(args.SrcDir.Path + "/**/*.csproj");
    foreach(var projectFile in projectFiles)
    {
        context.Information($"Processing file: {projectFile}");
        var lines = System.IO.File.ReadAllLines(projectFile.FullPath).ToList();
        var import = @"<Import Project=""..\..\common.props""/>";
        var containsImport = lines.FirstOrDefault(line=>line.Contains(import))!=null;
        if(!containsImport)
        {
            lines.Insert(1, $"  {import}");
            System.IO.File.WriteAllLines(projectFile.FullPath, lines);
            context.Information($"Project: {projectFile}; Import inserted: {import}");
        }
        else
            context.Information($"Project: {projectFile}; Import already exists: {import}");
    }
}

public static void FillProjectAttributes(this ScriptArgs args)
{
    /*
    <Product></Product>
    <Copyright></Copyright>
    <Authors></Authors>
    <PackageIconUrl></PackageIconUrl>
    <PackageProjectUrl></PackageProjectUrl>
    <PackageLicenseUrl></PackageLicenseUrl>
    <RepositoryType></RepositoryType>
    <RepositoryUrl></RepositoryUrl>
    */

    var result = ProcessUtils.StartProcessAndReturnOutput(args.Context, "git", "remote get-url origin");
    if(result.ExitCode==0)
    {
        args.SetParam("RepositoryType", "git");
        //Ex: https://github.com/micro-elements/MicroElements.DevOps
        var repoUrl = result.Output.TrimEnd();
        args.SetParam("RepositoryUrl", repoUrl);
        args.SetParam("PackageProjectUrl", repoUrl);

        var segments = repoUrl.Split('/');
        var serverIndex = Array.IndexOf(segments, "github.com");
        if(serverIndex>0&&serverIndex<segments.Length-2)
        {
            var userName = segments[serverIndex+1];
            args.SetParam("userName", userName);

            var projectName = segments[serverIndex+2];
            args.SetParamIfNotExists("projectName", projectName);

            //<PackageLicenseUrl>https://raw.githubusercontent.com/micro-elements/MicroElements.DevOps/master/LICENSE</PackageLicenseUrl>
            args.SetParam("PackageLicenseUrl", $"https://raw.githubusercontent.com/{userName}/{projectName}/master/LICENSE");
        }

        if(args.ContainsKey("projectName"))
        {
            args.SetParam("Product", args.GetStringParam("projectName"));
            args.SetParam("Copyright", $"{DateTime.Today.Year}");
        }

        //Authors
        if(args.ContainsKey("userName"))
        {
            args.SetParam("Authors", args.GetStringParam("userName"));
        }

        args.PrintParams();
    }
}

/// <summary>
/// Checks that gitignore exists. If not exists downloads from github.
/// </summary>
public static void CheckOrDownloadGitIgnore(this ScriptArgs args)
{
    var context = args.Context;
    var gitIgnoreFile = args.RootDir + context.File(".gitignore");
    var gitIgnoreFileName = gitIgnoreFile.Path.FullPath;
    var gitIgnoreExternalPath = "https://raw.githubusercontent.com/github/gitignore/master/VisualStudio.gitignore";

    if(context.FileExists(gitIgnoreFile.Path))
    {
        context.Information(".gitignore exists.");
    }
    else
    {
        context.DownloadFile(gitIgnoreExternalPath, gitIgnoreFile);
        context.Information($".gitignore downloaded from {gitIgnoreExternalPath}.");
    }   
}

/// <summary>
/// Adds cake rule to gitignore.
/// </summary>
public static void GitIgnoreAddCakeRule(this ScriptArgs args)
{
    var context = args.Context;
    var gitIgnoreFile = args.RootDir + context.File(".gitignore");
    var gitIgnoreFileName = gitIgnoreFile.Path.FullPath;
    var cakeRule = "tools/**";
    var cakeRuleCommented = "# tools/**";

    if(context.FileExists(gitIgnoreFile.Path))
    {  
        var gitIgnoreText = System.IO.File.ReadAllText(gitIgnoreFileName);
        if(gitIgnoreText.Contains(cakeRule) && !gitIgnoreText.Contains(cakeRuleCommented))
        {
            context.Information(".gitignore already has cake rules.");
            return;
        }

        var message = $"uncommented {cakeRule} in .gitignore.";
        var gitIgnoreChanged = gitIgnoreText.Replace(cakeRuleCommented, cakeRule);
        if(gitIgnoreChanged==gitIgnoreText)
        {
            message = $"added {cakeRule} to .gitignore.";
            gitIgnoreChanged = gitIgnoreText + Environment.NewLine + cakeRule;
        }

        System.IO.File.WriteAllText(gitIgnoreFileName, gitIgnoreChanged);
        context.Information(message);
    }
    else
    {
        context.Information(".gitignore does not exists. Download it from 'https://github.com/github/gitignore/blob/master/VisualStudio.gitignore'");
    }
}

public static void CreateProjects(this ScriptArgs args)
{
    var context = args.Context;
    string projectName = args.ProjectName.Value;
    string solutionFile = args.GetStringParam("solutionFile");

    var projectDir = args.SrcDir + context.Directory(projectName);

    if(context.DirectoryExists(projectDir))
        context.Information("projectDir already exists.");
    else
    {
        context.CreateDirectory(projectDir);
        context.Information("projectDir created.");

        // dotnet new classlib
        context.DotNetCoreTool(projectDir.Path.FullPath, "new", 
            new ProcessArgumentBuilder().Append("classlib").Append($"--output {projectName}") );
    }

    var testProjectDir = args.TestDir + context.Directory(projectName+".Tests");

    if(context.DirectoryExists(testProjectDir))
        context.Information("testProjectDir already exists.");
    else
    {
        context.CreateDirectory(testProjectDir);
        context.Information("testProjectDir created.");

        // dotnet new test project
        context.DotNetCoreTool(testProjectDir.Path.FullPath, "new", 
            new ProcessArgumentBuilder().Append("xunit").Append($"--output {projectName}.Tests") );
    }

    if(!context.FileExists(solutionFile))
    {
        // dotnet new sln
        context.StartProcess("dotnet", new ProcessSettings()
            .UseWorkingDirectory(args.RootDir)
            .WithArguments(arguments=>arguments.Append($"new sln --name {projectName}")));

        // dotnet sln add
        context.StartProcess("dotnet", new ProcessSettings()
            .UseWorkingDirectory(args.RootDir)
            .WithArguments(arguments=>arguments.Append($"sln add {projectDir}/{projectName}.csproj")));
        
        // dotnet sln add
        context.StartProcess("dotnet", new ProcessSettings()
            .UseWorkingDirectory(args.RootDir)
            .WithArguments(arguments=>arguments.Append($"sln add {testProjectDir}/{projectName}.Tests.csproj")));
    }
    else
    {
        context.Information($"Solution file {solutionFile} already exists.");
    }
}

public static void AddTravisFile(this ScriptArgs args)
{
    args.AddFileFromTemplate(".travis.yml", args.RootDir);
}

public static void AddCakeBootstrapFiles(this ScriptArgs args)
{
    args.AddFileFromResource("build.sh", args.RootDir);
    args.AddFileFromResource("build.ps1", args.RootDir);
}
