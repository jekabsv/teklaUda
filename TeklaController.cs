using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace TeklaUniversalUdaController
{
    public class TeklaController
    {
        public string TeklaBinPath = string.Empty;
        public bool IsTeklaLinked = false;
        public string DetectedVersion = "None";

        public Assembly ModelAssembly;
        public object ModelInstance;
        public object EventsInstance;

        public string ActiveProject = string.Empty;
        public string SelectedObjectInfo = "No object selected.";
        public object CurrentSelectedObject = null;

        private readonly HashSet<string> _validatedVersions = new() { "2022.0", "2023.0", "2024.0", "2025.0", "2026.0" };
        private readonly object _selectionLock = new object();

        private static readonly HashSet<string> _systemPassthrough = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Memory",
            "System.Buffers",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Numerics.Vectors",
            "System.Threading.Tasks.Extensions",
        };

        public void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveTeklaAssemblies;
            DetectTekla();
        }

        public void DetectTekla()
        {
            Process[] processes = Process.GetProcessesByName("TeklaStructures");
            if (processes.Length > 0)
            {
                try
                {
                    string exePath = processes[0].MainModule.FileName;
                    TeklaBinPath = Path.GetDirectoryName(exePath);

                    FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    DetectedVersion = $"{fileVersionInfo.ProductMajorPart}.{fileVersionInfo.ProductMinorPart}";

                    Console.WriteLine($"Location Path: {TeklaBinPath}");
                    Console.WriteLine($"Active Version: {DetectedVersion}");

                    if (!_validatedVersions.Contains(DetectedVersion))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("WARNING! Tekla version falls outside validated database... ");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR! Could not extract execution data trace: {ex.Message}");
                }
                finally
                {
                    foreach (var p in processes) p?.Dispose();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("WARNING! TeklaStructures process was not found running on this system.");
                Console.ResetColor();
            }
        }

        public Assembly OnResolveTeklaAssemblies(object sender, ResolveEventArgs args)
        {
            string shortName = new AssemblyName(args.Name).Name;

            if (!_systemPassthrough.Contains(shortName))
            {
                if (shortName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) || shortName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                    || shortName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase)
                    || shortName.Equals("netstandard", StringComparison.OrdinalIgnoreCase)
                    || shortName.Equals("WindowsBase", StringComparison.OrdinalIgnoreCase)
                    || shortName.Equals("PresentationCore", StringComparison.OrdinalIgnoreCase)
                    || shortName.Equals("PresentationFramework", StringComparison.OrdinalIgnoreCase)
                    || shortName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            string dllName = $"{shortName}.dll";

            bool requiresVersionMatch =
                shortName.Equals("Tekla.Structures", StringComparison.OrdinalIgnoreCase) ||
                shortName.StartsWith("Tekla.Structures.", StringComparison.OrdinalIgnoreCase);

            string expectedMajor = DetectedVersion.Split('.')[0];

            List<string> directPaths = new List<string>
            {
                Path.Combine(TeklaBinPath, dllName),
                Path.Combine(TeklaBinPath, "NetCoreRuntime", dllName),
                Path.Combine(TeklaBinPath, "nt", "bin", dllName),
                Path.Combine(TeklaBinPath, "plugins", dllName)
            };

            string rootDrive = Path.GetPathRoot(TeklaBinPath);
            string environmentCommon = Path.Combine(rootDrive, "TeklaStructures", DetectedVersion, "environments", "common", "extensions");
            directPaths.Add(Path.Combine(environmentCommon, dllName));

            foreach (string targetDllPath in directPaths)
            {
                if (targetDllPath.IndexOf("Net48Runtime", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!File.Exists(targetDllPath)) continue;

                if (requiresVersionMatch)
                {
                    string ver = GetFileVersion(targetDllPath);
                    if (ver == null || !ver.StartsWith(expectedMajor + "."))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"skipping version-mismatched copy ({ver ?? "unknown"}) at {targetDllPath}");
                        Console.ResetColor();
                        continue;
                    }
                }

                return LogAndLoad(targetDllPath);
            }

            string parentDir = Directory.GetParent(TeklaBinPath).FullName;
            if (Directory.Exists(parentDir))
            {
                //Console.WriteLine($"WARNING! Deep scanning installation tree under {parentDir}...");
                var foundFiles = Directory.GetFiles(parentDir, dllName, SearchOption.AllDirectories);

                var ranked = foundFiles
                    .Select(f => new
                    {
                        Path = f,
                        Version = GetFileVersion(f),
                        IsNet48 = f.IndexOf("Net48Runtime", StringComparison.OrdinalIgnoreCase) >= 0,
                        IsSubApp = f.IndexOf(@"\applications\", StringComparison.OrdinalIgnoreCase) >= 0,
                    })
                    .Where(c => !requiresVersionMatch ||
                                (c.Version != null && c.Version.StartsWith(expectedMajor + ".")))
                    .OrderBy(c => c.IsSubApp ? 1 : 0)
                    .ThenBy(c => c.IsNet48 ? 1 : 0)
                    .ToList();

                if (ranked.Count > 0)
                {
                    return LogAndLoad(ranked[0].Path);
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
                    $"No copy of {dllName} on disk matches running Tekla {DetectedVersion}. " +
                    $"Candidates examined: {foundFiles.Length}.");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UNRESOLVED {dllName}");
            Console.ResetColor();
            return null;
        }

        private string GetFileVersion(string path)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(path).FileVersion;
            }
            catch
            {
                return null;
            }
        }

        private Assembly LogAndLoad(string path)
        {
            string ver = GetFileVersion(path) ?? "unknown";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Success -> Loading v{ver} from: {path}");
            Console.ResetColor();
            return Assembly.LoadFrom(path);
        }

        public void ConnectToTekla()
        {
            if (IsTeklaLinked)
            {
                Console.WriteLine("Aborted: Connection is already active.");
                return;
            }
            if (string.IsNullOrEmpty(TeklaBinPath))
            {
                Console.WriteLine("Aborted: Valid Tekla binary directory is missing.");
                return;
            }

            try
            {
                Assembly.Load("Tekla.Structures");

                Console.WriteLine("Loading Tekla.Structures.Model assembly...");
                ModelAssembly = Assembly.Load("Tekla.Structures.Model");

                Type modelType = ModelAssembly.GetType("Tekla.Structures.Model.Model");
                if (modelType == null)
                {
                    throw new TypeLoadException("Could not extract Model reference structure from assemblies.");
                }

                Console.WriteLine("Instantiating Model instance...");
                ModelInstance = Activator.CreateInstance(modelType);

                MethodInfo connectionMethod = modelType.GetMethod("GetConnectionStatus");
                if (connectionMethod == null)
                {
                    throw new MissingMethodException("Method GetConnectionStatus not found on Model type.");
                }

                bool isConnected = (bool)connectionMethod.Invoke(ModelInstance, null);

                if (isConnected)
                {
                    IsTeklaLinked = true;
                    ResolveActiveProject(modelType);
                    RegisterSelectionEvents();
                }
                else
                {
                    SelectedObjectInfo = "Tekla detected, but no open model was found.";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Connection established with process, but no active model is open in Tekla Structures.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Exception trueException = (ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex;

                SelectedObjectInfo = $"Link Exception: {trueException.Message}";

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nERROR!: {trueException.Message}");
                Console.WriteLine($"Source: {trueException.Source}");
                Console.WriteLine($"Stack Trace:\n{trueException.StackTrace}\n");
                Console.ResetColor();
            }
        }

        public void ResolveActiveProject(Type modelType)
        {
            try
            {
                object modelInfo = modelType.GetMethod("GetInfo").Invoke(ModelInstance, null);
                Type infoType = modelInfo.GetType();

                string name = infoType.GetProperty("ModelName").GetValue(modelInfo) as string;
                string path = infoType.GetProperty("ModelPath").GetValue(modelInfo) as string;

                ActiveProject = $"{name} [{path}]";
                Console.WriteLine($"Connected to workspace: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING! Could not resolve project paths: {ex.Message}");
                ActiveProject = "Active project could not be tracked.";
            }
        }

        public void RegisterSelectionEvents()
        {
            try
            {
                Type eventsType = ModelAssembly.GetType("Tekla.Structures.Model.Events");
                if (eventsType == null)
                    throw new TypeLoadException("Tekla.Structures.Model.Events not found in Model assembly.");

                EventsInstance = Activator.CreateInstance(eventsType);

                EventInfo selectionEvent = eventsType.GetEvent("SelectionChange");
                if (selectionEvent == null)
                {
                    Console.WriteLine("Events on Events type:");
                    foreach (var e in eventsType.GetEvents())
                        Console.WriteLine($" - {e.Name} ({e.EventHandlerType?.Name})");
                    throw new MissingMemberException("SelectionChange not found.");
                }

                MethodInfo handlerMethod = typeof(TeklaController).GetMethod(
                    nameof(OnTeklaSelectionChangedCallback),
                    BindingFlags.NonPublic | BindingFlags.Instance);

                Delegate del = Delegate.CreateDelegate(selectionEvent.EventHandlerType, this, handlerMethod);
                selectionEvent.AddMethod.Invoke(EventsInstance, new object[] { del });

                eventsType.GetMethod("Register").Invoke(EventsInstance, null);
            }
            catch (Exception ex)
            {
                Exception real = (ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"ERROR! {real.GetType().Name}: {real.Message}");
                Console.ResetColor();
            }
        }

        private void OnTeklaSelectionChangedCallback()
        {
            lock (_selectionLock)
            {
                try
                {
                    Type selectorType = ModelAssembly.GetType("Tekla.Structures.Model.UI.ModelObjectSelector");
                    if (selectorType == null)
                    {
                        SelectedObjectInfo = "ModelObjectSelector type not found.";
                        return;
                    }

                    object selector = Activator.CreateInstance(selectorType);
                    object enumerator = selectorType.GetMethod("GetSelectedObjects").Invoke(selector, null);
                    Type enumType = enumerator.GetType();

                    bool hasNext = (bool)enumType.GetMethod("MoveNext").Invoke(enumerator, null);
                    if (hasNext)
                    {
                        CurrentSelectedObject = enumType.GetProperty("Current").GetValue(enumerator);
                        Type objType = CurrentSelectedObject.GetType();

                        object identifier = objType.GetProperty("Identifier").GetValue(CurrentSelectedObject);
                        Guid guid = (Guid)identifier.GetType().GetProperty("GUID").GetValue(identifier);

                        SelectedObjectInfo = $"Type: {objType.Name}\nGUID: {guid}";
                        Console.WriteLine($" {objType.Name} / {guid}");
                    }
                    else
                    {
                        CurrentSelectedObject = null;
                        SelectedObjectInfo = "No object selected inside workspace.";
                    }
                }
                catch (Exception ex)
                {
                    Exception real = (ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex;
                    SelectedObjectInfo = $"Error mapping object properties: {real.Message}";
                    Console.WriteLine($"ERROR! {real.Message}");
                }
            }
        }

        public void CommitUDA(string udaKey, string udaVal)
        {
            object target;
            lock (_selectionLock)
            {
                target = CurrentSelectedObject;
            }
            if (target == null)
                return;

            try
            {
                Type objType = target.GetType();

                MethodInfo selectMethod = objType.GetMethod("Select");
                selectMethod?.Invoke(target, null);

                MethodInfo setPropertyMethod = objType.GetMethod("SetUserProperty", new[] { typeof(string), typeof(string) });
                bool success = (bool)setPropertyMethod.Invoke(target, new object[] { udaKey, udaVal });

                if (success)
                {
                    objType.GetMethod("Modify").Invoke(target, null);

                    Type modelType = ModelInstance.GetType();
                    modelType.GetMethod("CommitChanges", new[] { typeof(string) })
                              .Invoke(ModelInstance, new object[] { $"Modified UDA: {udaKey}" });

                    Console.WriteLine($"Pushed UDA: {udaKey} = {udaVal}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR! failed: {ex.Message}");
            }
        }
    }
}