namespace Library

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

open NXUI.FSharp.Extensions
open Navs
open Navs.Avalonia

open Library.Env
open Library.Views

module Routes =

  let build (env: Env) =
    AvaloniaRouter([ Route.define<Control>("home", "/", Home.view env) ])


type App() =
  inherit Application()


  let env = Env.create()

  let router = Routes.build env

  override this.Initialize() = this.Styles.Add(FluentTheme())

  override this.OnFrameworkInitializationCompleted() =
    match this.ApplicationLifetime with
    | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
      desktopLifetime.MainWindow <- Main.Window(router)
    | :? ISingleViewApplicationLifetime as singleViewLifetime ->
      singleViewLifetime.MainView <- Main.Content(router)
    | _ -> ()

    base.OnFrameworkInitializationCompleted()
