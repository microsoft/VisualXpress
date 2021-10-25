// Copyright Microsoft Corp. All Rights Reserved.
using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.Specialized;
using System.Text;

namespace Microsoft.VisualXpress
{
    // RunUAT.bat MegaXGE -Target{n}="{Target} {PlatformA}|{PlatformB}|{PlatformC} {ConfigurationA}|{ConfigurationB}|{ConfigurationC}"
    // RunUAT.bat MegaXGE -Target1="GearGame Win64|UWP64|XboxOne Debug -Target2="GearGameEditor Win64 Debug"

    [PluginCommand("buildmegaxge", "Build selected project(s) using MegaXGE")]
    [PluginCommandOption(PluginCommandBuildMegaXge.OptionNameProjectName, "The project name to build", 1, PlugCommandOptionFlag.Required)]
    [PluginCommandOption(PluginCommandBuildMegaXge.OptionNameProcessPath, "The path to the executable to run for the build", 1, PlugCommandOptionFlag.Required)]
    [PluginCommandOption(PluginCommandBuildMegaXge.OptionNameProcessArgs, "Any arguments required for the build exectuable", 1, PlugCommandOptionFlag.Optional)]
    public class PluginCommandBuildMegaXge : PluginCommand
    {
        public const string OptionNameProjectName = "-pn";
        public const string OptionNameProcessPath = "-pr";
        public const string OptionNameProcessArgs = "-args";

        public static String GlobalConfig { get; set; }

        private bool Build(Project project, String path, String args, String command)
        {
            if (project == null)
            {
                Log.Error($"'{nameof(project)}' argument not provided.");
                return false;
            }

            if (String.IsNullOrEmpty(path))
            {
                Log.Error($"'{nameof(path)}' argument not provided.");
                return false;
            }

            if (String.IsNullOrEmpty(command))
            {
                Log.Error($"'{nameof(command)}' argument not provided.");
                return false;
            }

            // The DLR is coming to our rescue here.. For some reason, the VS team have different 
            // assembly identifiers for each version of Microsoft.VisualStudio.VCProjectEngine 
            // for each release of Visual Studio. You can't choose which specific assembly to 
            // deploy to a specific Visual Studio version when you install the .vsix extension,
            // so you are stuck if you want to support multiple versions (e.g. 2015 and 2017).

            // To get around this, we are just casting to dynamic and letting the runtime
            // dynamically bind to the correct version.
            var Project = project?.Object as dynamic /* VCProject */;

            if (Project == null)
            {
                Log.Error($"Valid project object not found.");
                return false;
            }

            var ActiveConfiguration = Project.ActiveConfiguration;

            if (ActiveConfiguration == null)
            {
                Log.Error($"No active configuration found.");
                return false;
            }

            Package.ActiveDTE2.ExecuteCommand("View.Output");

            var Command = $"{path} {args} {command}";

            ActiveConfiguration.BuildWithProperty(0 /*bldActionTypes.TOB_Build*/,
                "NMakeBuildCommandLine", Command, null);

            return true;
        }

        // https://msdn.microsoft.com/en-us/library/hb23x61k(v=vs.80).aspx
        private const String vsProjectItemKindVisualCPP = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        private const String vsProjectItemKindVisualCSharp = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

        private static Project FindProject(ProjectItem project, String name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null)
            {
                return null;
            }

            if (project.Kind == Constants.vsProjectItemKindSolutionItems)
            {
                if (project.SubProject != null)
                {
                    var Result = FindProject(project.SubProject, name);

                    if (Result != null)
                    {
                        return Result;
                    }
                }
            }

            return null;
        }

        private static Project FindProject(Project project, String name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null)
            {
                return null;
            }

            if (project.Kind == Constants.vsProjectKindSolutionItems)
            {
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem ProjectItem in project.ProjectItems)
                    {
                        var Result = FindProject(ProjectItem, name);

                        if (Result != null)
                        {
                            return Result;
                        }
                    }
                }
            }
            else
            {
                if (project.Name.Equals(name, 
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        private Project FindProject(String name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project Project in Package.ActiveDTE2.Solution.Projects)
            {
                var Result = FindProject(Project, name);

                if (Result != null)
                {
                    return Result;
                }
            }

            return null;
        }

        public override bool Execute(PluginCommandOptions options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var ProjectName = options.GetFlag<string>(OptionNameProjectName);

            if (String.IsNullOrEmpty(ProjectName))
            {
                Log.Error($"Project name ({OptionNameProjectName}) argument not provided.");
                return false;
            }

            var ProcessPath = options.GetFlag<string>(OptionNameProcessPath);

            if (String.IsNullOrEmpty(ProcessPath))
            {
                Log.Error($"Process path ({OptionNameProcessPath}) argument not provided.");
                return false;
            }

            var ProcessArgs = options.GetFlag<string>(OptionNameProcessArgs);
            var Project = FindProject(ProjectName);

            if (Project == null)
            {
                Log.Error($"Unable to find project '{ProjectName}'.. Is this a valid solution? Is it loaded yet?");
                return false;
            }

            var Thread = new System.Threading.Thread(new ThreadStart(() =>
            {
                var ViewModel = new WindowViewModel(Package, ProjectName);
                ViewModel.Deserialize(GlobalConfig);

                var Window = new BuildMegaXgeWindow()
                {
                    DataContext = ViewModel,
                    Topmost = true
                };

                var IsBuildRequested = false;

                ViewModel.OnBuildRequested += () =>
                {
                    IsBuildRequested = true;
                    Window.Close();
                };

                Window.ShowDialog();

                GlobalConfig = ViewModel.Serialize(SerializeFlags.ForVisualStudio);

                if (IsBuildRequested)
                {
                    Build(Project, ProcessPath, ProcessArgs, 
                        ViewModel.Serialize(SerializeFlags.ForUnrealAutomationTool));
                }

            }));

            Thread.SetApartmentState(ApartmentState.STA);
            Thread.Start();

            return true;
        }

        #region ViewModels

        private class ValueViewModel : ViewModelWithOwner<ComponentViewModel>, IMultiSelectable
        {
            private String _Value;
            private bool _IsSelected;

            public ValueViewModel(ComponentViewModel owner, String value, bool selected = false)
                : base(owner)
            {

                Value = value;
                IsSelected = selected;
            }

            public String Value
            {
                get
                {
                    return _Value;
                }
                set
                {
                    _Value = value;
                    RaisePropertyChanged();
                }
            }

            public bool IsMultiSelect
            {
                get
                {
                    if (Owner != null)
                    {
                        return Owner.IsMultiSelect;
                    }

                    return false;
                }
            }

            public String GetOutValue()
            {
                if (Owner != null && Owner.ValueConverterOut != null)
                {
                    return Owner.ValueConverterOut(Value);
                }

                return Value;
            }

            public String GetInValue()
            {
                if (Owner != null && Owner.ValueConverterIn != null)
                {
                    return Owner.ValueConverterIn(Value);
                }

                return Value;
            }

            public bool IsSelected
            {
                get
                {
                    return _IsSelected;
                }
                set
                {
                    _IsSelected = value;
                    RaisePropertyChanged();

                    if (Owner != null)
                    {
                        Owner.RaiseSelectedValuesChanged();
                    }
                }
            }
        }

        private class ComponentViewModel : ViewModelWithOwner<TargetViewModel>, IMultiSelectable
        {
            private bool _IsMultiSelect;
            private ValueViewModel _SelectedValue;

            public ComponentViewModel(TargetViewModel owner,
                Func<String, String> inConverter = null,
                Func<String, String> outConverter = null,
                params String[] values)
                : base(owner)
            {
                ValueConverterIn = inConverter;
                ValueConverterOut = outConverter;

                Values = new ObservableCollection<ValueViewModel>();

                if (values != null)
                {
                    foreach (var Value in values)
                    {
                        var ValueModel = new ValueViewModel(this, Value);
                        ValueModel.Value = ValueModel.GetInValue();

                        Values.Add(ValueModel);
                    }
                }

                SelectedValue = Values.FirstOrDefault();

                if (SelectedValue != null)
                {
                    SelectedValue.IsSelected = true;
                }

                RaiseSelectedValuesChanged();
            }

            public ObservableCollection<ValueViewModel> Values { get; private set; }

            public Func<String, String> ValueConverterIn { get; private set; }
            public Func<String, String> ValueConverterOut { get; private set; }

            public String DisplayValue
            {
                get
                {
                    if (IsMultiSelect)
                    {
                        return String.Join(" | ", GetSelectedValues().Select(x => x.Value));
                    }
                    else
                    {
                        return SelectedValue?.Value;
                    }
                }
            }

            public ValueViewModel SelectedValue
            {
                get
                {
                    return _SelectedValue;
                }
                set
                {
                    _SelectedValue = value;

                    RaisePropertyChanged();
                    RaiseSelectedValuesChanged();
                }
            }

            public IEnumerable<ValueViewModel> GetSelectedValues()
            {
                if (!IsMultiSelect)
                {
                    yield return SelectedValue;
                }
                else
                {
                    foreach (var Value in Values.Where(x => x.IsSelected))
                    {
                        yield return Value;
                    }
                }
            }

            public bool IsValid
            {
                get
                {
                    return GetSelectedValues().Any();
                }
            }

            public bool IsMultiSelect
            {
                get
                {
                    return _IsMultiSelect;
                }
                set
                {
                    _IsMultiSelect = value;
                    RaisePropertyChanged();
                }
            }

            public void Deselect()
            {
                foreach (var Value in Values)
                {
                    Value.IsSelected = false;
                }
            }

            public IEnumerable<ValueViewModel> GetCorrespondingValues(params String[] values)
            {
                if (values == null || !values.Any())
                {
                    yield break;
                }

                var SelectValues = new HashSet<String>(values,
                    StringComparer.InvariantCultureIgnoreCase);

                foreach (var Value in Values.Where(x => SelectValues.Contains(x.GetOutValue())))
                {
                    yield return Value;
                }
            }

            public void SelectValues(params String[] values)
            {
                var SelectValues = GetCorrespondingValues(values);
                Deselect();

                if (SelectValues != null)
                {
                    if (IsMultiSelect)
                    {
                        foreach (var Value in SelectValues)
                        {
                            Value.IsSelected = true;
                        }
                    }
                    else
                    {
                        SelectedValue = SelectValues.FirstOrDefault();
                    }
                }
            }

            public void RaiseSelectedValuesChanged()
            {
                RaisePropertyChanged(nameof(DisplayValue));

                if (Owner != null)
                {
                    Owner.RaiseChanged();
                }
            }
        }

        private class TargetViewModel : ViewModelWithOwner<WindowViewModel>
        {
            private const Char HeaderMarker = '-';
            private const Char Assignment = '=';
            private const Char Scope = '"';
            private const Char Itemizer = '|';
            private const Char Separator = ' ';

            private const String HeaderName = "Target";

            private bool _IsSelected;

            public TargetViewModel(WindowViewModel owner)
                : base(owner)
            {
                Mode = new ComponentViewModel(this,
                    Owner.ConvertToDisplayableMode,
                    Owner.ConvertFromDisplayableMode,
                    owner.GetAvailableModes().ToArray());

                Platforms = new ComponentViewModel(this, null, null,
                    owner.GetAvailablePlatforms().ToArray())
                { IsMultiSelect = true };

                Configurations = new ComponentViewModel(this, null, null,
                    owner.GetAvailableConfigurations().ToArray())
                { IsMultiSelect = true };
            }

            public int? Id
            {
                get
                {
                    var Index = Owner?.Targets?.IndexOf(this);

                    if (Index.HasValue)
                    {
                        return Index.Value + 1;
                    }

                    return Index;
                }
            }

            public bool IsSelected
            {
                get
                {
                    return _IsSelected;
                }
                set
                {
                    _IsSelected = value;
                    RaisePropertyChanged();
                }
            }

            public ComponentViewModel Mode { get; private set; }
            public ComponentViewModel Platforms { get; private set; }
            public ComponentViewModel Configurations { get; private set; }

            private ComponentViewModel GetComponentFromValues(params String[] values)
            {
                if (Mode != null)
                {
                    if (Mode.GetCorrespondingValues(values).Any())
                    {
                        return Mode;
                    }
                }

                if (Platforms != null)
                {
                    if (Platforms.GetCorrespondingValues(values).Any())
                    {
                        return Platforms;
                    }
                }

                if (Configurations != null)
                {
                    if (Configurations.GetCorrespondingValues(values).Any())
                    {
                        return Configurations;
                    }
                }

                return null;
            }

            public void SetComponentValues(params String[] values)
            {
                var Component = GetComponentFromValues(values);

                if (Component != null)
                {
                    Component.SelectValues(values);
                }
            }

            public bool IsValid
            {
                get
                {
                    if (!Mode.IsValid)
                    {
                        return false;
                    }

                    if (!Platforms.IsValid)
                    {
                        return false;
                    }

                    if (!Configurations.IsValid)
                    {
                        return false;
                    }

                    return true;
                }
            }

            public void RaiseChanged()
            {
                if (Owner != null)
                {
                    Owner.RaiseChanged();
                }
            }

            public void RaiseIdChanged()
            {
                RaisePropertyChanged(nameof(Id));
            }

            public String Serialize(String command = null, 
                SerializeFlags flags = SerializeFlags.None)
            {
                var Builder = new StringBuilder();

                Builder.Append(HeaderMarker); // -
                Builder.Append(HeaderName); // Target
                Builder.Append(Id);

                Builder.Append(Assignment); // =

                Builder.Append(Scope); // "

                Builder.Append(command ?? SerializeCommand());

                Builder.Append(Scope); // "

                return Builder.ToString();
            }

            private String GetSerializedValues(ComponentViewModel component)
            {
                if (component == null)
                {
                    return String.Empty;
                }

                if (component.IsMultiSelect)
                {
                    return String.Join(Itemizer.ToString(),
                        component.GetSelectedValues().Select(x => x.GetOutValue()));
                }
                else
                {
                    return component.SelectedValue?.GetOutValue();
                }
            }

            public String SerializeCommand(SerializeFlags flags 
                = SerializeFlags.None)
            {
                var ModeValue = GetSerializedValues(Mode);
                var PlatformsValue = GetSerializedValues(Platforms);
                var ConfigurationsValue = GetSerializedValues(Configurations);

                var Builder = new StringBuilder();

                Builder.Append(ModeValue);
                Builder.Append(Separator);
                Builder.Append(PlatformsValue);
                Builder.Append(Separator);
                Builder.Append(ConfigurationsValue);

                if (flags.HasFlag(SerializeFlags.ForUnrealAutomationTool))
                {
                    if (Owner != null)
                    {
                        var VersionArgument = Owner.GetVersionArgument();

                        if (!String.IsNullOrEmpty(VersionArgument))
                        {
                            Builder.Append(Separator);
                            Builder.Append(VersionArgument);
                        }
                    }
                }

                return Builder.ToString();
            }

            private static IEnumerable<String> ParseScopedTargets(String input)
            {
                if (String.IsNullOrWhiteSpace(input))
                {
                    yield break;
                }

                var InScope = false;
                var Builder = new StringBuilder();

                foreach (var Current in input)
                {
                    if (Current == Scope)
                    {
                        InScope = !InScope;
                    }

                    if (Current == Separator && !InScope)
                    {
                        yield return Builder.ToString().Trim();
                        Builder.Clear();
                    }
                    else
                    {
                        Builder.Append(Current);
                    }
                }

                yield return Builder.ToString().Trim();
            }

            public static IEnumerable<TargetViewModel> Parse(WindowViewModel owner, String input)
            {
                var Targets = new List<TargetViewModel>();

                foreach (var ScopedTarget in ParseScopedTargets(input))
                {
                    var AssignmentIndex = ScopedTarget.IndexOf(Assignment);

                    if (AssignmentIndex < 0)
                    {
                        continue;
                    }

                    var Header = ScopedTarget.Substring(0, AssignmentIndex);

                    if (!Header.StartsWith(HeaderMarker.ToString()))
                    {
                        continue;
                    }

                    Header = Header.TrimStart(HeaderMarker);

                    if (!Header.StartsWith(HeaderName,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    var IndexValue = Header.Substring(HeaderName.Length);

                    int Index;
                    if (!int.TryParse(IndexValue, out Index))
                    {
                        continue;
                    }

                    var Target = new TargetViewModel(owner);

                    var Command = ScopedTarget.Substring(AssignmentIndex).TrimStart(Assignment).Trim(Scope);

                    foreach (var ComponentValue in Command.Split(Separator))
                    {
                        Target.SetComponentValues(ComponentValue.Split(Itemizer));
                    }

                    if (Index > Targets.Count)
                    {
                        Targets.Add(Target);
                    }
                    else
                    {
                        Targets.Insert(Index, Target);
                    }
                }

                foreach (var Target in Targets)
                {
                    yield return Target;
                }
            }
        }

        [Flags]
        public enum SerializeFlags
        {
            None = 0,
            ForUnrealAutomationTool = 1 << 0,
            ForVisualStudio = 1 << 1
        }

        private class WindowViewModel : ViewModel
        {
            private const int MaxTargets = 100;

            private DelegateCommand _AddNewCommand;
            private DelegateCommand _RemoveSelectedCommand;
            private DelegateCommand _BuildCommand;

            public WindowViewModel(Package package, String project)
            {
                _Package = package;
                _Project = project;

                Initialize();

                Targets = new ObservableCollection<TargetViewModel>();
                Targets.CollectionChanged += OnTargetsChanged;
            }

            private readonly Package _Package;
            private readonly String _Project;

            private readonly HashSet<String> _AvailableModes = new HashSet<String>(StringComparer.InvariantCultureIgnoreCase);
            private readonly HashSet<String> _AvailablePlatforms = new HashSet<String>(StringComparer.InvariantCultureIgnoreCase);
            private readonly HashSet<String> _AvailableConfigurations = new HashSet<String>(StringComparer.InvariantCultureIgnoreCase);

            public IEnumerable<String> GetAvailableModes()
            {
                foreach (var Mode in _AvailableModes)
                {
                    yield return Mode;
                }
            }

            public IEnumerable<String> GetAvailablePlatforms()
            {
                foreach (var Platform in _AvailablePlatforms.OrderByDescending(x => x))
                {
                    yield return Platform;
                }
            }

            public IEnumerable<String> GetAvailableConfigurations()
            {
                foreach (var Configuration in _AvailableConfigurations)
                {
                    yield return Configuration;
                }
            }

            public String GetVersionArgument()
            {
                var Version = _Package?.ActiveDTE2?.Version;

                switch (Version)
                {
                    case "11.0": return "-2012";
                    case "12.0": return "-2013";
                    case "14.0": return "-2015";
                    case "15.0": return "-2017";
                }

                return String.Empty;
            }

            private void Initialize()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _AvailableModes.Clear();
                _AvailableConfigurations.Clear();
                _AvailablePlatforms.Clear();

                SolutionBuild2 SolutionBuild = (SolutionBuild2)_Package.ActiveDTE2.Solution.SolutionBuild;

                foreach (SolutionConfiguration2 Configuration in SolutionBuild.SolutionConfigurations)
                {
                    var Components = Configuration.Name.Split(' ');

                    if (Components.Skip(1).Any())
                    {
                        _AvailableConfigurations.Add(Components.FirstOrDefault());
                        _AvailableModes.Add(Components.LastOrDefault());
                    }

                    _AvailablePlatforms.Add(Configuration.PlatformName);
                }
            }

            public String ConvertToDisplayableMode(String mode)
            {
                if (!String.IsNullOrEmpty(mode))
                {
                    if (mode.Equals("Client", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return "Game";
                    }
                }

                return mode;
            }

            public String ConvertFromDisplayableMode(String mode)
            {
                if (!String.IsNullOrEmpty(mode))
                {
                    if (!String.IsNullOrEmpty(_Project))
                    {
                        if (_Project.EndsWith(mode, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return _Project;
                        }
                        else
                        {
                            return $"{_Project}{mode}";
                        }
                    }
                }

                return mode;
            }

            public event Action OnBuildRequested = delegate { };

            private void OnTargetsChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                foreach (var Target in Targets)
                {
                    Target.RaiseIdChanged();
                }

                AddNewCommand.OnCanExecuteChanged();
                RemoveSelectedCommand.OnCanExecuteChanged();

                RaiseChanged();
            }

            public ObservableCollection<TargetViewModel> Targets { get; private set; }

            public void RaiseChanged()
            {
                BuildCommand.OnCanExecuteChanged();
            }

            public String Serialize(
                SerializeFlags flags = SerializeFlags.None)
            {
                var ProcessedCommands = new HashSet<String>(StringComparer.InvariantCultureIgnoreCase);
                var TargetCommands = new List<String>();

                foreach (var Target in Targets)
                {
                    var Command = Target.SerializeCommand(flags);

                    if (ProcessedCommands.Contains(Command))
                    {
                        continue;
                    }

                    ProcessedCommands.Add(Command);
                    TargetCommands.Add(Target.Serialize(Command, flags));
                }

                if (!TargetCommands.Any())
                {
                    return null;
                }

                return String.Join(" ", TargetCommands);
            }

            public DelegateCommand BuildCommand
            {
                get
                {
                    if (_BuildCommand == null)
                    {
                        _BuildCommand = new DelegateCommand(
                            (x) => Build(), (x) => CanBuild());
                    }

                    return _BuildCommand;
                }
            }

            public bool CanBuild()
            {
                if (!Targets.Any())
                {
                    return false;
                }

                if (Targets.Any(x => !x.IsValid))
                {
                    return false;
                }

                return true;
            }

            public DelegateCommand AddNewCommand
            {
                get
                {
                    if (_AddNewCommand == null)
                    {
                        _AddNewCommand = new DelegateCommand(
                            (x) => AddNew(), (x) => Targets.Count < MaxTargets);
                    }

                    return _AddNewCommand;
                }
            }

            public DelegateCommand RemoveSelectedCommand
            {
                get
                {
                    if (_RemoveSelectedCommand == null)
                    {
                        _RemoveSelectedCommand = new DelegateCommand(
                            (x) => RemoveSelected(), (x) => Targets.Any());
                    }

                    return _RemoveSelectedCommand;
                }
            }

            private void Build()
            {
                OnBuildRequested();
            }

            public void Deserialize(String command)
            {
                Targets.Clear();

                if (String.IsNullOrWhiteSpace(command))
                {
                    return;
                }

                foreach (var Target in TargetViewModel.Parse(this, command))
                {
                    Targets.Add(Target);
                }
            }

            private void RemoveSelected()
            {
                var SelectedTargets = Targets.Where(x => x.IsSelected).ToList();

                if (!SelectedTargets.Any())
                {
                    return;
                }

                var LastIndex = SelectedTargets.Max(x => x.Id);

                foreach (var Target in SelectedTargets)
                {
                    Targets.Remove(Target);
                }

                if (Targets.Any())
                {
                    TargetViewModel LastSelect = null;

                    if (LastIndex.HasValue)
                    {
                        LastSelect = Targets.ElementAtOrDefault(LastIndex.Value);
                    }

                    if (LastSelect == null)
                    {
                        LastSelect = Targets.LastOrDefault();
                    }

                    if (LastSelect != null)
                    {
                        LastSelect.IsSelected = true;
                    }
                }
            }

            private void AddNew()
            {
                foreach (var Target in Targets.Where(x => x.IsSelected))
                {
                    Target.IsSelected = false;
                }

                Targets.Add(new TargetViewModel(this) { IsSelected = true });
            }
        }

        #endregion
    }
}
