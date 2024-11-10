namespace Library

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

open NXUI.FSharp.Extensions
open Navs
open Navs.Avalonia

open Library
open Library.Stores
open Library.Views

module Routes =

  let build (env: Env) =
    let hs = HomeStore.create(env)
    AvaloniaRouter([ Route.define<Control>("home", "/", Home.view hs) ])


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
