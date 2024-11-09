namespace Library

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

open NXUI.FSharp.Extensions
open Navs
open Navs.Avalonia

open Library.Views


module Routes =

  let build() =
      AvaloniaRouter([
        Route.define<Control>("home", "/", Home.view)
      ])


type App() =
  inherit Application()

  let router = Routes.build()
  let mainView = Main.Content router

  override this.Initialize() =
    this.Styles.Add(FluentTheme())

  override this.OnFrameworkInitializationCompleted() =
    match this.ApplicationLifetime with
    | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
      let window =
        Window()
          .width(420)
          .height(420)
          .content(mainView)

      desktopLifetime.MainWindow <- window
    | :? ISingleViewApplicationLifetime as singleViewLifetime ->
      singleViewLifetime.MainView <- mainView
    | _ -> ()

    base.OnFrameworkInitializationCompleted()
