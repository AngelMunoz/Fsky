namespace Project.Android

open Android.App
open Android.Content.PM
open Avalonia
open Avalonia.ReactiveUI
open Avalonia.Android

open Library

[<Activity(Label = "Project.Android",
           Theme = "@style/MyTheme.NoActionBar",
           Icon = "@drawable/icon",
           MainLauncher = true,
           ConfigurationChanges =
             (ConfigChanges.Orientation
              ||| ConfigChanges.ScreenSize
              ||| ConfigChanges.UiMode))>]
type MainActivity() =
  inherit AvaloniaMainActivity<Application>()

  override _.CustomizeAppBuilder _ =
    AppBuilder
      .Configure<Application>(fun _ -> App())
      .UseAndroid()
      .UseReactiveUI()
      .WithInterFont()
      .UseReactiveUI()
