﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NuGet.Client;
using NuGet.Dialog.PackageManagerUI;
using NuGet.VisualStudio;

namespace NuGet.Tools
{
    /// <summary>
    /// Interaction logic for PackageDetail.xaml
    /// </summary>
    public partial class PackageDetail : UserControl
    {
        public PackageManagerControl Control { get; set; }
        public PackageManagerSession Session { get { return Control.Session; } }

        private enum Metadatas
        {
            Authors,
            Owners,
            License,
            Donwloads,
            DatePublished,
            ProjectInformation,
            Tags
        }

        // item in the dependency installation behavior list view
        private class DependencyBehaviorItem
        {
            public string Text
            {
                get;
                private set;
            }

            public DependencyBehavior Behavior
            {
                get;
                private set;
            }

            public DependencyBehaviorItem(string text, DependencyBehavior dependencyBehavior)
            {
                Text = text;
                Behavior = dependencyBehavior;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        private static DependencyBehaviorItem[] _dependencyBehaviors = new[] {
            new DependencyBehaviorItem("Ignore Dependencies", DependencyBehavior.Ignore),
            new DependencyBehaviorItem("Lowest", DependencyBehavior.Lowest),
            new DependencyBehaviorItem("HighestPath", DependencyBehavior.HighestPatch),
            new DependencyBehaviorItem("HighestMinor", DependencyBehavior.HighestMinor),
            new DependencyBehaviorItem("Highest", DependencyBehavior.Highest),
        };

        public PackageDetail()
        {
            InitializeComponent();
            this.DataContextChanged += PackageDetail_DataContextChanged;

            this.Visibility = System.Windows.Visibility.Collapsed;

            _dependencyBehavior.Items.Clear();
            foreach (var d in _dependencyBehaviors)
            {
                _dependencyBehavior.Items.Add(d);
            }
            _dependencyBehavior.SelectedItem = _dependencyBehaviors[1];

            foreach (var v in Enum.GetValues(typeof(FileConflictResolution)))
            {
                _fileConflictAction.Items.Add(v);
            }
            _fileConflictAction.SelectedItem = FileConflictResolution.Overwrite;
        }

        private void PackageDetail_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.Visibility = DataContext is PackageDetailControlModel ?
                System.Windows.Visibility.Visible :
                System.Windows.Visibility.Collapsed;
        }

        private void Versions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var model = (PackageDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            var v = (VersionForDisplay)_versions.SelectedItem;            
            model.SelectVersion(v == null ? null : v.Version);
            UpdateInstallUninstallButton();
        }

        private async void Preview(PackageActionType action)
        {
            var actions = await ResolveActions(action);

            PreviewActions(actions);
        }

        private async Task<IEnumerable<PackageActionDescription>> ResolveActions(PackageActionType action)
        {
            var package = (PackageDetailControlModel)DataContext;
            Control.SetBusy(true);
            var actions = await Session.CreateActionResolver().ResolveActions(
                action,
                new PackageName(package.Package.Id, package.Package.Version),
                new ResolverContext()
                {
                    DependencyBehavior = ((DependencyBehaviorItem)_dependencyBehavior.SelectedItem).Behavior,
                    AllowPrerelease = false
                });
            Control.SetBusy(false);
            return actions;
        }

        private void PreviewActions(
            IEnumerable<PackageActionDescription> actions)
        {
            // Show result
            // values:
            // 1: unchanged
            // 0: deleted
            // 2: added
            var packageStatus = Session
                .GetInstalledPackageList()
                .GetInstalledPackages()
                .ToDictionary(p => /* key */ p, _ => /* value */ 1);

            foreach (var action in actions)
            {
                if (action.ActionType == PackageActionType.Install)
                {
                    packageStatus[action.Package] = 2;
                }
                else if (action.ActionType == PackageActionType.Uninstall)
                {
                    packageStatus[action.Package] = 0;
                }
            }

            var w = new PreviewWindow(
                unchanged: packageStatus.Where(v => v.Value == 1).Select(v => v.Key),
                deleted: packageStatus.Where(v => v.Value == 0).Select(v => v.Key),
                added: packageStatus.Where(v => v.Value == 2).Select(v => v.Key));
            w.Owner = Window.GetWindow(Control);
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            w.ShowDialog();
        }

        private async void PerformPackageAction(PackageActionType action)
        {
            var actions = await ResolveActions(action);

            // show license agreeement
            bool acceptLicense = ShowLicenseAgreement(actions);
            if (!acceptLicense)
            {
                return;
            }

            await Session.CreateActionExecutor().ExecuteActions(actions);

            Control.UpdatePackageStatus();
            UpdatePackageStatus();
        }

        private void UpdateInstallUninstallButton()
        {
            var model = (PackageDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            var isInstalled = Session.GetInstalledPackageList().IsInstalled(model.Package.Id, model.Package.Version);
            if (isInstalled)
            {
                _dropdownButton.SetItems(
                    new[] { "Uninstall", "Uninstall Preview" });
            }
            else
            {
                _dropdownButton.SetItems(
                    new[] { "Install", "Install Preview" });
            }
        }

        private void UpdatePackageStatus()
        {
            var model = (PackageDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            UpdateInstallUninstallButton();
            var installedVersion = Session.GetInstalledPackageList().GetInstalledVersion(model.Package.Id);
            model.CreateVersions(installedVersion);
        }

        protected bool ShowLicenseAgreement(IEnumerable<PackageActionDescription> operations)
        {
            var licensePackages = operations.Where(op => op.ActionType == PackageActionType.AcceptLicense);

            // display license window if necessary
            if (licensePackages.Any())
            {
                var result = MessageBox.Show(
                    "Accept Licenses? TODO: Show proper license dialog!",
                    "NuGet",
                    MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                {
                    return false;
                }
                //IUserNotifierServices uss = new UserNotifierServices();
                //bool accepted = uss.ShowLicenseWindow(
                //    licensePackages.Distinct<IPackage>(PackageEqualityComparer.IdAndVersion));
                //if (!accepted)
                //{
                //    return false;
                //}
            }

            return true;
        }

        private void ExecuteOpenLicenseLink(object sender, ExecutedRoutedEventArgs e)
        {
            Hyperlink hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink != null && hyperlink.NavigateUri != null)
            {
                UriHelper.OpenExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        private void _dropdownButton_Clicked(object sender, DropdownButtonClickEventArgs e)
        {
            switch (e.ButtonText)
            {
                case "Install":
                    PerformPackageAction(PackageActionType.Install);
                    break;

                case "Install Preview":
                    Preview(PackageActionType.Install);
                    break;

                case "Uninstall":
                    PerformPackageAction(PackageActionType.Uninstall);
                    break;

                case "Uninstall Preview":
                    Preview(PackageActionType.Uninstall);
                    break;
            }
        }
    }
}