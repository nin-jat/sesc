// Space Engineers Script Checker (SESC) checks your ingame scripts without the game running.
// May Clang be with you.
// - Ninjat 2026

using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Collections.Generic;

class Program {
  static int Main(string[] args) {

    List<string> codeLines = null;

    // If the user needs help. Give them help!
    if (args.Contains("--help") || args.Contains("-h")) {
      Console.WriteLine(HELP_TEXT);
      return 0;
    }

    string filePath = args.FirstOrDefault(arg => !arg.StartsWith('-')) ?? "unknown_file";
    bool editorOutput = args.Contains("--editor") || args.Contains("-e");

    // Read the code from stdin.
    if (Console.IsInputRedirected) {
      using (var reader = new StreamReader(Console.OpenStandardInput())) {
        string line;
        codeLines = new List<string>();
        while ((line = reader.ReadLine()) != null) {
          codeLines.Add(line);
        }
      }
    }
    // Read from provided file.
    else if (args.Length > 0) {
      if (File.Exists(filePath)) {
        codeLines = File.ReadAllLines(filePath).ToList();
      } else {
        Console.WriteLine($"File '{filePath}' not found");
        return 1;
      }
    }
    else {
      Console.WriteLine(HELP_TEXT);
      return 1;
    }

    // Insert the user's code between the in-game script's prefix and suffix.
    // This creates valid C# code that will compile.
    // TODO: Write this to a file (or pipe it) so regular C# tools can work with this.
    string wrappedCode = string.Concat(IGS_PREFIX, string.Join('\n', codeLines), IGS_SUFFIX);

    // This list stores all the dependencies/refrences/dlls required to compile an inagme script.
    var references = new List<MetadataReference>();

    // Get the system and dotnet 4.8 dependencies and add them to our list.
    var dotnetRefs = GetDotNetReferences();
    if (dotnetRefs == null || dotnetRefs.Count == 0) {
      Console.WriteLine("Failed to get dotnet 4.8 refrences.");
      return 1;
    }
    references.AddRange(dotnetRefs);

    // Guess where space engineers is installed, by default use 'Program Files (x86)'.
    string seBinDir = @"C:\Program Files (x86)\Steam\steamapps\collectionommon\SpaceEngineers\Bin64";

    // If where not on windows, assume where on linux and use the default steam location.
    if (!OperatingSystem.IsWindows())
      seBinDir = Path.Combine(
                  Environment.GetEnvironmentVariable("HOME") ?? "",
                  ".steam/steam/steamapps/common/SpaceEngineers/Bin64"
      );

    // If the user set 'SE_BIN_DIR' ignore everything and just use that.
    if (Environment.GetEnvironmentVariable("SE_BIN_DIR") != null)
      seBinDir = Environment.GetEnvironmentVariable("SE_BIN_DIR");

    // Get the refrences from the space engineers directoy required to compile an ingame script.
    var gameRefs = GetGameReferences(seBinDir);
    if (gameRefs == null || gameRefs.Count == 0) {
      Console.WriteLine($"Required Space Engineers Bin64 refrences not found at '{seBinDir}'");
      Console.WriteLine("Prehaps set the SE_BIN_DIR environment variable or read the manual.");
      return 1;
    }
    // Add the space engineers dependencies to our list.
    references.AddRange(gameRefs);

    // Setup the compiller with the user's code and the dependencies/refrences we got.
    var compilation = CSharpCompilation.Create(
        "SEScriptAssembly",
        new[] { CSharpSyntaxTree.ParseText(wrappedCode) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
    );

    // Create a memory stream to build our script into and biuld it.
    using (var ms = new System.IO.MemoryStream()) {
      var result = compilation.Emit(ms);

      // Go over each diagnostic item and tell the user how shit their code is.
      foreach (var diagnostic in result.Diagnostics) {
        
        // Modify the line numbers to compensate for the prefix we added.
        // If the user is geting negative line numbers, then somebody stuffed up.
        var lineSpan = diagnostic.Location.GetLineSpan();
        int adjustedLine = lineSpan.StartLinePosition.Line - IGS_PREFIX_LINES;

        // Extract the position of the error on the line.
        int col = lineSpan.StartLinePosition.Character;
        int width = int.Max(1, lineSpan.EndLinePosition.Character - lineSpan.StartLinePosition.Character);
        string pos = $"({adjustedLine + 1}:{col + 1})";

        // Set the colours if we need to.
        switch (diagnostic.Severity) {
          case DiagnosticSeverity.Error:
            Console.ForegroundColor = ConsoleColor.DarkRed;
            break;
          case DiagnosticSeverity.Warning:
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            break;
        }

        // Just work with errors from the user's code.
        if(adjustedLine >= 0 && adjustedLine < codeLines.Count) {

          // Write the error to the console.
          if (editorOutput) {
            Console.WriteLine(string.Join(':',
                  filePath,
                  (adjustedLine + 1).ToString(),
                  (col + 1).ToString(),
                  (col + 1 + width).ToString(),
                  diagnostic.Severity,
                  diagnostic.GetMessage()
            ));
          } else {
            Console.WriteLine($"{pos}: {diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetMessage()}");
            // Add the acutal line and an indicator to help the freshies out.
            Console.WriteLine($"    {codeLines[adjustedLine]}");
            Console.WriteLine($"    {new string(' ', col)}{new string('^', width)}");
          }
        }
        else if (diagnostic.Severity != DiagnosticSeverity.Hidden) {
          Console.WriteLine($"Internal Error: {diagnostic}");
        }
        // Undo the coulour changes.
        Console.ResetColor();
      }

      // Let the user know it's all okay and exit successfully.
      if (result.Success) {
        Console.WriteLine($"Compiled '{args[0]}' successfully");
        return 0;
      }
      // Rats!, it didn't compile. Exit bad.
      return 1;
    }
  }

  // Automatically find .NET Framework 4.8 reference assemblies from the NuGet package cache
  // This function is vibecoded, I couldn't be stuffed. It uses NUGET packages.
  // TODO: This looks fragile, It'll probably not work on windows. Prehaps use the Dotnet 4.8
  // that the game would have installed.
  static List<MetadataReference> GetDotNetReferences() {
    var references = new List<MetadataReference>();

    string nuGetPackagesDir = Path.Combine(
        Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
        Path.Combine(
          Environment.GetEnvironmentVariable("HOME") ?? "",
          ".nuget", "packages")
    );

    string refAssembliesPath = Path.Combine(
        nuGetPackagesDir,
        "microsoft.netframework.referenceassemblies.net48");

    if (Directory.Exists(refAssembliesPath)) {
      string targetDir = Directory.GetDirectories(refAssembliesPath).FirstOrDefault();
      if (targetDir != null) {
        string buildPath = Path.Combine(targetDir, "build", ".NETFramework", "v4.8");
        if (Directory.Exists(buildPath)) {
          foreach (var dll in Directory.GetFiles(buildPath, "*.dll")) {
            // Had to skip these wrapper and thunk dlls... why do they exist?
            if (dll.EndsWith("Wrapper.dll", StringComparison.OrdinalIgnoreCase) ||
                dll.EndsWith("Thunk.dll", StringComparison.OrdinalIgnoreCase)) {
              continue;
            }

            references.Add(MetadataReference.CreateFromFile(dll));
          }
        }

        string netStandardPath = Path.Combine(
            targetDir,
            "build",
            ".NETFramework",
            "v4.8",
            "Facades",
            "netstandard.dll"
          );

        if (!File.Exists(netStandardPath))
          netStandardPath = Directory.GetFiles(
            targetDir,
            "netstandard.dll",
            SearchOption.AllDirectories
          ).FirstOrDefault();

        if (netStandardPath != null && File.Exists(netStandardPath))
          references.Add(MetadataReference.CreateFromFile(netStandardPath));
      }
    }

    if (references.Count == 0) {
      Console.WriteLine("Error: Could not locate .NET Framework 4.8 reference assemblies.");
      Console.WriteLine("Perhaps run 'dotnet restore' to download dependencies.");
      return null;
    }

    return references;
  }

  // Automatically find Space Engineers game binaries.
  static List<MetadataReference> GetGameReferences(string seBin64) {
    // Bail if the directoy is invalid.
    if (!Directory.Exists(seBin64)) {
      return null;
    }
    var references = new List<MetadataReference>();

    string[] requiredGameDlls = {
            "Sandbox.Common.dll",
            "Sandbox.Game.dll",
            "SpaceEngineers.Game.dll",
            "VRage.Game.dll",
            "VRage.Library.dll",
            "VRage.Math.dll"
        };

    // Search for each required DLL and add it to our neat little list.
    foreach (var dll in requiredGameDlls) {
      string dllPath = Path.Combine(seBin64, dll);
      if (File.Exists(dllPath)) {
        references.Add(MetadataReference.CreateFromFile(dllPath));
      }
    }

    return references;
  }

  const string HELP_TEXT =@"Space Engineers Script Checker.
https://github.com/nin-jat/sesc

This is a small utilitiy checks your in-game scripts for common issues.
Your scripts can be written exactly like you see it in-game, so no need
for getting dependencies or adding using or mucking around with
templates.

Usage: sesc [options] script_file
Options:
  -e  --editor    Format the output suitable for editors and other tools.
                      (script_file:line:col_start:col_end:error_type:message)
  -h  --help      Get this help text.";

  // How many lines is inside the prefix.:
  const int IGS_PREFIX_LINES = 26;

  const string IGS_PREFIX =
@"using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;


namespace IngameScript
{
    public sealed class Program : MyGridProgram
    {
#region User Provided Ingame Script
";

  const string IGS_SUFFIX = @"
#endregion
    }
}";
}
