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

module Router =

  let build (env: Env) =
    AvaloniaRouter(
      [
        Route.define("home", "/home", Home.view env) |> Route.cache NoCache
        Route.define("timelines", "/", Timelines.view())
      ]
    )


type App() =
  inherit Application()


  let env = Env.create()

  let router = Router.build env

  override this.Initialize() = this.Styles.Add(FluentTheme())

  override this.OnFrameworkInitializationCompleted() =
    match this.ApplicationLifetime with
    | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
      desktopLifetime.MainWindow <- Main.Window(router)
    | :? ISingleViewApplicationLifetime as singleViewLifetime ->
      singleViewLifetime.MainView <- Main.Content(router)
    | _ -> ()

    base.OnFrameworkInitializationCompleted()
