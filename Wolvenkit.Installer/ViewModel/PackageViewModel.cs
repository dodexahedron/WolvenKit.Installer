﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Wolvenkit.Installer.Helper;
using Wolvenkit.Installer.Models;
using Wolvenkit.Installer.Services;

namespace Wolvenkit.Installer.ViewModel;

public partial class PackageViewModel : ObservableObject
{
    private readonly PackageModel _model;
    private readonly ILibraryService _libraryService;
    private readonly INotificationService _notificationService;

    //private const int s_height = 85;
    //private const int s_expandedHeight = 140;

    public PackageViewModel(PackageModel model, EPackageStatus installed, string imagePath)
    {
        _libraryService = App.Current.Services.GetService<ILibraryService>();
        _notificationService = App.Current.Services.GetService<INotificationService>();

        _model = model;
        Status = installed;
        ImagePath = imagePath;

        //Height = s_expandedHeight;

        if (Status == EPackageStatus.UpdateAvailable)
        {
            IsUpdateAvailable = true;
            //Height = s_expandedHeight;
        }
    }

    public string Name => _model.Name;
    public string Version => _model.Version;
    public string Location => _model.Path;

    // LOCAL
    // Todo refactor with custom converter
    [ObservableProperty]
    private bool isUpdateAvailable;

    //[ObservableProperty]
    //private int height;

    public string ImagePath { get; }
    public EPackageStatus Status { get; set; }



    //public string DisplayName => $"{Name} | {Version}";


    internal PackageModel GetModel() => _model;


    [RelayCommand()]
    private void Open()
    {
        if (Directory.Exists(_model.Path))
        {
            Process.Start("explorer.exe", "\"" + _model.Path + "\"");
        }
    }

    [RelayCommand()]
    private async Task Debug()
    {
        _notificationService.StartIndeterminate();
        _notificationService.DisplayBanner("Uninstalling", $"Uninstalling {Name}. Please wait", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational);

        await Task.Delay(4000);

        _notificationService.CloseBanner();
        _notificationService.StopIndeterminate();
    }

    [RelayCommand()]
    private void Launch()
    {
        if (Directory.Exists(_model.Path))
        {
            if (_libraryService.TryGetRemote(_model, out var remote))
            {
                var exe = Path.Combine(_model.Path, remote.GetModel().Executable);
                if (File.Exists(exe))
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo(exe)
                        {
                            WorkingDirectory = Path.GetDirectoryName(exe)
                        };
                        Process.Start(startInfo);
                    }
                    catch (System.Exception)
                    {
                    }
                }
            }
        }
    }

    private bool IsProcessRunning()
    {
        var pname = Process.GetProcessesByName("WolvenKit");
        return pname is not null && pname.Length != 0;
    }

    private async Task<bool> UninstallModal()
    {
        if (IsProcessRunning())
        {
            var dlg = new ContentDialog()
            {
                XamlRoot = App.MainRoot.XamlRoot,
                Title = "Process running",
                Content = $"Please close WolvenKit before continuing",
                PrimaryButtonText = "Ok"
            };

            var r = await dlg.ShowAsync();

            return false;
        }

        if (Directory.Exists(_model.Path))
        {
            // managed packages need confirmation
            //if (_model.Files is null || _model.Files.Length == 0)
            if (!SettingsHelper.GetUseZipInstallers())
            {
                // find uninstaller
                var unInstaller = Directory.GetFiles(_model.Path, "unins000.exe").FirstOrDefault();
                if (unInstaller is not null && File.Exists(unInstaller))
                {
                    using var p = new Process();
                    p.StartInfo.FileName = unInstaller;
                    p.Start();
                    p.WaitForExit();
                }
                else
                {
                    // try deleting manually
                    if (!await RemoveFolder())
                    {
                        _notificationService.DisplayError($"Could not uninstall {_model.Name}. Delete {_model.Path} manually.");
                        return false;
                    }
                }
            }
            else
            {
                if (!await RemoveFolder())
                {
                    _notificationService.DisplayError($"Could not uninstall {_model.Name}. Delete {_model.Path} manually.");
                    return false;
                }
            }
        }

        return await _libraryService.RemoveAsync(_model);
    }

    private async Task<bool> RemoveFolder()
    {
        var dlg = new ContentDialog()
        {
            XamlRoot = App.MainRoot.XamlRoot,
            Title = "Remove",
            Content = $"Are you sure you want to delete this folder? Path: {_model.Path}",
            PrimaryButtonText = "Yes",
            CloseButtonText = "Cancel"
        };

        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
        {
            try
            {
                Directory.Delete(_model.Path, true);
                return true;
            }
            catch (Exception)
            {
                _notificationService.DisplayError($"Could not uninstall {_model.Name}. Delete {_model.Path} manually.");
                return false;
            }
        }
        return false;
    }

    [RelayCommand()]
    private async Task Uninstall()
    {
        _notificationService.StartIndeterminate();
        _notificationService.DisplayBanner("Uninstalling", $"Uninstalling {Name}. Please wait", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational);

        if (await UninstallModal())
        {
            _notificationService.CloseBanner();
        }

        _notificationService.StopIndeterminate();
    }

    [RelayCommand()]
    private async Task Update()
    {
        if (IsProcessRunning())
        {
            var dlg = new ContentDialog()
            {
                XamlRoot = App.MainRoot.XamlRoot,
                Title = "Process running",
                Content = $"Please close WolvenKit before continuing",
                PrimaryButtonText = "Ok"
            };

            var r = await dlg.ShowAsync();

            return;
        }

        _notificationService.StartIndeterminate();
        _notificationService.DisplayBanner("Updating", $"Updating {Name}. Please wait", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational);

        // install
        if (await UninstallModal())
        {
            await _libraryService.InstallAsync(_model, _model.Path);
            IsUpdateAvailable = false;
            //.Height = s_expandedHeight;

            _notificationService.CloseBanner();
        }

        _notificationService.StopIndeterminate();
    }
}

public enum EPackageStatus
{
    NotInstalled,
    Installed,
    UpdateAvailable,
}